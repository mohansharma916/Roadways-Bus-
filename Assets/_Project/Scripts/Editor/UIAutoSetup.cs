using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadwaysGame.Editor
{
    [InitializeOnLoad]
    public static class UIAutoSetup
    {
        static UIAutoSetup()
        {
            EditorApplication.delayCall += EnsureUIInScene;
        }

        [MenuItem("Tools/Roadways/Ensure Game UI")]
        public static void EnsureUIInScene()
        {
            // 1. Ensure Unity Remote Device is set to Any Android Device for Hot Reloading
            EditorSettings.unityRemoteDevice = "Any Android Device";
            EditorSettings.unityRemoteCompression = "JPEG";
            EditorSettings.unityRemoteResolution = "Normal";

            // 2. Force reload URP settings so cached memory picks up the assigned Mobile_Renderer
            AssetDatabase.ImportAsset("Assets/_Project/Settings/Mobile_Renderer.asset", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/_Project/Settings/Mobile_RPAsset.asset", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/_Project/Settings/Mobile_URP_Asset.asset", ImportAssetOptions.ForceUpdate);

            // 2. Open / Load MainGame scene
            Scene scene = SceneManager.GetActiveScene();
            string targetScenePath = "Assets/_Project/Scenes/MainGame.unity";

            if (!scene.isLoaded || !scene.name.Contains("MainGame"))
            {
                if (EditorApplication.isPlaying)
                {
                    Debug.Log("<color=#3498db>[UIAutoSetup] Loading MainGame in Play Mode...</color>");
                    SceneManager.LoadScene("MainGame");
                    return;
                }
                else
                {
                    Debug.Log("<color=#3498db>[UIAutoSetup] Opening MainGame scene: " + targetScenePath + "</color>");
                    scene = EditorSceneManager.OpenScene(targetScenePath, OpenSceneMode.Single);
                }
            }

            // 3. Find or create UI root
            GameObject uiRoot = GameObject.Find("--- [UI] ---");
            if (uiRoot == null)
            {
                uiRoot = new GameObject("--- [UI] ---");
            }

            GameUIController uiController = uiRoot.GetComponent<GameUIController>();
            if (uiController == null)
            {
                uiController = uiRoot.AddComponent<GameUIController>();
            }

            if (uiController != null)
            {
                uiController.BuildUI();
                if (!EditorApplication.isPlaying)
                {
                    EditorUtility.SetDirty(uiRoot);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                Debug.Log("<color=#2ecc71>[UIAutoSetup] Game UI successfully built and validated!</color>");
            }
        }
    }
}
