using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private YuiActivityBanner backendAssetBanner;
        private float nextBackendAssetPoll;
        private bool backendAssetPollBusy;
        private string backendAssetObservedJob, backendAssetObservedServer;

        private void PollBackendAssetDownloads()
        {
            if (client == null || cancellationTokenSource == null || string.IsNullOrWhiteSpace(backendUrl)) return;
            if (backendAssetObservedServer != backendUrl)
            {
                backendAssetObservedServer=backendUrl;backendAssetObservedJob=null;
                if(backendAssetBanner!=null)backendAssetBanner.Close();
                nextBackendAssetPoll=0;
            }
            if (backendAssetPollBusy || Time.realtimeSinceStartup < nextBackendAssetPoll || Time.realtimeSinceStartup-lastBackendSuccessAt > 20) return;
            backendAssetPollBusy=true;nextBackendAssetPoll=Time.realtimeSinceStartup+2;
            _=ReadBackendAssetDownloadsAsync();
        }
        public static bool BackendAssetPhaseActive(string phase)
            => phase=="queued" || phase=="downloading" || phase=="verifying" || phase=="installing" || phase=="preparing" || phase=="cancelling";
        private async Task ReadBackendAssetDownloadsAsync()
        {
            var source=client;var server=backendUrl;
            try
            {
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
                deadline.CancelAfter(3000);
                var status=await source.GetAssetDownloadStatusAsync(deadline.Token);
                if(this==null || client!=source || backendUrl!=server)return;
                var jobs=(status["jobs"] as JArray)?.OfType<JObject>().Take(20).ToArray() ?? Array.Empty<JObject>();
                var job=jobs.FirstOrDefault(j=>BackendAssetPhaseActive((string)j["phase"]))
                    ?? jobs.FirstOrDefault(j=>(string)j["asset_id"]==backendAssetObservedJob);
                if(job==null)return;
                var phase=(string)job["phase"];
                var active=BackendAssetPhaseActive(phase);
                if(active)backendAssetObservedJob=(string)job["asset_id"];
                else if(backendAssetObservedJob==null)return; // No stale completion notices on initial connection.
                var progress=Mathf.Clamp01((float)((double?)job["downloaded_bytes"]??0)/Math.Max(1,(float)((double?)job["total_bytes"]??0)));
                var text=phase=="downloading" ? $"{Mathf.FloorToInt(progress*100)}% / 100%"
                    : phase=="ready" ? YuiSimpleDialog.L("準備完了 · 音声設定から選べます","Ready · Select in Voice settings")
                    : phase=="failed" ? YuiSimpleDialog.L("準備できませんでした · Backendで確認","Preparation failed · Check Backend")
                    : phase=="cancelled" ? YuiSimpleDialog.L("取得を中止しました","Download cancelled")
                    : phase=="verifying" ? YuiSimpleDialog.L("ファイルを検証中…","Verifying files…")
                    : phase=="cancelling" ? YuiSimpleDialog.L("中止しています…","Cancelling…")
                    : YuiSimpleDialog.L("音声を準備中…","Preparing voices…");
                if(backendAssetBanner==null)backendAssetBanner=YuiActivityBanner.Create("");
                backendAssetBanner.Set(YuiSimpleDialog.L("Backendの追加データ · ","Backend data · ")+text,phase=="downloading"?progress:-1);
                // Use the lowest free slot so a lone notice does not cover chat tabs.
                backendAssetBanner.PlaceAboveOtherBanners();
                if(active)backendAssetBanner.SetAction(YuiSimpleDialog.L("詳細","Details"),()=>{
                    var details=YuiSimpleDialog.Create(YuiSimpleDialog.L("Backendの追加データ","Backend download"),
                        YuiSimpleDialog.L("Backendで追加データを取得・準備しています。取得中も会話とアプリの操作を続けられます。中止・再試行はBackendの管理画面から行えます。","The Backend is downloading and preparing additional data. You can keep using the app. Cancel or retry from the Backend Console."));
                    details.AddButton(YuiSimpleDialog.L("閉じる","Close"),details.Close);details.Compact();
                });
                else backendAssetBanner.SetAction(YuiSimpleDialog.L("閉じる","Close"),()=>{
                    backendAssetBanner.Close();backendAssetObservedJob=null;
                });
                if(!active)backendAssetObservedJob=null;
            }
            catch(Exception) { nextBackendAssetPoll=Time.realtimeSinceStartup+10; } // Older Backend versions have no acquisition endpoint.
            finally { backendAssetPollBusy=false; }
        }
    }
}
