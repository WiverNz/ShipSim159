using System.Collections;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class GorodetsRuntimeTests
    {
        [UnityTest]
        public IEnumerator CurrentField_DischargeMultiplierChangesSample()
        {
            GameObject gameObject = new GameObject("Current Field");
            CurrentFieldProvider provider =
                gameObject.AddComponent<CurrentFieldProvider>();
            provider.Configure(new Vector3(0f, 0f, 1f), new CurrentRegionData[0]);

            yield return null;
            Assert.That(provider.Sample(Vector3.zero).z,
                Is.EqualTo(1f).Within(0.001f));

            provider.SetDischargeMultiplier(0.5f);
            Assert.That(provider.Sample(Vector3.zero).z,
                Is.EqualTo(0.5f).Within(0.001f));

            Object.Destroy(gameObject);
        }

        [UnityTest]
        public IEnumerator Bathymetry_WaterLevelOffsetChangesClearanceDepth()
        {
            GameObject routeObject = new GameObject("Route");
            FairwayRoute route = routeObject.AddComponent<FairwayRoute>();
            route.Configure(new[]
            {
                Sample(new Vector3(0f, 0f, 0f)),
                Sample(new Vector3(0f, 0f, 100f))
            });
            GameObject depthObject = new GameObject("Bathymetry");
            ScenarioBathymetry bathymetry =
                depthObject.AddComponent<ScenarioBathymetry>();
            bathymetry.Configure(route, new BathymetryHazard[0]);
            float baseline = bathymetry.Sample(new Vector3(0f, 0f, 50f)).DepthM;

            bathymetry.SetWaterLevelOffset(-0.8f);
            yield return null;

            Assert.That(bathymetry.Sample(new Vector3(0f, 0f, 50f)).DepthM,
                Is.EqualTo(baseline - 0.8f).Within(0.001f));
            Object.Destroy(routeObject);
            Object.Destroy(depthObject);
        }

        [UnityTest]
        public IEnumerator WeatherController_EnablesRainFogAndConfiguredWind()
        {
            GameObject weatherObject = new GameObject("Weather Test");
            WeatherController weather = weatherObject.AddComponent<WeatherController>();
            weather.Configure(315f, 8f, 0.7f, 0.65f);

            yield return null;

            ParticleSystem rain =
                weatherObject.GetComponentInChildren<ParticleSystem>();
            Assert.That(rain, Is.Not.Null);
            Assert.That(rain.isPlaying, Is.True);
            Assert.That(rain.emission.rateOverTime.constant,
                Is.GreaterThan(1000f));
            Assert.That(RenderSettings.fogDensity, Is.GreaterThan(0.003f));
            Assert.That(weather.WindVelocityMps.magnitude,
                Is.EqualTo(8f).Within(0.001f));

            Object.Destroy(weatherObject);
            RenderSettings.fogDensity = 0.0011f;
        }

        private static FairwayRouteSample Sample(Vector3 position)
        {
            return new FairwayRouteSample
            {
                position = position,
                leftWidthM = 30f,
                rightWidthM = 30f,
                centerDepthM = 5f,
                leftEdgeDepthM = 2f,
                rightEdgeDepthM = 2f,
                speedLimitMps = 3f
            };
        }
    }
}
