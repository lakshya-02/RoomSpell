using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Meta.XR.BuildingBlocks.AIBlocks;
using Oculus.Interaction;
using RoomSpell;
using TMPro;
using Unity.InferenceEngine;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.Profiling;

[InitializeOnLoad]
public static class RoomSpellSimulatorValidation
{
    const string Pending = "RoomSpell.SimulatorValidation";
    static string Output => Environment.GetEnvironmentVariable("ROOMSPELL_VALIDATION_DIR");
    [Serializable] public class Check { public string name; public bool passed; public string detail; }
    [Serializable] public class Result
    {
        public bool passed;
        public string unityVersion, xrRuntime, cameraStatus, inputTest = "Gameplay callbacks and XR rig state; physical gestures are not validated.";
        public string[] cameraLabels;
        public double startupMilliseconds, warmupMilliseconds, averageFrameMilliseconds, memoryBytes;
        public List<Check> checks = new List<Check>();
        public List<Check> inference = new List<Check>();
        public List<string> errors = new List<string>();
        public List<string> editorDiagnostics = new List<string>();
    }
    static Result result;
    static double frameSum;
    static int frames;
    static bool finishing;

    static RoomSpellSimulatorValidation()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            {
                SessionState.SetBool(Pending, false);
                EditorApplication.delayCall += PlayChecks;
            }
        };
    }

    [CliCommand("roomspell_validate", "Run RoomSpell XR Simulator and fixture validation")]
    public static string StartValidation() { Run(); return "Validation started; see results.json in ROOMSPELL_VALIDATION_DIR."; }

    public static void Run()
    {
        try
        {
            if (string.IsNullOrEmpty(Output)) throw new InvalidOperationException("ROOMSPELL_VALIDATION_DIR is required.");
            Directory.CreateDirectory(Output);
            EditorSceneManager.OpenScene("Assets/Scenes/RoomSpell.unity");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/RoomSpell.unity", true) };
            AssetDatabase.SaveAssets();
            result = new Result { unityVersion = Application.unityVersion };
            CheckThat(Application.unityVersion == "6000.3.19f1", "Unity version", Application.unityVersion);
            var game = UnityEngine.Object.FindAnyObjectByType<RoomSpellGame>();
            CheckThat(game && game.Board && game.Detection && game.Detection.Provider && game.Detection.CameraAccess,
                "Scene dependencies", "Board, camera and real neural model assigned");
            var missing = game.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(child => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0).Select(child => child.name).ToArray();
            CheckThat(missing.Length == 0, "No missing scene scripts", string.Join(", ", missing));
            File.WriteAllText(Path.Combine(Output, "fixture-results.json"), JsonUtility.ToJson(result, true));
            SessionState.SetBool(Pending, true);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex) { Fail(ex); }
    }

    static async Task FixtureChecks(UnityInferenceEngineProvider original)
    {
        var provider = UnityEngine.Object.Instantiate(original);
        try
        {
            var settings = new SerializedObject(provider);
            settings.FindProperty("backend").intValue = (int)BackendType.GPUCompute;
            settings.FindProperty("splitOverFrames").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            await provider.WarmUp();
            result.warmupMilliseconds = timer.Elapsed.TotalMilliseconds;
            var blank = new Texture2D(640, 640, TextureFormat.RGB24, false);
            var pixels = Enumerable.Repeat(new Color32(128, 128, 128, 255), 640 * 640).ToArray();
            blank.SetPixels32(pixels); blank.Apply();
            var negativePath = "Tests/Fixtures/empty.png";
            if (!File.Exists(negativePath)) File.WriteAllBytes(negativePath, blank.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(blank);
            foreach (var file in new[] { "bus.jpg", "room.jpg", "empty.png" })
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                try
                {
                    CheckThat(texture.LoadImage(File.ReadAllBytes("Tests/Fixtures/" + file)), "Load fixture " + file, "Image decoded");
                    timer.Restart();
                    var predictions = await provider.DetectAsync(texture);
                    var elapsed = timer.Elapsed.TotalMilliseconds;
                    var labels = predictions.Where(p => p.score >= 0.5f).Select(p => p.label).Distinct().ToArray();
                    var detail = $"{elapsed:0.0} ms; " + string.Join(", ", predictions.Select(p => $"{p.label} {p.score:0.00}"));
                    result.inference.Add(new Check { name = file, passed = true, detail = detail });
                    if (file == "bus.jpg") CheckThat(labels.Contains("bus"), "Positive neural fixture", detail);
                    if (file == "room.jpg") CheckThat(labels.Count(l => new[] { "chair", "potted plant", "clock", "book", "cup", "couch", "tv", "remote" }.Contains(l)) >= 2,
                        "Multiple related room objects", detail);
                    if (file == "room.jpg")
                    {
                        var filter = new DetectionCandidates();
                        var batch = predictions.Select(p => new ObjectObservation(p.label, p.score)).ToArray();
                        filter.Update(batch);
                        var confirmed = filter.Update(batch);
                        CheckThat(confirmed.Any(p => p.Label == "tv monitor"), "Model labels reach candidates", "The provider's TV class is supported by the word bank");
                        CheckThat(WordCatalog.TryNext(confirmed.Select(p => p.Label).Take(3), new HashSet<string>(), 0, out _),
                            "Neural fixture creates related words", string.Join(", ", confirmed.Select(p => p.Label)));
                    }
                    if (file == "empty.png") CheckThat(labels.Length == 0, "Negative neural fixture", detail);
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(provider); }
    }

    static async void PlayChecks()
    {
        finishing = false;
        try
        {
            result = JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(Output, "fixture-results.json")));
            Application.logMessageReceived += OnLog;
            EditorApplication.update += SampleFrame;
            Application.runInBackground = true;
            await Until(() =>
            {
                var available = new List<XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(available);
                return available.Any(d => d.running);
            }, 30, "XR display did not become active.");
            await Task.Delay(1500);
            var displays = new List<XRDisplaySubsystem>(); SubsystemManager.GetSubsystems(displays);
            result.xrRuntime = OpenXRRuntime.name + " " + OpenXRRuntime.version;
            CheckThat(displays.Any(x => x.running) && OpenXRRuntime.name.Contains("Simulator"), "Running XR Simulator", result.xrRuntime);
            result.startupMilliseconds = (DateTime.UtcNow - DateTime.Parse(Environment.GetEnvironmentVariable("ROOMSPELL_STARTED_UTC"), null,
                System.Globalization.DateTimeStyles.RoundtripKind)).TotalMilliseconds;
            var game = UnityEngine.Object.FindAnyObjectByType<RoomSpellGame>();
            await FixtureChecks(game.Detection.Provider);
            File.WriteAllText(Path.Combine(Output, "model-results.json"), JsonUtility.ToJson(result, true));
            CheckThat(game.InteractionRig.activeInHierarchy && EventSystem.current.GetComponent<PointableCanvasModule>().enabled,
                "Meta UI input active", "Desktop mouse fallback is disabled in XR");
            CheckThat(game.InteractionRig.GetComponentsInChildren<RayInteractor>(true).Any(x => x.enabled && x.gameObject.activeInHierarchy),
                "Ray interaction rig", "Enabled ray interactors");
            CheckThat(game.InteractionRig.GetComponentsInChildren<PokeInteractor>(true).Any(x => x.enabled && x.gameObject.activeInHierarchy),
                "Poke interaction rig", "Enabled poke interactors");
            var origin = game.Board.View.position;
            File.WriteAllText(Path.Combine(Output, "pose-ready.txt"), JsonUtility.ToJson(new PoseRecord { position = origin, rotation = game.Board.View.rotation }));
            await Until(() => Vector3.Distance(game.Board.View.position, origin) > 0.1f, 45, "Simulator head override did not reach the camera rig.");
            game.Board.PlaceInFront();
            var expected = game.Board.View.position + Vector3.ProjectOnPlane(game.Board.View.forward, Vector3.up).normalized * 1.4f - Vector3.up * 0.08f;
            CheckThat(Vector3.Distance(game.Board.transform.position, expected) < 0.02f, "Tracking and recenter", "Runtime pose changes reached the board");
            File.WriteAllText(Path.Combine(Output, "pose-complete.txt"), "passed");
            for (var scenario = 0; scenario < 3; scenario++)
            {
                game.StartDummyScenario(scenario);
                CheckThat(game.Detection.LastCandidates.Length == 0, "First dummy frame " + scenario, "One frame is not confirmed");
                await Task.Delay(750);
                var labels = DummyScenarios.Labels(scenario);
                CheckThat(game.Detection.LastCandidates.Length == 3, "Confirmed dummy frame " + scenario, string.Join(", ", labels));
                foreach (var label in labels) game.Board.ObjectSelected(label);
                RoomSpellVerification.CaptureBoard(game.Board, Path.Combine(Output, $"selection-{scenario}.png"));
                game.Board.MakeRequested();
                if (scenario == 2)
                {
                    CheckThat(game.CurrentRound == null, "Unrelated dummy rejection", "No puzzle generated");
                    continue;
                }
                var round = game.CurrentRound;
                CheckThat(round != null, "Related dummy puzzle " + scenario, round?.Answer);
                var scoreBefore = game.Score;
                for (var i = 0; i < round.Tiles.Length; i++) game.Board.TileSelected(i);
                CheckThat(round.IsFull && !round.IsSolved && game.Score == scoreBefore, "Incorrect letters " + scenario, "Scrambled order cannot score");
                game.Board.ClearRequested();
                CheckThat(!round.IsFull && round.FormedWord.All(c => c == '_'), "Clear letters " + scenario, "Slots reset");
                Solve(game, round);
                CheckThat(round.IsSolved && game.Score == scoreBefore + 100, "Solve and score " + scenario, round.Answer);
                RoomSpellVerification.CaptureBoard(game.Board, Path.Combine(Output, $"solved-{scenario}.png"));
                game.Board.NextRequested();
                CheckThat(game.CurrentRound != null && game.CurrentRound.Answer != round.Answer, "Different next word " + scenario, game.CurrentRound?.Answer);
            }
            var observations = new List<string>();
            game.Detection.InferenceCompleted += items => observations.AddRange(items.Select(x => $"{x.Label} {x.Confidence:0.00}"));
            game.Board.ScanRequested();
            await Until(() => game.Detection.InferenceCount >= 2 || !game.Detection.IsScanning, 45, "Synthetic camera pipeline exceeded 45 seconds without a diagnosis.");
            result.cameraStatus = game.Detection.InferenceCount >= 2 ? "Two synthetic camera inference passes completed." : game.Detection.LastStatus;
            result.cameraLabels = observations.Distinct().ToArray();
            CheckThat(game.Detection.InferenceCount >= 2 || !string.IsNullOrEmpty(result.cameraStatus), "Synthetic camera diagnosis", result.cameraStatus);
            if (game.Detection.CameraAccess.IsPlaying)
            {
                var texture = game.Detection.CameraAccess.GetTexture();
                var rt = RenderTexture.GetTemporary(texture.width, texture.height);
                Graphics.Blit(texture, rt);
                var previous = RenderTexture.active; RenderTexture.active = rt;
                var copy = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                copy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); copy.Apply();
                File.WriteAllBytes(Path.Combine(Output, "synthetic-camera.png"), copy.EncodeToPNG());
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); UnityEngine.Object.Destroy(copy);
            }
            game.Detection.StopScanning();
            await Until(() => !game.Detection.IsBusy, 10, "In-flight camera inference did not finish after stopping.");
            CheckThat(!game.Detection.CameraAccess.enabled, "Stop camera safely", "Capture released after the pending GPU pass");
            File.WriteAllText(Path.Combine(Output, "capture-ready.txt"), "ready");
            await Until(() => File.Exists(Path.Combine(Output, "capture-complete.txt")), 30, "Composited XR screenshot was not captured.");
            await Task.Delay(300);
            Finish();
        }
        catch (Exception ex) { Fail(ex); }
    }

    [Serializable] class PoseRecord { public Vector3 position; public Quaternion rotation; }
    static void Solve(RoomSpellGame game, PuzzleRound round)
    {
        var used = new HashSet<int>();
        foreach (var letter in round.Answer)
        {
            var tile = Enumerable.Range(0, round.Tiles.Length).First(i => !used.Contains(i) && round.Tiles[i] == letter);
            used.Add(tile); game.Board.TileSelected(tile);
        }
    }
    static async Task Until(Func<bool> condition, double seconds, string failure)
    {
        var deadline = EditorApplication.timeSinceStartup + seconds;
        while (!condition())
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException(failure);
            await Task.Delay(100);
        }
    }
    static void CheckThat(bool passed, string name, string detail)
    {
        result.checks.Add(new Check { name = name, passed = passed, detail = detail });
        if (!passed) throw new InvalidOperationException(name + ": " + detail);
    }
    static void OnLog(string message, string stack, LogType type)
    {
        if (message.StartsWith("[Package Manager Window] Error while getting access token:"))
        {
            result.editorDiagnostics.Add(message); return;
        }
        if (!finishing && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)) result.errors.Add(message);
    }
    static int sampledFrame;
    static void SampleFrame()
    {
        if (!EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (sampledFrame == Time.frameCount) return;
        sampledFrame = Time.frameCount; frameSum += Time.unscaledDeltaTime * 1000; frames++;
    }
    static void Fail(Exception ex)
    {
        if (result == null) result = new Result();
        result.errors.Add(ex.ToString());
        Debug.LogException(ex); Finish();
    }
    static void Finish()
    {
        finishing = true;
        SessionState.SetBool(Pending, false);
        EditorApplication.update -= SampleFrame;
        Application.logMessageReceived -= OnLog;
        result.averageFrameMilliseconds = frames == 0 ? 0 : frameSum / frames;
        result.memoryBytes = Profiler.GetTotalAllocatedMemoryLong();
        result.passed = result.errors.Count == 0 && result.checks.All(x => x.passed);
        if (!string.IsNullOrEmpty(Output)) File.WriteAllText(Path.Combine(Output, "results.json"), JsonUtility.ToJson(result, true));
        Debug.Log(result.passed ? "ROOMSPELL_SIMULATOR_PASSED" : "ROOMSPELL_SIMULATOR_FAILED");
        EditorApplication.Exit(result.passed ? 0 : 1);
    }
}
