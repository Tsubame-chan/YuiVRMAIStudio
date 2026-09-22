using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiVoiceEnvironmentOptionsTests
    {
        private static ProviderStatusResponse Backend(string state = "ok") => new ProviderStatusResponse {
            Providers = new Dictionary<string, ProviderStatusItem> {
                ["aivis"] = new ProviderStatusItem { Selectable = true, Status = state },
                ["http_tts"] = new ProviderStatusItem { Selectable = true, Status = state, Engine = "irodori_mlx" },
                ["voicevox"] = new ProviderStatusItem { Selectable = true, Status = state }
            }
        };

        [Test]
        public void PhoneWithoutBackend_OnlyShowsAvailableDeviceVoices()
        {
            var options = YuiVoiceEnvironmentOptions.Build(null, false, false, true, false, true, "server");
            CollectionAssert.AreEqual(new[] { "server", "local-ai", "silent" }, options.Select(p => p.Key));
        }

        [TestCase(YuiConversationModes.LocalAi)]
        [TestCase(YuiConversationModes.DirectOpenAi)]
        [TestCase(YuiConversationModes.Stable)]
        public void LocalOrCloudLlm_CanUseBackendAivis(string mode)
        {
            var options = YuiVoiceEnvironmentOptions.Build(Backend(), true, true, true, false, false, "server");
            Assert.IsTrue(options.Any(p => p.Key == "aivis"));
            Assert.IsTrue(options.Any(p => p.Key == "server-http" && p.Value.StartsWith("Irodori")));
            Assert.IsTrue(YuiBackendMonitorPolicy.ShouldMonitorBackend(mode, "aivis", true));
            Assert.IsTrue(YuiBackendMonitorPolicy.ShouldMonitorBackend(mode, "server-http", true));
        }

        [Test]
        public void InstalledButStoppedDesktopEngine_RemainsSelectable()
        {
            var options = YuiVoiceEnvironmentOptions.Build(Backend("offline"), false, true, true, false, false, "server");
            Assert.IsTrue(options.Any(p => p.Key == "aivis"));
            Assert.IsTrue(options.Any(p => p.Key == "server-http"));
        }

        [Test]
        public void PhoneDisconnectedFromOldBackend_HidesItsUnselectedVoices()
        {
            var options = YuiVoiceEnvironmentOptions.Build(Backend(), false, false, true, false, true, "server");
            Assert.IsFalse(options.Any(p => p.Key == "aivis" || p.Key == "server-http"));
        }

        [Test]
        public void SavedUnavailableVoice_IsNotSilentlyChanged()
        {
            var options = YuiVoiceEnvironmentOptions.Build(null, false, false, true, false, false, "aivis");
            Assert.AreEqual("AivisSpeech · Backend · Unavailable", options.Single(p => p.Key == "aivis").Value);
        }

        [Test]
        public void NativeAivis_IsDistinctFromBackendAndDoesNotMonitorBackend()
        {
            var options = YuiVoiceEnvironmentOptions.Build(null, false, false, true, true, true, "aivis-native");
            Assert.IsTrue(options.Any(p => p.Key == "aivis-native"));
            Assert.IsFalse(options.Any(p => p.Key == "aivis"));
            Assert.IsFalse(YuiBackendMonitorPolicy.ShouldMonitorBackend(YuiConversationModes.LocalAi, "aivis-native", true));
        }

        [Test]
        public void DefaultAivisUrlAlone_DoesNotAdvertiseMissingEngine()
        {
            var backend = Backend("offline");
            backend.Providers["aivis"].Selectable = false;
            backend.Providers["aivis"].BaseUrl = "http://127.0.0.1:10101";
            var options = YuiVoiceEnvironmentOptions.Build(backend, true, true, true, false, false, "server");
            Assert.IsFalse(options.Any(p => p.Key == "aivis"));
        }
    }
}
