using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    public static class GameplayInput
    {
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        public static bool KeyboardBlocked
        {
            get
            {
                if (VoyageMenu.IsOpen) return true;
                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                return selected != null && selected.activeInHierarchy && (selected.GetComponent<InputField>() != null ||
                    selected.GetComponent<Slider>() != null || selected.GetComponent<Dropdown>() != null);
            }
        }

        // Query directly: gameplay Update can precede the EventSystem's cached pointer update.
        public static bool PointerOverUi()
        {
            if (VoyageMenu.IsOpen) return true;
            if (EventSystem.current == null || Mouse.current == null) return false;
            var pointer = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
            Hits.Clear();
            EventSystem.current.RaycastAll(pointer, Hits);
            bool found = Hits.Exists(hit => hit.module is GraphicRaycaster);
            Hits.Clear();
            return found;
        }
    }
}
