using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Meta.XR.BuildingBlocks.AIBlocks;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using RoomSpell;
using TMPro;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class RoomSpellVerification
{
    const string PendingKey = "RoomSpell.VerifyPlayFlow";
    static double playAt;

    static RoomSpellVerification()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingKey, false)) return;
            playAt = EditorApplication.timeSinceStartup + 1;
            EditorApplication.update += CheckPlayFlow;
        };
    }

    public static async void Run()
    {
        UnityInferenceEngineProvider provider = null;
        Texture2D image = null;
        try
        {
            Directory.CreateDirectory("Validation");
            EditorSceneManager.OpenScene("Assets/Scenes/RoomSpell.unity");
            var game = UnityEngine.Object.FindAnyObjectByType<RoomSpellGame>();
            Require(game && game.Detection && game.Board && game.InteractionRig, "Game scene dependencies");
            Require(game.Detection.Provider && game.Detection.CameraAccess, "Model and camera references");
            Require(!game.Detection.CameraAccess.enabled, "Camera waits for explicit scan");
            Require(game.Board.Font && game.Board.GetComponent<Canvas>().renderMode == RenderMode.WorldSpace, "Readable world-space UI");
            Require(UnityEngine.Object.FindObjectsByType<RayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length > 0,
                "Meta ray interactors are present");
            foreach (var reference in game.InteractionRig.GetComponentsInChildren<OVRCameraRigRef>(true))
                Require(reference.CameraRig && reference.LeftHand && reference.RightHand, "Meta interaction rig references");
            foreach (var root in game.gameObject.scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "No missing scripts");
            var xr = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            Require(xr && xr.Manager.activeLoaders.Any(x => x is OpenXRLoader), "Android OpenXR loader enabled");

            provider = UnityEngine.Object.Instantiate(game.Detection.Provider);
            var settings = new SerializedObject(provider);
            settings.FindProperty("backend").intValue = (int)BackendType.CPU;
            settings.FindProperty("splitOverFrames").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            image = new Texture2D(2, 2, TextureFormat.RGB24, false);
            Require(image.LoadImage(File.ReadAllBytes("Tests/Fixtures/bus.jpg")), "Inference image fixture loaded");
            await provider.WarmUp();
            var predictions = await provider.DetectAsync(image);
            Require(predictions.Any(x => x.label == "bus" && x.score >= 0.5f), "Real model detects bus in fixture");
            var modelResult = "Model inference passed: " + string.Join(", ", predictions.Select(x => $"{x.label} {x.score:0.00}"));
            File.WriteAllText("Validation/model-and-scene.txt", modelResult + "\nScene dependencies and Android OpenXR checks passed.\n");
            Debug.Log("ROOMSPELL_MODEL_AND_SCENE_PASSED: " + modelResult);
            UnityEngine.Object.DestroyImmediate(provider);
            provider = null;
            UnityEngine.Object.DestroyImmediate(image);
            image = null;
            SessionState.SetBool(PendingKey, true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception ex)
        {
            if (provider) UnityEngine.Object.DestroyImmediate(provider);
            if (image) UnityEngine.Object.DestroyImmediate(image);
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    static void CheckPlayFlow()
    {
        if (EditorApplication.timeSinceStartup < playAt) return;
        EditorApplication.update -= CheckPlayFlow;
        SessionState.SetBool(PendingKey, false);
        try
        {
            var game = UnityEngine.Object.FindAnyObjectByType<RoomSpellGame>();
            var board = game.Board;
            board.DemoRequested();
            board.ObjectSelected("cup");
            board.ObjectSelected("pen");
            board.ObjectSelected("notebook");
            board.MakeRequested();
            var field = typeof(RoomSpellGame).GetField("round", BindingFlags.NonPublic | BindingFlags.Instance);
            var round = (PuzzleRound)field.GetValue(game);
            Require(round != null, "UI creates a related round");
            var used = new System.Collections.Generic.HashSet<int>();
            for (var i = 0; i < round.Answer.Length; i++)
            {
                var index = Enumerable.Range(0, round.Tiles.Length).First(t => !used.Contains(t) && round.Tiles[t] == round.Answer[i]);
                used.Add(index);
                board.TileSelected(index);
            }
            Require(round.IsSolved, "Complete application solves the round");
            Require(board.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("Solved!")), "Player sees solved feedback");
            CaptureBoard(board);
            board.NextRequested();
            var next = (PuzzleRound)field.GetValue(game);
            Require(next != null && next.Answer != round.Answer, "Same objects create a different answer");
            File.WriteAllText("Validation/play-flow.txt", "Play flow passed: demo objects -> create puzzle -> letter selection -> solved feedback -> different next word.\n");
            Debug.Log("ROOMSPELL_PLAY_FLOW_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    public static void CaptureBoard(RoomSpellBoard board, string path = "Preview.png")
    {
        Canvas.ForceUpdateCanvases();
        foreach (var text in board.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        var camera = board.View.GetComponent<Camera>();
        var target = new RenderTexture(1600, 1200, 24);
        target.Create();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
        RenderPipeline.SubmitRenderRequest(camera, request);
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply();
        File.WriteAllBytes(path, image.EncodeToPNG());
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(image);
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Verification failed: " + message);
    }
}
