using UnityEngine;

namespace SASCoop
{
    static class DesktopDisplay
    {
        // Override windowed preferences saved by older releases, once at startup.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void StartFullscreen()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            var resolution = Screen.currentResolution;
            Screen.SetResolution(resolution.width, resolution.height, FullScreenMode.FullScreenWindow);
#endif
        }
    }
}
