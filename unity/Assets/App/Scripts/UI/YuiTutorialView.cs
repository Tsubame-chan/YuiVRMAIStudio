using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace YuiPhysicalAI.UI
{
    // Screenshots remain unmodified. Crop and highlights are lightweight UI elements.
    public sealed class YuiTutorialView : MonoBehaviour
    {
        private int page;
        private RectTransform safe,card,imageSlot,bodyViewport,navigation;
        private RawImage image;
        private Text heading,body,count,imageCaption;
        private CanvasScaler scaler;
        private Button next,back,skip;
        private Vector2 previousSize;
        private static readonly string[] Names={"talk","models","character","api"};
        private static readonly string[] JaTitles={"会話する","AIを選ぶ","キャラクターを変える","推奨のAPIで会話"};
        private static readonly string[] EnTitles={"Start a conversation","Choose your AI","Make it your character","Recommended: API"};
        private static readonly string[] JaBodies={
            "入力欄にメッセージを書き、\nSendを押すと会話が始まります。\n\nTalkは日常会話、Workは相談や\n作業のサポートに使えます。\n\n返答を待っている間や読み上げ中は、\nStopで止められます。",
            "標準の2Bモデルは同梱されているので、\nそのままオフラインで話せます。\n\nより高品質な4Bモデルは、\n設定 → AI → 端末内AIから\nダウンロードして選べます。\n\n4Bは返答に時間がかかり、\n必要な容量や端末の負荷も増えます。",
            "設定 → キャラクターから、\nアバターを変更できます。\n別のアバターを使うには、\nVRMファイルをご用意ください。\n\n「キャラクターの性格・口調」で\n話し方や役柄を指定できます。\n変更したら設定を保存してください。",
            "高品質な会話には、\nOpenAI APIをおすすめします。\n\nOpenAI APIキーと通信が必要です。\n通信料とAPI利用料金がかかります。\nChatGPTの月額プランとは別料金です。\n\nこの案内はヘルプから見直せます。"};
        private static readonly string[] EnBodies={
            "Type a message and press Send to start a conversation.\n\nUse Talk for everyday conversation and Work for tasks.\n\nPress Stop to cancel a reply or speech.",
            "The standard 2B model is included, so you can talk offline right away.\n\nDownload and select the more capable 4B model in Settings → AI → On-device AI.\n\n4B takes longer to reply and needs more storage and memory.",
            "Change your avatar in Settings → Character. You will need a VRM file to use another avatar.\n\nSet tone and role under Character personality / Tone. Save your settings after editing.",
            "OpenAI API is recommended for high-quality conversations.\n\nAn OpenAI API key and internet access are required. Data charges and API fees apply, separately from a ChatGPT subscription.\n\nYou can reopen this guide from Help."};
        private static readonly Rect[] Crops={new Rect(0,.10f,1,.33f),new Rect(.04f,.35f,.92f,.15f),new Rect(.07f,.17f,.86f,.55f),new Rect(.07f,.23f,.86f,.49f)};
        // Normalized areas within each displayed crop, bottom-left origin.
        private static readonly Rect[][] Targets={
            new[]{new Rect(.14f,.04f,.83f,.18f),new Rect(.05f,.86f,.25f,.12f)},
            new[]{new Rect(.08f,.54f,.84f,.38f),new Rect(.08f,.09f,.84f,.38f)},
            new[]{new Rect(.04f,.67f,.92f,.25f),new Rect(.04f,.208f,.92f,.16f)},
            new[]{new Rect(.04f,.611f,.92f,.093f),new Rect(.04f,.166f,.92f,.087f)}};
        public static YuiTutorialView Create(int page)
        {
            var root=new GameObject("YuiTutorial",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32500;
            var view=root.AddComponent<YuiTutorialView>();view.page=page;
            view.scaler=root.GetComponent<CanvasScaler>();view.scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;view.scaler.referenceResolution=new Vector2(390,844);
            var shade=Box(root.transform,"Shade",new Color(0,0,0,.72f));Stretch(shade.rectTransform);
            view.safe=Rect(root.transform,"Safe");view.card=Box(view.safe,"TutorialCard",YuiUiTheme.Surface).rectTransform;YuiUiTheme.Round(view.card.GetComponent<Image>());
            view.heading=Label(view.card,"Heading",21,TextAnchor.MiddleLeft);
            view.skip=Button(view.card,"Skip",YuiSimpleDialog.L("スキップ","Skip"),YuiTutorial.Finish,false,14);
            view.imageSlot=Box(view.card,"ScreenshotSurface",YuiUiTheme.Field).rectTransform;YuiUiTheme.Round(view.imageSlot.GetComponent<Image>());
            var imageRoot=Rect(view.imageSlot,"Screenshot");view.image=imageRoot.gameObject.AddComponent<RawImage>();view.image.raycastTarget=false;
            view.imageCaption=Label(view.imageSlot,"Context",14,TextAnchor.MiddleLeft);view.imageCaption.color=YuiUiTheme.Muted;
            view.bodyViewport=Rect(view.card,"Explanation");view.bodyViewport.gameObject.AddComponent<RectMask2D>();
            var transparent=view.bodyViewport.gameObject.AddComponent<Image>();transparent.color=Color.clear;
            view.body=Label(view.bodyViewport,"Body",17,TextAnchor.UpperLeft);view.body.supportRichText=false;
            view.body.rectTransform.anchorMin=new Vector2(0,1);view.body.rectTransform.anchorMax=Vector2.one;view.body.rectTransform.pivot=new Vector2(.5f,1);view.body.rectTransform.offsetMin=view.body.rectTransform.offsetMax=Vector2.zero;
            view.body.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=view.bodyViewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=view.bodyViewport;scroll.content=view.body.rectTransform;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            view.count=Label(view.card,"Page",14,TextAnchor.MiddleCenter);view.count.color=YuiUiTheme.Muted;
            view.navigation=Rect(view.card,"Navigation");
            view.back=Button(view.navigation,"Back",YuiSimpleDialog.L("戻る","Back"),()=>YuiTutorial.Show(page-1));
            view.next=Button(view.navigation,"Next",YuiSimpleDialog.L(page==3?"会話を始める":"次へ",page==3?"Start chatting":"Next"),()=>{if(page==3)YuiTutorial.Finish();else YuiTutorial.Show(page+1);},true);
            view.Render();view.Layout();YuiUiLocalization.Changed+=view.Render;
            EventSystem.current?.SetSelectedGameObject(view.next.gameObject);
            return view;
        }
        private void Render()
        {
            var ja=YuiSimpleDialog.Japanese;heading.text=(ja?JaTitles:EnTitles)[page];body.text=(ja?JaBodies:EnBodies)[page];count.text=$"{page+1} / 4";
            next.GetComponentInChildren<Text>().text=YuiSimpleDialog.L(page==3?"会話を始める":"次へ",page==3?"Start chatting":"Next");
            back.GetComponentInChildren<Text>().text=YuiSimpleDialog.L("戻る","Back");skip.GetComponentInChildren<Text>().text=YuiSimpleDialog.L("スキップ","Skip");
            back.gameObject.SetActive(page>0);
            imageCaption.text=page==0?YuiSimpleDialog.L("会話画面","Conversation"):page==1?YuiSimpleDialog.L("設定 → AI → 端末内AI","Settings → AI → On-device AI"):page==2?YuiSimpleDialog.L("設定 → キャラクター","Settings → Character"):YuiSimpleDialog.L("設定 → AI","Settings → AI");
            image.texture=Resources.Load<Texture2D>("YuiTutorial/"+Names[page]+(ja?"-ja":"-en"));image.uvRect=Crops[page];
            foreach(Transform child in image.transform)Destroy(child.gameObject);
            if(image.texture!=null)for(var n=0;n<Targets[page].Length;n++)Highlight(image.transform,Targets[page][n],n+1);
            previousSize=Vector2.zero;
        }
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape))YuiTutorial.Finish();
            else if(Input.GetKeyDown(KeyCode.RightArrow) && page<3)YuiTutorial.Show(page+1);
            else if(Input.GetKeyDown(KeyCode.LeftArrow) && page>0)YuiTutorial.Show(page-1);
            if(new Vector2(Screen.width,Screen.height)!=previousSize || TouchScreenKeyboard.visible)Layout();
        }
        private void Layout()
        {
            previousSize=new Vector2(Screen.width,Screen.height);var portrait=Screen.width<=Screen.height;scaler.matchWidthOrHeight=portrait?0:1;
            var area=Screen.safeArea;safe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);safe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
            var scale=portrait?Screen.width/390f:Screen.height/844f;
            var width=Mathf.Min(360,area.width/scale-24);var height=Mathf.Min(760,area.height/scale-24);
            card.anchorMin=card.anchorMax=new Vector2(.5f,.5f);card.sizeDelta=new Vector2(width,height);card.anchoredPosition=Vector2.zero;
            At(heading.rectTransform,18,14,width-106,52);At((RectTransform)skip.transform,width-78,18,62,44);
            var imageHeight=Mathf.Min(page==1?210:330,height*.43f);At(imageSlot,18,80,width-36,imageHeight);
            At(imageCaption.rectTransform,12,4,width-60,28);
            if(image.texture!=null)
            {
                var aspect=image.texture.width*Crops[page].width/(image.texture.height*Crops[page].height);
                var w=width-44;var h=imageHeight-40;if(w/h>aspect)w=h*aspect;else h=w/aspect;
                var r=image.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(w,h);r.anchoredPosition=new Vector2(0,-14);
            }
            var bodyTop=80+imageHeight+20;At(bodyViewport,20,bodyTop,width-40,Mathf.Max(70,height-bodyTop-112));
            At(count.rectTransform,20,height-106,width-40,22);At(navigation,18,height-72,width-36,48);
            Stretch((RectTransform)next.transform);Stretch((RectTransform)back.transform);
            if(page>0){var r=(RectTransform)back.transform;r.anchorMax=new Vector2(.34f,1);var nr=(RectTransform)next.transform;nr.anchorMin=new Vector2(.38f,0);}
        }
        public void Close(){gameObject.SetActive(false);Destroy(gameObject);}
        private void OnDestroy(){YuiUiLocalization.Changed-=Render;}
        private static void Highlight(Transform parent,Rect area,int number)
        {
            var root=Rect(parent,"Highlight"+number);root.anchorMin=area.min;root.anchorMax=area.max;root.offsetMin=root.offsetMax=Vector2.zero;
            foreach(var edge in new[]{new Vector4(0,0,1,0),new Vector4(0,1,1,1),new Vector4(0,0,0,1),new Vector4(1,0,1,1)})
            {
                var bar=Box(root,"Outline",YuiUiTheme.Accent);bar.raycastTarget=false;bar.rectTransform.anchorMin=new Vector2(edge.x,edge.y);bar.rectTransform.anchorMax=new Vector2(edge.z,edge.w);bar.rectTransform.offsetMin=bar.rectTransform.offsetMax=Vector2.zero;bar.rectTransform.sizeDelta=edge.x==edge.z?new Vector2(3,0):new Vector2(0,3);
            }
        }
        private static Button Button(Transform parent,string name,string caption,Action callback,bool primary=false,int size=16)
        {
            var image=Box(parent,name,YuiUiTheme.Field);var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;
            var t=Label(image.transform,"Label",size,TextAnchor.MiddleCenter);t.text=caption;Stretch(t.rectTransform);
            YuiUiTheme.ButtonStyle(b,primary);t.fontSize=size;b.onClick.AddListener(()=>callback());return b;
        }
        private static RectTransform Rect(Transform parent,string name){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
        private static Image Box(Transform parent,string name,Color color){var r=Rect(parent,name);var i=r.gameObject.AddComponent<Image>();i.color=color;return i;}
        private static Text Label(Transform parent,string name,int size,TextAnchor alignment){var r=Rect(parent,name);var t=r.gameObject.AddComponent<Text>();t.font=YuiUiTypography.Regular;t.fontSize=size;t.color=YuiUiTheme.Text;t.alignment=alignment;t.raycastTarget=false;t.supportRichText=false;return t;}
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static void At(RectTransform r,float left,float top,float width,float height){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(left,-top);r.sizeDelta=new Vector2(width,height);}
    }
}
