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

        [MenuItem("Roadways/Setup/Ensure Game UI")]
        public static void EnsureUIInScene()
        {
            // 1. Ensure Unity Remote Device is set to Any Android Device for Hot Reloading
            EditorSettings.unityRemoteDevice = "Any Android Device";
            EditorSettings.unityRemoteCompression = "JPEG";
            EditorSettings.unityRemoteResolution = "Normal";

            // 2. Open / Load MainGame scene if not active
            Scene scene = SceneManager.GetActiveScene();
            // Only set up MainGame when it is what's open (or nothing is). Respect any other saved scene, and never
            // switch scenes in play mode: it used to hijack every Play and every reload, including the slice scene.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!string.IsNullOrEmpty(scene.path) && !scene.path.EndsWith("MainGame.unity")) return;
            string targetScenePath = "Assets/_Project/Scenes/MainGame.unity";

            // 2b. Ensure CircuitRoad texture is imported as Sprite
            AssetDatabase.Refresh();
            string texturePath = "Assets/_Project/Textures/UI/CircuitRoad.png";
            if (System.IO.File.Exists(texturePath))
            {
                TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.alphaIsTransparency = true;
                    importer.mipmapEnabled = false;
                    AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                }
            }

            string resPath = "Assets/_Project/Resources/CircuitRoad.png";
            if (System.IO.File.Exists(resPath))
            {
                TextureImporter resImp = AssetImporter.GetAtPath(resPath) as TextureImporter;
                if (resImp != null)
                {
                    resImp.textureType = TextureImporterType.Sprite;
                    resImp.spriteImportMode = SpriteImportMode.Single;
                    resImp.alphaIsTransparency = true;
                    resImp.mipmapEnabled = false;
                    AssetDatabase.ImportAsset(resPath, ImportAssetOptions.ForceUpdate);
                }
            }

            if (!scene.isLoaded || !scene.name.Contains("MainGame"))
            {
                Debug.Log("<color=#3498db>[UIAutoSetup] Opening MainGame scene: " + targetScenePath + "</color>");
                scene = EditorSceneManager.OpenScene(targetScenePath, OpenSceneMode.Single);
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
                Sprite roadSp = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Textures/UI/CircuitRoad.png");
                if (roadSp != null)
                {
                    SerializedObject so = new SerializedObject(uiController);
                    SerializedProperty prop = so.FindProperty("roadCircuitSprite");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = roadSp;
                        so.ApplyModifiedProperties();
                    }
                }
                uiController.ParkOnlyMode = false;
                uiController.ApplyDisplayMode();
                uiController.BuildUI();
                if (!EditorApplication.isPlaying)
                {
                    EditorUtility.SetDirty(uiRoot);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                Debug.Log("<color=#2ecc71>[UIAutoSetup] Full Game view successfully applied!</color>");
            }
        }
    }
}
