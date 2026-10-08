using System.Threading;
using UnityEngine;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private CancellationTokenSource speechLanguageCancellation = new CancellationTokenSource();
        public float EnglishSpeed { get => Mathf.Clamp(PlayerPrefs.GetFloat("Yui.EnglishSpeed", 1f), 0.5f, 2f); set { PlayerPrefs.SetFloat("Yui.EnglishSpeed", Mathf.Clamp(value, 0.5f, 2f)); PlayerPrefs.Save(); } }
        public string EnglishVoice => YuiKokoroSpeech.ResolveVoice(PlayerPrefs.GetString("Yui.EnglishVoice", "af_bella"), YuiKokoroSpeech.InstalledVoices(Application.persistentDataPath));
        public string EnglishVoiceName => EnglishVoice.Substring(3, 1).ToUpperInvariant() + EnglishVoice.Substring(4);
        public void ToggleEnglishVoice()
        {
            var voices = YuiKokoroSpeech.InstalledVoices(Application.persistentDataPath);
            if (voices.Length == 0) return;
            var index = System.Array.IndexOf(voices, EnglishVoice);
            PlayerPrefs.SetString("Yui.EnglishVoice", voices[(index + 1) % voices.Length]); PlayerPrefs.Save();
        }
        public bool EnglishVoiceInstalled => YuiKokoroSpeech.IsInstalled(Application.persistentDataPath);
        public void RefreshAfterEnglishVoiceInstall() { SetStatus("English voice ready"); }
        public void DownloadEnglishVoice() { ShowVoicePackDownload(false); }
        private void OfferFirstRunEnglishVoice()
        {
            if (this != null && YuiSpeechLanguage.IsEnglish(YuiUiLocalization.Language)
                && YuiKokoroSpeech.Supported && !EnglishVoiceInstalled)
                ShowVoicePackDownload(false);
        }
        private void OnSpeechLanguageChanged()
        {
            if (aiRuntimeRouter != null) aiRuntimeRouter.SpeechLanguageCode = YuiUiLocalization.Language;
            speechLanguageCancellation.Cancel(); speechLanguageCancellation.Dispose();
            speechLanguageCancellation = new CancellationTokenSource();
            if (audioSource != null) { audioSource.Stop(); ReleaseCurrentPlaybackClip(); }
        }
    }
}
