using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace YuiPhysicalAI.Api
{
    public sealed class YuiDirectOpenAiClient
    {
        public const string DefaultModel = "gpt-5.4-mini";
        private const string ResponsesUrl = "https://api.openai.com/v1/responses";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly string openAiApiKey;
        private readonly string model;

        public YuiDirectOpenAiClient(string apiKey, string model = null)
        {
            openAiApiKey = NormalizeApiKey(apiKey);
            this.model = NormalizeModel(model);
        }

        public string Model => model;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(openAiApiKey);

        public async Task<ChatResponse> SendChatAsync(
            ChatRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("OpenAI APIキーが未設定です。Settings > Advanced の OpenAI API Key を入力してください。");
            }

            EnsureRequestId(request);
            var payload = BuildResponsesPayload(request, model);
            var json = payload.ToString(Formatting.None);
            var bytes = Encoding.UTF8.GetBytes(json);

            using var webRequest = new UnityWebRequest(ResponsesUrl, UnityWebRequest.kHttpVerbPOST);
            webRequest.timeout = 60;
            webRequest.uploadHandler = new UploadHandlerRaw(bytes);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            webRequest.SetRequestHeader("Accept", "application/json");
            webRequest.SetRequestHeader("Authorization", "Bearer " + openAiApiKey);

            await SendAsync(webRequest, cancellationToken);
            var responseJson = webRequest.downloadHandler != null ? webRequest.downloadHandler.text : string.Empty;
            return NormalizeResponse(ParseChatResponse(responseJson));
        }

        public static JObject BuildResponsesPayload(ChatRequest request, string model)
        {
            request ??= new ChatRequest();
            var input = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["content"] = BuildUserContent(request)
                }
            };

            return new JObject
            {
                ["model"] = NormalizeModel(model),
                ["instructions"] = BuildInstructions(request),
                ["input"] = input,
                ["text"] = new JObject
                {
                    ["format"] = new JObject
                    {
                        ["type"] = "json_schema",
                        ["name"] = "yui_chat_response",
                        ["strict"] = true,
                        ["schema"] = ChatResponseJsonSchema()
                    }
                },
                ["max_output_tokens"] = 700
            };
        }

        public static ChatResponse ParseChatResponse(string responseJson)
        {
            var root = JObject.Parse(responseJson ?? "{}");
            var outputText = ExtractOutputText(root);
            if (string.IsNullOrWhiteSpace(outputText))
            {
                throw new InvalidOperationException("OpenAI API response did not contain output text.");
            }

            var cleaned = CleanJsonText(outputText);
            try
            {
                return JsonConvert.DeserializeObject<ChatResponse>(cleaned, JsonSettings);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("OpenAI API response was not valid Yui chat JSON: " + ex.Message);
            }
        }

        public static string ExtractOutputText(JObject root)
        {
            var direct = root?["output_text"]?.Value<string>();
            if (!string.IsNullOrWhiteSpace(direct))
            {
                return direct;
            }

            var output = root?["output"] as JArray;
            if (output == null)
            {
                return string.Empty;
            }

            foreach (var item in output)
            {
                var content = item?["content"] as JArray;
                if (content == null)
                {
                    continue;
                }

                foreach (var part in content)
                {
                    var text = part?["text"]?.Value<string>();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            return string.Empty;
        }

        public static string NormalizeModel(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? DefaultModel : value.Trim();
        }

        private static JToken BuildUserContent(ChatRequest request)
        {
            var contentText = BuildContentText(request);
            var imageDataUrl = request?.Context?.Extra != null
                && request.Context.Extra.TryGetValue("image_data_url", out var imageValue)
                    ? imageValue as string
                    : null;

            if (string.IsNullOrWhiteSpace(imageDataUrl) || !imageDataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return contentText;
            }

            var detail = "auto";
            if (request.Context.Extra.TryGetValue("image_detail", out var detailValue))
            {
                detail = detailValue as string;
                if (detail != "low" && detail != "high" && detail != "auto")
                {
                    detail = "auto";
                }
            }

            return new JArray
            {
                new JObject
                {
                    ["type"] = "input_text",
                    ["text"] = contentText
                },
                new JObject
                {
                    ["type"] = "input_image",
                    ["image_url"] = imageDataUrl,
                    ["detail"] = detail
                }
            };
        }

        private static string BuildContentText(ChatRequest request)
        {
            var builder = new StringBuilder();
            builder.Append(request?.Message ?? string.Empty);

            var customInstruction = (request?.CustomInstruction ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(customInstruction))
            {
                builder.AppendLine();
                builder.AppendLine();
                builder.AppendLine("Lower-priority user custom instruction for Yui's behavior in this session:");
                builder.Append(customInstruction.Length > 1200 ? customInstruction.Substring(0, 1200) : customInstruction);
            }

            var screenContext = request?.Context?.ScreenContext;
            if (!string.IsNullOrWhiteSpace(screenContext))
            {
                builder.AppendLine();
                builder.AppendLine();
                builder.AppendLine("Previous visual context summary for continuity:");
                builder.Append(screenContext);
            }

            return builder.ToString();
        }

        private static string BuildInstructions(ChatRequest request)
        {
            var characterName = string.IsNullOrWhiteSpace(request?.CharacterName)
                ? "Yui"
                : request.CharacterName.Trim();
            if (characterName.Length > 40)
            {
                characterName = characterName.Substring(0, 40);
            }

            return
                $"You are {characterName}, a friendly Japanese VRM embodied AI assistant. " +
                "Reply in natural Japanese as the character. Keep replies concise but useful: usually 2 to 4 short sentences. " +
                "For complex questions, answer enough to be useful without becoming a lecture. Start with the answer itself. " +
                "Do not announce that you will summarize, organize, keep it brief, or explain your style. " +
                "Natural roleplay, warmth, and light characterful reactions are welcome when they fit the user, but do not invent facts. " +
                "When the current user message includes an attached image, inspect the image directly and answer based on visible details. " +
                "For follow-up questions about that image, use the attached image and the prior visual context. " +
                "Because the reply will be spoken aloud, avoid Markdown, bold markers, code fences, decorative bullets, and raw URLs unless explicitly requested. " +
                "Return only the structured output requested by the schema.";
        }

        private static JObject ChatResponseJsonSchema()
        {
            return new JObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JObject
                {
                    ["text"] = new JObject { ["type"] = "string" },
                    ["face"] = new JObject { ["type"] = "string" },
                    ["animation"] = new JObject { ["type"] = "string" },
                    ["voice_style"] = new JObject { ["type"] = "string" },
                    ["should_use_vision"] = new JObject { ["type"] = "boolean" },
                    ["memory_action"] = new JObject { ["type"] = "string" },
                    ["should_tts"] = new JObject { ["type"] = "boolean" }
                },
                ["required"] = new JArray(
                    "text",
                    "face",
                    "animation",
                    "voice_style",
                    "should_use_vision",
                    "memory_action",
                    "should_tts")
            };
        }

        private static ChatResponse NormalizeResponse(ChatResponse response)
        {
            response ??= new ChatResponse();
            response.Text = string.IsNullOrWhiteSpace(response.Text)
                ? "うまく返答を作れませんでした。もう一度言ってくれる？"
                : response.Text.Trim();
            response.Face = string.IsNullOrWhiteSpace(response.Face) ? "Neutral" : response.Face.Trim();
            response.Animation = string.IsNullOrWhiteSpace(response.Animation) ? "idle" : response.Animation.Trim();
            response.VoiceStyle = string.IsNullOrWhiteSpace(response.VoiceStyle) ? "normal" : response.VoiceStyle.Trim();
            response.MemoryAction = string.IsNullOrWhiteSpace(response.MemoryAction) ? "none" : response.MemoryAction.Trim();
            response.ShouldTts = response.ShouldTts || !string.IsNullOrWhiteSpace(response.Text);
            return response;
        }

        private static async Task SendAsync(UnityWebRequest request, CancellationToken cancellationToken)
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (request.result == UnityWebRequest.Result.Success)
            {
                return;
            }

            var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            var detail = string.IsNullOrWhiteSpace(body) ? request.error : body;
            throw new InvalidOperationException($"OpenAI API request failed: {request.responseCode} {detail}");
        }

        private static string NormalizeApiKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string CleanJsonText(string value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = text.IndexOf('\n');
                var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewline >= 0 && lastFence > firstNewline)
                {
                    text = text.Substring(firstNewline + 1, lastFence - firstNewline - 1).Trim();
                }
            }

            return text;
        }

        private static void EnsureRequestId(ChatRequest request)
        {
            if (request != null && string.IsNullOrWhiteSpace(request.RequestId))
            {
                request.RequestId = Guid.NewGuid().ToString("N");
            }
        }
    }
}
