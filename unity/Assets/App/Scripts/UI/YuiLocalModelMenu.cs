using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.UI
{
    public static partial class YuiLocalModelMenu
    {
        private static bool downloading;
        private static YuiSimpleDialog dialog;
        private static CancellationTokenSource cancellation;
        private static YuiActivityBanner banner;
        private static string downloadName;
        private const string PendingKey="yui.local-model.pending-download";
        private static string L(string ja,string en)=>YuiSimpleDialog.L(ja,en);
        private static Action returnToSettings;
        private static readonly System.Collections.Generic.Dictionary<string,YuiLocalModelOptions> modelDrafts=new System.Collections.Generic.Dictionary<string,YuiLocalModelOptions>();
        private static string DraftKey(string id,bool work)=>id+(work?"/work":"/talk");
        public static void Open(YuiChatPanel panel,Action onClose)
        {
            modelDrafts.Clear();returnToSettings=onClose;Show(panel);
        }
        private static void ClosePanel()
        {
            ClearPicked();modelDrafts.Clear();if(dialog!=null)dialog.Close();dialog=null;
            var callback=returnToSettings;returnToSettings=null;callback?.Invoke();
        }
        private static void Navigate(int page,YuiChatPanel panel)
        {
            if(IsBusy)return;ClearPicked();
            if(page==0)Show(panel);else if(page==1)ShowImportIntro(panel);else ShowAdvanced(panel);
        }
        public static void HandleDesktopKeys(YuiChatPanel panel)
        {
            if(dialog==null || !dialog.gameObject.activeInHierarchy)return;
            if(Input.GetKeyDown(KeyCode.Escape)){dialog.RequestClose();return;}
            bool command=Input.GetKey(KeyCode.LeftCommand)||Input.GetKey(KeyCode.RightCommand)||Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl);
            if(command) {
                if(Input.GetKeyDown(KeyCode.O) && !IsBusy)ChooseImport(panel);
                else if(Input.GetKeyDown(KeyCode.S) && !IsBusy)dialog.InvokePrimaryAction();
                else if(Input.GetKeyDown(KeyCode.Alpha1))Navigate(0,panel);
                else if(Input.GetKeyDown(KeyCode.Alpha2))Navigate(1,panel);
                else if(Input.GetKeyDown(KeyCode.Alpha3))Navigate(2,panel);
            }
            if(Input.GetKeyDown(KeyCode.Tab))YuiControlAffordance.MoveSettingsFocus(dialog.transform,Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift));
        }
        private static YuiSimpleDialog ManagerShell(YuiChatPanel panel,int page,string body)
        {
            if(dialog!=null)dialog.Close();
            dialog=YuiSimpleDialog.Create(L("端末内AI","On-device AI"),body);dialog.Body.fontSize=15;
            dialog.SettingsTabs(page,new[]{L("モデル","Models"),L("追加","Add"),L("詳細設定","Settings")},i=>Navigate(i,panel));
            dialog.AddCloseButton(ClosePanel);dialog.Compact(640);return dialog;
        }
        private static string ImportGuide()
        {
            return L("追加モデルは実験用です。大きいモデルは遅延・停止・アプリ終了の可能性があります。",
                "Additional models are experimental. Large models may be slow, stop, or cause the app to close.")+
                (Application.isMobilePlatform?L("\nモバイルは小さい量子化モデルから試すのが目安です。","\nOn mobile, start with a small quantized model."):"");
        }
        internal static string FormatGuide()
        {
            return L("対応形式：単一ファイルのGGUF（.gguf）／互換LiteRT-LM（.litertlm）。文章・会話用モデルを選んでください。Hugging Faceなどで配布されている対応ファイルをダウンロードし、この端末のファイル選択から取り込みます。利用できる形式はOSとアプリに含まれる実行エンジンによって異なります。\n\n.safetensorsや分割GGUF、画像・音声用の追加ファイルは直接取り込めません。形式が同じでも、モデル構造・量子化・実行エンジンのバージョンにより動かない場合があります。短い入力への応答を確認してから登録しますが、すべての会話の品質を保証するものではありません。",
                "Supported files: single-file GGUF (.gguf) and compatible LiteRT-LM (.litertlm) text/chat models. Download a compatible file from a source such as Hugging Face, then import it using this device’s file picker. Available formats depend on the OS and the runtime included in the app.\n\nRaw .safetensors, split GGUF and separate vision/audio files cannot be imported directly. Model architecture, quantization and runtime version affect compatibility even within one format. Import tests a short input before registration; this does not guarantee quality in every conversation.")+"\n\n"+ImportGuide();
        }
        private static void ShowImportIntro(YuiChatPanel panel)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if(!YuiLlamaRuntime.PlatformSupported)
            {
                ManagerShell(panel,1,L("このWindowsビルドでは互換LiteRT-LMの文章モデルを追加できます。GGUF実行エンジンは同梱されていません。PC上の別のGGUFサーバーを使う場合はBackend経由で接続してください。\n\n", "This Windows build can import compatible LiteRT-LM chat models. A GGUF runtime is not bundled. To use a separate GGUF server on your PC, connect through Backend.\n\n")+FormatGuide());
                dialog.AddFooterButton(L("モデルファイルを選ぶ","Choose model file"),()=>ChooseImport(panel),true).interactable=!IsBusy;
                return;
            }
#endif
            ManagerShell(panel,1,L("GGUF / LiteRT-LMの文章モデルを追加できます。PCやBackendは不要です。\n\n", "Add a GGUF or LiteRT-LM chat model. No PC or Backend is required.\n\n")+FormatGuide()+
                L("\n\nファイルを選び、短い応答を確認して保存します。元ファイルは保持します。", "\n\nChoose a file, test a short reply, then save it. The original file is kept."));
            dialog.AddFooterButton(L("モデルファイルを選ぶ","Choose model file"),()=>ChooseImport(panel),true).interactable=!IsBusy;
        }
        public static void ResumeConsentedDownload(YuiChatPanel panel)
        {
            var id=PlayerPrefs.GetString(PendingKey,"");
            if (string.IsNullOrEmpty(id) || downloading) return;
            var pack=YuiLocalModelSelection.Find(id);
            if(pack==null){PlayerPrefs.DeleteKey(PendingKey);PlayerPrefs.Save();return;}
            if(YuiLocalModelSelection.Installed(pack) && !WindowsRuntimeMissing) { PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save(); return; }
            Download(pack,panel);
        }
        public static void Show(YuiChatPanel panel)
        {
            if(dialog!=null)dialog.Close();
            YuiLocalModelSelection.MigrateInstalledChoice();
            try { new YuiLocalModelImporter(Application.persistentDataPath).Recover(); } catch(Exception) { /* Existing catalog is preserved on recovery failure. */ }
            var available=YuiLocalModelSelection.Available.ToArray();
            ManagerShell(panel,0,L("候補を選ぶと切り替わります。\n2Bは軽快、4Bは品質重視です。", "Choose a model to switch.\n2B favors speed; 4B favors quality.")+"\n"+
                L($"標準{available.Count(p=>p.SourceKind!="local-file")}件 · 追加{available.Count(p=>p.SourceKind=="local-file")}件",$"{available.Count(p=>p.SourceKind!="local-file")} standard · {available.Count(p=>p.SourceKind=="local-file")} added"));
            foreach(var pack in available)AddModel(pack,YuiLocalModelSelection.Name(pack),panel);
            if(WindowsRuntimeMissing)dialog.Body.text += L("\nWindowsの実行データも必要です。取得操作で準備します。", "\nWindows runtime files are also prepared during download.");
        }
        private static void ShowAdvanced(YuiChatPanel panel,bool work=false)
        {
            var pack=YuiLocalModelSelection.Find(YuiLocalModelSelection.SelectedId);
            if(pack==null)return;
            if(dialog!=null)dialog.Close();
            var key=DraftKey(pack.Id,work);
            if(!modelDrafts.TryGetValue(key,out var options))modelDrafts[key]=options=YuiLocalModelOptions.Load(pack,work);
            ManagerShell(panel,2,YuiLocalModelSelection.Name(pack)+"\n\n"+
                L("Talk/Work別に保存します。値を増やすと処理時間・メモリが増えます。編集はタブを変えても保持し、閉じると未保存の変更を破棄します。",
                  "Saved separately for Talk and Work. Higher limits take more time and memory. Edits survive tab changes; closing discards unsaved edits."));
            dialog.AddButton(work?"Work · "+L("Talkに切り替え", "Switch to Talk"):"Talk · "+L("Workに切り替え", "Switch to Work"),()=>ShowAdvanced(panel,!work));
            dialog.AddButton(work?L("Workの設定を初期値に戻す","Reset Work settings"):L("Talkの設定を初期値に戻す","Reset Talk settings"),()=>{
                modelDrafts[key]=YuiLocalModelOptions.Defaults(pack,work);ShowAdvanced(panel,work);
                dialog.Body.text=L("このモデルの"+(work?"Work":"Talk")+"設定を初期値に戻しました。保存後、次の会話から適用します。\n\n", "Restored this model's "+(work?"Work":"Talk")+" defaults. Save to apply to the next request.\n\n")+dialog.Body.text;
            });
            dialog.AddSetting(L("コンテキスト上限","Context limit"),options.ContextTokens,4096,8192,512,v=>options.ContextTokens=(int)v);
            dialog.AddSetting(L("生成上限（推論＋回答）","Output limit (thinking + reply)"),options.OutputTokens,256,4096,128,v=>options.OutputTokens=(int)v);
            if(pack.SupportsThinking)dialog.AddSetting(L("推論上限","Thinking limit"),options.ThinkingTokens,1,2048,64,v=>options.ThinkingTokens=(int)v);
            dialog.AddSetting(L("温度","Temperature"),options.Temperature,0,1.5f,.05f,v=>options.Temperature=v);
            dialog.AddSetting("Top K",options.TopK,1,100,1,v=>options.TopK=(int)v);
            dialog.AddSetting("Top P",options.TopP,.1f,1,.05f,v=>options.TopP=v);
            dialog.AddSetting(L("待ち時間上限（秒）","Timeout (seconds)"),options.TimeoutSeconds,30,600,30,v=>options.TimeoutSeconds=(int)v);
            dialog.AddInput(L("LLMへの追加指示（口調・人格とは別）","LLM instructions (separate from personality)"),options.CustomPrompt,v=>options.CustomPrompt=v);
            dialog.AddFooterButton(L("設定を保存","Save settings"),()=>{options.Save(pack,work);ShowAdvanced(panel,work);dialog.Body.text=L("保存しました。メモリと回答用の余地を確保するため、生成上限はコンテキストの半分まで、推論上限は生成上限より小さく調整します。次の会話から適用します。", "Saved. Output is limited to half the context, and thinking leaves room for a final reply. Applies to your next request.");},true);
            if(pack.SourceKind=="local-file")dialog.AddButton(L("追加モデルの管理・削除","Manage or remove model"),()=>ShowImported(pack,panel));
        }
        private static bool WindowsRuntimeMissing =>
            (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            && !YuiDesktopInferenceProcess.IsAvailable;
        private static void AddModel(YuiLocalAiModelPack pack,string name,YuiChatPanel panel)
        {
            if(pack==null)return;
            if(pack.SourceKind=="local-file") {
                bool selected=pack.Id==YuiLocalModelSelection.SelectedId;
                bool present=YuiLocalModelSelection.Installed(pack);
                dialog.AddModelChoice(name,(pack.Format=="gguf"?"GGUF":"LiteRT-LM")+L(!present?" · ファイルなし · 管理":selected?" · 選択中":" · このモデルを使う",!present?" · Missing file · Manage":selected?" · Selected":" · Use this model"),()=>{
                    if(!present){ShowImported(pack,panel);return;}
                    if(IsBusy || (panel!=null&&!panel.CanSwitchLocalModel)){dialog.Body.text=L("処理を停止してから切り替えてください。","Stop the current operation before switching.");return;}
                    if(string.IsNullOrWhiteSpace(pack.VerifiedRuntime)){ShowImported(pack,panel);return;}
                    YuiLocalModelSelection.Select(pack.Id);panel?.RefreshLocalAiRuntimeAfterAssetInstall();Show(panel);
                },selected);return;
            }
            bool installed=YuiLocalModelSelection.Installed(pack);
            if(!installed && string.IsNullOrEmpty(pack.AppleAssetPackId) && string.IsNullOrEmpty(pack.Sha256))
            {
                dialog.Body.text += L("\n\n標準データが見つかりません。アプリを再インストールしてください。", "\n\nStandard data is missing. Reinstall the app to restore it.");
                return;
            }
            bool isSelected=installed && !WindowsRuntimeMissing && pack.Id==YuiLocalModelSelection.SelectedId;
            var button=dialog.AddModelChoice(name,L(isSelected?"選択中":installed&&WindowsRuntimeMissing?"実行データを準備":installed?"このモデルを使う":downloading?"取得中":"ダウンロードする",isSelected?"Selected":installed&&WindowsRuntimeMissing?"Prepare runtime":installed?"Use this model":downloading?"Downloading":"Download model"),()=>{
                if(installed && !WindowsRuntimeMissing){
                    if(panel != null && !panel.CanSwitchLocalModel) { dialog.Body.text=L("会話や読み上げが終わってから切り替えてください。停止ボタンでも中止できます。", "Finish or stop the current conversation before switching models."); return; }
                    YuiLocalModelSelection.Select(pack.Id);panel?.RefreshLocalAiRuntimeAfterAssetInstall();Show(panel);
                }
                else Confirm(pack,name,panel);
            },isSelected);
            button.interactable=installed || !IsBusy;
            if (!installed && downloading) dialog.Body.text += L("\n\nモデルを取得中です。画面を閉じても続行します。", "\n\nModel download continues when you close this window.");
        }
        private static void Confirm(YuiLocalAiModelPack pack,string name,YuiChatPanel panel)
        {
            if(IsBusy)return;
            dialog.Close();
            dialog=YuiSimpleDialog.Create(name,L($"最大約{pack.DiskBudgetMb/1000f:0.0} GBのデータを取得します。展開にも空き容量が必要です。\n\nWi-Fiでの通信を推奨します。準備が終わればオフラインで話せます。",$"Download up to about {pack.DiskBudgetMb/1000f:0.0} GB. Extraction also needs free storage.\n\nWi-Fi is recommended. Once ready, you can chat offline."));
            if(WindowsRuntimeMissing)dialog.Body.text += L("\n\n選択モデルに加え、配布manifestの必須実行データ・標準音声も取得します。その分の通信・空き容量が必要です。", "\n\nAlso downloads required runtime and standard voices. Additional bandwidth and free storage are required.");
            dialog.AddButton(L("ダウンロードを開始","Start download"),()=>Download(pack,panel));
            dialog.AddButton(L("あとで","Later"),()=>Show(panel));
            dialog.FitToContent(400);
        }
        private static async void Download(YuiLocalAiModelPack pack,YuiChatPanel panel)
        {
            if(IsBusy)return;
            downloading=true;cancellation=new CancellationTokenSource();var token=cancellation.Token;
            downloadName=YuiLocalModelSelection.Name(pack);
            PlayerPrefs.SetString(PendingKey,pack.Id); PlayerPrefs.Save();
            if(dialog!=null)dialog.Close(); dialog=null;
            if(banner!=null)banner.Close();
            banner=YuiActivityBanner.Create(downloadName+L(" · 取得中…"," · Downloading…"));
            banner.SetAction(L("中止","Cancel"),()=>cancellation?.Cancel());
            try
            {
                if(YuiAppleHostedAssets.Enabled && !string.IsNullOrEmpty(pack.AppleAssetPackId))
                {
                    YuiAppleHostedAssets.Prepare(pack);
                    using(token.Register(YuiAppleHostedAssets.Cancel))
                    {
                        while(true)
                        {
                            await Task.Delay(200);var state=YuiAppleHostedAssets.Status();
                            if(state.state=="failed")throw new IOException(state.message);
                            if(state.state=="cancelled")throw new OperationCanceledException();
                            SetProgress(state.progress, state.state=="paused");
                            if(state.state=="ready")break;
                        }
                    }
                }
                else {
                    if(Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                        await PrepareWindowsRuntime(panel,token);
                    if(!YuiLocalModelSelection.Installed(pack)) await DownloadDesktop(pack,token);
                }
                if(!YuiLocalModelSelection.Installed(pack))throw new IOException("Model not found after download.");
                if(WindowsRuntimeMissing)throw new IOException("Windows inference runtime is incomplete.");
                panel?.RefreshLocalAiRuntimeAfterAssetInstall();
                banner.Set(downloadName+L(" · 準備完了 · 設定から選べます"," · Ready · Select in Settings"),1);
            }
            catch(OperationCanceledException){banner.Set(L("取得を中止しました · 設定から再試行","Cancelled · Retry in Settings"));}
            catch(Exception ex){Debug.LogWarning("Model download: "+ex.Message);banner.Set(L("取得失敗 · 通信と空き容量を確認","Download failed · Check network/storage"));}
            finally
            {
                cancellation.Dispose();cancellation=null;downloading=false;
                PlayerPrefs.DeleteKey(PendingKey); PlayerPrefs.Save();
                banner.SetAction(L("閉じる","Close"),()=>banner.Close());
            }
        }
        private static async Task PrepareWindowsRuntime(YuiChatPanel panel,CancellationToken token)
        {
            banner.Set(L("Windowsの実行データを確認しています…", "Checking Windows runtime…"));
            var downloader = new YuiLocalAiAssetDownloader(new YuiUnityAssetHttpClient(), Application.persistentDataPath,
                Path.Combine(Application.temporaryCachePath, "YuiLocalAI"));
            var source = panel!=null ? panel.GetComponent<YuiLocalAiDownloadOverlay>() : null;
            var manifest = await downloader.FetchManifestAsync(source!=null ? source.CurrentManifestUrl : YuiLocalAiDownloadOverlay.ResolveManifestUrl(), token);
            var plan = YuiLocalAiAssetStore.PlanRequiredDownloads(manifest,
                YuiLocalAiInstalledAssetLedger.Load(downloader.LedgerPath), Application.persistentDataPath, "windows");
            if(plan.State == YuiLocalAiAssetPlanState.NoRequiredAssets)
                throw new IOException("Windows required runtime is missing from the release manifest.");
            var progress = new Progress<YuiLocalAiAssetDownloadProgress>(p => {
                if(banner!=null)banner.Set(L("Windows実行データ · ", "Windows runtime · ")+YuiLocalAiDownloadOverlay.DownloadProgressLabel(p), p.Percent);
            });
            var result = await downloader.InstallAssetsAsync(manifest, plan.AssetsToDownload, progress, token);
            if(!result.Success)throw new IOException(result.ErrorMessage);
        }

        private static async Task DownloadDesktop(YuiLocalAiModelPack pack,CancellationToken token)
        {
            // Reviewed artifacts are verified before becoming visible to the runtime.
            var hash=pack.Sha256;
            if(string.IsNullOrWhiteSpace(hash))throw new IOException("Model registration needs a SHA-256 checksum.");
            var destination=YuiLocalAiModelPathResolver.PersistentModelPath(pack);Directory.CreateDirectory(Path.GetDirectoryName(destination));
            var stage=destination+".download";
            try
            {
                using(var request=UnityWebRequest.Get(string.IsNullOrEmpty(pack.DownloadUrl)?"https://huggingface.co/"+pack.ModelId+"/resolve/main/"+YuiLocalAiModelPathResolver.ModelFileName(pack):pack.DownloadUrl))
                {
                    request.downloadHandler=new DownloadHandlerFile(stage){removeFileOnAbort=true};var operation=request.SendWebRequest();
                    while(!operation.isDone){if(token.IsCancellationRequested)request.Abort();token.ThrowIfCancellationRequested();SetProgress(request.downloadProgress);await Task.Delay(100,token);}
                    if(request.result!=UnityWebRequest.Result.Success)throw new IOException(request.error);
                }
                banner.Set(downloadName+L(" · データを確認中…"," · Verifying…"));
                var digest=await Task.Run(()=>{using var sha=SHA256.Create();using var file=File.OpenRead(stage);return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();},token);
                token.ThrowIfCancellationRequested();if(!string.Equals(digest,hash,StringComparison.OrdinalIgnoreCase))throw new IOException("Model checksum mismatch.");
                if(!File.Exists(destination))File.Move(stage,destination);
            }
            finally { if(File.Exists(stage))File.Delete(stage); }
        }
        private static void SetProgress(float value, bool paused=false)
        { banner.Set(downloadName+" · "+(paused?L("接続を待っています","Waiting for connection"):L("取得中 ","Downloading ")+(value<0?"…":$"{Mathf.FloorToInt(Mathf.Clamp01(value)*100)}% / 100%")),value); }
    }
}
