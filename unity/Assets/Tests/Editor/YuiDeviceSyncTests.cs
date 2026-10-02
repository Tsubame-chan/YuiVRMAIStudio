using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using YuiPhysicalAI.Core;
using YuiPhysicalAI.UI;

namespace YuiPhysicalAI.Tests
{
    public sealed class YuiDeviceSyncTests
    {
        private string root;
        [SetUp]public void Setup(){root=Path.Combine(Path.GetTempPath(),"yui-sync-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);}
        [TearDown]public void Cleanup(){Directory.Delete(root,true);}
        [Test]public void ImportCommitsAllFilesAndRetainsPreviousBytes()
        {
            var profile=Path.Combine(root,"CharacterProfiles","one.json");Directory.CreateDirectory(Path.GetDirectoryName(profile));File.WriteAllText(profile,"before 🌸");
            var history=Path.Combine(root,"ConversationHistory","one.jsonl");
            YuiSyncFileTransaction.Apply(root,new Dictionary<string,string>{{profile,"after"},{history,"history"}});
            Assert.AreEqual("after",File.ReadAllText(profile));Assert.AreEqual("history",File.ReadAllText(history));
            Assert.IsFalse(File.Exists(Path.Combine(root,"DeviceSync","import.pending.json")));
            Assert.AreEqual("before 🌸",File.ReadAllText(Directory.GetFiles(Path.Combine(root,"DeviceSync","Backups"),"*.bak",SearchOption.AllDirectories)[0]));
        }
        [Test]public void InterruptedImportRestoresOriginalFilesAndRemovesOnlyNewFiles()
        {
            var profile=Path.Combine(root,"CharacterProfiles","one.json");Directory.CreateDirectory(Path.GetDirectoryName(profile));File.WriteAllText(profile,"partially applied");
            var newFile=Path.Combine(root,"CharacterMemory","one.json");Directory.CreateDirectory(Path.GetDirectoryName(newFile));File.WriteAllText(newFile,"new");
            var backup=Path.Combine(root,"DeviceSync","Backups","one.bak");Directory.CreateDirectory(Path.GetDirectoryName(backup));File.WriteAllText(backup,"original");
            File.WriteAllText(Path.Combine(root,"DeviceSync","import.pending.json"),JsonConvert.SerializeObject(new[]{
                new {Path=profile,Backup=backup,Existed=true},new {Path=newFile,Backup=backup,Existed=false}}));
            YuiSyncFileTransaction.Recover(root);
            Assert.AreEqual("original",File.ReadAllText(profile));Assert.IsFalse(File.Exists(newFile));Assert.IsTrue(File.Exists(backup));
        }
        [Test]public void ForeignPathIsRejectedWithoutChangingIt()
        {
            var foreign=root+"-foreign";File.WriteAllText(foreign,"keep");
            try {Assert.Throws<InvalidDataException>(()=>YuiSyncFileTransaction.Apply(root,new Dictionary<string,string>{{foreign,"overwrite"}}));Assert.AreEqual("keep",File.ReadAllText(foreign));}
            finally{File.Delete(foreign);}
        }
        [Test]public void DiffKeepsStableIdsAndExplicitlyRecordsDeletionAgainstLastAgreement()
        {
            var previous=new JArray(new JObject{{"kind","memory"},{"id","one"},{"version",7},{"deleted",false},{"value",new JObject{{"content","previous"}}}},
                new JObject{{"kind","history"},{"id","two"},{"version",8},{"deleted",false},{"value",new JObject{{"text","keep"}}}});
            var current=new JObject{{"history:two",new JObject{{"text","keep"}}},{"memory:three",new JObject{{"content","new"}}}};
            var method=typeof(YuiChatPanel).GetMethod("SyncOperations",BindingFlags.Static|BindingFlags.NonPublic);
            var result=(JArray)method.Invoke(null,new object[]{current,previous});
            Assert.AreEqual(3,result.Count);
            foreach(var item in result){
                if((string)item["id"]=="one"){Assert.IsTrue((bool)item["deleted"]);Assert.AreEqual(7,(int)item["base_version"]);Assert.IsEmpty((JObject)item["value"]);}
                if((string)item["id"]=="two")Assert.IsFalse((bool)item["changed"]);
                if((string)item["id"]=="three")Assert.AreEqual(0,(int)item["base_version"]);
            }
        }
    }
}
