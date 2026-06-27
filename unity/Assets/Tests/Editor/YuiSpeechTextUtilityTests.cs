using NUnit.Framework;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiSpeechTextUtilityTests
    {
        [Test]
        public void CleanSpeechText_RemovesRawUrls()
        {
            var cleaned = YuiSpeechTextUtility.CleanSpeechText(
                "詳細はこちらです。https://example.com/path を見てね。");

            Assert.That(cleaned, Does.Not.Contain("https://"));
            Assert.That(cleaned, Is.EqualTo("詳細はこちらです。 を見てね。"));
        }

        [Test]
        public void CleanSpeechText_RemovesDomainOnlyCitationLabels()
        {
            var cleaned = YuiSpeechTextUtility.CleanSpeechText(
                "中央区観光協会の情報です。([chuo-kanko.or.jp])");

            Assert.That(cleaned, Is.EqualTo("中央区観光協会の情報です。"));
        }

        [Test]
        public void CleanSpeechText_LeavesNormalParenthesizedText()
        {
            var cleaned = YuiSpeechTextUtility.CleanSpeechText(
                "浜町公園（中央区）で開催予定です。");

            Assert.That(cleaned, Is.EqualTo("浜町公園 中央区 で開催予定です。"));
        }

        [Test]
        public void SplitSpeechText_CanKeepShortSentencesTogetherForSlowTts()
        {
            var chunks = YuiSpeechTextUtility.SplitSpeechText(
                "ほどほどに元気ならよかった。開発は頭も使うし、結構しんどいよね。無理しすぎないでね。少し休憩しながら進めよっか。",
                180,
                80,
                80);

            Assert.That(chunks, Has.Length.EqualTo(2));
            Assert.That(chunks[0], Does.Contain("開発は頭も使うし"));
            Assert.That(chunks[1], Does.Contain("少し休憩しながら"));
        }

    }
}
