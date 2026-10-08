using System;
using System.IO;
using System.Collections;
using UnityEngine.TestTools;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using YuiPhysicalAI.LocalAI;
namespace YuiPhysicalAI.Tests.Editor
{
    public sealed class YuiVoicePackInstallerTests
    {
        private class Http:IYuiLocalAiAssetHttpClient
        {
            public int Calls;public bool Corrupt;
            public Task<string> GetStringAsync(string u,CancellationToken t)=>throw new NotSupportedException();
            public Task DownloadFileAsync(string u,string p,long size,IProgress<YuiLocalAiAssetDownloadProgress> progress,CancellationToken t)
            {Calls++;File.WriteAllText(p,Corrupt?"bad":"model");return Task.CompletedTask;}
        }
        [UnityTest] public IEnumerator VerifiedFilesResumeAndCorruptionDoesNotReplaceWorkingPack()
        {
            var root=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try {
                using var sha=SHA256.Create();var hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("model"))).Replace("-", "").ToLowerInvariant();
                var manifest=new JObject{["version"]="test",["base_url"]="https://example.com/",["files"]=new JArray(new JObject{["path"]="model.bin",["bytes"]=5,["sha256"]=hash})}.ToString();
                var installed=YuiIrodoriSpeech.Root(root);Directory.CreateDirectory(installed);File.WriteAllText(Path.Combine(installed,"old"),"keep");
                var http=new Http{Corrupt=true};
                var failed=YuiVoicePackInstaller.InstallIrodoriAsync(manifest,root,http,null,CancellationToken.None);
                while(!failed.IsCompleted)yield return null;
                Assert.IsTrue(failed.IsFaulted);Assert.IsInstanceOf<InvalidDataException>(failed.Exception.GetBaseException());
                Assert.IsTrue(File.Exists(Path.Combine(installed,"old")));Assert.IsFalse(YuiIrodoriSpeech.IsInstalled(root));
                http.Corrupt=false;Directory.CreateDirectory(installed+".download");File.WriteAllText(Path.Combine(installed+".download","model.bin"),"model");
                var resumed=YuiVoicePackInstaller.InstallIrodoriAsync(manifest,root,http,null,CancellationToken.None);
                while(!resumed.IsCompleted)yield return null;
                resumed.GetAwaiter().GetResult();
                Assert.AreEqual(1,http.Calls);Assert.IsTrue(YuiIrodoriSpeech.IsInstalled(root));Assert.IsFalse(File.Exists(Path.Combine(installed,"old")));
                Assert.Throws<InvalidDataException>(()=>YuiVoicePackInstaller.SafePath(root,"../outside"));
            }finally{Directory.Delete(root,true);}
        }
    }
}
