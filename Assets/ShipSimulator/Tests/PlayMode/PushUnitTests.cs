using System.Collections;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.Persistence;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class PushUnitTests
    {
        private GameObject root, obstacle;
        private ShipPhysicsController tug;
        private PushUnit unit;
        private SimulationMode previous;
        private float maximumError;

        [SetUp] public void SetUp()
        {
            previous = UnityEngine.Physics.simulationMode; UnityEngine.Physics.simulationMode = SimulationMode.Script;
        }
        [TearDown] public void TearDown()
        {
            if (unit != null && unit.Barge != null) Object.DestroyImmediate(unit.Barge.gameObject);
            if (root != null) Object.DestroyImmediate(root);
            if (obstacle != null) Object.DestroyImmediate(obstacle);
            UnityEngine.Physics.simulationMode = previous;
        }
        private void Spawn(bool combined = true)
        {
            root = Object.Instantiate(VesselCatalogue.Load().Find(combined ? "serpukhov-mn52" : "serpukhov-r96").prefab);
            tug = root.GetComponent<ShipPhysicsController>(); tug.ManualStepping = true;
            tug.SetAmbientCurrent(Vector3.zero); tug.SetWindVelocity(Vector3.zero); tug.SetDepthProvider(_ => 20);
            unit = root.GetComponent<PushUnit>();
            if (unit != null) { unit.EnsureSpawned(); unit.Barge.ManualStepping = true; }
            Step(1000); maximumError = 0;
        }
        private void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                tug.Simulate(.02f); if (unit != null) unit.Barge.Simulate(.02f);
                UnityEngine.Physics.Simulate(.02f);
                if (unit != null && unit.Coupled) maximumError = Mathf.Max(maximumError, unit.ContactError);
            }
        }

        [UnityTest]
        public IEnumerator PushUnit_IsHeavierSlowerAndStableThenReverses()
        {
            Spawn(false); tug.SetThrottleCommand(1); Step(4500); float soloSpeed = tug.Body.linearVelocity.z;
            Object.DestroyImmediate(root); Spawn();
            Assert.That(unit.Barge.transform.parent, Is.Null);
            Assert.That(unit.Barge.Body.mass, Is.GreaterThan(tug.Body.mass * 3));
            Assert.That(Mathf.Abs(unit.Barge.Body.position.y), Is.LessThan(.25f));
            tug.SetThrottleCommand(1); Step(4500); float pushedSpeed = tug.Body.linearVelocity.z;
            Assert.That(pushedSpeed, Is.GreaterThan(.5f)); Assert.That(pushedSpeed, Is.LessThan(soloSpeed * .85f));
            tug.SetThrottleCommand(0); Step(2500);
            Assert.That(tug.Body.linearVelocity.z, Is.LessThan(pushedSpeed));
            tug.SetThrottleCommand(-1); Step(7000);
            Assert.That(tug.Body.linearVelocity.z, Is.LessThan(-.3f));
            Assert.That(maximumError, Is.LessThan(.08f));
            Assert.That(Vector3.Angle(tug.transform.up, Vector3.up), Is.LessThan(8));
            TestContext.WriteLine($"solo={soloSpeed:F3}, pushed={pushedSpeed:F3}, reverse={tug.Body.linearVelocity.z:F3}, contact={maximumError:F4}");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PushUnit_TurnsBothWaysWithoutRelativeYaw([Values(-1f, 1f)] float helm)
        {
            Spawn(); tug.SetThrottleCommand(1); Step(3500); tug.SetRudderCommand(helm); Step(5000);
            Assert.That(tug.Body.angularVelocity.y * helm, Is.GreaterThan(.0001f));
            Assert.That(Quaternion.Angle(tug.Body.rotation, unit.Barge.Body.rotation), Is.LessThan(2));
            Assert.That(maximumError, Is.LessThan(.08f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BargeCollision_StopsBothBodiesAndDetachRestoresPairCollisions([Values(false, true)] bool side)
        {
            Spawn();
            Vector3 direction = side ? Vector3.right : Vector3.forward;
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = unit.Barge.Body.position + direction * (side ? 5 : 18);
            obstacle.transform.localScale = side ? new Vector3(1, 20, 100) : new Vector3(100, 20, 1);
            UnityEngine.Physics.SyncTransforms();
            Vector3 start = unit.Barge.Body.position;
            tug.Body.linearVelocity = unit.Barge.Body.linearVelocity = direction * 3;
            Step(1500);
            Assert.That(Vector3.Dot(unit.Barge.Body.position - start, direction), Is.LessThan(side ? 2.3f : 3.9f));
            Assert.That(Vector3.Dot(tug.Body.linearVelocity, direction), Is.LessThan(.3f));
            Assert.That(maximumError, Is.LessThan(.15f));
            var a = tug.GetComponentInChildren<Collider>(); var b = unit.Barge.GetComponentInChildren<Collider>();
            Assert.That(UnityEngine.Physics.GetIgnoreCollision(a, b), Is.True);
            Assert.That(UnityEngine.Physics.GetIgnoreCollision(b, obstacle.GetComponent<Collider>()), Is.False);
            unit.Detach(); Assert.That(UnityEngine.Physics.GetIgnoreCollision(a, b), Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DetachedBarge_DriftsInCurrentWithoutAnyPropulsion()
        {
            Spawn(); unit.Detach();
            unit.Barge.Body.position += Vector3.forward * 60;
            Vector3 start = unit.Barge.Body.position;
            tug.SetAmbientCurrent(Vector3.right);
            Step(2500);
            Assert.That(unit.Barge.Body.position.x - start.x, Is.GreaterThan(1));
            Assert.That(unit.Barge.Diagnostics.PropellerX, Is.Zero);
            Assert.That(unit.Barge.Model.Shafts, Is.Empty);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveRestore_RestoresBothBodiesAndCouplingAndResetMovesBoth()
        {
            Spawn(); tug.SetThrottleCommand(1); Step(1500);
            var save = new VoyageSave { position = tug.Body.position, rotation = tug.Body.rotation,
                velocity = tug.Body.linearVelocity, angularVelocity = tug.Body.angularVelocity, pushUnit = unit.Capture() };
            Step(500); tug.RestoreVoyage(save);
            Assert.That(Vector3.Distance(unit.Barge.Body.position, save.pushUnit.position), Is.LessThan(.001f));
            Assert.That(unit.Coupled, Is.True); Step(100); Assert.That(unit.ContactError, Is.LessThan(.08f));
            unit.Detach(); var detached = unit.Capture(); unit.Restore(detached); Assert.That(unit.Coupled, Is.False);
            tug.SendMessage("ResetVessel"); Assert.That(unit.Coupled, Is.True);
            Assert.That(unit.ContactError, Is.LessThan(.001f));
            yield return null;
        }
    }
}
