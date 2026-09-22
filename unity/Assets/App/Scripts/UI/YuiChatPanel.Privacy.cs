using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private Task pendingConsent;
        private Task<T> SendWithPermissionAsync<T>(string destination, bool directOpenAi,
            Func<Task<T>> send, CancellationToken token) => YuiExternalDataConsent.SendAsync(
                t => EnsureExternalDataPermissionAsync(destination, directOpenAi, t), send, token);

        private async Task EnsureExternalDataPermissionAsync(string destination, bool directOpenAi, CancellationToken token)
        {
            destination = YuiExternalDataConsent.Destination(destination);
            token.ThrowIfCancellationRequested();
            if (YuiExternalDataConsent.HasPermission(destination)) return;
            if (pendingConsent != null)
            {
                await pendingConsent;
                token.ThrowIfCancellationRequested();
                if (YuiExternalDataConsent.HasPermission(destination)) return;
            }
            var completion = new TaskCompletionSource<bool>();
            pendingConsent = completion.Task;
            var root = CreateSavedDataPanel("Allow data sharing?");
            root.gameObject.AddComponent<YuiConsentDialogLifetime>().Completion = completion;
            var body = directOpenAi
                ? "Messages and recent conversation context, character instructions, recorded audio, and images you choose are sent to OpenAI to generate replies, transcribe speech, and analyze images. Web search may send a search query to search providers. Your API key is sent only to OpenAI."
                : "Messages and recent conversation context, character instructions, recorded audio, selected images, and text to read aloud are sent to this Backend. Its administrator chooses AI and voice providers, which may include OpenAI or other external services. Use a Backend you trust.";
            SavedDataText(root, destination + "\n\n" + YuiUiLocalization.Text(body) + "\n\n" +
                YuiUiLocalization.Text("Secret mode does not prevent this transfer or control the provider's retention. You can withdraw permission in Help → Quick guide → Privacy."));
            ComposerButton(root, "Deny", "Not now", () => completion.TrySetCanceled(), .03f, .04f, .34f, .16f);
            if (directOpenAi) ComposerButton(root, "Privacy", "OpenAI privacy policy", () =>
                Application.OpenURL("https://openai.com/policies/privacy-policy/"), .36f, .04f, .66f, .16f);
            ComposerButton(root, "Allow", "Allow", () => {
                if (completion.Task.IsCompleted || token.IsCancellationRequested) return;
                YuiExternalDataConsent.Grant(destination); completion.TrySetResult(true);
            }, .68f, .04f, .97f, .16f);
            using var registration = token.Register(() => completion.TrySetCanceled());
            try { await completion.Task; token.ThrowIfCancellationRequested(); }
            finally
            {
                if (root != null) Destroy(root.gameObject);
                if (pendingConsent == completion.Task) { pendingConsent = null; }
            }
        }

        public void ShowPrivacyInformation()
        {
            var root = CreateSavedDataPanel("Privacy");
            SavedDataText(root, YuiUiLocalization.Text("Conversation text, saved answers, and imported avatars are stored on this device until you delete them. Secret mode skips conversation history, but still sends requests through your selected AI and voice connections. OpenAI mode sends data directly to OpenAI; Backend mode uses the server and providers configured by its administrator. On-device AI does not send your messages to an AI server. Model downloads contact the hosting service. The iOS/macOS app stores your API key in Keychain. Windows/Android currently use app settings. Removing an API key in Settings deletes the saved key.") + "\n\n" +
                YuiUiLocalization.Text("Withdrawing permission affects future requests; it does not delete data already received by a server. For server retention or deletion, contact its administrator or provider."));
            ComposerButton(root, "Withdraw", "Withdraw sharing permission", () => {
                YuiExternalDataConsent.RevokeAll();
                Destroy(root.gameObject); RefreshCharacterSettings();
            }, .03f, .05f, .97f, .17f);
        }

        private void ShowKeyStorageError()
        {
            var root = CreateSavedDataPanel("API key not saved");
            SavedDataText(root, YuiUiLocalization.Text(YuiApiKeyStore.LastError));
        }

        public async Task<bool> RecoverSavedApiKeyAsync()
        {
            if (!await YuiApiKeyStore.RecoverAsync()) return false;
            openAiApiKey = YuiApiKeyStore.Read();
            directOpenAiClient = null;
            ConfigureAiRuntimeRouter();
            return true;
        }
    }
}
