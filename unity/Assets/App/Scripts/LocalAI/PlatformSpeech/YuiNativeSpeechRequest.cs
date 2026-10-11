using System;
using System.Threading;

namespace YuiPhysicalAI.LocalAI
{
    // Register native ownership before token callbacks can run. Keep ownership until
    // the native worker exits: cancellation must not delete an STT file still in use.
    public static class YuiNativeSpeechRequest
    {
        public static T Run<T>(string id, Func<T> invoke, CancellationToken token,
            Action<string> begin, Action<string> cancel, Action<string> end)
        {
            token.ThrowIfCancellationRequested();
            begin(id);
            try
            {
                using var registration = token.Register(() => cancel(id));
                token.ThrowIfCancellationRequested();
                var result = invoke();
                token.ThrowIfCancellationRequested();
                return result;
            }
            finally { end(id); }
        }
    }
}
