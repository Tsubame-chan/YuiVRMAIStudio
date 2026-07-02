using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiPendingVisionImageAttachment
    {
        private string imageDataUrl;

        public void SetImageDataUrl(string value)
        {
            imageDataUrl = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        public bool ApplyTo(RequestContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(imageDataUrl))
            {
                return false;
            }

            context.Extra["image_data_url"] = imageDataUrl;
            context.Extra["image_detail"] = "auto";
            return true;
        }

        public void MarkConsumedAfterSuccessfulChat()
        {
            imageDataUrl = null;
        }
    }
}
