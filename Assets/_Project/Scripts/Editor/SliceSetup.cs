#if UNITY_EDITOR
using System.IO;
using RoadwaysGame.Slice;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoadwaysGame.Editor
{
    public static class SliceSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Slice.unity";

        /// <summary>Opens the vertical-slice scene, creating it (one SliceGame object; everything else is built at runtime) if needed.</summary>
        [MenuItem("Roadways/Slice/Open Slice Scene")]
        public static void OpenSlice()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Slice] Stop play mode first."); return; }
            if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogWarning("[Slice] The open scene has unsaved changes; not switching."); return; }
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("SliceGame").AddComponent<SliceGame>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Slice] Created " + ScenePath);
        }

        /// <summary>Opens a scene that shows one vehicle on a turntable, creating it if needed. Press Play to see it.</summary>
        [MenuItem("Roadways/Vehicles/Open Car Preview")]
        public static void OpenCarPreview()
        {
            const string path = "Assets/_Project/Scenes/VehiclePreview.unity";
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Slice] Stop play mode first."); return; }
            if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogWarning("[Slice] The open scene has unsaved changes; not switching."); return; }
            if (File.Exists(path)) { EditorSceneManager.OpenScene(path); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("VehiclePreview").AddComponent<VehiclePreview>();
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[Slice] Created " + path);
        }

        /// <summary>Material templates in Resources, so device builds include the shaders the slice uses. The slice makes
        /// every material at runtime, and a build strips any shader no asset references (Shader.Find then returns null).</summary>
        [MenuItem("Roadways/Slice/Create Material Templates")]
        public static void EnsureMaterialTemplates()
        {
            Template("Lit", "Universal Render Pipeline/Lit");
            Template("Ocean", "Roadways/OceanWater");
        }

        private static void Template(string name, string shader)
        {
            const string dir = "Assets/_Project/Resources/Slice";
            string path = dir + "/" + name + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Project/Resources", "Slice");
            AssetDatabase.CreateAsset(new Material(Shader.Find(shader)), path);
            Debug.Log("[Slice] Created " + path);
        }

        public const string SdfFontPath = "Assets/_Project/Resources/Fonts/LilitaOne-Regular SDF.asset";

        /// <summary>Sharp text, one step per run (run until it says ready): imports TextMesh Pro's essential resources without
        /// the import dialog, then makes an SDF font asset from Lilita One. Saved as an asset, its material references
        /// the SDF shader, so device builds include it.</summary>
        [MenuItem("Roadways/Slice/Set Up Sharp Text")]
        public static void SetUpSharpText()
        {
            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                AssetDatabase.ImportPackage(Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage"), false);
                Debug.Log("[Slice] Imported TMP Essential Resources; run Set Up Sharp Text again to create the font asset.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(SdfFontPath) != null) { Debug.Log("[Slice] Sharp text ready."); return; }
            Selection.objects = new Object[] { AssetDatabase.LoadAssetAtPath<Font>("Assets/_Project/Resources/Fonts/LilitaOne-Regular.ttf") };
            EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Font Asset/SDF");
            Debug.Log("[Slice] Created " + SdfFontPath);
        }

        /// <summary>Keeps play mode running while the editor is unfocused, so the game can be verified (and streamed to
        /// Unity Remote) while you work in another window. Android ignores runInBackground, so builds are unaffected.</summary>
        [MenuItem("Roadways/Slice/Enable Background Play")]
        public static void EnableBackgroundPlay()
        {
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("[Slice] Run In Background enabled.");
        }
    }
}
#endif
