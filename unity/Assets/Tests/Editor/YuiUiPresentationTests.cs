using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using YuiPhysicalAI.UI;
using YuiPhysicalAI.Core;
namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiUiPresentationTests
    {
        [TestCase("", SystemLanguage.Japanese, "ja")]
        [TestCase("", SystemLanguage.English, "en")]
        [TestCase("", SystemLanguage.French, "en")]
        [TestCase("en", SystemLanguage.Japanese, "en")]
        [TestCase("ja", SystemLanguage.English, "ja")]
        public void Language_UsesSavedChoiceThenSystem(string saved,SystemLanguage system,string expected)
        { Assert.That(YuiUiLocalization.ResolveLanguage(saved,system), Is.EqualTo(expected)); }
        [Test]
        public void Language_FormatsAndReversesAppLabelsWithoutChangingIdentifiers()
        {
            Assert.That(YuiUiLocalization.Translate("Sources 6","ja"), Is.EqualTo("Sources 6"));
            Assert.That(YuiUiLocalization.Translate("Sources 6","en"), Is.EqualTo("Sources 6"));
            Assert.That(YuiUiLocalization.Translate("AivisSpeech · Backend","ja"), Is.EqualTo("AivisSpeech · Backend"));
            Assert.That(YuiUiLocalization.Translate("Custom VRM 2 · Empty","ja"), Is.EqualTo("カスタムVRM 2 · 空き"));
            Assert.That(YuiUiLocalization.Translate("server-http","ja"), Is.EqualTo("server-http"));
        }
        [Test]
        public void Language_UpdatesExistingAndHiddenLabelsAndPersistsOnlyItsPreference()
        {
            var had = PlayerPrefs.HasKey(YuiPrefsKeys.UiLanguage);
            var original = PlayerPrefs.GetString(YuiPrefsKeys.UiLanguage, "");
            var previousLanguage = YuiUiLocalization.Language;
            var mode = PlayerPrefs.GetString(YuiPrefsKeys.ConversationMode,"unchanged");
            var go = new GameObject("Locale test",typeof(RectTransform),typeof(Text));
            try
            {
                var label = go.GetComponent<Text>(); YuiUiLocalization.Set(label,"Settings");
                // EditMode does not dispatch enable callbacks for ordinary MonoBehaviours.
                EnableBinding(go, true);
                YuiUiLocalization.SetLanguage("ja"); Assert.That(label.text, Is.EqualTo("設定"));
                EnableBinding(go, false);go.SetActive(false);YuiUiLocalization.SetLanguage("en");go.SetActive(true);EnableBinding(go, true);
                Assert.That(label.text, Is.EqualTo("Settings"));
                Assert.That(PlayerPrefs.GetString(YuiPrefsKeys.UiLanguage), Is.EqualTo("en"));
                Assert.That(PlayerPrefs.GetString(YuiPrefsKeys.ConversationMode,"unchanged"), Is.EqualTo(mode));
            }
            finally
            {
                EnableBinding(go, false);Object.DestroyImmediate(go);YuiUiLocalization.SetLanguage(previousLanguage);
                if(had)PlayerPrefs.SetString(YuiPrefsKeys.UiLanguage,original);else PlayerPrefs.DeleteKey(YuiPrefsKeys.UiLanguage);
                PlayerPrefs.Save();
            }
        }
        private static void EnableBinding(GameObject go, bool enabled)
        {
            var method = typeof(YuiLocalizedLabel).GetMethod(enabled ? "OnEnable" : "OnDisable",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method.Invoke(go.GetComponent<YuiLocalizedLabel>(), null);
        }
        [Test]
        public void Language_KeepsConsoleCommandsEnglishAndLocalizesDetailedErrors()
        {
            foreach (var command in new[] { "Talk", "Work", "Mic", "Send", "API Mode ON", "Connected", "Secret Mode" })
                Assert.That(YuiUiLocalization.Translate(command,"ja"),Is.EqualTo(command));
            Assert.That(YuiUiLocalization.Translate("Backendエラー (502): provider failed","en"),Is.EqualTo("Backend error (502): provider failed"));
            Assert.That(YuiUiLocalization.Translate("対象: 3件。必要なデータをGitHub Releasesから取得します。","en"),Is.EqualTo("3 required downloads from GitHub Releases."));
            Assert.That(YuiUiLocalization.Translate("カメラ画像を取得しました: 640x480","en"),Is.EqualTo("Camera frame captured: 640x480"));
        }
        [Test]
        public void Language_LegacyErrorTemplatesUseCurrentPlatformNeutralRecoveryCopy()
        {
            const string oldMessage = "Backendに接続できません。scripts/run_backend.ps1 を起動してください。url=http://127.0.0.1:8000";
            Assert.That(YuiUiLocalization.Translate(oldMessage,"ja"),Is.EqualTo("Backendに接続できません。起動状態とURLを確認してください: http://127.0.0.1:8000"));
            Assert.That(YuiUiLocalization.Translate(oldMessage,"en"),Is.EqualTo("Could not connect to the backend. Start it and check this URL: http://127.0.0.1:8000"));
            Assert.That(YuiUiLocalization.Translate("マイクが見つかりません。WindowsとUnityのマイク設定を確認してください。","ja"),Is.EqualTo("マイクが見つかりません。端末とアプリの設定を確認してください。"));
        }
        [TestCase("Appearance", YuiUiTypography.Label, 52, 700)]
        [TestCase("キャラクター名", YuiUiTypography.Label, 52, 700)]
        [TestCase("Your character", YuiUiTypography.Heading, 66, 700)]
        [TestCase("あなたのキャラクター", YuiUiTypography.Heading, 66, 700)]
        [TestCase("キャラクター", 26, 88, 171)]
        [TestCase("Advanced", 28, 88, 171)]
        public void Typography_FitsBothLanguagesWithoutShrinking(string value,int size,int height,int width)
        {
            var go = new GameObject("Font metrics",typeof(RectTransform),typeof(Text));
            try
            {
                var text = go.GetComponent<Text>();text.font = YuiUiTypography.Regular;text.fontSize=size;
                var preferred = text.cachedTextGeneratorForLayout.GetPreferredHeight(value,text.GetGenerationSettings(new Vector2(width,0))) / text.pixelsPerUnit;
                Assert.That(preferred,Is.LessThanOrEqualTo(height),value);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test]
        public void Settings_LocalizedDeviceLabelsRetainOriginalDeviceIds()
        {
            var go = new GameObject("Settings locale values");
            var field = new GameObject("Mic",typeof(RectTransform),typeof(Dropdown));
            try
            {
                var settings=go.AddComponent<YuiSettingsOverlay>();var dropdown=field.GetComponent<Dropdown>();
                dropdown.options.Add(new Dropdown.OptionData("標準のマイク"));
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                typeof(YuiSettingsOverlay).GetField("microphoneDropdown",flags).SetValue(settings,dropdown);
                Assert.That(typeof(YuiSettingsOverlay).GetMethod("MicrophoneValue",flags).Invoke(settings,null),Is.EqualTo("Default"));
            }
            finally {Object.DestroyImmediate(field);Object.DestroyImmediate(go);}
        }
        [Test]
        public void Language_NeverBindsUserInputOrDropdownCaptionsEvenOnHiddenPages()
        {
            var root=new GameObject("Hidden settings page");
            var inputObject=new GameObject("Name",typeof(RectTransform),typeof(InputField));inputObject.transform.SetParent(root.transform);
            var valueObject=new GameObject("Text",typeof(RectTransform),typeof(Text));valueObject.transform.SetParent(inputObject.transform);
            var dropdownObject=new GameObject("Voice",typeof(RectTransform),typeof(Dropdown));dropdownObject.transform.SetParent(root.transform);
            var captionObject=new GameObject("Label",typeof(RectTransform),typeof(Text));captionObject.transform.SetParent(dropdownObject.transform);
            try
            {
                var value=valueObject.GetComponent<Text>();value.text="Settings";inputObject.GetComponent<InputField>().textComponent=value;
                var caption=captionObject.GetComponent<Text>();caption.text="Default";dropdownObject.GetComponent<Dropdown>().captionText=caption;
                root.SetActive(false);YuiUiLocalization.BindKnownLabels(root.transform);
                Assert.That(value.GetComponent<YuiLocalizedLabel>(),Is.Null);
                Assert.That(caption.GetComponent<YuiLocalizedLabel>(),Is.Null);
                Assert.That(value.text,Is.EqualTo("Settings"));
            }
            finally {Object.DestroyImmediate(root);}
        }
        [Test]
        public void Font_BundlesLatinAndJapaneseWithoutOperatingSystemDependencies()
        {
            var font = YuiUiTypography.Regular;
            Assert.That(font, Is.Not.Null);
            var importer = (TrueTypeFontImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(font));
            Assert.That(importer.includeFontData, Is.True);
            Assert.That(font.dynamic, Is.True);
            font.RequestCharactersInTexture("Connected 日本語 設定 会話 髪 衣装",30,FontStyle.Normal);
            foreach (var c in "Connected日本語設定会話髪衣装") Assert.That(font.HasCharacter(c),Is.True,"Missing " + c);
            Assert.That(YuiChatLogStyle.ResolveFont(null),Is.SameAs(font));
        }
        [TestCase("OpenAI APIやOpenAIのバックエンドでは、Web検索や都市を指定した天気の質問ができます。参照先はSourcesから開けます。端末内AIはWeb検索に非対応です。")]
        [TestCase("設定 → キャラクターで空のカスタムVRMを選び、「アバターを読み込む」。VRChat用アバターはUnityのYui Avatar BridgeでVRMを書き出し、この端末へコピーします。")]
        public void Typography_JapaneseHelpDoesNotOrphanTheLatinPrefix(string source)
        {
            var had=PlayerPrefs.HasKey(YuiPrefsKeys.UiLanguage);var saved=PlayerPrefs.GetString(YuiPrefsKeys.UiLanguage,"");var locale=YuiUiLocalization.Language;
            var go=new GameObject("Japanese wrapping",typeof(RectTransform),typeof(Text));
            try
            {
                YuiUiLocalization.SetLanguage("ja");var text=go.GetComponent<Text>();text.font=YuiUiTypography.Regular;text.fontSize=YuiUiTypography.Note;
                var display=YuiUiTypography.JapaneseParagraph(source);
                var settings=text.GetGenerationSettings(new Vector2(800,1000));
                text.cachedTextGenerator.Populate(display,settings);
                Assert.That(text.cachedTextGenerator.lines.Count,Is.GreaterThan(1));
                Assert.That(text.cachedTextGenerator.lines[1].startCharIdx,Is.GreaterThan(20),"Do not put only OpenAI or 設定 → on the first line.");
                Assert.That(display.Replace('\u00a0',' '),Is.EqualTo(source));
            }
            finally
            {
                Object.DestroyImmediate(go);YuiUiLocalization.SetLanguage(locale);
                if(had)PlayerPrefs.SetString(YuiPrefsKeys.UiLanguage,saved);else PlayerPrefs.DeleteKey(YuiPrefsKeys.UiLanguage);PlayerPrefs.Save();
            }
        }
    }
}
