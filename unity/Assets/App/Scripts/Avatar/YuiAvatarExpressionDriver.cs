using System;
using System.Collections.Generic;
using UnityEngine;
using UniVRM10;

namespace YuiPhysicalAI.Avatar
{
    // Route VRM expressions through its API; direct mesh writes would be overwritten
    // by UniVRM and can interfere with authored expression bindings.
    public sealed class YuiAvatarExpressionDriver : MonoBehaviour
    {
        private Vrm10Instance vrm;
        private readonly List<(SkinnedMeshRenderer Renderer, int Index, float Baseline)> blinkShapes = new List<(SkinnedMeshRenderer, int, float)>();
        private float nextBlink, blinkStarted = -1;
        private bool initialized;
        private static readonly ExpressionKey[] Emotions = {
            new ExpressionKey(ExpressionPreset.happy), new ExpressionKey(ExpressionPreset.relaxed),
            new ExpressionKey(ExpressionPreset.angry), new ExpressionKey(ExpressionPreset.sad), new ExpressionKey(ExpressionPreset.surprised) };
        private void Awake()
        {
            EnsureInitialized();
        }
        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            vrm = GetComponentInChildren<Vrm10Instance>(true);
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.sharedMesh == null) continue;
                var mesh = renderer.sharedMesh;
                var combined = -1;
                foreach (var candidate in new [] { "Blink", "vrc.Blink", "Fcl_EYE_Close", "eye_close", "まばたき" })
                {
                    for (var i = 0; i < mesh.blendShapeCount; i++)
                        if (string.Equals(mesh.GetBlendShapeName(i), candidate, StringComparison.OrdinalIgnoreCase)) { combined = i; break; }
                    if (combined >= 0) break;
                }
                if (combined >= 0) blinkShapes.Add((renderer, combined, renderer.GetBlendShapeWeight(combined)));
                else foreach (var candidate in new [] { "Blink_L", "Blink_R", "blinkLeft", "blinkRight" })
                {
                    var index = mesh.GetBlendShapeIndex(candidate);
                    if (index >= 0) blinkShapes.Add((renderer, index, renderer.GetBlendShapeWeight(index)));
                }
            }
        }
        private void OnEnable() { nextBlink = Time.time + UnityEngine.Random.Range(2.5f, 5f); blinkStarted = -1; }
        private void OnDisable() { SetBlink(0); }
        private void LateUpdate()
        {
            if (blinkStarted < 0 && Time.time >= nextBlink) blinkStarted = Time.time;
            if (blinkStarted < 0) return;
            var elapsed = Time.time - blinkStarted;
            SetBlink(Mathf.Clamp01(1f - Mathf.Abs(elapsed - .08f) / .08f));
            if (elapsed >= .16f) { blinkStarted = -1; nextBlink = Time.time + UnityEngine.Random.Range(2.5f, 5f); }
        }
        public void SetBlink(float weight)
        {
            EnsureInitialized();
            if (vrm != null && vrm.Runtime != null)
            {
                var combined = vrm.Vrm != null && vrm.Vrm.Expression.Blink != null;
                vrm.Runtime.Expression.SetWeight(new ExpressionKey(ExpressionPreset.blink), combined ? Mathf.Clamp01(weight) : 0);
                vrm.Runtime.Expression.SetWeight(new ExpressionKey(ExpressionPreset.blinkLeft), combined ? 0 : Mathf.Clamp01(weight));
                vrm.Runtime.Expression.SetWeight(new ExpressionKey(ExpressionPreset.blinkRight), combined ? 0 : Mathf.Clamp01(weight));
            }
            else foreach (var shape in blinkShapes) if (shape.Renderer != null && shape.Renderer.sharedMesh != null) shape.Renderer.SetBlendShapeWeight(shape.Index, Mathf.Lerp(shape.Baseline, 100f, Mathf.Clamp01(weight)));
        }
        public bool ApplyFace(string face)
        {
            EnsureInitialized();
            if (vrm == null || vrm.Runtime == null) return false;
            var selected = -1;
            switch ((face ?? "").ToLowerInvariant())
            {
                case "joy": case "happy": selected = 0; break;
                case "fun": case "relaxed": selected = 1; break;
                case "angry": selected = 2; break;
                case "sorrow": case "sad": selected = 3; break;
                case "surprised": selected = 4; break;
            }
            for (var i = 0; i < Emotions.Length; i++) vrm.Runtime.Expression.SetWeight(Emotions[i], i == selected ? .65f : 0f);
            return true;
        }
    }
}
