using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class BuoyVisualTests
    {
        [Test]
        public void Flash_HasARepeatablePulseAndLongDarkInterval()
        {
            var owner = new GameObject("Beacon test");
            try
            {
                var light = owner.AddComponent<Light>();
                var flash = owner.AddComponent<NavigationBeaconFlasher>();
                flash.Configure(light, null, 2.5f, 0.35f, 0);
                flash.SetNight(true);
                Assert.That(flash.EvaluateLit(0.1f), Is.True);
                Assert.That(flash.EvaluateLit(0.4f), Is.False);
                Assert.That(flash.EvaluateLit(2.4f), Is.False);
                Assert.That(flash.EvaluateLit(2.6f), Is.True);
                flash.SetNight(false);
                Assert.That(flash.EvaluateLit(2.6f), Is.False);
                Assert.That(light.enabled, Is.False);
            }
            finally { Object.DestroyImmediate(owner); }
        }
        [Test]
        public void DetailedBuoy_BuildIsIdempotentAndPreservesCollisionGeometry()
        {
            var owner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            try
            {
                var collider = owner.GetComponent<Collider>();
                var buoy = owner.AddComponent<BuoyVisualRig>();
                buoy.Build(true);
                int count = owner.GetComponentsInChildren<Renderer>().Length;
                buoy.Build(true);
                Assert.That(owner.GetComponentsInChildren<Renderer>().Length, Is.EqualTo(count));
                Assert.That(collider.enabled, Is.True);
                Assert.That(owner.GetComponent<Renderer>().enabled, Is.False);
                Assert.That(buoy.Floating.Find("Lantern glass"), Is.Not.Null);
                Assert.That(buoy.Floating.Find("Painted steel"), Is.Not.Null);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
