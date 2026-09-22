"""Regression tests for iOS recognition snapshots, independent of Speech hardware."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]

class SpeechTimelineTests(unittest.TestCase):
    @unittest.skipUnless(shutil.which("swift"), "Swift toolchain required")
    def test_pause_revision_and_zero_duration_snapshots(self):
        source = (ROOT / "unity/Assets/Plugins/iOS/YuiPlatformSpeechBridge.swift").read_text()
        helper = source[source.index("struct YuiSpeechTranscriptTimeline {"):source.index('@_cdecl("YuiPlatformSpeechBridge_Transcribe")')]
        checks = 'typealias S = YuiSpeechTranscriptTimeline.Segment\nvar t = YuiSpeechTranscriptTimeline()\nt.update([S(start:0,duration:1,text:"音声が途切れました。"),S(start:1.2,duration:1,text:"シンガポール旅行は")])\nt.update([S(start:3,duration:1,text:"マーライオンを"),S(start:4.2,duration:1,text:"見て終わりでなく")])\nassert(t.text == "音声が途切れました。シンガポール旅行はマーライオンを見て終わりでなく")\nt.update([S(start:3,duration:1,text:"マーライオンを"),S(start:4.2,duration:1.2,text:"見て終わりではなく、")])\nassert(t.text == "音声が途切れました。シンガポール旅行はマーライオンを見て終わりではなく、")\nt.update([])\nassert(t.segments.count == 4)\nt.update([S(start:0,duration:5.4,text:"全文の最終訂正")])\nassert(t.text == "全文の最終訂正")\nprint("PASS: pause suffix retention, hypothesis revision, empty snapshot, full final replacement")\n\nvar zero = YuiSpeechTranscriptTimeline()\nzero.update([S(start:0,duration:0,text:"仮")])\nzero.update([S(start:0,duration:0,text:"訂正")])\nassert(zero.text == "訂正")\nprint("PASS: zero-duration partial replacement")\n'
        with tempfile.TemporaryDirectory() as directory:
            script = Path(directory) / "timeline.swift"
            script.write_text("import Foundation\n" + helper + checks)
            result = subprocess.run(["swift", "-module-cache-path", str(Path(directory) / "cache"), str(script)], capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stderr)

if __name__ == "__main__":
    unittest.main()
