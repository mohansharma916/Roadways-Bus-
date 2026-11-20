#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoadwaysGame.Editor
{
    public static class MasterSetup
    {
        [MenuItem("Roadways/Build & Save All Phases to Scene")]
        public static void BuildAndSaveAllPhases()
        {
            Debug.Log("<color=#3498db>[MasterSetup] Executing Full Scene Rebuild (Phases 1-4)...</color>");

            string scenePath = "Assets/_Project/Scenes/MainGame.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Phase 1 - Environment & Camera
            Phase1Setup.ExecutePhase1Setup();

            // 2. Phase 2 - Track Network
            Phase2Setup.ExecutePhase2TrackSetup();

            // 3. Phase 3 - Characters
            Phase3Setup.ExecutePhase3CharacterSetup();

            // 4. Phase 4 - Bus & Bays
            Phase4Setup.ExecutePhase4BusSetup();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#2ecc71>[MasterSetup] Full Scene Rebuild Completed Successfully!</color>");
        }
    }
}
#endif
