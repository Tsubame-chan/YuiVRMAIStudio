import Foundation
import BackgroundAssets
import System

private let yuiAssetLock = NSLock()
private var yuiAssetState = "{\"state\":\"idle\",\"progress\":0}"
private var yuiAssetTask: Task<Void, Never>?
private var yuiAssetGeneration = 0
private var yuiAssetTerminal = false
private var yuiActiveDownload: BADownload?
@_cdecl("YuiAppleAssets_Cancel")
public func YuiAppleAssets_Cancel() {
    yuiAssetLock.lock(); let task = yuiAssetTask; let download = yuiActiveDownload; yuiAssetLock.unlock()
    task?.cancel()
    if let download { try? BADownloadManager.shared.cancel(download) }
}
private func setYuiAssetState(_ state: String, _ progress: Double = -1, _ message: String = "", generation: Int) {
    yuiAssetLock.lock(); defer { yuiAssetLock.unlock() }
    // A delayed progress callback must not undo completion, including after retry.
    guard generation == yuiAssetGeneration, !yuiAssetTerminal else { return }
    yuiAssetTerminal = state == "ready" || state == "failed" || state == "cancelled"
    if yuiAssetTerminal { yuiAssetTask = nil; yuiActiveDownload = nil }
    let fraction = progress.isFinite ? min(1, max(-1, progress)) : -1
    let data = try! JSONSerialization.data(withJSONObject: ["state": state, "progress": fraction, "message": message])
    yuiAssetState = String(data: data, encoding: .utf8)!
}
@_cdecl("YuiAppleAssets_Enabled")
public func YuiAppleAssets_Enabled() -> Int32 {
    if #available(iOS 26.0, *), Bundle.main.object(forInfoDictionaryKey: "BAUsesAppleHosting") as? Bool == true { return 1 }
    return 0
}
@_cdecl("YuiAppleAssets_Path")
public func YuiAppleAssets_Path(_ relative: UnsafePointer<CChar>?) -> UnsafeMutablePointer<CChar>? {
    guard YuiAppleAssets_Enabled() == 1, let relative else { return nil }
    let path = String(cString: relative)
    guard !path.hasPrefix("/"), !path.split(separator: "/").contains("..") else { return nil }
    if #available(iOS 26.0, *), let url = try? AssetPackManager.shared.url(for: FilePath(path)),
       FileManager.default.fileExists(atPath: url.path) { return strdup(url.path) }
    return nil
}
@_cdecl("YuiAppleAssets_Free")
public func YuiAppleAssets_Free(_ pointer: UnsafeMutablePointer<CChar>?) { free(pointer) }
@_cdecl("YuiAppleAssets_Status")
public func YuiAppleAssets_Status() -> UnsafeMutablePointer<CChar>? {
    yuiAssetLock.lock(); defer { yuiAssetLock.unlock() }
    return strdup(yuiAssetState)
}
@_cdecl("YuiAppleAssets_Prepare")
public func YuiAppleAssets_Prepare(_ assetPackId: UnsafePointer<CChar>?) {
    guard let assetPackId else { return }
    let id = String(cString: assetPackId)
    guard !id.isEmpty else { return }
    guard YuiAppleAssets_Enabled() == 1 else { return }
    yuiAssetLock.lock(); defer { yuiAssetLock.unlock() }
    guard yuiAssetTask == nil else { return }
    yuiAssetGeneration += 1
    let generation = yuiAssetGeneration
    yuiAssetTerminal = false
    yuiAssetState = "{\"state\":\"downloading\",\"progress\":-1}"
    yuiAssetTask = Task {
        if #available(iOS 26.0, *) {
            setYuiAssetState("downloading", generation: generation)
            let observer = Task {
                for await update in AssetPackManager.shared.statusUpdates(forAssetPackWithID: id) {
                    if Task.isCancelled { break }
                    if case .downloading(_, let progress) = update { setYuiAssetState("downloading", progress.fractionCompleted, generation: generation) }
                    if case .paused = update { setYuiAssetState("paused", -1, "Waiting for the system to resume the download.", generation: generation) }
                }
            }
            defer { observer.cancel() }
            do {
                // Called only after user consent; the registered on-demand pack
                // is managed and resumed by the system.
                let pack = try await AssetPackManager.shared.assetPack(withID: id)
                try Task.checkCancellation()
                setYuiActiveDownload(pack.download(for: nil))
                try Task.checkCancellation()
                try await AssetPackManager.shared.ensureLocalAvailability(of: pack)
                try Task.checkCancellation()
                setYuiAssetState("ready", 1, generation: generation)
            } catch { setYuiAssetState(Task.isCancelled ? "cancelled" : "failed", 0, error.localizedDescription, generation: generation) }
        }
    }
}
private func setYuiActiveDownload(_ download: BADownload) {
    yuiAssetLock.lock(); defer { yuiAssetLock.unlock() }; yuiActiveDownload = download
}
