using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Avatar;

namespace YuiPhysicalAI.UI
{
    public sealed partial class YuiChatPanel
    {
        private int localMemoryPage;
        private Task ShowMemoriesAsync()
        {
            var character=ChatCharacterId();
            var root=CreateSavedDataPanel("このキャラクターの記憶 · 端末内");
            ComposerButton(root,"Backend","Backendの記憶",()=>{ _=ShowBackendMemoriesAsync(); },.03f,.03f,.65f,.13f);
            ComposerButton(root,"Add","追加",()=>EditLocalMemory(character,null),.68f,.03f,.97f,.13f);
            ComposerButton(root,"ClearMemory","このキャラクターの記憶を削除",()=>ConfirmLocalMemoryDeletion(character),.03f,.77f,.97f,.84f);
            try {
                var entries=CharacterMemoryStore.Read(character).OrderByDescending(e=>e.CreatedUtc).ToList();
                localMemoryPage=Math.Max(0,Math.Min(localMemoryPage,Math.Max(0,(entries.Count-1)/12)));
                var page=entries.Skip(localMemoryPage*12).Take(12).ToList();
                ComposerButton(root,"Prev","前へ",()=>{localMemoryPage--;_=ShowMemoriesAsync();},.03f,.15f,.45f,.24f).interactable=localMemoryPage>0;
                ComposerButton(root,"Next","次へ",()=>{localMemoryPage++;_=ShowMemoriesAsync();},.55f,.15f,.97f,.24f).interactable=(localMemoryPage+1)*12<entries.Count;
                var viewport=new GameObject("Memories",typeof(RectTransform),typeof(Image),typeof(Mask),typeof(ScrollRect));
                viewport.transform.SetParent(root,false);Place(viewport.GetComponent<RectTransform>(),.03f,.26f,.97f,.75f);
                YuiUiTheme.SurfaceOn(viewport.GetComponent<Image>(),YuiControlAffordance.InputSurface);viewport.GetComponent<Mask>().showMaskGraphic=true;
                var content=new GameObject("Content",typeof(RectTransform));content.transform.SetParent(viewport.transform,false);
                var rect=content.GetComponent<RectTransform>();rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.sizeDelta=new Vector2(0,Mathf.Max(120,page.Count*95));
                var scroll=viewport.GetComponent<ScrollRect>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=rect;scroll.horizontal=false;
                YuiControlAffordance.Scrollbar(scroll);
                for(var i=0;i<page.Count;i++) {
                    var item=page[i];var button=ComposerButton(content.transform,"Memory"+item.Id,(item.Pinned?"★ ":"")+item.Content,()=>EditLocalMemory(character,item),0,0,1,1,false);
                    var r=button.GetComponent<RectTransform>();r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.sizeDelta=new Vector2(0,85);r.anchoredPosition=new Vector2(0,-i*95);
                }
                if(page.Count==0){ SavedDataText(root,YuiUiLocalization.Text("記憶はまだありません。通常の会話で伝えた好みや約束を記録します。追加ボタンから、必ず覚えてほしい内容を登録できます。\n\nローカルAIとAPIで共有します。他のキャラクターには共有しません。シークレット会話は記録しません。")); Place(root.Find("Reader").GetComponent<RectTransform>(),.03f,.26f,.97f,.75f); }
            } catch(Exception ex) { SetStatus("記憶を読み込めません: "+ex.Message); }
            return Task.CompletedTask;
        }
        private void ConfirmLocalMemoryDeletion(string character)
        {
            var root=CreateSavedDataPanel("記憶の削除");
            SavedDataText(root,YuiUiLocalization.Text("このキャラクターの端末内の記憶をすべて削除します。会話履歴・人格設定・他のキャラクターの記憶・Backendの記憶は残ります。取り消せません。"));
            ComposerButton(root,"Cancel","キャンセル",()=>{_=ShowMemoriesAsync();},.03f,.07f,.45f,.19f);
            ComposerButton(root,"Delete","削除する",()=>{
                if(HasStoppableComposerOperation||isSending){SetStatus("返答を停止してから記憶を変更してください。");return;}
                try{CharacterMemoryStore.Clear(character);_=ShowMemoriesAsync();}catch(Exception ex){SetStatus("記憶を削除できません: "+ex.Message);}
            },.55f,.07f,.97f,.19f);
        }
        private void EditLocalMemory(string character,YuiCharacterMemoryStore.Entry entry)
        {
            var root=CreateSavedDataPanel(entry==null?"記憶を追加":"記憶を編集");
            var field=SavedDataField(root,entry?.Content??"",false);field.characterLimit=4000;
            var pinned=entry?.Pinned??true;
            Button pin=null;
            pin=ComposerButton(root,"Pinned",pinned?"優先して参照: ON":"優先して参照: OFF",()=>{pinned=!pinned;YuiUiLocalization.Set(pin.GetComponentInChildren<Text>(),pinned?"優先して参照: ON":"優先して参照: OFF");},.03f,.77f,.97f,.84f);
            Place(field.GetComponent<RectTransform>(),.03f,.23f,.97f,.75f);
            ComposerButton(root,"Save","保存",()=>{
                if(HasStoppableComposerOperation||isSending){SetStatus("返答を停止してから記憶を変更してください。");return;}
                try { CharacterMemoryStore.Save(character,field.text,entry?.Id,pinned);_=ShowMemoriesAsync(); }
                catch(Exception ex){SetStatus("記憶を保存できません: "+ex.Message);}
            },.03f,.07f,.45f,.19f);
            if(entry!=null) {
                var confirmed=false;Button delete=null;
                delete=ComposerButton(root,"Delete","削除",()=>{
                    if(!confirmed){confirmed=true;YuiUiLocalization.Set(delete.GetComponentInChildren<Text>(),"本当に削除");return;}
                    if(HasStoppableComposerOperation||isSending){SetStatus("返答を停止してから記憶を変更してください。");return;}
                    try{CharacterMemoryStore.Delete(character,entry.Id);_=ShowMemoriesAsync();}catch(Exception ex){SetStatus("記憶を削除できません: "+ex.Message);}
                },.48f,.07f,.72f,.19f);
            }
            ComposerButton(root,"Back","戻る",()=>{_=ShowMemoriesAsync();},.75f,.07f,.97f,.19f);
        }
    }
}
