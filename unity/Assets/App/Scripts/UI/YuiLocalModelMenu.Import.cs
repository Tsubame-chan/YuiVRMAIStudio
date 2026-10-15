using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.Platform;

namespace YuiPhysicalAI.UI
{
    public static partial class YuiLocalModelMenu
    {
        static bool importing;
        public static bool ImportBusy => importing;
        public static bool IsBusy => downloading || importing || YuiLocalModelImporter.Busy;
        static CancellationTokenSource importCancellation;
        static YuiFilePicker.Result picked;
        static void CancelImport(CancellationTokenSource source) {try{source.Cancel();}catch(ObjectDisposedException){}}
        static void ClearPicked() { picked.Dispose();picked=default; }
        static void ShowError(string message,YuiChatPanel panel) {
            if(dialog!=null)dialog.Close();
            dialog=YuiSimpleDialog.Create(L("モデルを追加できませんでした","Could not add model"),message);
            dialog.AddButton(L("モデル一覧へ戻る","Back to models"),()=>Show(panel));dialog.AddCloseButton(()=>Show(panel));dialog.FitToContent(500);
        }
        static async void ChooseImport(YuiChatPanel panel) {
            if(IsBusy)return;
            if(panel!=null && !panel.CanSwitchLocalModel){ShowError(L("会話や音声処理を停止してから追加してください。","Finish or stop the current conversation before adding a model."),panel);return;}
            ClearPicked();importing=true;importCancellation=new CancellationTokenSource();var token=importCancellation.Token;
            if(dialog!=null)dialog.Close();
            banner=YuiActivityBanner.Create(L("モデルファイルを選んでください","Choose a model file"));
            var pickerBanner=banner;var pickerCancellation=importCancellation;
            banner.SetAction(L("中止","Cancel"),()=>CancelImport(pickerCancellation));
            try {
                picked=await YuiFilePicker.OpenModelFileAsync(token,new Progress<float>(p=>{if(pickerBanner!=null)pickerBanner.Set(p<0?L("モデルファイルを選んでください","Choose a model file"):L("モデルを準備中 · ","Preparing model · ")+Mathf.FloorToInt(p*100)+"%",p);}));
                token.ThrowIfCancellationRequested();
                if(!picked.Opened){if(!string.IsNullOrEmpty(picked.UserMessage))ShowError(picked.UserMessage,panel);else Show(panel);return;}
                var format=YuiLocalModelImporter.DetectFormat(picked.Path);
                if(format=="gguf" && !YuiLlamaRuntime.PlatformSupported)throw new InvalidOperationException(L("このビルドには、このOS向けGGUF実行エンジンがありません。GGUF対応ビルドを使ってください。","This build has no GGUF engine for this OS. Use a build with its platform GGUF runtime."));
                ShowImportConfirmation(format,panel);
            } catch(OperationCanceledException){ClearPicked();Show(panel);}
            catch(Exception ex){ClearPicked();ShowError(ex.Message,panel);}
            finally {importing=false;importCancellation.Dispose();importCancellation=null;if(banner!=null)banner.Close();}
        }
        static void ShowImportConfirmation(string format,YuiChatPanel panel) {
            var size=new FileInfo(picked.Path).Length;var name=Path.GetFileNameWithoutExtension(picked.Path);bool thinking=false;
            ManagerShell(panel,1,$"{(format=="gguf"?"GGUF":"LiteRT-LM")} · {size/1000000000d:0.00} GB\n\n"+ImportGuide()+
                L("\n\n短い応答を確認して保存します。配布条件に従って利用してください。元ファイルは保持します。", "\n\nA short reply is tested before saving. Follow the model's distribution terms. The original file is kept."));
            dialog.AddInput(L("モデルの表示名","Model display name"),name,value=>name=value,false);
            if(format=="litert-lm")dialog.AddToggle(L("推論機能を使う（対応モデルのみ）","Enable thinking (compatible models only)"),false,value=>thinking=value);
            dialog.AddFooterButton(L("取り込んで端末内で確認","Import and test on this device"),()=>ImportPicked(name,thinking,panel),true);
        }
        static async void ImportPicked(string name,bool thinking,YuiChatPanel panel) {
            if(IsBusy)return;
            if(panel!=null && !panel.CanSwitchLocalModel){ClearPicked();ShowError(L("会話を停止してから追加してください。","Stop the conversation before importing."),panel);return;}
            importing=true;importCancellation=new CancellationTokenSource();var token=importCancellation.Token;
            dialog.Close();banner=YuiActivityBanner.Create(L("モデルを保存・検査中…","Saving and checking model…"));
            var operationBanner=banner;var operationCancellation=importCancellation;bool hashingComplete=false;
            banner.SetAction(L("中止","Cancel"),()=>CancelImport(operationCancellation));
            try {
                var importer=new YuiLocalModelImporter(Application.persistentDataPath);
                var pack=await importer.ImportAsync(picked.Path,picked.TemporaryCopy,name,thinking,YuiLocalAiModelRegistry.FromStreamingAssetsOrDefault().Packs,
                    async (candidate,path,t)=> {
                        hashingComplete=true;
                        if(operationBanner!=null)operationBanner.Set(L("端末内で短い応答を確認中…","Testing a short reply on this device…"));
                        var code=YuiLocalModelProbe.NewCode();
                        var request=new YuiLocalAiChatRequest {Message=YuiLocalModelProbe.Prompt(code),LanguageCode="en"};
                        YuiLocalAiChatResponse result;
                        if(candidate.Format=="gguf") result=await YuiLlamaRuntime.GenerateAsync(candidate,path,YuiLocalAiPromptBuilder.PrepareChatRequest(request,true),YuiLocalModelOptions.Defaults(candidate,false),t);
                        else {
                            var runtime=new YuiGoogleAiEdgeLocalAiRuntime(new YuiLocalAiModelRegistry(new[]{candidate}),(p,ct)=>Task.FromResult(path),YuiGoogleAiEdgeBridge.Invoke,YuiGoogleAiEdgeBridge.IsSupported);
                            try {result=await runtime.ChatAsync(request,t);}
                            finally {
                                // The probe's temporary path is about to move or be
                                // removed. Release its engine before publishing/cleanup.
                                await Task.Run(()=>{using var lease=YuiLocalModelExecutionGate.Enter(CancellationToken.None);YuiGoogleAiEdgeBridge.ReleaseCachedEngine();});
                            }
                        }
                        if(!result.Success || string.IsNullOrWhiteSpace(result.Text))throw new InvalidDataException(result.ErrorMessage??L("モデルが返答を生成できませんでした。","The model could not produce a reply."));
                        if(!YuiLocalModelProbe.Matches(result.Text,code))throw new InvalidDataException(L("応答は生成されましたが、入力内容を確認できませんでした。モデルと実行エンジンの互換性を確認してください。", "The model generated text but did not return the input verification code. Check model and runtime compatibility."));
                        return true;
                    },new Progress<float>(p=>{if(!hashingComplete && operationBanner!=null)operationBanner.Set(L("モデルを保存・検査中 · ","Saving and checking model · ")+Mathf.FloorToInt(p*100)+"%",p);}),token);
                ClearPicked();ShowImported(pack,panel);
            } catch(OperationCanceledException){ClearPicked();ShowError(L("取り込みを中止しました。既存のモデルは保持しています。","Import cancelled. Existing models were kept."),panel);}
            catch(Exception ex){ClearPicked();ShowError(ex.Message,panel);}
            finally {importing=false;importCancellation.Dispose();importCancellation=null;if(banner!=null)banner.Close();}
        }
        static void ShowImported(YuiLocalAiModelPack pack,YuiChatPanel panel) {
            if(dialog!=null)dialog.Close();
            var present=YuiLocalModelSelection.Installed(pack);
            dialog=YuiSimpleDialog.Create(L("追加モデル","Additional model"),pack.DisplayName+"\n"+$"{(pack.Format=="gguf"?"GGUF":"LiteRT-LM")} · {pack.FileBytes/1000000000d:0.00} GB\n"+
                (present?L("この端末で短い応答を確認済みです。実際の速度と品質はモデル・端末によって異なります。","A short reply was tested on this device. Actual speed and quality depend on model and device."):L("モデルファイルが見つかりません。追加し直してください。","Model file is missing. Import it again.")));
            dialog.AddFooterButton(L("このモデルを使う","Use this model"),()=> {
                if(IsBusy || (panel!=null&&!panel.CanSwitchLocalModel)){dialog.Body.text=L("会話やモデル処理を停止してから切り替えてください。","Stop the conversation or model operation before switching.");return;}
                if(!present || string.IsNullOrWhiteSpace(pack.VerifiedRuntime)){dialog.Body.text=L("動作確認済みのファイルを追加してください。","Import and test the model file first.");return;}
                YuiLocalModelSelection.Select(pack.Id);panel?.RefreshLocalAiRuntimeAfterAssetInstall();Show(panel);
            },true).interactable=present;
            dialog.AddButton(L("追加モデルを削除","Remove imported model"),()=> {
                dialog.Close();dialog=YuiSimpleDialog.Create(L("追加モデルを削除しますか？","Remove this model?"),L("アプリ内の追加モデルと実行キャッシュを削除します。元のファイル、人格、記憶、標準モデルは保持します。選択中の場合は先に別モデルへ切り替えてください。","Removes this imported copy and its runtime cache. Keeps the original file, personality, memories and standard models. Select another model first if this one is active."));
                dialog.AddButton(L("削除","Remove"),()=>{try{if(IsBusy || (panel!=null&&!panel.CanSwitchLocalModel))throw new InvalidOperationException(L("処理を停止してください。","Stop the current operation first."));new YuiLocalModelImporter(Application.persistentDataPath).Delete(pack.Id);YuiLocalModelOptions.Reset(pack,false);YuiLocalModelOptions.Reset(pack,true);modelDrafts.Remove(DraftKey(pack.Id,false));modelDrafts.Remove(DraftKey(pack.Id,true));panel?.RefreshLocalAiRuntimeAfterAssetInstall();Show(panel);}catch(Exception ex){ShowError(ex.Message,panel);}});
                dialog.AddButton(L("戻る","Back"),()=>ShowImported(pack,panel));dialog.AddCloseButton(()=>ShowImported(pack,panel));dialog.FitToContent(430);
            });
            dialog.AddButton(L("標準モデルに戻る","Use standard model"),()=>{if(IsBusy || (panel!=null&&!panel.CanSwitchLocalModel))return;YuiLocalModelSelection.Select(YuiLocalModelSelection.StandardId);panel?.RefreshLocalAiRuntimeAfterAssetInstall();Show(panel);});
            dialog.AddButton(L("モデル一覧へ戻る","Back to models"),()=>Show(panel));dialog.AddCloseButton(()=>Show(panel));dialog.FitToContent(520);
        }
    }
}
