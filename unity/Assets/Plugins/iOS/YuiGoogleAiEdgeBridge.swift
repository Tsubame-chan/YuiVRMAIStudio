import Foundation
import LiteRTLM
import UIKit

// A per-request handle also covers cancellation arriving just before native invocation.
private final class YuiLiteRtRequest {
    private let lock = NSLock()
    private var task: Task<Void, Never>?
    private var cancelled = false
    func attach(_ value: Task<Void, Never>) {
        lock.lock(); task = value; let stop = cancelled; lock.unlock()
        if stop { value.cancel() }
    }
    func cancel() {
        lock.lock(); cancelled = true; let value = task; lock.unlock()
        value?.cancel()
    }
}
private enum YuiLiteRtRequests {
    static let lock = NSLock()
    static var requests: [String: YuiLiteRtRequest] = [:]
    static func get(_ id: String) -> YuiLiteRtRequest {
        lock.lock(); defer { lock.unlock() }
        if let existing = requests[id] { return existing }
        let value = YuiLiteRtRequest(); requests[id] = value; return value
    }
    static func cancelAll() {
        lock.lock(); let active = Array(requests.values); lock.unlock()
        for request in active { request.cancel() }
    }
    static func forget(_ id: String) {
        lock.lock(); requests.removeValue(forKey: id); lock.unlock()
    }
}
@_cdecl("YuiGoogleAiEdgeBridge_Cancel")
public func YuiGoogleAiEdgeBridge_Cancel(_ id: UnsafePointer<CChar>?) {
    if let id { YuiLiteRtRequests.get(String(cString: id)).cancel() }
}
@_cdecl("YuiGoogleAiEdgeBridge_Forget")
public func YuiGoogleAiEdgeBridge_Forget(_ id: UnsafePointer<CChar>?) {
    if let id { YuiLiteRtRequests.forget(String(cString: id)) }
}

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

private func yuiChatHistory(_ payload: [String: Any]) -> [Message] {
    guard let history = payload["History"] as? [[String: Any]] else { return [] }
    var result: [Message] = []
    for item in history.suffix(8) {
        let expected = result.count.isMultiple(of: 2) ? "user" : "assistant"
        guard item["role"] as? String == expected, let content = item["content"] as? String,
              !content.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { continue }
        result.append(Message(String(content.prefix(600)), role: expected == "user" ? .user : .model))
    }
    if !result.count.isMultiple(of: 2) { result.removeLast() }
    return result
}

private func yuiSamplerTemperature(for capability: String) -> Float {
    capability == "Chat" ? 0.65 : 0.45
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
    private var visionEnabled = false
    private var speculativeEnabled = false
    private var idleReleaseTask: Task<Void, Never>?
    private var releaseRequested = false
    private var lifecycleObservers: [NSObjectProtocol] = []
    private var inferenceBusy = false
    private var inferenceWaiters: [(UUID, CheckedContinuation<Void, Error>)] = []

    private init() {
        for name in [UIApplication.didReceiveMemoryWarningNotification, UIApplication.didEnterBackgroundNotification] {
            lifecycleObservers.append(NotificationCenter.default.addObserver(forName: name, object: nil, queue: nil) { _ in
                // Metal inference cannot continue safely after the app leaves
                // the foreground. Cancel instead of retrying a model on CPU.
                if name == UIApplication.didEnterBackgroundNotification { YuiLiteRtRequests.cancelAll() }
                Task { await YuiLiteRtLmEngineStore.shared.reset() }
            })
        }
    }

    private func acquireInferenceSlot() async throws {
        try Task.checkCancellation()
        idleReleaseTask?.cancel()
        idleReleaseTask = nil
        if !inferenceBusy { inferenceBusy = true; return }
        let id = UUID()
        try await withTaskCancellationHandler(operation: {
            try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
                if Task.isCancelled { continuation.resume(throwing: CancellationError()) }
                else { inferenceWaiters.append((id, continuation)) }
            }
        }, onCancel: { Task { await self.cancelWaiter(id) } })
    }
    private func cancelWaiter(_ id: UUID) {
        guard let index = inferenceWaiters.firstIndex(where: { $0.0 == id }) else { return }
        inferenceWaiters.remove(at: index).1.resume(throwing: CancellationError())
    }
    private func releaseInferenceSlot() {
        // A lifecycle notification must never destroy an engine in use. Honor
        // it between requests, before handing the slot to a waiting caller.
        if releaseRequested { clearEngine() }
        if inferenceWaiters.isEmpty {
            inferenceBusy = false
            idleReleaseTask = Task {
                do { try await Task.sleep(nanoseconds: 30_000_000_000) }
                catch { return }
                self.releaseIdleEngine()
            }
        } else {
            inferenceWaiters.removeFirst().1.resume()
        }
    }

    private func releaseIdleEngine() {
        guard !inferenceBusy else { return }
        clearEngine()
    }

    func send(
        modelPath requestedModelPath: String,
        cacheDir requestedCacheDir: String,
        backend: Backend,
        maxNumTokens requestedMaxNumTokens: Int,
        prompt: String,
        systemInstruction: String,
        history: [Message],
        outputTokens: Int,
        supportsThinking: Bool,
        thinkingTokenBudget: Int,
        speculativeDecoding: Bool,
        samplerConfig: SamplerConfig
    ) async throws -> String {
        return try await sendContents(
            modelPath: requestedModelPath,
            cacheDir: requestedCacheDir,
            backend: backend,
            maxNumTokens: requestedMaxNumTokens,
            contents: Contents.of(.text(prompt)),
            samplerConfig: samplerConfig,
            systemInstruction: systemInstruction,
            history: history,
            outputTokens: outputTokens,
            supportsThinking: supportsThinking,
            thinkingTokenBudget: thinkingTokenBudget,
            speculativeDecoding: speculativeDecoding
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
            samplerConfig: samplerConfig,
            requestedVisionEnabled: true
        )
    }

    private func sendContents(
        modelPath requestedModelPath: String,
        cacheDir requestedCacheDir: String,
        backend: Backend,
        maxNumTokens requestedMaxNumTokens: Int,
        contents: Contents,
        samplerConfig: SamplerConfig,
        systemInstruction: String = "",
        history: [Message] = [],
        outputTokens: Int = 256,
        requestedVisionEnabled: Bool = false,
        supportsThinking: Bool = false,
        thinkingTokenBudget: Int = 768,
        speculativeDecoding: Bool = false
    ) async throws -> String {
        let requestedBackendName = backend.rawValue
        // Actor methods can interleave at await. Keep model replacement and the
        // SDK's process-wide experimental settings stable for the whole request.
        try await acquireInferenceSlot()
        defer { releaseInferenceSlot() }
        try Task.checkCancellation()
        if engine == nil
            || modelPath != requestedModelPath
            || cacheDir != requestedCacheDir
            || backendName != requestedBackendName
            || maxNumTokens != requestedMaxNumTokens
            || visionEnabled != requestedVisionEnabled
            || speculativeEnabled != speculativeDecoding {
            engine = nil
            ExperimentalFlags.optIntoExperimentalAPIs()
            ExperimentalFlags.enableSpeculativeDecoding = speculativeDecoding
            // Compiled GPU caches depend on decoding mode. A failed draft
            // initialization must not poison a normal-GPU or CPU retry.
            let profile = requestedBackendName.lowercased().contains("gpu")
                ? (speculativeDecoding ? "gpu-speculative" : "gpu-standard") : "cpu"
            let engineCacheDir = (requestedCacheDir as NSString).appendingPathComponent(profile)
            try FileManager.default.createDirectory(atPath: engineCacheDir, withIntermediateDirectories: true)
            let config = try EngineConfig(
                modelPath: requestedModelPath,
                backend: backend,
                visionBackend: requestedVisionEnabled ? .cpu() : nil,
                maxNumTokens: requestedMaxNumTokens,
                cacheDir: engineCacheDir
            )
            let newEngine = Engine(engineConfig: config)
            let initializeStarted = Date()
            try await newEngine.initialize()
            NSLog("Yui LiteRT-LM engine ready: backend=\(requestedBackendName), context=\(requestedMaxNumTokens), seconds=\(Date().timeIntervalSince(initializeStarted))")
            engine = newEngine
            modelPath = requestedModelPath
            cacheDir = requestedCacheDir
            backendName = requestedBackendName
            maxNumTokens = requestedMaxNumTokens
            visionEnabled = requestedVisionEnabled
            speculativeEnabled = speculativeDecoding
        }
        try Task.checkCancellation()

        guard let engine else {
            throw NSError(domain: "YuiLiteRtLmEngineStore", code: 1, userInfo: [
                NSLocalizedDescriptionKey: "LiteRT-LM engine was not initialized."
            ])
        }

        let conversationConfig = ConversationConfig(
            systemMessage: systemInstruction.isEmpty ? nil : Message(systemInstruction, role: .system),
            initialMessages: history,
            samplerConfig: samplerConfig,
            thinkingConfig: supportsThinking ? ThinkingConfig(enableThinking: true, thinkingTokenBudget: max(1, min(2048, thinkingTokenBudget))) : nil)
        let conversation = try await engine.createConversation(with: conversationConfig)
        let text = try await withTaskCancellationHandler(operation: {
            try Task.checkCancellation()
            var output = ""
            for try await chunk in conversation.sendMessageStream(Message(contents: contents, role: .user), maxOutputTokens: outputTokens) {
                try Task.checkCancellation()
                for content in chunk.contents {
                    if case .text(let chunkText) = content { output += chunkText }
                }
                // A small model can enter an endless repeated phrase. Do not
                // spend the whole request deadline generating that loop.
                let tail = Array(output.suffix(384))
                if tail.count >= 144 {
                    for width in 24...min(64, tail.count / 6) {
                        let end = tail.count
                        let phrase = Array(tail[(end-width)..<end])
                        if (2...6).allSatisfy({ repeatIndex in
                            Array(tail[(end-width*repeatIndex)..<(end-width*(repeatIndex-1))]) == phrase
                        }) {
                            try? conversation.cancel()
                            throw NSError(domain: "YuiRepetitiveResponse", code: 1, userInfo: [
                                NSLocalizedDescriptionKey: "The local model repeated the same phrase. Please shorten or rephrase the request."
                            ])
                        }
                    }
                }
            }
            try Task.checkCancellation()
            return output
        }, onCancel: { try? conversation.cancel() })
        let trimmedText = text.trimmingCharacters(in: .whitespacesAndNewlines)
        if trimmedText.isEmpty {
            throw NSError(domain: "YuiLiteRtLmEngineStore", code: 2, userInfo: [
                NSLocalizedDescriptionKey: "LiteRT-LM streaming response was empty."
            ])
        }
        return trimmedText
    }

    func reset() {
        idleReleaseTask?.cancel()
        idleReleaseTask = nil
        if inferenceBusy { releaseRequested = true; return }
        clearEngine()
    }

    private func clearEngine() {
        engine = nil
        modelPath = ""
        cacheDir = ""
        backendName = ""
        maxNumTokens = 0
        visionEnabled = false
        speculativeEnabled = false
        releaseRequested = false
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

    let requestId = (request["request_id"] as? String) ?? UUID().uuidString
    let requestHandle = YuiLiteRtRequests.get(requestId)
    defer { YuiLiteRtRequests.forget(requestId) }
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
            (payload["Input"] as? String)
            ?? (payload["prompt"] as? String)
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
    let history = capability == "Chat" ? yuiChatHistory(payload) : []
    let workMode = ((payload["Mode"] as? String) ?? "").lowercased() == "work"
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
            NSLog("Yui LiteRT-LM iOS request: capability=\(capability), model=\(modelPath), size=\(modelSize), cache=\(cacheDir), available=\(availableBytes), prompt_chars=\(prompt.count)")

            // This is input + output context capacity, not the response length.
            let maxNumTokens = capability == "Chat" ? max(4096,min(8192,(request["context_tokens"] as? Int) ?? 8192)) : 4096

            func generate(backend: Backend, speculativeDecoding: Bool = false) async throws -> String {
                let samplerConfig = try SamplerConfig(
                    topK: max(1,min(100,(request["top_k"] as? Int) ?? 30)),
                    topP: max(0.1,min(1,(request["top_p"] as? NSNumber)?.floatValue ?? 0.85)),
                    temperature: max(0,min(1.5,(request["temperature"] as? NSNumber)?.floatValue ?? (workMode ? 0.45 : yuiSamplerTemperature(for: capability))))
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
                        prompt: prompt,
                        imageData: imageData,
                        samplerConfig: samplerConfig
                    )
                }

                return try await YuiLiteRtLmEngineStore.shared.send(
                    modelPath: modelPath,
                    cacheDir: cacheDir,
                    backend: backend,
                    maxNumTokens: maxNumTokens,
                    prompt: prompt,
                    systemInstruction: systemInstruction,
                    history: history,
                    outputTokens: max(256, min(4096, (request["max_output_tokens"] as? Int) ?? (workMode ? 2304 : 1280))),
                    supportsThinking: (request["supports_thinking"] as? Bool) ?? false,
                    thinkingTokenBudget: (request["thinking_token_budget"] as? Int) ?? 768,
                    speculativeDecoding: speculativeDecoding,
                    samplerConfig: samplerConfig
                )
            }

            let text: String
            var gpuFailure: String? = nil
            do {
                let useDraft = capability == "Chat" && ((request["supports_speculative_decoding"] as? Bool) ?? false)
                do {
                    text = try await generate(backend: .gpu, speculativeDecoding: useDraft)
                } catch {
                    guard useDraft else { throw error }
                    try Task.checkCancellation()
                    if (error as NSError).domain == "YuiRepetitiveResponse" { throw error }
                    // A draft model is an optional speed optimization. Retry the
                    // same installed model on GPU without its extra allocations.
                    NSLog("Yui LiteRT-LM speculative GPU failed: \(yuiDetailedError(error))")
                    await YuiLiteRtLmEngineStore.shared.reset()
                    text = try await generate(backend: .gpu)
                }
            } catch {
                try Task.checkCancellation()
                if (error as NSError).domain == "YuiRepetitiveResponse" { throw error }
                gpuFailure = yuiDetailedError(error)
                NSLog("Yui LiteRT-LM iOS GPU generation failed; retrying CPU: \(gpuFailure ?? "unknown")")
                await YuiLiteRtLmEngineStore.shared.reset()
                do {
                    text = try await generate(backend: .cpu())
                } catch {
                    await YuiLiteRtLmEngineStore.shared.reset()
                    try Task.checkCancellation()
                    let message = "GPU failed: \(gpuFailure ?? "unknown"). CPU failed: \(yuiDetailedError(error)). model_size=\(modelSize), cache_available=\(availableBytes)"
                    box.complete(yuiError("litert_lm_error", message))
                    return
                }
            }
            // Keep a successful engine briefly for the next turn. Idle,
            // background and memory-pressure paths release it; repeated
            // destruction/reinitialization otherwise grows Metal allocations.

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
        } catch is CancellationError {
            box.complete(yuiError("cancelled", "Local AI generation stopped."))
        } catch {
            box.complete(yuiError("litert_lm_error", error.localizedDescription))
        }
    }

    requestHandle.attach(task)
    let timeoutSeconds = max(30,min(600,(request["timeout_seconds"] as? Int) ?? 120))
    if semaphore.wait(timeout: .now() + .seconds(timeoutSeconds)) == .timedOut {
        if box.markTimedOut() {
            task.cancel()
            return yuiError("litert_lm_timeout", "LiteRT-LM iOS generation timed out.")
        }
        // Completion can win the race with the semaphore deadline. Return its
        // owned result instead of discarding it and leaking the native string.
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
