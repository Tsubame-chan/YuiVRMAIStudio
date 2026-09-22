using System;
using System.IO;
using UnityEngine;

namespace YuiPhysicalAI.Platform
{
    internal static class YuiMacProcessWorkingDirectory
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            // Finder/LaunchServices can inherit the app's parent directory in Documents.
            // Mono's first relative dylib lookup opens "." while holding dyld's loader lock;
            // macOS file-access mediation can then block both that worker and AppKit.
            // Runtime assets already use absolute paths. Keep relative native lookups in
            // this app's own data directory, without requesting access to users' Documents.
            try
            {
                var directory = Application.persistentDataPath;
                Directory.CreateDirectory(directory);
                Directory.SetCurrentDirectory(directory);
                Debug.Log("Yui macOS native loader: using the application data directory.");
            }
            catch (Exception ex) { Debug.LogWarning("Yui macOS working directory: " + ex.GetType().Name); }
#endif
        }
    }
}
