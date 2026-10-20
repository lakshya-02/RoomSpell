using UnityEngine;
using UnityEngine.UI;

namespace RoomSpell
{
    // Rounded gradients stay sharp in XR without textures or a blur pass.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoomSpellSurface : MaskableGraphic
    {
        public float Radius = 24;
        public Color Left = Color.white;
        public Color Right = Color.white;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0) return;
            var radius = Mathf.Clamp(Radius, 0, Mathf.Min(rect.width, rect.height) * .5f);
            Add(mesh, rect.center, rect);
            const int steps = 10;
            for (var corner = 0; corner < 4; corner++)
            {
                var center = new Vector2(corner == 0 || corner == 3 ? rect.xMax - radius : rect.xMin + radius,
                    corner < 2 ? rect.yMax - radius : rect.yMin + radius);
                for (var step = 0; step <= steps; step++)
                {
                    var angle = (corner * 90 + step * 90f / steps) * Mathf.Deg2Rad;
                    Add(mesh, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, rect);
                }
            }
            var count = 4 * (steps + 1);
            for (var i = 1; i <= count; i++) mesh.AddTriangle(0, i, i == count ? 1 : i + 1);
        }

        void Add(VertexHelper mesh, Vector2 point, Rect rect)
        {
            var vertex = UIVertex.simpleVert;
            vertex.position = point;
            vertex.color = Color.Lerp(Left, Right, Mathf.InverseLerp(rect.xMin, rect.xMax, point.x)) * color;
            mesh.AddVert(vertex);
        }
    }
}
