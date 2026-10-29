using UnityEngine;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    // Geometry keeps a constant stroke width when the chart grows or its range changes.
    public sealed class MapRangeRing : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            if (radius < 1) return;
            const int segments = 128;
            const float thickness = 1.25f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                float b = (i + 1) * Mathf.PI * 2 / segments;
                Vector2 first = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 next = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                int index = vh.currentVertCount;
                vh.AddVert(rect.center + first * radius, color, Vector2.zero);
                vh.AddVert(rect.center + first * (radius - thickness), color, Vector2.zero);
                vh.AddVert(rect.center + next * (radius - thickness), color, Vector2.zero);
                vh.AddVert(rect.center + next * radius, color, Vector2.zero);
                vh.AddTriangle(index, index + 1, index + 2);
                vh.AddTriangle(index, index + 2, index + 3);
            }
        }
    }
}
