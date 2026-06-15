using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    /// <summary>
    /// Procedurally generated icon glyphs used across the HUD. Drawn once and cached.
    /// </summary>
    public enum HudIcon
    {
        Speed,
        Compass,
        Depth,
        Current,
        Cargo,
        Wind,
        Rain,
        Fog,
        Camera,
        Time,
        Rudder,
        Warning
    }

    /// <summary>
    /// Central HUD design system. Provides a single consistent palette, spacing
    /// constants, and procedurally generated rounded-rectangle / soft sprites so
    /// panels and buttons get soft corners, borders, shadows and glow without any
    /// imported art. Everything is generated once and shared, so the cost is paid
    /// a single time at first use.
    /// </summary>
    public static class HudTheme
    {
        // ---- Palette ------------------------------------------------------
        public static readonly Color PanelFill = new Color(0.047f, 0.078f, 0.110f, 0.82f);
        public static readonly Color PanelFillSoft = new Color(0.075f, 0.110f, 0.150f, 0.55f);
        public static readonly Color PanelBorder = new Color(0.42f, 0.64f, 0.76f, 0.34f);
        public static readonly Color PanelShadow = new Color(0f, 0.008f, 0.02f, 0.50f);

        public static readonly Color TextPrimary = new Color(0.94f, 0.97f, 0.99f);
        public static readonly Color TextSecondary = new Color(0.60f, 0.71f, 0.79f);

        public static readonly Color Accent = new Color(0.26f, 0.78f, 0.95f);
        public static readonly Color AccentSoft = new Color(0.52f, 0.87f, 1f);
        public static readonly Color Warning = new Color(1f, 0.71f, 0.20f);
        public static readonly Color Danger = new Color(0.97f, 0.33f, 0.27f);
        public static readonly Color Safe = new Color(0.38f, 0.85f, 0.53f);

        public static readonly Color ButtonIdle = new Color(0.105f, 0.165f, 0.225f, 0.92f);
        public static readonly Color ButtonActive = new Color(0.16f, 0.63f, 0.78f, 1f);
        public static readonly Color ButtonText = new Color(0.72f, 0.82f, 0.88f);

        // ---- Spacing ------------------------------------------------------
        public const float Margin = 22f;
        public const float Pad = 14f;
        public const int PanelRadius = 16;
        public const int ButtonRadius = 10;

        private const float PixelsPerUnit = 100f;

        private static readonly Dictionary<int, Sprite> RoundedCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> SoftCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> OutlineCache = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> IconCache = new Dictionary<int, Sprite>();
        private static Sprite triangleSprite;
        private static Sprite discSprite;
        private static Sprite ringSprite;

        // ---- Sprite factories --------------------------------------------

        /// <summary>Solid rounded rectangle, exported as a 9-slice sprite.</summary>
        public static Sprite Rounded(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 64);
            if (RoundedCache.TryGetValue(radius, out Sprite cached)) return cached;

            int size = radius * 2 + 6;
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundBox(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                float a = Mathf.Clamp01(0.5f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

            Sprite sprite = MakeSprite(pixels, size, radius);
            RoundedCache[radius] = sprite;
            return sprite;
        }

        /// <summary>Hollow rounded rectangle border ring, 9-slice.</summary>
        public static Sprite Outline(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 64);
            if (OutlineCache.TryGetValue(radius, out Sprite cached)) return cached;

            int size = radius * 2 + 6;
            float half = size * 0.5f;
            const float thickness = 1.6f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundBox(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                // |d| small near the boundary -> draw a thin ring.
                float a = Mathf.Clamp01(thickness - Mathf.Abs(d + thickness * 0.5f));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

            Sprite sprite = MakeSprite(pixels, size, radius);
            OutlineCache[radius] = sprite;
            return sprite;
        }

        /// <summary>
        /// Soft feathered rounded blob used for drop shadows, glows and the radar
        /// depth heat-map so cells blend instead of reading as hard squares.
        /// </summary>
        public static Sprite Soft(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 48);
            if (SoftCache.TryGetValue(radius, out Sprite cached)) return cached;

            int feather = Mathf.Max(3, radius);
            int size = (radius + feather) * 2 + 6;
            float half = size * 0.5f;
            float boxHalf = half - feather;
            float cornerRadius = Mathf.Min(radius, boxHalf - 1f);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundBox(x + 0.5f - half, y + 0.5f - half, boxHalf, boxHalf, cornerRadius);
                float a = 1f - SmoothStep(-feather, 0f, d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }

            int border = Mathf.RoundToInt(feather + cornerRadius);
            Sprite sprite = MakeSprite(pixels, size, border);
            SoftCache[radius] = sprite;
            return sprite;
        }

        /// <summary>Upward-pointing filled triangle (ship marker, +Y is forward).</summary>
        public static Sprite Triangle()
        {
            if (triangleSprite != null) return triangleSprite;
            const int size = 48;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float coverage = 0f;
                for (int sx = 0; sx < 2; sx++)
                for (int sy = 0; sy < 2; sy++)
                {
                    float px = (x + 0.25f + sx * 0.5f) / size;
                    float py = (y + 0.25f + sy * 0.5f) / size;
                    // Triangle apex at top centre, base across the bottom, slightly inset.
                    float halfWidth = Mathf.Lerp(0.46f, 0.02f, py) ;
                    bool inside = py > 0.08f && Mathf.Abs(px - 0.5f) < halfWidth;
                    if (inside) coverage += 0.25f;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(coverage * 255f));
            }
            triangleSprite = MakeSprite(pixels, size, 0);
            return triangleSprite;
        }

        /// <summary>Anti-aliased circular outline (radar range rings).</summary>
        public static Sprite Ring()
        {
            if (ringSprite != null) return ringSprite;
            const int size = 128;
            float half = size * 0.5f;
            float radius = half - 3f;
            const float thickness = 2.2f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) +
                                     (y + 0.5f - half) * (y + 0.5f - half));
                float a = Mathf.Clamp01(thickness - Mathf.Abs(d - radius));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            ringSprite = MakeSprite(pixels, size, 0);
            return ringSprite;
        }

        /// <summary>Anti-aliased filled disc (buoys, dots, waypoint).</summary>
        public static Sprite Disc()
        {
            if (discSprite != null) return discSprite;
            const int size = 32;
            float half = size * 0.5f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) +
                                     (y + 0.5f - half) * (y + 0.5f - half));
                float a = Mathf.Clamp01(half - 1f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            discSprite = MakeSprite(pixels, size, 0);
            return discSprite;
        }

        // ---- Icons --------------------------------------------------------

        public static Sprite Icon(HudIcon icon)
        {
            int key = (int)icon;
            if (IconCache.TryGetValue(key, out Sprite cached)) return cached;

            const int size = 64;
            var alpha = new float[size * size];
            DrawIcon(icon, alpha, size);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha[i]) * 255f));

            Sprite sprite = MakeSprite(pixels, size, 0);
            IconCache[key] = sprite;
            return sprite;
        }

        // ---- Drawing helpers ---------------------------------------------

        private static void DrawIcon(HudIcon icon, float[] a, int s)
        {
            float c = s * 0.5f;
            switch (icon)
            {
                case HudIcon.Speed: // forward chevrons
                    for (int k = 0; k < 3; k++)
                    {
                        float x = s * (0.30f + k * 0.16f);
                        Line(a, s, x, s * 0.30f, x + s * 0.16f, c, 3.4f);
                        Line(a, s, x, s * 0.70f, x + s * 0.16f, c, 3.4f);
                    }
                    break;
                case HudIcon.Compass:
                    Ring(a, s, c, c, s * 0.40f, 3.2f);
                    Line(a, s, c, s * 0.20f, c, s * 0.80f, 3.0f); // needle
                    Line(a, s, c - s * 0.12f, s * 0.42f, c, s * 0.20f, 3.0f);
                    Line(a, s, c + s * 0.12f, s * 0.42f, c, s * 0.20f, 3.0f);
                    break;
                case HudIcon.Depth: // down arrow over depth bars
                    Line(a, s, c, s * 0.18f, c, s * 0.62f, 3.2f);
                    Line(a, s, c, s * 0.62f, c - s * 0.14f, s * 0.44f, 3.2f);
                    Line(a, s, c, s * 0.62f, c + s * 0.14f, s * 0.44f, 3.2f);
                    Line(a, s, s * 0.22f, s * 0.80f, s * 0.78f, s * 0.80f, 3.0f);
                    break;
                case HudIcon.Current: // wavy horizontal arrow
                    Wave(a, s, s * 0.18f, s * 0.78f, c, 3.0f);
                    Line(a, s, s * 0.78f, c, s * 0.62f, c - s * 0.12f, 3.0f);
                    Line(a, s, s * 0.78f, c, s * 0.62f, c + s * 0.12f, 3.0f);
                    break;
                case HudIcon.Cargo: // stacked container box
                    Rect(a, s, s * 0.24f, s * 0.40f, s * 0.76f, s * 0.80f, 3.0f);
                    Line(a, s, c, s * 0.40f, c, s * 0.80f, 2.6f);
                    Line(a, s, s * 0.24f, s * 0.60f, s * 0.76f, s * 0.60f, 2.6f);
                    Line(a, s, s * 0.30f, s * 0.40f, s * 0.42f, s * 0.24f, 2.6f);
                    Line(a, s, s * 0.70f, s * 0.40f, s * 0.58f, s * 0.24f, 2.6f);
                    Line(a, s, s * 0.42f, s * 0.24f, s * 0.58f, s * 0.24f, 2.6f);
                    break;
                case HudIcon.Wind:
                    Wave(a, s, s * 0.18f, s * 0.66f, s * 0.38f, 2.8f);
                    Wave(a, s, s * 0.18f, s * 0.78f, s * 0.54f, 2.8f);
                    Arc(a, s, s * 0.66f, s * 0.38f, s * 0.10f, -90f, 160f, 2.8f);
                    Arc(a, s, s * 0.78f, s * 0.54f, s * 0.10f, -90f, 160f, 2.8f);
                    break;
                case HudIcon.Rain:
                    Arc(a, s, c, s * 0.40f, s * 0.22f, 180f, 180f, 3.0f); // cloud top
                    Line(a, s, s * 0.34f, s * 0.40f, s * 0.66f, s * 0.40f, 3.0f);
                    for (int k = 0; k < 3; k++)
                    {
                        float x = s * (0.34f + k * 0.16f);
                        Line(a, s, x, s * 0.56f, x - s * 0.05f, s * 0.78f, 2.6f);
                    }
                    break;
                case HudIcon.Fog:
                    for (int k = 0; k < 4; k++)
                    {
                        float y = s * (0.30f + k * 0.14f);
                        float inset = (k % 2) * s * 0.10f;
                        Line(a, s, s * 0.18f + inset, y, s * 0.82f - inset, y, 2.8f);
                    }
                    break;
                case HudIcon.Camera:
                    Rect(a, s, s * 0.18f, s * 0.34f, s * 0.82f, s * 0.74f, 3.0f);
                    Line(a, s, s * 0.34f, s * 0.34f, s * 0.42f, s * 0.24f, 3.0f);
                    Line(a, s, s * 0.54f, s * 0.24f, s * 0.62f, s * 0.34f, 3.0f);
                    Line(a, s, s * 0.42f, s * 0.24f, s * 0.54f, s * 0.24f, 3.0f);
                    Ring(a, s, c, s * 0.54f, s * 0.13f, 3.0f);
                    break;
                case HudIcon.Time:
                    Ring(a, s, c, c, s * 0.38f, 3.0f);
                    Line(a, s, c, c, c, s * 0.30f, 2.8f);
                    Line(a, s, c, c, c + s * 0.16f, c, 2.8f);
                    break;
                case HudIcon.Rudder:
                    Ring(a, s, c, c, s * 0.38f, 3.0f);
                    Line(a, s, c, s * 0.50f, c, s * 0.16f, 3.2f);
                    Line(a, s, c - s * 0.10f, s * 0.30f, c, s * 0.16f, 3.0f);
                    Line(a, s, c + s * 0.10f, s * 0.30f, c, s * 0.16f, 3.0f);
                    break;
                case HudIcon.Warning:
                    Line(a, s, c, s * 0.18f, s * 0.16f, s * 0.80f, 3.2f);
                    Line(a, s, c, s * 0.18f, s * 0.84f, s * 0.80f, 3.2f);
                    Line(a, s, s * 0.16f, s * 0.80f, s * 0.84f, s * 0.80f, 3.2f);
                    Line(a, s, c, s * 0.40f, c, s * 0.62f, 3.4f);
                    Disc(a, s, c, s * 0.70f, 2.0f);
                    break;
            }
        }

        private static void Line(float[] a, int s, float x0, float y0, float x1, float y1, float w)
        {
            float half = w * 0.5f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(x0, x1) - half - 1));
            int maxX = Mathf.Min(s - 1, Mathf.CeilToInt(Mathf.Max(x0, x1) + half + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(y0, y1) - half - 1));
            int maxY = Mathf.Min(s - 1, Mathf.CeilToInt(Mathf.Max(y0, y1) + half + 1));
            float dx = x1 - x0, dy = y1 - y0;
            float lenSq = Mathf.Max(1e-4f, dx * dx + dy * dy);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float t = Mathf.Clamp01(((x - x0) * dx + (y - y0) * dy) / lenSq);
                float px = x0 + t * dx, py = y0 + t * dy;
                float dist = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                Accumulate(a, s, x, y, Mathf.Clamp01(half + 0.5f - dist));
            }
        }

        private static void Ring(float[] a, int s, float cx, float cy, float r, float w)
            => Arc(a, s, cx, cy, r, 0f, 360f, w);

        private static void Arc(float[] a, int s, float cx, float cy, float r,
            float startDeg, float sweepDeg, float w)
        {
            float half = w * 0.5f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r - half - 1));
            int maxX = Mathf.Min(s - 1, Mathf.CeilToInt(cx + r + half + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r - half - 1));
            int maxY = Mathf.Min(s - 1, Mathf.CeilToInt(cy + r + half + 1));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (sweepDeg < 360f)
                {
                    float ang = Mathf.Repeat(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - startDeg, 360f);
                    if (ang > sweepDeg) continue;
                }
                Accumulate(a, s, x, y, Mathf.Clamp01(half + 0.5f - Mathf.Abs(dist - r)));
            }
        }

        private static void Wave(float[] a, int s, float x0, float x1, float y, float w)
        {
            float amp = s * 0.06f;
            float prevX = x0, prevY = y;
            int steps = 20;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float x = Mathf.Lerp(x0, x1, t);
                float yy = y + Mathf.Sin(t * Mathf.PI * 2f) * amp;
                Line(a, s, prevX, prevY, x, yy, w);
                prevX = x; prevY = yy;
            }
        }

        private static void Rect(float[] a, int s, float x0, float y0, float x1, float y1, float w)
        {
            Line(a, s, x0, y0, x1, y0, w);
            Line(a, s, x1, y0, x1, y1, w);
            Line(a, s, x1, y1, x0, y1, w);
            Line(a, s, x0, y1, x0, y0, w);
        }

        private static void Disc(float[] a, int s, float cx, float cy, float r)
        {
            int minX = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1));
            int maxX = Mathf.Min(s - 1, Mathf.CeilToInt(cx + r + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1));
            int maxY = Mathf.Min(s - 1, Mathf.CeilToInt(cy + r + 1));
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                Accumulate(a, s, x, y, Mathf.Clamp01(r + 0.5f - dist));
            }
        }

        private static void Accumulate(float[] a, int s, int x, int y, float value)
        {
            int i = y * s + x;
            if (value > a[i]) a[i] = value;
        }

        // ---- Low level ----------------------------------------------------

        private static float RoundBox(float px, float py, float halfX, float halfY, float r)
        {
            float qx = Mathf.Abs(px) - halfX + r;
            float qy = Mathf.Abs(py) - halfY + r;
            float ox = Mathf.Max(qx, 0f);
            float oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static Sprite MakeSprite(Color32[] pixels, int size, int border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "HudGenerated",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            Vector4 borders = border > 0
                ? new Vector4(border, border, border, border)
                : Vector4.zero;
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect, borders);
            sprite.name = "HudGenerated";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
