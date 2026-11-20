#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Roadways.Editor
{
    public static class HotReloadManager
    {
        private const string PackageName = "com.roadways.busgame";
        private const string RemotePackageName = "com.unity3d.mobileremote";

        [MenuItem("Roadways/Hot Reload/1. Enable Unity Remote Live Streaming")]
        public static void EnableUnityRemote()
        {
            Debug.Log("<color=#3498db>[HotReload] Enabling Unity Remote 5 for instant real-time live preview...</color>");

            // 1. Configure EditorSettings to stream to the connected Android device
            EditorSettings.unityRemoteDevice = "Any Android Device";
            EditorSettings.unityRemoteCompression = "JPEG";
            EditorSettings.unityRemoteResolution = "Normal";

            Debug.Log("<color=#2ecc71>[HotReload] EditorSettings configured for 'Any Android Device' (Normal resolution, JPEG compression).</color>");

            // 2. Launch Unity Remote 5 on the connected phone via ADB
            RunAdbCommand($"shell am start -n {RemotePackageName}/com.unity3d.player.UnityPlayerActivity");

            Debug.Log("<color=#2ecc71>[HotReload] Unity Remote 5 launched on device! Now whenever you press 'Play' in Unity, the game will live-stream to your phone with full touch input instantly without building an APK!</color>");
        }

        [MenuItem("Roadways/Hot Reload/2. Fast Patch & Run (Incremental Build)")]
        public static void FastPatchAndRun()
        {
            Debug.Log("<color=#3498db>[HotReload] Starting Fast Incremental Patch & Run (skips full APK re-packaging)...</color>");

            AndroidBuildScript.ConfigureAndroidTools();

            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
            if (!Directory.Exists(buildDir)) Directory.CreateDirectory(buildDir);
            string apkPath = Path.Combine(buildDir, "RoadwaysBusGame.apk");

            string[] scenes = new string[] { "Assets/_Project/Scenes/MainGame.unity" };

            // Switch to Android if needed
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            EditorUserBuildSettings.development = true;

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                // PatchPackage only updates changed files without full re-packaging
                options = BuildOptions.Development | BuildOptions.PatchPackage | BuildOptions.AutoRunPlayer
            };

            Debug.Log("[HotReload] Building patch and pushing directly to device...");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"<color=#2ecc71>[HotReload] Fast Patch Success! Deployed in {summary.totalTime.TotalSeconds:F1}s.</color>");
            }
            else
            {
                Debug.LogError($"<color=#e74c3c>[HotReload] Fast Patch failed with result {summary.result}. Reverting to standard build.</color>");
            }
        }

        [MenuItem("Roadways/Hot Reload/3. Fast Script-Only Build & Run")]
        public static void FastScriptOnlyBuildAndRun()
        {
            Debug.Log("<color=#3498db>[HotReload] Starting Script-Only Build & Run (recompiles C# only)...</color>");

            AndroidBuildScript.ConfigureAndroidTools();

            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
            string apkPath = Path.Combine(buildDir, "RoadwaysBusGame.apk");

            string[] scenes = new string[] { "Assets/_Project/Scenes/MainGame.unity" };

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | BuildOptions.BuildScriptsOnly | BuildOptions.AutoRunPlayer
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"<color=#2ecc71>[HotReload] Script-Only Build Success! Deployed in {summary.totalTime.TotalSeconds:F1}s.</color>");
            }
            else
            {
                Debug.LogError($"[HotReload] Script-Only Build failed with result {summary.result}.");
            }
        }

        private static void RunAdbCommand(string args)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process proc = Process.Start(psi))
                {
                    proc.WaitForExit(5000);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[HotReload] Could not execute ADB: {ex.Message}");
            }
        }
    }
}
#endif
