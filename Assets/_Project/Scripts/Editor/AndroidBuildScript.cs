using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

namespace Roadways.Editor
{
    public static class AndroidBuildScript
    {
        private const string UnityEditorBase = "/Applications/Unity/Hub/Editor/6000.6.6f1/PlaybackEngines/AndroidPlayer";

        public static void ConfigureAndroidTools()
        {
            string sdk = Path.Combine(UnityEditorBase, "SDK");
            string ndk = Path.Combine(UnityEditorBase, "NDK");
            string jdk = Path.Combine(UnityEditorBase, "OpenJDK");

            Debug.Log($"[AndroidBuildScript] Configuring Android Tools:\nSDK: {sdk}\nNDK: {ndk}\nJDK: {jdk}");

            if (Directory.Exists(sdk))
            {
                EditorPrefs.SetString("AndroidSdkRoot", sdk);
            }
            if (Directory.Exists(ndk))
            {
                EditorPrefs.SetString("AndroidNdkRoot", ndk);
                EditorPrefs.SetString("AndroidNdkRootR23b", ndk);
            }
            if (Directory.Exists(jdk))
            {
                EditorPrefs.SetString("JdkPath", jdk);
            }
        }

        [MenuItem("Roadways/Build Android APK")]
        public static void BuildAPK()
        {
            ConfigureAndroidTools();

            Debug.Log("[AndroidBuildScript] Starting Android build...");

            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
            if (!Directory.Exists(buildDir))
            {
                Directory.CreateDirectory(buildDir);
            }
            string apkPath = Path.Combine(buildDir, "RoadwaysBusGame.apk");

            string[] scenes = new string[]
            {
                "Assets/_Project/Scenes/MainGame.unity"
            };

            // Switch active build target to Android
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[AndroidBuildScript] Switching active build target to Android...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            // Set PlayerSettings for Android
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[AndroidBuildScript] Building APK to: {apkPath}");
            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[AndroidBuildScript] SUCCESS! Built {summary.totalSize} bytes in {summary.totalTime.TotalSeconds:F1}s at {apkPath}");
            }
            else
            {
                Debug.LogError($"[AndroidBuildScript] BUILD FAILED! Result: {summary.result}, Errors: {summary.totalErrors}");
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        {
                            Debug.LogError($"[Step: {step.name}] {msg.content}");
                        }
                    }
                }
                EditorApplication.Exit(1);
            }
        }
    }
}
