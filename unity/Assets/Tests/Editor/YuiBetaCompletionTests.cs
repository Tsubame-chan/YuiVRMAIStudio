using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using NUnit.Framework;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiBetaCompletionTests
    {
        [Test] public void WorkModePreservesItsIdentityAndPermitsUsefulFormattedResults()
        {
            var prepared = YuiLocalAiPromptBuilder.PrepareChatRequest(new YuiLocalAiChatRequest {
                CharacterName = "星", Mode = "work", Message = "手順を箇条書きで", CustomInstruction = "落ち着いて話す" }, true);
            Assert.AreEqual("work", prepared.Mode);
            StringAssert.Contains("星", prepared.SystemInstruction);
            StringAssert.Contains("必要なら箇条書き", prepared.SystemInstruction);
            StringAssert.DoesNotContain("40〜80字", prepared.SystemInstruction);
            StringAssert.Contains("落ち着いて話す", prepared.Prompt);
        }
        [TestCase("```csharp\nif (ready) { Run(); }\n```")]
        [TestCase("{\"text\":\"This is user-requested JSON\"}")]
        public void LocalWorkKeepsCodeAndJsonInsteadOfApplyingTalkCleanup(string text)
        {
            var result = YuiLocalAiBackendCompatibility.ToChatResponse(new YuiLocalAiChatResponse {Text=text, Success=true, ShouldTts=true}, "work");
            Assert.AreEqual(text,result.Text);
            Assert.AreEqual("作業結果を画面にまとめたよ。",result.SpokenText);
            Assert.IsTrue(result.ShouldTts);
        }
        [Test] public void CorruptInstallLedgerRecoversBackupWithoutRewritingOriginal()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try {
                var path = Path.Combine(dir, "ledger.json");
                var ledger = new YuiLocalAiInstalledAssetLedger { ReleaseVersion = "first" }; ledger.Save(path);
                ledger.ReleaseVersion = "second"; ledger.Save(path); File.WriteAllText(path, "{broken");
                Assert.AreEqual("first", YuiLocalAiInstalledAssetLedger.Load(path).ReleaseVersion);
                Assert.AreEqual("{broken", File.ReadAllText(path));
            } finally { Directory.Delete(dir, true); }
        }
        [Test] public void AndroidPackagedVoiceExtractionIsBoundedAndPreservesOldFilesOnFailure()
        {
            var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try {
                var zip = Path.Combine(dir, "base.apk"); var dest = Path.Combine(dir, "Voicevox");
                using (var a = ZipFile.Open(zip, ZipArchiveMode.Create)) {
                    foreach (var name in new[] { "Models/meimei_himari_1.vvm", "open_jtalk_dic_utf_8-1.11/sys.dic" }) {
                        using var w = new StreamWriter(a.CreateEntry("assets/YuiLocalAI/Voicevox/" + name).Open()); w.Write("voice-data");
                    }
                    using var ignored = new StreamWriter(a.CreateEntry("assets/private.txt").Open()); ignored.Write("other-data");
                }
                YuiPackagedVoicevoxAssets.Extract(zip, dest, "1", CancellationToken.None);
                Assert.AreEqual("1", File.ReadAllText(Path.Combine(dest, ".bundled-version")));
                Assert.IsFalse(File.Exists(Path.Combine(dest, "private.txt")));
                using var stop = new CancellationTokenSource(); stop.Cancel();
                Assert.Throws<OperationCanceledException>(() => YuiPackagedVoicevoxAssets.Extract(zip, dest, "2", stop.Token));
                Assert.AreEqual("voice-data", File.ReadAllText(Path.Combine(dest, "Models/meimei_himari_1.vvm")));
                var broken = Path.Combine(dir, "broken.apk"); using (var a = ZipFile.Open(broken, ZipArchiveMode.Create)) a.CreateEntry("assets/YuiLocalAI/Voicevox/../../escape");
                Assert.Throws<InvalidDataException>(() => YuiPackagedVoicevoxAssets.Extract(broken, dest, "2", CancellationToken.None));
                Assert.AreEqual("1", File.ReadAllText(Path.Combine(dest, ".bundled-version")));
            } finally { Directory.Delete(dir, true); }
        }
    }
}
