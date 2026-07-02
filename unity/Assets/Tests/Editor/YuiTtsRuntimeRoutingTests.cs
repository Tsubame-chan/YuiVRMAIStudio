using NUnit.Framework;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiTtsRuntimeRoutingTests
    {
        [Test]
        public void ProviderForBackendAivis_UsesBackendAivis()
        {
            Assert.AreEqual("aivis", YuiTtsRuntimeRouting.BackendProviderForMode("aivis"));
            Assert.IsFalse(YuiTtsRuntimeRouting.UsesNativeSpeech("aivis"));
        }

        [Test]
        public void ProviderForBackendVoicevox_UsesBackendVoicevox()
        {
            Assert.AreEqual("voicevox", YuiTtsRuntimeRouting.BackendProviderForMode("server"));
            Assert.IsFalse(YuiTtsRuntimeRouting.UsesNativeSpeech("server"));
        }

        [Test]
        public void ProviderForIrodori_UsesHttpTtsProvider()
        {
            Assert.AreEqual("http", YuiTtsRuntimeRouting.BackendProviderForMode("server-http"));
            Assert.IsFalse(YuiTtsRuntimeRouting.UsesNativeSpeech("server-http"));
        }

        [Test]
        public void NativeModes_DoNotClaimBackendProvider()
        {
            Assert.IsTrue(YuiTtsRuntimeRouting.UsesNativeSpeech("aivis-native"));
            Assert.IsTrue(YuiTtsRuntimeRouting.UsesNativeSpeech("voicevox-native"));
            Assert.IsNull(YuiTtsRuntimeRouting.BackendProviderForMode("aivis-native"));
            Assert.IsNull(YuiTtsRuntimeRouting.BackendProviderForMode("voicevox-native"));
        }

        [Test]
        public void VoicevoxRoute_UsesBackendWhenHealthyAndNativeWhenBackendIsUnavailable()
        {
            Assert.AreEqual(
                YuiTtsExecutionRoute.Backend,
                YuiTtsRuntimeRouting.ResolveVoicevoxRoute(
                    backendVoicevoxAvailable: true,
                    nativeVoicevoxAvailable: true,
                    backendIsRemote: false));
            Assert.AreEqual(
                YuiTtsExecutionRoute.NativeVoicevox,
                YuiTtsRuntimeRouting.ResolveVoicevoxRoute(
                    backendVoicevoxAvailable: false,
                    nativeVoicevoxAvailable: true,
                    backendIsRemote: false));
        }

        [Test]
        public void VoicevoxRoute_DoesNotRequireBackendForZipAndRunExperience()
        {
            Assert.AreEqual(
                YuiTtsExecutionRoute.NativeVoicevox,
                YuiTtsRuntimeRouting.ResolveVoicevoxRoute(
                    backendVoicevoxAvailable: false,
                    nativeVoicevoxAvailable: true,
                    backendIsRemote: false));
        }

        [Test]
        public void VoicevoxRoute_AvoidsRemoteBackendWhenNativeVoicevoxExists()
        {
            Assert.AreEqual(
                YuiTtsExecutionRoute.NativeVoicevox,
                YuiTtsRuntimeRouting.ResolveVoicevoxRoute(
                    backendVoicevoxAvailable: true,
                    nativeVoicevoxAvailable: true,
                    backendIsRemote: true));
        }

        [Test]
        public void ChatdollKitVoicevoxFallback_IsLimitedToVoicevoxLegacyLocalMode()
        {
            Assert.IsTrue(YuiTtsRuntimeRouting.ShouldTryChatdollKitVoicevoxFallback("local"));
            Assert.IsFalse(YuiTtsRuntimeRouting.ShouldTryChatdollKitVoicevoxFallback("server"));
            Assert.IsFalse(YuiTtsRuntimeRouting.ShouldTryChatdollKitVoicevoxFallback("aivis"));
            Assert.IsFalse(YuiTtsRuntimeRouting.ShouldTryChatdollKitVoicevoxFallback("server-http"));
            Assert.IsFalse(YuiTtsRuntimeRouting.ShouldTryChatdollKitVoicevoxFallback("silent"));
        }
    }
}
