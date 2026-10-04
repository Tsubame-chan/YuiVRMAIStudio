using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace YuiPhysicalAI.Core
{
    // A durable rollback journal keeps the local agreement and files together.
    // Recovery rolls an interrupted import back before any new export starts.
    public static class YuiSyncFileTransaction
    {
        private sealed class Before { public string Path, Backup; public bool Existed; }
        private static string Journal(string root) => Path.Combine(root,"DeviceSync","import.pending.json");
        private static string HistoryForgetMarker(string root) => Path.Combine(root,"DeviceSync","history-forget.pending");
        public static void Recover(string root)
        {
            var journal=Journal(root);
            if(File.Exists(journal))
            {
                var entries=JsonConvert.DeserializeObject<List<Before>>(File.ReadAllText(journal));
                if(entries==null)throw new InvalidDataException("同期の復元記録を読み込めません。元データを保持しています。");
                foreach(var entry in entries)
                {
                    ValidatePath(root,entry.Path);ValidatePath(root,entry.Backup);
                    if(entry.Existed)Write(entry.Path,File.ReadAllBytes(entry.Backup));
                    else if(File.Exists(entry.Path))File.Delete(entry.Path);
                }
                File.Delete(journal);
            }
            if(File.Exists(HistoryForgetMarker(root)))CompleteHistoryForget(root);
        }
        public static void BeginHistoryForget(string root)
        {
            if(File.Exists(Journal(root)))Recover(root);
            Write(HistoryForgetMarker(root),System.Text.Encoding.UTF8.GetBytes("pending"));
            PurgeGeneratedBackups(root);
        }
        public static void CompleteHistoryForget(string root)
        {
            if(File.Exists(Journal(root)))throw new InvalidOperationException("Sync recovery must finish before history backup cleanup.");
            PurgeGeneratedBackups(root);
            if(File.Exists(HistoryForgetMarker(root)))File.Delete(HistoryForgetMarker(root));
        }
        private static void PurgeGeneratedBackups(string root)
        {
            var backups=Path.Combine(root,"DeviceSync","Backups");
            if(!Directory.Exists(backups))return;
            foreach(var folder in Directory.GetDirectories(backups))
                if(Regex.IsMatch(Path.GetFileName(folder),@"^\d{8}T\d{6}-[a-f0-9]{32}$"))
                    Directory.Delete(folder,true);
        }
        public static void Apply(string root, IDictionary<string,string> files)
        {
            if(File.Exists(HistoryForgetMarker(root)))throw new InvalidOperationException("History deletion is in progress.");
            Recover(root);
            var backup=Path.Combine(root,"DeviceSync","Backups",DateTime.UtcNow.ToString("yyyyMMddTHHmmss")+"-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            var before=new List<Before>();
            foreach(var pair in files)
            {
                ValidatePath(root,pair.Key);
                var entry=new Before{Path=pair.Key,Existed=File.Exists(pair.Key),Backup=Path.Combine(backup,before.Count+".bak")};
                if(entry.Existed)Write(entry.Backup,File.ReadAllBytes(pair.Key));
                before.Add(entry);
            }
            Write(Journal(root),System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(before)));
            try {
                foreach(var pair in files)Write(pair.Key,System.Text.Encoding.UTF8.GetBytes(pair.Value));
                // Deleting the journal is the local commit point. Backups remain for recovery by the owner.
                File.Delete(Journal(root));
            }catch {Recover(root);throw;}
        }
        public static void Write(string file, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));var temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
            }finally{if(File.Exists(temp))File.Delete(temp);}
        }
        private static void ValidatePath(string root,string path)
        {
            var absolute=Path.GetFullPath(path);var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!absolute.StartsWith(prefix,StringComparison.Ordinal))throw new InvalidDataException("同期の保存先が不正です。元データを保持しています。");
            var relative=absolute.Substring(prefix.Length);
            if(!(relative.StartsWith("DeviceSync"+Path.DirectorySeparatorChar,StringComparison.Ordinal)
                     || relative.StartsWith("CharacterProfiles"+Path.DirectorySeparatorChar,StringComparison.Ordinal)
                     || relative.StartsWith("CharacterMemory"+Path.DirectorySeparatorChar,StringComparison.Ordinal)
                     || relative.StartsWith("ConversationHistory"+Path.DirectorySeparatorChar,StringComparison.Ordinal)))
                throw new InvalidDataException("同期の保存先が不正です。元データを保持しています。");
        }
    }
}
