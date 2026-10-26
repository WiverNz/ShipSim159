using ShipSimulator.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShipSimulator.Physics
{
    // Two independent bodies, locked at their contact datum. World collisions stay enabled.
    [RequireComponent(typeof(ShipPhysicsController))]
    public sealed class PushUnit : MonoBehaviour
    {
        [SerializeField] private GameObject bargePrefab;
        [SerializeField] private Vector3 tugConnection = new Vector3(0, .65f, 10.9f);
        [SerializeField] private Vector3 bargeConnection = new Vector3(0, .65f, -14.45f);
        private ShipPhysicsController tug;
        private ConfigurableJoint joint;
        private Collider[] tugColliders, bargeColliders;
        private bool[,] priorIgnored;
        private int oldIterations, oldVelocityIterations;
        public PassiveVessel Barge { get; private set; }
        public bool Coupled => joint != null;
        public GameObject BargePrefab => bargePrefab;
        public Vector3 TugConnection => tugConnection;
        public Vector3 BargeConnection => bargeConnection;
        public float ContactError => Barge == null ? 0 : Vector3.Distance(
            transform.TransformPoint(tugConnection), Barge.transform.TransformPoint(bargeConnection));

        public void Configure(GameObject prefab) => bargePrefab = prefab;
        private void Start() => EnsureSpawned();

        public void EnsureSpawned()
        {
            if (Barge != null || bargePrefab == null) return;
            tug = GetComponent<ShipPhysicsController>();
            oldIterations = tug.Body.solverIterations;
            oldVelocityIterations = tug.Body.solverVelocityIterations;
            GameObject instance = Instantiate(bargePrefab, transform.TransformPoint(tugConnection - bargeConnection), transform.rotation);
            instance.name = "MN52_T77";
            SceneManager.MoveGameObjectToScene(instance, gameObject.scene);
            Barge = instance.GetComponent<PassiveVessel>();
            Barge.EnvironmentSource = tug;
            tugColliders = GetComponentsInChildren<Collider>();
            bargeColliders = instance.GetComponentsInChildren<Collider>();
            priorIgnored = new bool[tugColliders.Length, bargeColliders.Length];
            for (int i = 0; i < tugColliders.Length; i++)
                for (int j = 0; j < bargeColliders.Length; j++)
                    priorIgnored[i, j] = UnityEngine.Physics.GetIgnoreCollision(tugColliders[i], bargeColliders[j]);
            Attach();
        }

        public void Attach()
        {
            if (Barge == null || joint != null) return;
            if (ContactError > .25f || Quaternion.Angle(tug.Body.rotation, Barge.Body.rotation) > 3)
                throw new System.InvalidOperationException("Align the two connection points before attaching.");
            tug.Body.solverIterations = 24; tug.Body.solverVelocityIterations = 12;
            IgnorePair(true);
            joint = gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = Barge.Body;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = tugConnection; joint.connectedAnchor = bargeConnection;
            joint.xMotion = joint.yMotion = joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = joint.angularYMotion = joint.angularZMotion = ConfigurableJointMotion.Locked;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = .03f; joint.projectionAngle = 1;
            joint.enableCollision = false;
            joint.enablePreprocessing = true;
        }

        public void Detach()
        {
            if (joint != null) { DestroyImmediate(joint); joint = null; }
            IgnorePair(false);
            if (tug != null && tug.Body != null)
            {
                tug.Body.solverIterations = oldIterations;
                tug.Body.solverVelocityIterations = oldVelocityIterations;
            }
        }

        private void IgnorePair(bool coupled)
        {
            if (tugColliders == null || bargeColliders == null) return;
            for (int i = 0; i < tugColliders.Length; i++)
                for (int j = 0; j < bargeColliders.Length; j++)
                    if (tugColliders[i] != null && bargeColliders[j] != null)
                        UnityEngine.Physics.IgnoreCollision(tugColliders[i], bargeColliders[j], coupled || priorIgnored[i, j]);
        }

        public PushUnitSave Capture()
        {
            EnsureSpawned();
            return Barge == null ? null : new PushUnitSave
            {
                present = true, bargeId = "mn52-t77", coupled = Coupled, position = Barge.Body.position,
                rotation = Barge.Body.rotation, velocity = Barge.Body.linearVelocity,
                angularVelocity = Barge.Body.angularVelocity
            };
        }

        public void Restore(PushUnitSave save)
        {
            if (save != null && !save.present) save = null;
            EnsureSpawned();
            if (Barge == null) return;
            Detach();
            if (save != null)
            {
                save.Validate();
                Barge.Body.position = save.position; Barge.Body.rotation = save.rotation;
                Barge.Body.linearVelocity = save.velocity; Barge.Body.angularVelocity = save.angularVelocity;
            }
            else
            {
                Barge.Body.position = tug.Body.position + tug.Body.rotation * (tugConnection - bargeConnection);
                Barge.Body.rotation = tug.Body.rotation;
                Barge.Body.linearVelocity = tug.Body.GetPointVelocity(Barge.Body.position);
                Barge.Body.angularVelocity = tug.Body.angularVelocity;
            }
            UnityEngine.Physics.SyncTransforms();
            if (save == null || save.coupled) Attach();
        }

        private void OnDestroy()
        {
            // Unity is already destroying the root's joint; do not destroy it twice.
            joint = null;
            IgnorePair(false);
            if (Barge != null) Destroy(Barge.gameObject);
        }
    }
}
