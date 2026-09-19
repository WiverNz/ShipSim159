using System.Collections;
using NUnit.Framework;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class ShipFollowCameraTests
    {
        [UnityTest]
        public IEnumerator MovingNavigator_RemainsAtTheModelEye()
        {
            GameObject vessel = TestVessel.Create(Vector3.zero);
            var cameraObject = new GameObject("Navigator regression camera", typeof(Camera));
            try
            {
                var ship = vessel.GetComponent<ShipPhysicsController>();
                var layout = vessel.AddComponent<VesselLayout>();
                var follow = cameraObject.AddComponent<ShipFollowCamera>();
                follow.SetTarget(ship);
                follow.SetView(follow.ViewCount - 1);
                ship.ManualStepping = true;
                ship.Body.useGravity = false;
                ship.Body.linearVelocity = Vector3.forward * 18f;
                for (int i = 0; i < 12; i++)
                {
                    yield return new WaitForFixedUpdate();
                    yield return null;
                    // LateUpdate follows the rendered/interpolated hull, so inspect after it directly.
                    cameraObject.SendMessage("LateUpdate");
                    Assert.That(Vector3.Distance(cameraObject.transform.position,
                        vessel.transform.TransformPoint(layout.NavigatorEye)), Is.LessThan(0.001f));
                }
            }
            finally
            {
                Object.Destroy(cameraObject);
                Object.Destroy(vessel);
            }
        }
    }
}
