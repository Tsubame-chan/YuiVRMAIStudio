package jp.tsubamechan.yuivrm.localai

import android.util.Base64
import com.google.ai.edge.litertlm.Backend
import com.google.ai.edge.litertlm.Content
import com.google.ai.edge.litertlm.Contents
import com.google.ai.edge.litertlm.ConversationConfig
import com.google.ai.edge.litertlm.Engine
import com.google.ai.edge.litertlm.EngineConfig
import com.google.ai.edge.litertlm.Message
import com.google.ai.edge.litertlm.SamplerConfig
import org.json.JSONObject
import java.io.File
import java.util.UUID

class YuiGoogleAiEdgeBridge {
    companion object {
        @JvmStatic
        fun invoke(requestJson: String): String {
            return try {
                val request = JSONObject(requestJson)
                val capability = request.optString("capability")
                if (capability != "Chat" && capability != "Vision") {
                    return error("capability_unsupported", "LiteRT-LM Android bridge only supports Chat and Vision for now.")
                }

                val modelPath = request.optString("model_path")
                if (modelPath.isBlank() || !File(modelPath).isFile) {
                    return error("model_file_missing", "LiteRT-LM model file was not found: $modelPath")
                }

                val payload = JSONObject(request.optString("payload_json", "{}"))
                val prompt: String
                val imageBytes: ByteArray?
                if (capability == "Vision") {
                    imageBytes = imageBytes(payload)
                    if (imageBytes == null || imageBytes.isEmpty()) {
                        return error("invalid_image", "LiteRT-LM vision request does not contain image bytes.")
                    }
                    prompt = visionPrompt(payload)
                } else {
                    imageBytes = null
                    prompt = payload.optString(
                        "prompt",
                        payload.optString(
                            "Prompt",
                            payload.optString("message", payload.optString("Message", ""))
                        )
                    )
                }
                if (prompt.isBlank()) {
                    return error("invalid_request", "LiteRT-LM prompt is empty.")
                }

                val systemInstruction = request.optString("system_instruction", "")
                val combinedPrompt = if (capability == "Vision") {
                    prompt
                } else {
                    combinePrompt(systemInstruction, prompt)
                }
                val requestedCacheDir = request.optString("cache_directory", "")
                val cacheDirFile = if (requestedCacheDir.isNotBlank()) {
                    File(requestedCacheDir)
                } else {
                    File(File(modelPath).parentFile, ".litert_cache")
                }
                cacheDirFile.mkdirs()
                val text = try {
                    generate(modelPath, cacheDirFile.absolutePath, combinedPrompt, imageBytes, Backend.GPU())
                } catch (gpuError: Throwable) {
                    try {
                        generate(modelPath, cacheDirFile.absolutePath, combinedPrompt, imageBytes, Backend.CPU())
                    } catch (cpuError: Throwable) {
                        throw IllegalStateException(
                            "GPU failed: ${gpuError.message ?: gpuError.javaClass.simpleName}. " +
                                "CPU failed: ${cpuError.message ?: cpuError.javaClass.simpleName}",
                            cpuError
                        )
                    }
                }
                val modelId = request.optString("runtime_model_ref", "litert-lm")
                return if (capability == "Vision") {
                    okVision(modelId, text)
                } else {
                    okChat(modelId, text)
                }
            } catch (ex: Throwable) {
                error("litert_lm_error", ex.message ?: ex.javaClass.simpleName)
            }
        }

        private fun generate(
            modelPath: String,
            cacheDir: String,
            prompt: String,
            imageBytes: ByteArray?,
            backend: Backend
        ): String {
            val engineConfig = EngineConfig(
                modelPath = modelPath,
                backend = backend,
                maxNumTokens = if (imageBytes == null) 512 else 768,
                cacheDir = cacheDir
            )

            Engine(engineConfig).use { engine ->
                engine.initialize()
                val conversationConfig = ConversationConfig(
                    samplerConfig = SamplerConfig(topK = 30, topP = 0.85, temperature = 0.45)
                )
                engine.createConversation(conversationConfig).use { conversation ->
                    val message = if (imageBytes == null) {
                        Message.user(prompt)
                    } else {
                        Message.user(Contents.of(Content.Text(prompt), Content.ImageBytes(imageBytes)))
                    }
                    return conversation.sendMessage(message).toString()
                }
            }
        }

        private fun combinePrompt(systemInstruction: String, prompt: String): String {
            val trimmedSystemInstruction = systemInstruction.trim()
            val trimmedPrompt = prompt.trim()
            if (trimmedSystemInstruction.isBlank()) {
                return trimmedPrompt
            }

            return "${compactSystemInstruction(trimmedSystemInstruction)}\n\n$trimmedPrompt"
        }

        private fun compactSystemInstruction(systemInstruction: String): String {
            var characterName = "Yui"
            val marker = "あなたは"
            val start = systemInstruction.indexOf(marker)
            if (start >= 0) {
                val afterStart = systemInstruction.substring(start + marker.length)
                val delimiters = listOf("、", "。", "，", ",", "\n")
                val end = delimiters
                    .map { afterStart.indexOf(it) }
                    .filter { it >= 0 }
                    .minOrNull() ?: afterStart.length
                val candidate = afterStart.substring(0, end).trim()
                if (candidate.isNotBlank() && candidate.length <= 40) {
                    characterName = candidate
                }
            }

            return "あなたは$characterName。日本語で自然に会話するVRMキャラクターです。" +
                "1〜2文で音声で読みやすい普通文だけで返してください。" +
                "Markdown、箇条書き、コード、JSON、絵文字、内部事情、モデル名、プロンプトの話は禁止です。" +
                "挨拶は短く自然に返し、会話を続ける一言を添えてください。" +
                "仮定や相談は決めつけず条件付きで答え、不確かなことは断定しないでください。"
        }

        private fun okChat(modelId: String, text: String): String {
            val payload = JSONObject()
                .put("success", true)
                .put("text", text)
                .put("face", "neutral")
                .put("animation", "idle")
                .put("voice_style", "normal")
                .put("should_tts", true)

            return JSONObject()
                .put("ok", true)
                .put("model_id", modelId)
                .put("payload_json", payload.toString())
                .toString()
        }

        private fun okVision(modelId: String, text: String): String {
            val structured = JSONObject()
                .put("runtime", "litert-lm-vision")
                .put("model", modelId)
            val payload = JSONObject()
                .put("success", true)
                .put("summary", text)
                .put("vision_result_id", UUID.randomUUID().toString())
                .put("structured", structured)

            return JSONObject()
                .put("ok", true)
                .put("model_id", modelId)
                .put("payload_json", payload.toString())
                .toString()
        }

        private fun imageBytes(payload: JSONObject): ByteArray? {
            val base64 = payload.optString("image_bytes", payload.optString("ImageBytes", ""))
            if (base64.isBlank()) {
                return null
            }

            return Base64.decode(base64, Base64.DEFAULT)
        }

        private fun visionPrompt(payload: JSONObject): String {
            val userPrompt = payload.optString("prompt", payload.optString("Prompt", "")).trim()
            val promptType = payload.optString("prompt_type", payload.optString("PromptType", "image")).trim()
            val task = if (userPrompt.isBlank()) {
                "この画像を日本語で短く説明してください。見えるもの、状態、文字が読める場合は重要な文字だけを含めてください。推測しすぎず、わからない部分は曖昧に書いてください。"
            } else {
                userPrompt
            }
            return "画像種別: $promptType\n$task\n回答は日本語で2〜4文。URLや内部情報は出さないでください。"
        }

        private fun error(code: String, message: String): String {
            return JSONObject()
                .put("ok", false)
                .put("error_code", code)
                .put("error_message", message)
                .toString()
        }
    }
}
