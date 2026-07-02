using System;
using YuiPhysicalAI.Core;

namespace YuiPhysicalAI.LocalAI
{
    public static class YuiLocalAiRoutingPolicy
    {
        public static bool RequestsLocalVision(string conversationMode)
        {
            return string.Equals(
                YuiConversationModes.Normalize(conversationMode),
                YuiConversationModes.LocalAi,
                StringComparison.OrdinalIgnoreCase);
        }

        public static bool ShouldPreferLocalVision(string conversationMode, bool localVisionAvailable)
        {
            return localVisionAvailable && RequestsLocalVision(conversationMode);
        }
    }
}
