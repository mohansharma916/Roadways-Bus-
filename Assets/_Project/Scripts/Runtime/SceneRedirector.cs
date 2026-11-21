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
            if (active != "MainGame" && active != "Slice") // the vertical slice is played directly from its own scene
            {
                Debug.Log("<color=#3498db>[SceneRedirector] Redirecting to MainGame scene...</color>");
                SceneManager.LoadScene(0);
            }
        }
    }
}
