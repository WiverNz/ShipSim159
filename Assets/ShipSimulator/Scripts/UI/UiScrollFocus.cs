using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    // Keyboard navigation must reveal the selected control in a clipped settings page.
    public sealed class UiScrollFocus : MonoBehaviour
    {
        private GameObject previous;
        private void LateUpdate()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == previous) return;
            previous = selected;
            var scroll = GetComponent<ScrollRect>();
            if (selected == null || !selected.transform.IsChildOf(scroll.content)) return;
            Canvas.ForceUpdateCanvases();
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, selected.transform);
            Rect view = scroll.viewport.rect;
            float shift = bounds.max.y > view.yMax ? view.yMax - bounds.max.y :
                bounds.min.y < view.yMin ? view.yMin - bounds.min.y : 0;
            scroll.content.anchoredPosition += Vector2.up * shift;
        }
    }
}
