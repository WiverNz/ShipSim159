using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    /// <summary>
    /// Filled, vertex-coloured navigable-channel ribbon for the river radar. The
    /// ribbon is rebuilt each frame from the fairway edges projected into the
    /// head-up radar space, so the channel shape, safe water and danger zones read
    /// at a glance as a continuous chart rather than a grid of tiles.
    /// </summary>
    public sealed class RadarChannel : MaskableGraphic
    {
        public struct Section
        {
            public Vector2 Left;
            public Vector2 Right;
            public Vector2 Center;
            public Color32 Color;
            public Color32 LeftColor, RightColor;
        }

        private readonly List<Section> sections = new List<Section>();

        public void SetSections(List<Section> source)
        {
            sections.Clear();
            if (source != null) sections.AddRange(source);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (sections.Count < 2) return;

            for (int i = 0; i < sections.Count; i++)
            {
                Section s = sections[i];
                vh.AddVert(s.Left, s.LeftColor.a == 0 ? s.Color : s.LeftColor, new Vector2(0f, 0f));
                vh.AddVert(s.Center, s.Color, new Vector2(.5f, 0f));
                vh.AddVert(s.Right, s.RightColor.a == 0 ? s.Color : s.RightColor, new Vector2(1f, 0f));
            }

            for (int i = 0; i < sections.Count - 1; i++)
            for (int strip = 0; strip < 2; strip++)
            {
                int a = i * 3 + strip, b = a + 3;
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }
    }
}
