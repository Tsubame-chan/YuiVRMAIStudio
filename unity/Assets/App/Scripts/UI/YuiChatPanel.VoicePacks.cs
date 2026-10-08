using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern long YuiVoicePack_AvailableBytes(string path);
#endif
        private static long VoicePackAvailableBytes()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return YuiVoicePack_AvailableBytes(Application.persistentDataPath);
#else
            return new DriveInfo(Path.GetPathRoot(Application.persistentDataPath)).AvailableFreeSpace;
#endif
        }
        public string LastVoicePackError { get; private set; }
        private CancellationTokenSource voicePackCancellation;
        private YuiSimpleDialog voicePackDialog;
        private YuiActivityBanner voicePackBanner;
        private string voicePackStatus="";
        private float voicePackProgress;
        public bool VoicePackBusy => voicePackCancellation != null;
        public bool IrodoriInstalled => YuiIrodoriSpeech.IsInstalled(Application.persistentDataPath);
        public bool UsesJapaneseIrodori => IsTtsMode("irodori-native") && IrodoriInstalled;
        public string IrodoriVoice => speakerId == 10002 ? "gentle_friend" : "soft_yui";
        public string IrodoriVoiceName => IrodoriVoice=="gentle_friend" ? YuiSimpleDialog.L("親しみやすく優しい女性","Gentle friend") : YuiSimpleDialog.L("やわらかく可愛い女性","Soft and sweet");
        public static bool IrodoriSupported => Application.platform==RuntimePlatform.IPhonePlayer || (Application.platform==RuntimePlatform.OSXPlayer || Application.platform==RuntimePlatform.OSXEditor) && SystemInfo.processorType.IndexOf("Apple",StringComparison.OrdinalIgnoreCase)>=0;
        public void ShowVoicePackDownload(bool irodori)
        {
            if(voicePackDialog!=null)voicePackDialog.Close();
            if(VoicePackBusy){ShowVoicePackProgress();return;}
            if((irodori && !IrodoriSupported) || (!irodori && !YuiKokoroSpeech.Supported)) {
                var unavailable=YuiSimpleDialog.Create(YuiSimpleDialog.L("この端末では未対応です","Not available on this device"),YuiSimpleDialog.L("この音声パックは、この版では利用できません。","This voice pack is not supported by this build."));
                unavailable.AddButton(YuiSimpleDialog.L("閉じる","Close"),unavailable.Close);return;
            }
            voicePackDialog=YuiSimpleDialog.Create(irodori?"Irodori · 日本語":"Kokoro · English",irodori
                ?YuiSimpleDialog.L("ダウンロード：約1.96 GB\nモデル：約1.96 GB、初回準備のキャッシュも使用します。空き容量は4.2 GB以上を推奨します。\n\n取得中も現在の声とアプリを使えます。完了後に声を選択してください。","Download: 1.96 GB\nModels: 1.96 GB plus first-use cache. Allow at least 4.2 GB free.\n\nKeep using the app while downloading. Select a voice after completion.")
                :YuiSimpleDialog.L("ダウンロード：67.4 MB\n導入後：約102 MB。空き容量250 MB以上が必要です。\nBella、Nova、Nicole、Heart、Puck、Michaelの6声。\n\n取得中や後回しにした場合もテキストで会話できます。設定の音声タブからいつでも取得できます。","Download: 67.4 MB\nInstalled: about 102 MB. Allow 250 MB free.\nSix voices: Bella, Nova, Nicole, Heart, Puck and Michael.\n\nYou can chat by text while downloading, or choose Not now. Download later in Settings > Voice."));
            voicePackDialog.AddButton(YuiSimpleDialog.L("ダウンロード","Download"),()=>{voicePackDialog.Close();_=DownloadVoicePackAsync(irodori);});
            voicePackDialog.AddButton(YuiSimpleDialog.L("今はしない","Not now"),()=>voicePackDialog.Close());
        }
        private void ShowVoicePackProgress()
        {
            voicePackDialog=YuiSimpleDialog.Create(YuiSimpleDialog.L("音声データを取得中","Downloading voice data"),voicePackStatus);
            voicePackDialog.SetProgress(voicePackProgress);
            voicePackDialog.AddButton(YuiSimpleDialog.L("閉じて続ける","Continue in background"),()=>voicePackDialog.Close());
            voicePackDialog.AddButton(YuiSimpleDialog.L("ダウンロードを中止","Cancel download"),()=>voicePackCancellation?.Cancel());
        }
        public async Task DownloadVoicePackAsync(bool irodori)
        {
            if(VoicePackBusy)return;
            LastVoicePackError=null;
            voicePackCancellation=new CancellationTokenSource();var token=voicePackCancellation.Token;
            voicePackStatus=YuiSimpleDialog.L("準備中…","Preparing…");voicePackProgress=0;
            if(voicePackDialog!=null)voicePackDialog.Close();
            voicePackDialog=null;
            if(voicePackBanner!=null)voicePackBanner.Close();
            var packName=irodori?"Irodori":"Kokoro";
            voicePackBanner=YuiActivityBanner.Create(packName+" · "+voicePackStatus);
            voicePackBanner.SetAction(YuiSimpleDialog.L("中止","Cancel"),()=>voicePackCancellation?.Cancel());
            try {
                var free=VoicePackAvailableBytes();
                if(free>=0 && free<(irodori?4200000000L:250000000L))throw new IOException(YuiSimpleDialog.L("空き容量が足りません。空きを増やして再試行してください。","Not enough free storage. Free space and try again."));
                var http=new YuiUnityAssetHttpClient();
                var progress=new Progress<YuiLocalAiAssetDownloadProgress>(p=>{
                    voicePackProgress=p.Percent;voicePackStatus=$"{p.DownloadedBytes/1000000f:F1} / {p.TotalBytes/1000000f:F1} MB";
                    if(voicePackBanner!=null)voicePackBanner.Set(packName+" · "+voicePackStatus,p.Percent);
                    if(voicePackDialog!=null){voicePackDialog.Body.text=voicePackStatus;voicePackDialog.SetProgress(p.Percent);}
                });
                var name=irodori?"irodori_files":"kokoro_pack";
                var catalog=Resources.Load<TextAsset>("YuiVoicePacks/"+name);
                if(catalog==null)throw new FileNotFoundException("Voice pack catalog is missing.");
                var json=catalog.text;
                if(irodori) {
                    await YuiIrodoriSpeech.ReleaseAsync();
                    await YuiVoicePackInstaller.InstallIrodoriAsync(json,Application.persistentDataPath,http,progress,token);
                    voicePackStatus=YuiSimpleDialog.L("音声モデルを準備中…", "Preparing the voice model…");
                    if(voicePackBanner!=null)voicePackBanner.Set(packName+" · "+voicePackStatus);
                    if(voicePackDialog!=null)voicePackDialog.Body.text=voicePackStatus;
                    await YuiIrodoriSpeech.PrepareAsync(IrodoriVoice,Application.persistentDataPath,token);
                }
                else {
                    var manifest=YuiLocalAiAssetManifest.FromJson(json);
                    var downloader=new YuiLocalAiAssetDownloader(http,Application.persistentDataPath,Path.Combine(Application.temporaryCachePath,"YuiVoicePacks"));
                    var result=await downloader.InstallAssetsAsync(manifest,manifest.Assets,progress,token);
                    if(!result.Success)throw new IOException(result.ErrorMessage);
                }
                if(voicePackDialog!=null)voicePackDialog.Close();
                voicePackBanner.Set(packName+YuiSimpleDialog.L(" · 準備完了 · 音声設定から選べます", " · Ready · Select in Voice settings"),1);
                SetStatus(YuiSimpleDialog.L("音声データの準備完了","Voice data ready"));
            } catch(OperationCanceledException){if(voicePackDialog!=null)voicePackDialog.Close();voicePackBanner.Set(YuiSimpleDialog.L("音声の取得を中止しました", "Voice download cancelled"));}
            catch(Exception ex){LastVoicePackError=ex.Message;Debug.LogException(ex);if(voicePackDialog!=null)voicePackDialog.Close();voicePackBanner.Set(YuiSimpleDialog.L("音声の取得に失敗 · 詳細を確認", "Voice download failed · View details"));}
            finally{
                if(LastVoicePackError==null)voicePackBanner.SetAction(YuiSimpleDialog.L("閉じる","Close"),()=>voicePackBanner.Close());
                else voicePackBanner.SetAction(YuiSimpleDialog.L("詳細","Details"),()=>{
                    var error=YuiSimpleDialog.Create(YuiSimpleDialog.L("取得できませんでした","Download failed"),LastVoicePackError);
                    error.AddButton(YuiSimpleDialog.L("再試行","Retry"),()=>{error.Close();ShowVoicePackDownload(irodori);});
                    error.AddButton(YuiSimpleDialog.L("閉じる","Close"),error.Close);
                });
                voicePackCancellation.Dispose();voicePackCancellation=null;FindObjectOfType<YuiSettingsOverlay>()?.RefreshVoicePackUi();}
        }
    }
}
