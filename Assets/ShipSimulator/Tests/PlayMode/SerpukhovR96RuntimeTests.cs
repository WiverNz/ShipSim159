using System.Collections;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class SerpukhovR96RuntimeTests
    {
        private GameObject vessel, obstacle;
        private SimulationMode previous;
        [SetUp] public void SetUp()
        {
            previous = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
        }
        [TearDown] public void TearDown()
        {
            if (vessel != null) Object.DestroyImmediate(vessel);
            if (obstacle != null) Object.DestroyImmediate(obstacle);
            UnityEngine.Physics.simulationMode = previous;
        }

        private ShipPhysicsController Spawn()
        {
            vessel = Object.Instantiate(VesselCatalogue.Load().Find("serpukhov-r96").prefab);
            var ship = vessel.GetComponent<ShipPhysicsController>();
            ship.ManualStepping = true;
            ship.SetAmbientCurrent(Vector3.zero); ship.SetWindVelocity(Vector3.zero);
            ship.SetDepthProvider(_ => 20);
            for (int i = 0; i < 1000; i++) Step(ship);
            return ship;
        }
        private static void Step(ShipPhysicsController ship)
        {
            ship.Simulate(.02f); UnityEngine.Physics.Simulate(.02f);
        }

        [UnityTest]
        public IEnumerator HullAndKnees_StopAgainstADockWithoutTunnelling([Values("bow", "stern", "side")] string side)
        {
            var ship = Spawn();
            Assert.That(Mathf.Abs(ship.Body.position.y), Is.LessThan(.2f), "Loaded waterline equilibrium");
            bool broadside = side == "side";
            Vector3 direction = broadside ? Vector3.right : side == "bow" ? Vector3.forward : Vector3.back;
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = direction * (broadside ? 5 : 14);
            obstacle.transform.localScale = broadside ? new Vector3(1, 20, 80) : new Vector3(80, 20, 1);
            UnityEngine.Physics.SyncTransforms();
            ship.Body.linearVelocity = direction * 3;
            float maximum = 0;
            for (int i = 0; i < 1000; i++)
            {
                Step(ship);
                maximum = Mathf.Max(maximum, Vector3.Dot(ship.Body.position, direction));
            }
            Assert.That(maximum, Is.LessThan(broadside ? 2.2f : side == "bow" ? 2.8f : 3.1f));
            Assert.That(maximum, Is.GreaterThan(1), "The hull must actually approach the obstacle");
            Assert.That(Mathf.Abs(Vector3.Dot(ship.Body.linearVelocity, direction)), Is.LessThan(.3f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SingleMasthead_CreatesFourRunningLights()
        {
            Spawn();
            var rig = vessel.AddComponent<NavigationLightRig>(); rig.EnsureCreated();
            Assert.That(vessel.GetComponentsInChildren<Light>().Length, Is.EqualTo(4));
            rig.SetNight(true);
            foreach (Light lamp in vessel.GetComponentsInChildren<Light>()) Assert.That(lamp.enabled, Is.True);
            yield return null;
        }
    }
}
