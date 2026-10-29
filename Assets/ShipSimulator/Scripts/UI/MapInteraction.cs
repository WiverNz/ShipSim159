using ShipSimulator.CameraSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ShipSimulator.UI
{
    public sealed class MapInteraction : MonoBehaviour, IScrollHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private ShipTelemetryUI owner;
        private bool dragging;
        public static bool Dragging { get; private set; }
        public void Initialise(ShipTelemetryUI hud) => owner = hud;
        private bool Available => owner != null && !VoyageMenu.IsOpen &&
            !(Camera.main != null && Camera.main.TryGetComponent<ShipFollowCamera>(out var camera) && camera.OwnsOrbitGesture);
        public void OnScroll(PointerEventData data)
        {
            if (!Available) return;
            float gain = EventSystem.current.currentInputModule is InputSystemUIInputModule module ? module.scrollDeltaPerTick : 1f;
            owner.ZoomMap(data.scrollDelta.y / Mathf.Max(.001f, gain));
        }
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            dragging = Available && VoyageSettings.MapExpanded;
            if (dragging) Dragging = true;
        }
        public void OnDrag(PointerEventData data)
        {
            if (!dragging) return;
            if (!Available) { Release(); return; }
            var rect = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var now);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position - data.delta, data.pressEventCamera, out var before);
            owner.PanMap(now - before);
        }
        public void OnEndDrag(PointerEventData data)
        { if (data.button == PointerEventData.InputButton.Left) Release(); }
        private void Release() { if (dragging) Dragging = false; dragging = false; }
        private void OnDisable() => Release();
        private void OnApplicationFocus(bool focused) { if (!focused) Release(); }
        private void Update() { if (dragging && !Available) Release(); }
    }
}
