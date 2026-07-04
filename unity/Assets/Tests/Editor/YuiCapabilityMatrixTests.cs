using System.Collections.Generic;
using NUnit.Framework;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiCapabilityMatrixTests
    {
        [Test]
        public void DesktopVoicevox_IsReadyWithNativeFallbackWhenBackendIsOffline()
        {
            var snapshot = YuiCapabilityMatrix.FromProviderStatus(
                providerStatus: null,
                backendReachable: false,
                nativeVoicevoxAvailable: true,
                localChatAvailable: true,
                directOpenAiConfigured: false);

            var voicevox = snapshot.Tts("server");

            Assert.AreEqual(YuiCapabilityState.Ready, voicevox.State);
            Assert.AreEqual(YuiCapabilityRoute.Native, voicevox.Route);
            StringAssert.Contains("Local", voicevox.Detail);
        }

        [Test]
        public void BackendOnlyTts_RemainsVisibleButRequiresBackendWhenOffline()
        {
            var snapshot = YuiCapabilityMatrix.FromProviderStatus(
                providerStatus: null,
                backendReachable: false,
                nativeVoicevoxAvailable: true,
                localChatAvailable: true,
                directOpenAiConfigured: false);

            Assert.AreEqual(YuiCapabilityState.NeedsBackend, snapshot.Tts("aivis").State);
            Assert.AreEqual(YuiCapabilityState.NeedsBackend, snapshot.Tts("server-http").State);
            Assert.IsTrue(snapshot.Tts("server-http").Visible);
        }

        [Test]
        public void ProviderStatus_DrivesBackendTtsReadiness()
        {
            var status = new ProviderStatusResponse
            {
                Backend = new ProviderSystemStatus { Status = "ok" },
                Providers = new Dictionary<string, ProviderStatusItem>
                {
                    ["voicevox"] = new ProviderStatusItem { Status = "ok" },
                    ["aivis"] = new ProviderStatusItem { Status = "configured" },
                    ["http_tts"] = new ProviderStatusItem { Status = "not_configured" },
                }
            };

            var snapshot = YuiCapabilityMatrix.FromProviderStatus(
                status,
                backendReachable: true,
                nativeVoicevoxAvailable: true,
                localChatAvailable: true,
                directOpenAiConfigured: true);

            Assert.AreEqual(YuiCapabilityRoute.Backend, snapshot.Tts("server").Route);
            Assert.AreEqual(YuiCapabilityState.Ready, snapshot.Tts("aivis").State);
            Assert.AreEqual(YuiCapabilityState.SetupRequired, snapshot.Tts("server-http").State);
        }

        [Test]
        public void ConversationModes_ExposeUnavailableRealtimeAsBackendRequired()
        {
            var snapshot = YuiCapabilityMatrix.FromProviderStatus(
                providerStatus: null,
                backendReachable: false,
                nativeVoicevoxAvailable: true,
                localChatAvailable: true,
                directOpenAiConfigured: true);

            Assert.AreEqual(YuiCapabilityState.Ready, snapshot.Conversation(YuiConversationModes.LocalAi).State);
            Assert.AreEqual(YuiCapabilityState.Ready, snapshot.Conversation(YuiConversationModes.DirectOpenAi).State);
            Assert.AreEqual(YuiCapabilityState.NeedsBackend, snapshot.Conversation(YuiConversationModes.BackendAi).State);
            Assert.AreEqual(YuiCapabilityState.NeedsBackend, snapshot.Conversation(YuiConversationModes.RealtimeTranslate).State);
        }

        [Test]
        public void DiagnosticsSummary_UsesSameCapabilitySnapshotAsHelpOverlay()
        {
            var status = new ProviderStatusResponse
            {
                Backend = new ProviderSystemStatus { Status = "ok" },
                Database = new ProviderSystemStatus { Status = "ok" },
                Providers = new Dictionary<string, ProviderStatusItem>
                {
                    ["openai"] = new ProviderStatusItem { Status = "missing_key" },
                    ["voicevox"] = new ProviderStatusItem { Status = "ok" },
                    ["aivis"] = new ProviderStatusItem { Status = "configured" },
                    ["http_tts"] = new ProviderStatusItem { Status = "configured" },
                }
            };

            var snapshot = YuiCapabilityMatrix.FromProviderStatus(
                status,
                backendReachable: true,
                nativeVoicevoxAvailable: true,
                localChatAvailable: true,
                directOpenAiConfigured: false);

            var summary = YuiCapabilityDiagnostics.FormatBody(snapshot);

            StringAssert.Contains("Backend", summary);
            StringAssert.Contains("Local VOICEVOX", summary);
            StringAssert.Contains("AivisSpeech HD", summary);
            StringAssert.Contains("Irodori TTS", summary);
        }

        [Test]
        public void Diagnostics_CanDecorateConversationLabelsWithoutChangingModeOrder()
        {
            var snapshot = YuiCapabilityMatrix.FromProviderStatus(
                providerStatus: null,
                backendReachable: false,
                nativeVoicevoxAvailable: true,
                localChatAvailable: true,
                directOpenAiConfigured: false);

            StringAssert.Contains(
                "Local ready",
                YuiCapabilityDiagnostics.DecorateConversationLabel(
                    "Auto Select (Backend > Local)",
                    YuiConversationModes.Stable,
                    snapshot));
            StringAssert.Contains(
                "Needs backend",
                YuiCapabilityDiagnostics.DecorateConversationLabel(
                    "Realtime Translation (Backend)",
                    YuiConversationModes.RealtimeTranslate,
                    snapshot));
        }
    }
}
