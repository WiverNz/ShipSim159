using UnityEngine;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    // Both canvases use the same physical scale; small screens reflow instead of shrinking text.
    public static class UiLayout
    {
        public static float Scale(Vector2 pixels, float preference) =>
            Mathf.Clamp(Mathf.Min(pixels.x / 1920f, pixels.y / 1080f), .8f, 2f) * preference;

        public static Vector2 Apply(Canvas canvas)
        {
            var pixels = canvas.pixelRect.size;
            if (pixels.x <= 0 || pixels.y <= 0) pixels = new Vector2(Screen.width, Screen.height);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = Scale(pixels, VoyageSettings.UiScale);
            canvas.scaleFactor = scaler.scaleFactor;
            return pixels / scaler.scaleFactor;
        }

        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Place(rect, x, y, w, h);
            return rect;
        }

        public static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
        }

        public static ScrollRect Scroll(RectTransform viewport, RectTransform content)
        {
            var image = viewport.gameObject.AddComponent<Image>();
            image.color = new Color(.025f, .06f, .08f, .94f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            var track = Rect(viewport, "Scrollbar", 0, 0, 10, 0);
            track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
            track.pivot = Vector2.one; track.offsetMin = new Vector2(-10, 4); track.offsetMax = new Vector2(-2, -4);
            track.gameObject.AddComponent<Image>().color = new Color(.12f, .21f, .25f);
            var handle = Rect(track, "Handle", 0, 0, 8, 30);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = HudTheme.AccentSoft;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle; bar.targetGraphic = handleImage;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            viewport.gameObject.AddComponent<UiScrollFocus>();
            return scroll;
        }
    }
}
