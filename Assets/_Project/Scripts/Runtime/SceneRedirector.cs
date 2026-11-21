using UnityEngine;
using UnityEngine.SceneManagement;

namespace RoadwaysGame
{
    public static class SceneRedirector
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            if (SceneManager.GetActiveScene().name != "MainGame")
            {
                Debug.Log("<color=#3498db>[SceneRedirector] Redirecting to MainGame scene...</color>");
                SceneManager.LoadScene(0);
            }
        }
    }
}
