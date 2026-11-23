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
