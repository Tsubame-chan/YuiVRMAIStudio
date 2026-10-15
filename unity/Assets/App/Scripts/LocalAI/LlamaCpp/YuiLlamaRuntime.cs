using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiLlamaRuntime : IYuiLocalAiRuntime
    {
#if UNITY_IOS && !UNITY_EDITOR
        const string Library = "__Internal";
#else
        const string Library = "YuiLlama";
#endif
        [DllImport(Library)] static extern IntPtr YuiLlama_Generate(string id,[MarshalAs(UnmanagedType.LPUTF8Str)] string path,IntPtr roles,IntPtr contents,int count,int context,int output,float temperature,int topK,float topP,int timeout,out IntPtr error);
        [DllImport(Library)] static extern void YuiLlama_Free(IntPtr p);
        [DllImport(Library)] static extern void YuiLlama_Cancel(string id);
        [DllImport(Library)] static extern void YuiLlama_CancelAll();
        static readonly object LifecycleSync=new object();
        static CancellationTokenSource lifecycleCancellation=new CancellationTokenSource();
        static bool suspended;
        public static void CancelActive() {
            // Requests capture this token before joining the execution queue.
            // Cancelling it also stops requests that have no native state yet.
            lock(LifecycleSync) {
                lifecycleCancellation.Cancel();
                if(!suspended)lifecycleCancellation=new CancellationTokenSource();
            }
            if(!PlatformSupported)return;
            try{YuiLlama_CancelAll();}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
        }
        public static void SetSuspended(bool value) {
            lock(LifecycleSync) {
                suspended=value;
                if(!value && lifecycleCancellation.IsCancellationRequested)lifecycleCancellation=new CancellationTokenSource();
            }
            if(value)CancelActive();
        }
        static CancellationToken LifecycleToken() {lock(LifecycleSync)return lifecycleCancellation.Token;}
        [DllImport(Library)] static extern void YuiLlama_Forget(string id);
        private readonly YuiLocalAiModelRegistry registry;
        public YuiLlamaRuntime(YuiLocalAiModelRegistry registry) { this.registry = registry; }
        public const string Version = "llama.cpp-b11617";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport(Library)] static extern int YuiLlama_ABIVersion();
#endif
        public static bool PlatformSupported {
            get {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                try{return YuiLlama_ABIVersion()==1;}catch(DllNotFoundException){return false;}catch(EntryPointNotFoundException){return false;}catch(BadImageFormatException){return false;}
#else
                return Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
#endif
            }
        }
        public string RuntimeName => "llama.cpp";
        public YuiLocalAiStatus GetStatus() => new YuiLocalAiStatus {Available=PlatformSupported,RuntimeName=RuntimeName,Detail="GGUF text inference through the platform native runtime",Capabilities=PlatformSupported ? new[]{YuiLocalAiCapability.Chat} : Array.Empty<YuiLocalAiCapability>()};
        // Keep a selected GGUF request on its route even if this build lacks its
        // native engine; report that error instead of answering with another model.
        public bool Supports(YuiLocalAiCapability capability) => capability == YuiLocalAiCapability.Chat && registry.EnabledFor(capability).Any(p=>p.Format=="gguf");
        public Task WarmAsync(YuiLocalAiCapability c,CancellationToken t) => Task.CompletedTask;
        public Task ReleaseAsync(YuiLocalAiCapability c,CancellationToken t) => Task.CompletedTask; // Native RAII frees models after every request.
        public Task<YuiLocalAiTranscriptionResponse> TranscribeAsync(YuiLocalAiAudioRequest r,CancellationToken t) => Task.FromResult(new YuiLocalAiTranscriptionResponse{Success=false,ErrorCode="capability_unsupported"});
        public Task<YuiLocalAiSpeechResponse> SynthesizeSpeechAsync(YuiLocalAiSpeechRequest r,CancellationToken t) => Task.FromResult(new YuiLocalAiSpeechResponse{Success=false,ErrorCode="capability_unsupported"});
        public Task<YuiLocalAiVisionResponse> AnalyzeImageAsync(YuiLocalAiVisionRequest r,CancellationToken t) => Task.FromResult(new YuiLocalAiVisionResponse{Success=false,ErrorCode="capability_unsupported"});
        public async Task<YuiLocalAiChatResponse> ChatAsync(YuiLocalAiChatRequest request,CancellationToken token)
        {
            var pack=registry.EnabledFor(YuiLocalAiCapability.Chat).FirstOrDefault(p=>p.Id==YuiLocalModelSelection.SelectedId && p.Format=="gguf");
            if(pack==null) return new YuiLocalAiChatResponse {Success=false,ErrorCode="selected_model_unavailable",ErrorMessage="The selected GGUF is unavailable. Select the standard model in Settings."};
            var options=YuiLocalModelOptions.Load(pack,request.Mode=="work");
            var prepared=YuiLocalAiPromptBuilder.PrepareChatRequest(request,true);
            prepared.SystemInstruction += "\n"+options.CustomPrompt;
            return await GenerateAsync(pack,YuiLocalAiModelPathResolver.PersistentModelPath(pack),prepared,options,token);
        }
        public static async Task<YuiLocalAiChatResponse> GenerateAsync(YuiLocalAiModelPack pack,string path,YuiLocalAiChatRequest prepared,YuiLocalModelOptions options,CancellationToken token)
        {
            if(!PlatformSupported) return new YuiLocalAiChatResponse {Success=false,ErrorCode="platform_unsupported",ErrorMessage="GGUF execution is not available in this build."};
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(token,LifecycleToken());
            token=linked.Token;
            var messages=new List<YuiLocalAiChatMessage> {new YuiLocalAiChatMessage {Role="system",Content=prepared.SystemInstruction??"Answer briefly."}};
            messages.AddRange((prepared.History??new List<YuiLocalAiChatMessage>()).Where(m=>m.Role=="user"||m.Role=="assistant").TakeLast(120));
            messages.Add(new YuiLocalAiChatMessage {Role="user",Content=prepared.Input??prepared.Message??"Say hello."});
            return await Task.Run(()=> {
                using var gate=YuiLocalModelExecutionGate.Enter(token);
                var id=Guid.NewGuid().ToString("N");
                using var roles=new Utf8Array(messages.Select(m=>m.Role));
                using var contents=new Utf8Array(messages.Select(m=>m.Content??""));
                IntPtr output=IntPtr.Zero,error=IntPtr.Zero;bool entered=false;
                try {
                    YuiGoogleAiEdgeBridge.ReleaseCachedEngine();
                    entered=true;using var cancel=token.Register(()=>YuiLlama_Cancel(id));
                    token.ThrowIfCancellationRequested();
                    output=YuiLlama_Generate(id,path,roles.Pointer,contents.Pointer,messages.Count,options.ContextTokens,options.OutputTokens,options.Temperature,options.TopK,options.TopP,options.TimeoutSeconds,out error);
                    token.ThrowIfCancellationRequested();
                    if(output==IntPtr.Zero) {
                        var failure=Read(error);
                        return new YuiLocalAiChatResponse {Success=false,ErrorCode=failure.StartsWith("Repeated output detected.",StringComparison.Ordinal)?"repetitive_response":"gguf_generation_failed",ErrorMessage=failure};
                    }
                    var text=StripReasoning(Read(output));
                    return new YuiLocalAiChatResponse {Success=!string.IsNullOrWhiteSpace(text),Text=text,ModelId=pack.ModelId,ErrorCode=string.IsNullOrWhiteSpace(text)?"empty_response":null,ErrorMessage=string.IsNullOrWhiteSpace(text)?"The model returned no final reply. Try another model or increase the output limit.":null};
                } catch(OperationCanceledException){throw;}
                catch(Exception ex){return new YuiLocalAiChatResponse {Success=false,ErrorCode="gguf_runtime_unavailable",ErrorMessage=ex is DllNotFoundException ? "The GGUF engine is missing from this build." : ex.Message};}
                finally { if(output!=IntPtr.Zero)YuiLlama_Free(output);if(error!=IntPtr.Zero)YuiLlama_Free(error);if(entered){try{YuiLlama_Forget(id);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}} }
            },token);
        }
        public static string StripReasoning(string text) => YuiLocalAiResponseNormalizer.ExtractFinalAnswer(Regex.Replace(text??"",@"^(?:\s*<think>.*?(?:</think>|$))+","",RegexOptions.Singleline|RegexOptions.IgnoreCase).Trim());
        static string Read(IntPtr p) { if(p==IntPtr.Zero)return "";int n=0;while(Marshal.ReadByte(p,n)!=0)n++;var bytes=new byte[n];Marshal.Copy(p,bytes,0,n);return Encoding.UTF8.GetString(bytes); }
        sealed class Utf8Array : IDisposable {
            public IntPtr Pointer; readonly IntPtr[] strings;
            public Utf8Array(IEnumerable<string> source) {
                var values=source.ToArray();strings=new IntPtr[values.Length];
                try {
                    Pointer=Marshal.AllocHGlobal(IntPtr.Size*values.Length);
                    for(int i=0;i<values.Length;i++){var bytes=Encoding.UTF8.GetBytes(values[i]+"\0");strings[i]=Marshal.AllocHGlobal(bytes.Length);Marshal.Copy(bytes,0,strings[i],bytes.Length);Marshal.WriteIntPtr(Pointer,i*IntPtr.Size,strings[i]);}
                } catch {Dispose();throw;}
            }
            public void Dispose(){for(int i=0;i<strings.Length;i++){Marshal.FreeHGlobal(strings[i]);strings[i]=IntPtr.Zero;}Marshal.FreeHGlobal(Pointer);Pointer=IntPtr.Zero;}
        }
    }
}
