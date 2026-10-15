using UnityEngine;
namespace YuiPhysicalAI.LocalAI
{
    public sealed class YuiLocalModelLifecycle : MonoBehaviour
    {
        static YuiLocalModelLifecycle instance;
        public static void Ensure() { if(!Application.isPlaying || instance!=null)return;var owner=new GameObject("YuiLocalModelLifecycle");DontDestroyOnLoad(owner);instance=owner.AddComponent<YuiLocalModelLifecycle>(); }
        void OnEnable(){Application.lowMemory+=YuiLlamaRuntime.CancelActive;}
        void OnDisable(){Application.lowMemory-=YuiLlamaRuntime.CancelActive;}
        void OnApplicationPause(bool pause){YuiLlamaRuntime.SetSuspended(pause);}
        void OnApplicationQuit(){YuiLlamaRuntime.SetSuspended(true);}
    }
}
