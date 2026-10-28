using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShipSimulator.Tests
{
    public sealed class ShipTelemetryUITests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in created)
                if (item != null) Object.Destroy(item);
            created.Clear();
            Time.timeScale = 1f;
            yield return null;
        }

        // A voyage saved at 2x or 4x restores the scale through the simulation time controller, not
        // through the HUD, so a readout that only followed its own buttons told the pilot the wrong time.
        [UnityTest]
        public IEnumerator TimeReadout_FollowsAScaleSetOutsideTheHud()
        {
            GameObject shipObject = Track(TestVessel.Create(Vector3.zero));
            GameObject hudObject = Track(new GameObject("TrainingUI"));
            ShipTelemetryUI hud = hudObject.AddComponent<ShipTelemetryUI>();
            hud.SetShip(shipObject.GetComponent<ShipPhysicsController>());
            yield return null;
            yield return null;

            hudObject.GetComponent<HudLayout>().Select(2);
            Assert.That(Readout(hudObject, "TIME "), Is.EqualTo("TIME 1x"));

            hudObject.GetComponent<SimulationTimeController>().SetScale(4f);
            yield return null;

            Assert.That(Readout(hudObject, "TIME "), Is.EqualTo("TIME 4x"));
        }

        [UnityTest]
        public IEnumerator RadarDraft_FollowsThePhysicalDraftInsteadOfTheLoadingEstimate()
        {
            GameObject vessel = Track(TestVessel.Create(Vector3.zero));
            ShipPhysicsController ship = vessel.GetComponent<ShipPhysicsController>();
            GameObject hudObject = Track(new GameObject("Draft test HUD"));
            var hud = hudObject.AddComponent<ShipTelemetryUI>();
            hud.SetShip(ship);
            yield return null;
            yield return null;
            ship.ManualStepping = true;
            ship.Body.isKinematic = true;
            vessel.transform.position = Vector3.up * 0.4f;
            UnityEngine.Physics.SyncTransforms();
            ship.Simulate(0.02f);
            yield return null;
            hudObject.GetComponent<HudLayout>().Select(4);
            string text = Readout(hudObject, "DEPTH ");
            Assert.That(ship.EffectiveDraftM, Is.LessThan(ship.EstimatedDraftM - 0.2f));
            Assert.That(text, Does.Contain($"DRAFT</color> {ship.EffectiveDraftM:F1} m"));
        }

        [UnityTest]
        public IEnumerator PanelModes_ApplyImmediatelyAndKeepPinnedControlsAcrossTabs()
        {
            const string key = "ShipSim159.Settings.Panel.WeatherBlock";
            bool existed = PlayerPrefs.HasKey(key);
            int previous = PlayerPrefs.GetInt(key);
            GameObject shipObject = Track(TestVessel.Create(Vector3.zero));
            GameObject hudObject = Track(new GameObject("Panel test HUD"));
            var hud = hudObject.AddComponent<ShipTelemetryUI>();
            hud.SetShip(shipObject.GetComponent<ShipPhysicsController>());
            yield return null;
            yield return null;
            var layout = hud.GetComponent<HudLayout>();
            RectTransform weather = null;
            foreach (var rect in hud.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "WeatherBlock") weather = rect;
            Assert.That(weather, Is.Not.Null);
            try
            {
                VoyageSettings.SetPanelMode("WeatherBlock", 2);
                layout.Select(0);
                yield return null;
                Assert.That(weather.gameObject.activeInHierarchy, Is.True);
                layout.Select(2);
                Assert.That(weather.gameObject.activeInHierarchy, Is.True);
                VoyageSettings.SetPanelMode("WeatherBlock", 0);
                yield return null;
                Assert.That(weather.gameObject.activeInHierarchy, Is.False);
                layout.Select(3);
                Assert.That(weather.gameObject.activeInHierarchy, Is.False);
                VoyageSettings.SetPanelMode("WeatherBlock", 1);
                yield return null;
                Assert.That(weather.gameObject.activeInHierarchy, Is.True);
                layout.Select(0);
                Assert.That(weather.gameObject.activeInHierarchy, Is.False);
            }
            finally
            {
                if (existed) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
                VoyageSettings.Save();
            }
        }

        [UnityTest]
        public IEnumerator MapShortcut_OpensTabOnlyMapAndDoesNotHidePinnedObjective()
        {
            const string key = "ShipSim159.Settings.Panel.MiniMap";
            const string objectiveKey = "ShipSim159.Settings.Panel.Objective";
            bool existed = PlayerPrefs.HasKey(key), objectiveExisted = PlayerPrefs.HasKey(objectiveKey);
            int previous = PlayerPrefs.GetInt(key), previousObjective = PlayerPrefs.GetInt(objectiveKey);
            GameObject shipObject = Track(TestVessel.Create(Vector3.zero));
            GameObject hudObject = Track(new GameObject("Map panel test HUD"));
            var hud = hudObject.AddComponent<ShipTelemetryUI>();
            hud.SetShip(shipObject.GetComponent<ShipPhysicsController>());
            yield return null;
            yield return null;
            var layout = hud.GetComponent<HudLayout>();
            try
            {
                VoyageSettings.SetPanelMode("MiniMap", 1);
                layout.Select(0);
                layout.ToggleMap();
                Assert.That(layout.SelectedGroup, Is.EqualTo(4));
                layout.ToggleMap();
                Assert.That(layout.SelectedGroup, Is.EqualTo(0));
                VoyageSettings.SetPanelMode("Objective", 2);
                VoyageSettings.SetPanelMode("MiniMap", 2);
                layout.ToggleMap();
                yield return null;
                foreach (var rect in hud.GetComponentsInChildren<RectTransform>(true))
                    if (rect.name == "Objective") Assert.That(rect.gameObject.activeInHierarchy, Is.True);
            }
            finally
            {
                if (existed) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
                if (objectiveExisted) PlayerPrefs.SetInt(objectiveKey, previousObjective); else PlayerPrefs.DeleteKey(objectiveKey);
                VoyageSettings.Save();
            }
        }

        private static string Readout(GameObject hud, string prefix)
        {
            foreach (Text text in hud.GetComponentsInChildren<Text>())
                if (text.text.StartsWith(prefix)) return text.text;
            return null;
        }

        private GameObject Track(GameObject item)
        {
            created.Add(item);
            return item;
        }
    }
}
