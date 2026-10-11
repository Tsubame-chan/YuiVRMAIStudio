"""Execute the production native request state without requiring Speech hardware."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class SpeechCancellationTests(unittest.TestCase):
    @unittest.skipUnless(shutil.which("swift"), "Swift toolchain required")
    def test_native_cancellation_completion_and_ownership(self):
        source = (ROOT / "unity/Assets/Plugins/iOS/YuiPlatformSpeechBridge.swift").read_text()
        helper = source[source.index("final class YuiSpeechOperation {"):
                        source.index('@_cdecl("YuiPlatformSpeechBridge_Synthesize")')]
        checks = r'''
func pump(until predicate: () -> Bool) {
    let deadline = Date().addingTimeInterval(2)
    while !predicate() && Date() < deadline { RunLoop.main.run(until: Date().addingTimeInterval(0.001)) }
    assert(predicate())
}
let early = YuiSpeechOperation()
early.cancel()
var earlyStopped = 0
early.installStop { earlyStopped += 1 }
assert(early.semaphore.wait(timeout: .now()) == .success)
pump { earlyStopped == 1 }
assert(early.completed && early.cancelled)
print("PASS: cancel before native startup wakes and stops a later-installed task")

let active = YuiSpeechOperation()
var stopped = 0
active.installStop { stopped += 1 }
DispatchQueue.global().async { active.cancel() }
assert(active.semaphore.wait(timeout: .now() + 2) == .success)
pump { stopped == 1 }
active.lock.lock()
assert(active.completed && active.cancelled)
active.lock.unlock()
active.cancel()
assert(active.semaphore.wait(timeout: .now()) == .timedOut)
print("PASS: active cancellation wakes once without an OS callback")

for _ in 0..<500 {
    let racing = YuiSpeechOperation()
    let group = DispatchGroup()
    group.enter()
    DispatchQueue.global().async { racing.cancel(); group.leave() }
    group.enter()
    DispatchQueue.global().async {
        racing.lock.lock()
        if !racing.completed { racing.finishLocked() }
        racing.lock.unlock()
        group.leave()
    }
    assert(group.wait(timeout: .now() + 2) == .success)
    assert(racing.semaphore.wait(timeout: .now()) == .success)
    assert(racing.semaphore.wait(timeout: .now()) == .timedOut)
}
print("PASS: racing completion and cancellation settle exactly once")

"one".withCString { YuiPlatformSpeechBridge_Begin($0) }
"two".withCString { YuiPlatformSpeechBridge_Begin($0) }
let one = yuiSpeechOperation(["request_id":"one"])
let two = yuiSpeechOperation(["request_id":"two"])
"one".withCString { YuiPlatformSpeechBridge_Cancel($0) }
assert(one.cancelled && !two.completed)
"one".withCString { YuiPlatformSpeechBridge_End($0); YuiPlatformSpeechBridge_Cancel($0) }
"two".withCString { YuiPlatformSpeechBridge_End($0) }
assert(yuiSpeechOperations.isEmpty)
print("PASS: request IDs isolate cancellation and registry ownership is released")

let expired = YuiSpeechOperation()
expired.expire()
var expiredStopped = false
expired.installStop { expiredStopped = true }
pump { expiredStopped }
assert(expired.completed && !expired.cancelled)
print("PASS: timeout also stops work that was queued but not yet started")
'''
        with tempfile.TemporaryDirectory() as directory:
            script = Path(directory) / "cancellation.swift"
            script.write_text("import Foundation\n" + helper + checks)
            result = subprocess.run(["swift", "-module-cache-path", str(Path(directory) / "cache"), str(script)],
                                    capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(result.stdout.count("PASS:"), 5, result.stdout)


if __name__ == "__main__":
    unittest.main()
