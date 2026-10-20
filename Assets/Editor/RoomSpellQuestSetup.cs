using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Meta.XR;
using Meta.XR.BuildingBlocks.AIBlocks;
using Oculus.Interaction;
using Oculus.Interaction.Editor;
using Oculus.Interaction.Input;
using RoomSpell;
using TMPro;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;

[InitializeOnLoad]
public static class RoomSpellQuestSetup
{
    const string PendingKey = "RoomSpell.QuestSetupPending";
    const string ScenePath = "Assets/Scenes/RoomSpell.unity";
    const string ProviderPath = "Assets/Models/RoomDetection.asset";

    static RoomSpellQuestSetup()
    {
        if (Application.isBatchMode && SessionState.GetBool(PendingKey, false))
            EditorApplication.delayCall += Configure;
    }

    [MenuItem("RoomSpell/Configure Quest application")]
    public static async void Configure()
    {
        try
        {
            SessionState.SetBool(PendingKey, true);
            Debug.Log("RoomSpell setup: import and validate resources");
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                var resources = Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).Single();
                await ImportResources(resources);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (!font) throw new InvalidOperationException("TMP essential font import failed.");
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/Models/yolo12nNoNMS.sentis");
            var labels = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Models/coco-labels.txt");
            if (!model || !labels) throw new InvalidOperationException("Detection model or labels are missing.");
            Debug.Log("RoomSpell setup: configure provider and Quest scene");

            var provider = AssetDatabase.LoadAssetAtPath<UnityInferenceEngineProvider>(ProviderPath);
            if (!provider)
            {
                provider = ScriptableObject.CreateInstance<UnityInferenceEngineProvider>();
                AssetDatabase.CreateAsset(provider, ProviderPath);
            }
            var properties = new SerializedObject(provider);
            properties.FindProperty("modelFile").objectReferenceValue = model;
            properties.FindProperty("classLabelsAsset").objectReferenceValue = labels;
            properties.FindProperty("mode").intValue = (int)UnityInferenceProviderMode.ObjectDetection;
            properties.FindProperty("backend").intValue = (int)BackendType.GPUCompute;
            properties.FindProperty("splitOverFrames").boolValue = true;
            properties.FindProperty("layersPerFrame").intValue = 22;
            properties.FindProperty("scoreThreshold").floatValue = 0.5f;
            properties.ApplyModifiedPropertiesWithoutUndo();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rigObject = InstantiatePrefab("126d619cf4daa52469682f85c1378b4a");
            var rig = rigObject.GetComponent<OVRCameraRig>();
            var manager = rigObject.GetComponent<OVRManager>();
            manager.isInsightPassthroughEnabled = true;
            var managerProperties = new SerializedObject(manager);
            managerProperties.FindProperty("_trackingOriginType").intValue = (int)OVRManager.TrackingOrigin.FloorLevel;
            managerProperties.ApplyModifiedPropertiesWithoutUndo();
            rigObject.AddComponent<OVRPassthroughLayer>();
            foreach (var camera in rig.GetComponentsInChildren<Camera>(true))
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.nearClipPlane = 0.1f;
                var additional = camera.GetUniversalAdditionalCameraData();
                additional.renderPostProcessing = false;
            }

            var interactions = InstantiatePrefab("0a7d2469f24041c4284c66706f84c45e", rig.transform);
            UnityObjectAddedBroadcaster.HandleObjectWasAdded(interactions);
            foreach (var reference in interactions.GetComponentsInChildren<OVRCameraRigRef>(true))
            {
                var referenceProperties = new SerializedObject(reference);
                referenceProperties.FindProperty("_ovrCameraRig").objectReferenceValue = rig;
                referenceProperties.ApplyModifiedPropertiesWithoutUndo();
            }

            var eventSystem = new GameObject("UI input", typeof(EventSystem), typeof(PointableCanvasModule));
            eventSystem.GetComponent<PointableCanvasModule>().ExclusiveMode = true;
            var desktopInput = eventSystem.AddComponent<InputSystemUIInputModule>();
            desktopInput.AssignDefaultActions();
            desktopInput.enabled = false;
            var boardObject = new GameObject("Word puzzle board", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RoomSpellBoard));
            var canvas = boardObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = rig.centerEyeAnchor.GetComponent<Camera>();
            boardObject.GetComponent<RectTransform>().sizeDelta = new Vector2(900, 740);
            boardObject.transform.localScale = Vector3.one * 0.001f;
            boardObject.transform.position = new Vector3(0, 1.2f, 1.4f);
            var board = boardObject.GetComponent<RoomSpellBoard>();
            board.Font = font;
            board.View = rig.centerEyeAnchor;
            AddCanvasInteraction("8369d93f7b6b99742bbea0649a41b7b1", canvas);
            AddCanvasInteraction("4db41829582c7d24f80ee9603868dd67", canvas);

            var cameraObject = new GameObject("Room camera", typeof(PassthroughCameraAccess));
            var access = cameraObject.GetComponent<PassthroughCameraAccess>();
            access.enabled = false;
            access.CameraPosition = PassthroughCameraAccess.CameraPositionType.Left;
            access.RequestedResolution = new Vector2Int(1280, 960);
            access.MaxFramerate = 30;
            var gameObject = new GameObject("RoomSpell", typeof(RoomSpellDetection), typeof(RoomSpellGame));
            var detection = gameObject.GetComponent<RoomSpellDetection>();
            detection.CameraAccess = access;
            detection.Provider = provider;
            var game = gameObject.GetComponent<RoomSpellGame>();
            game.Detection = detection;
            game.Board = board;
            game.InteractionRig = interactions;

            PlayerSettings.companyName = "RoomSpell";
            PlayerSettings.productName = "RoomSpell";
            PlayerSettings.bundleVersion = "0.2.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.roomspell.game");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.bundleVersionCode = 2;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var config = OVRProjectConfig.CachedProjectConfig;
            config.targetDeviceTypes = new System.Collections.Generic.List<OVRProjectConfig.DeviceType>
                { OVRProjectConfig.DeviceType.Quest3, OVRProjectConfig.DeviceType.Quest3S };
            config.insightPassthroughSupport = OVRProjectConfig.FeatureSupport.Required;
            config.isPassthroughCameraAccessEnabled = true;
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
            config.minHorizonOsSdkVersion = 74;
            OVRProjectConfig.CommitProjectConfig(config);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("RoomSpell setup: apply Meta project fixes");
            await OVRProjectSetup.FixAllAsync(BuildTargetGroup.Android);
            await OVRProjectSetup.FixAllAsync(BuildTargetGroup.Standalone);
            EnableFeatures(BuildTargetGroup.Android);
            EnableFeatures(BuildTargetGroup.Standalone);
            OVRManifestPreprocessor.GenerateOrUpdateAndroidManifest(true);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            var manifest = File.ReadAllText("Assets/Plugins/Android/AndroidManifest.xml");
            if (!manifest.Contains("horizonos.permission.HEADSET_CAMERA") || !manifest.Contains("com.oculus.feature.PASSTHROUGH"))
                throw new InvalidOperationException("Generated manifest is missing camera/passthrough declarations.");
            Debug.Log("ROOMSPELL_QUEST_SETUP_PASSED: model, rig, interactions, scene and manifest configured.");
            SessionState.SetBool(PendingKey, false);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            SessionState.SetBool(PendingKey, false);
            Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }

    static async Task ImportResources(string path)
    {
        var completion = new TaskCompletionSource<bool>();
        void Completed(string name) => completion.TrySetResult(true);
        void Failed(string name, string error) => completion.TrySetException(new InvalidOperationException(error));
        AssetDatabase.importPackageCompleted += Completed;
        AssetDatabase.importPackageFailed += Failed;
        try
        {
            AssetDatabase.ImportPackage(path, false);
            if (await Task.WhenAny(completion.Task, Task.Delay(90000)) != completion.Task)
                throw new TimeoutException("TMP resource import timed out.");
            await completion.Task;
        }
        finally
        {
            AssetDatabase.importPackageCompleted -= Completed;
            AssetDatabase.importPackageFailed -= Failed;
        }
    }

    static GameObject InstantiatePrefab(string guid, Transform parent = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        if (!prefab) throw new InvalidOperationException("SDK prefab missing: " + guid);
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    static void AddCanvasInteraction(string guid, Canvas canvas)
    {
        var interaction = InstantiatePrefab(guid, canvas.transform);
        var rect = interaction.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one * 0.5f;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localPosition = Vector3.zero;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one;
        interaction.GetComponent<PointableCanvas>().InjectCanvas(canvas);
    }

    static void EnableFeatures(BuildTargetGroup target)
    {
        const string path = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        var general = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
        if (!general)
        {
            general = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(general, path);
        }
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, general, true);
        if (!general.HasSettingsForBuildTarget(target)) general.CreateDefaultSettingsForBuildTarget(target);
        if (!general.HasManagerSettingsForBuildTarget(target)) general.CreateDefaultManagerSettingsForBuildTarget(target);
        var xr = general.SettingsForBuildTarget(target);
        xr.InitManagerOnStart = true;
        EditorUtility.SetDirty(xr);
        if (!XRPackageMetadataStore.AssignLoader(xr.Manager, typeof(OpenXRLoader).FullName, target))
            throw new InvalidOperationException("Could not assign OpenXR loader for " + target);
        var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(target);
        if (!settings) throw new InvalidOperationException("OpenXR settings missing for " + target);
        settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
        foreach (var feature in settings.GetFeatures())
        {
            if (feature is MetaXRFeature || feature is MetaXRFoveationFeature ||
                feature is OculusTouchControllerProfile || feature is MetaQuestTouchPlusControllerProfile)
                feature.enabled = true;
        }
        EditorUtility.SetDirty(settings);
    }

    [MenuItem("RoomSpell/Build Quest APK")]
    public static void Build()
    {
        Directory.CreateDirectory("Builds");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = "Builds/RoomSpell.apk",
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException("Quest build failed: " + report.summary.result);
        Debug.Log("ROOMSPELL_ANDROID_BUILD_PASSED: " + report.summary.totalSize + " bytes");
    }
}
