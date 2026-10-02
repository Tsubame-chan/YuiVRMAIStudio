using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.UI
{
    public static class YuiLocalModelMenu
    {
        private static bool downloading;
        private static YuiSimpleDialog dialog;
        private static CancellationTokenSource cancellation;
        private static YuiActivityBanner banner;
        private static string downloadName;
        private const string PendingKey="yui.local-model.pending-download";
        private static string L(string ja,string en)=>YuiSimpleDialog.L(ja,en);
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
            dialog=YuiSimpleDialog.Create(L("端末内AIを選ぶ","On-device models"),L(
                "標準（2B）: 軽快な会話向け。\n高品質（4B）: 回答品質を優先。応答時間・負荷・容量が増えます。\n\n取得後に「使う」で切り替えます。",
                "2B: fast conversations.\n4B: higher quality; slower, with more storage and memory.\n\nChoose Use after downloading."));
            foreach(var pack in YuiLocalModelSelection.Available)
                AddModel(pack,YuiLocalModelSelection.Name(pack),panel);
            if(WindowsRuntimeMissing)dialog.Body.text += L("\n\nモデルとは別にWindowsの実行データが必要です。取得操作で両方を準備します。", "\n\nWindows runtime files are required in addition to the model. Download prepares both.");
            dialog.AddButton(L("選択中のモデル · 詳細設定","Selected model · Advanced"),()=>ShowAdvanced(panel));
            dialog.AddButton(L("閉じる","Close"),()=>dialog.Close());
            dialog.Compact();
        }
        private static void ShowAdvanced(YuiChatPanel panel,bool work=false)
        {
            var pack=YuiLocalModelSelection.Find(YuiLocalModelSelection.SelectedId);
            if(pack==null)return;
            if(dialog!=null)dialog.Close();
            var options=YuiLocalModelOptions.Load(pack,work);
            dialog=YuiSimpleDialog.Create(YuiLocalModelSelection.Name(pack)+L(" · 詳細設定"," · Advanced"),
                L("トークとワークで別々に保存します。値を増やすと応答時間やメモリ使用量が増えます。温度が低いほど安定、高いほど表現が多様になります。設定だけで正確さは保証されません。\n\nここでの追加指示は、このモデルとモードに適用します。性格・口調はキャラクター設定、共通の回答方針はAI設定で編集できます。", "Saved separately for Talk and Work. Larger limits increase latency and memory use. Lower temperature is more consistent; higher values vary expression. Settings do not guarantee accuracy.\n\nInstructions here apply to this model and mode. Edit personality in Character settings and shared response style in AI settings."));
            dialog.AddButton(work?"Work · "+L("Talkに切り替え", "Switch to Talk"):"Talk · "+L("Workに切り替え", "Switch to Work"),()=>ShowAdvanced(panel,!work));
            dialog.AddSetting(L("コンテキスト上限","Context limit"),options.ContextTokens,4096,8192,512,v=>options.ContextTokens=(int)v);
            dialog.AddSetting(L("生成上限（推論＋回答）","Output limit (thinking + reply)"),options.OutputTokens,256,4096,128,v=>options.OutputTokens=(int)v);
            if(pack.SupportsThinking)dialog.AddSetting(L("推論上限","Thinking limit"),options.ThinkingTokens,1,2048,64,v=>options.ThinkingTokens=(int)v);
            dialog.AddSetting(L("温度","Temperature"),options.Temperature,0,1.5f,.05f,v=>options.Temperature=v);
            dialog.AddSetting("Top K",options.TopK,1,100,1,v=>options.TopK=(int)v);
            dialog.AddSetting("Top P",options.TopP,.1f,1,.05f,v=>options.TopP=v);
            dialog.AddSetting(L("待ち時間上限（秒）","Timeout (seconds)"),options.TimeoutSeconds,30,600,30,v=>options.TimeoutSeconds=(int)v);
            dialog.AddInput(L("LLMへの追加指示（口調・人格とは別）","LLM instructions (separate from personality)"),options.CustomPrompt,v=>options.CustomPrompt=v);
            dialog.AddButton(L("保存","Save"),()=>{options.Save(pack,work);ShowAdvanced(panel,work);dialog.Body.text=L("保存しました。メモリと回答用の余地を確保するため、生成上限はコンテキストの半分まで、推論上限は生成上限より小さく調整します。次の会話から適用します。", "Saved. Output is limited to half the context, and thinking leaves room for a final reply. Applies to your next request.");});
            dialog.AddButton(L("このモードを既定値に戻す","Reset this mode"),()=>{YuiLocalModelOptions.Reset(pack,work);ShowAdvanced(panel,work);});
            dialog.AddButton(L("変更せず戻る","Back without saving"),()=>Show(panel));
            dialog.Compact(640);
        }
        private static bool WindowsRuntimeMissing =>
            (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            && !YuiDesktopInferenceProcess.IsAvailable;
        private static void AddModel(YuiLocalAiModelPack pack,string name,YuiChatPanel panel)
        {
            if(pack==null)return;
            bool installed=YuiLocalModelSelection.Installed(pack);
            if(!installed && string.IsNullOrEmpty(pack.AppleAssetPackId) && string.IsNullOrEmpty(pack.Sha256))
            {
                dialog.Body.text += L("\n\n標準データが見つかりません。アプリを再インストールしてください。", "\n\nStandard data is missing. Reinstall the app to restore it.");
                return;
            }
            var button=dialog.AddButton(name+L(installed&&WindowsRuntimeMissing?" · 実行データを準備":installed?"を使う":downloading?"を取得中":"をダウンロード",installed&&WindowsRuntimeMissing?" — Prepare runtime":installed?" — Use":downloading?" — Downloading":" — Download"),()=>{
                if(installed && !WindowsRuntimeMissing){
                    if(panel != null && !panel.CanSwitchLocalModel) { dialog.Body.text=L("会話や読み上げが終わってから切り替えてください。停止ボタンでも中止できます。", "Finish or stop the current conversation before switching models."); return; }
                    YuiLocalModelSelection.Select(pack.Id);panel?.RefreshLocalAiRuntimeAfterAssetInstall();Show(panel);
                }
                else Confirm(pack,name,panel);
            });
            button.interactable=installed || !downloading;
            if(installed && !WindowsRuntimeMissing && pack.Id==YuiLocalModelSelection.SelectedId)
            {
                button.GetComponent<UnityEngine.UI.Image>().color=YuiUiTheme.Selected;
                button.GetComponentInChildren<UnityEngine.UI.Text>().text=name+L(" · 選択中"," · Selected");
            }
            if (!installed && downloading) dialog.Body.text += L("\n\nモデルを取得中です。画面を閉じても続行します。", "\n\nModel download continues when you close this window.");
        }
        private static void Confirm(YuiLocalAiModelPack pack,string name,YuiChatPanel panel)
        {
            if(downloading)return;
            dialog.Close();
            dialog=YuiSimpleDialog.Create(name,L($"最大約{pack.DiskBudgetMb/1000f:0.0} GBのデータを取得します。展開にも空き容量が必要です。\n\nWi-Fiでの通信を推奨します。準備が終わればオフラインで話せます。",$"Download up to about {pack.DiskBudgetMb/1000f:0.0} GB. Extraction also needs free storage.\n\nWi-Fi is recommended. Once ready, you can chat offline."));
            if(WindowsRuntimeMissing)dialog.Body.text += L("\n\n選択モデルに加え、配布manifestの必須実行データ・標準音声も取得します。その分の通信・空き容量が必要です。", "\n\nAlso downloads required runtime and standard voices. Additional bandwidth and free storage are required.");
            dialog.AddButton(L("ダウンロードを開始","Start download"),()=>Download(pack,panel));
            dialog.AddButton(L("あとで","Later"),()=>Show(panel));
            dialog.Compact(400);
        }
        private static async void Download(YuiLocalAiModelPack pack,YuiChatPanel panel)
        {
            if(downloading)return;
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
                if(banner!=null)banner.Set(L("Windows実行データ · ", "Windows runtime · ")+p.Stage, p.Percent);
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
