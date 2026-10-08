using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace YuiPhysicalAI.LocalAI
{
    // Retain verified files across cancellation; never activate a partial model directory.
    public static class YuiVoicePackInstaller
    {
        public static async Task InstallIrodoriAsync(string manifestJson,string persistent,
            IYuiLocalAiAssetHttpClient http,IProgress<YuiLocalAiAssetDownloadProgress> progress,CancellationToken token)
        {
            var manifest=JObject.Parse(manifestJson);
            var files=(JArray)manifest["files"];
            var total=files.Sum(f=>(long)f["bytes"]);
            var root=YuiIrodoriSpeech.Root(persistent);
            var stage=root+".download";var backup=root+".previous";
            Directory.CreateDirectory(stage);
            long completed=0;
            foreach(var file in files) {
                token.ThrowIfCancellationRequested();
                var relative=(string)file["path"];
                var path=SafePath(stage,relative);
                var size=(long)file["bytes"];var sha=(string)file["sha256"];
                var valid=await Task.Run(()=>Valid(path,size,sha,token),token);
                if(!valid) {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    var offset=completed;
                    var part=path+".partial";
                    var reporter=new InlineProgress(p=>progress?.Report(new YuiLocalAiAssetDownloadProgress("Irodori",offset+p.DownloadedBytes,total,(float)(offset+p.DownloadedBytes)/total,"download")));
                    try {
                        await http.DownloadFileAsync((string)manifest["base_url"]+string.Join("/",relative.Split('/').Select(Uri.EscapeDataString)),part,size,reporter,token);
                        if(!await Task.Run(()=>Valid(part,size,sha,token),token))throw new InvalidDataException("Irodori model verification failed: "+relative);
                        if(File.Exists(path))File.Delete(path);File.Move(part,path);
                    } finally {if(File.Exists(part))File.Delete(part);}
                }
                completed+=size;
                progress?.Report(new YuiLocalAiAssetDownloadProgress("Irodori",completed,total,(float)completed/total,"verify"));
            }
            token.ThrowIfCancellationRequested();
            File.WriteAllText(Path.Combine(stage,".verified"),(string)manifest["version"]);
            // Recover an interrupted swap before creating a fresh backup.
            if(!Directory.Exists(root)&&Directory.Exists(backup))Directory.Move(backup,root);
            if(Directory.Exists(backup))Directory.Delete(backup,true);
            if(Directory.Exists(root))Directory.Move(root,backup);
            try {Directory.Move(stage,root);} catch {if(Directory.Exists(backup))Directory.Move(backup,root);throw;}
            if(Directory.Exists(backup))Directory.Delete(backup,true);
        }
        public static string SafePath(string root,string relative)
        {
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains("\\"))throw new InvalidDataException("Invalid model path.");
            var prefix=Path.GetFullPath(root)+Path.DirectorySeparatorChar;
            var path=Path.GetFullPath(Path.Combine(root,relative));
            if(!path.StartsWith(prefix,StringComparison.Ordinal))throw new InvalidDataException("Invalid model path.");
            return path;
        }
        private static bool Valid(string path,long size,string hash,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(!File.Exists(path)||new FileInfo(path).Length!=size)return false;
            using var stream=File.OpenRead(path);using var sha=SHA256.Create();
            var buffer=new byte[1024*1024];int count;
            while((count=stream.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();sha.TransformBlock(buffer,0,count,null,0);}
            sha.TransformFinalBlock(Array.Empty<byte>(),0,0);
            return string.Equals(BitConverter.ToString(sha.Hash).Replace("-", ""),hash,StringComparison.OrdinalIgnoreCase);
        }
        private sealed class InlineProgress:IProgress<YuiLocalAiAssetDownloadProgress>
        {private readonly Action<YuiLocalAiAssetDownloadProgress> report;public InlineProgress(Action<YuiLocalAiAssetDownloadProgress> report){this.report=report;}public void Report(YuiLocalAiAssetDownloadProgress value)=>report(value);}
    }
}
