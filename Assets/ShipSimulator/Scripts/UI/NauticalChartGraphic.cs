using UnityEngine;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class NauticalChartGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            Vector2 Map(float x, float y) => new Vector2(bounds.xMin + x * bounds.width, bounds.yMin + y * bounds.height);
            Color grid = new Color(0.27f, 0.58f, 0.60f, 0.065f);
            Color ink = new Color(0.34f, 0.68f, 0.68f, 0.22f);
            Color brass = new Color(0.84f, 0.70f, 0.43f, 0.85f);
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                Line(mesh, Map(t, 0), Map(t, 1), 1f, grid);
                Line(mesh, Map(0, t), Map(1, t), 1f, grid);
            }
            for (int bank = 0; bank < 8; bank++)
            {
                Vector2 previous = Vector2.zero;
                for (int i = 0; i <= 100; i++)
                {
                    float y = i / 100f;
                    float x = 0.46f + Mathf.Sin(y * 7f + 0.5f) * 0.12f +
                        (bank < 4 ? -1 : 1) * (0.11f + (bank % 4) * 0.027f);
                    Vector2 point = Map(x, y);
                    if (i > 0) Line(mesh, previous, point, bank % 4 == 0 ? 2f : 1f, ink);
                    previous = point;
                }
            }
            for (int i = 0; i < 65; i += 2)
            {
                float y = 0.10f + i / 80f;
                float y2 = y + 0.0125f;
                Line(mesh, Map(0.46f + Mathf.Sin(y * 7f + 0.5f) * 0.12f, y),
                    Map(0.46f + Mathf.Sin(y2 * 7f + 0.5f) * 0.12f, y2), 2f, brass);
            }
            Vector2 center = Map(0.48f, 0.43f);
            float radius = bounds.width * 0.24f;
            for (int ring = 1; ring <= 3; ring++)
                for (int i = 0; i < 120; i++)
                    Line(mesh, center + Direction(i * 3) * radius * ring / 3f,
                        center + Direction((i + 1) * 3) * radius * ring / 3f, 1f, ink);
            for (int i = 0; i < 72; i++)
                Line(mesh, center + Direction(i * 5) * radius,
                    center + Direction(i * 5) * (radius + (i % 6 == 0 ? 12 : 5)), 1.5f, brass);
            Line(mesh, center - Vector2.up * (radius + 22), center + Vector2.up * (radius + 22), 1f, ink);
            Line(mesh, center - Vector2.right * (radius + 22), center + Vector2.right * (radius + 22), 1f, ink);
            Vector2[] hull = { new Vector2(0, 50), new Vector2(14, 24), new Vector2(14, -43),
                new Vector2(-14, -43), new Vector2(-14, 24), new Vector2(0, 50) };
            for (int i = 1; i < hull.Length; i++) Line(mesh, center + hull[i - 1], center + hull[i], 2.5f, brass);
            Line(mesh, center + new Vector2(-10, -22), center + new Vector2(10, -22), 4f, brass);
        }

        private static Vector2 Direction(float angle) => new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), Mathf.Cos(angle * Mathf.Deg2Rad));

        private static void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 delta = (to - from).normalized;
            Vector2 normal = new Vector2(-delta.y, delta.x) * width * 0.5f;
            int index = mesh.currentVertCount;
            mesh.AddVert(from - normal, tint, Vector2.zero);
            mesh.AddVert(from + normal, tint, Vector2.zero);
            mesh.AddVert(to + normal, tint, Vector2.zero);
            mesh.AddVert(to - normal, tint, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
