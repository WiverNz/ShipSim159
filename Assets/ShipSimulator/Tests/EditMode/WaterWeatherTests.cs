using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class WaterWeatherTests
    {
        [TestCase(0, -56.25f, 112.5f, true)]
        [TestCase(-90, -56.25f, 112.5f, true)]
        [TestCase(90, -56.25f, 112.5f, false)]
        [TestCase(180, 0, 225, false)]
        [TestCase(110, 0, 225, true)]
        [TestCase(-179, 180, 135, true)]
        [TestCase(0, 180, 135, false)]
        public void RunningLights_RespectNavigationSectors(float bearing, float center, float arc, bool visible)
        {
            Assert.That(NavigationLightSectors.IsVisible(bearing, center, arc), Is.EqualTo(visible));
        }

        [Test]
        public void LongVessel_HasHigherAftMastheadAndNoExtraBowLight()
        {
            var vessel = new GameObject("Sector test");
            try
            {
                var rig = vessel.AddComponent<NavigationLightRig>();
                rig.EnsureCreated();
                var forward = vessel.transform.Find("Forward Masthead Light");
                var aft = vessel.transform.Find("Aft Masthead Light");
                Assert.That(aft.localPosition.y - forward.localPosition.y, Is.GreaterThanOrEqualTo(3));
                Assert.That(forward.localPosition.z, Is.GreaterThan(aft.localPosition.z));
                Assert.That(vessel.transform.Find("Bow Navigation Light"), Is.Null);
                Assert.That(vessel.GetComponentsInChildren<NavigationBeaconFlasher>(), Is.Empty);
                foreach (Light light in vessel.GetComponentsInChildren<Light>())
                    Assert.That(light.cookie, Is.Not.Null);
            }
            finally { Object.DestroyImmediate(vessel); }
        }

        [TestCase("RiverTrainingScene")]
        [TestCase("GorodetsTrainingScene")]
        public void WaterlineProfile_SeparatesWaterFromDryBanks(string scene)
        {
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + scene + ".unity");
            var owner = new GameObject("Shore test");
            try
            {
                var shore = owner.AddComponent<RiverShoreProfile>();
                shore.Build();
                Assert.That(shore.IsReady, Is.True);
                Vector4 range = Shader.GetGlobalVector("_RiverShoreRange");
                var texture = (Texture2D)Shader.GetGlobalTexture("_RiverShoreProfile");
                for (int i = 100; i < texture.width - 100; i += 137)
                {
                    Color banks = texture.GetPixel(i, 0);
                    float z = range.x + range.y * i / (texture.width - 1);
                    Assert.That(banks.g - banks.r, Is.GreaterThan(30));
                    Assert.That(shore.DistanceToBank(new Vector3((banks.r + banks.g) * 0.5f, 0, z)), Is.GreaterThan(10));
                    Assert.That(shore.DistanceToBank(new Vector3(banks.r - 2, 0, z)), Is.LessThan(0));
                    Assert.That(shore.DistanceToBank(new Vector3(banks.g + 2, 0, z)), Is.LessThan(0));
                }
            }
            finally { Object.DestroyImmediate(owner); }
        }

        [TestCase("ShipSimulator/RiverWater")]
        [TestCase("ShipSimulator/NavigationSectorLight")]
        public void WaterAndNavigationShaders_Compile(string name)
        {
            Shader shader = Shader.Find(name);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }
    }
}
