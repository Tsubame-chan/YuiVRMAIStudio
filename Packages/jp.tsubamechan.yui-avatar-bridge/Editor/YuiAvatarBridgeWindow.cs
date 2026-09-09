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
        private string displayName = string.Empty;
        private string outputPath = string.Empty;
        private bool buildWindows = true;
        private bool buildMacOS = true;
        private bool buildAndroid = false;
        private bool buildIOS = false;
        private bool rightsAcknowledged;
        private Vector2 scroll;

        [MenuItem("Yui/Avatar Bridge/Export Avatar for Yui", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<YuiAvatarBridgeWindow>("Yui Avatar Bridge");
            window.minSize = new Vector2(520f, 600f);
            window.UseSelection();
            window.Show();
        }

        private void OnEnable()
        {
            if (avatarRoot == null) UseSelection();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject != null)
            {
                UseSelection();
                Repaint();
            }
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Yui Avatar Bridge", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("VCCプロジェクト内の選択アバターを複製・安全化し、対応するWindows/macOS/Android/iOS用payloadを1つのZIPへ保存します。元PrefabやSceneは変更しません。", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            avatarRoot = (GameObject)EditorGUILayout.ObjectField("Avatar root", avatarRoot, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck()) Analyze();
            if (GUILayout.Button("Use current Hierarchy selection")) UseSelection();

            if (analysis != null)
            {
                DrawSummary();
                DrawIssues();
                DrawMappings();
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("書き出したZIPをYuiの「アバター追加」で開いてください。スマホではFiles／Downloadsへ転送してから選択します。VRChatへのログインやアップロードは不要です。", MessageType.Info);
            if (GUILayout.Button("スマホへの転送・導入ガイド"))
                Application.OpenURL("https://github.com/Tsubame-chan/YuiVRMAIStudio/blob/main/docs/AVATAR_IMPORT.md");
            displayName = EditorGUILayout.TextField("Display name", displayName);
            EditorGUILayout.BeginHorizontal();
            outputPath = EditorGUILayout.TextField("Output", outputPath);
            if (GUILayout.Button("Choose...", GUILayout.Width(90f))) ChooseOutput();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("使用する端末（スマホ用は必要な場合だけ選択）", EditorStyles.boldLabel);
            buildWindows = DrawTargetToggle("Include Windows payload", buildWindows, BuildTarget.StandaloneWindows64);
            buildMacOS = DrawTargetToggle("Include macOS payload", buildMacOS, BuildTarget.StandaloneOSX);
            buildAndroid = DrawTargetToggle("Include Android payload", buildAndroid, BuildTarget.Android);
            buildIOS = DrawTargetToggle("Include iOS payload", buildIOS, BuildTarget.iOS);

            EditorGUILayout.Space();
            rightsAcknowledged = EditorGUILayout.ToggleLeft(
                "私は、このアバターをVRChat外で変換・使用する権利または許可を持っています。",
                rightsAcknowledged);

            var canExport = analysis != null && !analysis.HasErrors && rightsAcknowledged
                && (buildWindows || buildMacOS || buildAndroid || buildIOS) && !string.IsNullOrWhiteSpace(outputPath);
            EditorGUI.BeginDisabledGroup(!canExport);
            if (GUILayout.Button("Export for Yui", GUILayout.Height(42f))) Export();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSummary()
        {
            var diagnostics = analysis.Diagnostics;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Compatibility", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("VRC Avatar Descriptor", diagnostics.hasVrcAvatarDescriptor ? "Found" : "Missing");
            EditorGUILayout.LabelField("Humanoid Animator", diagnostics.hasHumanoidAnimator ? "Valid" : "Invalid");
            EditorGUILayout.LabelField("Meshes / Materials / BlendShapes", $"{diagnostics.skinnedMeshCount} / {diagnostics.materialCount} / {diagnostics.blendShapeCount}");
            EditorGUILayout.LabelField("Shaders", $"{diagnostics.materials.Count(material => material.shaderFound)} / {diagnostics.materials.Length}");
            EditorGUILayout.LabelField("Visemes", $"{diagnostics.visemes.Count(item => item.found)} / 5");
            var facial = diagnostics.expressionClips.Count(clip => clip.category == "facial_expression" || clip.category == "facial_option");
            EditorGUILayout.LabelField("Facial / all animation clips", $"{facial} / {diagnostics.expressionClips.Length}");
            EditorGUILayout.LabelField("PhysBone chains", diagnostics.physBones.Length.ToString());
        }

        private void DrawIssues()
        {
            foreach (var issue in analysis.Diagnostics.issues)
            {
                var type = issue.severity == "error" ? MessageType.Error : issue.severity == "warning" ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox(issue.message, type);
            }
        }

        private void DrawMappings()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Lip sync mapping", EditorStyles.boldLabel);
            foreach (var viseme in analysis.Diagnostics.visemes)
            {
                EditorGUILayout.LabelField(viseme.vowel, viseme.found ? $"{viseme.rendererPath} : {viseme.blendShape}" : "Not found");
            }

            if (analysis.Diagnostics.expressionClips.Length > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Expression/animation candidates", EditorStyles.boldLabel);
                foreach (var clip in analysis.Diagnostics.expressionClips.Take(30))
                {
                    EditorGUILayout.LabelField(clip.emotion, clip.name);
                }
                if (analysis.Diagnostics.expressionClips.Length > 30) EditorGUILayout.LabelField($"...and {analysis.Diagnostics.expressionClips.Length - 30} more");
            }
        }

        private void UseSelection()
        {
            avatarRoot = Selection.activeGameObject;
            Analyze();
        }

        private void Analyze()
        {
            analysis = YuiAvatarBridgeAnalyzer.Analyze(avatarRoot);
            if (avatarRoot != null && string.IsNullOrWhiteSpace(displayName)) displayName = avatarRoot.name;
            if (avatarRoot != null && string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), YuiAvatarBridgeAnalyzer.SafeName(avatarRoot.name) + "_AvatarPackage.zip");
            }
        }

        private void ChooseOutput()
        {
            var directory = string.IsNullOrWhiteSpace(outputPath) ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) : Path.GetDirectoryName(outputPath);
            var filename = string.IsNullOrWhiteSpace(displayName) ? "avatar" : YuiAvatarBridgeAnalyzer.SafeName(displayName);
            var selected = EditorUtility.SaveFilePanel("Export Avatar for Yui", directory, filename + "_AvatarPackage", "zip");
            if (!string.IsNullOrWhiteSpace(selected)) outputPath = selected;
        }

        private void Export()
        {
            try
            {
                var manifest = YuiAvatarBridgeExporter.Export(analysis, new YuiAvatarExportOptions
                {
                    OutputPath = outputPath,
                    DisplayName = displayName,
                    BuildWindows = buildWindows,
                    BuildMacOS = buildMacOS,
                    BuildAndroid = buildAndroid,
                    BuildIOS = buildIOS,
                    RightsAcknowledged = rightsAcknowledged,
                });
                EditorUtility.RevealInFinder(outputPath);
                EditorUtility.DisplayDialog("Yui Avatar Bridge", $"書き出し完了\n{manifest.displayName}\n{outputPath}\n\nこのZIPを端末へ転送し、Yuiのアバター追加から開いてください。", "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Yui Avatar Bridge", $"Export failed\n{ex.Message}", "OK");
            }
        }

        private static bool DrawTargetToggle(string label, bool selected, BuildTarget target)
        {
            var available = YuiAvatarBridgeExporter.IsBuildTargetAvailable(target);
            if (!available) selected = false;
            EditorGUI.BeginDisabledGroup(!available);
            selected = EditorGUILayout.ToggleLeft(available ? label : label + " (build support not installed)", selected);
            EditorGUI.EndDisabledGroup();
            return selected;
        }
    }
}
