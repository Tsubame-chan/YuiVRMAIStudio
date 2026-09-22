using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.UI
{
    // Closing, replacing, disabling or destroying the dialog never means permission.
    public sealed class YuiConsentDialogLifetime : MonoBehaviour
    {
        public TaskCompletionSource<bool> Completion;
        private void OnDisable() { Completion?.TrySetCanceled(); }
        private void OnDestroy() { Completion?.TrySetCanceled(); }
    }
}
