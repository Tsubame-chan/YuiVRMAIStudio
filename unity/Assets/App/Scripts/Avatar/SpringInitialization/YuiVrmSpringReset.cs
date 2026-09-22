using UniVRM10;

namespace YuiPhysicalAI.Avatar
{
    public static class YuiVrmSpringReset
    {
        public static void ResetParticlePositions(Vrm10Instance vrm)
        {
            // UniVRM's shared-job reset initializes tails in world space, while
            // UpdateFastSpringBoneJob reads center-relative space when Center exists.
            // Rebuild once, then seed all three Verlet buffers from the placed pose.
            // Only this avatar is changed. No private reflection or vendor patch.
            if (!(vrm.Runtime.SpringBone is Vrm10FastSpringboneRuntime)) return;
            var combiner = UniVRM10.FastSpringBones.FastSpringBoneService.Instance.BufferCombiner;
            combiner.ReconstructIfDirty(default).Complete();
            var buffer = combiner.Combined;
            if (buffer == null) return;
            var current = buffer.CurrentTails; var previous = buffer.PrevTails; var next = buffer.NextTails;
            foreach (var spring in buffer.Springs)
            {
                var offset = spring.transformIndexOffset;
                var center = spring.centerTransformIndex >= 0 ? buffer.TransformAccessArray[spring.centerTransformIndex + offset] : null;
                for (var i = spring.logicSpan.startIndex; i < spring.logicSpan.startIndex + spring.logicSpan.count; i++)
                {
                    var logic = buffer.Logics[i];
                    var head = buffer.TransformAccessArray[logic.headTransformIndex + offset];
                    if (!head.IsChildOf(vrm.transform)) continue;
                    var tail = buffer.TransformAccessArray[(logic.tailTransformIndex >= 0 ? logic.tailTransformIndex : logic.headTransformIndex) + offset];
                    var position = center != null ? center.InverseTransformPoint(tail.position) : tail.position;
                    current[i] = previous[i] = next[i] = position;
                }
            }
        }

    }
}
