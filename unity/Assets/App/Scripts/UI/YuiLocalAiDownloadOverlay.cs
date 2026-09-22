using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using YuiPhysicalAI.Backend;
using YuiPhysicalAI.LocalAI;

namespace YuiPhysicalAI.UI
{
    public sealed class YuiLocalAiDownloadOverlay : MonoBehaviour
    {
        public const string DefaultManifestUrl = "https://github.com/Tsubame-chan/YuiVRMAIStudio/releases/latest/download/YuiVRMAIStudio_AssetManifest.json";
        public const string ManifestUrlEnvironmentVariable = "YUI_ASSET_MANIFEST_URL";
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
        private bool forceDownloadMode;

        private void OnDestroy()
        {
            downloadCancellation?.Cancel();
            if (root != null) Destroy(root);
        }

        private void Update()
        {
            if (root != null && root.activeSelf && Input.GetKeyDown(KeyCode.Escape)) CancelDownload();
        }

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
                EnsureUi();
                Show();
                SetBody("会話データを確認できませんでした。", "ネットワーク接続を確認して、もう一度試してください。");
                SetButtons(download: false, retry: true, cancel: true);
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
                SetButtons(download: false, retry: true, cancel: true);
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
                SetButtons(download: false, retry: true, cancel: true);
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
            manifest = await downloader.FetchManifestAsync(ResolveManifestUrl(manifestUrl), cancellationToken);
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
            manifest = await downloader.FetchManifestAsync(ResolveManifestUrl(manifestUrl), cancellationToken);
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
            SetTitle(optionalTtsDownloadMode ? "追加音声ダウンロード" : "会話データの準備");
            forceDownloadMode = force && currentPlan != null && currentPlan.State == YuiLocalAiAssetPlanState.UpToDate;
            if (!forceDownloadMode && (currentPlan == null || currentPlan.State != YuiLocalAiAssetPlanState.NeedsDownload))
            {
                if (optionalTtsDownloadMode && currentPlan != null && currentPlan.State == YuiLocalAiAssetPlanState.NoRequiredAssets)
                {
                    SetBody(
                        "このOS向けの追加音声パックはまだありません。",
                        CurrentStatusText);
                    SetButtons(download: false, retry: false, cancel: true);
                    return;
                }

                SetBody(
                    optionalTtsDownloadMode ? "追加音声データは準備できています。" : "ローカルAIデータは準備できています。",
                    CurrentStatusText);
                SetButtons(download: false, retry: false, cancel: true);
                return;
            }

            var count = forceDownloadMode ? currentPlan.Items.Count : currentPlan.AssetsToDownload.Count;
            if (optionalTtsDownloadMode)
            {
                SetBody(
                    "追加音声データをダウンロードします。",
                    $"対象: {count}件。AivisSpeech HDなどの追加TTSデータをGitHub Releasesから取得します。");
            }
            else
            {
                SetBody(
                    forceDownloadMode ? "会話データを再取得して修復します。" : "初回のデータダウンロードを開始します。",
                    $"対象: {count}件。必要なデータをGitHub Releasesから取得します。");
            }
            SetButtons(download: true, retry: false, cancel: true);
            SetProgress(0f, null);
        }

        private async void StartDownload()
        {
            if (currentPlan == null || (!forceDownloadMode && currentPlan.AssetsToDownload.Count == 0))
            {
                Hide();
                return;
            }

            if (downloadCancellation != null) return;
            downloadCancellation = new CancellationTokenSource();
            var optionalMode = optionalTtsDownloadMode;
            SetButtons(download: false, retry: false, cancel: true);
            SetBody(
                optionalMode ? "追加音声データをダウンロードしています。" : "初回データをダウンロードしています。",
                "完了までアプリを閉じずにお待ちください。");
            try
            {
                if (optionalMode) await RefreshOptionalTtsPlanAsync(downloadCancellation.Token);
                else await RefreshPlanAsync(downloadCancellation.Token);
                var assets = forceDownloadMode ? currentPlan.Items.Select(item => item.Asset).ToArray() : currentPlan.AssetsToDownload;
                var downloader = CreateDownloader();
                var progress = new Progress<YuiLocalAiAssetDownloadProgress>(UpdateProgress);
                var result = await downloader.InstallAssetsAsync(
                    manifest,
                    assets,
                    progress,
                    downloadCancellation.Token);
                if (!result.Success)
                {
                    SetBody(
                        optionalMode ? "追加音声データのインストールに失敗しました。" : "ローカルAIデータのインストールに失敗しました。",
                        result.ErrorMessage);
                    SetButtons(download: false, retry: true, cancel: true);
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
                if (chatPanel != null && chatPanel.RequiresBackend) backendSupervisor?.RequestEnsureBackend(forceRestart: true);
                SetProgress(1f, "完了");
                SetBody(
                    optionalMode ? "追加音声データの準備が完了しました。" : "ローカルAIデータの準備が完了しました。",
                    optionalMode ? "必要に応じてBackendを再起動すると追加TTSが有効になります。" : "Local Gemmaを使用できます。");
                SetButtons(download: false, retry: false, cancel: true);
                await Task.Delay(1200);
                Hide();
            }
            catch (OperationCanceledException)
            {
                SetBody("ダウンロードを中断しました。", "準備できたらもう一度開始してください。");
                SetButtons(download: true, retry: false, cancel: true);
                CurrentStatusText = optionalMode
                    ? "Additional voices: download cancelled"
                    : "Local AI data: download cancelled";
            }
            catch (Exception ex)
            {
                SetBody(
                    optionalMode ? "追加音声データのダウンロードに失敗しました。" : "ローカルAIデータのダウンロードに失敗しました。",
                    ex.Message);
                SetButtons(download: false, retry: true, cancel: true);
                CurrentStatusText = optionalMode
                    ? $"Additional voices: failed ({ex.Message})"
                    : $"Local AI data: failed ({ex.Message})";
            }
            finally
            {
                downloadCancellation?.Dispose();
                downloadCancellation = null;
                if (cancelButton != null) YuiUiLocalization.Set(cancelButton.GetComponentInChildren<Text>(),"Close");
            }
        }

        private void CancelDownload()
        {
            if (downloadCancellation != null)
            {
                SetBody("中断しています…", "取得済みのデータは保持します。");
                cancelButton.interactable = false;
                downloadCancellation.Cancel();
            }
            else Hide();
        }

        private YuiLocalAiAssetDownloader CreateDownloader()
        {
            return new YuiLocalAiAssetDownloader(
                new YuiUnityAssetHttpClient(),
                AssetStorageRoot(),
                CacheRoot());
        }

        public static string ResolveManifestUrl(string configuredUrl = null)
        {
            var overrideUrl = Environment.GetEnvironmentVariable(ManifestUrlEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(overrideUrl))
            {
                return overrideUrl.Trim();
            }

            return string.IsNullOrWhiteSpace(configuredUrl)
                ? DefaultManifestUrl
                : configuredUrl.Trim();
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
            // A root canvas avoids inheriting the chat panel's size, clipping and input state.
            // It is owned explicitly by this component and removed in OnDestroy.
            root = canvasObject;
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvasObject.transform, false);
            var backdropImage = backdrop.GetComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.62f);
            Stretch(backdrop.transform);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelImage = panel.GetComponent<Image>();
            YuiUiTheme.SurfaceOn(panelImage,YuiUiTheme.Surface);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(640f, 440f);

            titleText = CreateText(panel.transform, "Title", YuiUiTypography.AtReferenceWidth(YuiUiTypography.Title,720), TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 1f));
            YuiUiLocalization.Set(titleText,"初回データダウンロード");
            SetRect(titleText.transform, 32f, 28f, 32f, 48f);

            bodyText = CreateText(panel.transform, "Body", YuiUiTypography.AtReferenceWidth(YuiUiTypography.Body,720), TextAnchor.UpperLeft, YuiUiTheme.Text);
            SetRect(bodyText.transform, 32f, 94f, 32f, 100f);

            detailText = CreateText(panel.transform, "Detail", YuiUiTypography.AtReferenceWidth(YuiUiTypography.Note,720), TextAnchor.UpperLeft, YuiUiTheme.Muted);
            SetRect(detailText.transform, 32f, 204f, 32f, 78f);

            progressSlider = CreateSlider(panel.transform);
            SetRect(progressSlider.transform, 32f, 306f, 32f, 14f);

            downloadButton = CreateButton(panel.transform, "DownloadButton", "ダウンロードを開始");
            retryButton = CreateButton(panel.transform, "RetryButton", "もう一度試す");
            cancelButton = CreateButton(panel.transform, "CancelButton", "キャンセル");
            SetRect(downloadButton.transform, 260f, 354f, 32f, 56f);
            SetRect(retryButton.transform, 260f, 354f, 32f, 56f);
            SetRect(cancelButton.transform, 32f, 354f, 420f, 56f);

            downloadButton.onClick.AddListener(StartDownload);
            retryButton.onClick.AddListener(RetryDownloadCheck);
            cancelButton.onClick.AddListener(CancelDownload);
            root.SetActive(false);
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
                YuiUiLocalization.Set(titleText,title ?? string.Empty);
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
                var wasVisible = root.activeSelf;
                root.SetActive(false);
                if (wasVisible && chatPanel != null) chatPanel.FocusDesktopComposer();
            }
        }

        private void SetBody(string body, string detail)
        {
            if (bodyText != null)
            {
                YuiUiLocalization.Set(bodyText,body ?? string.Empty,true);
            }

            if (detailText != null)
            {
                YuiUiLocalization.Set(detailText,detail ?? string.Empty,true);
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
                YuiUiLocalization.Set(detailText,detail);
            }
        }

        private void SetButtons(bool download, bool retry, bool cancel)
        {
            SetButtonVisible(downloadButton, download);
            SetButtonVisible(retryButton, retry);
            SetButtonVisible(cancelButton, cancel);
            if (cancelButton != null) cancelButton.interactable = true;
            if (cancelButton != null) YuiUiLocalization.Set(cancelButton.GetComponentInChildren<Text>(),downloadCancellation != null ? "Cancel" : "Close");
            var selected = download ? downloadButton : retry ? retryButton : cancel ? cancelButton : null;
            if (selected != null && root != null && root.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selected.gameObject);
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
            text.font = YuiUiTypography.Regular;
            text.fontSize = size;
            text.raycastTarget = false;
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
            var primary=name!="CancelButton";
            YuiUiTheme.SurfaceOn(image,primary ? YuiUiTheme.Accent : YuiUiTheme.Field);
            var button = obj.GetComponent<Button>();

            var labelText = CreateText(obj.transform, "Label", YuiUiTypography.AtReferenceWidth(YuiUiTypography.Button,720), TextAnchor.MiddleCenter, primary ? new Color32(45,32,65,255) : YuiUiTheme.Text);
            YuiUiLocalization.Set(labelText,label);
            Stretch(labelText.transform);
            return button;
        }

        private static Slider CreateSlider(Transform parent)
        {
            var root = new GameObject("Progress", typeof(RectTransform), typeof(Slider));
            root.transform.SetParent(parent, false);
            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(root.transform, false);
            YuiUiTheme.SurfaceOn(background.GetComponent<Image>(),YuiUiTheme.Field);
            Stretch(background.transform);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            Stretch(fillArea.transform);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            YuiUiTheme.SurfaceOn(fill.GetComponent<Image>(),YuiUiTheme.Accent);
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
