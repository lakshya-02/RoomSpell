# RoomSpell

A Quest 3 / Quest 3S mixed reality word game. Look at your surroundings, select three related objects, and arrange letter tiles to make a new word inspired by them. Solving a round unlocks another answer from the same objects.

## Run

Open the project in **Unity 6000.3.19f1** and open `Assets/Scenes/RoomSpell.unity`.

On Quest, point a controller ray at a button and press the trigger, or use the Meta hand interactions. Tap **Scan room**, allow the camera permission, and look steadily at related objects. Tap three detected object buttons, then **Make my puzzle**. Tap letters in order. **Clear letters** retries an incorrect answer; **Another word** becomes available after solving. **Recenter board** moves the board in front of you.

In the Editor without a headset, mouse input and **Dummy scenarios** let you play the complete puzzle loop. The button cycles through study (cup, pen, notebook), kitchen (cup, bowl, spoon) and an unrelated set. Each supplies two batches through the same confirmation filter used by the detector. **DUMMY DATA** stays visible and does not claim neural recognition. Dummy input is compiled only in the Editor and development builds.

## Run XR Simulator from the terminal

From the project directory in PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run-roomspell-validation.ps1
```

The default command runs the dotnet checks, starts an owned GameRoom synthetic environment, starts Unity using Meta XR Simulator v207, runs real GPU model fixtures and the full dummy puzzle scenarios, overrides the simulated head pose through Meta XR Operator, probes the synthetic camera, reruns the original model/play-flow checks, then builds the Android APK. Use `-SkipApk` to omit rebuilding an already verified APK during iteration.

Unity 6000.3.19f1, its Android SDK/NDK/JDK, Meta XR Simulator v207, .NET 9 and Unity CLI must be installed. Unity Pipeline is included in the project. The desktop must have an interactive graphics session. Close this project's Editor before starting; the runner owns and cleans up its processes. It sets the runtime only for the child Unity process and never changes the machine-wide OpenXR registry.

Results, logs, runtime configuration, inference timings, startup time, frame time, allocated memory and screenshots are saved to a timestamped `Validation/Simulator/` directory. `results.json` records assertion outcomes and camera diagnostics. Selection and solved-state captures render the application board; `synthetic-camera.png`, when available, contains the camera's synthetic input. `composited-view.png` captures the actual board over the synthetic room through Meta XR Operator. Desktop measurements do not predict Quest performance.

The input validation covers runtime head tracking, recentering, active ray/poke components and public board callbacks. It does not claim automated controller-trigger or continuous finger-gesture coverage. The board displays **SIMULATOR CAMERA** in the Editor and **QUEST CAMERA** on the headset.

Use **RoomSpell > Build Quest APK** to create `Builds/RoomSpell.apk`. The project targets Android ARM64 with IL2CPP, Vulkan and OpenXR. The generated manifest declares passthrough and `horizonos.permission.HEADSET_CAMERA`.

## Detection pipeline

| Part | Implementation |
| --- | --- |
| Camera frames | Meta MR Utility Kit `PassthroughCameraAccess`, left camera, requested 1280 × 960 at 30 FPS |
| Inference runtime | `com.unity.ai.inference` 2.6.1 (Unity Inference Engine, formerly Sentis) |
| Provider | Meta `UnityInferenceEngineProvider`, GPUCompute, execution split across frames |
| Model | Meta AI Building Blocks default `yolo12nNoNMS.sentis`, with the matching COCO label file |
| Detection cadence | At most one inference at a time, requested every 0.5 seconds while scanning |
| Confirmation | Confidence of at least 0.5 and recognition in two consecutive inference passes; duplicate boxes count once |
| Interaction | Meta XR Interaction SDK comprehensive rig, ray and poke canvas interactions |
| Game UI | TextMesh Pro on a world-space canvas, placed once in front of the player |

Camera processing begins when the player requests a scan. Frames are processed locally and are not uploaded or saved. Inference stops during puzzles and when the application pauses. Camera startup and model errors appear on the board.

## Word generation and object coverage

Object recognition uses the neural model. **Word challenges currently use a curated offline relation bank**, rather than an LLM. There are several answers per object theme, and solved answers do not repeat in the session. An unrelated set of three objects is rejected instead of producing a misleading clue.

Cup + pen + notebook can produce STUDY, LEARN, FOCUS and CREATE. Other supported themes include kitchen, relaxation, travel and sport. The answer cannot be one of the selected object names.

**The COCO detector does not have a pen class or a separate notebook class.** It detects cups and books. The board provides explicitly labelled manual pen/notebook additions so that the intended desk round is playable. Automatic recognition of all three target objects still needs a model trained for those labels; the provider asset accepts a replacement model and matching label file. Current detections are listed on the board; object-aligned 3D bounding boxes are not implemented.

## Checks and rebuilds

Run `dotnet run --project Tests/PuzzleRoundChecks.csproj` for the puzzle, word-bank and detection-filter checks.

The editor verification entry point is `RoomSpellVerification.Run`. It checks scene wiring, runs the real neural model on an image fixture, and plays through a puzzle before verifying that the next answer is different. Reports are written to `Validation/`, with a rendered board image at `Preview.png`.

These checks passed on 11 October 2026: the supplied model detected a bus at 0.91 confidence, the saved scene had its camera/model/interaction references and Android OpenXR loader, and the Editor game solved a round and produced a different next answer. Desktop tests do not verify live Quest camera frames, GPU inference performance or hand/controller input on the headset.

`RoomSpellQuestSetup.Configure` reconstructs the starter Quest scene and applies Meta's project setup fixes. It overwrites `Assets/Scenes/RoomSpell.unity`; use it only when intentionally resetting the scene setup. It imports TMP resources, configures the model provider and interaction rig, enables OpenXR features, and regenerates the Android manifest through Meta's manifest tool.

## Verified references

The implementation was checked against Meta documentation and the installed SDK source on 11 October 2026:

- [Unity Inference Engine and Meta's provider](https://developers.meta.com/vr/documentation/unity/unity-ai-unity-inference-engine/)
- [Passthrough Camera API](https://developers.meta.com/horizon/documentation/unity/unity-pca-overview/)
- [Meta's multi-object detection sample](https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples/tree/main/Assets/PassthroughCameraApiSamples/MultiObjectDetection)
- [Meta XR project setup](https://developers.meta.com/horizon/documentation/unity/unity-project-setup/)

Meta XR Core, MR Utility Kit, Interaction Essentials, and Interaction OVR packages are pinned to 207.0.0. See `THIRD_PARTY_NOTICES.md` for model and test-image provenance.

The Meta version has no blockchain. This development build is not a competition submission or a store-ready release; device verification, the target-object model and submission-specific checks remain necessary.

## ElevenLabs follow-up

Voice is deferred until simulator validation is stable. The next voice milestone should generate narration for instructions, clues and solve/retry feedback ahead of time and bundle the clips for offline playback. A terminal generation tool will read `ELEVENLABS_API_KEY` from the user environment. The key must never be placed in Unity assets, project source or an APK. No key or ElevenLabs dependency is required for the current tests.

See [.agent-docs](.agent-docs/index.md) for the project map and [validation status](Validation/REPORT.md) for the latest verified capabilities.

## Source checkout and local-only files

The GitHub repository contains Unity source, scene/assets, pinned package manifests, test fixtures and tooling. Open with Unity 6000.3.19f1 and let Package Manager restore dependencies. Run `dotnet run --project Tests/PuzzleRoundChecks.csproj` to check the pure game logic.

Builds, validation captures/logs, caches and developer credentials stay local. The validation links above refer to locally generated results and are not included in a fresh source checkout. Meta developer-agent and platform-tool settings are also excluded; configure those locally if needed. No cloud API key is required for the current game. Never store service keys in Unity Resources, source, an APK or a commit. Future cloud generation must keep provider credentials on a server.
