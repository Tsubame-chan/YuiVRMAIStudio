using UnityEngine;

namespace YuiPhysicalAI.UI
{
    public static class YuiAvatarFraming
    {
        // The face is the conversational focus. Keep it above the console and
        // leave headroom consistently, independent of avatar/world-space scale.
        public const float FaceViewportY = .75f;
        public static Quaternion CameraRotation => Quaternion.Euler(8f, 0f, 0f);

        public static Vector3 CameraPosition(Vector3 face, float bodyHeight, float verticalFov)
        {
            var distance = Mathf.Max(.1f, bodyHeight) * 1.9f;
            var halfHeight = distance * Mathf.Tan(Mathf.Clamp(verticalFov, 10f, 80f) * Mathf.Deg2Rad * .5f);
            return face - CameraRotation * new Vector3(0f, (FaceViewportY - .5f) * 2f * halfHeight, distance);
        }

        public static float RequiredHeadroomShift(Vector3 cameraLocalPoint, float verticalFov, float safeTop)
        {
            if (cameraLocalPoint.z <= 0) return 0;
            var top = (2f * safeTop - 1f) * cameraLocalPoint.z * Mathf.Tan(verticalFov * Mathf.Deg2Rad * .5f);
            return Mathf.Max(0, cameraLocalPoint.y - top);
        }

        // Solve only when establishing the default view, never during a drag.
        public static void FitVisibleHeadroom(Camera camera, Transform root, float safeTop)
        {
            var shift = 0f;
            foreach (var point in VisibleWorldPoints(root))
                shift = Mathf.Max(shift, RequiredHeadroomShift(camera.transform.InverseTransformPoint(point), camera.fieldOfView, safeTop));
            camera.transform.position += camera.transform.up * shift;
        }

        public static float RequiredOrbitDistance(Transform root, Vector3 pivot, Quaternion rotation,
            float verticalFov, float aspect, float safeBottom, float safeTop)
        {
            var inverse = Quaternion.Inverse(rotation);
            var tangent = Mathf.Tan(verticalFov * Mathf.Deg2Rad * .5f);
            var top = Mathf.Max(.1f, 2f * safeTop - 1f) * tangent;
            var bottom = Mathf.Max(.1f, 1f - 2f * safeBottom) * tangent;
            var side = Mathf.Max(.1f, aspect) * tangent * .92f;
            var distance = .1f;
            foreach (var point in VisibleWorldPoints(root))
            {
                var local = inverse * (point - pivot);
                distance = Mathf.Max(distance, Mathf.Max(local.y / top - local.z,
                    Mathf.Max(-local.y / bottom - local.z, Mathf.Abs(local.x) / side - local.z)));
            }
            return distance;
        }

        // Exact geometry avoids oversized skin bounds and includes separate hats.
        private static System.Collections.Generic.IEnumerable<Vector3> VisibleWorldPoints(Transform root)
        {
            var baked = new Mesh();
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled) continue;
                    Mesh mesh = null;
                    if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                    {
                        // Compensate skin transform scale before applying localToWorldMatrix.
                        baked.Clear(); skin.BakeMesh(baked, true); mesh = baked;
                    }
                    else if (renderer is MeshRenderer)
                        mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                    if (mesh != null && mesh.isReadable)
                    {
                        var matrix = renderer.localToWorldMatrix;
                        foreach (var vertex in mesh.vertices)
                            yield return matrix.MultiplyPoint3x4(vertex);
                    }
                    else if (renderer is MeshRenderer)
                    {
                        var bounds = renderer.bounds;
                        for (var i = 0; i < 8; i++)
                            yield return bounds.center + Vector3.Scale(bounds.extents,
                                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    }
                }
            }
            finally
            {
                if (Application.isPlaying) Object.Destroy(baked); else Object.DestroyImmediate(baked);
            }
        }
    }
}
