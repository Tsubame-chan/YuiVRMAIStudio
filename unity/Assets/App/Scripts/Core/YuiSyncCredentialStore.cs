using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace YuiPhysicalAI.Core
{
    public static class YuiSyncCredentialStore
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string Library = "__Internal";
#else
        private const string Library = "YuiCredentialStore";
#endif
        [DllImport(Library)] private static extern int YuiCredentialRead(string service, out IntPtr value);
        [DllImport(Library)] private static extern int YuiCredentialWrite(string service, string value);
        [DllImport(Library)] private static extern void YuiCredentialFree(IntPtr value);
        [DllImport("libc", SetLastError=true)] private static extern int chmod(string path, uint mode);
        [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
        [DllImport("crypt32", SetLastError=true, CharSet=CharSet.Unicode, ExactSpelling=true)] private static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
        [DllImport("crypt32", SetLastError=true, CharSet=CharSet.Unicode, ExactSpelling=true)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
        [DllImport("kernel32")] private static extern IntPtr LocalFree(IntPtr value);
        private static bool Windows => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
        public static string ServerKey(string server)
        { using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(server))).Replace("-", "").ToLowerInvariant(); }
        private static string FileFor(string server) => Path.Combine(Application.persistentDataPath,"DeviceSync",ServerKey(server),"credential.bin");
        private static string Service(string server) => Application.identifier+".device-sync."+ServerKey(server);
        public static string Read(string server)
        {
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            if(YuiCredentialRead(Service(server),out var pointer)!=0)throw new IOException("端末の登録情報を読み込めません。");
            try{return pointer==IntPtr.Zero?"":Marshal.PtrToStringAnsi(pointer);}finally{if(pointer!=IntPtr.Zero)YuiCredentialFree(pointer);}
#else
            var file=FileFor(server);if(!File.Exists(file))return "";
            var bytes=File.ReadAllBytes(file);return Encoding.UTF8.GetString(Windows?Transform(bytes,false):bytes);
#endif
        }
        public static void Write(string server,string token)
        {
#if (UNITY_IOS || UNITY_STANDALONE_OSX) && !UNITY_EDITOR
            if(YuiCredentialWrite(Service(server),token??"")!=0)throw new IOException("端末の登録情報を安全に保存できません。");
#else
            var file=FileFor(server);Directory.CreateDirectory(Path.GetDirectoryName(file));
            var temp=file+".tmp";var bytes=Encoding.UTF8.GetBytes(token??"");
            try {File.WriteAllBytes(temp,Windows?Transform(bytes,true):bytes);
                if(!Windows && chmod(temp,384)!=0)throw new IOException("登録情報のファイル権限を設定できません。");
                if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
            }finally{if(File.Exists(temp))File.Delete(temp);}
#endif
        }
        private static byte[] Transform(byte[] bytes,bool protect)
        {
            var input=new Blob{Length=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};
            try {
                Marshal.Copy(bytes,0,input.Data,bytes.Length);
                var success=protect?CryptProtectData(ref input,"Yui device registration",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out var output)
                    :CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
                if(!success)throw new IOException("端末の登録情報を保護できません。");
                try{var result=new byte[output.Length];Marshal.Copy(output.Data,result,0,result.Length);return result;}finally{LocalFree(output.Data);}
            }finally{Marshal.FreeHGlobal(input.Data);}
        }
    }
}
