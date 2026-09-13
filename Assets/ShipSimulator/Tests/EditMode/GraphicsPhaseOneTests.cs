using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
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
        [Test]
        public void PostProcessingProfile_StoresEveryComponentInTheAsset()
        {
            const string path = "Assets/ShipSimulator/Settings/RiverVisualProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.That(profile.components, Is.Not.Empty);
            foreach (VolumeComponent component in profile.components)
            {
                Assert.That(component, Is.Not.Null, "A component that is not a sub-asset reloads as null.");
                Assert.That(AssetDatabase.GetAssetPath(component), Is.EqualTo(path));
            }
            Assert.That(profile.TryGet(out Tonemapping tonemapping), Is.True);
            Assert.That(tonemapping.mode.overrideState && tonemapping.mode.value == TonemappingMode.ACES, Is.True);
            Assert.That(profile.TryGet(out ColorAdjustments color), Is.True);
            Assert.That(color.saturation.value, Is.EqualTo(0f));
        }
        [TestCase("RiverTrainingScene")]
        [TestCase("GorodetsTrainingScene")]
        public void MainCamera_KeepsMostTemporalHistory(string name)
        {
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity");
            var camera = GameObject.Find("Main Camera").GetComponent<UniversalAdditionalCameraData>();
            Assert.That(camera.antialiasing, Is.EqualTo(AntialiasingMode.TemporalAntiAliasing));
            Assert.That(camera.taaSettings.baseBlendFactor, Is.GreaterThanOrEqualTo(0.8f));
        }
        [Test]
        public void CloudCookie_TexelsMapBackOntoTheSunRayUrpProjects()
        {
            Matrix4x4 lightToWorld = Matrix4x4.TRS(new Vector3(12, 40, -7), Quaternion.Euler(38, -48, 0), Vector3.one);
            const float size = 6000;
            var offset = new Vector2(135, -820);
            // Same construction as URP LightCookieManager.SetupMainLight.
            Matrix4x4 uvTransform = Matrix4x4.Scale(new Vector3(1 / size, 1 / size, 1));
            uvTransform.SetColumn(3, new Vector4(-offset.x / size, -offset.y / size, 0, 1));
            Matrix4x4 worldToCookie = Matrix4x4.Ortho(-0.5f, 0.5f, -0.5f, 0.5f, -0.5f, 0.5f) *
                uvTransform * lightToWorld.inverse;
            Matrix4x4 cookieToWorld = RiverLighting.CookieToWorld(lightToWorld, size, offset);
            Vector3 forward = lightToWorld.MultiplyVector(Vector3.forward).normalized;
            var random = new System.Random(3);
            for (int i = 0; i < 20; i++)
            {
                var world = new Vector3((float)random.NextDouble() * 4000 - 2000,
                    (float)random.NextDouble() * 30, (float)random.NextDouble() * 4000 - 2000);
                Vector3 projected = worldToCookie.MultiplyPoint(world);
                var uv = new Vector3(projected.x * 0.5f + 0.5f, projected.y * 0.5f + 0.5f, 0);
                Vector3 texel = cookieToWorld.MultiplyPoint(uv);
                Assert.That(Vector3.Cross(texel - world, forward).magnitude, Is.LessThan(0.05f),
                    "The texel must lie on the same sun ray as the shaded point.");
            }
        }
        [Test]
        public void SkyAmbient_UniformSkyLightsUpwardSurfacesWithItsRadianceAndGroundLess()
        {
            var samples = new Vector4[RiverLighting.SkyColumns * RiverLighting.SkyRows];
            for (int i = 0; i < samples.Length; i++) samples[i] = new Vector4(0.5f, 0.5f, 0.5f, 1);
            SphericalHarmonicsL2 sh = RiverLighting.ProjectSkyToAmbient(samples, RiverLighting.SkyColumns, RiverLighting.SkyRows);
            var colors = new Color[2];
            sh.Evaluate(new[] { Vector3.up, Vector3.down }, colors);
            Assert.That(colors[0].r, Is.EqualTo(0.5f).Within(0.07f));
            Assert.That(colors[1].r, Is.EqualTo(0.5f * RiverLighting.GroundBounce).Within(0.07f));
        }
    }
}
