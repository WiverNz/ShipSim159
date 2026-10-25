#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShipSimulator.Tests
{
    public sealed class VolgoDonContactTests
    {
        private GameObject vessel, obstacle, bed;
        private SimulationMode previous;
        [SetUp] public void SetUp() { previous = UnityEngine.Physics.simulationMode; UnityEngine.Physics.simulationMode = SimulationMode.Script; }
        [TearDown] public void TearDown()
        {
            if (vessel != null) Object.DestroyImmediate(vessel);
            if (obstacle != null) Object.DestroyImmediate(obstacle);
            if (bed != null) Object.DestroyImmediate(bed);
            UnityEngine.Physics.simulationMode = previous;
        }
        [Serializable] private sealed class Result
        {
            public string contact;
            public float dt, load, penetrationM, maxSpeedMps, maxRiseM, finalSpeedMps, travelM;
        }
        private ShipPhysicsController Spawn(float load)
        {
            var inactive = new GameObject("Inactive test setup"); inactive.SetActive(false);
            vessel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab"), inactive.transform);
            vessel.SetActive(false); vessel.transform.SetParent(null); Object.DestroyImmediate(inactive);
            vessel.AddComponent<GroundingController>();
            vessel.SetActive(true);
            var ship = vessel.GetComponent<ShipPhysicsController>();
            ship.ManualStepping = true;
            ship.Grounding.Configure(ship, null);
            ship.SetDepthProvider(_ => 20f); ship.SetAmbientCurrent(Vector3.zero); ship.SetWindVelocity(Vector3.zero);
            if (load < 0)
            {
                LoadingState loading = LoadingModel.Initial(ship.Data, 0f);
                for (int i = 2; i < loading.massKg.Length; i++) loading.massKg[i] = ship.Data.loading.compartments[i].capacityKg;
                ship.RestoreVoyage(new VoyageSave { position = Vector3.zero, loading = loading });
            }
            else ship.ConfigureLoading(load);
            for (int i = 0; i < 1500; i++) Step(ship, 0.02f);
            return ship;
        }
        private static void Step(ShipPhysicsController ship, float dt) { ship.Simulate(dt); UnityEngine.Physics.Simulate(dt); }

        [UnityTest]
        public IEnumerator HullContact_DoesNotTunnelLaunchOrSnag(
            [Values("side", "bow", "stern", "angle")] string contact,
            [Values(0.01f, 0.02f, 0.04f)] float dt,
            [Values(-1f, 0f, 0.5f, 1f)] float load)
        {
            var ship = Spawn(load);
            if (contact == "angle") ship.Body.rotation = Quaternion.Euler(0, 5, 0) * ship.Body.rotation;
            UnityEngine.Physics.SyncTransforms();
            Collider[] hull = vessel.GetComponentsInChildren<Collider>();
            Bounds bounds = hull[0].bounds;
            foreach (Collider part in hull) bounds.Encapsulate(part.bounds);
            bool side = contact == "side" || contact == "angle";
            Vector3 normal = side ? Vector3.right : contact == "bow" ? Vector3.forward : Vector3.back;
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.localScale = side ? new Vector3(1, 20, 400) : new Vector3(100, 20, 1);
            obstacle.transform.position = side ? new Vector3(bounds.max.x + 0.7f, 0, 0) :
                new Vector3(0, 0, contact == "bow" ? bounds.max.z + 0.7f : bounds.min.z - 0.7f);
            var wall = obstacle.GetComponent<BoxCollider>();
            ship.Body.linearVelocity = normal * (side ? 0.05f : 0.5f) + (side ? Vector3.forward * 0.5f : Vector3.zero);
            Vector3 start = ship.Body.position;
            var result = new Result { contact = contact, load = load, dt = dt };
            for (int i = 0; i < Mathf.RoundToInt(40 / dt); i++)
            {
                Step(ship, dt);
                result.maxSpeedMps = Mathf.Max(result.maxSpeedMps, ship.Body.linearVelocity.magnitude);
                result.maxRiseM = Mathf.Max(result.maxRiseM, ship.Body.position.y - start.y);
                foreach (Collider part in hull)
                    if (UnityEngine.Physics.ComputePenetration(part, part.transform.position, part.transform.rotation,
                        wall, wall.transform.position, wall.transform.rotation, out _, out float distance))
                        result.penetrationM = Mathf.Max(result.penetrationM, distance);
            }
            result.finalSpeedMps = ship.Body.linearVelocity.magnitude;
            result.travelM = Vector3.Dot(ship.Body.position - start, normal);
            Directory.CreateDirectory("Logs/Part16");
            File.AppendAllText("Logs/Part16/contact.jsonl", JsonUtility.ToJson(result) + "\n");
            Assert.That(result.penetrationM, Is.LessThan(0.15f));
            Assert.That(result.maxSpeedMps, Is.LessThan(1f));
            Assert.That(result.maxRiseM, Is.LessThan(0.3f));
            float nearFace = side ? wall.bounds.min.x : contact == "bow" ? wall.bounds.min.z : -wall.bounds.max.z;
            float extreme = hull.Max(c => side ? c.bounds.max.x : contact == "bow" ? c.bounds.max.z : -c.bounds.min.z);
            Assert.That(extreme, Is.LessThan(nearFace + 0.15f), "No hull section may pass through the quay");
            if (side) Assert.That(ship.Body.position.z - start.z, Is.GreaterThan(1f), "Tangential motion must remain possible");
            yield return null;
        }

        [UnityTest]
        public IEnumerator GroundingUnderAnchorLoad_RemainsBounded([Values(.01f, .02f, .04f)] float dt)
        {
            var ship = Spawn(1f);
            float depth = ship.Parameters.Draft - .05f;
            ship.SetDepthProvider(_ => depth);
            ship.SetWindVelocity(new Vector3(12, 0, 0));
            var tackle = ship.GroundTackle;
            tackle.SetChainCommand(0, ChainCommand.LetGo);
            for (int i = 0; i < Mathf.CeilToInt(120 / dt) && tackle.Phase(0) != AnchorPhase.Bottom; i++) Step(ship, dt);
            Assert.That(tackle.Phase(0), Is.EqualTo(AnchorPhase.Bottom));
            float target = tackle.ChainOutM(0) + 2;
            tackle.SetChainCommand(0, ChainCommand.Veer);
            for (int i = 0; i < Mathf.CeilToInt(30 / dt) && tackle.ChainOutM(0) < target; i++) Step(ship, dt);
            tackle.SetChainCommand(0, ChainCommand.Hold);
            float maxChain = 0, maxGround = 0, maxSpeed = 0, maxRise = 0;
            int simultaneous = 0;
            float startY = ship.Body.position.y;
            for (int i = 0; i < Mathf.CeilToInt(180 / dt); i++)
            {
                Step(ship, dt);
                float chain = tackle.ChainTensionN(0), ground = ship.Grounding.ContactNormalForceN;
                maxChain = Mathf.Max(maxChain, chain); maxGround = Mathf.Max(maxGround, ground);
                if (chain > 100 && ground > 100) simultaneous++;
                maxSpeed = Mathf.Max(maxSpeed, ship.Body.linearVelocity.magnitude);
                maxRise = Mathf.Max(maxRise, ship.Body.position.y - startY);
                Assert.That(float.IsFinite(ground) && float.IsFinite(chain), Is.True);
            }
            Assert.That(simultaneous * dt, Is.GreaterThan(1), "Both contact and chain load must actually occur together");
            Assert.That(maxSpeed, Is.LessThan(2));
            Assert.That(maxRise, Is.LessThan(.5f));
            Directory.CreateDirectory("Logs/Part16");
            File.AppendAllText("Logs/Part16/anchor-contact.csv", FormattableString.Invariant($"{dt},{maxGround},{maxChain},{maxSpeed},{maxRise},{simultaneous * dt}\n"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator LightBankContact_CanBePulledSidewaysIntoDeepWater()
        {
            var ship = Spawn(1f);
            float draft = ship.Parameters.Draft;
            ship.SetDepthProvider(point => Mathf.Clamp(draft + 0.01f * (7f - point.x), 0.5f, 20f));
            float maximumReaction = 0;
            for (int i = 0; i < 2000; i++)
            {
                Step(ship, 0.02f);
                maximumReaction = Mathf.Max(maximumReaction, ship.Grounding.ContactNormalForceN);
            }
            Assert.That(maximumReaction, Is.GreaterThan(0));
            Vector3 start = ship.Body.position;
            for (int i = 0; i < 30000; i++)
            {
                ship.Body.AddForce(Vector3.left * 30000f);
                Step(ship, 0.02f);
                if (ship.Grounding.MinimumClearanceM > 0.05f) break;
            }
            Assert.That(ship.Grounding.ContactNormalForceN, Is.Zero);
            Assert.That(ship.Body.position.x, Is.LessThan(start.x - 1f));
            Assert.That(ship.Body.position.y - start.y, Is.LessThan(0.3f));
            Directory.CreateDirectory("Logs/Part16");
            File.WriteAllText("Logs/Part16/side-recovery.txt", $"External lateral pull 30 kN, sand 1:100, peak initial reaction {maximumReaction} N, travel {ship.Body.position.x-start.x} m, final clearance {ship.Grounding.MinimumClearanceM} m");
            yield return null;
        }

        [UnityTest]
        public IEnumerator GroundingImmediatelyAfterRestore_UsesRestoredBodyPose()
        {
            var ship = Spawn(1f);
            ship.SetDepthProvider(point => point.z > 0 ? 3f : 20f);
            ship.RestoreVoyage(new VoyageSave { position = new Vector3(0,0,200), rotation = Quaternion.identity });
            Step(ship, 0.02f);
            Assert.That(ship.Grounding.ContactNormalForceN, Is.GreaterThan(0));
            ship.RestoreVoyage(new VoyageSave { position = new Vector3(0,0,-200), rotation = Quaternion.identity });
            ship.Grounding.Step(0.02f);
            Assert.That(ship.Grounding.ContactNormalForceN, Is.Zero, "The old transform must not apply a soil force after loading an afloat save");
            yield return null;
        }

        [UnityTest]
        public IEnumerator BathymetrySurface_ExcludesOnlyDuplicateContact()
        {
            var ship = Spawn(1f);
            bed = new GameObject("bathymetry");
            var provider = bed.AddComponent<ScenarioBathymetry>();
            obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.AddComponent<BathymetryContactSurface>();
            ship.Grounding.Configure(ship, provider);
            foreach (Collider part in vessel.GetComponentsInChildren<Collider>())
                Assert.That(UnityEngine.Physics.GetIgnoreCollision(part, obstacle.GetComponent<Collider>()), Is.True);
            Object.DestroyImmediate(obstacle.GetComponent<BathymetryContactSurface>());
            foreach (Collider part in vessel.GetComponentsInChildren<Collider>())
                Assert.That(UnityEngine.Physics.GetIgnoreCollision(part, obstacle.GetComponent<Collider>()), Is.False);
            yield return null;
        }
    }
}
#endif
