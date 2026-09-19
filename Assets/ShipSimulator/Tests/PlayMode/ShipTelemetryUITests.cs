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
            string text = Readout(hudObject, "DEPTH ");
            Assert.That(ship.EffectiveDraftM, Is.LessThan(ship.EstimatedDraftM - 0.2f));
            Assert.That(text, Does.Contain($"DRAFT</color> {ship.EffectiveDraftM:F1} m"));
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
