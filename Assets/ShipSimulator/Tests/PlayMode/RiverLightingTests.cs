using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.TestTools;

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

        private GameObject Track(GameObject item)
        {
            created.Add(item);
            return item;
        }
    }
}
