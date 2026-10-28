using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    public sealed class HudTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        ISelectHandler, IDeselectHandler
    {
        private GameObject hint;
        public void Initialise(Font font, string caption)
        {
            var rect = UiLayout.Rect(transform, "Tooltip", 0, -42, 240, 36);
            hint = rect.gameObject;
            var background = hint.AddComponent<Image>();
            background.color = HudTheme.PanelFill;
            background.raycastTarget = false;
            var label = UiLayout.Rect(rect, "Caption", 8, 0, 224, 36).gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = 16;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = HudTheme.TextPrimary;
            label.raycastTarget = false;
            LocalizedLabel.BindSource(label, caption);
            hint.SetActive(false);
        }
        public void OnPointerEnter(PointerEventData data) => Show(true);
        public void OnPointerExit(PointerEventData data) => Show(false);
        public void OnSelect(BaseEventData data) => Show(true);
        public void OnDeselect(BaseEventData data) => Show(false);
        private void OnDisable() => Show(false);
        private void Show(bool visible) { if (hint != null) hint.SetActive(visible); }
    }
}
