using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShipSimulator.Tests
{
    public sealed class RiverLightingTests
    {
        private readonly List<GameObject> created = new List<GameObject>();
        private Light previousSun;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in created)
                if (item != null) Object.Destroy(item);
            created.Clear();
            RenderSettings.sun = previousSun;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Night_DimsTheSunWhenTheClockLivesOnAnotherObject()
        {
            previousSun = RenderSettings.sun;
            var sunObject = Track(new GameObject("Test sun"));
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            RenderSettings.sun = sun;
            // Gorodets layout: the weather system is serialized, the HUD adds the clock elsewhere.
            Track(new GameObject("Weather System")).AddComponent<WeatherController>();
            DayNightController clock = Track(new GameObject("TrainingUI")).AddComponent<DayNightController>();
            yield return null;

            clock.Apply(true);
            yield return null;

            Assert.That(RiverLighting.Active, Is.Not.Null);
            Assert.That(RiverLighting.Active.IsNight, Is.True);
            Assert.That(sun.intensity, Is.EqualTo(0.28f).Within(0.001f));

            clock.Apply(false);
            yield return null;

            Assert.That(RiverLighting.Active.IsNight, Is.False);
            Assert.That(sun.intensity, Is.EqualTo(1.25f).Within(0.001f));
        }

        // The HUD creates the weather controller when a scene has none, so the lighting and its sky
        // capture rig end up parented to the HUD object.
        [UnityTest]
        public IEnumerator SkyCaptureRig_SurvivesAHudRebuildOnTheSameObject()
        {
            GameObject shipObject = Track(TestVessel.Create(Vector3.zero));
            GameObject hudObject = Track(new GameObject("TrainingUI"));
            hudObject.AddComponent<WeatherController>();
            ShipTelemetryUI hud = hudObject.AddComponent<ShipTelemetryUI>();
            hud.SetShip(shipObject.GetComponent<ShipPhysicsController>());
            yield return null;
            yield return null;

            Transform rig = hudObject.transform.Find("Sky capture camera");
            Assert.That(rig, Is.Not.Null);
            Assert.That(hudObject.GetComponentsInChildren<Text>().Length, Is.GreaterThan(0));

            foreach (RectTransform instrument in hudObject.GetComponentsInChildren<RectTransform>())
                if (instrument.parent == hudObject.transform) Object.Destroy(instrument.gameObject);
            yield return null;
            yield return null;

            Assert.That(rig == null, Is.False, "The HUD destroyed a rig it did not create.");
            Assert.That(hudObject.GetComponentsInChildren<Text>().Length, Is.GreaterThan(0),
                "The HUD rebuilt its own instruments.");
        }

        [UnityTest]
        public IEnumerator DetailedBuoy_HasDynamicContactsAndNoSolidPlaceholder()
        {
            var navigation = Track(new GameObject("Navigation"));
            GameObject marker = Track(new GameObject("Right Red Buoy 01"));
            marker.transform.SetParent(navigation.transform, false);
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            placeholder.name = "Float";
            placeholder.transform.SetParent(marker.transform, false);
            Assert.That(marker.GetComponentsInChildren<Collider>().Length, Is.EqualTo(1));

            marker.AddComponent<ShipSimulator.Physics.MooredBuoy>().Initialize();
            marker.AddComponent<BuoyVisualRig>().Build(true);
            Track(new GameObject("Buoy day-night test")).AddComponent<DayNightController>().Apply(true);
            yield return null;

            Assert.That(placeholder.GetComponent<Collider>().enabled, Is.False);
            foreach (Collider mark in marker.transform.Find("Buoy contact").GetComponents<Collider>())
            {
                Assert.That(mark.isTrigger, Is.False);
                Assert.That(mark.attachedRigidbody.isKinematic, Is.False);
            }
            Assert.That(marker.transform.Find("Detailed buoy"), Is.Not.Null);
            Assert.That(marker.GetComponentInChildren<NavigationBeaconFlasher>(), Is.Not.Null);
            Assert.That(marker.GetComponentsInChildren<MeshCollider>(), Is.Empty,
                "A decorative lens must not leave a concave collider on the dynamic buoy.");
        }

        private GameObject Track(GameObject item)
        {
            created.Add(item);
            return item;
        }
    }
}
