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
        private const string PackageName = "com.roadways.busgame";
        private const string MainScene = "Assets/_Project/Scenes/MainGame.unity", SliceScene = "Assets/_Project/Scenes/Slice.unity";

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

        public static void ConfigureKeystore()
        {
            string keystorePath = Path.Combine(Directory.GetCurrentDirectory(), "Keystores/roadways_release.keystore");
            if (File.Exists(keystorePath))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystorePath;
                PlayerSettings.Android.keystorePass = "RoadwaysPass2026";
                PlayerSettings.Android.keyaliasName = "roadways";
                PlayerSettings.Android.keyaliasPass = "RoadwaysPass2026";
                Debug.Log($"<color=#2ecc71>[AndroidBuildScript] Release Keystore configured: {keystorePath} (alias: roadways)</color>");
            }
            else
            {
                Debug.LogWarning($"<color=#f39c12>[AndroidBuildScript] Keystore not found at {keystorePath}, building with debug signature.</color>");
            }
        }

        public static void ConfigurePlayerSettings(bool isRelease)
        {
            // Scripting & Architecture
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // IL2CPP Compiler & Code Optimization
            if (isRelease)
            {
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSpeed);
                EditorUserBuildSettings.development = false;
            }
            else
            {
                PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Debug);
                EditorUserBuildSettings.development = true;
            }

            // Screen & Display Settings
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.fullscreenMode = FullScreenMode.FullScreenWindow;
        }

        [MenuItem("Roadways/Build Android APK")]
        public static void BuildAPK()
        {
            BuildInternal(true, false);
        }

        [MenuItem("Roadways/Build & Sign Release APK")]
        public static void BuildReleaseAPK()
        {
            BuildInternal(true, false);
        }

        [MenuItem("Roadways/Build, Sign & Deploy Release APK")]
        public static void BuildAndDeployReleaseAPK()
        {
            BuildInternal(true, true);
        }

        /// <summary>Release-signed build of the vertical slice (Slice scene only), installed on the connected phone and launched.
        /// ARMv7 only: the test phone (Realme RMX3690) runs 32-bit Android (armeabi-v7a), and one architecture halves the
        /// IL2CPP compile. An ARM64-only build fails to install there with INSTALL_FAILED_NO_MATCHING_ABIS.</summary>
        [MenuItem("Roadways/Slice/Build & Run Slice on Device")]
        public static void BuildAndRunSlice()
        {
            // Installs with adb directly: Unity's AutoRunPlayer step reported "No compatible Android device found" for a phone adb could see.
            RoadwaysGame.Editor.SliceSetup.EnsureMaterialTemplates();
            string apk = BuildInternal(true, false, SliceScene, AndroidArchitecture.ARMv7);
            if (apk == null) return;
            Debug.Log("[AndroidBuildScript] install: " + Adb($"install -r \"{apk}\"").Replace('\n', ' '));
            Debug.Log("[AndroidBuildScript] launch: " + Adb($"shell monkey -p {PackageName} -c android.intent.category.LAUNCHER 1"));
        }

        private static string Adb(string args)
        {
            string adb = Path.Combine(UnityEditorBase, "SDK/platform-tools/adb");
            var psi = new System.Diagnostics.ProcessStartInfo(File.Exists(adb) ? adb : "adb", args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            };
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                string output = proc.StandardOutput.ReadToEnd() + proc.StandardError.ReadToEnd();
                proc.WaitForExit(120000);
                return output.Trim();
            }
        }

        /// <returns>The APK path on success, otherwise null.</returns>
        private static string BuildInternal(bool isRelease, bool deployToDevice, string scene = MainScene, AndroidArchitecture onlyArchitecture = AndroidArchitecture.None)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureAndroidTools();
            if (isRelease)
            {
                ConfigureKeystore();
            }
            ConfigurePlayerSettings(isRelease);

            string buildDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
            if (!Directory.Exists(buildDir))
            {
                Directory.CreateDirectory(buildDir);
            }

            bool slice = scene == SliceScene;
            string apkFilename = slice ? "RoadwaysSlice.apk" : isRelease ? "RoadwaysBusGame_Release.apk" : "RoadwaysBusGame.apk";
            string apkPath = Path.Combine(buildDir, apkFilename);

            string[] scenes = { scene };

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[AndroidBuildScript] Switching active build target to Android...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }

            BuildOptions options = isRelease ? BuildOptions.None : BuildOptions.Development;
            if (deployToDevice)
            {
                options |= BuildOptions.AutoRunPlayer;
            }

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options
            };

            Debug.Log($"<color=#3498db>[AndroidBuildScript] Starting {(isRelease ? "RELEASE" : "DEVELOPMENT")} build to: {apkPath}</color>");
            AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;
            if (onlyArchitecture != AndroidArchitecture.None) PlayerSettings.Android.targetArchitectures = onlyArchitecture;
            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(buildPlayerOptions); }
            finally
            {
                PlayerSettings.Android.targetArchitectures = architectures; // leave the project's settings as they were...
                AssetDatabase.SaveAssets();                                  // ...on disk too: the build saved them mid-build
            }
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"<color=#2ecc71>[AndroidBuildScript] SUCCESS! Built {summary.totalSize} bytes ({summary.totalSize / (1024f * 1024f):F2} MB) in {summary.totalTime.TotalSeconds:F1}s at {apkPath}</color>");

                // Also copy to canonical Builds/RoadwaysBusGame.apk if release
                if (isRelease && !slice)
                {
                    string canonicalPath = Path.Combine(buildDir, "RoadwaysBusGame.apk");
                    File.Copy(apkPath, canonicalPath, true);
                    Debug.Log($"<color=#2ecc71>[AndroidBuildScript] Copied release build to canonical path: {canonicalPath}</color>");
                }
                return apkPath;
            }
            else
            {
                Debug.LogError($"<color=#e74c3c>[AndroidBuildScript] BUILD FAILED! Result: {summary.result}, Errors: {summary.totalErrors}</color>");
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
                return null;
            }
        }
    }
}
