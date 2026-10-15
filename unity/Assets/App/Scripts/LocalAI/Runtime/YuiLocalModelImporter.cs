using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YuiPhysicalAI.LocalAI
{
    // Files are owned copies; a successful probe precedes atomic catalog publication.
    public sealed class YuiLocalModelImporter
    {
        public const long MaxBytes = 32L * 1024 * 1024 * 1024;
        public const long ReserveBytes = 512L * 1024 * 1024;
        public static bool Busy { get; private set; }
        static readonly SemaphoreSlim Mutation = new SemaphoreSlim(1,1);
        readonly string root, models, catalog, staging, journal;
        public YuiLocalModelImporter(string persistentRoot) {
            root=Path.Combine(persistentRoot,"YuiLocalAI");models=Path.Combine(root,"Models");catalog=Path.Combine(root,"additional-models.json");staging=Path.Combine(root,"ImportStaging");journal=Path.Combine(root,"pending-import.json");
        }
        public static string DetectFormat(string path) {
            using var file=File.OpenRead(path);var bytes=new byte[8];int n=file.Read(bytes,0,8);
            var ext=Path.GetExtension(path).ToLowerInvariant();
            if(ext==".gguf" && n==8 && Encoding.ASCII.GetString(bytes,0,4)=="GGUF" && BitConverter.ToUInt32(bytes,4)==3) {
                if(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(path),@"-\d{5}-of-\d{5}\.gguf$")) throw new InvalidDataException("Split GGUF files are not supported. Choose a single-file chat model.");
                return "gguf";
            }
            if(ext==".litertlm" && n==8 && Encoding.ASCII.GetString(bytes)=="LITERTLM")return "litert-lm";
            throw new InvalidDataException("Choose a valid GGUF v3 or LiteRT-LM model. Renaming another format does not convert it.");
        }
        public static long AvailableBytes(string path) {
#if UNITY_IOS && !UNITY_EDITOR
            var bytes=YuiIOSModelFreeBytes(path);
            if(bytes<0)throw new IOException("Could not determine free storage for this model.");
            return bytes;
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var files=new UnityEngine.AndroidJavaClass("jp.tsubamechan.yuivrm.localai.YuiAndroidFilePicker");
            return files.CallStatic<long>("availableBytes",path);
#else
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))).AvailableFreeSpace;
#endif
        }
#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern long YuiIOSModelFreeBytes(string path);
#endif
        List<YuiLocalAiModelPack> Read() {
            if(!File.Exists(catalog))return new List<YuiLocalAiModelPack>();
            // A malformed catalog must be repaired deliberately, never silently overwritten.
            var manifest=JsonConvert.DeserializeObject<YuiLocalAiModelPackManifest>(File.ReadAllText(catalog));
            if(manifest?.Packs==null || manifest.Packs.Any(p=>p==null))throw new InvalidDataException("The model catalog is damaged. Existing files were kept.");
            return manifest.Packs;
        }
        void Save(List<YuiLocalAiModelPack> packs) {
            Directory.CreateDirectory(root);var temp=catalog+".tmp";
            using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
                var bytes=Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new YuiLocalAiModelPackManifest {Packs=packs},Formatting.Indented));file.Write(bytes,0,bytes.Length);file.Flush(true);
            }
            try { if(File.Exists(catalog))File.Replace(temp,catalog,catalog+".backup");else File.Move(temp,catalog); }
            finally { if(File.Exists(temp))File.Delete(temp); }
        }
        public void Recover() {
            if(Busy || !Mutation.Wait(0))return;
            try {
                var entries=Read();
                if(File.Exists(journal)) {
                    var name=File.ReadAllText(journal);
                    if(System.Text.RegularExpressions.Regex.IsMatch(name,@"^imported_[a-f0-9]{64}\.(gguf|litertlm)$") && !entries.Any(p=>p.RuntimeModelRef==name)) {
                        var abandoned=Path.Combine(models,name);if(File.Exists(abandoned))File.Delete(abandoned);
                        var abandonedCache=Path.Combine(root,"RuntimeCache",name);if(Directory.Exists(abandonedCache))Directory.Delete(abandonedCache,true);
                    }
                    File.Delete(journal);
                }
                if(Directory.Exists(staging)) foreach(var path in Directory.GetFiles(staging,"*.stage"))File.Delete(path);
            } finally { Mutation.Release(); }
        }
        public async Task<YuiLocalAiModelPack> ImportAsync(string source,bool ownedTemporary,string name,bool thinking,
            IEnumerable<YuiLocalAiModelPack> builtins,Func<YuiLocalAiModelPack,string,CancellationToken,Task<bool>> probe,
            IProgress<float> progress,CancellationToken token,Func<string,long> freeSpace=null,Action<string,string> moveFile=null) {
            if(!await Mutation.WaitAsync(0,token))throw new InvalidOperationException("Another model operation is running.");
            Busy=true;string stage=null,final=null;bool published=false,created=false;YuiLocalAiModelPack candidate=null;
            try {
                token.ThrowIfCancellationRequested();var format=DetectFormat(source);var length=new FileInfo(source).Length;
                if(length<32 || length>MaxBytes)throw new InvalidDataException("The model file is empty, truncated or exceeds 32 GiB.");
                var entries=Read();Directory.CreateDirectory(staging);Directory.CreateDirectory(models);
                var space=(freeSpace??AvailableBytes)(root);
                if(space < (ownedTemporary?0:length)+(format=="litert-lm"?length:0)+ReserveBytes)throw new IOException("Not enough free storage for the model and temporary runtime data.");
                stage=Path.Combine(staging,Guid.NewGuid().ToString("N")+".stage");
                var digest=await Task.Run(()=> {
                    bool moved=false;
                    if(ownedTemporary) {
                        try {(moveFile??File.Move)(source,stage);moved=true;}
                        catch(IOException) {if(File.Exists(stage))throw;}
                    }
                    // Android's picker cache and persistent files can be on
                    // different volumes. Fall back to a streamed owned copy.
                    if(!moved && space<length+(format=="litert-lm"?length:0)+ReserveBytes)throw new IOException("Not enough free storage to copy this model between volumes.");
                    using var sha=SHA256.Create();
                    using var input=File.OpenRead(moved?stage:source);
                    using var output=moved?null:new FileStream(stage,FileMode.CreateNew,FileAccess.Write,FileShare.None);
                    var buffer=new byte[1024*1024];int n;long copied=0;
                    while((n=input.Read(buffer,0,buffer.Length))>0) {token.ThrowIfCancellationRequested();copied+=n;if(copied>length)throw new IOException("The selected file changed during import.");output?.Write(buffer,0,n);sha.TransformBlock(buffer,0,n,buffer,0);progress?.Report((float)((double)copied/length));}
                    if(copied!=length)throw new IOException("The selected file was truncated during import.");
                    sha.TransformFinalBlock(Array.Empty<byte>(),0,0);output?.Flush(true);
                    return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
                },token);
                token.ThrowIfCancellationRequested();
                var existing=entries.Concat(builtins??Array.Empty<YuiLocalAiModelPack>()).ToList();
                if(existing.Any(p=>string.Equals(p.Sha256,digest,StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("This model is already registered. Select the existing copy.");
                var id="imported_"+digest;
                var pack=new YuiLocalAiModelPack {
                    Id=id,DisplayName=string.IsNullOrWhiteSpace(name)?Path.GetFileNameWithoutExtension(source):name.Trim(),Format=format,
                    Provider=format=="gguf"?"llama.cpp":"google-litert-lm",ModelId=id,RuntimeModelRef=id+(format=="gguf"?".gguf":".litertlm"),SourceKind="local-file",
                    Sha256=digest,FileBytes=length,Platforms=format=="gguf"?new[]{"ios","macos","windows"}:new[]{"ios","macos","windows","android"},
                    EnabledByDefault=true,DownloadRequired=false,Capabilities=new[]{YuiLocalAiCapability.Chat},SupportsThinking=thinking && format=="litert-lm",
                    ThinkingTokenBudget=128,WorkThinkingTokenBudget=512,TalkOutputTokenBudget=512,WorkOutputTokenBudget=1536,
                    DiskBudgetMb=(int)Math.Ceiling(length/1000000d),Priority=50
                };
                if(!YuiLocalModelCatalog.TryValidate(pack,existing,out var reason))throw new InvalidDataException(reason);
                candidate=pack;
                File.WriteAllText(journal,pack.RuntimeModelRef);
                if(probe==null || !await probe(pack,stage,token))throw new InvalidDataException("This model did not produce a final reply on this device. Existing models were kept.");
                token.ThrowIfCancellationRequested();pack.VerifiedRuntime=(format=="gguf"?YuiLlamaRuntime.Version:"litert-lm-0.17")+"/"+YuiLocalAiModelRegistry.CurrentPlatformKey();
                final=Path.Combine(models,pack.RuntimeModelRef);
                if(File.Exists(final))throw new IOException("An unregistered model file already occupies the destination. Recover pending imports first.");
                File.WriteAllText(journal,pack.RuntimeModelRef);File.Move(stage,final);created=true;
                entries.Add(pack);Save(entries);published=true;File.Delete(journal);return pack;
            } finally {
                try {
                    if(stage!=null && File.Exists(stage))File.Delete(stage);
                    if(ownedTemporary && File.Exists(source))File.Delete(source);
                    if(!published && created && File.Exists(final))File.Delete(final);
                    if(candidate!=null) {
                        // Probe compilation uses a temporary model path. It must
                        // not leave duplicate caches when the final path is used.
                        var cache=Path.Combine(root,"RuntimeCache",candidate.RuntimeModelRef);if(Directory.Exists(cache))Directory.Delete(cache,true);
                        if(!published && File.Exists(journal))File.Delete(journal);
                    }
                } finally { Busy=false;Mutation.Release(); }
            }
        }
        public void Delete(string id) {
            if(!Mutation.Wait(0))throw new InvalidOperationException("Another model operation is running.");
            try {
                var entries=Read();var pack=entries.SingleOrDefault(p=>p.Id==id && p.SourceKind=="local-file");
                if(pack==null || !YuiLocalModelCatalog.TryValidate(pack,Array.Empty<YuiLocalAiModelPack>(),out _))throw new InvalidOperationException("Only imported models can be removed.");
                if(id==YuiLocalModelSelection.SelectedId || YuiLocalModelExecutionGate.Busy)throw new InvalidOperationException("Select another model and stop the current request before removing this model.");
                // Journal also recovers deletion interrupted after the atomic catalog update.
                File.WriteAllText(journal,pack.RuntimeModelRef);entries.Remove(pack);Save(entries);
                var path=Path.Combine(models,pack.RuntimeModelRef);if(File.Exists(path))File.Delete(path);
                var cache=Path.Combine(root,"RuntimeCache",pack.RuntimeModelRef);if(Directory.Exists(cache))Directory.Delete(cache,true);
                File.Delete(journal);
            } finally { Mutation.Release(); }
        }
    }
}
