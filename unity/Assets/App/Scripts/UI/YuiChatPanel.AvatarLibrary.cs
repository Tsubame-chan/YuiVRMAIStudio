using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Avatar;
using YuiPhysicalAI.Core;
namespace YuiPhysicalAI.UI {
 public sealed partial class YuiChatPanel {
  private GameObject avatarLibraryPanel;
  private int avatarLibraryPage;
  private void ShowAvatarLibrary() {
   if(avatarLibraryPanel!=null)Destroy(avatarLibraryPanel);
   var root=CreateSavedDataPanel("My characters");
   avatarLibraryPanel=root.gameObject;
   ComposerButton(root,"Add","Import avatar",async ()=>{if(await ImportAvatarAsync())ShowAvatarLibrary();},.03f,.78f,.97f,.85f);
   var rows=YuiAvatarLibrary.Read();rows.Insert(0,new YuiAvatarLibrary.Entry { id="builtin:demo", name=YuiUiLocalization.Text("Demo Avatar") });var pages=Mathf.Max(1,(rows.Count+2)/3);avatarLibraryPage=Mathf.Clamp(avatarLibraryPage,0,pages-1);
   for(int i=0;i<3;i++) {
    var index=avatarLibraryPage*3+i;if(index>=rows.Count)break;var entry=rows[index];float y=.575f-i*.195f;
    var builtin=entry.id=="builtin:demo";
   var exists=builtin || File.Exists(YuiAvatarLibrary.Resolve(entry));
    var select=ComposerButton(root,"Avatar"+i,entry.name+"\n"+YuiUiLocalization.Text((builtin ? !YuiAvatarSlots.IsCustomVrm(avatarSlot) : ChatCharacterId()==entry.id) ? "使用中" : exists?"この姿を使う":"ファイルが見つかりません"),async ()=>{
     if(!CanChangeCharacter())return;
     if(builtin) { if(await SetAvatarSlotAsync(GetDefaultAvatarSlot())) { RefreshCharacterSettings(); Destroy(avatarLibraryPanel); } return; }
     if(runtimeVrmImporter==null){ShowAvatarError("Custom avatar importer is not configured", ShowAvatarLibrary);return;}
     Destroy(avatarLibraryPanel);SetStatus("アバターを読み込み中…");
     var ok=await runtimeVrmImporter.ImportFromPathAsync(YuiAvatarLibrary.Resolve(entry),true,YuiAvatarSlots.CustomVrm1,entry.id);
     if(ok){SetAvatarSlot(YuiAvatarSlots.CustomVrm1);RefreshCharacterSettings();SetStatus("アバターを切り替えました");}else ShowAvatarError(runtimeVrmImporter.LastImportMessage, ShowAvatarLibrary);
    },.22f,y,.72f,y+.18f,false);select.interactable=exists;
    if (builtin) continue;
    var rename=ComposerButton(root,"Rename"+i,"名前",()=>ShowAvatarRename(entry),.74f,y+.125f,.97f,y+.18f);
    ComposerButton(root,"Update"+i,"着替え",()=>ShowCharacterAppearances(entry),.74f,y+.062f,.97f,y+.12f);
    ComposerButton(root,"Remove"+i,"一覧から外す",()=>RemoveCharacterFromLibrary(entry),.74f,y,.97f,y+.057f);
    var path=Path.Combine(YuiAvatarLibrary.DirectoryPath,entry.id+".png");
    if(File.Exists(path)){var tex=new Texture2D(2,2);if(tex.LoadImage(File.ReadAllBytes(path))){var g=new GameObject("Preview",typeof(RectTransform),typeof(YuiOwnedPreviewTexture));g.transform.SetParent(root,false);g.GetComponent<YuiOwnedPreviewTexture>().Texture=tex;Place(g.GetComponent<RectTransform>(),.03f,y,.2f,y+.18f);var image=new GameObject("Image",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));image.transform.SetParent(g.transform,false);image.GetComponent<RawImage>().texture=tex;image.GetComponent<RawImage>().raycastTarget=false;var fit=image.GetComponent<AspectRatioFitter>();fit.aspectRatio=(float)tex.width/tex.height;fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;}else Destroy(tex);}
   }
   ComposerButton(root,"Guide","Avatar guide",()=>{Destroy(avatarLibraryPanel);ShowAvatarImportGuide();},.03f,.135f,.97f,.20f);
   ComposerButton(root,"Prev","前へ",()=>{avatarLibraryPage--;ShowAvatarLibrary();},.03f,.02f,.25f,.12f).interactable=avatarLibraryPage>0;
   ComposerButton(root,"Next","次へ",()=>{avatarLibraryPage++;ShowAvatarLibrary();},.75f,.02f,.97f,.12f).interactable=avatarLibraryPage+1<pages;
   var label=ComposerButton(root,"Count",rows.Count==0?"まだ登録されていません":$"{avatarLibraryPage+1} / {pages}",()=>{},.28f,.02f,.72f,.12f);label.interactable=false;
   FocusModal(root);
  }
  private void ShowCharacterAppearances(YuiAvatarLibrary.Entry entry, int page = 0) {
   if (!CanChangeCharacter()) return;
   if (avatarLibraryPanel != null) Destroy(avatarLibraryPanel);
   var root = CreateSavedDataPanel(entry.name + " · " + YuiUiLocalization.Text("着替え"), false);
   // All registered appearances are available; no attempt to infer who an outfit belongs to.
   var description=ComposerButton(root,"Description","Change appearance; keep name, personality, voice and memory.",()=>{},.04f,.77f,.96f,.85f);
   description.interactable=false;description.GetComponent<Image>().color=YuiUiTheme.Surface;
   var appearances = YuiAvatarLibrary.Read()
    .OrderByDescending(e => e.id == entry.id)
    .SelectMany(e => e.appearances ?? new System.Collections.Generic.List<YuiAvatarLibrary.Appearance>())
    .GroupBy(a => a.file).Select(g => g.First()).ToArray();
   var pages = Mathf.Max(1, (appearances.Length + 3) / 4);
   page = Mathf.Clamp(page, 0, pages - 1);
   for (var i = 0; i < 4; i++) {
    var index = page * 4 + i; if (index >= appearances.Length) break;
    var appearance = appearances[index];
    var path = YuiAvatarLibrary.Resolve(new YuiAvatarLibrary.Entry { file = appearance.file });
    var exists = File.Exists(path);
    var y = .65f - i * .13f;
    var button = ComposerButton(root, "Appearance" + i,
     appearance.name + (appearance.file == entry.file ? " · " + YuiUiLocalization.Text("使用中") : exists ? "" : " · " + YuiUiLocalization.Text("ファイルなし")),
     async () => {
      if (!CanChangeCharacter() || runtimeVrmImporter == null) return;
      Destroy(root.gameObject); SetStatus("着替えています…");
      var ok = await runtimeVrmImporter.ImportFromPathAsync(path, true, YuiAvatarSlots.CustomVrm1, entry.id);
      if (ok) { SetAvatarSlot(YuiAvatarSlots.CustomVrm1); RefreshCharacterSettings(); SetStatus(runtimeVrmImporter.LastImportMessage); }
      else ShowAvatarError(runtimeVrmImporter.LastImportMessage, () => ShowCharacterAppearances(entry));
     }, .04f, y, .96f, y + .11f, false);
    button.interactable = exists && appearance.file != entry.file;
   }
   ComposerButton(root, "Prev", "前へ", () => { Destroy(root.gameObject); ShowCharacterAppearances(entry, page - 1); }, .04f, .16f, .28f, .24f).interactable = page > 0;
   ComposerButton(root, "Next", "次へ", () => { Destroy(root.gameObject); ShowCharacterAppearances(entry, page + 1); }, .72f, .16f, .96f, .24f).interactable = page + 1 < pages;
   ComposerButton(root, "Add", "ファイルから新しい外見を追加", async () => { if(await ImportAvatarAsync(null, entry.id))ShowAvatarLibrary(); }, .04f, .04f, .96f, .14f);
  }
  private void RemoveCharacterFromLibrary(YuiAvatarLibrary.Entry entry) {
   if (!CanChangeCharacter()) return;
   if (ChatCharacterId() == entry.id) { SetStatus("別のキャラクターへ切り替えてから一覧から外してください。"); return; }
   try { YuiAvatarLibrary.RemoveFromList(entry); ShowAvatarLibrary(); }
   catch (System.Exception ex) { SetStatus("一覧を更新できません: " + ex.Message); }
  }
  private bool RenameLibraryCharacter(YuiAvatarLibrary.Entry entry, string name) {
   if (string.IsNullOrWhiteSpace(name)) return false;
   try {
    var profile = ProfileStore.Read(entry.id) ?? new YuiCharacterProfile();
    profile.Name = name.Trim(); ProfileStore.Save(entry.id, profile);
    YuiAvatarLibrary.Rename(entry, profile.Name);
    if (activeCharacterProfileId == entry.id) SetCharacterName(profile.Name);
    return true;
   } catch (System.Exception ex) { SetStatus("名前を保存できません: " + ex.Message); return false; }
  }
  private void ShowAvatarRename(YuiAvatarLibrary.Entry entry) {
   if(inputField==null)return;
   Destroy(avatarLibraryPanel);
   var panel=CreateSavedDataPanel("Rename character");
   var field=SavedDataField(panel,entry.name,false);Place(field.GetComponent<RectTransform>(),.04f,.62f,.96f,.73f);
   field.lineType=InputField.LineType.SingleLine;field.characterLimit=80;field.textComponent.alignment=TextAnchor.MiddleLeft;
   if(field.placeholder is Text placeholder)YuiUiLocalization.Set(placeholder,"Character name");
   ComposerButton(panel,"Save","名前を保存",()=>{if(RenameLibraryCharacter(entry,field.text)){Destroy(panel.gameObject);ShowAvatarLibrary();}},.04f,.47f,.63f,.58f);
   ComposerButton(panel,"Cancel","戻る",()=>{Destroy(panel.gameObject);ShowAvatarLibrary();},.68f,.47f,.96f,.58f);field.ActivateInputField();
  }
 }
 public sealed class YuiOwnedPreviewTexture : MonoBehaviour {public Texture2D Texture;private void OnDestroy(){if(Texture!=null)Destroy(Texture);}}
}
