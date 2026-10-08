import Foundation
import CoreML
import CryptoKit

// Keep a stable compiled-model URL so CoreML can reuse device specialization
// after process exit. Temporary compile URLs defeat that persistent cache.
enum YuiCoreMLModelCache {
    static func load(package: URL, configuration: MLModelConfiguration) throws -> (MLModel, URL) {
        let files = FileManager.default
        let keys: [URLResourceKey] = [.isRegularFileKey, .fileSizeKey, .contentModificationDateKey]
        let entries = (files.enumerator(at: package, includingPropertiesForKeys: keys)?.allObjects as? [URL] ?? [])
            .sorted { $0.path < $1.path }
        var signature = package.lastPathComponent
        for entry in entries {
            let info = try entry.resourceValues(forKeys: Set(keys))
            if info.isRegularFile == true {
                signature += "\n\(entry.path.dropFirst(package.path.count)):\(info.fileSize ?? 0):\(info.contentModificationDate?.timeIntervalSince1970 ?? 0)"
            }
        }
        let digest = SHA256.hash(data: Data(signature.utf8)).map { String(format: "%02x", $0) }.joined()
        let base = try files.url(for: .cachesDirectory, in: .userDomainMask, appropriateFor: nil, create: true)
            .appendingPathComponent("yui-irodori-audioseal-v1", isDirectory: true)
        try files.createDirectory(at: base, withIntermediateDirectories: true)
        let cached = base.appendingPathComponent(digest + ".mlmodelc", isDirectory: true)
        if files.fileExists(atPath: cached.path) {
            do {
                let model = try MLModel(contentsOf: cached, configuration: configuration)
                NSLog("Yui AudioSeal compiled cache hit %@", package.lastPathComponent)
                return (model, cached)
            } catch {
                // An interrupted write or invalid cache should not permanently break speech.
                try files.removeItem(at: cached)
            }
        }
        let temporary = try MLModel.compileModel(at: package)
        defer { try? files.removeItem(at: temporary) }
        do { try files.moveItem(at: temporary, to: cached) }
        catch { if !files.fileExists(atPath: cached.path) { throw error } }
        do { return (try MLModel(contentsOf: cached, configuration: configuration), cached) }
        catch { try? files.removeItem(at: cached); throw error }
    }
}
