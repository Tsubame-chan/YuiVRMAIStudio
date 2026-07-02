using NUnit.Framework;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiRuntimeRoutingPolicyTests
    {
        [Test]
        public void PendingVisionImageAttachment_IsConsumedAfterSuccessfulChat()
        {
            var attachment = new YuiPendingVisionImageAttachment();
            attachment.SetImageDataUrl("data:image/jpeg;base64,AAAA");

            var firstContext = new RequestContext();
            Assert.IsTrue(attachment.ApplyTo(firstContext));
            Assert.AreEqual("data:image/jpeg;base64,AAAA", firstContext.Extra["image_data_url"]);

            attachment.MarkConsumedAfterSuccessfulChat();

            var secondContext = new RequestContext();
            Assert.IsFalse(attachment.ApplyTo(secondContext));
            Assert.IsFalse(secondContext.Extra.ContainsKey("image_data_url"));
        }

        [Test]
        public void BackendMonitorPolicy_MonitorsWhenBackendTtsIsSelectedAfterBackendIndependentModes()
        {
            Assert.IsFalse(YuiBackendMonitorPolicy.ShouldMonitorBackend(
                YuiConversationModes.DirectOpenAi,
                "voicevox-native"));
            Assert.IsFalse(YuiBackendMonitorPolicy.ShouldMonitorBackend(
                YuiConversationModes.LocalAi,
                "silent"));

            Assert.IsTrue(YuiBackendMonitorPolicy.ShouldMonitorBackend(
                YuiConversationModes.DirectOpenAi,
                "server-http"));
            Assert.IsTrue(YuiBackendMonitorPolicy.ShouldMonitorBackend(
                YuiConversationModes.LocalAi,
                "server"));
            Assert.IsTrue(YuiBackendMonitorPolicy.ShouldMonitorBackend(
                YuiConversationModes.Stable,
                "voicevox-native"));
        }
    }
}
