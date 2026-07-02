using System.Collections.Generic;
using NUnit.Framework;
using YuiPhysicalAI.Api;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiTtsVoiceOptionCatalogTests
    {
        [Test]
        public void OptionsForMode_UsesAllBackendAivisVoices()
        {
            var options = YuiTtsVoiceOptionCatalog.OptionsForMode(
                "aivis",
                new List<TtsVoiceOption>
                {
                    new TtsVoiceOption { Id = 1431611904, Label = "女性ボイス①" },
                    new TtsVoiceOption { Id = 888753760, Label = "女性ボイス②" },
                    new TtsVoiceOption { Id = 888753761, Label = "中性的ボイス" },
                    new TtsVoiceOption { Id = 888753762, Label = "男性ボイス" },
                });

            Assert.AreEqual(4, options.Count);
            Assert.AreEqual(1431611904, options[0].Id);
            Assert.AreEqual("女性ボイス②", options[1].Label);
            Assert.AreEqual(888753762, options[3].Id);
        }

        [Test]
        public void OptionsForMode_FallsBackToEmbeddedAivisVoiceWhenBackendHasNoVoices()
        {
            var options = YuiTtsVoiceOptionCatalog.OptionsForMode("aivis", new List<TtsVoiceOption>());

            Assert.AreEqual(1, options.Count);
            Assert.AreEqual(1431611904, options[0].Id);
        }

        [Test]
        public void OptionsForMode_UsesOnlyEmbeddedAivisVoiceForNativeAivis()
        {
            var options = YuiTtsVoiceOptionCatalog.OptionsForMode(
                "aivis-native",
                new List<TtsVoiceOption>
                {
                    new TtsVoiceOption { Id = 1431611904, Label = "女性ボイス①" },
                    new TtsVoiceOption { Id = 888753760, Label = "女性ボイス②" },
                    new TtsVoiceOption { Id = 888753761, Label = "中性的ボイス" },
                    new TtsVoiceOption { Id = 888753762, Label = "男性ボイス" },
                });

            Assert.AreEqual(1, options.Count);
            Assert.AreEqual(1431611904, options[0].Id);
            Assert.AreEqual("女性ボイス①", options[0].Label);
        }

        [Test]
        public void OptionsForMode_KeepsVoicevoxVoicesForServerMode()
        {
            var options = YuiTtsVoiceOptionCatalog.OptionsForMode(
                "server",
                new List<TtsVoiceOption>
                {
                    new TtsVoiceOption { Id = 1431611904, Label = "女性ボイス①" },
                });

            Assert.Greater(options.Count, 10);
            Assert.AreEqual(14, options[0].Id);
        }
    }
}
