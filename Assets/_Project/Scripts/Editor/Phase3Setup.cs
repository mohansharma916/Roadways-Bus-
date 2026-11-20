#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoadwaysGame.Editor
{
    public static class Phase3Setup
    {
        [MenuItem("Roadways/Phase 3/Execute Phase 3 Character Setup")]
        public static void ExecutePhase3CharacterSetup()
        {
            Debug.Log("<color=#2ecc71>[Phase 3] Starting 2.5D Chilbi Rig & Formation Setup...</color>");

            string scenePath = "Assets/_Project/Scenes/MainGame.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject charRoot = GameObject.Find("--- [CHARACTERS] ---");
            if (charRoot == null)
            {
                charRoot = new GameObject("--- [CHARACTERS] ---");
            }

            Transform formationObj = charRoot.transform.Find("ChilbiFormationManager");
            if (formationObj == null)
            {
                formationObj = new GameObject("ChilbiFormationManager").transform;
                formationObj.SetParent(charRoot.transform, false);
            }

            ChilbiFormationManager mgr = formationObj.GetComponent<ChilbiFormationManager>();
            if (mgr == null) mgr = formationObj.gameObject.AddComponent<ChilbiFormationManager>();

            mgr.InitializeFormation();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#2ecc71>[Phase 3] Character Rig & 430 Formation Setup Completed & Saved Successfully!</color>");
        }
    }
}
#endif
