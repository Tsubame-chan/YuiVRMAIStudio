using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Yui.AvatarBridge.Editor
{
    public static class YuiPortableMeshes
    {
        // UniVRM's ModelExporter reads morph deltas, but not the renderer's current weights.
        // Bake the visible customization into a private mesh; leave a normalized residual
        // target so activating a mapped expression still reaches its authored endpoint.
        public static void Prepare(GameObject clone, List<Object> owned)
        {
            foreach (var renderer in clone.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!renderer.enabled || renderer.sharedMesh == null) continue;
                var source = renderer.sharedMesh;
                var mesh = Object.Instantiate(source); owned.Add(mesh);
                mesh.name = source.name + "_Yui";
                var positions = mesh.vertices; var normals = mesh.normals;
                mesh.ClearBlendShapes();
                for (var i = 0; i < source.blendShapeCount; i++)
                {
                    var frames = source.GetBlendShapeFrameCount(i);
                    var end = frames > 0 ? source.GetBlendShapeFrameWeight(i, frames - 1) : 0;
                    var weight = renderer.GetBlendShapeWeight(i);
                    if (frames != 1 || !Finite(end) || end <= 0 || !Finite(weight) || weight < 0 || weight > end)
                        throw new InvalidOperationException($"{renderer.name} / {source.GetBlendShapeName(i)} は複数段階または範囲外の変形です。現在の共通ファイル書出しでは変形を保てません。");
                    var delta = new Vector3[source.vertexCount]; var deltaNormals = new Vector3[source.vertexCount]; var deltaTangents = new Vector3[source.vertexCount];
                    source.GetBlendShapeFrameVertices(i, 0, delta, deltaNormals, deltaTangents);
                    var ratio = weight / end;
                    for (var v = 0; v < positions.Length; v++)
                    {
                        positions[v] += delta[v] * ratio;
                        if (normals.Length == positions.Length) normals[v] += deltaNormals[v] * ratio;
                        delta[v] *= 1f - ratio; deltaNormals[v] *= 1f - ratio; deltaTangents[v] *= 1f - ratio;
                    }
                    mesh.AddBlendShapeFrame(source.GetBlendShapeName(i), 100f, delta, deltaNormals, deltaTangents);
                }
                mesh.vertices = positions;
                if (normals.Length == positions.Length) { for (var i = 0; i < normals.Length; i++) normals[i].Normalize(); mesh.normals = normals; }
                EnsureAttributes(mesh);
                mesh.RecalculateBounds(); renderer.sharedMesh = mesh;
                for (var i = 0; i < mesh.blendShapeCount; i++) renderer.SetBlendShapeWeight(i, 0);
            }
            foreach (var filter in clone.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                var mesh = Object.Instantiate(filter.sharedMesh); owned.Add(mesh);
                EnsureAttributes(mesh); filter.sharedMesh = mesh;
            }
        }
        private static void EnsureAttributes(Mesh mesh)
        {
            if (mesh.normals.Length != mesh.vertexCount) mesh.RecalculateNormals();
            // UniVRM 0.127.2's mesh writer requires UV0 even for untextured geometry.
            if (mesh.uv.Length != mesh.vertexCount) mesh.uv = new Vector2[mesh.vertexCount];
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
