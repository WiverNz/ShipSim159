using NUnit.Framework;
using ShipSimulator.Editor;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class WaterOpticsTests
    {
        [Test]
        public void Absorption_PreservesContactAndHidesDeepBed()
        {
            Assert.That(WaterOptics.Transmittance(0, 1.2f, 0), Is.EqualTo(Vector3.one));
            Vector3 shallow = WaterOptics.Transmittance(0.5f, 1.2f, 0);
            Vector3 deep = WaterOptics.Transmittance(8, 1.2f, 0);
            Assert.That(shallow.x, Is.GreaterThan(shallow.y));
            Assert.That(shallow.y, Is.GreaterThan(shallow.z));
            Assert.That(shallow.z, Is.GreaterThan(0.2f));
            Assert.That(deep.magnitude, Is.LessThan(0.001f));
            Assert.That(WaterOptics.Transmittance(0.5f, 1.2f, 1).magnitude, Is.LessThan(shallow.magnitude));
        }
        [Test]
        public void Absorption_ComposesAlongConsecutivePathSegments()
        {
            Vector3 a = WaterOptics.Transmittance(0.4f, 1.2f, 0.3f);
            Vector3 b = WaterOptics.Transmittance(0.7f, 1.2f, 0.3f);
            Assert.That((Vector3.Scale(a, b) - WaterOptics.Transmittance(1.1f, 1.2f, 0.3f)).magnitude, Is.LessThan(0.00001f));
        }
        [TestCase("GorodetsTrainingScene")]
        [TestCase("RiverTrainingScene")]
        public void BuilderAndStoredMaterial_UseEstimatedVisibilityDepth(string name)
        {
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity");
            Material stored = GameObject.Find("RiverWater").GetComponent<Renderer>().sharedMaterial;
            Assert.That(stored.GetFloat("_SecchiDepth"), Is.EqualTo(1.2f).Within(0.001f));
            var copy = new Material(stored);
            try
            {
                copy.SetFloat("_SecchiDepth", 4);
                RiverWaterAndSkyBuilder.ConfigureOptics(copy);
                Assert.That(copy.GetFloat("_SecchiDepth"), Is.EqualTo(stored.GetFloat("_SecchiDepth")));
                Assert.That(copy.GetFloat("_RefractionStrength"), Is.EqualTo(stored.GetFloat("_RefractionStrength")));
                Assert.That(ShaderUtil.ShaderHasError(stored.shader), Is.False);
            }
            finally { Object.DestroyImmediate(copy); }
        }
    }
}
