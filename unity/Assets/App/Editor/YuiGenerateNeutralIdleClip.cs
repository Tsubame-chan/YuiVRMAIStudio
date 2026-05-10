using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace YuiPhysicalAI.Editor
{
    public static class YuiGenerateNeutralIdleClip
    {
        private const string ClipOutputPath = "Assets/App/Resources/YuiNeutralIdle.anim";
        private const string ControllerPath = "Assets/App/Resources/YuiCustomVrmIdle.controller";

        [MenuItem("Yui/Generate/Neutral Idle Clip", false, 504)]
        public static void Generate()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ClipOutputPath));

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipOutputPath);
            if (clip == null)
            {
                clip = new AnimationClip
                {
                    name = "YuiNeutralIdle"
                };
                AssetDatabase.CreateAsset(clip, ClipOutputPath);
            }

            ClearCurves(clip);
            ConfigureClipSettings(clip);
            ApplyNeutralMuscles(clip);
            EditorUtility.SetDirty(clip);

            var controller = EnsureController();
            if (controller != null)
            {
                var state = EnsureSingleIdleState(controller);
                state.motion = clip;
                state.speed = 1f;
                state.iKOnFeet = true;
                EditorUtility.SetDirty(controller);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Yui] Neutral Custom VRM idle generated: {ClipOutputPath}");
        }

        private static void ClearCurves(AnimationClip clip)
        {
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        private static void ConfigureClipSettings(AnimationClip clip)
        {
            clip.frameRate = 30f;
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            settings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void ApplyNeutralMuscles(AnimationClip clip)
        {
            SetMuscle(clip, "Left Shoulder Down-Up", -0.15f);
            SetMuscle(clip, "Left Shoulder Front-Back", 0.05f);
            SetMuscle(clip, "Left Arm Down-Up", -0.55f);
            SetMuscle(clip, "Left Arm Front-Back", 0.05f);
            SetMuscle(clip, "Left Arm Twist In-Out", 0.10f);
            SetMuscle(clip, "Left Forearm Stretch", 0.35f);
            SetMuscle(clip, "Left Forearm Twist In-Out", -0.05f);
            SetMuscle(clip, "Left Hand Down-Up", 0f);
            SetMuscle(clip, "Left Hand In-Out", 0.05f);

            SetMuscle(clip, "Right Shoulder Down-Up", -0.15f);
            SetMuscle(clip, "Right Shoulder Front-Back", 0.05f);
            SetMuscle(clip, "Right Arm Down-Up", -0.55f);
            SetMuscle(clip, "Right Arm Front-Back", 0.05f);
            SetMuscle(clip, "Right Arm Twist In-Out", -0.10f);
            SetMuscle(clip, "Right Forearm Stretch", 0.35f);
            SetMuscle(clip, "Right Forearm Twist In-Out", 0.05f);
            SetMuscle(clip, "Right Hand Down-Up", 0f);
            SetMuscle(clip, "Right Hand In-Out", -0.05f);

            SetMuscle(clip, "Spine Front-Back", 0.02f);
            SetMuscle(clip, "Chest Front-Back", 0.02f);
            SetMuscle(clip, "Neck Nod Down-Up", 0f);
            SetMuscle(clip, "Head Nod Down-Up", 0f);
        }

        private static AnimatorController EnsureController()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            return controller != null
                ? controller
                : AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        private static AnimatorState EnsureSingleIdleState(AnimatorController controller)
        {
            while (controller.layers.Length > 1)
            {
                controller.RemoveLayer(controller.layers.Length - 1);
            }

            var layer = controller.layers[0];
            layer.name = "Base Layer";
            var stateMachine = layer.stateMachine;

            foreach (var childMachine in stateMachine.stateMachines.ToArray())
            {
                stateMachine.RemoveStateMachine(childMachine.stateMachine);
            }

            AnimatorState idle = null;
            foreach (var child in stateMachine.states)
            {
                if (idle == null && child.state.name == "Yui Custom VRM Idle")
                {
                    idle = child.state;
                    continue;
                }

                stateMachine.RemoveState(child.state);
            }

            if (idle == null)
            {
                idle = stateMachine.AddState("Yui Custom VRM Idle");
            }

            stateMachine.defaultState = idle;
            return idle;
        }

        private static void SetMuscle(AnimationClip clip, string muscleName, float value)
        {
            var binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(Animator),
                propertyName = muscleName
            };
            var curve = AnimationCurve.Constant(0f, 1f / 30f, value);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }
}
