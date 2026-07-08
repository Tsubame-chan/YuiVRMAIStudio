using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Backend;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiLocalAiDownloadOverlay : MonoBehaviour
    {
        public const string DefaultManifestUrl = "https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/latest/download/YuiVRMAIStudio_AssetManifest.json";
        private const string OptionalTtsAddonKind = "optional_tts_addon";

        [SerializeField] private string manifestUrl = DefaultManifestUrl;

        private YuiChatPanel chatPanel;
        private YuiLocalAiAssetManifest manifest;
        private YuiLocalAiAssetPlan currentPlan;
        private CancellationTokenSource downloadCancellation;
        private GameObject root;
        private Text titleText;
        private Text bodyText;
        private Text detailText;
        private Slider progressSlider;
        private Button downloadButton;
        private Button retryButton;
        private Button cancelButton;
        private bool checkInProgress;
        private bool optionalTtsDownloadMode;

        public string CurrentStatusText { get; private set; } = "Local AI data: not checked";

        public bool IsDesktopSupported
        {
            get
            {
#if (UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR_OSX || UNITY_EDITOR_WIN)
                return true;
#else
                return false;
#endif
            }
        }

        public void Initialize(YuiChatPanel panel)
        {
            chatPanel = panel;
        }

        public async Task CheckAndPromptIfNeededAsync(CancellationToken cancellationToken)
        {
            if (!IsDesktopSupported || checkInProgress)
            {
                return;
            }

            checkInProgress = true;
            try
            {
                optionalTtsDownloadMode = false;
                await RefreshPlanAsync(cancellationToken);
                if (currentPlan == null || currentPlan.State != YuiLocalAiAssetPlanState.NeedsDownload)
                {
                    Hide();
                    return;
                }

                ShowDownloadPrompt();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                CurrentStatusText = $"Local AI data: check failed ({ex.Message})";
                Debug.LogWarning($"Yui Local AI asset check failed: {ex.Message}");
            }
            finally
            {
                checkInProgress = false;
            }
        }

        public async void ShowRepairDownload()
        {
            if (!IsDesktopSupported)
            {
                return;
            }

            try
            {
                optionalTtsDownloadMode = false;
                await RefreshPlanAsync(CancellationToken.None);
                ShowDownloadPrompt(force: true);
            }
            catch (Exception ex)
            {
                EnsureUi();
                Show();
                SetBody("ローカルAIデータの確認に失敗しました。", ex.Message);
                SetButtons(download: false, retry: true, cancel: false);
            }
        }

        public async void ShowOptionalTtsDownload()
        {
            if (!IsDesktopSupported)
            {
                return;
            }

            try
            {
                await RefreshOptionalTtsPlanAsync(CancellationToken.None);
                ShowDownloadPrompt(force: true);
            }
            catch (Exception ex)
            {
                EnsureUi();
                Show();
                optionalTtsDownloadMode = true;
                SetTitle("追加音声ダウンロード");
                SetBody("追加音声データの確認に失敗しました。", ex.Message);
                SetButtons(download: false, retry: true, cancel: false);
            }
        }

        public async Task RefreshPlanAsync(CancellationToken cancellationToken)
        {
            if (!IsDesktopSupported)
            {
                CurrentStatusText = "Local AI data: managed by platform store";
                currentPlan = null;
                return;
            }

            CurrentStatusText = "Local AI data: checking...";
            var downloader = CreateDownloader();
            manifest = await downloader.FetchManifestAsync(manifestUrl, cancellationToken);
            var ledger = YuiLocalAiInstalledAssetLedger.Load(downloader.LedgerPath);
            currentPlan = YuiLocalAiAssetStore.PlanRequiredDownloads(
                manifest,
                ledger,
                AssetStorageRoot(),
                YuiLocalAiModelRegistry.CurrentPlatformKey());
            CurrentStatusText = FormatPlanStatus(currentPlan);
        }

        private async Task RefreshOptionalTtsPlanAsync(CancellationToken cancellationToken)
        {
            if (!IsDesktopSupported)
            {
                CurrentStatusText = "Additional voices: managed by platform store";
                currentPlan = null;
                return;
            }

            optionalTtsDownloadMode = true;
            CurrentStatusText = "Additional voices: checking...";
            var downloader = CreateDownloader();
            manifest = await downloader.FetchManifestAsync(manifestUrl, cancellationToken);
            var ledger = YuiLocalAiInstalledAssetLedger.Load(downloader.LedgerPath);
            currentPlan = YuiLocalAiAssetStore.PlanOptionalDownloads(
                manifest,
                ledger,
                AssetStorageRoot(),
                YuiLocalAiModelRegistry.CurrentPlatformKey(),
                OptionalTtsAddonKind);
            CurrentStatusText = FormatOptionalTtsPlanStatus(currentPlan);
        }

        private void ShowDownloadPrompt(bool force = false)
        {
            EnsureUi();
            Show();
            SetTitle(optionalTtsDownloadMode ? "追加音声ダウンロード" : "初回データダウンロード");
            if (currentPlan == null || currentPlan.State != YuiLocalAiAssetPlanState.NeedsDownload)
            {
                if (optionalTtsDownloadMode && currentPlan != null && currentPlan.State == YuiLocalAiAssetPlanState.NoRequiredAssets)
                {
                    SetBody(
                        "このOS向けの追加音声パックはまだありません。",
                        CurrentStatusText);
                    SetButtons(download: false, retry: false, cancel: false);
                    return;
                }

                SetBody(
                    optionalTtsDownloadMode ? "追加音声データは準備できています。" : "ローカルAIデータは準備できています。",
                    CurrentStatusText);
                SetButtons(download: false, retry: false, cancel: false);
                return;
            }

            var count = currentPlan.AssetsToDownload.Count;
            if (optionalTtsDownloadMode)
            {
                SetBody(
                    "追加音声データをダウンロードします。",
                    $"対象: {count}件。AivisSpeech HDなどの追加TTSデータをGitHub Releasesから取得します。");
            }
            else
            {
                SetBody(
                    "初回のデータダウンロードを開始します。",
                    $"対象: {count}件。必要なデータをGitHub Releasesから取得します。");
            }
            SetButtons(download: true, retry: false, cancel: false);
            SetProgress(0f, "待機中");
        }

        private async void StartDownload()
        {
            if (currentPlan == null || currentPlan.AssetsToDownload.Count == 0)
            {
                Hide();
                return;
            }

            downloadCancellation?.Cancel();
            downloadCancellation = new CancellationTokenSource();
            var optionalMode = optionalTtsDownloadMode;
            SetButtons(download: false, retry: false, cancel: true);
            SetBody(
                optionalMode ? "追加音声データをダウンロードしています。" : "初回データをダウンロードしています。",
                "完了までアプリを閉じずにお待ちください。");
            try
            {
                var downloader = CreateDownloader();
                var progress = new Progress<YuiLocalAiAssetDownloadProgress>(UpdateProgress);
                var result = await downloader.InstallAssetsAsync(
                    manifest,
                    currentPlan.AssetsToDownload,
                    progress,
                    downloadCancellation.Token);
                if (!result.Success)
                {
                    SetBody(
                        optionalMode ? "追加音声データのインストールに失敗しました。" : "ローカルAIデータのインストールに失敗しました。",
                        result.ErrorMessage);
                    SetButtons(download: false, retry: true, cancel: false);
                    CurrentStatusText = optionalMode
                        ? $"Additional voices: failed ({result.ErrorMessage})"
                        : $"Local AI data: failed ({result.ErrorMessage})";
                    return;
                }

                if (optionalMode)
                {
                    await RefreshOptionalTtsPlanAsync(CancellationToken.None);
                    chatPanel?.RefreshAfterOptionalTtsAssetInstall();
                }
                else
                {
                    await RefreshPlanAsync(CancellationToken.None);
                    chatPanel?.RefreshLocalAiRuntimeAfterAssetInstall();
                }
                var backendSupervisor = GetComponent<YuiDesktopBackendSupervisor>();
                backendSupervisor?.RequestEnsureBackend();
                SetProgress(1f, "完了");
                SetBody(
                    optionalMode ? "追加音声データの準備が完了しました。" : "ローカルAIデータの準備が完了しました。",
                    optionalMode ? "必要に応じてBackendを再起動すると追加TTSが有効になります。" : "Local Gemmaを使用できます。");
                SetButtons(download: false, retry: false, cancel: false);
                await Task.Delay(1200);
                Hide();
            }
            catch (OperationCanceledException)
            {
                SetBody("ダウンロードを中断しました。", "準備できたらもう一度開始してください。");
                SetButtons(download: true, retry: false, cancel: false);
                CurrentStatusText = optionalMode
                    ? "Additional voices: download cancelled"
                    : "Local AI data: download cancelled";
            }
            catch (Exception ex)
            {
                SetBody(
                    optionalMode ? "追加音声データのダウンロードに失敗しました。" : "ローカルAIデータのダウンロードに失敗しました。",
                    ex.Message);
                SetButtons(download: false, retry: true, cancel: false);
                CurrentStatusText = optionalMode
                    ? $"Additional voices: failed ({ex.Message})"
                    : $"Local AI data: failed ({ex.Message})";
            }
        }

        private void CancelDownload()
        {
            downloadCancellation?.Cancel();
        }

        private YuiLocalAiAssetDownloader CreateDownloader()
        {
            return new YuiLocalAiAssetDownloader(
                new YuiUnityAssetHttpClient(),
                AssetStorageRoot(),
                CacheRoot());
        }

        private static string AssetStorageRoot()
        {
            return Application.persistentDataPath;
        }

        private static string CacheRoot()
        {
            return Path.Combine(Application.temporaryCachePath, "YuiLocalAI");
        }

        private static string FormatPlanStatus(YuiLocalAiAssetPlan plan)
        {
            if (plan == null)
            {
                return "Local AI data: not checked";
            }

            switch (plan.State)
            {
                case YuiLocalAiAssetPlanState.UpToDate:
                    return "Local AI data: ready";
                case YuiLocalAiAssetPlanState.NoRequiredAssets:
                    return "Local AI data: no required desktop assets";
                default:
                    return $"Local AI data: {plan.AssetsToDownload.Count} download(s) required";
            }
        }

        private static string FormatOptionalTtsPlanStatus(YuiLocalAiAssetPlan plan)
        {
            if (plan == null)
            {
                return "Additional voices: not checked";
            }

            switch (plan.State)
            {
                case YuiLocalAiAssetPlanState.UpToDate:
                    return "Additional voices: ready";
                case YuiLocalAiAssetPlanState.NoRequiredAssets:
                    return "Additional voices: no add-on assets for this platform";
                default:
                    return $"Additional voices: {plan.AssetsToDownload.Count} download(s) available";
            }
        }

        private void EnsureUi()
        {
            if (root != null)
            {
                return;
            }

            var canvasObject = new GameObject("YuiLocalAiDownloadOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            root = canvasObject;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvasObject.transform, false);
            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.62f);
            Stretch(backdrop.transform);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, 42f);
            panelRect.sizeDelta = new Vector2(740f, 380f);

            titleText = CreateText(panel.transform, "Title", 26, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 1f));
            titleText.text = "初回データダウンロード";
            SetRect(titleText.transform, 36f, 30f, 36f, 44f);

            bodyText = CreateText(panel.transform, "Body", 18, TextAnchor.UpperLeft, new Color(0.93f, 0.95f, 1f, 1f));
            SetRect(bodyText.transform, 36f, 86f, 36f, 82f);

            detailText = CreateText(panel.transform, "Detail", 14, TextAnchor.UpperLeft, new Color(0.68f, 0.75f, 0.86f, 1f));
            SetRect(detailText.transform, 36f, 176f, 36f, 58f);

            progressSlider = CreateSlider(panel.transform);
            SetRect(progressSlider.transform, 36f, 252f, 36f, 18f);

            downloadButton = CreateButton(panel.transform, "DownloadButton", "ダウンロードを開始");
            retryButton = CreateButton(panel.transform, "RetryButton", "もう一度試す");
            cancelButton = CreateButton(panel.transform, "CancelButton", "キャンセル");
            SetRect(downloadButton.transform, 420f, 312f, 36f, 44f);
            SetRect(retryButton.transform, 420f, 312f, 36f, 44f);
            SetRect(cancelButton.transform, 552f, 312f, 36f, 44f);

            downloadButton.onClick.AddListener(StartDownload);
            retryButton.onClick.AddListener(RetryDownloadCheck);
            cancelButton.onClick.AddListener(CancelDownload);
            Hide();
        }

        private void RetryDownloadCheck()
        {
            if (optionalTtsDownloadMode)
            {
                ShowOptionalTtsDownload();
            }
            else
            {
                ShowRepairDownload();
            }
        }

        private void SetTitle(string title)
        {
            if (titleText != null)
            {
                titleText.text = title ?? string.Empty;
            }
        }

        private void Show()
        {
            if (root != null)
            {
                root.SetActive(true);
            }
        }

        private void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void SetBody(string body, string detail)
        {
            if (bodyText != null)
            {
                bodyText.text = body ?? string.Empty;
            }

            if (detailText != null)
            {
                detailText.text = detail ?? string.Empty;
            }
        }

        private void UpdateProgress(YuiLocalAiAssetDownloadProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            var mb = progress.DownloadedBytes / 1024f / 1024f;
            var total = progress.TotalBytes > 0 ? progress.TotalBytes / 1024f / 1024f : 0f;
            var detail = total > 0f
                ? $"{progress.Stage}: {mb:F1} MB / {total:F1} MB"
                : $"{progress.Stage}: {mb:F1} MB";
            SetProgress(progress.Percent, detail);
        }

        private void SetProgress(float value, string detail)
        {
            if (progressSlider != null)
            {
                progressSlider.value = Mathf.Clamp01(value);
            }

            if (detailText != null && !string.IsNullOrWhiteSpace(detail))
            {
                detailText.text = detail;
            }
        }

        private void SetButtons(bool download, bool retry, bool cancel)
        {
            SetButtonVisible(downloadButton, download);
            SetButtonVisible(retryButton, retry);
            SetButtonVisible(cancelButton, cancel);
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
            }
        }

        private static Text CreateText(Transform parent, string name, int size, TextAnchor alignment, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = new Color(0.20f, 0.43f, 0.90f, 1f);
            var button = obj.GetComponent<Button>();

            var labelText = CreateText(obj.transform, "Label", 15, TextAnchor.MiddleCenter, Color.white);
            labelText.text = label;
            Stretch(labelText.transform);
            return button;
        }

        private static Slider CreateSlider(Transform parent)
        {
            var root = new GameObject("Progress", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            background.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.26f, 1f);
            Stretch(background.transform);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch(fillArea.transform);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            fill.GetComponent<Image>().color = new Color(0.33f, 0.78f, 0.58f, 1f);
            Stretch(fill.transform);

            var slider = root.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;
            slider.fillRect = fill.GetComponent<RectTransform>();
            return slider;
        }

        private static void SetRect(Transform transform, float left, float top, float right, float height)
        {
            var rect = transform.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Stretch(Transform transform)
        {
            var rect = transform.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
