#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    // The deck machinery runs on scaled frame time: pause stops it, simulation speed applies once, and the
    // result does not depend on the frame rate.
    public sealed class MechanismTimingTests
    {
        private GameObject vessel;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            Time.captureDeltaTime = 0;
            if (vessel != null) Object.Destroy(vessel);
            yield return null;
        }

        private IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private VesselMechanisms Spawn()
        {
            vessel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab"));
            vessel.GetComponent<ShipPhysicsController>().SetDepthProvider(_ => float.PositiveInfinity);
            return vessel.GetComponent<VesselMechanisms>();
        }

        [UnityTest]
        public IEnumerator PauseStops_SimulationSpeedAppliesOnce_FrameRateDoesNotMatter()
        {
            Time.captureDeltaTime = 0.02f;
            VesselMechanisms rig = Spawn();
            yield return Frames(3);
            rig.SetHatches(HatchPosition.HalfOpen);
            ShipPhysicsController ship = vessel.GetComponent<ShipPhysicsController>();
            ship.SetAnchorsReleased(true);
            float remaining = rig.HatchSecondsRemaining;
            yield return Frames(100);
            float normal = remaining - rig.HatchSecondsRemaining;
            Assert.That(normal, Is.EqualTo(2f).Within(0.05f), "100 frames of 20 ms are two seconds of machinery.");

            // A new time scale applies from the next frame; this frame's delta was taken at its start.
            Time.timeScale = 0;
            yield return null;
            remaining = rig.HatchSecondsRemaining;
            float chain = ship.GroundTackle.ChainOutM(0);
            Assert.That(chain, Is.GreaterThan(1), "The chain runs out on the physics step.");
            yield return Frames(50);
            Assert.That(rig.HatchSecondsRemaining, Is.EqualTo(remaining));
            Assert.That(ship.GroundTackle.ChainOutM(0), Is.EqualTo(chain), "Paused machinery stays put.");

            Time.timeScale = 2;
            yield return null;
            remaining = rig.HatchSecondsRemaining;
            yield return Frames(100);
            Assert.That(remaining - rig.HatchSecondsRemaining, Is.EqualTo(4f).Within(0.1f), "Twice the speed, once.");

            Time.timeScale = 1;
            Time.captureDeltaTime = 0.1f;
            yield return null;
            remaining = rig.HatchSecondsRemaining;
            yield return Frames(20);
            Assert.That(remaining - rig.HatchSecondsRemaining, Is.EqualTo(2f).Within(0.05f), "Ten frames per second, same rate.");
        }

        [UnityTest]
        public IEnumerator ResettingTheVessel_StowsMachineryAndKeepsTheRoot()
        {
            VesselMechanisms rig = Spawn();
            yield return null;
            Time.captureDeltaTime = 0.5f;
            Time.timeScale = 4;
            rig.SetHatches(HatchPosition.ForwardOpen);
            ShipPhysicsController ship = vessel.GetComponent<ShipPhysicsController>();
            ship.SetAnchorsReleased(true);
            yield return Frames(30);
            Assert.That(rig.Opening, Is.GreaterThan(0));
            Assert.That(ship.GroundTackle.ChainOutM(0), Is.GreaterThan(1));
            ship.ResetToStart();
            Assert.That(rig.Opening, Is.Zero);
            Assert.That(ship.GroundTackle.ChainOutM(0), Is.Zero);
            Assert.That(ship.GroundTackle.Phase(0), Is.EqualTo(AnchorPhase.Stowed));
        }
    }
}
#endif
