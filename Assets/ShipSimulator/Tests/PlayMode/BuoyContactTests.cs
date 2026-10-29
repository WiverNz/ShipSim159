using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class BuoyContactTests
    {
        private Scene scene;
        private PhysicsScene physics;
        private GameObject hull, marker;
        private float oldScale;

        [SetUp] public void Setup()
        {
            oldScale = Time.timeScale;
            scene = SceneManager.CreateScene("Isolated buoy contacts", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            physics = scene.GetPhysicsScene();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            Time.timeScale = oldScale;
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private MooredBuoy Mark(Vector3 position)
        {
            marker = new GameObject("Test Buoy");
            SceneManager.MoveGameObjectToScene(marker, scene);
            marker.transform.position = position;
            var buoy = marker.AddComponent<MooredBuoy>();
            buoy.enabled = false;
            return buoy;
        }

        private Rigidbody Hull(VesselCatalogue.Entry entry)
        {
            hull = new GameObject(entry.id);
            SceneManager.MoveGameObjectToScene(hull, scene);
            foreach (Collider source in entry.prefab.GetComponentsInChildren<Collider>())
            {
                if (!source.enabled || source.isTrigger) continue;
                var part = new GameObject(source.name);
                part.transform.SetParent(hull.transform, false);
                part.transform.localPosition = entry.prefab.transform.InverseTransformPoint(source.transform.position);
                part.transform.localRotation = Quaternion.Inverse(entry.prefab.transform.rotation) * source.transform.rotation;
                part.transform.localScale = source.transform.lossyScale;
                Collider shape;
                if (source is BoxCollider box)
                {
                    var copy = part.AddComponent<BoxCollider>(); copy.center = box.center; copy.size = box.size; shape = copy;
                }
                else if (source is MeshCollider mesh)
                {
                    var copy = part.AddComponent<MeshCollider>(); copy.sharedMesh = mesh.sharedMesh; copy.convex = mesh.convex; shape = copy;
                }
                else { Assert.Fail("Uncovered hull collider: " + source.GetType()); return null; }
                shape.sharedMaterial = source.sharedMaterial;
            }
            Rigidbody body = hull.AddComponent<Rigidbody>();
            var parameters = VesselParameters.Create(entry.LoadData());
            body.mass = parameters.Mass;
            body.centerOfMass = parameters.CentreOfMassLocalM;
            body.inertiaTensor = new Vector3(parameters.PitchInertia, parameters.YawInertiaAtG, parameters.RollInertia);
            body.inertiaTensorRotation = Quaternion.identity;
            body.useGravity = false;
            body.collisionDetectionMode = HullCollisionDetection.Mode;
            body.solverIterations = 12; body.solverVelocityIterations = 4;
            return body;
        }

        [UnityTest]
        public IEnumerator CatalogueHulls_YieldBuoysWithoutTunnellingOrBlocking(
            [Values(0.01f, 0.02f, 0.04f)] float dt,
            [Values(1f, 2f, 4f)] float timeScale)
        {
            Time.timeScale = timeScale;
            Directory.CreateDirectory("Logs/BuoyPart5");
            string report = System.FormattableString.Invariant($"Logs/BuoyPart5/contacts-{dt}-{timeScale}.csv");
            File.WriteAllText(report, "vessel,approach,speed,dt,timeScale,contacts,penetration,peakBuoySpeed,displacement\n");
            var entries = VesselCatalogue.Load().Entries.ToList();
            var push = entries.First(e => e.prefab.GetComponent<PushUnit>() != null).prefab.GetComponent<PushUnit>();
            entries.Add(new VesselCatalogue.Entry { id = "mn52-t77", prefab = push.BargePrefab });
            foreach (var entry in entries)
            foreach (float speed in new[] { 0.5f, entry.LoadData().controlLimits.maxLoadedSpeedMps * 0.75f,
                entry.LoadData().controlLimits.maxLoadedSpeedMps })
            foreach (string approach in new[] { "bow", "stern", "side", "glancing" })
            {
                Rigidbody ship = Hull(entry);
                var parts = hull.GetComponentsInChildren<Collider>();
                UnityEngine.Physics.SyncTransforms();
                Bounds bounds = parts[0].bounds;
                foreach (Collider part in parts) bounds.Encapsulate(part.bounds);
                Vector3 direction = approach == "stern" ? Vector3.back : approach == "side" ? Vector3.right : Vector3.forward;
                Vector3 station = approach == "stern" ? new Vector3(0, 0, bounds.min.z - 2) : approach == "side" ?
                    new Vector3(bounds.max.x + 2, 0, bounds.center.z) : new Vector3(0, 0, bounds.max.z + 2);
                if (approach == "glancing") { station = new Vector3(bounds.max.x + 0.6f, 0, bounds.center.z); direction = new Vector3(0.3f, 0, 1).normalized; }
                // Use each hull's actual water-contact band, independent of its visual import origin.
                station.y = Mathf.Clamp(0, bounds.min.y + 0.1f, bounds.max.y - 0.1f);
                MooredBuoy buoy = Mark(station);
                ship.linearVelocity = direction * speed;
                float penetration = 0, peak = 0, displacement = 0;
                int steps = Mathf.CeilToInt((4 / speed + 1.5f) / dt);
                for (int i = 0; i < steps; i++)
                {
                    buoy.Step(dt); physics.Simulate(dt);
                    peak = Mathf.Max(peak, buoy.Body.linearVelocity.magnitude);
                    displacement = Mathf.Max(displacement, Vector3.Distance(buoy.Body.position, station));
                    foreach (Collider part in parts)
                    foreach (Collider contact in marker.GetComponentsInChildren<Collider>())
                        if (UnityEngine.Physics.ComputePenetration(part, PosePosition(part), PoseRotation(part),
                            contact, PosePosition(contact), PoseRotation(contact), out _, out float depth))
                            penetration = Mathf.Max(penetration, depth);
                }
                string label = $"{entry.id},{approach},{speed},{dt},{timeScale}";
                File.AppendAllText(report, $"{label},{buoy.ContactCount},{penetration},{peak},{displacement}\n");
                Assert.That(buoy.ContactCount, Is.GreaterThan(0), label);
                Assert.That(displacement, Is.GreaterThan(0.05f), label);
                Assert.That(penetration, Is.LessThan(0.35f), label);
                Assert.That(peak, Is.LessThan(speed * 2 + 5), label);
                Assert.That(Vector3.Dot(ship.linearVelocity, direction), Is.GreaterThan(speed * 0.1f), label);
                ship.linearVelocity = -direction * 2;
                for (int i = 0; i < 100; i++) { buoy.Step(dt); physics.Simulate(dt); }
                Assert.That(Vector3.Dot(ship.linearVelocity, direction), Is.LessThan(-1f), "Reverse release: " + label);
                Object.DestroyImmediate(hull); Object.DestroyImmediate(marker);
            }
            yield return null;
        }

        // Interpolated render transforms lag behind the pose being tested by the isolated physics scene.
        private static Vector3 PosePosition(Collider shape)
        {
            Rigidbody body = shape.attachedRigidbody;
            return body.position + body.rotation * body.transform.InverseTransformPoint(shape.transform.position);
        }

        private static Quaternion PoseRotation(Collider shape)
        {
            Rigidbody body = shape.attachedRigidbody;
            return body.rotation * Quaternion.Inverse(body.transform.rotation) * shape.transform.rotation;
        }

        [UnityTest]
        public IEnumerator CatalogueHulls_StopAtSolidWallsAndCanReverseAway()
        {
            foreach (var entry in VesselCatalogue.Load().Entries)
            foreach (float dt in new[] { 0.01f, 0.02f, 0.04f })
            foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.right })
            {
                Rigidbody ship = Hull(entry);
                Collider[] parts = hull.GetComponentsInChildren<Collider>();
                UnityEngine.Physics.SyncTransforms();
                Bounds bounds = parts[0].bounds;
                foreach (Collider part in parts) bounds.Encapsulate(part.bounds);
                bool side = direction == Vector3.right;
                float face = side ? bounds.max.x : direction.z > 0 ? bounds.max.z : -bounds.min.z;
                marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                SceneManager.MoveGameObjectToScene(marker, scene);
                marker.transform.localScale = side ? new Vector3(0.5f, 100, 500) : new Vector3(500, 100, 0.5f);
                marker.transform.position = direction * (face + 2.25f);
                var wall = marker.GetComponent<Collider>();
                float speed = entry.LoadData().controlLimits.maxLoadedSpeedMps;
                ship.linearVelocity = direction * speed;
                UnityEngine.Physics.SyncTransforms();
                float penetration = 0;
                for (int i = 0; i < Mathf.CeilToInt(5 / dt); i++)
                {
                    physics.Simulate(dt);
                    foreach (Collider part in parts)
                        if (UnityEngine.Physics.ComputePenetration(part, PosePosition(part), PoseRotation(part),
                            wall, wall.transform.position, wall.transform.rotation, out _, out float depth))
                            penetration = Mathf.Max(penetration, depth);
                }
                string label = $"{entry.id}, {direction}, dt={dt}";
                Assert.That(penetration, Is.LessThan(0.16f), label);
                Assert.That(Vector3.Dot(ship.position, direction), Is.LessThan(2.16f), label);
                Assert.That(Vector3.Dot(ship.linearVelocity, direction), Is.LessThan(0.3f), label);
                ship.linearVelocity = -direction;
                Vector3 start = ship.position;
                for (int i = 0; i < Mathf.CeilToInt(3 / dt); i++) physics.Simulate(dt);
                Assert.That(Vector3.Dot(ship.position - start, direction), Is.LessThan(-2), label);
                Object.DestroyImmediate(hull); Object.DestroyImmediate(marker);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SustainedThrust_CanPushPastTheMarkAndBackAway()
        {
            hull = new GameObject("Powered contact probe");
            SceneManager.MoveGameObjectToScene(hull, scene);
            hull.transform.position = new Vector3(0, 0, -3);
            hull.AddComponent<BoxCollider>().size = new Vector3(2, 2, 2);
            var ship = hull.AddComponent<Rigidbody>();
            ship.mass = 20000; ship.useGravity = false;
            ship.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            ship.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            MooredBuoy buoy = Mark(Vector3.zero);
            for (int i = 0; i < 1000; i++)
            {
                ship.AddForce(Vector3.forward * 8000); buoy.Step(0.02f); physics.Simulate(0.02f);
            }
            Assert.That(buoy.ContactCount, Is.GreaterThan(0));
            Assert.That(ship.position.z, Is.GreaterThan(3), "Finite mooring load must not become a fixed constraint.");
            float turningPosition = ship.position.z;
            for (int i = 0; i < 2000; i++)
            {
                ship.AddForce(Vector3.back * 8000); buoy.Step(0.02f); physics.Simulate(0.02f);
            }
            Assert.That(ship.position.z, Is.LessThan(turningPosition - 3));
            Assert.That(ship.linearVelocity.z, Is.LessThan(-1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Environment_FollowsWaterLevelAndLocalCurrent()
        {
            var environment = new GameObject("Buoy environment");
            SceneManager.MoveGameObjectToScene(environment, scene);
            var bathymetry = environment.AddComponent<ScenarioBathymetry>();
            bathymetry.SetWaterLevelOffset(1);
            var current = environment.AddComponent<CurrentFieldProvider>();
            current.Configure(Vector3.right, null);
            environment.AddComponent<BoxCollider>().size = Vector3.one * 100;
            var zone = environment.AddComponent<RiverCurrentZone>();
            zone.Configure(Vector3.forward * 2);
            zone.enabled = false;
            MooredBuoy buoy = Mark(Vector3.zero);
            buoy.enabled = true;
            yield return null;
            buoy.enabled = false;
            for (int i = 0; i < 2000; i++) { buoy.Step(0.02f); physics.Simulate(0.02f); }
            Assert.That(buoy.Body.position.y, Is.EqualTo(1).Within(0.03f));
            Assert.That(buoy.Body.position.x, Is.InRange(0.6f, 2f));
            Assert.That(buoy.MooringLoadN, Is.InRange(400, 900));
            zone.enabled = true;
            for (int i = 0; i < 2000; i++) { buoy.Step(0.02f); physics.Simulate(0.02f); }
            Assert.That(buoy.Body.position.z, Is.GreaterThan(1.5f), "Legacy river zone must override the field.");
            Assert.That(Mathf.Abs(buoy.Body.position.x), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator Buoy_ReturnsToStationAndRestoresSavedMotion()
        {
            MooredBuoy buoy = Mark(Vector3.zero);
            buoy.Body.position = new Vector3(8, -0.4f, 3);
            buoy.Body.rotation = Quaternion.Euler(25, 15, 20);
            buoy.Body.linearVelocity = new Vector3(2, 0, 1);
            var save = JsonUtility.FromJson<BuoySave>(JsonUtility.ToJson(buoy.Capture()));
            BuoySave.Validate(new[] { save });
            for (int i = 0; i < 3000; i++) { buoy.Step(0.02f); physics.Simulate(0.02f); }
            Assert.That(new Vector2(buoy.Body.position.x, buoy.Body.position.z).magnitude, Is.LessThan(0.7f));
            Assert.That(Mathf.Abs(buoy.Body.position.y), Is.LessThan(0.05f));
            Assert.That(Vector3.Angle(buoy.Body.rotation * Vector3.up, Vector3.up), Is.LessThan(1));
            MooredBuoy.RestoreScene(scene, new[] { save }, 0);
            Assert.That(Vector3.Distance(buoy.Body.position, save.position), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(buoy.Body.linearVelocity, save.velocity), Is.LessThan(0.0001f));
            MooredBuoy.RestoreScene(scene, null, 1.2f);
            Assert.That(buoy.Body.position, Is.EqualTo(Vector3.up * 1.2f));
            Assert.That(buoy.Body.linearVelocity, Is.EqualTo(Vector3.zero));
            yield return null;
        }
    }
}
