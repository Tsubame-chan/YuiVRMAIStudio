using System.IO;
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
   avatarLibraryPanel=new GameObject("AvatarLibrary",typeof(RectTransform),typeof(Image));avatarLibraryPanel.transform.SetParent(transform,false);Place(avatarLibraryPanel.GetComponent<RectTransform>(),.02f,.02f,.98f,.98f);avatarLibraryPanel.GetComponent<Image>().color=new Color(.055f,.07f,.11f,.99f);
   var root=avatarLibraryPanel.transform;
   ComposerButton(root,"Add","VRM / ZIPを追加",()=>{Destroy(avatarLibraryPanel);ImportCustomVrmFromFilePicker();},.03f,.85f,.73f,.97f);
   ComposerButton(root,"Close","閉じる",()=>Destroy(avatarLibraryPanel),.77f,.85f,.97f,.97f);
   var rows=YuiAvatarLibrary.Read();var pages=Mathf.Max(1,(rows.Count+2)/3);avatarLibraryPage=Mathf.Clamp(avatarLibraryPage,0,pages-1);
   for(int i=0;i<3;i++) {
    var index=avatarLibraryPage*3+i;if(index>=rows.Count)break;var entry=rows[index];float y=.62f-i*.22f;
    var exists=File.Exists(YuiAvatarLibrary.Resolve(entry));
    var select=ComposerButton(root,"Avatar"+i,entry.name+(exists?"\nこの姿を使う":"\nファイルが見つかりません"),async ()=>{
     if(runtimeVrmImporter==null){SetStatus("アバター読込の準備ができていません");return;}
     Destroy(avatarLibraryPanel);SetStatus("アバターを読み込み中…");
     var ok=await runtimeVrmImporter.ImportFromPathAsync(YuiAvatarLibrary.Resolve(entry),true,YuiAvatarSlots.CustomVrm1);
     if(ok)SetAvatarSlot(YuiAvatarSlots.CustomVrm1);SetStatus(ok?"アバターを切り替えました":runtimeVrmImporter.LastImportMessage);
    },.22f,y,.72f,y+.18f);select.interactable=exists;
    var rename=ComposerButton(root,"Rename"+i,"名前",()=>ShowAvatarRename(entry),.74f,y+.095f,.97f,y+.18f);
    ComposerButton(root,"Remove"+i,"一覧から外す",()=>{YuiAvatarLibrary.RemoveFromList(entry);ShowAvatarLibrary();},.74f,y,.97f,y+.085f);
    var path=Path.Combine(YuiAvatarLibrary.DirectoryPath,entry.id+".png");
    if(File.Exists(path)){var tex=new Texture2D(2,2);if(tex.LoadImage(File.ReadAllBytes(path))){var g=new GameObject("Preview",typeof(RectTransform),typeof(RawImage),typeof(YuiOwnedPreviewTexture));g.transform.SetParent(root,false);g.GetComponent<RawImage>().texture=tex;g.GetComponent<YuiOwnedPreviewTexture>().Texture=tex;Place(g.GetComponent<RectTransform>(),.03f,y,.2f,y+.18f);}else Destroy(tex);}
   }
   ComposerButton(root,"Prev","前へ",()=>{avatarLibraryPage--;ShowAvatarLibrary();},.03f,.02f,.25f,.12f).interactable=avatarLibraryPage>0;
   ComposerButton(root,"Next","次へ",()=>{avatarLibraryPage++;ShowAvatarLibrary();},.75f,.02f,.97f,.12f).interactable=avatarLibraryPage+1<pages;
   var label=ComposerButton(root,"Count",rows.Count==0?"まだ登録されていません":$"{avatarLibraryPage+1} / {pages}",()=>{},.28f,.02f,.72f,.12f);label.interactable=false;
  }
  private void ShowAvatarRename(YuiAvatarLibrary.Entry entry) {
   if(inputField==null)return;
   Destroy(avatarLibraryPanel);
   var panel=new GameObject("RenameAvatar",typeof(RectTransform),typeof(Image));panel.transform.SetParent(transform,false);Place(panel.GetComponent<RectTransform>(),.03f,.36f,.97f,.84f);panel.GetComponent<Image>().color=new Color(.055f,.07f,.11f,1);
   var field=Instantiate(inputField,panel.transform);Place(field.GetComponent<RectTransform>(),.04f,.5f,.96f,.9f);field.text=entry.name;field.onEndEdit.RemoveAllListeners();field.onValueChanged.RemoveAllListeners();field.lineType=InputField.LineType.SingleLine;
   ComposerButton(panel.transform,"Save","名前を保存",()=>{YuiAvatarLibrary.Rename(entry,field.text);Destroy(panel);ShowAvatarLibrary();},.04f,.1f,.63f,.4f);
   ComposerButton(panel.transform,"Cancel","戻る",()=>{Destroy(panel);ShowAvatarLibrary();},.68f,.1f,.96f,.4f);field.ActivateInputField();
  }
 }
 public sealed class YuiOwnedPreviewTexture : MonoBehaviour {public Texture2D Texture;private void OnDestroy(){if(Texture!=null)Destroy(Texture);}}
}
