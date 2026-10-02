"""Regression tests for iOS recognition snapshots, independent of Speech hardware."""
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]


class SpeechTimelineTests(unittest.TestCase):
    @unittest.skipUnless(shutil.which("swift"), "Swift toolchain required")
    def test_utterance_commits_and_provisional_timing(self):
        source = (ROOT / "unity/Assets/Plugins/iOS/YuiPlatformSpeechBridge.swift").read_text()
        helper = source[source.index("struct YuiSpeechTranscriptTimeline {"):
                        source.index('@_cdecl("YuiPlatformSpeechBridge_Transcribe")')]
        checks = r'''
typealias S = YuiSpeechTranscriptTimeline.Segment
let prefix = "シンガポール観光について質問。空港の周りに見るべきところがあります。"
let suffix = "そして何より観光目的で行く国ではないと聞きました。本当ですか？"
var t = YuiSpeechTranscriptTimeline()
// Apple can commit an utterance with metadata while isFinal is still false.
t.update([S(start:0,duration:0,text:"シンガポール感光")], isFinal:false, hasMetadata:false)
t.update([S(start:0,duration:12,text:prefix)], isFinal:false, hasMetadata:true)
// The NEXT utterance starts from zero in provisional callbacks. These must
// neither erase committed speech nor be appended as independent audio ranges.
t.update([S(start:0,duration:0,text:"そして何より観光")], isFinal:false, hasMetadata:false)
t.update([S(start:0,duration:0,text:suffix)], isFinal:false, hasMetadata:false)
assert(t.segments.count == 1)
assert(t.text == prefix + suffix)
t.update([S(start:15,duration:7,text:suffix)], isFinal:false, hasMetadata:true)
t.update([S(start:15,duration:7,text:suffix)], isFinal:true, hasMetadata:true)
assert(t.text == prefix + suffix)
assert(t.segments.count == 2)
print("PASS: zero-time suffix preserves prefix and commits exactly once")

// Provisional times can also be nonzero and drift. They are not evidence of
// a new audio range, regardless of the apparent gap from the previous result.
var drift = YuiSpeechTranscriptTimeline()
drift.update([S(start:0,duration:5,text:"先に話した内容。")], isFinal:false, hasMetadata:true)
drift.update([S(start:0.2,duration:1,text:"後に話した仮の内容")], isFinal:false, hasMetadata:false)
drift.update([S(start:30,duration:2,text:"後に話した訂正内容")], isFinal:false, hasMetadata:false)
drift.update([S(start:6,duration:3,text:"後に話した内容。")], isFinal:true, hasMetadata:true)
assert(drift.text == "先に話した内容。後に話した内容。")
print("PASS: drifting partial timestamps cannot replace committed ranges")

// Actual repetitions in different audio ranges must survive. Text-based
// deduplication would silently alter what the user said.
var repeated = YuiSpeechTranscriptTimeline()
repeated.update([S(start:0,duration:2,text:"本当ですか？")], isFinal:false, hasMetadata:true)
repeated.update([S(start:0,duration:0,text:"本当ですか？")], isFinal:false, hasMetadata:false)
repeated.update([S(start:3,duration:2,text:"本当ですか？")], isFinal:true, hasMetadata:true)
assert(repeated.text == "本当ですか？本当ですか？")
print("PASS: real repeated speech is retained")

// Revisions of committed hypotheses and a cumulative final result replace
// the same audio range instead of appending another copy.
var revision = YuiSpeechTranscriptTimeline()
revision.update([S(start:0,duration:2,text:"最初の文。")], isFinal:false, hasMetadata:true)
revision.update([S(start:3,duration:2,text:"見て終わりでなく")], isFinal:false, hasMetadata:true)
revision.update([S(start:3,duration:2.2,text:"見て終わりではなく、")], isFinal:false, hasMetadata:true)
assert(revision.text == "最初の文。見て終わりではなく、")
revision.update([S(start:0,duration:5.2,text:"全文の最終訂正。")], isFinal:true, hasMetadata:true)
assert(revision.text == "全文の最終訂正。")
print("PASS: range revisions and cumulative final replace earlier hypotheses")

// An empty final callback after a trailing pause must not erase speech.
revision.update([], isFinal:true, hasMetadata:false)
assert(revision.text == "全文の最終訂正。")
var pending = YuiSpeechTranscriptTimeline()
pending.update([S(start:0,duration:2,text:"確定した文。")], isFinal:false, hasMetadata:true)
pending.update([S(start:0,duration:0,text:"仮")], isFinal:false, hasMetadata:false)
pending.update([S(start:0,duration:0,text:"末尾の訂正。")], isFinal:false, hasMetadata:false)
pending.update([], isFinal:true, hasMetadata:false)
assert(pending.text == "確定した文。末尾の訂正。")
var empty = YuiSpeechTranscriptTimeline()
empty.update([], isFinal:true, hasMetadata:false)
assert(empty.text.isEmpty)
print("PASS: trailing empty callback, hypothesis replacement, empty recording")

// Replay actual iPhone 16 Pro / iOS 26.6.2 callbacks from synthesized speech.
let fixtureData = try Data(contentsOf: URL(fileURLWithPath: CommandLine.arguments[1]))
let fixture = try JSONSerialization.jsonObject(with: fixtureData) as! [String: Any]
var device = YuiSpeechTranscriptTimeline()
for row in fixture["snapshots"] as! [[String: Any]] {
    let formatted = row["text"] as! NSString
    let sourceSegments = row["segments"] as! [[String: Any]]
    let parts = sourceSegments.enumerated().map { index, part in
        let start = index == 0 ? 0 : part["location"] as! Int
        let end = index + 1 < sourceSegments.count ? sourceSegments[index + 1]["location"] as! Int : formatted.length
        return S(start: part["start"] as! Double, duration: part["duration"] as! Double,
                 text: formatted.substring(with: NSRange(location: start, length: end - start)))
    }
    device.update(parts, isFinal: row["final"] as! Bool, hasMetadata: row["hasMetadata"] as! Bool)
}
assert(device.text.hasPrefix("シンガポール観光について質問"))
assert(device.text.contains("空港の周り"))
assert(device.text.contains("1泊2日"))
assert(device.text.hasSuffix("本当ですか？"))
assert(device.text.components(separatedBy: "観光目的").count == 2)
assert(device.text == fixture["expectedText"] as! String)
print("PASS: actual iPhone callback replay retains all three utterances once")
'''
        with tempfile.TemporaryDirectory() as directory:
            script = Path(directory) / "timeline.swift"
            script.write_text("import Foundation\n" + helper + checks)
            result = subprocess.run(
                ["swift", "-module-cache-path", str(Path(directory) / "cache"), str(script),
                 str(ROOT / "tests/fixtures/ios_speech_pause_callbacks.json")],
                capture_output=True, text=True, timeout=60)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(result.stdout.count("PASS:"), 6, result.stdout)


if __name__ == "__main__":
    unittest.main()
