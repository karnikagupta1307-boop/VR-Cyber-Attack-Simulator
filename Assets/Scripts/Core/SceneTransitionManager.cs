using UnityEngine;
using UnityEngine.SceneManagement;

namespace CyberDefenseVR
{
    public static class SceneTransitionManager
    {
        public const string MainMenu = "MainMenu";
        public const string Training = "CyberDefenseTraining";
        public const string DesktopTest = "DesktopTest";
        static bool loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { loading = false; }

        public static void Load(string sceneName)
        {
            if (loading) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[CyberDefenseVR] Scene '" + sceneName + "' is not in Build Settings.");
                return;
            }
            loading = true;
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.completed += _ => loading = false;
        }
    }
}
