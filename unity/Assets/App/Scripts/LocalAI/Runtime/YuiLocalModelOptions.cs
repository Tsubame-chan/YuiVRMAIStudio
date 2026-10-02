using Newtonsoft.Json;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiLocalModelOptions
    {
        public int ContextTokens = 8192, OutputTokens = 512, ThinkingTokens = 128, TopK = 30;
        public int TimeoutSeconds = 120;
        public string CustomPrompt = "";
        public float Temperature = .65f, TopP = .85f;
        private static string Key(string id,bool work) => "yui.local-model.options."+id+(work?".work":".talk");
        public static YuiLocalModelOptions Defaults(YuiLocalAiModelPack pack,bool work) => new YuiLocalModelOptions {
            OutputTokens=work?pack.WorkOutputTokenBudget:pack.TalkOutputTokenBudget,
            ThinkingTokens=work?pack.WorkThinkingTokenBudget:pack.ThinkingTokenBudget,
            Temperature=work?.45f:.65f
        };
        public static YuiLocalModelOptions Load(YuiLocalAiModelPack pack,bool work)
        {
            var value=Defaults(pack,work);
            try { value=JsonConvert.DeserializeObject<YuiLocalModelOptions>(PlayerPrefs.GetString(Key(pack.Id,work),""))??value; }
            catch(JsonException) { }
            value.Clamp();return value;
        }
        public void Clamp()
        {
            ContextTokens=Mathf.Clamp(ContextTokens,4096,8192);
            OutputTokens=Mathf.Clamp(OutputTokens,256,Mathf.Min(4096,ContextTokens/2));
            ThinkingTokens=Mathf.Clamp(ThinkingTokens,1,Mathf.Max(1,Mathf.Min(2048,OutputTokens-256)));
            TimeoutSeconds=Mathf.Clamp(TimeoutSeconds,30,600);
            CustomPrompt=CustomPrompt??"";
            if(CustomPrompt.Length>2000)CustomPrompt=CustomPrompt.Substring(0,2000);
            Temperature=float.IsNaN(Temperature)||float.IsInfinity(Temperature)?.65f:Mathf.Clamp(Temperature,0,1.5f);
            TopK=Mathf.Clamp(TopK,1,100);
            TopP=float.IsNaN(TopP)||float.IsInfinity(TopP)?.85f:Mathf.Clamp(TopP,.1f,1);
        }
        public void Save(YuiLocalAiModelPack pack,bool work) { Clamp();PlayerPrefs.SetString(Key(pack.Id,work),JsonConvert.SerializeObject(this));PlayerPrefs.Save(); }
        public static void Reset(YuiLocalAiModelPack pack,bool work) { PlayerPrefs.DeleteKey(Key(pack.Id,work));PlayerPrefs.Save(); }
    }
}
