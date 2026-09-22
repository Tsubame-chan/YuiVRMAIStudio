using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UniGLTF;
using UniVRM10;
using Object = UnityEngine.Object;

namespace Yui.AvatarBridge.Editor
{
    public static class YuiPortableAvatarExporter
    {
        public static YuiAvatarDiagnostics Export(GameObject source, string outputPath, string displayName, bool rightsAcknowledged)
        {
            if (source == null) throw new ArgumentException("Hierarchyでアバターを選んでください。");
            if (!rightsAcknowledged) throw new InvalidOperationException("アバターを外部アプリで使用する許可を確認してください。");
            if (Path.GetExtension(outputPath).ToLowerInvariant() != ".vrm") throw new ArgumentException("書出し先に.vrmファイルを選んでください。");
            GameObject clone = null;
            var objects = new List<Object>();
            var temporary = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // Source processing occurs on a detached copy, before SDK components are removed.
                clone = Object.Instantiate(source); clone.name = source.name;
                clone.transform.SetParent(null, false);
                clone.transform.position = Vector3.zero; clone.transform.rotation = Quaternion.identity;
                clone.SetActive(true);
                BakeSourceCopy(clone);
                var analysis = YuiAvatarBridgeAnalyzer.Analyze(clone, portable: true);
                ValidateStructure(clone, analysis);
                var errors = analysis.Diagnostics.issues.Where(i => i.severity == "error" && i.code != "descriptor.missing").ToArray();
                if (errors.Length > 0) throw new InvalidOperationException(string.Join("\n", errors.Select(i => i.message)));
                var blink = YuiPortableExpressions.Blink(clone, analysis.Descriptor);
                if (blink.Length == 0) YuiAvatarBridgeAnalyzer.AddIssue(analysis, "warning", "blink.missing", "まばたきに使用できる変形を確認できませんでした。目は自動では閉じません。");
                var diagnostics = analysis.Diagnostics;
                YuiPortableMeshes.Prepare(clone, objects);
                foreach (var animator in clone.GetComponentsInChildren<Animator>(true)) animator.runtimeAnimatorController = null;
                foreach (var component in clone.GetComponentsInChildren<Component>(true).Reverse())
                    if (component != null && !YuiAvatarBridgeExporter.IsAllowedRuntimeComponent(component)) Object.DestroyImmediate(component);

                var converted = new Dictionary<Material, Material>();
                foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    for (var i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) { mats[i] = YuiAvatarBasicMaterials.Convert(null); objects.Add(mats[i]); continue; }
                        if (!converted.TryGetValue(mats[i], out var basic))
                        { basic = YuiAvatarBasicMaterials.Convert(mats[i]); converted.Add(mats[i], basic); objects.Add(basic); }
                        mats[i] = basic;
                    }
                    renderer.sharedMaterials = mats;
                }

                var vrmObject = ScriptableObject.CreateInstance<VRM10Object>(); objects.Add(vrmObject);
                vrmObject.Meta.Name = string.IsNullOrWhiteSpace(displayName) ? source.name : displayName.Trim();
                vrmObject.Meta.Version = "1";
                vrmObject.Meta.Authors = new List<string> { "Original avatar creator (see source asset licence)" };
                vrmObject.Meta.CopyrightInformation = "Original avatar rights remain with its creators. Exported for the owner's personal use with Yui.";
                vrmObject.Meta.Redistribution = false;
                var expression = vrmObject.Expression;
                foreach (var mapping in diagnostics.visemes.Where(v => v.found))
                {
                    var renderer = ResolveRenderer(clone, mapping.rendererPath);
                    var index = renderer != null ? renderer.sharedMesh.GetBlendShapeIndex(mapping.blendShape) : -1;
                    if (index < 0) continue;
                    var clip = MakeExpression(mapping.rendererPath, index, objects);
                    switch (mapping.vowel) { case "aa": expression.Aa = clip; break; case "ih": expression.Ih = clip; break; case "ou": expression.Ou = clip; break; case "ee": expression.Ee = clip; break; case "oh": expression.Oh = clip; break; }
                }
                expression.Blink = MakeExpression(blink, objects);
                expression.Happy = FindExpression(clone, objects, "Fcl_ALL_Joy", "Joy", "Happy", "笑い");
                expression.Angry = FindExpression(clone, objects, "Fcl_ALL_Angry", "Angry", "怒り");
                expression.Sad = FindExpression(clone, objects, "Fcl_ALL_Sorrow", "Sorrow", "Sad", "悲しい");
                expression.Relaxed = FindExpression(clone, objects, "Fcl_ALL_Fun", "Fun", "Relaxed");
                expression.Surprised = FindExpression(clone, objects, "Fcl_ALL_Surprised", "Surprised");
                if (expression.Happy == null && expression.Angry == null && expression.Sad == null && expression.Relaxed == null && expression.Surprised == null)
                    YuiAvatarBridgeAnalyzer.AddIssue(analysis, "warning", "expression.missing", "対応する基本表情名を検出できませんでした。元の表情アニメーションは実行しません。");
                // A partial or single mouth can still open for every vowel.
                var fallback = expression.Aa ?? expression.Ih ?? expression.Ou ?? expression.Ee ?? expression.Oh;
                expression.Aa = expression.Aa ?? fallback; expression.Ih = expression.Ih ?? fallback;
                expression.Ou = expression.Ou ?? fallback; expression.Ee = expression.Ee ?? fallback; expression.Oh = expression.Oh ?? fallback;
                clone.SetActive(false);
                var instance = clone.AddComponent<Vrm10Instance>(); instance.Vrm = vrmObject; instance.UpdateType = Vrm10Instance.UpdateTypes.None;
                AddSprings(clone, instance, diagnostics.physBones);
                clone.SetActive(true);

                using (var arrays = new NativeArrayManager())
                {
                    var converter = new ModelExporter(); var model = converter.Export(arrays, clone);
                    model.ConvertCoordinate(VrmLib.Coordinates.Vrm1, ignoreVrm: false);
                    using var exporter = new Vrm10Exporter(new GltfExportSettings { UseSparseAccessorForMorphTarget = true, ExportOnlyBlendShapePosition = true });
                    exporter.Export(clone, model, converter, new VrmLib.ExportArgs { sparse = true }, vrmObject.Meta);
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                    File.WriteAllBytes(temporary, exporter.Storage.ToGlbBytes());
                }
                if (File.Exists(outputPath)) File.Replace(temporary, outputPath, null); else File.Move(temporary, outputPath);
                return diagnostics;
            }
            finally
            {
                if (clone != null) Object.DestroyImmediate(clone);
                foreach (var value in objects) if (value != null) Object.DestroyImmediate(value);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static void BakeSourceCopy(GameObject clone)
        {
            if (!YuiAvatarBridgeAnalyzer.RequiresSourceBake(clone)) return;
            var processor = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("nadena.dev.ndmf.AvatarProcessor")).FirstOrDefault(t => t != null);
            var platformType = processor?.Assembly.GetType("nadena.dev.ndmf.platform.INDMFPlatformProvider");
            var registry = processor?.Assembly.GetType("nadena.dev.ndmf.platform.PlatformRegistry");
            var platform = registry?.GetMethod("GetPrimaryPlatformForAvatar")?.Invoke(null, new object[] { clone });
            var method = platformType == null ? null : processor?.GetMethod("ProcessAvatar", BindingFlags.Public | BindingFlags.Static, null, new [] { typeof(GameObject), platformType }, null);
            if (method == null || platform == null) throw new InvalidOperationException("衣装の加工に必要なNDMFを利用できません。元プロジェクトのModular Avatarを更新して、もう一度書き出してください。");
            try {
                var context = method.Invoke(null, new object[] { clone, platform });
                if (!(YuiAvatarBridgeAnalyzer.GetMemberValue(context, "Successful") is bool succeeded) || !succeeded)
                    throw new InvalidOperationException("衣装の加工でエラーが報告されました。NDMFのエラーレポートを確認してください。元アバターは変更していません。");
            }
            catch (TargetInvocationException ex) { throw new InvalidOperationException("衣装の加工に失敗しました。元アバターは変更していません。\n" + ex.InnerException?.Message, ex.InnerException); }
            // NDMF 1.14 leaves these generated portable metadata components even on
            // a successful VRChat build. VRC descriptors/PhysBones remain our inputs.
            var generatedTypes = new [] { "nadena.dev.ndmf.runtime.components.NDMFAvatarRoot", "nadena.dev.ndmf.multiplatform.components.PortableDynamicBone", "nadena.dev.ndmf.multiplatform.components.PortableDynamicBoneCollider" };
            foreach (var component in clone.GetComponentsInChildren<Component>(true))
                if (component != null && generatedTypes.Contains(component.GetType().FullName)) Object.DestroyImmediate(component);
            if (YuiAvatarBridgeAnalyzer.RequiresSourceBake(clone)) throw new InvalidOperationException("衣装の加工が完了していません: " + string.Join(", ", clone.GetComponentsInChildren<Component>(true).Where(c => c != null && YuiAvatarBridgeAnalyzer.IsBuildTimeComponent(c.GetType().FullName)).Select(c => c.GetType().FullName).Distinct()));
        }

        private static void ValidateStructure(GameObject root, YuiAvatarAnalysis analysis)
        {
            var animator = root.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) throw new InvalidOperationException("Humanoid Animatorが付いているアバターのルートを選んでください。");
            var paths = new HashSet<string>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var path = AnimationUtility.CalculateTransformPath(t, root.transform);
                if (t.name.Contains("/") || !paths.Add(path)) throw new InvalidOperationException("階層に同名の兄弟または / を含む名前があり、表情とボーンを一意に参照できません: " + path);
            }
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!renderer.enabled || renderer.sharedMesh == null) continue;
                if (renderer.transform == root.transform) throw new InvalidOperationException("メッシュはアバタールートの子に配置してください。");
                if (renderer.bones.Any(b => b == null || !b.IsChildOf(root.transform))) throw new InvalidOperationException(renderer.name + " のボーンに参照切れ、またはアバター外への参照があります。");
                if (renderer.sharedMaterials.Length < renderer.sharedMesh.subMeshCount) throw new InvalidOperationException(renderer.name + " のMaterialスロットが不足しています。");
            }
            if (analysis.Diagnostics.visemes.Any(v => v.source == "ambiguous"))
                YuiAvatarBridgeAnalyzer.AddIssue(analysis, "warning", "viseme.ambiguous", "口の候補が複数のメッシュにあります。推定せず、VRC Avatar Descriptorの設定が確認できた口形のみ使用します。");
        }

        private static SkinnedMeshRenderer ResolveRenderer(GameObject root, string path) => (string.IsNullOrEmpty(path) ? root.transform : root.transform.Find(path))?.GetComponent<SkinnedMeshRenderer>();
        private static VRM10Expression MakeExpression(string path, int index, List<Object> objects) => MakeExpression(new [] { new MorphTargetBinding(path, index, 1f) }, objects);
        private static VRM10Expression MakeExpression(MorphTargetBinding[] bindings, List<Object> objects)
        {
            if (bindings.Length == 0) return null;
            var clip = ScriptableObject.CreateInstance<VRM10Expression>(); objects.Add(clip);
            clip.MorphTargetBindings = bindings; return clip;
        }
        private static VRM10Expression FindExpression(GameObject root, List<Object> objects, params string[] names) => MakeExpression(YuiPortableExpressions.KnownShape(root, names), objects);

        private static float SafeUnit(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp01(value);
        private static void AddSprings(GameObject root, Vrm10Instance instance, IEnumerable<YuiAvatarPhysBoneMapping> mappings)
        {
            var body = new HashSet<Transform>(); var animator = root.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman) for (var i = 0; i < (int)HumanBodyBones.LastBone; i++) body.Add(animator.GetBoneTransform((HumanBodyBones)i));
            var visited = new HashSet<Transform>(); var count = 0;
            foreach (var mapping in mappings.Take(128))
            {
                if (string.IsNullOrEmpty(mapping.rootPath)) continue;
                var boneRoot = root.transform.Find(mapping.rootPath); if (boneRoot == null) continue;
                var ignored = new HashSet<Transform>((mapping.ignorePaths ?? Array.Empty<string>()).Where(p => !string.IsNullOrEmpty(p)).Select(p => root.transform.Find(p)).Where(t => t != null));
                var pending = new Queue<Transform>(); pending.Enqueue(boneRoot);
                while (pending.Count > 0 && count < 256)
                {
                    var start = pending.Dequeue(); var chain = new Vrm10InstanceSpringBone.Spring(start.name); var bone = start;
                    while (bone != null && !ignored.Contains(bone) && !body.Contains(bone) && !visited.Contains(bone) && count < 256)
                    {
                        visited.Add(bone); count++;
                        var joint = bone.gameObject.AddComponent<VRM10SpringBoneJoint>();
                        joint.m_stiffnessForce = .5f + 3f * SafeUnit(mapping.pull);
                        joint.m_dragForce = .4f; joint.m_gravityPower = SafeUnit(Mathf.Abs(mapping.gravity)) * .1f;
                        joint.m_gravityDir = mapping.gravity < 0 ? Vector3.up : Vector3.down; joint.m_jointRadius = 0;
                        chain.Joints.Add(joint);
                        var children = Enumerable.Range(0, bone.childCount).Select(bone.GetChild).Where(t => !ignored.Contains(t) && !body.Contains(t)).ToArray();
                        for (var i = 1; i < children.Length; i++) pending.Enqueue(children[i]);
                        bone = children.FirstOrDefault();
                    }
                    if (chain.Joints.Count >= 2) instance.SpringBone.Springs.Add(chain);
                }
            }
        }
    }
}
