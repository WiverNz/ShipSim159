#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ShipSimulator.Tests
{
    // Reproducible anchoring and mooring cases on the 507B prefab, stepped by hand on flat bathymetry. Each
    // case appends its measurements to Logs/Part14/tackle-cases.jsonl for the Part 14 report.
    public sealed class GroundTackleTests
    {
        private const string PrefabPath = "Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab";
        private const string ResultPath = "Logs/Part14/tackle-cases.jsonl";
        private const int Port = 0, Starboard = 1, Stern = 2;
        private SimulationMode previousMode;
        private readonly List<GameObject> created = new List<GameObject>();

        [Serializable]
        private sealed class Result
        {
            public string name;
            public float dt, depthM, seconds, chainOutM, maxTensionN, finalTensionN, maxStepChangeN, capacityN;
            public float displacementM, finalSpeedMps, anchorTravelM, positiveWorkJ, netWorkJ;
            public string anchorStatus, notes;
            public bool finite = true;
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
            foreach (GameObject item in created) if (item != null) Object.Destroy(item);
            created.Clear();
            UnityEngine.Physics.simulationMode = previousMode;
            yield return null;
        }

        // ---- Anchors ----

        [UnityTest]
        public IEnumerator LetGo_ReachesTheBottomAtEachDepth_OrHangsWithoutOne([Values(5f, 12f, 25f)] float depth)
        {
            ShipPhysicsController ship = Spawn(depth);
            GroundTackle tackle = ship.GroundTackle;
            tackle.SetChainCommand(Port, ChainCommand.LetGo);
            Run(ship, 60);
            Assert.That(tackle.Phase(Port), Is.EqualTo(AnchorPhase.Bottom));
            Assert.That(tackle.Command(Port), Is.EqualTo(ChainCommand.Hold), "The brake is set once the anchor lands.");
            // The crown, below the shackle, touches first; the chain stops there plus the unseat from the pocket.
            float bed = ship.WaterLevel - depth, drop = tackle.ShackleWorld(Port).y - bed;
            Assert.That(drop, Is.GreaterThan(0.5f).And.LessThan(3f), "The shackle rests a crown's height above the bed.");
            float hang = tackle.HawseWorld(Port).y - tackle.ShackleWorld(Port).y;
            Assert.That(tackle.ChainOutM(Port), Is.EqualTo(hang + GroundTackle.UnseatChainM).Within(0.1f),
                "Chain runs out to the bottom and no further.");
            Record(new Result { name = $"let go in {depth} m", dt = 0.02f, depthM = depth, seconds = 60,
                chainOutM = tackle.ChainOutM(Port), anchorStatus = tackle.AnchorStatus(Port) });
            yield return null;
        }

        [UnityTest]
        public IEnumerator LetGo_WithoutBottomData_HangsAtTheChainLimitAndSaysSo()
        {
            ShipPhysicsController ship = Spawn(float.PositiveInfinity);
            GroundTackle tackle = ship.GroundTackle;
            tackle.SetChainCommand(Stern, ChainCommand.LetGo);
            Run(ship, 120);
            Assert.That(tackle.Phase(Stern), Is.EqualTo(AnchorPhase.Hanging));
            Assert.That(tackle.ChainOutM(Stern), Is.EqualTo(tackle.ChainLimitM(Stern)));
            Assert.That(tackle.AnchorStatus(Stern), Is.EqualTo("NO BOTTOM DATA"));
            Assert.That(tackle.NoBottom(Stern), Is.True, "No depth is invented where there is none.");
            yield return null;
        }

        // A short scope in deep water lifts the shank as soon as the ship drifts, and the anchor cannot hold;
        // a long scope lies slack on the bottom and passes nothing until the ship has drifted it straight.
        [UnityTest]
        public IEnumerator ShortScopeBreaksOut_LongScopeLiesSlackFirst()
        {
            ShipPhysicsController ship = Spawn(12f);
            GroundTackle tackle = ship.GroundTackle;
            Anchor(ship, Port, 0);
            Assert.That(tackle.Phase(Port), Is.EqualTo(AnchorPhase.Bottom));
            ship.SetWindVelocity(new Vector3(0, 0, -14));
            Result shortScope = Track(ship, "short scope, 12 m depth, 14 m/s wind astern", 240, Port);
            Assert.That(shortScope.anchorTravelM, Is.GreaterThan(5f), "With the shank lifted the anchor drags.");
            Assert.That(shortScope.finite, Is.True);

            Object.DestroyImmediate(ship.gameObject);
            ship = Spawn(12f);
            tackle = ship.GroundTackle;
            Anchor(ship, Port, 90);
            ship.SetWindVelocity(new Vector3(0, 0, -14));
            Run(ship, 5);
            Assert.That(tackle.AnchorStatus(Port), Is.EqualTo("ON BOTTOM, SLACK"));
            Assert.That(tackle.AnchorHorizontalN(Port), Is.LessThan(2000f), "Slack chain passes next to nothing.");
            Result longScope = Track(ship, "long scope 90 m extra, 12 m depth, 14 m/s wind astern", 900, Port);
            Assert.That(longScope.maxTensionN, Is.GreaterThan(10000f), "Drifting back takes the slack out.");
            Assert.That(longScope.anchorTravelM, Is.LessThan(shortScope.anchorTravelM), "Scope holds better.");
            Assert.That(longScope.finite, Is.True);
            yield return null;
        }

        // Wind and current together load the chain up gradually, without single large steps in tension.
        [UnityTest]
        public IEnumerator SlackToTaut_UnderCurrentAndWind_IsSmoothAndHoldsWithinCapacity()
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            Anchor(ship, Port, 30);
            ship.SetAmbientCurrent(new Vector3(0, 0, -0.5f));
            ship.SetWindVelocity(new Vector3(-6, 0, -8));
            Result result = Track(ship, "hold: 0.5 m/s current, 10 m/s wind, 8 m depth, 40 m chain", 1500, Port);
            Assert.That(result.finite, Is.True);
            Assert.That(result.maxTensionN, Is.GreaterThan(10000f));
            Assert.That(result.anchorStatus, Is.EqualTo("HOLDING"));
            Assert.That(result.finalSpeedMps, Is.LessThan(0.15f), "She lies to her anchor.");
            Assert.That(result.maxStepChangeN, Is.LessThan(0.1f * result.maxTensionN + 5000f), "No snatch in a single step.");
            Assert.That(result.finalTensionN, Is.LessThan(result.capacityN), "Holding, so within what the anchor holds.");
            yield return null;
        }

        // Full astern pulls far beyond what a 1750 kg Hall anchor holds in sand: it drags, at a load bounded by the
        // soil, and the ship is not pinned.
        [UnityTest]
        public IEnumerator Overload_DragsTheAnchorAtABoundedLoad([Values(0.01f, 0.02f, 0.04f)] float dt)
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            Anchor(ship, Port, 35);
            ship.SetThrottleCommand(-1f);
            Result result = Track(ship, $"drag: full astern, 8 m depth, 43 m chain, dt {dt}", 400, Port, dt);
            Assert.That(result.finite, Is.True);
            Assert.That(result.anchorTravelM, Is.GreaterThan(30f), "The anchor drags along the bed.");
            Assert.That(tackle.AnchorStatus(Port), Is.EqualTo("DRAGGING"));
            Assert.That(result.displacementM, Is.GreaterThan(30f), "The vessel is not pinned to a world point.");
            float sand = tackle.Spec.sandHoldingRatio * 1750 * 9.81f * (1 - 1000f / 7850f);
            float friction = tackle.Spec.chainBedFriction * 36.8f * 9.81f * (1 - 1000f / 7850f) * tackle.ChainOutM(Port);
            Assert.That(result.maxTensionN, Is.LessThan(1.3f * (sand + friction) + 10000f), "Tension stays near the holding limit.");
            yield return null;
        }

        // Heaving pulls the ship up to the anchor, breaks it out and stows it; the windlass does that work.
        [UnityTest]
        public IEnumerator Heave_BringsHerUpToTheAnchorBreaksItOutAndStowsIt()
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            Anchor(ship, Port, 30);
            ship.SetWindVelocity(new Vector3(0, 0, -8));
            Run(ship, 600);
            float chain = tackle.ChainOutM(Port);
            tackle.SetChainCommand(Port, ChainCommand.Heave);
            bool aweigh = false;
            var result = new Result { name = "heave up from 40 m, 8 m depth, 8 m/s wind" };
            for (float t = 0; t < 900 && tackle.Phase(Port) != AnchorPhase.Stowed; t += 0.02f)
            {
                Step(ship, 0.02f);
                aweigh |= tackle.Phase(Port) == AnchorPhase.Hanging;
                result.maxTensionN = Mathf.Max(result.maxTensionN, tackle.ChainTensionN(Port));
                result.seconds = t;
            }
            Assert.That(aweigh, Is.True, "The anchor breaks out before it is stowed.");
            Assert.That(tackle.Phase(Port), Is.EqualTo(AnchorPhase.Stowed));
            Assert.That(tackle.ChainOutM(Port), Is.Zero);
            Assert.That(result.seconds, Is.GreaterThan(chain / tackle.Spec.anchors[0].heaveMps * 0.95f),
                "Not faster than the windlass rate.");
            Assert.That(result.maxTensionN, Is.LessThan(tackle.Spec.anchors[0].stallPullN * 1.05f + 5000f),
                "The windlass cannot pull harder than its stall pull.");
            result.chainOutM = chain;
            Record(result);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoAnchors_ShareTheLoad()
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            Anchor(ship, Port, 30);
            tackle.SetChainCommand(Starboard, ChainCommand.LetGo);
            Run(ship, 30);
            tackle.SetChainCommand(Starboard, ChainCommand.Veer);
            Run(ship, 60);
            tackle.SetChainCommand(Starboard, ChainCommand.Hold);
            ship.SetWindVelocity(new Vector3(0, 0, -16));
            Result result = Track(ship, "two bow anchors, 16 m/s wind astern, 8 m depth", 1200, Port);
            Assert.That(tackle.AnchorHorizontalN(Starboard), Is.GreaterThan(0.2f * tackle.AnchorHorizontalN(Port)),
                "Both chains carry load.");
            Assert.That(result.finite, Is.True);
            Record(new Result { name = "two bow anchors, load split",
                notes = $"port {tackle.AnchorHorizontalN(Port):0} N, starboard {tackle.AnchorHorizontalN(Starboard):0} N" });
            yield return null;
        }

        // Starting slack, released from a push with brakes holding and nothing else acting, the chain may store and
        // return energy but never add any: its running work on the ship stays at or below zero.
        [UnityTest]
        public IEnumerator FreeOscillation_AddsNoEnergy()
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            Anchor(ship, Port, 15);
            ship.RestoreVoyage(new VoyageSave
            {
                position = ship.Body.position, rotation = ship.Body.rotation, velocity = new Vector3(0, 0, -0.6f),
                engineCommands = new[] { 0f, 0f }, shaftRps = new[] { 0f, 0f }
            });
            float work = 0, highest = 0, initial = 0.5f * ship.Body.mass * 0.36f;
            for (int i = 0; i < 30000; i++)
            {
                Step(ship, 0.02f);
                work += tackle.PowerW * 0.02f;
                highest = Mathf.Max(highest, work);
            }
            Record(new Result { name = "free oscillation from 0.6 m/s astern, 8 m depth", positiveWorkJ = highest, netWorkJ = work,
                anchorStatus = tackle.AnchorStatus(Port), notes = $"initial kinetic energy {initial:0} J" });
            Assert.That(highest, Is.LessThan(0.01f * initial), "The chain returns no more than it stored.");
            yield return null;
        }

        // ---- Mooring lines ----

        [UnityTest]
        public IEnumerator Lines_PassOnlyWithinReach_HoldAgainstCurrentAndWind_AndCastOff()
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            MooringPoint far = Shore("far", new Vector3(60, 1.7f, 60));
            int bow = StationIndex(tackle, "bow-head-starboard"), stern = StationIndex(tackle, "stern-starboard");
            int fwd = StationIndex(tackle, "fwd-spring-starboard"), aft = StationIndex(tackle, "aft-spring-starboard");
            Assert.That(tackle.PassLine(bow, far), Does.Contain("OUT OF REACH"));
            Assert.That(tackle.LineCount, Is.Zero);
            MooringPoint b1 = Shore("b1", new Vector3(20, 1.7f, 80)), b2 = Shore("b2", new Vector3(20, 1.7f, 5));
            MooringPoint b3 = Shore("b3", new Vector3(20, 1.7f, -10)), b4 = Shore("b4", new Vector3(20, 1.7f, -80));
            Assert.That(tackle.PassLine(bow, b1), Is.Null);
            Assert.That(tackle.PassLine(bow, b1), Does.Contain("ALREADY"));
            Assert.That(tackle.PassLine(fwd, b2), Is.Null);
            Assert.That(tackle.PassLine(aft, b3), Is.Null);
            Assert.That(tackle.PassLine(stern, b4), Is.Null);
            ship.SetAmbientCurrent(new Vector3(-0.3f, 0, 0.3f));
            ship.SetWindVelocity(new Vector3(-10, 0, 0));
            Result result = TrackLines(ship, "four lines, 0.42 m/s current and 10 m/s wind off the berth", 900);
            Assert.That(result.finite, Is.True);
            Assert.That(result.displacementM, Is.LessThan(8f), "Four lines hold her near the berth.");
            Assert.That(result.finalSpeedMps, Is.LessThan(0.05f));
            Assert.That(tackle.LineCount, Is.EqualTo(4));
            for (int k = 0; k < tackle.LineCount; k++) Assert.That(tackle.LineLengthM(k), Is.GreaterThan(0));
            Assert.That(tackle.CastOff(bow), Is.True);
            Assert.That(tackle.LineCount, Is.EqualTo(3));
            Run(ship, 30);
            Assert.That(ship.Body.linearVelocity.magnitude, Is.LessThan(1f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Haul_Slack_AndCastOffUnderLoad()
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            int fwd = StationIndex(tackle, "fwd-spring-starboard"), aft = StationIndex(tackle, "aft-spring-starboard");
            Assert.That(tackle.PassLine(fwd, Shore("s1", new Vector3(30, 1.7f, 32))), Is.Null);
            Assert.That(tackle.PassLine(aft, Shore("s2", new Vector3(30, 1.7f, -36))), Is.Null);
            float start = ship.Body.position.x;
            tackle.SetLineCommand(fwd, LineCommand.Haul);
            tackle.SetLineCommand(aft, LineCommand.Haul);
            // Up to 8 m of the 22 m to the bollards; the winches, not her momentum, set the tension on the way.
            float most = 0;
            for (int i = 0; i < 30000 && ship.Body.position.x - start < 8f; i++)
            {
                Step(ship, 0.02f);
                for (int k = 0; k < tackle.LineCount; k++) most = Mathf.Max(most, tackle.LineTensionN(k));
            }
            Assert.That(ship.Body.position.x - start, Is.GreaterThanOrEqualTo(8f), "Hauling brings her towards the bollards.");
            Assert.That(most, Is.LessThan(tackle.Spec.stallPullFactor * 24525f + 2000f), "No more than the winch stall pull.");
            Record(new Result { name = "haul two breast lines 8 m in", maxTensionN = most, displacementM = ship.Body.position.x - start });
            tackle.SetLineCommand(fwd, LineCommand.Slack);
            tackle.SetLineCommand(aft, LineCommand.Slack);
            ship.SetAmbientCurrent(new Vector3(-0.3f, 0, 0));
            float hauled = tackle.LineLengthM(0);
            Run(ship, 60);
            Assert.That(tackle.LineLengthM(0), Is.EqualTo(hauled + 30f).Within(0.5f), "Slackening pays out at the slack rate.");
            tackle.SetLineCommand(fwd, LineCommand.Hold);
            tackle.SetLineCommand(aft, LineCommand.Hold);
            Run(ship, 300);
            Assert.That(tackle.LineTensionN(0), Is.GreaterThan(1000f), "Under load before casting off.");
            Assert.That(tackle.CastOff(fwd) && tackle.CastOff(aft), Is.True);
            Run(ship, 1);
            Assert.That(tackle.PowerW, Is.Zero, "A line cast off carries nothing.");
            yield return null;
        }

        // Full ahead against a single head line parts it at its breaking load; the line is gone afterwards.
        [UnityTest]
        public IEnumerator Overload_PartsTheLine()
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            int stern = StationIndex(tackle, "stern-starboard");
            Assert.That(tackle.PassLine(stern, Shore("x", new Vector3(12, 1.7f, -95))), Is.Null);
            var events = new List<TackleEvent>();
            tackle.Changed += (change, _) => events.Add(change);
            ship.SetThrottleCommand(1f);
            float most = 0;
            for (int i = 0; i < 20000 && tackle.LineCount > 0; i++)
            {
                Step(ship, 0.02f);
                if (tackle.LineCount > 0)
                {
                    most = Mathf.Max(most, tackle.LineTensionN(0));
                    if (tackle.LineTensionN(0) > tackle.Spec.overloadFraction * tackle.LineBreakingLoadN)
                        Assert.That(tackle.LineStateAt(0), Is.EqualTo(LineState.Overloaded));
                }
            }
            Assert.That(tackle.LineCount, Is.Zero);
            Assert.That(events, Does.Contain(TackleEvent.LineParted));
            Assert.That(tackle.Notice, Does.Contain("PARTED"));
            Assert.That(most, Is.LessThanOrEqualTo(tackle.LineBreakingLoadN * 1.001f));
            Record(new Result { name = "single stern line, full ahead", maxTensionN = most, notes = "line parted" });
            yield return null;
        }

        // Hauled alongside a quay with lines taut, she rests against it quietly: the collider and the lines do not
        // fight each other into jitter.
        [UnityTest]
        public IEnumerator AlongsideAQuay_WithTautLines_IsStable()
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            var quay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            created.Add(quay);
            quay.transform.position = new Vector3(10.4f + 6f, -1f, 0);
            quay.transform.localScale = new Vector3(12f, 6f, 160f);
            string[] stations = { "bow-head-starboard", "fwd-spring-starboard", "aft-spring-starboard", "stern-starboard" };
            float[] z = { 85, 10, -10, -85 };
            for (int i = 0; i < stations.Length; i++)
                Assert.That(tackle.PassLine(StationIndex(tackle, stations[i]), Shore("q" + i, new Vector3(11.4f, 2.4f, z[i]))), Is.Null);
            foreach (string station in stations) tackle.SetLineCommand(StationIndex(tackle, station), LineCommand.Haul);
            Run(ship, 300);
            foreach (string station in stations) tackle.SetLineCommand(StationIndex(tackle, station), LineCommand.Hold);
            float sumSquares = 0, most = 0;
            int samples = 0;
            Vector3 before = ship.Body.position;
            for (int i = 0; i < 15000; i++)
            {
                Step(ship, 0.02f);
                Vector3 v = ship.Body.linearVelocity;
                sumSquares += v.sqrMagnitude;
                samples++;
                for (int k = 0; k < tackle.LineCount; k++) most = Mathf.Max(most, tackle.LineTensionN(k));
            }
            float rms = Mathf.Sqrt(sumSquares / samples);
            Record(new Result { name = "alongside a quay, four lines hauled taut", maxTensionN = most,
                displacementM = Vector3.Distance(before, ship.Body.position), finalSpeedMps = rms, notes = "finalSpeedMps is the RMS speed" });
            Assert.That(ship.Body.position.x, Is.GreaterThan(1f), "Hauled in towards the quay.");
            Assert.That(ship.Body.position.x + 8.35f, Is.LessThan(10.4f + 0.3f), "Not through the quay face.");
            Assert.That(rms, Is.LessThan(0.03f), "Lying alongside, not jittering.");
            Assert.That(tackle.LineCount, Is.EqualTo(4), "No line parted while lying alongside.");
            yield return null;
        }

        // ---- Lifecycle ----

        [UnityTest]
        public IEnumerator SaveLoadMidDrag_ContinuesWithoutAJump_AndMissingBollardIsReported()
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            Anchor(ship, Port, 35);
            int fwd = StationIndex(tackle, "fwd-spring-starboard");
            // Leading aft, so going astern slackens it rather than parting it.
            Assert.That(tackle.PassLine(fwd, Shore("keep", new Vector3(20, 1.7f, 0))), Is.Null);
            ship.SetThrottleCommand(-0.65f);
            Run(ship, 120);
            tackle.SetChainCommand(Starboard, ChainCommand.LetGo);
            Run(ship, 2);
            VoyageSave shipState = ShipState(ship);
            string json = JsonUtility.ToJson(tackle.CaptureState());
            float tension = tackle.ChainTensionN(Port), line = tackle.LineLengthM(0), chain = tackle.ChainOutM(Starboard);
            Run(ship, 1);
            Vector3 continued = ship.Body.position;
            float continuedTension = tackle.ChainTensionN(Port);

            ship.RestoreVoyage(shipState);
            var save = JsonUtility.FromJson<GroundTackleSave>(json);
            Assert.That(tackle.RestoreState(save, null), Is.Null);
            Assert.That(tackle.ChainOutM(Starboard), Is.EqualTo(chain), "No second let-go: the chain is where it was.");
            Assert.That(tackle.Command(Starboard), Is.EqualTo(ChainCommand.LetGo), "The running command continues.");
            Assert.That(tackle.LineLengthM(0), Is.EqualTo(line));
            Run(ship, 1);
            Assert.That(tackle.ChainTensionN(Port), Is.EqualTo(continuedTension).Within(0.05f * tension + 2000f), "No jump in tension.");
            Assert.That(Vector3.Distance(continued, ship.Body.position), Is.LessThan(0.05f));

            save.lines[0].shorePointId = "elsewhere";
            string message = tackle.RestoreState(save, null);
            Assert.That(message, Does.Contain("elsewhere"));
            Assert.That(tackle.LineCount, Is.Zero);
            Assert.That(tackle.Notice, Does.Contain("ELSEWHERE"));

            // A Part 13 save carried only the chain paid out; the anchor hangs and settles on the bottom.
            Assert.That(tackle.RestoreState(new GroundTackleSave(), new[] { 12f, 0f, 0f }), Is.Null);
            Run(ship, 1);
            Assert.That(tackle.Phase(Port), Is.EqualTo(AnchorPhase.Bottom));
            Assert.That(tackle.ChainOutM(Port), Is.EqualTo(12f));
            Assert.That(tackle.Phase(Stern), Is.EqualTo(AnchorPhase.Stowed));

            ship.ResetToStart();
            Assert.That(tackle.LineCount, Is.Zero);
            for (int i = 0; i < tackle.AnchorCount; i++) Assert.That(tackle.Phase(i), Is.EqualTo(AnchorPhase.Stowed));
            Assert.Throws<InvalidDataException>(() => new GroundTackleSave
            {
                present = true, anchors = new[] { new AnchorSave { id = "port", chainOutM = -1 } }
            }.Validate());
            yield return null;
        }

        // With anchors stowed and no lines the vessel moves exactly as one without ground tackle at all.
        [UnityTest]
        public IEnumerator StowedTackle_LeavesTheMotionUnchanged()
        {
            Vector3[] with = Manoeuvre(true), without = Manoeuvre(false);
            for (int i = 0; i < with.Length; i++) Assert.That(with[i], Is.EqualTo(without[i]), "sample " + i);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StepCost_IsSmallAndAllocatesNothing()
        {
            ShipPhysicsController ship = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            Anchor(ship, Port, 30);
            tackle.SetChainCommand(Starboard, ChainCommand.LetGo);
            tackle.SetChainCommand(Stern, ChainCommand.LetGo);
            Run(ship, 30);
            string[] stations = { "bow-breast-starboard", "fwd-spring-starboard", "aft-spring-starboard", "stern-starboard" };
            float[] z = { 60, 20, -20, -60 };
            for (int i = 0; i < stations.Length; i++)
                Assert.That(tackle.PassLine(StationIndex(tackle, stations[i]), Shore("c" + i, new Vector3(18, 1.7f, z[i]))), Is.Null);
            ship.SetWindVelocity(new Vector3(-10, 0, 0));
            Run(ship, 60);
            for (int i = 0; i < 50; i++) tackle.Step(0.02f);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            const int steps = 2000;
            for (int i = 0; i < steps; i++) tackle.Step(0.02f);
            clock.Stop();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            float micro = (float)clock.Elapsed.TotalMilliseconds * 1000f / steps;
            Record(new Result { name = "step cost, three anchors on the bottom and four lines",
                notes = $"{micro:0.0} us per physics step, {bytes} bytes allocated over {steps} steps" });
            Assert.That(bytes, Is.LessThan(1024), "The physics step allocates nothing.");
            Assert.That(micro, Is.LessThan(2000f));
            yield return null;
        }

        // ---- Helpers ----

        private Vector3[] Manoeuvre(bool keepTackle)
        {
            ShipPhysicsController ship = Spawn(8f);
            if (!keepTackle) Object.DestroyImmediate(ship.GetComponent<GroundTackle>());
            ship.SetAmbientCurrent(new Vector3(0.2f, 0, 0.4f));
            ship.SetWindVelocity(new Vector3(-9, 0, 3));
            ship.SetThrottleCommand(0.65f);
            ship.SetRudderCommand(0.5f);
            var samples = new List<Vector3>();
            for (int i = 1; i <= 10000; i++)
            {
                Step(ship, 0.02f);
                if (i % 500 == 0) samples.Add(ship.Body.position);
            }
            return samples.ToArray();
        }

        private ShipPhysicsController Spawn(float depth)
        {
            GameObject vessel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            created.Add(vessel);
            var ship = vessel.GetComponent<ShipPhysicsController>();
            ship.ManualStepping = true;
            ship.SetDepthProvider(_ => depth);
            ship.SetAmbientCurrent(Vector3.zero);
            ship.SetWindVelocity(Vector3.zero);
            ship.RestoreVoyage(new VoyageSave { position = Vector3.zero, engineCommands = new[] { 0f, 0f }, shaftRps = new[] { 0f, 0f } });
            Run(ship, 20);
            Assert.That(ship.GroundTackle, Is.Not.Null);
            return ship;
        }

        // Let go, then veer the given extra chain.
        private static void Anchor(ShipPhysicsController ship, int anchor, float extra)
        {
            GroundTackle tackle = ship.GroundTackle;
            tackle.SetChainCommand(anchor, ChainCommand.LetGo);
            for (int i = 0; i < 6000 && tackle.Phase(anchor) != AnchorPhase.Bottom; i++) Step(ship, 0.02f);
            if (extra <= 0) return;
            float target = tackle.ChainOutM(anchor) + extra;
            tackle.SetChainCommand(anchor, ChainCommand.Veer);
            while (tackle.ChainOutM(anchor) < target) Step(ship, 0.02f);
            tackle.SetChainCommand(anchor, ChainCommand.Hold);
        }

        private Result Track(ShipPhysicsController ship, string name, float seconds, int anchor, float dt = 0.02f)
        {
            GroundTackle tackle = ship.GroundTackle;
            var result = new Result { name = name, dt = dt, seconds = seconds };
            Vector3 start = ship.Body.position, anchorStart = tackle.ShackleWorld(anchor);
            float previous = tackle.AnchorHorizontalN(anchor), work = 0;
            for (float t = 0; t < seconds; t += dt)
            {
                Step(ship, dt);
                float tension = tackle.ChainTensionN(anchor), horizontal = tackle.AnchorHorizontalN(anchor);
                result.finite &= float.IsFinite(tension) && float.IsFinite(ship.Body.position.x) && tackle.ChainOutM(anchor) >= 0;
                result.maxTensionN = Mathf.Max(result.maxTensionN, tension);
                result.maxStepChangeN = Mathf.Max(result.maxStepChangeN, Mathf.Abs(horizontal - previous) * 0.02f / dt);
                previous = horizontal;
                work += tackle.PowerW * dt;
                result.positiveWorkJ = Mathf.Max(result.positiveWorkJ, work);
            }
            result.netWorkJ = work;
            result.finalTensionN = tackle.ChainTensionN(anchor);
            result.capacityN = tackle.HoldingCapacityN(anchor);
            result.chainOutM = tackle.ChainOutM(anchor);
            result.displacementM = Vector3.Distance(start, ship.Body.position);
            result.finalSpeedMps = ship.Body.linearVelocity.magnitude;
            Vector3 travel = tackle.ShackleWorld(anchor) - anchorStart;
            travel.y = 0;
            result.anchorTravelM = travel.magnitude;
            result.anchorStatus = tackle.AnchorStatus(anchor);
            Record(result);
            return result;
        }

        private Result TrackLines(ShipPhysicsController ship, string name, float seconds)
        {
            GroundTackle tackle = ship.GroundTackle;
            var result = new Result { name = name, dt = 0.02f, seconds = seconds };
            Vector3 start = ship.Body.position;
            for (float t = 0; t < seconds; t += 0.02f)
            {
                Step(ship, 0.02f);
                for (int k = 0; k < tackle.LineCount; k++)
                {
                    result.maxTensionN = Mathf.Max(result.maxTensionN, tackle.LineTensionN(k));
                    result.finite &= float.IsFinite(tackle.LineTensionN(k)) && tackle.LineLengthM(k) > 0;
                }
                result.finite &= float.IsFinite(ship.Body.position.x);
            }
            result.displacementM = Vector3.Distance(start, ship.Body.position);
            result.finalSpeedMps = ship.Body.linearVelocity.magnitude;
            result.notes = $"{tackle.LineCount} lines";
            Record(result);
            return result;
        }

        private MooringPoint Shore(string id, Vector3 position)
        {
            var point = new GameObject("Shore " + id);
            created.Add(point);
            point.transform.position = position;
            MooringPoint mooring = point.AddComponent<MooringPoint>();
            mooring.Configure(id, id.ToUpperInvariant(), MooringPointKind.Shore, Vector3.zero);
            return mooring;
        }

        private static int StationIndex(GroundTackle tackle, string id)
        {
            for (int i = 0; i < tackle.StationCount; i++) if (tackle.Station(i).Id == id) return i;
            Assert.Fail("No station " + id);
            return -1;
        }

        private static VoyageSave ShipState(ShipPhysicsController ship) => new VoyageSave
        {
            position = ship.Body.position, rotation = ship.Body.rotation, velocity = ship.Body.linearVelocity,
            angularVelocity = ship.Body.angularVelocity, engineCommands = ship.CaptureEngineCommands(),
            shaftRps = ship.CaptureShaftRps(), rudder = ship.RudderCommand, rudderAngle = ship.RudderAngleDeg
        };

        private static void Step(ShipPhysicsController ship, float dt)
        {
            ship.Simulate(dt);
            UnityEngine.Physics.Simulate(dt);
        }

        private static void Run(ShipPhysicsController ship, float seconds, float dt = 0.02f)
        {
            int steps = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < steps; i++) Step(ship, dt);
        }

        private static void Record(Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.AppendAllText(ResultPath, JsonUtility.ToJson(result) + "\n");
            Debug.Log("TACKLE_CASE|" + JsonUtility.ToJson(result));
        }
    }
}
#endif
