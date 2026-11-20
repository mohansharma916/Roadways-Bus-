#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoadwaysGame.Editor
{
    public static class Phase4Setup
    {
        [MenuItem("Roadways/Phase 4/Execute Phase 4 Bus & Bay Setup")]
        public static void ExecutePhase4BusSetup()
        {
            Debug.Log("<color=#2ecc71>[Phase 4] Starting Top-Down Bus Fleet & Parking Bays Setup...</color>");

            string scenePath = "Assets/_Project/Scenes/MainGame.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject busRoot = GameObject.Find("--- [BUS_SYSTEM] ---");
            if (busRoot == null)
            {
                busRoot = new GameObject("--- [BUS_SYSTEM] ---");
            }

            // 1. Trapezium & Bays
            Transform panelObj = busRoot.transform.Find("TrapeziumPanel");
            if (panelObj == null)
            {
                panelObj = new GameObject("TrapeziumPanel").transform;
                panelObj.SetParent(busRoot.transform, false);
            }

            ParkingBayManager bayMgr = panelObj.GetComponent<ParkingBayManager>();
            if (bayMgr == null) bayMgr = panelObj.gameObject.AddComponent<ParkingBayManager>();
            bayMgr.BuildTrapeziumTerminal();

            // 2. Bus Fleet Depot
            Transform depotObj = busRoot.transform.Find("BusFleetDepot");
            if (depotObj == null)
            {
                depotObj = new GameObject("BusFleetDepot").transform;
                depotObj.SetParent(busRoot.transform, false);
            }

            BusFleetController fleet = depotObj.GetComponent<BusFleetController>();
            if (fleet == null) fleet = depotObj.gameObject.AddComponent<BusFleetController>();
            fleet.BuildDepotFleet();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#2ecc71>[Phase 4] Bus Fleet & Parking Bays Setup Completed & Saved Successfully!</color>");
        }
    }
}
#endif
