using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.Audio
{
    public static class YuiMicrophonePermission
    {
        public static async Task<bool> EnsureAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                var request = Application.RequestUserAuthorization(UserAuthorization.Microphone);
                while (!request.isDone) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
            }
            token.ThrowIfCancellationRequested();
            return Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
            await Task.CompletedTask;
            return true;
#endif
        }
    }
}
