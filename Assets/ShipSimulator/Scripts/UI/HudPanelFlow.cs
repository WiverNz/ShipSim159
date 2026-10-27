using UnityEngine;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    public sealed class HudPanelFlow : MonoBehaviour
    {
        private Text status;
        private HudButton[] buttons;
        private float[] minimumWidths;
        private int[] groupSizes;
        private string previousText, previousLanguage;
        private float previousWidth, previousScale;
        private bool dirty = true;

        public static void Configure(Text status, params int[] groups)
        {
            if (status == null) return;
            var flow = status.transform.parent.gameObject.AddComponent<HudPanelFlow>();
            flow.status = status;
            flow.buttons = flow.GetComponentsInChildren<HudButton>();
            flow.minimumWidths = new float[flow.buttons.Length];
            for (int i = 0; i < flow.buttons.Length; i++)
                flow.minimumWidths[i] = ((RectTransform)flow.buttons[i].transform).sizeDelta.x;
            flow.groupSizes = groups;
        }

        private void OnEnable() { dirty = true; Font.textureRebuilt += FontRebuilt; }
        private void OnDisable() => Font.textureRebuilt -= FontRebuilt;
        private void FontRebuilt(Font font) { if (status != null && status.font == font) dirty = true; }

        private void LateUpdate()
        {
            var panel = (RectTransform)transform;
            if (status == null || (!dirty && previousScale == status.pixelsPerUnit && previousText == status.text && previousLanguage == Localization.Language &&
                previousWidth == panel.rect.width)) return;
            dirty = false;
            previousScale = status.pixelsPerUnit;
            previousText = status.text;
            previousLanguage = Localization.Language;
            previousWidth = panel.rect.width;
            float available = panel.rect.width - 16;
            UiLayout.Place(status.rectTransform, 8, 8, available, Mathf.Max(24, status.preferredHeight + 8));
            float y = 28 + status.preferredHeight;
            int index = 0;
            foreach (int count in groupSizes)
            {
                float x = 8, rowHeight = 32;
                for (int i = 0; i < count; i++, index++)
                {
                    Text label = buttons[index].Label;
                    float width = Mathf.Min(available, Mathf.Max(minimumWidths[index], Mathf.Ceil(label.preferredWidth) + 20));
                    if (x > 8 && x + width > panel.rect.width - 8)
                    {
                        y += rowHeight + 6;
                        x = 8; rowHeight = 32;
                    }
                    var rect = (RectTransform)buttons[index].transform;
                    UiLayout.Place(rect, x, y, width, 32);
                    float height = Mathf.Max(32, label.preferredHeight + 8);
                    rect.sizeDelta = new Vector2(width, height);
                    rowHeight = Mathf.Max(rowHeight, height);
                    x += width + 4;
                }
                y += rowHeight + 8;
            }
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, y);
            GetComponentInParent<HudLayout>()?.Invalidate();
        }
    }
}
