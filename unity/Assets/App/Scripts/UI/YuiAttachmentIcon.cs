using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YuiPhysicalAI.UI
{
    // A resolution-independent paperclip, using the same white line style as the toolbar.
    public sealed class YuiAttachmentIcon : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var points = new List<Vector2> { new Vector2(.24f, .70f), new Vector2(.24f, .34f) };
            Arc(points, new Vector2(.50f, .34f), .26f, 180, 360);
            points.Add(new Vector2(.76f, .70f));
            Arc(points, new Vector2(.56f, .70f), .20f, 0, 180);
            points.Add(new Vector2(.36f, .36f));
            Arc(points, new Vector2(.50f, .36f), .14f, 180, 360);
            points.Add(new Vector2(.64f, .70f));
            var rect = rectTransform.rect;
            var size = Mathf.Min(rect.width, rect.height) * .86f;
            for (var i = 0; i < points.Count; i++)
            {
                var p = points[i] - Vector2.one * .5f;
                points[i] = rect.center + new Vector2(p.x * .866f + p.y * .5f, -p.x * .5f + p.y * .866f) * size;
            }
            var width = size * .055f;
            for (var i = 1; i < points.Count; i++)
            {
                var direction = (points[i] - points[i - 1]).normalized;
                var normal = new Vector2(-direction.y, direction.x) * width * .5f;
                var start = mesh.currentVertCount;
                mesh.AddVert(points[i - 1] - normal, color, Vector2.zero);
                mesh.AddVert(points[i - 1] + normal, color, Vector2.zero);
                mesh.AddVert(points[i] + normal, color, Vector2.zero);
                mesh.AddVert(points[i] - normal, color, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2); mesh.AddTriangle(start, start + 2, start + 3);
            }
        }
        private static void Arc(List<Vector2> points, Vector2 center, float radius, float from, float to)
        {
            for (var i = 1; i <= 16; i++)
            {
                var angle = Mathf.Lerp(from, to, i / 16f) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }
    }
}
