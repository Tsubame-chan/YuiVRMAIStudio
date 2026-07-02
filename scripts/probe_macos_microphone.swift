import AVFoundation
import Foundation

let outputURL = URL(fileURLWithPath: "/private/tmp/yui_mic_probe.wav")
try? FileManager.default.removeItem(at: outputURL)

let semaphore = DispatchSemaphore(value: 0)
var granted = false

if #available(macOS 10.14, *) {
    AVCaptureDevice.requestAccess(for: .audio) { ok in
        granted = ok
        semaphore.signal()
    }
    _ = semaphore.wait(timeout: .now() + 30)
} else {
    granted = true
}

print("permission_granted=\(granted)")
if !granted {
    exit(2)
}

let settings: [String: Any] = [
    AVFormatIDKey: Int(kAudioFormatLinearPCM),
    AVSampleRateKey: 44100,
    AVNumberOfChannelsKey: 1,
    AVLinearPCMBitDepthKey: 16,
    AVLinearPCMIsFloatKey: false,
    AVLinearPCMIsBigEndianKey: false
]

let recorder = try AVAudioRecorder(url: outputURL, settings: settings)
recorder.isMeteringEnabled = true
guard recorder.record(forDuration: 3.0) else {
    print("record_started=false")
    exit(3)
}

print("record_started=true")
Thread.sleep(forTimeInterval: 3.4)
recorder.stop()

let data = try Data(contentsOf: outputURL)
let samplesStart = 44
var sum = 0.0
var peak = 0.0
var count = 0
if data.count > samplesStart {
    var index = samplesStart
    while index + 1 < data.count {
        let lo = UInt16(data[index])
        let hi = UInt16(data[index + 1]) << 8
        let raw = Int16(bitPattern: hi | lo)
        let value = abs(Double(raw) / 32768.0)
        sum += value * value
        peak = max(peak, value)
        count += 1
        index += 2
    }
}

let rms = count > 0 ? sqrt(sum / Double(count)) : 0.0
print("file=\(outputURL.path)")
print("bytes=\(data.count)")
print(String(format: "rms=%.8f", rms))
print(String(format: "peak=%.8f", peak))
