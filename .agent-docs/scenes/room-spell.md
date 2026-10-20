# Scenes

Last updated: 2026-10-13

`Assets/Scenes/RoomSpell.unity` is the only enabled build scene. It contains:

- OVRCameraRig: OVRManager with floor tracking and passthrough, center/left/right cameras.
- Comprehensive Meta interaction rig: nested hand/controller input, rays, pokes and rig references.
- UI input: EventSystem, Meta PointableCanvasModule and disabled desktop InputSystemUIInputModule.
- Word puzzle board: world-space Canvas, scaler, raycaster, RoomSpellBoard, ray/poke canvas templates.
- Room camera: initially disabled PassthroughCameraAccess; scan explicitly enables it.
- RoomSpell: game and detection controllers, assigned board/model/camera references.

There are no scene transitions. The board constructs its controls in Awake and moves once in front of the head after startup. Recenter moves it again.

`SampleScene.unity` is the preserved URP template. `Assets/_Recovery/0.unity` is a preserved recovery file. Neither belongs to the build or runtime flow.

`RoomSpellQuestSetup.Configure` rebuilds the scene and applies settings; it intentionally overwrites the starter scene. Ordinary validation opens the scene and does not reconstruct it.
