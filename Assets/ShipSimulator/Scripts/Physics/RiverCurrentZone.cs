using UnityEngine;

namespace ShipSimulator.Physics
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RiverCurrentZone : MonoBehaviour
    {
        [SerializeField] private Vector3 currentVelocityMps = new Vector3(0f, 0f, 0.5f);
        public Vector3 CurrentVelocityMps => currentVelocityMps;

        public void Configure(Vector3 velocityMps)
        {
            currentVelocityMps = velocityMps;
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            ShipPhysicsController ship = other.GetComponentInParent<ShipPhysicsController>();
            if (ship != null) ship.RegisterCurrentZone(this);
        }

        private void OnTriggerStay(Collider other)
        {
            ShipPhysicsController ship = other.GetComponentInParent<ShipPhysicsController>();
            if (ship != null) ship.RegisterCurrentZone(this);
        }

        private void OnTriggerExit(Collider other)
        {
            ShipPhysicsController ship = other.GetComponentInParent<ShipPhysicsController>();
            if (ship != null) ship.UnregisterCurrentZone(this);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, currentVelocityMps * 10f);
        }
    }
}
