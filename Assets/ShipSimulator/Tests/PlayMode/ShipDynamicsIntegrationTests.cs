using System.Collections;
using System.IO;
using NUnit.Framework;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    // Steps Unity physics by hand so long manoeuvres finish in one frame and match the pure simulator's step.
    public sealed class ShipDynamicsIntegrationTests
    {
        private const float Step = 0.02f;
        private SimulationMode previousMode;
        private GameObject shipObject;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (shipObject != null) Object.Destroy(shipObject);
            UnityEngine.Physics.simulationMode = previousMode;
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadedVessel_FloatsLevelAtItsDesignDraft()
        {
            VesselData data = LoadVolgoDon();
            ShipPhysicsController ship = CreateShip(data, float.PositiveInfinity, false);
            Run(ship, 60f);

            Assert.That(ship.EffectiveDraftM, Is.EqualTo(data.dimensions.loadedDraftM).Within(0.06f));
            Assert.That(Vector3.Angle(ship.transform.up, Vector3.up), Is.LessThan(0.3f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RigidbodyIntegration_MatchesPureSimulatorInATurn()
        {
            VesselData data = LoadVolgoDon();
            VesselParameters parameters = VesselParameters.Create(data);
            ManoeuvringSimulator reference = ManoeuvringTrials.Approach(parameters, 1f, float.PositiveInfinity);
            float approach = reference.SurgeSpeed;
            var rps = new float[reference.Model.Shafts.Length];
            for (int i = 0; i < rps.Length; i++) rps[i] = reference.Model.Shafts[i].Rps;
            reference.RudderCommand = 1f;

            ShipPhysicsController ship = CreateShip(data, float.PositiveInfinity, false);
            ship.RestoreVoyage(new VoyageSave
            {
                velocity = new Vector3(0f, 0f, approach),
                engineCommands = new[] { 1f, 1f },
                shaftRps = rps,
                rudder = 1f
            });

            float unityHeading = 0f;
            for (float t = 0f; t < 240f; t += Step)
            {
                reference.Step(Step);
                ship.Simulate(Step);
                UnityEngine.Physics.Simulate(Step);
                unityHeading += ship.Body.angularVelocity.y * Step;
            }

            float referenceHeading = reference.HeadingRad;
            Assert.That(referenceHeading, Is.GreaterThan(0.5f), "The reference ship must be turning to starboard.");
            Assert.That(unityHeading, Is.EqualTo(referenceHeading).Within(0.05f * referenceHeading));
            Vector3 horizontal = ship.Body.linearVelocity;
            horizontal.y = 0f;
            float referenceSpeed = Mathf.Sqrt(reference.SurgeSpeed * reference.SurgeSpeed + reference.SwaySpeed * reference.SwaySpeed);
            Assert.That(horizontal.magnitude, Is.EqualTo(referenceSpeed).Within(0.05f * referenceSpeed));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BeamWindFromPort_HeelsToStarboardAndDriftsDownwind()
        {
            ShipPhysicsController ship = CreateShip(LoadVolgoDon(), float.PositiveInfinity, false);
            ship.SetWindVelocity(new Vector3(15f, 0f, 0f));
            Run(ship, 90f);

            Assert.That(ship.Body.linearVelocity.x, Is.GreaterThan(0.05f));
            Assert.That(ship.transform.right.y, Is.LessThan(-1e-4f), "The starboard side must be lower.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Grounding_OnShoal_StopsVesselWithBottomFriction()
        {
            VesselData data = LoadVolgoDon();
            ShipPhysicsController free = CreateShip(data, float.PositiveInfinity, false);
            free.RestoreVoyage(new VoyageSave { position = new Vector3(0f, 0f, -100f), velocity = new Vector3(0f, 0f, 2f) });
            Run(free, 60f);
            float freeSpeed = free.Body.linearVelocity.magnitude;
            Object.Destroy(shipObject);
            yield return null;

            ShipPhysicsController grounded = CreateShip(data, float.PositiveInfinity, true);
            grounded.SetDepthProvider(position => position.z > 40f ? 2.5f : float.PositiveInfinity);
            grounded.RestoreVoyage(new VoyageSave { position = new Vector3(0f, 0f, -100f), velocity = new Vector3(0f, 0f, 2f) });
            Run(grounded, 60f);

            float groundedSpeed = grounded.Body.linearVelocity.magnitude;
            Assert.That(float.IsNaN(groundedSpeed), Is.False);
            Assert.That(groundedSpeed, Is.LessThan(0.5f * freeSpeed));
            Assert.That(grounded.Grounding.State, Is.EqualTo(GroundingState.Touching).Or.EqualTo(GroundingState.HardGrounding));
            Assert.That(grounded.Grounding.DamagePoints, Is.GreaterThan(0f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BowThruster_TurnsTheRigidbodyAndRestoresFromSave()
        {
            ShipPhysicsController ship = CreateShip(LoadVolgoDon(), float.PositiveInfinity, false);
            Assert.That(ship.HasBowThruster, Is.True);
            ship.SetBowThrusterCommand(1f);
            Run(ship, 60f);

            Assert.That(ship.Body.angularVelocity.y, Is.GreaterThan(0f));
            Assert.That(ship.BowThrusterThrustN, Is.GreaterThan(0f));

            ship.RestoreVoyage(new VoyageSave { bowThrusterCommand = -0.5f, bowThrusterOutput = -0.5f });
            Assert.That(ship.BowThrusterCommand, Is.EqualTo(-0.5f));
            Assert.That(ship.BowThrusterOutput, Is.EqualTo(-0.5f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator InertiaTensor_MapsRollToTheLongitudinalAxis()
        {
            ShipPhysicsController ship = CreateShip(LoadVolgoDon(), float.PositiveInfinity, false);
            VesselParameters p = ship.Parameters;

            Assert.That(ship.Body.inertiaTensor.z, Is.EqualTo(p.RollInertia).Within(1e-3f * p.RollInertia));
            Assert.That(ship.Body.inertiaTensor.x, Is.EqualTo(p.PitchInertia).Within(1e-3f * p.PitchInertia));
            Assert.That(ship.Body.inertiaTensor.z, Is.LessThan(0.1f * ship.Body.inertiaTensor.x),
                "A long narrow hull rolls far more easily than it pitches.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SaveWithoutPerEngineState_RestoresSharedThrottleOnEveryEngine()
        {
            ShipPhysicsController ship = CreateShip(LoadVolgoDon(), float.PositiveInfinity, false);
            ship.RestoreVoyage(new VoyageSave { throttle = 0.6f, actualThrottle = 0.4f });

            for (int i = 0; i < ship.EngineCount; i++)
                Assert.That(ship.EngineCommand(i), Is.EqualTo(0.6f));
            Assert.That(ship.ActualThrottle, Is.EqualTo(0.4f).Within(1e-5f));
            yield return null;
        }

        private ShipPhysicsController CreateShip(VesselData data, float depth, bool withGrounding)
        {
            shipObject = new GameObject("Dynamics test ship");
            shipObject.SetActive(false);
            shipObject.AddComponent<Rigidbody>();
            shipObject.AddComponent<VesselDataLoader>().Configure(new TextAsset(JsonUtility.ToJson(data)));
            if (withGrounding) shipObject.AddComponent<GroundingController>();
            var ship = shipObject.AddComponent<ShipPhysicsController>();
            ship.ManualStepping = true;
            shipObject.SetActive(true);
            ship.SetDepthProvider(_ => depth);
            ship.SetAmbientCurrent(Vector3.zero);
            if (withGrounding) ship.Grounding.Configure(ship, null);
            return ship;
        }

        private static void Run(ShipPhysicsController ship, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Step);
            for (int i = 0; i < steps; i++)
            {
                ship.Simulate(Step);
                UnityEngine.Physics.Simulate(Step);
            }
        }

        private static VesselData LoadVolgoDon()
        {
            string path = Path.Combine(Application.dataPath, "ShipSimulator/Data/Vessels/VolgoDon507B.json");
            return JsonUtility.FromJson<VesselData>(File.ReadAllText(path));
        }
    }
}
