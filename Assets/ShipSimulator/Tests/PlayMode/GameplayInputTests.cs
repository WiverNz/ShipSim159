using System.Collections;
using NUnit.Framework;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShipSimulator.Tests
{
    public sealed class GameplayInputTests
    {
        [UnityTest]
        public IEnumerator SliderFocus_BlocksGameplayUntilFocusLeavesTheVisibleControl()
        {
            var existing = EventSystem.current;
            var system = existing != null ? existing : new GameObject("Input test events", typeof(EventSystem)).GetComponent<EventSystem>();
            var previous = system.currentSelectedGameObject;
            var slider = new GameObject("Input test slider", typeof(RectTransform), typeof(Slider));
            var button = new GameObject("Input test button", typeof(RectTransform), typeof(Button));
            try
            {
                yield return null;
                EventSystem.current = system;
                system.SetSelectedGameObject(slider);
                Assert.That(system.currentSelectedGameObject, Is.EqualTo(slider));
                Assert.That(GameplayInput.KeyboardBlocked, Is.True);
                slider.SetActive(false);
                Assert.That(GameplayInput.KeyboardBlocked, Is.False, "A hidden settings slider must not block the resumed voyage.");
                system.SetSelectedGameObject(button);
                Assert.That(GameplayInput.KeyboardBlocked, Is.False);
            }
            finally
            {
                system.SetSelectedGameObject(previous);
                Object.Destroy(slider); Object.Destroy(button);
                if (existing == null) Object.Destroy(system.gameObject);
            }
        }
    }
}
