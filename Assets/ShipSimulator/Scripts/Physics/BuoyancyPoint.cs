using UnityEngine;

namespace ShipSimulator.Physics
{
    public sealed class BuoyancyPoint : MonoBehaviour
    {
        public float Weight { get; set; } = 1f;
        public float Submersion01 { get; private set; }

        public void Apply(Rigidbody body, float waterLevel, float forceAtFullSubmersion, float maxDepth, float damping)
        {
            float depth = waterLevel - transform.position.y;
            Submersion01 = Mathf.Clamp01(depth / Mathf.Max(0.01f, maxDepth));
            if (Submersion01 <= 0f) return;

            float verticalVelocity = body.GetPointVelocity(transform.position).y;
            float force = forceAtFullSubmersion * Weight * Submersion01 - verticalVelocity * damping;
            body.AddForceAtPosition(Vector3.up * Mathf.Max(0f, force), transform.position, ForceMode.Force);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.Lerp(Color.yellow, Color.cyan, Submersion01);
            Gizmos.DrawWireSphere(transform.position, 0.45f);
        }
    }
}
