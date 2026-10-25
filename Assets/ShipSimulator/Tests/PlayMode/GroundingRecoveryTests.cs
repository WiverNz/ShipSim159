#if UNITY_EDITOR
using UnityEditor;
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShipSimulator.Tests
{
    // Reproducible grounding and refloating cases on sloping bathymetry, stepped by hand. Each case
    // appends its measurements to Logs/Part12/grounding.jsonl so the report can quote them.
    public sealed class GroundingRecoveryTests
    {
        private const string ResultPath = "Logs/Part12/grounding.jsonl";
        private const float DeepM = 8f;
        private SimulationMode previousMode;
        private GameObject shipObject;

        [Serializable]
        private sealed class Result
        {
            public string name, vessel, phase;
            public float dt, approachMps, slope, groundedBowZ, groundedReactionN, groundedHoldingN;
            public float contactSpeedMps, recoveryThrustN, maxReactionN, maxRiseM, verticalRmsMps, minClearanceM;
            public bool freed;
            public float timeToFreeS, finalZ, frictionWorkJ, frictionEnergyGainJ;
        }

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

        // A sandbank rising at the given slope ahead of (or astern of) the ship, as deep as her draft at z0.
        private static Func<Vector3, float> Shoal(float z0, float draft, float slope, float sign) =>
            position => Mathf.Clamp(draft - slope * sign * (position.z - z0), 0.3f, DeepM);

        // Full astern alone works only while friction mu R stays below the astern thrust, about 132 kN for
        // the loaded 507B.
        [UnityTest]
        public IEnumerator LightBowTouch_FullAsternBacksHerOff()
        {
            Result result = Ground("light bow touch, 0.25 m/s onto 1:100", "VolgoDon507B.json", null, 0.02f,
                1f / 100f, 0.25f, 0f);
            Assert.That(result.groundedReactionN, Is.GreaterThan(0f), "She has to touch first.");
            result = Recover(result, "light bow touch, full astern", Astern, 600f);
            Assert.That(result.freed, Is.True, "Full astern has to back her off a light touch.");
            AssertWellBehaved(result);
            yield return null;
        }

        // A partial entry that leaves the friction limit just under the effective astern thrust, so she starts to
        // move slowly and gathers way as the bank lets go. Repeated at three steps for step sensitivity.
        [UnityTest]
        public IEnumerator PartialEntryOnGentleShoal_FullAsternWorksHerOffGradually([Values(0.01f, 0.02f, 0.04f)] float dt)
        {
            Result grounded = Ground($"partial entry, 0.33 m/s onto 1:100, dt {dt}", "VolgoDon507B.json", null, dt,
                1f / 100f, 0.33f, 0f);
            // The effective astern push is (1 - t) of the 132 kN propeller thrust, about 106 kN.
            Assert.That(grounded.groundedHoldingN, Is.GreaterThan(0.75f * 106000f), "A real partial entry, not a touch.");
            Result result = Recover(grounded, $"partial entry, full astern, dt {dt}", Astern, 900f);
            Assert.That(result.freed, Is.True);
            Assert.That(result.timeToFreeS, Is.GreaterThan(30f), "She works off gradually rather than jumping clear.");
            AssertWellBehaved(result);
            yield return null;
        }

        // Further up the bank the friction limit exceeds the astern thrust. Working the engines and nozzles
        // must not add grip or energy, and does not by itself break a long flat-bottom contact free.
        [UnityTest]
        public IEnumerator FirmerEntry_SplitEnginesNeitherGripNorAddEnergy()
        {
            Result grounded = Ground("firmer entry, 0.7 m/s onto 1:100", "VolgoDon507B.json", null, 0.02f,
                1f / 100f, 0.7f, 0.1f);
            Result straight = Recover(grounded, "firmer entry, full astern", Astern, 300f, keepShip: true);
            Assert.That(straight.freed, Is.False, "Here mu R exceeds the astern thrust.");
            Result twisted = Recover(straight, "firmer entry, split engines and nozzles", Twist, 600f);
            Assert.That(twisted.maxReactionN, Is.LessThanOrEqualTo(grounded.groundedReactionN * 1.05f));
            AssertWellBehaved(twisted);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SternOnShoal_FullAheadWorksHerOff()
        {
            Result result = Ground("stern touch, 0.25 m/s astern onto 1:100", "VolgoDon507B.json", null, 0.02f,
                1f / 100f, -0.25f, 0f);
            Assert.That(result.groundedReactionN, Is.GreaterThan(0f));
            result = Recover(result, "stern touch, full ahead", (ship, t) => ship.SetThrottleCommand(1f), 600f);
            Assert.That(result.freed, Is.True);
            AssertWellBehaved(result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BowTrimmedLoading_BacksOffTheShoal()
        {
            VesselData data = Load("VolgoDon507B.json");
            LoadingState fore = LoadingModel.Initial(data, 0f);
            fore.massKg[0] = data.loading.compartments[0].capacityKg;
            fore.massKg[4] = data.loading.compartments[4].capacityKg;
            Result result = Ground("forward hold only, 0.3 m/s onto 1:100", "VolgoDon507B.json", fore, 0.02f,
                1f / 100f, 0.3f, 0f);
            Assert.That(result.groundedReactionN, Is.GreaterThan(0f));
            result = Recover(result, "forward hold only, full astern", Astern, 600f);
            Assert.That(result.freed, Is.True);
            AssertWellBehaved(result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HardGrounding_HoldsUntilDraftIsReduced()
        {
            Result grounded = Ground("hard grounding, full ahead onto 1:25", "VolgoDon507B.json", null, 0.02f,
                1f / 25f, 4.5f, 1f);
            Result stuck = Recover(grounded, "hard grounding, full astern", Astern, 600f, keepShip: true);
            Assert.That(stuck.freed, Is.False, "Driven hard onto the bank, full astern alone must not free her.");
            Result twisted = Recover(stuck, "hard grounding, split engines and nozzles", Twist, 600f, keepShip: true);
            Assert.That(twisted.freed, Is.False, "Working the engines must not free a hard grounding either.");

            // Discharge the forward hold over ten minutes, engines stopped, then try again.
            ShipPhysicsController ship = shipObject.GetComponent<ShipPhysicsController>();
            ship.SetThrottleCommand(0f);
            ship.SetRudderCommand(0f);
            float initial = ship.CompartmentMassKg(0);
            var tracker = new Tracker(ship, ship.Body.position.y);
            for (int i = 1; i <= 60; i++)
            {
                LoadingState lighter = ship.CaptureLoading();
                lighter.massKg[0] = initial * (1f - i / 60f);
                ship.RestoreVoyage(new VoyageSave
                {
                    position = ship.Body.position, rotation = ship.Body.rotation,
                    velocity = ship.Body.linearVelocity, angularVelocity = ship.Body.angularVelocity,
                    engineCommands = new[] { 0f, 0f }, shaftRps = ship.CaptureShaftRps(), loading = lighter
                });
                Run(ship, 10f, grounded.dt, tracker);
            }
            Result refloat = Recover(twisted, "hard grounding, forward hold discharged, full astern", Astern, 600f);
            Assert.That(refloat.freed, Is.True, "With the forward hold out she backs off the bank under her own power.");
            Assert.That(tracker.MaxRise, Is.LessThan(2.5f), "Lightening lifts her gradually, without a jump.");
            AssertWellBehaved(refloat);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OtherCargoShips_BackOffALightBowTouch([Values("VolgoBalt295AR.json", "Volgoneft1577.json")] string file)
        {
            Result result = Ground("light bow touch, 0.3 m/s onto 1:100", file, null, 0.02f, 1f / 100f, 0.3f, 0f);
            Assert.That(result.groundedReactionN, Is.GreaterThan(0f));
            result = Recover(result, "light bow touch, full astern", Astern, 600f);
            Assert.That(result.freed, Is.True);
            AssertWellBehaved(result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SectionContacts_FollowTheRealBottomAndPropellerTips()
        {
            VesselData data = Load("VolgoDon507B.json");
            ShipPhysicsController ship = CreateShip(data);
            ship.Grounding.Step(0.02f);
            VesselParameters p = ship.Parameters;
            Assert.That(ship.Grounding.UsesHullSections, Is.True);
            int tips = 0;
            for (int i = 0; i < ship.Grounding.ContactPointCount; i++)
            {
                Vector3 point = ship.Grounding.ContactPoint(i);
                float height = point.y - p.KeelLocalY;
                Assert.That(height, Is.GreaterThanOrEqualTo(-1e-3f), "No contact point below the keel.");
                if (Mathf.Abs(point.z - data.propeller.longitudinalPositionsM[0]) < 0.01f &&
                    Mathf.Abs(Mathf.Abs(point.x) - 3.6f) < 0.01f) tips++;
                else
                    Assert.That(height, Is.EqualTo(p.Sections.BottomHeightAt(point.z, Mathf.Abs(point.x))).Within(1e-3f));
            }
            Assert.That(tips, Is.EqualTo(2), "Both propeller tip circles touch first under the cut-up stern.");
            yield return null;
        }

        private static void Astern(ShipPhysicsController ship, float t) => ship.SetThrottleCommand(-1f);

        // One engine full astern and the other slow ahead, nozzles hard over to swing the stern, alternating
        // sides every 40 s; the net thrust stays astern.
        private static void Twist(ShipPhysicsController ship, float t)
        {
            float side = Mathf.Repeat(t, 80f) < 40f ? 1f : -1f;
            ship.SetEngineCommand(0, side > 0f ? -1f : 0.3f);
            ship.SetEngineCommand(1, side > 0f ? 0.3f : -1f);
            ship.SetRudderCommand(side);
        }

        private static void AssertWellBehaved(Result result)
        {
            Assert.That(result.maxRiseM, Is.LessThan(0.6f), "The bottom holds her up; it must not throw her.");
            Assert.That(result.verticalRmsMps, Is.LessThan(0.02f), "Resting on the bottom must not jitter.");
            Assert.That(result.frictionEnergyGainJ, Is.LessThan(1e-3f * Mathf.Abs(result.frictionWorkJ) + 1f),
                "Friction must not add energy.");
        }

        private Result Ground(string name, string file, LoadingState loading, float dt, float slope,
            float approachMps, float approachCommand)
        {
            VesselData data = Load(file);
            if (shipObject != null) Object.DestroyImmediate(shipObject);
            ShipPhysicsController ship = CreateShip(data);
            float sign = Mathf.Sign(approachMps);
            float halfLength = 0.5f * data.dimensions.lengthOverallM;
            float z0 = sign * (halfLength + 20f);
            ship.SetDepthProvider(Shoal(z0, data.dimensions.loadedDraftM, slope, sign));
            ship.RestoreVoyage(new VoyageSave { position = Vector3.zero, loading = loading });
            Run(ship, 30f, dt);
            ship.RestoreVoyage(new VoyageSave
            {
                position = ship.Body.position, rotation = ship.Body.rotation,
                velocity = new Vector3(0f, 0f, approachMps), loading = loading,
                engineCommands = new[] { approachCommand, approachCommand },
                shaftRps = new[] { approachCommand * ship.Parameters.RatedRps, approachCommand * ship.Parameters.RatedRps }
            });
            var result = new Result
            {
                name = name, vessel = data.identity.project, phase = "grounding", dt = dt, approachMps = approachMps,
                slope = slope
            };
            var tracker = new Tracker(ship, ship.Body.position.y);
            for (float t = 0f; t < 1500f; t += dt)
            {
                Step(ship, dt, tracker);
                if (t > 20f && ship.Body.linearVelocity.magnitude < 0.02f && ship.Grounding.ContactNormalForceN > 0f) break;
            }
            ship.SetThrottleCommand(0f);
            Run(ship, 20f, dt, tracker);
            result.contactSpeedMps = tracker.ContactSpeed;
            result.groundedBowZ = ship.Body.position.z + sign * halfLength - z0;
            result.groundedReactionN = ship.Grounding.ContactNormalForceN;
            result.groundedHoldingN = ship.Grounding.HoldingForceN;
            Finish(result, ship, tracker);
            result.freed = false;
            Record(result);
            return result;
        }

        private Result Recover(Result grounded, string name, Action<ShipPhysicsController, float> orders, float seconds,
            bool keepShip = false)
        {
            ShipPhysicsController ship = shipObject.GetComponent<ShipPhysicsController>();
            float dt = grounded.dt;
            var tracker = new Tracker(ship, ship.Body.position.y);
            var result = new Result
            {
                name = name, vessel = grounded.vessel, phase = "recovery", dt = dt, approachMps = grounded.approachMps,
                slope = grounded.slope, groundedBowZ = grounded.groundedBowZ, contactSpeedMps = grounded.contactSpeedMps,
                groundedReactionN = ship.Grounding.ContactNormalForceN, groundedHoldingN = ship.Grounding.HoldingForceN,
                timeToFreeS = float.NaN
            };
            Vector3 start = ship.Body.position;
            for (float t = 0f; t < seconds; t += dt)
            {
                orders(ship, t);
                Step(ship, dt, tracker);
                if (result.recoveryThrustN == 0f && t >= 30f)
                    result.recoveryThrustN = ship.Model.PropellerThrustN[0] + ship.Model.PropellerThrustN[1];
                bool afloat = ship.Grounding.ContactNormalForceN <= 0f && ship.Grounding.MinimumClearanceM > 0.05f;
                if (afloat && Vector3.Distance(ship.Body.position, start) > 5f)
                {
                    result.freed = true;
                    result.timeToFreeS = t;
                    break;
                }
            }
            Finish(result, ship, tracker);
            Record(result);
            if (!keepShip) Object.DestroyImmediate(shipObject);
            return result;
        }

        private static void Finish(Result result, ShipPhysicsController ship, Tracker tracker)
        {
            result.finalZ = ship.Body.position.z;
            result.maxReactionN = tracker.MaxReaction;
            result.maxRiseM = tracker.MaxRise;
            result.verticalRmsMps = tracker.VerticalRms;
            result.minClearanceM = tracker.MinClearance;
            result.frictionWorkJ = tracker.FrictionWork;
            result.frictionEnergyGainJ = tracker.FrictionGain;
        }

        private sealed class Tracker
        {
            private readonly ShipPhysicsController ship;
            private readonly float floatY;
            private float sumSquares;
            private int samples;

            public Tracker(ShipPhysicsController ship, float floatY)
            {
                this.ship = ship;
                this.floatY = floatY;
            }

            public float MaxReaction, MaxRise, MinClearance = float.PositiveInfinity, FrictionWork, FrictionGain;
            public float ContactSpeed = float.NaN;
            public float VerticalRms => samples > 0 ? Mathf.Sqrt(sumSquares / samples) : 0f;

            public void Sample(float dt)
            {
                GroundingController grounding = ship.Grounding;
                if (float.IsNaN(ContactSpeed) && grounding.ContactNormalForceN > 0f)
                    ContactSpeed = ship.Body.linearVelocity.magnitude;
                MaxReaction = Mathf.Max(MaxReaction, grounding.ContactNormalForceN);
                MaxRise = Mathf.Max(MaxRise, ship.Body.position.y - floatY);
                MinClearance = Mathf.Min(MinClearance, grounding.MinimumClearanceM);
                FrictionWork += grounding.FrictionPowerW * dt;
                FrictionGain += Mathf.Max(0f, grounding.FrictionPowerW) * dt;
                if (grounding.ContactNormalForceN <= 0f) return;
                float vertical = ship.Body.linearVelocity.y;
                sumSquares += vertical * vertical;
                samples++;
            }
        }

        private static void Step(ShipPhysicsController ship, float dt, Tracker tracker)
        {
            ship.Simulate(dt);
            UnityEngine.Physics.Simulate(dt);
            tracker?.Sample(dt);
        }

        private static void Run(ShipPhysicsController ship, float seconds, float dt, Tracker tracker = null)
        {
            int steps = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < steps; i++) Step(ship, dt, tracker);
        }

        private ShipPhysicsController CreateShip(VesselData data)
        {
            if (data.identity.project == "507B")
            {
                var inactive = new GameObject("Inactive grounding setup"); inactive.SetActive(false);
                shipObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab"), inactive.transform);
                shipObject.SetActive(false); shipObject.transform.SetParent(null); Object.DestroyImmediate(inactive);
                shipObject.AddComponent<GroundingController>(); shipObject.SetActive(true);
                var integrated = shipObject.GetComponent<ShipPhysicsController>();
                integrated.ManualStepping = true;
                integrated.SetAmbientCurrent(Vector3.zero);
                integrated.Grounding.Configure(integrated, null);
                return integrated;
            }
            shipObject = new GameObject("Grounding test ship");
            shipObject.SetActive(false);
            shipObject.AddComponent<Rigidbody>();
            shipObject.AddComponent<VesselDataLoader>().Configure(new TextAsset(JsonUtility.ToJson(data)));
            shipObject.AddComponent<GroundingController>();
            var ship = shipObject.AddComponent<ShipPhysicsController>();
            ship.ManualStepping = true;
            shipObject.SetActive(true);
            ship.SetAmbientCurrent(Vector3.zero);
            ship.Grounding.Configure(ship, null);
            return ship;
        }

        private static void Record(Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.AppendAllText(ResultPath, JsonUtility.ToJson(result) + "\n");
            Debug.Log("GROUNDING_CASE|" + JsonUtility.ToJson(result));
        }

        private static VesselData Load(string file)
        {
            string path = Path.Combine(Application.dataPath, "ShipSimulator/Data/Vessels", file);
            return JsonUtility.FromJson<VesselData>(File.ReadAllText(path));
        }
    }
}

#endif
