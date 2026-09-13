using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Tests
{
    public sealed class GraphicsPhaseOneTests
    {
        [Test]
        public void Exposure_DarkensBrightFramesAndPreservesNight()
        {
            Assert.That(RiverLighting.MeterExposure(new[] { 4f, 4f, 4f }, false), Is.LessThan(0));
            Assert.That(RiverLighting.MeterExposure(new[] { 0.01f, 0.01f }, false), Is.GreaterThan(0));
            Assert.That(RiverLighting.MeterExposure(new[] { 0.01f, 0.01f }, true), Is.LessThanOrEqualTo(1.2f));
            Assert.That(RiverLighting.MeterExposure(new[] { float.NaN, float.PositiveInfinity }, false), Is.EqualTo(0));
        }
        [Test]
        public void Exposure_RejectsSmallBrightOutliers()
        {
            var values = new float[100];
            for (int i = 0; i < values.Length; i++) values[i] = 0.18f;
            float reference = RiverLighting.MeterExposure(values, false);
            values[0] = 100000;
            Assert.That(RiverLighting.MeterExposure(values, false), Is.EqualTo(reference).Within(0.01f));
        }
        [TestCase("ShipSimulator/RiverFoliage", "MotionVectors")]
        [TestCase("ShipSimulator/RiverWater", "RiverMotionVectors")]
        public void AnimatedSurface_ProvidesMotionPass(string shader, string pass)
        {
            var material = new Material(Shader.Find(shader));
            try { Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0)); }
            finally { Object.DestroyImmediate(material); }
        }
        [Test]
        public void DesktopRenderer_HasExposureAndWaterMotionFeatures()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            Assert.That(renderer.rendererFeatures.Exists(f => f is RiverTemporalFeature && f.isActive), Is.True);
            Assert.That(renderer.rendererFeatures.Exists(f => f is RiverExposureFeature e && e.isActive && e.meterShader != null), Is.True);
        }
    }
}
