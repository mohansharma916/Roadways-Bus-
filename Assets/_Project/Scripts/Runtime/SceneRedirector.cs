using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadwaysGame
{
    public static class SceneRedirector
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            string active = SceneManager.GetActiveScene().name;
            if (active != "MainGame" && active != "Slice" && active != "VehiclePreview") // these scenes are played directly
            {
                Debug.Log("<color=#3498db>[SceneRedirector] Redirecting to MainGame scene...</color>");
                SceneManager.LoadScene(0);
            }
        }
    }
}
