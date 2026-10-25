using NUnit.Framework;
using UnityEngine;
using ShipSimulator.Editor;

namespace ShipSimulator.Tests
{
    public sealed class RuntimeShaderBuildTests
    {
        [Test]
        public void RuntimeNavigationAndRainShadersHaveBuildDependencies()
        {
            RuntimeShaderBuildAssets.Ensure();
            Material navigation = Resources.Load<Material>("RuntimeShaders/NavigationUnlit");
            Material rain = Resources.Load<Material>("RuntimeShaders/RainUnlit");
            Assert.That(navigation, Is.Not.Null);
            Assert.That(navigation.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            Assert.That(rain, Is.Not.Null);
            Assert.That(rain.shader.name, Is.EqualTo("Universal Render Pipeline/Particles/Unlit"));
            Assert.That(rain.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"), Is.True);
        }
    }
}
