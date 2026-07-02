import Foundation
import Vision

private func yuiVisionJson(_ value: Any) -> String {
    guard JSONSerialization.isValidJSONObject(value),
          let data = try? JSONSerialization.data(withJSONObject: value, options: []),
          let text = String(data: data, encoding: .utf8) else {
        return "{\"ok\":false,\"error_code\":\"invalid_json\",\"error_message\":\"Failed to encode vision bridge JSON.\"}"
    }
    return text
}

private func yuiVisionCString(_ text: String) -> UnsafePointer<CChar>? {
    let bytes = text.utf8CString
    let buffer = UnsafeMutablePointer<CChar>.allocate(capacity: bytes.count)
    bytes.withUnsafeBufferPointer { source in
        buffer.initialize(from: source.baseAddress!, count: bytes.count)
    }
    return UnsafePointer(buffer)
}

private func yuiVisionError(_ code: String, _ message: String) -> UnsafePointer<CChar>? {
    yuiVisionCString(yuiVisionJson([
        "ok": false,
        "error_code": code,
        "error_message": message
    ]))
}

private func yuiVisionParse(_ requestJsonPointer: UnsafePointer<CChar>?) -> [String: Any]? {
    guard let requestJsonPointer else {
        return nil
    }
    let requestJson = String(cString: requestJsonPointer)
    guard let data = requestJson.data(using: .utf8) else {
        return nil
    }
    return try? JSONSerialization.jsonObject(with: data) as? [String: Any]
}

@_cdecl("YuiPlatformVisionBridge_Analyze")
public func YuiPlatformVisionBridge_Analyze(_ requestJsonPointer: UnsafePointer<CChar>?) -> UnsafePointer<CChar>? {
    guard let request = yuiVisionParse(requestJsonPointer) else {
        return yuiVisionError("invalid_request", "Platform vision request is not valid JSON.")
    }

    let imagePath = (request["image_path"] as? String) ?? ""
    guard FileManager.default.fileExists(atPath: imagePath) else {
        return yuiVisionError("image_missing", "Selected image file was not found.")
    }

    let imageUrl = URL(fileURLWithPath: imagePath)
    let handler = VNImageRequestHandler(url: imageUrl, options: [:])
    var labels = [[String: Any]]()
    var recognizedText = [String]()
    var requestError: Error?

    let classifyRequest = VNClassifyImageRequest { request, error in
        if let error {
            requestError = error
            return
        }

        let observations = (request.results as? [VNClassificationObservation]) ?? []
        for observation in observations.prefix(8) where observation.confidence >= 0.10 {
            labels.append([
                "identifier": observation.identifier,
                "confidence": observation.confidence
            ])
        }
    }

    let textRequest = VNRecognizeTextRequest { request, error in
        if let error {
            requestError = error
            return
        }

        let observations = (request.results as? [VNRecognizedTextObservation]) ?? []
        for observation in observations.prefix(12) {
            if let candidate = observation.topCandidates(1).first, candidate.confidence >= 0.30 {
                recognizedText.append(candidate.string)
            }
        }
    }
    textRequest.recognitionLevel = .fast
    textRequest.usesLanguageCorrection = true
    textRequest.recognitionLanguages = ["ja-JP", "en-US"]

    do {
        try handler.perform([classifyRequest, textRequest])
    } catch {
        return yuiVisionError("vision_error", error.localizedDescription)
    }

    if let requestError {
        return yuiVisionError("vision_error", requestError.localizedDescription)
    }

    let labelNames = labels
        .compactMap { $0["identifier"] as? String }
        .map { yuiVisionLocalizedLabel($0) }
    let text = recognizedText.joined(separator: "\n")
    let summary = yuiVisionSummary(labels: labelNames, recognizedText: text)
    return yuiVisionCString(yuiVisionJson([
        "ok": true,
        "summary": summary,
        "labels": labelNames,
        "recognized_text": text
    ]))
}

@_cdecl("YuiPlatformVisionBridge_Free")
public func YuiPlatformVisionBridge_Free(_ pointer: UnsafeMutablePointer<CChar>?) {
    pointer?.deallocate()
}

private func yuiVisionSummary(labels: [String], recognizedText: String) -> String {
    var parts = [String]()
    if labels.isEmpty {
        parts.append("画像の主要な被写体は特定できませんでした。")
    } else {
        parts.append("主な候補として、\(labels.prefix(5).joined(separator: "、"))が検出されています。")
    }

    let text = recognizedText.trimmingCharacters(in: .whitespacesAndNewlines)
    if !text.isEmpty {
        parts.append("画像内の文字として「\(text.prefix(160))」を読み取りました。")
    }

    return parts.joined(separator: " ")
}

private func yuiVisionLocalizedLabel(_ identifier: String) -> String {
    let normalized = identifier
        .lowercased()
        .replacingOccurrences(of: "_", with: " ")
        .trimmingCharacters(in: .whitespacesAndNewlines)
    let labels = [
        "wine": "ワイン",
        "wine bottle": "ワインボトル",
        "bottle": "ボトル",
        "container": "容器",
        "liquid": "液体",
        "drink": "飲み物",
        "beverage": "飲み物",
        "alcohol": "アルコール飲料",
        "glass": "グラス",
        "tableware": "食器",
        "plate": "皿",
        "dish": "皿",
        "fork": "フォーク",
        "spoon": "スプーン",
        "cup": "カップ",
        "coffee": "コーヒー",
        "person": "人物",
        "face": "顔",
        "food": "食べ物",
        "cake": "ケーキ",
        "dessert": "デザート",
        "mousse": "ムース",
        "table": "テーブル",
        "text": "文字",
        "document": "書類",
        "screen": "画面",
        "computer": "コンピューター",
        "phone": "スマートフォン",
        "animal": "動物",
        "cat": "猫",
        "adult cat": "成猫",
        "domestic cat": "飼い猫",
        "dog": "犬"
    ]
    return labels[normalized] ?? identifier
}
