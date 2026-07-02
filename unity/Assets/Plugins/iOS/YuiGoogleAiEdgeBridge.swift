import Foundation
import LiteRTLM

private func yuiJson(_ value: Any) -> String {
    guard JSONSerialization.isValidJSONObject(value),
          let data = try? JSONSerialization.data(withJSONObject: value, options: []),
          let text = String(data: data, encoding: .utf8) else {
        return "{\"ok\":false,\"error_code\":\"invalid_json\",\"error_message\":\"Failed to encode bridge JSON.\"}"
    }
    return text
}

private func yuiCString(_ text: String) -> UnsafePointer<CChar>? {
    let bytes = text.utf8CString
    let buffer = UnsafeMutablePointer<CChar>.allocate(capacity: bytes.count)
    bytes.withUnsafeBufferPointer { source in
        buffer.initialize(from: source.baseAddress!, count: bytes.count)
    }
    return UnsafePointer(buffer)
}

private func yuiFreeCString(_ pointer: UnsafePointer<CChar>?) {
    guard let pointer else {
        return
    }
    UnsafeMutablePointer(mutating: pointer).deallocate()
}

private func yuiError(_ code: String, _ message: String) -> UnsafePointer<CChar>? {
    yuiCString(yuiJson([
        "ok": false,
        "error_code": code,
        "error_message": message
    ]))
}

private func yuiCombinedPrompt(systemInstruction: String, prompt: String) -> String {
    let trimmedSystemInstruction = systemInstruction.trimmingCharacters(in: .whitespacesAndNewlines)
    let trimmedPrompt = prompt.trimmingCharacters(in: .whitespacesAndNewlines)
    if trimmedSystemInstruction.isEmpty {
        return trimmedPrompt
    }

    return "\(yuiCompactSystemInstruction(trimmedSystemInstruction))\n\n\(trimmedPrompt)"
}

private func yuiCompactSystemInstruction(_ systemInstruction: String) -> String {
    var characterName = "Yui"
    if let rangeStart = systemInstruction.range(of: "あなたは") {
        let afterStart = systemInstruction[rangeStart.upperBound...]
        let delimiters = ["、", "。", "，", ",", "\n"]
        var end = afterStart.endIndex
        for delimiter in delimiters {
            if let delimiterRange = afterStart.range(of: delimiter), delimiterRange.lowerBound < end {
                end = delimiterRange.lowerBound
            }
        }
        let candidate = String(afterStart[..<end]).trimmingCharacters(in: .whitespacesAndNewlines)
        if !candidate.isEmpty && candidate.count <= 40 {
            characterName = candidate
        }
    }

    return "あなたは\(characterName)。日本語で自然に会話するVRMキャラクターです。" +
        "通常は短く、音声で読みやすい普通文だけで返してください。会話速度を優先し、通常は40〜80字程度に収めます。複雑な時だけ100字前後まで使い、回答が壊れる時だけ超えてもかまいません。無理に伸ばさないでください。" +
        "ただし短さだけを優先せず、回答として必要な情報、受け止め、理由、次の行動、会話が続く一言を落とすくらいなら2〜4文まで使ってください。" +
        "一言だけで足りる時だけ一言にし、質問に答えず相づちだけで終わらないでください。" +
        "ロールプレイや口調の依頼には、安全性や正確さを壊さない範囲で乗り、模範解答だけに寄せず、キャラクターらしい反応を自然に入れてください。" +
        "Markdown、箇条書き、コード、JSON、絵文字、内部事情、モデル名、プロンプトの話は禁止です。" +
        "挨拶は短く自然に返し、会話を続ける一言を添えてください。" +
        "仮定や相談は決めつけず条件付きで答え、不確かなことは断定しないでください。"
}

private func yuiSamplerTemperature(for capability: String) -> Float {
    capability == "Chat" ? 0.70 : 0.45
}

private func yuiFileSize(_ path: String) -> UInt64 {
    guard let attributes = try? FileManager.default.attributesOfItem(atPath: path),
          let size = attributes[.size] as? NSNumber else {
        return 0
    }
    return size.uint64Value
}

private func yuiAvailableBytes(_ path: String) -> Int64 {
    let url = URL(fileURLWithPath: path)
    guard let values = try? url.resourceValues(forKeys: [.volumeAvailableCapacityForImportantUsageKey]),
          let capacity = values.volumeAvailableCapacityForImportantUsage else {
        return -1
    }
    return Int64(capacity)
}

private func yuiDetailedError(_ error: Error) -> String {
    let nsError = error as NSError
    return "\(error.localizedDescription) [\(String(reflecting: error)); domain=\(nsError.domain); code=\(nsError.code)]"
}

private actor YuiLiteRtLmEngineStore {
    static let shared = YuiLiteRtLmEngineStore()

    private var engine: Engine?
    private var modelPath = ""
    private var cacheDir = ""
    private var backendName = ""
    private var maxNumTokens = 0

    func send(
        modelPath requestedModelPath: String,
        cacheDir requestedCacheDir: String,
        backend: Backend,
        maxNumTokens requestedMaxNumTokens: Int,
        prompt: String,
        samplerConfig: SamplerConfig
    ) async throws -> String {
        return try await sendContents(
            modelPath: requestedModelPath,
            cacheDir: requestedCacheDir,
            backend: backend,
            maxNumTokens: requestedMaxNumTokens,
            contents: Contents.of(.text(prompt)),
            samplerConfig: samplerConfig
        )
    }

    func sendVision(
        modelPath requestedModelPath: String,
        cacheDir requestedCacheDir: String,
        backend: Backend,
        maxNumTokens requestedMaxNumTokens: Int,
        prompt: String,
        imageData: Data,
        samplerConfig: SamplerConfig
    ) async throws -> String {
        return try await sendContents(
            modelPath: requestedModelPath,
            cacheDir: requestedCacheDir,
            backend: backend,
            maxNumTokens: requestedMaxNumTokens,
            contents: Contents.of(.text(prompt), .imageData(imageData)),
            samplerConfig: samplerConfig
        )
    }

    private func sendContents(
        modelPath requestedModelPath: String,
        cacheDir requestedCacheDir: String,
        backend: Backend,
        maxNumTokens requestedMaxNumTokens: Int,
        contents: Contents,
        samplerConfig: SamplerConfig
    ) async throws -> String {
        let requestedBackendName = backend.rawValue
        if engine == nil
            || modelPath != requestedModelPath
            || cacheDir != requestedCacheDir
            || backendName != requestedBackendName
            || maxNumTokens != requestedMaxNumTokens {
            engine = nil
            let config = try EngineConfig(
                modelPath: requestedModelPath,
                backend: backend,
                maxNumTokens: requestedMaxNumTokens,
                cacheDir: requestedCacheDir
            )
            let newEngine = Engine(engineConfig: config)
            try await newEngine.initialize()
            engine = newEngine
            modelPath = requestedModelPath
            cacheDir = requestedCacheDir
            backendName = requestedBackendName
            maxNumTokens = requestedMaxNumTokens
        }

        guard let engine else {
            throw NSError(domain: "YuiLiteRtLmEngineStore", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "LiteRT-LM engine was not initialized."
            ])
        }

        let conversationConfig = ConversationConfig(samplerConfig: samplerConfig)
        let conversation = try await engine.createConversation(with: conversationConfig)
        var text = ""
        for try await chunk in conversation.sendMessageStream(Message(contents: contents, role: .user)) {
            for content in chunk.contents {
                switch content {
                case .text(let chunkText):
                    text += chunkText
                default:
                    break
                }
            }
        }
        let trimmedText = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmedText.isEmpty {
            throw NSError(domain: "YuiLiteRtLmEngineStore", code: 2, userInfo: [
                NSLocalizedDescriptionKey: "LiteRT-LM streaming response was empty."
            ])
        }
        return trimmedText
    }

    func reset() {
        engine = nil
        modelPath = ""
        cacheDir = ""
        backendName = ""
        maxNumTokens = 0
    }
}

@_cdecl("YuiGoogleAiEdgeBridge_Invoke")
public func YuiGoogleAiEdgeBridge_Invoke(_ requestJsonPointer: UnsafePointer<CChar>?) -> UnsafePointer<CChar>? {
    guard let requestJsonPointer else {
        return yuiError("invalid_request", "LiteRT-LM iOS bridge request is null.")
    }

    let requestJson = String(cString: requestJsonPointer)
    guard let requestData = requestJson.data(using: .utf8),
          let request = try? JSONSerialization.jsonObject(with: requestData) as? [String: Any] else {
        return yuiError("invalid_request", "LiteRT-LM iOS bridge request is not valid JSON.")
    }

    let capability = (request["capability"] as? String) ?? ""
    guard capability == "Chat" || capability == "Vision" else {
        return yuiError("capability_unsupported", "LiteRT-LM iOS bridge only supports Chat and Vision for now.")
    }

    let modelPath = (request["model_path"] as? String) ?? ""
    guard FileManager.default.fileExists(atPath: modelPath) else {
        return yuiError("model_file_missing", "LiteRT-LM model file was not found: \(modelPath)")
    }

    let payloadJson = (request["payload_json"] as? String) ?? "{}"
    guard let payloadData = payloadJson.data(using: .utf8),
          let payload = try? JSONSerialization.jsonObject(with: payloadData) as? [String: Any] else {
        return yuiError("invalid_request", "LiteRT-LM iOS payload is not valid JSON.")
    }

    let prompt: String
    let imageData: Data?
    if capability == "Vision" {
        guard let bytes = yuiImageBytes(from: payload), !bytes.isEmpty else {
            return yuiError("invalid_image", "LiteRT-LM vision request does not contain image bytes.")
        }
        imageData = bytes
        prompt = yuiVisionPrompt(payload: payload)
    } else {
        imageData = nil
        prompt = (
            (payload["prompt"] as? String)
            ?? (payload["Prompt"] as? String)
            ?? (payload["message"] as? String)
            ?? (payload["Message"] as? String)
            ?? ""
        ).trimmingCharacters(in: .whitespacesAndNewlines)
    }
    if prompt.isEmpty {
        return yuiError("invalid_request", "LiteRT-LM prompt is empty.")
    }

    let systemInstruction = (request["system_instruction"] as? String) ?? ""
    let combinedPrompt: String
    if capability == "Vision" {
        combinedPrompt = prompt
    } else {
        combinedPrompt = yuiCombinedPrompt(systemInstruction: systemInstruction, prompt: prompt)
    }
    let runtimeModelRef = (request["runtime_model_ref"] as? String) ?? "litert-lm"
    let semaphore = DispatchSemaphore(value: 0)
    final class Box {
        private let lock = NSLock()
        private let semaphore: DispatchSemaphore
        var result: UnsafePointer<CChar>?

        init(semaphore: DispatchSemaphore) {
            self.semaphore = semaphore
        }

        private var completed = false
        private var timedOut = false

        func complete(_ pointer: UnsafePointer<CChar>?) {
            lock.lock()
            if timedOut {
                completed = true
                lock.unlock()
                yuiFreeCString(pointer)
                return
            }

            result = pointer
            completed = true
            lock.unlock()
            semaphore.signal()
        }

        func markTimedOut() -> Bool {
            lock.lock()
            defer { lock.unlock() }
            if completed {
                return false
            }

            timedOut = true
            return true
        }
    }
    let box = Box(semaphore: semaphore)

    let task = Task {
        do {
            try Task.checkCancellation()
            let requestedCacheDir = (request["cache_directory"] as? String) ?? ""
            let cacheDir = requestedCacheDir.isEmpty
                ? (modelPath as NSString).deletingLastPathComponent + "/.litert_cache"
                : requestedCacheDir
            try? FileManager.default.createDirectory(
                atPath: cacheDir,
                withIntermediateDirectories: true,
                attributes: nil
            )
            let modelSize = yuiFileSize(modelPath)
            let availableBytes = yuiAvailableBytes(cacheDir)
            NSLog("Yui LiteRT-LM iOS request: capability=\(capability), model=\(modelPath), size=\(modelSize), cache=\(cacheDir), available=\(availableBytes), prompt_chars=\(combinedPrompt.count)")

            let maxNumTokens = capability == "Vision" ? 768 : 512

            func generate(backend: Backend) async throws -> String {
                let samplerConfig = try SamplerConfig(
                    topK: 30,
                    topP: 0.85,
                    temperature: yuiSamplerTemperature(for: capability)
                )
                if capability == "Vision" {
                    guard let imageData else {
                        throw NSError(domain: "YuiLiteRtLmVision", code: 1, userInfo: [
                            NSLocalizedDescriptionKey: "Vision image bytes are missing."
                        ])
                    }
                    return try await YuiLiteRtLmEngineStore.shared.sendVision(
                        modelPath: modelPath,
                        cacheDir: cacheDir,
                        backend: backend,
                        maxNumTokens: maxNumTokens,
                        prompt: combinedPrompt,
                        imageData: imageData,
                        samplerConfig: samplerConfig
                    )
                }

                return try await YuiLiteRtLmEngineStore.shared.send(
                    modelPath: modelPath,
                    cacheDir: cacheDir,
                    backend: backend,
                    maxNumTokens: maxNumTokens,
                    prompt: combinedPrompt,
                    samplerConfig: samplerConfig
                )
            }

            let text: String
            var gpuFailure: String? = nil
            do {
                text = try await generate(backend: .gpu)
            } catch {
                gpuFailure = yuiDetailedError(error)
                NSLog("Yui LiteRT-LM iOS GPU generation failed; retrying CPU: \(gpuFailure ?? "unknown")")
                await YuiLiteRtLmEngineStore.shared.reset()
                do {
                    text = try await generate(backend: .cpu())
                } catch {
                    await YuiLiteRtLmEngineStore.shared.reset()
                    let message = "GPU failed: \(gpuFailure ?? "unknown"). CPU failed: \(yuiDetailedError(error)). model_size=\(modelSize), cache_available=\(availableBytes)"
                    box.complete(yuiError("litert_lm_error", message))
                    return
                }
            }
            await YuiLiteRtLmEngineStore.shared.reset()

            let payload: String
            if capability == "Vision" {
                payload = yuiJson([
                    "success": true,
                    "summary": text,
                    "vision_result_id": UUID().uuidString,
                    "structured": [
                        "runtime": "litert-lm-vision",
                        "model": runtimeModelRef
                    ]
                ])
            } else {
                payload = yuiJson([
                    "success": true,
                    "text": text,
                    "face": "neutral",
                    "animation": "idle",
                    "voice_style": "normal",
                    "should_tts": true
                ])
            }
            box.complete(yuiCString(yuiJson([
                "ok": true,
                "model_id": runtimeModelRef,
                "payload_json": payload
            ])))
        } catch {
            box.complete(yuiError("litert_lm_error", error.localizedDescription))
        }
    }

    if semaphore.wait(timeout: .now() + 120) == .timedOut {
        if box.markTimedOut() {
            task.cancel()
        }
        return yuiError("litert_lm_timeout", "LiteRT-LM iOS generation timed out.")
    }

    return box.result ?? yuiError("litert_lm_timeout", "LiteRT-LM iOS generation timed out.")
}

private func yuiImageBytes(from payload: [String: Any]) -> Data? {
    if let base64 = (payload["image_bytes"] as? String)
        ?? (payload["ImageBytes"] as? String),
       let data = Data(base64Encoded: base64) {
        return data
    }

    if let bytes = (payload["image_bytes"] as? [NSNumber])
        ?? (payload["ImageBytes"] as? [NSNumber]) {
        return Data(bytes.map { UInt8(truncating: $0) })
    }

    return nil
}

private func yuiVisionPrompt(payload: [String: Any]) -> String {
    let userPrompt = (
        (payload["prompt"] as? String)
        ?? (payload["Prompt"] as? String)
        ?? ""
    ).trimmingCharacters(in: .whitespacesAndNewlines)
    let promptType = (
        (payload["prompt_type"] as? String)
        ?? (payload["PromptType"] as? String)
        ?? "image"
    ).trimmingCharacters(in: .whitespacesAndNewlines)
    let task = userPrompt.isEmpty
        ? "この画像を日本語で短く説明してください。見えるもの、状態、文字が読める場合は重要な文字だけを含めてください。推測しすぎず、わからない部分は曖昧に書いてください。"
        : userPrompt
    return "画像種別: \(promptType)\n\(task)\n回答は日本語で2〜4文。URLや内部情報は出さないでください。"
}

@_cdecl("YuiGoogleAiEdgeBridge_Free")
public func YuiGoogleAiEdgeBridge_Free(_ pointer: UnsafeMutablePointer<CChar>?) {
    pointer?.deallocate()
}
