using System;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.Api
{
    public sealed class YuiBackendException : Exception
    {
        public YuiBackendException(long statusCode, string transportError, string responseBody, string url)
            : base(BuildMessage(statusCode, transportError, responseBody, url))
        {
            StatusCode = statusCode;
            TransportError = transportError;
            ResponseBody = responseBody;
            Url = url;
        }

        public long StatusCode { get; }
        public string TransportError { get; }
        public string ResponseBody { get; }
        public string Url { get; }
        public string Detail => ExtractDetail(ResponseBody);
        public string UserMessage => BuildUserMessage(StatusCode, TransportError, Detail);

        private static string BuildMessage(
            long statusCode,
            string transportError,
            string responseBody,
            string url)
        {
            return $"Backend request failed ({statusCode}) {transportError} at {url}: {responseBody}";
        }

        private static string ExtractDetail(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return string.Empty;
            }

            try
            {
                var json = JObject.Parse(responseBody);
                var detail = json["detail"];
                if (detail == null)
                {
                    return responseBody.Trim();
                }

                if (detail.Type == JTokenType.String)
                {
                    return detail.Value<string>()?.Trim() ?? string.Empty;
                }

                return detail.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch
            {
                return responseBody.Trim();
            }
        }

        private static string BuildUserMessage(long statusCode, string transportError, string detail)
        {
            if (statusCode == 0)
            {
                return "Backendに接続できません。起動状態とURLを確認してください。";
            }

            if (!string.IsNullOrWhiteSpace(detail))
            {
                return $"Backendエラー ({statusCode}): {detail}";
            }

            return $"Backendエラー ({statusCode}): {transportError}";
        }
    }
}
