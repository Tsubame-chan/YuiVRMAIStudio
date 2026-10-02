using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public static class YuiTutorial
    {
        // Existing completion is preserved; Help can reopen the redesigned guide.
        public const string CompletedKey="yui.tutorial.completed.v1";
        private static YuiTutorialView current;
        public static void ShowIfNeeded() { if(PlayerPrefs.GetInt(CompletedKey,0)==0)Show(); }
        public static void Show(int page=0)
        {
            if(current!=null)current.Close();
            current=YuiTutorialView.Create(Mathf.Clamp(page,0,3));
        }
        internal static void Finish()
        { PlayerPrefs.SetInt(CompletedKey,1);PlayerPrefs.Save();if(current!=null)current.Close();current=null; }
    }
}
