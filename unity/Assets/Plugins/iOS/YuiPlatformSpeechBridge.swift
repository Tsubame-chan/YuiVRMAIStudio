import AVFoundation
import Foundation
import Speech

private func yuiSpeechJson(_ value: Any) -> String {
    guard JSONSerialization.isValidJSONObject(value),
          let data = try? JSONSerialization.data(withJSONObject: value, options: []),
          let text = String(data: data, encoding: .utf8) else {
        return "{\"ok\":false,\"error_code\":\"invalid_json\",\"error_message\":\"Failed to encode speech bridge JSON.\"}"
    }
    return text
}

private func yuiSpeechCString(_ text: String) -> UnsafePointer<CChar>? {
    let bytes = text.utf8CString
    let buffer = UnsafeMutablePointer<CChar>.allocate(capacity: bytes.count)
    bytes.withUnsafeBufferPointer { source in
        buffer.initialize(from: source.baseAddress!, count: bytes.count)
    }
    return UnsafePointer(buffer)
}

private func yuiSpeechError(_ code: String, _ message: String) -> UnsafePointer<CChar>? {
    yuiSpeechCString(yuiSpeechJson([
        "ok": false,
        "error_code": code,
        "error_message": message
    ]))
}

private func yuiSpeechParse(_ requestJsonPointer: UnsafePointer<CChar>?) -> [String: Any]? {
    guard let requestJsonPointer else {
        return nil
    }
    let requestJson = String(cString: requestJsonPointer)
    guard let data = requestJson.data(using: .utf8) else {
        return nil
    }
    return try? JSONSerialization.jsonObject(with: data) as? [String: Any]
}

@_cdecl("YuiPlatformSpeechBridge_Synthesize")
public func YuiPlatformSpeechBridge_Synthesize(_ requestJsonPointer: UnsafePointer<CChar>?) -> UnsafePointer<CChar>? {
    guard let request = yuiSpeechParse(requestJsonPointer) else {
        return yuiSpeechError("invalid_request", "Platform TTS request is not valid JSON.")
    }

    let text = ((request["text"] as? String) ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
    if text.isEmpty {
        return yuiSpeechError("invalid_request", "Platform TTS text is empty.")
    }

    let speedScale = max(0.75, min(1.35, (request["speed_scale"] as? Double) ?? 1.0))
    let pitchScale = max(-0.3, min(0.3, (request["pitch_scale"] as? Double) ?? 0.0))
    let utterance = AVSpeechUtterance(string: text)
    utterance.voice = preferredJapaneseVoice()
    utterance.rate = Float(max(0.38, min(0.58, Double(AVSpeechUtteranceDefaultSpeechRate) * speedScale)))
    utterance.pitchMultiplier = Float(max(0.85, min(1.25, 1.04 + pitchScale)))
    utterance.volume = 1.0

    let synthesizer = AVSpeechSynthesizer()
    let semaphore = DispatchSemaphore(value: 0)
    var pcmSamples = [Int16]()
    var sampleRate = 24000
    var failure: String?

    synthesizer.write(utterance) { buffer in
        guard let pcmBuffer = buffer as? AVAudioPCMBuffer else {
            return
        }

        if pcmBuffer.frameLength == 0 {
            semaphore.signal()
            return
        }

        sampleRate = Int(pcmBuffer.format.sampleRate.rounded())
        let channels = max(1, Int(pcmBuffer.format.channelCount))
        let frameCount = Int(pcmBuffer.frameLength)

        if let floatData = pcmBuffer.floatChannelData {
            let firstChannel = floatData[0]
            for frame in 0..<frameCount {
                var sample = firstChannel[frame]
                if channels > 1 {
                    var mixed = sample
                    for channel in 1..<channels {
                        mixed += floatData[channel][frame]
                    }
                    sample = mixed / Float(channels)
                }
                pcmSamples.append(Int16(max(-1.0, min(1.0, sample)) * Float(Int16.max)))
            }
        } else if let int16Data = pcmBuffer.int16ChannelData {
            let firstChannel = int16Data[0]
            for frame in 0..<frameCount {
                if channels == 1 {
                    pcmSamples.append(firstChannel[frame])
                } else {
                    var mixed = Int(firstChannel[frame])
                    for channel in 1..<channels {
                        mixed += Int(int16Data[channel][frame])
                    }
                    pcmSamples.append(Int16(max(Int(Int16.min), min(Int(Int16.max), mixed / channels))))
                }
            }
        } else {
            failure = "Unsupported AVSpeech audio buffer format."
            semaphore.signal()
        }
    }

    if semaphore.wait(timeout: .now() + 45) == .timedOut {
        synthesizer.stopSpeaking(at: .immediate)
        return yuiSpeechError("tts_timeout", "Platform TTS timed out.")
    }

    if let failure {
        return yuiSpeechError("tts_error", failure)
    }

    guard !pcmSamples.isEmpty else {
        return yuiSpeechError("empty_audio", "Platform TTS returned no audio.")
    }

    let wav = yuiSpeechWavData(samples: pcmSamples, sampleRate: sampleRate)
    let durationMs = Int((Double(pcmSamples.count) / Double(max(1, sampleRate))) * 1000.0)
    return yuiSpeechCString(yuiSpeechJson([
        "ok": true,
        "audio_base64": wav.base64EncodedString(),
        "sample_rate": sampleRate,
        "duration_ms": durationMs
    ]))
}

// Recognition snapshots can restart after a pause on recent iOS versions.
// Preserve earlier audio ranges, but replace revised overlapping hypotheses.
struct YuiSpeechTranscriptTimeline {
    struct Segment {
        let start: Double
        let duration: Double
        let text: String
    }
    private(set) var segments: [Segment] = []
    mutating func update(_ incoming: [Segment]) {
        guard let first = incoming.first else { return }
        segments.removeAll { $0.start >= first.start - 0.05 || $0.start + $0.duration > first.start + 0.05 }
        segments.append(contentsOf: incoming)
    }
    var text: String { segments.map { $0.text }.joined() }
}

@_cdecl("YuiPlatformSpeechBridge_Transcribe")
public func YuiPlatformSpeechBridge_Transcribe(_ requestJsonPointer: UnsafePointer<CChar>?) -> UnsafePointer<CChar>? {
    guard let request = yuiSpeechParse(requestJsonPointer) else {
        return yuiSpeechError("invalid_request", "Platform STT request is not valid JSON.")
    }

    let audioPath = (request["audio_path"] as? String) ?? ""
    guard FileManager.default.fileExists(atPath: audioPath) else {
        return yuiSpeechError("audio_missing", "Recorded audio file was not found.")
    }

    let authorization = SFSpeechRecognizer.authorizationStatus()
    guard authorization == .authorized else {
        return yuiSpeechError("speech_not_authorized", "Speech recognition permission is not granted.")
    }

    let recognizer = SFSpeechRecognizer(locale: Locale(identifier: "ja-JP"))
    guard let recognizer, recognizer.isAvailable else {
        return yuiSpeechError("speech_unavailable", "Japanese speech recognizer is not available.")
    }

    if #available(iOS 13.0, *) {
        guard recognizer.supportsOnDeviceRecognition else {
            return yuiSpeechError("on_device_stt_unavailable", "On-device Japanese speech recognition is not available on this device.")
        }
    }

    let recognitionRequest = SFSpeechURLRecognitionRequest(url: URL(fileURLWithPath: audioPath))
    recognitionRequest.shouldReportPartialResults = true
    if #available(iOS 16.0, *) {
        recognitionRequest.addsPunctuation = true
    }
    if #available(iOS 13.0, *) {
        recognitionRequest.requiresOnDeviceRecognition = true
    }

    let semaphore = DispatchSemaphore(value: 0)
    var timeline = YuiSpeechTranscriptTimeline()
    let resultLock = NSLock()
    var completed = false
    var confidence: Float?
    var failure: Error?
    var task: SFSpeechRecognitionTask?
    task = recognizer.recognitionTask(with: recognitionRequest) { result, error in
        resultLock.lock()
        defer { resultLock.unlock() }
        guard !completed else { return }
        if let result {
            let transcription = result.bestTranscription
            let formatted = transcription.formattedString as NSString
            timeline.update(transcription.segments.enumerated().map { index, segment in
                let start = index == 0 ? 0 : segment.substringRange.location
                let end = index + 1 < transcription.segments.count
                    ? transcription.segments[index + 1].substringRange.location : formatted.length
                let text = start <= end && end <= formatted.length
                    ? formatted.substring(with: NSRange(location: start, length: end - start)) : segment.substring
                return YuiSpeechTranscriptTimeline.Segment(start: segment.timestamp, duration: segment.duration, text: text)
            })
            confidence = result.bestTranscription.segments.last?.confidence
            if result.isFinal {
                completed = true
                semaphore.signal()
            }
        }
        if let error {
            failure = error
            completed = true
            semaphore.signal()
        }
    }

    if semaphore.wait(timeout: .now() + 30) == .timedOut {
        resultLock.lock(); completed = true; resultLock.unlock()
        task?.cancel()
        return yuiSpeechError("stt_timeout", "Platform STT timed out.")
    }

    resultLock.lock()
    let recognizedText = timeline.text
    let finalFailure = failure
    let finalConfidence = confidence
    resultLock.unlock()
    if let failure = finalFailure {
        return yuiSpeechError("stt_error", failure.localizedDescription)
    }

    let text = recognizedText.trimmingCharacters(in: .whitespacesAndNewlines)
    guard !text.isEmpty else {
        return yuiSpeechError("empty_transcript", "Platform STT returned an empty transcript.")
    }

    var response: [String: Any] = [
        "ok": true,
        "text": text
    ]
    if let confidence = finalConfidence {
        response["confidence"] = confidence
    }
    return yuiSpeechCString(yuiSpeechJson(response))
}

@_cdecl("YuiPlatformSpeechBridge_Free")
public func YuiPlatformSpeechBridge_Free(_ pointer: UnsafeMutablePointer<CChar>?) {
    pointer?.deallocate()
}

private func preferredJapaneseVoice() -> AVSpeechSynthesisVoice? {
    let voices = AVSpeechSynthesisVoice.speechVoices()
        .filter { $0.language == "ja-JP" }

    let preferredNames = ["Kyoko", "Otoya"]
    for name in preferredNames {
        if let voice = voices.first(where: { $0.name.localizedCaseInsensitiveContains(name) }) {
            return voice
        }
    }

    return AVSpeechSynthesisVoice(language: "ja-JP")
}

@_cdecl("YuiPlatformSpeechBridge_AuthorizationStatus")
public func YuiPlatformSpeechBridge_AuthorizationStatus() -> Int32 {
    return Int32(SFSpeechRecognizer.authorizationStatus().rawValue)
}

@_cdecl("YuiPlatformSpeechBridge_RequestAuthorization")
public func YuiPlatformSpeechBridge_RequestAuthorization() {
    DispatchQueue.main.async {
        if SFSpeechRecognizer.authorizationStatus() == .notDetermined {
            SFSpeechRecognizer.requestAuthorization { _ in }
        }
    }
}

private func yuiSpeechWavData(samples: [Int16], sampleRate: Int) -> Data {
    var data = Data()
    let byteRate = sampleRate * 2
    let blockAlign: UInt16 = 2
    let bitsPerSample: UInt16 = 16
    let subchunk2Size = UInt32(samples.count * 2)
    let chunkSize = UInt32(36) + subchunk2Size

    data.append("RIFF".data(using: .ascii)!)
    data.appendLE(chunkSize)
    data.append("WAVE".data(using: .ascii)!)
    data.append("fmt ".data(using: .ascii)!)
    data.appendLE(UInt32(16))
    data.appendLE(UInt16(1))
    data.appendLE(UInt16(1))
    data.appendLE(UInt32(sampleRate))
    data.appendLE(UInt32(byteRate))
    data.appendLE(blockAlign)
    data.appendLE(bitsPerSample)
    data.append("data".data(using: .ascii)!)
    data.appendLE(subchunk2Size)
    for sample in samples {
        data.appendLE(UInt16(bitPattern: sample))
    }
    return data
}

private extension Data {
    mutating func appendLE(_ value: UInt16) {
        var littleEndian = value.littleEndian
        append(Data(bytes: &littleEndian, count: MemoryLayout<UInt16>.size))
    }

    mutating func appendLE(_ value: UInt32) {
        var littleEndian = value.littleEndian
        append(Data(bytes: &littleEndian, count: MemoryLayout<UInt32>.size))
    }
}
