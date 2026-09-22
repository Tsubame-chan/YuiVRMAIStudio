using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Api;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private GameObject savedDataPanel;
        public bool HasSavedDataPanel => savedDataPanel != null && savedDataPanel.activeInHierarchy;
        private bool sharedMemories;
        private int memoriesPage;
        private YuiSavedResultStore ResultStore => new YuiSavedResultStore(Path.Combine(Application.persistentDataPath, "SavedResults"));

        private Transform CreateSavedDataPanel(string title, bool localize = true)
        {
            if (savedDataPanel != null) Destroy(savedDataPanel);
            savedDataPanel = new GameObject("SavedData", typeof(RectTransform), typeof(Image));
            savedDataPanel.transform.SetParent(GetComponentInParent<Canvas>().rootCanvas.transform, false);
            Place(savedDataPanel.GetComponent<RectTransform>(), .02f, .02f, .98f, .98f);
            var modalColor = YuiUiTheme.Surface; modalColor.a = 1;
            YuiUiTheme.SurfaceOn(savedDataPanel.GetComponent<Image>(), modalColor);
            ForegroundModal(savedDataPanel);
            var root = savedDataPanel.transform;
            var heading = ComposerButton(root, "Title", title, () => {}, .03f, .87f, .75f, .97f, localize);
            heading.interactable = false;
            var headingText=heading.GetComponentInChildren<Text>();headingText.fontSize=YuiUiTypography.Title;
            headingText.fontStyle=FontStyle.Bold;headingText.alignment=TextAnchor.MiddleLeft;
            ComposerButton(root, "Close", "閉じる", () => { Destroy(root.gameObject); RefreshCharacterSettings(); }, .78f, .87f, .97f, .97f);
            FocusModal(root);
            return root;
        }

        private void SavedDataText(Transform root, string text)
        {
            var reader=new GameObject("Reader",typeof(RectTransform),typeof(Image),typeof(ScrollRect),typeof(YuiReadOnlyTextView));
            reader.transform.SetParent(root,false);
            Place(reader.GetComponent<RectTransform>(),.03f,.23f,.97f,.83f);
            YuiUiTheme.SurfaceOn(reader.GetComponent<Image>(),YuiControlAffordance.InputSurface);
            reader.GetComponent<YuiReadOnlyTextView>().Configure(text);
        }

        private InputField SavedDataField(Transform root, string text, bool readOnly)
        {
            var field = Instantiate(inputField, root);
            field.onEndEdit.RemoveAllListeners(); field.onValueChanged.RemoveAllListeners(); field.onSubmit.RemoveAllListeners();
            field.lineType = InputField.LineType.MultiLineNewline;
            field.characterLimit = readOnly ? 0 : 20000;
            field.readOnly = readOnly; field.interactable = true;
            field.textComponent.supportRichText = false;
            field.textComponent.alignment = TextAnchor.UpperLeft;
            field.text = text;
            Place(field.GetComponent<RectTransform>(), .03f, .23f, .97f, .83f);
            return field;
        }

        private void ShowSavedResults()
        { savedHistory=true;backendHistory=false;historyPage=0;ShowHistory(); }

        private async Task ShowMemoriesAsync()
        {
            try { await EnsureExternalDataPermissionAsync(backendUrl, false, cancellationTokenSource.Token); }
            catch (OperationCanceledException) { return; }
            var root = CreateSavedDataPanel(sharedMemories ? "以前の共通記憶 · Backend" : "このキャラクターの記憶 · Backend");
            var scope = sharedMemories ? null : ChatCharacterId();
            var owner = userId;
            ComposerButton(root, "Scope", sharedMemories ? "キャラクターの記憶へ" : "以前の共通記憶を見る", () => { sharedMemories = !sharedMemories; memoriesPage = 0; _ = ShowMemoriesAsync(); }, .03f, .03f, .65f, .13f);
            ComposerButton(root, "Add", "追加", () => EditMemory(null, owner, scope), .68f, .03f, .97f, .13f);
            try
            {
                var response = await client.SearchMemoryAsync(new MemorySearchRequest { UserId = owner, CharacterId = scope, Query = "", Limit = 20, Offset = memoriesPage * 20 }, cancellationTokenSource.Token);
                if (root == null || savedDataPanel != root.gameObject) return;
                ComposerButton(root, "Prev", "前へ", () => { memoriesPage--; _ = ShowMemoriesAsync(); }, .03f, .15f, .45f, .24f).interactable = memoriesPage > 0;
                ComposerButton(root, "Next", "次へ", () => { memoriesPage++; _ = ShowMemoriesAsync(); }, .55f, .15f, .97f, .24f).interactable = response.Items.Count == 20;
                // Scrollable list: all returned entries are reachable on mobile.
                var viewport = new GameObject("Memories", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
                viewport.transform.SetParent(root, false); Place(viewport.GetComponent<RectTransform>(), .03f, .26f, .97f, .84f);
                YuiUiTheme.SurfaceOn(viewport.GetComponent<Image>(),YuiControlAffordance.InputSurface);
                viewport.GetComponent<Mask>().showMaskGraphic = true;
                var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(viewport.transform, false);
                var rect = content.GetComponent<RectTransform>(); rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
                rect.sizeDelta = new Vector2(0, Mathf.Max(120, response.Items.Count * 95));
                var scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = viewport.GetComponent<RectTransform>(); scroll.content = rect; scroll.horizontal = false;
                for (var i = 0; i < response.Items.Count; i++)
                {
                    var item = response.Items[i];
                    var button = ComposerButton(content.transform, "Memory" + item.Id, item.Content, () => EditMemory(item, owner, scope), 0, 0, 1, 1, false);
                    var r = button.GetComponent<RectTransform>(); r.anchorMin = new Vector2(0, 1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f, 1); r.sizeDelta = new Vector2(0, 85); r.anchoredPosition = new Vector2(0, -i * 95);
                }
                if (response.Items.Count == 0) ComposerButton(root, "Empty", "記憶はまだありません", () => {}, .05f, .4f, .95f, .6f).interactable = false;
            }
            catch (Exception ex) { if (root != null) ComposerButton(root, "Error", "Backendへ接続できません。閉じて再試行してください。", () => {}, .05f, .4f, .95f, .7f).interactable = false; Debug.LogWarning(ex.Message); }
        }

        private void EditMemory(MemoryItem item, string owner, string scope)
        {
            var root = CreateSavedDataPanel(item == null ? "記憶を追加" : "記憶を編集");
            var field = SavedDataField(root, item?.Content ?? "", false);
            var busy = false;
            ComposerButton(root, "Save", "保存", async () => {
                if (busy || string.IsNullOrWhiteSpace(field.text)) return;
                busy = true;
                try {
                    await EnsureExternalDataPermissionAsync(backendUrl, false, cancellationTokenSource.Token);
                    var request = new MemorySaveRequest { UserId = owner, CharacterId = scope, Content = field.text.Trim(), Importance = item?.Importance ?? 3, Tags = item?.Tags ?? new System.Collections.Generic.List<string>() };
                    if (item == null) await client.SaveMemoryAsync(request, cancellationTokenSource.Token);
                    else await client.UpdateMemoryAsync(item.Id, request, cancellationTokenSource.Token);
                    if (root != null && savedDataPanel == root.gameObject) await ShowMemoriesAsync();
                } catch (Exception ex) { SetStatus("記憶を保存できません: " + ex.Message); } finally { busy = false; }
            }, .03f, .07f, .45f, .19f);
            if (item != null) {
                var confirm = false;
                Button delete = null;
                delete = ComposerButton(root, "Delete", "削除", async () => {
                    if (busy) return;
                    if (!confirm) { confirm = true; YuiUiLocalization.Set(delete.GetComponentInChildren<Text>(),"本当に削除"); return; }
                    busy = true;
                    try { await client.DeleteMemoryAsync(item.Id, new MemorySaveRequest { UserId = owner, CharacterId = scope }, cancellationTokenSource.Token); if (root != null && savedDataPanel == root.gameObject) await ShowMemoriesAsync(); }
                    catch (Exception ex) { SetStatus("記憶を削除できません: " + ex.Message); } finally { busy = false; }
                }, .48f, .07f, .72f, .19f);
            }
            ComposerButton(root, "Back", "戻る", () => { _ = ShowMemoriesAsync(); }, .75f, .07f, .97f, .19f);
        }
    }
}
