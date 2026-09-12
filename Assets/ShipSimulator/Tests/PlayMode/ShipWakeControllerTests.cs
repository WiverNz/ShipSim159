using System.Collections;
using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class ShipWakeControllerTests
    {
        private GameObject shipObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (shipObject != null) Object.Destroy(shipObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MovingVessel_PublishesItsWakeUntilDisabled()
        {
            shipObject = TestVessel.Create(Vector3.zero);
            shipObject.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.None;
            ShipWakeController wake = shipObject.AddComponent<ShipWakeController>();

            for (int metre = 1; metre <= 40; metre++)
            {
                shipObject.transform.position = new Vector3(0f, 0f, metre);
                UnityEngine.Physics.SyncTransforms();
                yield return null;
            }

            Assert.That(Shader.GetGlobalFloat("_WakeCount"), Is.GreaterThanOrEqualTo(5f));
            Vector4[] points = Shader.GetGlobalVectorArray("_WakePoints");
            Assert.That(points.Length, Is.EqualTo(ShipWakeTrack.Capacity));
            Assert.That(points[0].y - points[1].y, Is.EqualTo(10f).Within(0.01f),
                "Bow and stern must straddle the 10 m test hull.");
            Assert.That(points[2].y, Is.LessThanOrEqualTo(points[1].y), "History trails astern.");

            wake.enabled = false;

            Assert.That(Shader.GetGlobalFloat("_WakeCount"), Is.EqualTo(0f));
        }
    }
}
