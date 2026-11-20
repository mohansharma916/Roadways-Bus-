#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

namespace RoadwaysGame.Editor
{
    public static class Phase1Setup
    {
        [MenuItem("Roadways/Phase 1/Execute Complete Phase 1 Setup")]
        public static void ExecutePhase1Setup()
        {
            Debug.Log("<color=#2ecc71>[Phase 1] Starting Automated Setup...</color>");

            ConfigurePlayerSettings();
            ConfigureURPSettings();
            CreateAndConfigureMainScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#2ecc71>[Phase 1] Setup Completed Successfully!</color>");
        }

        public static void ConfigurePlayerSettings()
        {
            Debug.Log("[Phase 1] Configuring PlayerSettings (Identity & Orientation)...");

            PlayerSettings.companyName = "Roadways";
            PlayerSettings.productName = "Roadways Bus Game";
            PlayerSettings.bundleVersion = "0.1.0";
            
            // Set Package Name for Android
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.roadways.busgame");
            PlayerSettings.Android.bundleVersionCode = 1;

            // Portrait Lock
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.useAnimatedAutorotation = false;

            // Fullscreen & Safe Area
            PlayerSettings.statusBarHidden = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;

            // Target Android Architectures
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        }

        public static void ConfigureURPSettings()
        {
            Debug.Log("[Phase 1] Configuring URP Mobile Quality & Asset Settings...");

            string dir = "Assets/_Project/Settings";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string assetPath = "Assets/_Project/Settings/Mobile_URP_Asset.asset";
            UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);

            if (urpAsset == null)
            {
                urpAsset = UniversalRenderPipelineAsset.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(urpAsset, assetPath);
            }

            urpAsset.supportsHDR = false;
            urpAsset.msaaSampleCount = 2;
            urpAsset.shadowDistance = 25f;
            urpAsset.mainLightShadowmapResolution = 1024;

            GraphicsSettings.defaultRenderPipeline = urpAsset;
            QualitySettings.renderPipeline = urpAsset;
        }

        public static void CreateAndConfigureMainScene()
        {
            Debug.Log("[Phase 1] Building Master MainGame.unity Scene Hierarchy...");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. --- [MANAGERS] ---
            GameObject managersRoot = new GameObject("--- [MANAGERS] ---");
            new GameObject("GameManager").transform.SetParent(managersRoot.transform, false);
            new GameObject("UIManager").transform.SetParent(managersRoot.transform, false);
            new GameObject("AudioManager").transform.SetParent(managersRoot.transform, false);
            new GameObject("InputManager").transform.SetParent(managersRoot.transform, false);

            // 2. --- [CAMERAS] ---
            GameObject camerasRoot = new GameObject("--- [CAMERAS] ---");
            GameObject mainCamObj = new GameObject("MainCamera");
            mainCamObj.tag = "MainCamera";
            mainCamObj.transform.SetParent(camerasRoot.transform, false);

            Camera cam = mainCamObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GameConstants.LawnColor;
            cam.fieldOfView = GameConstants.BaseVerticalFOV;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 150f;

            mainCamObj.AddComponent<AudioListener>();
            mainCamObj.AddComponent<CameraRigController>();
            mainCamObj.AddComponent<ResponsiveCameraFOV>();

            // Target Point for Rig
            GameObject targetObj = new GameObject("CameraTarget");
            targetObj.transform.SetParent(camerasRoot.transform, false);
            targetObj.transform.position = GameConstants.CameraLookTarget;

            // Apply camera rig position immediately
            mainCamObj.GetComponent<CameraRigController>().ApplyRigTransform();

            // 3. --- [ENVIRONMENT] ---
            GameObject envRoot = new GameObject("--- [ENVIRONMENT] ---");
            
            // Directional Light
            GameObject dirLightObj = new GameObject("DirectionalLight");
            dirLightObj.transform.SetParent(envRoot.transform, false);
            dirLightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            Light dirLight = dirLightObj.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = GameConstants.DirectionalLightColor;
            dirLight.intensity = 0.85f;
            dirLight.shadows = LightShadows.Soft;

            // Park Ground & Zen Elements
            GameObject parkObj = new GameObject("ParkEnvironment");
            parkObj.transform.SetParent(envRoot.transform, false);
            EnvironmentBuilder envBuilder = parkObj.AddComponent<EnvironmentBuilder>();
            envBuilder.BuildEnvironment();

            new GameObject("GardenStones").transform.SetParent(envRoot.transform, false);
            new GameObject("Decals").transform.SetParent(envRoot.transform, false);

            // 4. --- [ROAD_NETWORK] ---
            GameObject roadRoot = new GameObject("--- [ROAD_NETWORK] ---");
            new GameObject("TrackSpline").transform.SetParent(roadRoot.transform, false);
            new GameObject("Curbs").transform.SetParent(roadRoot.transform, false);
            new GameObject("Shoulders").transform.SetParent(roadRoot.transform, false);
            new GameObject("HighwayNeck").transform.SetParent(roadRoot.transform, false);

            // 5. --- [CHARACTERS] ---
            GameObject charRoot = new GameObject("--- [CHARACTERS] ---");
            new GameObject("ChilbiFormationManager").transform.SetParent(charRoot.transform, false);
            new GameObject("WalkerPool").transform.SetParent(charRoot.transform, false);

            // 6. --- [BUS_SYSTEM] ---
            GameObject busRoot = new GameObject("--- [BUS_SYSTEM] ---");
            new GameObject("BusFleetDepot").transform.SetParent(busRoot.transform, false);
            new GameObject("TrapeziumPanel").transform.SetParent(busRoot.transform, false);
            new GameObject("BayManager").transform.SetParent(busRoot.transform, false);

            // 7. --- [UI] ---
            GameObject uiRoot = new GameObject("--- [UI] ---");
            new GameObject("CanvasScaler").transform.SetParent(uiRoot.transform, false);
            new GameObject("WatermarkHeader").transform.SetParent(uiRoot.transform, false);
            new GameObject("ReplayButton").transform.SetParent(uiRoot.transform, false);

            // Lighting settings
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = GameConstants.AmbientLightColor;

            string scenePath = "Assets/_Project/Scenes/MainGame.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"[Phase 1] MainGame.unity saved to {scenePath}");

            // Set scene in Build Settings
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
        }
    }
}
#endif
