#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoadwaysGame.Editor
{
    public static class Phase2Setup
    {
        [MenuItem("Roadways/Phase 2/Execute Phase 2 Track Network Setup")]
        public static void ExecutePhase2TrackSetup()
        {
            Debug.Log("<color=#2ecc71>[Phase 2] Starting Track Network Setup...</color>");

            string scenePath = "Assets/_Project/Scenes/MainGame.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject roadNetworkRoot = GameObject.Find("--- [ROAD_NETWORK] ---");
            if (roadNetworkRoot == null)
            {
                roadNetworkRoot = new GameObject("--- [ROAD_NETWORK] ---");
            }

            TrackNetworkManager netMgr = roadNetworkRoot.GetComponent<TrackNetworkManager>();
            if (netMgr == null)
            {
                netMgr = roadNetworkRoot.AddComponent<TrackNetworkManager>();
            }

            netMgr.BuildCompleteTrackNetwork();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#2ecc71>[Phase 2] Track Network Setup Completed & Saved Successfully!</color>");
        }
    }
}
#endif
