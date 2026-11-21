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

        [MenuItem("Roadways/Force Open MainGame Scene")]
        public static void ForceOpenMainGame()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainGame.unity", OpenSceneMode.Single);
            EnsureUIInScene();
        }

        [MenuItem("Roadways/View/1. Show Cherry Blossom Park Only")]
        public static void ShowCherryBlossomParkOnly()
        {
            SetParkOnlyMode(true);
        }

        [MenuItem("Roadways/View/2. Show Full Game (All Systems)")]
        public static void ShowFullGame()
        {
            SetParkOnlyMode(false);
        }

        public static void SetParkOnlyMode(bool parkOnly)
        {
            GameObject uiRoot = GameObject.Find("--- [UI] ---");
            if (uiRoot != null)
            {
                GameUIController uiController = uiRoot.GetComponent<GameUIController>();
                if (uiController != null)
                {
                    uiController.ParkOnlyMode = parkOnly;
                    if (!EditorApplication.isPlaying)
                    {
                        EditorUtility.SetDirty(uiRoot);
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    }
                    Debug.Log($"<color=#2ecc71>[UIAutoSetup] View updated: ParkOnlyMode = {parkOnly}</color>");
                }
            }
        }

        [MenuItem("Tools/Roadways/Ensure Game UI")]
        public static void EnsureUIInScene()
        {
            // 1. Ensure Unity Remote Device is set to Any Android Device for Hot Reloading
            EditorSettings.unityRemoteDevice = "Any Android Device";
            EditorSettings.unityRemoteCompression = "JPEG";
            EditorSettings.unityRemoteResolution = "Normal";

            // 2. Open / Load MainGame scene if not active
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
                uiController.ParkOnlyMode = true;
                uiController.ApplyDisplayMode();
                uiController.BuildUI();
                if (!EditorApplication.isPlaying)
                {
                    EditorUtility.SetDirty(uiRoot);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                Debug.Log("<color=#2ecc71>[UIAutoSetup] Cherry Blossom Park view successfully applied!</color>");
            }
        }
    }
}
