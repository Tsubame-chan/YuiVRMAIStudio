import Foundation
import IrodoriTTS

private let lock = NSLock()
private var running: Task<Void, Never>?
private var cancelled = false
// Unity serializes prepare/synthesis/release through one gate. Keep weights between utterances.
private let engine = IrodoriEngine()

// Called only on the Unity worker thread; never block its UI thread.
@_cdecl("YuiIrodori_Synthesize")
public func synthesize(_ root: UnsafePointer<CChar>, _ text: UnsafePointer<CChar>, _ caption: UnsafePointer<CChar>, _ reference: UnsafePointer<CChar>, _ output: UnsafePointer<CChar>) -> UnsafeMutablePointer<CChar>? {
    let model = String(cString: root), words = String(cString: text)
    let direction = String(cString: caption), destination = String(cString: output)
    let referenceURL = URL(fileURLWithPath: String(cString: reference))
    let done = DispatchSemaphore(value: 0)
    var failure: String?
    lock.lock()
    let task = Task.detached {
        do {
            try Task.checkCancellation()
            let loadMs = try await engine.prepare(modelDirectory: URL(fileURLWithPath: model))
            try Task.checkCancellation()
            let reference = try await engine.registerReference(referenceURL)
            NSLog("Yui Irodori load_ms=%.2f reference_ms=%.2f reference_cache=%@", loadMs, reference.milliseconds, reference.cacheHit.description)
            // Keep SDK sentence splitting: the raw multi-sentence probe lost voice character in listening tests.
            let audio = try await engine.synthesize(words, caption: direction, rawText: false, splitSentences: true)
            try Task.checkCancellation()
            try audio.writeWAV(to: URL(fileURLWithPath: destination))
        } catch { failure = error.localizedDescription }
        done.signal()
    }
    running = task; if cancelled { task.cancel() }; lock.unlock()
    done.wait()
    lock.lock(); running = nil; lock.unlock()
    return failure.flatMap { strdup($0) }
}
@_cdecl("YuiIrodori_Reset") public func reset() { lock.lock(); cancelled = false; lock.unlock() }
@_cdecl("YuiIrodori_Cancel") public func cancel() { lock.lock(); cancelled = true; running?.cancel(); lock.unlock() }
@_cdecl("YuiIrodori_Free") public func freeError(_ p: UnsafeMutablePointer<CChar>?) { free(p) }

// Call after acquiring Unity's gate; release on memory pressure, never on every utterance.
@_cdecl("YuiIrodori_Release") public func releaseEngine() {
    let done = DispatchSemaphore(value: 0)
    Task.detached { await engine.release(); done.signal() }
    done.wait()
}

@_cdecl("YuiIrodori_Prepare")
public func prepareEngine(_ root: UnsafePointer<CChar>, _ reference: UnsafePointer<CChar>) -> UnsafeMutablePointer<CChar>? {
    let model = URL(fileURLWithPath: String(cString: root))
    let reference = URL(fileURLWithPath: String(cString: reference))
    let done = DispatchSemaphore(value: 0)
    var failure: String?
    lock.lock()
    let task = Task.detached {
        do {
            try Task.checkCancellation()
            let loadMs = try await engine.prepare(modelDirectory: model)
            try Task.checkCancellation()
            let registration = try await engine.registerReference(reference)
            NSLog("Yui Irodori prepare load_ms=%.2f reference_ms=%.2f", loadMs, registration.milliseconds)
        } catch { failure = error.localizedDescription }
        done.signal()
    }
    running = task; if cancelled { task.cancel() }; lock.unlock()
    done.wait()
    lock.lock(); running = nil; lock.unlock()
    return failure.flatMap { strdup($0) }
}
