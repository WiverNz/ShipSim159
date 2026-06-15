using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    /// <summary>
    /// Reusable HUD button. Renders a rounded glass fill with an outline and a
    /// label, and animates between idle / hover / pressed / active (selected)
    /// states so the currently engaged control is always obvious. Purely visual
    /// and input-forwarding; it never touches simulation state itself.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class HudButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        private Image fill;
        private Image outline;
        private Image glow;
        private Text label;
        private Action onClick;
        private bool active;
        private bool hovered;
        private bool pressed;

        public Image Fill => fill;
        public Text Label => label;
        public bool IsActive => active;

        public void Initialise(Image fillImage, Image outlineImage, Image glowImage,
            Text labelText, Action click)
        {
            fill = fillImage;
            outline = outlineImage;
            glow = glowImage;
            label = labelText;
            onClick = click;
            Refresh();
        }

        public void SetActive(bool value)
        {
            if (active == value) return;
            active = value;
            Refresh();
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; Refresh(); }
        public void OnPointerDown(PointerEventData eventData) { pressed = true; Refresh(); }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; Refresh(); }

        public void OnPointerClick(PointerEventData eventData) => onClick?.Invoke();

        private void Refresh()
        {
            if (fill == null) return;

            Color baseColor = active ? HudTheme.ButtonActive : HudTheme.ButtonIdle;
            if (pressed) baseColor = Multiply(baseColor, 0.84f);
            else if (hovered) baseColor = Multiply(baseColor, active ? 1.12f : 1.30f);
            fill.color = baseColor;

            if (outline != null)
                outline.color = active
                    ? HudTheme.AccentSoft
                    : hovered ? HudTheme.Accent * 0.95f : HudTheme.PanelBorder;

            if (glow != null)
            {
                Color glowColor = HudTheme.AccentSoft;
                glowColor.a = active ? 0.55f : hovered ? 0.18f : 0f;
                glow.color = glowColor;
            }

            if (label != null)
            {
                label.color = active ? Color.white
                    : hovered ? HudTheme.TextPrimary : HudTheme.ButtonText;
                label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            }

            transform.localScale = active ? Vector3.one * 1.08f
                : pressed ? Vector3.one * 0.96f : Vector3.one;
        }

        private static Color Multiply(Color color, float factor)
        {
            return new Color(
                Mathf.Clamp01(color.r * factor),
                Mathf.Clamp01(color.g * factor),
                Mathf.Clamp01(color.b * factor),
                color.a);
        }
    }
}
