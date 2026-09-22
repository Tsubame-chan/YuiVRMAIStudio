using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yui.AvatarBridge.Editor
{
    public sealed class YuiAvatarBridgeWindow : EditorWindow
    {
        private GameObject avatarRoot;
        private YuiAvatarAnalysis analysis;
        private string displayName = "";
        private bool rightsAcknowledged, details;
        private Vector2 scroll;

        [MenuItem("Yui/Avatar Bridge/Export Avatar for Yui", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<YuiAvatarBridgeWindow>("Yuiへ書き出す");
            window.minSize = new Vector2(440, 440); window.UseSelection(); window.Show();
        }
        private void OnEnable() { if (avatarRoot == null) UseSelection(); }
        private void UseSelection()
        {
            var selected = Selection.activeGameObject;
            if (selected == null) return;
            // Prefer the closest humanoid root, even when the user selected its clothes.
            var animator = selected.GetComponentsInParent<Animator>(true).FirstOrDefault(a => a.isHuman);
            avatarRoot = animator != null ? animator.gameObject : selected;
            displayName = avatarRoot.name; Analyze();
        }
        private void Analyze() { analysis = YuiAvatarBridgeAnalyzer.Analyze(avatarRoot, portable: true); }
        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("いつものアバターをYuiへ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("表示中の姿を1つのファイルにします。Windows・Mac・iPhone・Androidで共通です。元のアバターは変更しません。", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            avatarRoot = (GameObject)EditorGUILayout.ObjectField("アバター", avatarRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) { displayName = avatarRoot != null ? avatarRoot.name : ""; Analyze(); }
            if (GUILayout.Button("Hierarchyの選択を使う")) UseSelection();
            displayName = EditorGUILayout.TextField("名前", displayName);
            EditorGUILayout.HelpBox("見た目の基本・口パク・まばたき・簡単な揺れを変換します。独自シェーダー、衣装メニュー、接触ギミックは再現しません。", MessageType.None);
            if (analysis != null)
            {
                foreach (var issue in analysis.Diagnostics.issues.Where(i => i.severity == "error")) EditorGUILayout.HelpBox(issue.message, MessageType.Error);
                var mouths = analysis.Diagnostics.visemes.Count(v => v.found);
                EditorGUILayout.LabelField("口パク", mouths > 0 ? "対応する口形を検出" : "未検出（書出し時に再確認）");
                details = EditorGUILayout.Foldout(details, "互換性の詳細");
                if (details) foreach (var issue in analysis.Diagnostics.issues.Where(i => i.severity != "error")) EditorGUILayout.HelpBox(issue.message, MessageType.None);
            }
            rightsAcknowledged = EditorGUILayout.ToggleLeft("VRChat外で変換・使用する許可を確認しました", rightsAcknowledged);
            using (new EditorGUI.DisabledScope(avatarRoot == null || !rightsAcknowledged || EditorApplication.isPlaying))
                if (GUILayout.Button("Yui用ファイルを書き出す", GUILayout.Height(40))) Export();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("書出したファイルを端末へコピーし、Yuiの「アバター追加」で開きます。", EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("スマホへの転送方法")) Application.OpenURL("https://github.com/Tsubame-chan/YuiVRMAIStudio/blob/main/docs/AVATAR_IMPORT.md");
            EditorGUILayout.EndScrollView();
        }
        private void Export()
        {
            var path = EditorUtility.SaveFilePanel("Yui用ファイルを書き出す", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), YuiAvatarBridgeAnalyzer.SafeName(displayName), "vrm");
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                EditorUtility.DisplayProgressBar("Yuiへ書き出す", "アバターのコピーを変換しています", .3f);
                var diagnostics = YuiPortableAvatarExporter.Export(avatarRoot, path, displayName, rightsAcknowledged);
                var missing = diagnostics.issues.Where(i => i.code == "viseme.none" || i.code == "viseme.ambiguous" || i.code == "blink.missing" || i.code == "constraint.removed").Select(i => i.message);
                EditorUtility.ClearProgressBar();
                EditorUtility.RevealInFinder(path);
                EditorUtility.DisplayDialog("書き出し完了", "このファイルをYuiの「アバター追加」で開いてください。\n\n" + string.Join("\n", missing), "OK");
            }
            catch (Exception ex) { Debug.LogException(ex); EditorUtility.ClearProgressBar(); EditorUtility.DisplayDialog("書き出せませんでした", ex.Message, "OK"); }
            finally { EditorUtility.ClearProgressBar(); }
        }
    }
}
