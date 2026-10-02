#if YUI_TUTORIAL_CAPTURE_REVIEW && UNITY_STANDALONE_OSX
using System.IO;
using System.Linq;
using YuiPhysicalAI.Avatar;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.LocalAI;
using YuiPhysicalAI.Api;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace YuiPhysicalAI.UI
{
    // Opt-in Mac review builds only. Does not ship in a release player.
    public sealed class YuiTutorialCaptureReview : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Prepare()
        {
            // A separate review product owns these preferences; never change the user's app.
            if(!Application.productName.Contains("Tutorial Review"))return;
            PlayerPrefs.SetInt(YuiTutorial.CompletedKey,1);
            if(!System.Environment.GetCommandLineArgs().Any(a=>(a.StartsWith("--yui-avatar-restore=") || a.StartsWith("--yui-window-review="))))
                PlayerPrefs.SetString(YuiAvatarSelectionPrefs.Key,YuiAvatarSlots.UnityChanDefault);
            PlayerPrefs.SetString(YuiPrefsKeys.ConversationMode,YuiConversationModes.LocalAi);
            PlayerPrefs.SetInt(YuiPrefsKeys.SecretMode,1);
            PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPreset,1);
            PlayerPrefs.SetInt(YuiPrefsKeys.WindowResolutionPresetListVersion,2);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartCaptureReview()
        {
            Screen.SetResolution(390,844,false);
            var root=new GameObject("Tutorial capture review");
            DontDestroyOnLoad(root);root.AddComponent<YuiTutorialCaptureReview>();
            Debug.Log("Tutorial capture directory: "+Path.Combine(Application.persistentDataPath,"TutorialCaptures"));
        }
        private System.Collections.IEnumerator Start()
        {
            yield return null; yield return null; Screen.SetResolution(390,844,FullScreenMode.Windowed);
            var storeCapture=System.Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--yui-store-capture="));
            if(storeCapture!=null){yield return CaptureStoreScreens(storeCapture.Substring("--yui-store-capture=".Length));yield break;}
            var window=System.Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--yui-window-review="));
            if(window!=null){RunWindowReview(window.Substring("--yui-window-review=".Length));yield break;}
            var restore=System.Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--yui-avatar-restore="));
            if(restore!=null){RunAvatarProbe(FindObjectOfType<YuiChatPanel>(),restore.Substring("--yui-avatar-restore=".Length),true);yield break;}
            var avatarReview=System.Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("--yui-avatar-review="));
            if(avatarReview!=null){RunAvatarProbe(FindObjectOfType<YuiChatPanel>(),avatarReview.Substring("--yui-avatar-review=".Length));yield break;}
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--yui-memory-review")<0)yield break;
            var panel=FindObjectOfType<YuiChatPanel>();
            if(panel==null){Debug.LogError("Review panel unavailable.");yield break;}
            RunProbe(panel,true);
            while(probing)yield return null;
            RunApiProbe(true);
        }
        private async void RunWindowReview(string output)
        {
            Directory.CreateDirectory(output);var results=new List<object>();
            try {
                var importer=FindObjectOfType<YuiRuntimeVrmImporter>();var switcher=FindObjectOfType<YuiAvatarSwitcher>();
                for(var i=0;i<300 && (importer.IsImporting || switcher.ActiveAvatar==null || switcher.ActiveAvatar.GetComponent<UniVRM10.Vrm10Instance>()==null);i++)await Task.Delay(100);
                var root=switcher.ActiveAvatar;var scale=root.transform.lossyScale;
                var camera=Camera.main;var controller=FindObjectOfType<YuiWindowResolutionController>();
                YuiAvatarLibrary.CaptureThumbnail(YuiAvatarLibrary.Read().Last());
                var viewer=FindObjectOfType<YuiConsoleVisibilityController>();
                foreach(var label in new[]{"S","M","L","landscape","viewer-L-90","viewer-L-180","final-L"}) {
                    if(label=="landscape")Screen.SetResolution(1000,700,FullScreenMode.Windowed);
                    else controller.SetPreset(label=="S"?1:label=="M"?2:3);
                    if(label.StartsWith("viewer-L")){
                        if(label=="viewer-L-90"){viewer.HideConsole();viewer.SetViewerRotatesAvatar(true);}
                        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                        var yaw=(float)viewer.GetType().GetField("defaultYaw",flags).GetValue(viewer);
                        viewer.GetType().GetField("currentYaw",flags).SetValue(viewer,yaw+(label.EndsWith("90")?90:180));
                    }
                    if(label=="final-L")viewer.GetType().GetMethod("SetConsoleVisible",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(viewer,new object[]{true});
                    await Task.Delay(1500);
                    var center=camera.transform.position+camera.transform.forward*2;
                    var x=Vector3.Distance(camera.WorldToScreenPoint(center-camera.transform.right*.1f),camera.WorldToScreenPoint(center+camera.transform.right*.1f));
                    var y=Vector3.Distance(camera.WorldToScreenPoint(center-camera.transform.up*.1f),camera.WorldToScreenPoint(center+camera.transform.up*.1f));
                    var ratio=x/y;var pixelAspect=(float)camera.pixelWidth/camera.pixelHeight;
                    results.Add(new{label,width=Screen.width,height=Screen.height,camera_aspect=camera.aspect,pixel_aspect=pixelAspect,equal_axes_pixel_ratio=ratio,scale_unchanged=(root.transform.lossyScale-scale).sqrMagnitude<.000001f,ok=Mathf.Abs(ratio-1)<.005f && Mathf.Abs(camera.aspect-pixelAspect)<.005f});
                    StartCoroutine(Capture(Path.Combine(output,label+".png")));
                    Debug.Log("Review window "+label+" "+Screen.width+"x"+Screen.height+" aspect="+camera.aspect+" axes="+ratio);
                }
                File.WriteAllText(Path.Combine(output,"window.json"),JsonConvert.SerializeObject(results,Formatting.Indented));
            }catch(System.Exception e){File.WriteAllText(Path.Combine(output,"error.txt"),e.ToString());Debug.LogException(e);}
        }
        private System.Collections.IEnumerator CaptureStoreScreens(string output)
        {
            Directory.CreateDirectory(output);
            Application.runInBackground=true;
            var tablet=System.Environment.GetCommandLineArgs().Contains("--yui-store-ipad");
            Screen.SetResolution(tablet?1024:540,tablet?1366:1170,FullScreenMode.Windowed);
            yield return new WaitForSeconds(4);
            var panel=FindObjectOfType<YuiChatPanel>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            // Use the unchanged native VOICEVOX playback and lip-sync path.
            // The isolated review data contains copies of the reviewed voice data.
            typeof(YuiChatPanel).GetField("ttsMode",flags).SetValue(panel,"voicevox-native");
            panel.SetSecretMode(false);
            typeof(YuiChatPanel).GetField("customInstruction",flags).SetValue(panel,"親しみやすい言葉で話してください。提案は短く、一つだけ教えてください。");
            YuiUiLocalization.SetLanguage("ja");
            CloseViews();
            // A real request through the app, not a fabricated assistant reply.
            var input=(InputField)typeof(YuiChatPanel).GetField("inputField",flags).GetValue(panel);
            input.text="ちょっと気分転換したいな";
            typeof(YuiChatPanel).GetMethod("SendCurrentInput",flags).Invoke(panel,null);
            var speech=(AudioSource)typeof(YuiChatPanel).GetField("audioSource",flags).GetValue(panel);
            var capturedSpeech=false;
            for(var i=0;i<1800 && (bool)typeof(YuiChatPanel).GetField("isSending",flags).GetValue(panel);i++) {
                if(!capturedSpeech && speech!=null && speech.isPlaying) {
                    yield return new WaitForSeconds(.20f);
                    for(var frame=0;frame<10 && speech.isPlaying;frame++) {
                        yield return Capture(Path.Combine(output,"talk-ja-speaking-"+frame+".png"));
                        yield return new WaitForSeconds(.15f);
                    }
                    capturedSpeech=true;
                    File.WriteAllText(Path.Combine(output,"speaking.json"),JsonConvert.SerializeObject(new{is_playing=speech.isPlaying,speech_time=speech.time,clip_seconds=speech.clip==null?0:speech.clip.length,lip_sync_weight=typeof(YuiSimpleLipSync).GetField("currentWeight",flags).GetValue(FindObjectOfType<YuiSimpleLipSync>())},Formatting.Indented));
                }
                yield return new WaitForSeconds(.1f);
            }
            var log=FindObjectOfType<YuiChatLogView>();
            File.WriteAllText(Path.Combine(output,"conversation.json"),JsonConvert.SerializeObject(new {
                empty=log.IsEmpty,
                texts=log.GetComponentsInChildren<Text>(true).Select(t=>new{name=t.name,text=t.text,active=t.gameObject.activeInHierarchy,position=t.transform.position.ToString(),rect=t.rectTransform.rect.ToString()})
            },Formatting.Indented));
            foreach(var language in new[]{"ja","en"})
            {
                if(language=="en"){CloseViews();YuiUiLocalization.SetLanguage(language);}
                yield return new WaitForSeconds(1);
                yield return Capture(Path.Combine(output,"talk-"+language+".png"));
                FindObjectOfType<YuiSettingsOverlay>().Show();InvokeButton("SettingsTab2");
                yield return new WaitForSeconds(1);yield return Capture(Path.Combine(output,"character-"+language+".png"));
                CloseViews();YuiLocalModelMenu.Show(panel);
                yield return new WaitForSeconds(1);yield return Capture(Path.Combine(output,"models-"+language+".png"));
                foreach(var b in FindObjectsOfType<Button>())if(b.GetComponentInChildren<Text>()?.text==YuiSimpleDialog.L("選択中のモデル · 詳細設定","Selected model · Advanced")){b.onClick.Invoke();break;}
                yield return new WaitForSeconds(1);yield return Capture(Path.Combine(output,"advanced-"+language+".png"));
                CloseViews();panel.OpenCharacterMemories();
                yield return new WaitForSeconds(1);yield return Capture(Path.Combine(output,"memory-"+language+".png"));
                CloseViews();FindObjectOfType<YuiSettingsOverlay>().Show();InvokeButton("SettingsTab2");
                yield return new WaitForSeconds(1);yield return Capture(Path.Combine(output,"avatar-"+language+".png"));
            }
            CloseViews();YuiUiLocalization.SetLanguage("ja");
            var viewer=FindObjectOfType<YuiConsoleVisibilityController>();
            viewer.SetViewerRotatesAvatar(true);viewer.HideConsole();
            var yaw=(float)viewer.GetType().GetField("defaultYaw",flags).GetValue(viewer);
            foreach(var angle in new[]{25f,80f,-25f}) {
                viewer.GetType().GetField("currentYaw",flags).SetValue(viewer,yaw-angle);
                yield return new WaitForSeconds(.3f);
                yield return Capture(Path.Combine(output,"viewer-"+angle+".png"));
            }
            viewer.GetType().GetMethod("SetConsoleVisible",flags).Invoke(viewer,new object[]{true});
            var character=(string)typeof(YuiChatPanel).GetMethod("ChatCharacterId",flags).Invoke(panel,null);
            typeof(YuiChatPanel).GetField("historyCharacter",flags).SetValue(panel,character);
            typeof(YuiChatPanel).GetMethod("ConfirmHistoryDeletion",flags).Invoke(panel,new object[]{false});
            InvokeButton("ConfirmDelete");yield return new WaitForSeconds(1);
            typeof(YuiChatPanel).GetMethod("ConfirmLocalMemoryDeletion",flags).Invoke(panel,new object[]{character});
            InvokeButton("Delete");yield return new WaitForSeconds(1);CloseViews();
            var store=(YuiCharacterMemoryStore)typeof(YuiChatPanel).GetProperty("CharacterMemoryStore",flags).GetValue(panel);
            File.WriteAllText(Path.Combine(output,"complete.txt"),"UnityChan normal-mode runtime screenshots; test history cleared through the app confirmation button; remaining memories="+store.Read(character).Count);
        }
        private async void RunAvatarProbe(YuiChatPanel panel,string output,bool restoring=false)
        {
            Directory.CreateDirectory(output);
            try {
                if(panel==null || (!restoring && !await panel.ImportAvatarAsync()))throw new System.InvalidOperationException("Avatar picker/import did not complete successfully.");
                var switcher=FindObjectOfType<YuiPhysicalAI.Avatar.YuiAvatarSwitcher>();
                if(restoring){
                    for(var n=0;n<300 && (switcher.ActiveAvatar==null || switcher.ActiveAvatar.GetComponent<UniVRM10.Vrm10Instance>()==null || FindObjectOfType<YuiRuntimeVrmImporter>().IsImporting);n++)await Task.Delay(100);
                    if(switcher.ActiveAvatar==null || switcher.ActiveAvatar.GetComponent<UniVRM10.Vrm10Instance>()==null)throw new System.InvalidOperationException("Saved avatar did not restore.");
                }
                await Task.Delay(2000);
                var root=switcher.ActiveAvatar;
                var faces=root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled && r.sharedMesh!=null).ToArray();
                var weights=faces.Select(r=>Enumerable.Range(0,r.sharedMesh.blendShapeCount).Select(r.GetBlendShapeWeight).ToArray()).ToArray();
                var instance=root.GetComponent<UniVRM10.Vrm10Instance>();
                var bones=instance.SpringBone.Springs.SelectMany(a=>a.Joints).Where(j=>j!=null).Select(j=>j.transform).Distinct().ToArray();
                var rest=bones.Select(b=>b.localRotation).ToArray();float blink=0,motion=0;
                for(var n=0;n<120;n++){
                    await Task.Delay(100);
                    for(var i=0;i<faces.Length;i++)for(var j=0;j<weights[i].Length;j++)blink=Mathf.Max(blink,Mathf.Abs(faces[i].GetBlendShapeWeight(j)-weights[i][j]));
                    for(var i=0;i<bones.Length;i++)motion=Mathf.Max(motion,Quaternion.Angle(rest[i],bones[i].localRotation));
                }
                var lip=FindObjectOfType<YuiSimpleLipSync>();var wasEnabled=lip.enabled;lip.enabled=false;
                var set=typeof(YuiSimpleLipSync).GetMethod("SetVisemes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var vowelWeights=new System.Collections.Generic.List<float>();
                foreach(var preset in new[]{UniVRM10.ExpressionPreset.aa,UniVRM10.ExpressionPreset.ih,UniVRM10.ExpressionPreset.ou,UniVRM10.ExpressionPreset.ee,UniVRM10.ExpressionPreset.oh}) {
                    var values=new object[]{0f,0f,0f,0f,0f};values[vowelWeights.Count]=100f;
                    set.Invoke(lip,values);await Task.Delay(100);
                    vowelWeights.Add(instance.Runtime.Expression.GetWeight(new UniVRM10.ExpressionKey(preset)));
                }
                set.Invoke(lip,new object[]{0f,0f,0f,0f,0f});lip.enabled=wasEnabled;
                var imported=YuiPhysicalAI.Avatar.YuiAvatarLibrary.Read().Last();var copy=YuiPhysicalAI.Avatar.YuiAvatarLibrary.Resolve(imported);
                var shaders=faces.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(m=>m.shader.name).Distinct().ToArray();
                var result=new {ok=root.GetComponent<Animator>().isHuman && blink>0 && vowelWeights.All(v=>v>.9f) && motion>0 && File.Exists(copy),character_id=imported.id,owned_copy=copy,renderers=faces.Length,blink_delta=blink,vowel_weights=vowelWeights,restored=restoring,spring_joints=bones.Length,spring_rotation_degrees=motion,shaders};
                File.WriteAllText(Path.Combine(output,"player-import.json"),JsonConvert.SerializeObject(result,Formatting.Indented));
                StartCoroutine(Capture(Path.Combine(output,"player-import.png")));Debug.Log("Review avatar import: "+JsonConvert.SerializeObject(result));
            }catch(System.Exception e){File.WriteAllText(Path.Combine(output,"player-error.txt"),e.ToString());Debug.LogException(e);}
        }
        private static void CloseViews()
        {
            foreach(var panel in FindObjectsOfType<YuiChatPanel>())
            {
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var saved=(GameObject)typeof(YuiChatPanel).GetField("savedDataPanel",flags).GetValue(panel);
                if(saved!=null)Destroy(saved);
            }
            foreach(var dialog in FindObjectsOfType<YuiSimpleDialog>())dialog.Close();
            foreach(var guide in FindObjectsOfType<YuiTutorialView>())guide.Close();
            FindObjectOfType<YuiSettingsOverlay>()?.Hide();
            FindObjectOfType<YuiHelpOverlay>()?.Hide();
        }
        private static void InvokeButton(string name)
        {
            foreach(var button in FindObjectsOfType<Button>())
                if(button.name==name){button.onClick.Invoke();return;}
            Debug.LogWarning("Review control missing: "+name);
        }
        private System.Collections.IEnumerator Capture(string path)
        {
            yield return new WaitForEndOfFrame();
            var pixels=ScreenCapture.CaptureScreenshotAsTexture(2);
            File.WriteAllBytes(path,pixels.EncodeToPNG());Destroy(pixels);
        }
        private static string ReviewMemoryContext()
        {
            var directory=Path.Combine(Path.GetTempPath(),"YuiMemoryReview-"+System.Guid.NewGuid().ToString("N"));
            try {
                var store=new YuiPhysicalAI.Avatar.YuiCharacterMemoryStore(directory);
                store.Remember("a","私はカレーライスが一番好き。中に入れる肉は鶏肉が好き。",false);
                for(var i=0;i<30;i++)store.Remember("a","私は作業"+i+"を終えた。",false);
                store.Remember("b","私は魚が好き。",false);
                store.Remember("a","私は秘密の料理が好き。",true);
                return new YuiPhysicalAI.Avatar.YuiCharacterMemoryStore(directory).Context("a","好きな食べ物とお肉");
            } finally {if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
        private bool probing;
        private bool resetFixtureActive;
        private async void RunProbe(YuiChatPanel panel, bool memoryOnly=false)
        {
            if(probing || panel==null)return;probing=true;CloseViews();
            var selected=YuiLocalModelSelection.SelectedId;
            var results=new List<object>();
            try
            {
                foreach(var pack in YuiLocalModelSelection.Available)
                {
                    if(!YuiLocalModelSelection.Installed(pack))continue;
                    YuiLocalModelSelection.Select(pack.Id);panel.RefreshLocalAiRuntimeAfterAssetInstall();
                    var field=typeof(YuiChatPanel).GetField("localAiService",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    var service=(YuiLocalAiService)field.GetValue(panel);
                    var route=new YuiAiRuntimeRouter(service,(r,t)=>throw new System.InvalidOperationException("Unexpected backend route"),(a,f,u,t)=>throw new System.InvalidOperationException("Unexpected transcription route"),(a,f,u,r,t)=>throw new System.InvalidOperationException("Unexpected vision route"));
                    route.PreferLocal=true;route.FallbackToBackend=false;
                    foreach(var mode in memoryOnly?new[]{"memory","roles"}:new[]{"talk","work","memory"})
                    {
                        var timer=System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            using var cancel=new CancellationTokenSource(180000);
                            var answer=await route.SendChatAsync(new ChatRequest{RequestId="review-"+pack.Id+"-"+mode,UserId="tutorial-review",Mode=(mode=="memory"||mode=="roles")?"talk":mode,Context=new RequestContext{Extra=new Dictionary<string,object>{{YuiPhysicalAI.Avatar.YuiCharacterMemoryStore.ContextKey,mode=="memory"?ReviewMemoryContext():""}}},Secret=true,CharacterName=mode=="roles"?"B":"ユイ",CustomInstruction="親しい友達のようにフレンドリーな日本語で話してください。",ResponseInstruction="回答を「結論:」で始め、短く答えてください。",Message=mode=="roles"?"私の名前はA、あなたの名前はBです。職場でCさんの当たりがきつくて困っています。Cさんに『あの資料、まだできていないの？』と言われた時、私はどう返すといいでしょう。返答例を一つ、誰が返すのかも短く教えて。":mode=="memory"?"私の好きな食べ物と、その中に入れる好きなお肉を教えて。":mode=="work"?"徳川家康の次の征夷大将軍は誰ですか。名前だけ短く答えてください。":"4+4-3の答えを短く教えてください。"},cancel.Token);
                            results.Add(new{model=pack.Id,mode,seconds=timer.Elapsed.TotalSeconds,ok=true,text=answer.Text});
                            Debug.Log("Review inference "+pack.Id+" "+mode+" "+timer.Elapsed.TotalSeconds.ToString("0.00")+"s "+answer.Text);
                        }
                        catch(System.Exception error){results.Add(new{model=pack.Id,mode,seconds=timer.Elapsed.TotalSeconds,ok=false,error=error.Message});Debug.LogWarning("Review inference failed: "+error.Message);}
                    }
                }
            }
            finally
            {
                YuiLocalModelSelection.Select(selected);panel.RefreshLocalAiRuntimeAfterAssetInstall();probing=false;
                File.WriteAllText(Path.Combine(Application.persistentDataPath,"review-inference.json"),JsonConvert.SerializeObject(results,Formatting.Indented));
                Debug.Log("Review inference suite finished.");
            }
        }
        private async void RunApiProbe(bool memoryOnly=false)
        {
            if(probing)return;probing=true;var results=new List<object>();
            try
            {
                var root=YuiPhysicalAI.Backend.YuiDesktopBackendPaths.ResolveBackendRoot(Application.dataPath,Application.persistentDataPath);
                string key=null;
                foreach(var line in File.ReadAllLines(Path.Combine(root,"backend/.env")))
                    if(line.TrimStart().StartsWith("OPENAI_API_KEY="))key=line.Substring(line.IndexOf('=')+1).Trim().Trim('"','\'');
                if(string.IsNullOrEmpty(key))throw new System.InvalidOperationException("Development key is unavailable.");
                var client=new YuiDirectOpenAiClient(key);
                foreach(var mode in memoryOnly?new[]{"memory","roles"}:new[]{"standard","work","memory"})
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew();
                    using var cancel=new CancellationTokenSource(60000);
                    var reply=await client.SendChatAsync(new ChatRequest{RequestId="review-direct-api-"+mode,UserId="tutorial-review",Mode=(mode=="memory"||mode=="roles")?"standard":mode,Context=new RequestContext{Extra=new Dictionary<string,object>{{YuiPhysicalAI.Avatar.YuiCharacterMemoryStore.ContextKey,mode=="memory"?ReviewMemoryContext():""}}},Secret=true,CharacterName=mode=="roles"?"B":"ユイ",CustomInstruction="親しい友達のように、フレンドリーな日本語で話してください。",ResponseInstruction="回答を「結論:」で始め、短く答えてください。",Message=mode=="roles"?"私の名前はA、あなたの名前はBです。職場でCさんの当たりがきつくて困っています。Cさんに『あの資料、まだできていないの？』と言われた時、私はどう返すといいでしょう。返答例を一つ、誰が返すのかも短く教えて。":mode=="memory"?"私の好きな食べ物と、その中に入れる好きなお肉を教えて。":"あなたの名前を教えて。それから4+4-3の答えを教えて。"},cancel.Token);
                    results.Add(new{mode,seconds=timer.Elapsed.TotalSeconds,ok=true,text=reply.Text});
                    Debug.Log("Review direct API "+mode+" "+timer.Elapsed.TotalSeconds.ToString("0.00")+"s "+reply.Text);
                }
            }
            catch(System.Exception error){results.Add(new{ok=false,error_type=error.GetType().Name});Debug.LogWarning("Review direct API failed: "+error.GetType().Name);}
            finally{probing=false;File.WriteAllText(Path.Combine(Application.persistentDataPath,"review-direct-api.json"),JsonConvert.SerializeObject(results,Formatting.Indented));Debug.Log("Review direct API suite finished.");}
        }
        private void Update()
        {
            var panel=FindObjectOfType<YuiChatPanel>();
            if(Input.GetKeyDown(KeyCode.F1))CloseViews();
            if(Input.GetKeyDown(KeyCode.F2)){CloseViews();YuiLocalModelMenu.Show(panel);}
            if(Input.GetKeyDown(KeyCode.F3)){CloseViews();FindObjectOfType<YuiSettingsOverlay>()?.Show();InvokeButton("SettingsTab2");}
            if(Input.GetKeyDown(KeyCode.F4)){CloseViews();FindObjectOfType<YuiSettingsOverlay>()?.Show();InvokeButton("SettingsTab0");}
            if(Input.GetKeyDown(KeyCode.F5))YuiUiLocalization.SetLanguage(YuiUiLocalization.Language=="ja"?"en":"ja");
            if(Input.GetKeyDown(KeyCode.F6)){CloseViews();YuiLocalModelMenu.Show(panel);foreach(var b in FindObjectsOfType<Button>())if(b.GetComponentInChildren<Text>()?.text==YuiSimpleDialog.L("選択中のモデル · 詳細設定","Selected model · Advanced")){b.onClick.Invoke();break;}}
            if(Input.GetKeyDown(KeyCode.F7)){CloseViews();FindObjectOfType<YuiHelpOverlay>()?.Show();InvokeButton("GuideTab");}
            if(Input.GetKeyDown(KeyCode.F11))RunApiProbe();
            if(Input.GetKeyDown(KeyCode.F12) && panel!=null){
                CloseViews();
                var field=typeof(YuiChatPanel).GetProperty("CharacterMemoryStore",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var store=(YuiPhysicalAI.Avatar.YuiCharacterMemoryStore)field.GetValue(panel);
                var id=(string)typeof(YuiChatPanel).GetMethod("ChatCharacterId",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(panel,null);
                if(store.Read(id).Count==0){store.Save(id,"検証用の記憶: 私は鶏肉カレーが好き",pinned:true);resetFixtureActive=true;Debug.Log("Review reset fixture created.");}
                else Debug.LogWarning("Existing review memories retained; reset fixture not created.");
                panel.OpenCharacterMemories();
            }
            if(Input.GetKeyDown(KeyCode.F13) && resetFixtureActive)InvokeButton("ClearMemory");
            if(Input.GetKeyDown(KeyCode.F14) && resetFixtureActive){
                InvokeButton("Delete");
                var store=(YuiPhysicalAI.Avatar.YuiCharacterMemoryStore)typeof(YuiChatPanel).GetProperty("CharacterMemoryStore",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(panel);
                var id=(string)typeof(YuiChatPanel).GetMethod("ChatCharacterId",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(panel,null);
                Debug.Log("Review settings reset remaining memories: "+store.Read(id).Count);resetFixtureActive=false;
            }
            if(Input.GetKeyDown(KeyCode.F15))RunProbe(panel,true);
            if(Input.GetKeyDown(KeyCode.F16))RunApiProbe(true);
            if(Input.GetKeyDown(KeyCode.F10))RunProbe(panel);
            if(Input.GetKeyDown(KeyCode.F8)){CloseViews();YuiTutorial.Show();}
            if(!Input.GetKeyDown(KeyCode.F9))return;
            var folder=Path.Combine(Application.persistentDataPath,"TutorialCaptures");Directory.CreateDirectory(folder);
            var path=Path.Combine(folder,"capture-"+System.DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".png");
            StartCoroutine(Capture(path));
            Debug.Log("Tutorial capture: "+path);
        }
    }
}
#endif
