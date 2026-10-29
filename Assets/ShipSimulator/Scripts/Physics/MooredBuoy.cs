using System.Collections.Generic;
using ShipSimulator.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShipSimulator.Physics
{
    [DisallowMultipleComponent]
    public sealed class MooredBuoy : MonoBehaviour
    {
        private Rigidbody body;
        private BuoyParameters parameters;
        private Vector3 station;
        private Quaternion stationRotation;
        private ScenarioBathymetry water;
        private CurrentFieldProvider current;
        private readonly List<(RiverCurrentZone Zone, BoxCollider Volume)> currentZones = new List<(RiverCurrentZone, BoxCollider)>();
        private PhysicsMaterial material;
        private bool initialized, ownsMaterial;
        public Rigidbody Body => body;
        public Vector3 Station => station;
        public float MooringLoadN { get; private set; }
        public int ContactCount { get; private set; }
        public string StableId => transform.parent == null ? name : transform.parent.name + "/" + name;

        private void Awake() => Initialize();

        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            parameters = BuoyParameters.Load();
            station = transform.position;
            stationRotation = transform.rotation;
            foreach (Collider old in GetComponentsInChildren<Collider>()) old.enabled = false;
            Transform contact = transform.Find("Buoy contact");
            if (contact == null)
            {
                contact = new GameObject("Buoy contact").transform;
                contact.SetParent(transform, false);
                var sphere = contact.gameObject.AddComponent<SphereCollider>();
                sphere.center = Vector3.up * parameters.floatCenterY;
                sphere.radius = parameters.floatRadiusM;
                var tower = contact.gameObject.AddComponent<CapsuleCollider>();
                tower.center = Vector3.up * 2.02f;
                tower.radius = 0.23f;
                tower.height = 2.44f;
            }
            material = Resources.Load<PhysicsMaterial>("BuoyContact");
            ownsMaterial = material == null;
            if (ownsMaterial) material = new PhysicsMaterial("Wet buoy steel")
            {
                dynamicFriction = 0.12f, staticFriction = 0.12f, bounciness = 0,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            foreach (Collider shape in contact.GetComponents<Collider>())
            {
                shape.enabled = true; shape.isTrigger = false; shape.sharedMaterial = material;
            }
            body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.mass = parameters.massKg;
            body.useGravity = true;
            body.isKinematic = false;
            body.centerOfMass = Vector3.down * 0.2f;
            body.linearDamping = 0;
            body.angularDamping = 0;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 12;
            body.solverVelocityIterations = 8;
            body.maxAngularVelocity = 4;
        }

        private void Start()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (water == null) water = root.GetComponentInChildren<ScenarioBathymetry>();
                if (current == null) current = root.GetComponentInChildren<CurrentFieldProvider>();
                foreach (RiverCurrentZone zone in root.GetComponentsInChildren<RiverCurrentZone>())
                    currentZones.Add((zone, zone.GetComponent<BoxCollider>()));
            }
        }

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void Step(float dt)
        {
            if (body == null || dt <= 0) return;
            Vector3 velocity = body.linearVelocity;
            float height = station.y + (water != null ? water.WaterLevelOffsetM : 0);
            float k = parameters.heaveStiffnessNPerM / body.mass;
            float c = parameters.heaveDampingNsPerM / body.mass;
            float vertical = (k * (height - body.position.y) - c * velocity.y) / (1 + c * dt + k * dt * dt);
            body.AddForce(Vector3.up * (Mathf.Clamp(vertical, -9.81f, 39.24f) + 9.81f), ForceMode.Acceleration);
            Vector3 relative = velocity - SampleCurrent();
            relative.y = 0;
            float drag = parameters.dragNPerMps + parameters.dragNPerMpsSquared * relative.magnitude;
            body.AddForce(-relative * (drag / (1 + drag * dt / body.mass)));
            Vector3 offset = body.position - station; offset.y = 0;
            float distance = offset.magnitude;
            MooringLoadN = 0;
            if (distance > parameters.mooringSlackM)
            {
                Vector3 away = offset / distance;
                // Like ship lines, the tether only pulls; its estimated holding load is finite.
                float tension = parameters.mooringStiffnessNPerM * (distance - parameters.mooringSlackM)
                    + parameters.mooringDampingNsPerM * Vector3.Dot(velocity, away);
                MooringLoadN = Mathf.Clamp(tension, 0, parameters.mooringMaxForceN);
                body.AddForce(-away * MooringLoadN);
            }
            Vector3 righting = Vector3.Cross(body.rotation * Vector3.up, Vector3.up) * parameters.rightingAcceleration;
            body.AddTorque((righting - body.angularVelocity * parameters.angularDamping) /
                (1 + parameters.angularDamping * dt), ForceMode.Acceleration);
        }

        private void OnCollisionEnter(Collision collision) => ContactCount++;

        private Vector3 SampleCurrent()
        {
            Vector3 total = Vector3.zero;
            int count = 0;
            foreach (var item in currentZones)
            {
                if (item.Zone == null || !item.Zone.isActiveAndEnabled || !item.Volume.enabled) continue;
                if ((item.Volume.ClosestPoint(body.position) - body.position).sqrMagnitude > 0.000001f) continue;
                total += item.Zone.CurrentVelocityMps;
                count++;
            }
            return count > 0 ? total / count : current != null ? current.Sample(body.position) : Vector3.zero;
        }

        public BuoySave Capture() => new BuoySave { id = StableId, position = body.position,
            rotation = body.rotation.normalized, velocity = body.linearVelocity, angularVelocity = body.angularVelocity };

        public void Restore(BuoySave state, float waterLevel)
        {
            Initialize();
            body.position = state == null ? station + Vector3.up * waterLevel : state.position;
            body.rotation = state == null ? stationRotation : state.rotation;
            body.linearVelocity = state == null ? Vector3.zero : state.velocity;
            body.angularVelocity = state == null ? Vector3.zero : state.angularVelocity;
            body.WakeUp();
        }

        public static BuoySave[] CaptureScene(Scene scene)
        {
            var result = new List<BuoySave>();
            foreach (MooredBuoy buoy in FindObjectsByType<MooredBuoy>())
                if (buoy.gameObject.scene == scene) result.Add(buoy.Capture());
            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result.ToArray();
        }

        public static void RestoreScene(Scene scene, BuoySave[] states, float waterLevel)
        {
            var byId = new Dictionary<string, BuoySave>();
            if (states != null) foreach (BuoySave state in states) byId.Add(state.id, state);
            foreach (MooredBuoy buoy in FindObjectsByType<MooredBuoy>())
            {
                if (buoy.gameObject.scene != scene) continue;
                byId.TryGetValue(buoy.StableId, out BuoySave state);
                buoy.Restore(state, waterLevel);
            }
        }

        private void OnDestroy()
        {
            if (material == null || !ownsMaterial) return;
            if (Application.isPlaying) Destroy(material); else DestroyImmediate(material);
        }
    }
}
