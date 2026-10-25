using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class PlanarReflectionProjectionTests
    {
        [Test]
        public void LowAndGrazingViews_HaveFiniteReflectedCullingFrusta()
        {
            var sourceObject = new GameObject("Reflection source test");
            var reflectedObject = new GameObject("Reflection target test");
            try
            {
                var source = sourceObject.AddComponent<Camera>();
                var reflected = reflectedObject.AddComponent<Camera>();
                source.enabled = reflected.enabled = false;
                source.aspect = 16f / 9f; source.nearClipPlane = .1f; source.farClipPlane = 3000;
                foreach (float height in new[] { .11f, .2f, 1f, 5f, 40f })
                foreach (float pitch in new[] { -25f, -1f, 0f, 1f, 22f, 30f, 60f, 89f })
                {
                    source.transform.position = new Vector3(30, height, -145);
                    source.transform.rotation = Quaternion.Euler(pitch, 140, 0);
                    reflected.CopyFrom(source); reflected.farClipPlane = 1400;
                    RiverPlanarReflection.SetReflectionMatrices(source, reflected, 0);
                    Assert.That(reflected.TryGetCullingParameters(out var culling), Is.True);
                    for (int i = 0; i < 16; i++)
                    {
                        Assert.That(float.IsFinite(reflected.projectionMatrix[i]), Is.True);
                        Assert.That(float.IsFinite(culling.cullingMatrix[i]), Is.True);
                    }
                    Assert.That(reflected.transform.position.y, Is.EqualTo(-height).Within(1e-5f));
                    Assert.That(reflected.projectionMatrix, Is.Not.EqualTo(source.projectionMatrix));
                }
            }
            finally { Object.DestroyImmediate(sourceObject); Object.DestroyImmediate(reflectedObject); }
        }
    }
}
