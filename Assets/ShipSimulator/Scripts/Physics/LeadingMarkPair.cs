using UnityEngine;

namespace ShipSimulator.Physics
{
    public sealed class LeadingMarkPair : MonoBehaviour
    {
        [SerializeField] private Transform frontMark;
        [SerializeField] private Transform rearMark;
        [SerializeField] private float routeStartM;
        [SerializeField] private float routeEndM = 500f;

        public void Configure(Transform front, Transform rear, float startM, float endM)
        {
            frontMark = front;
            rearMark = rear;
            routeStartM = startM;
            routeEndM = endM;
        }

        public bool IsActiveAt(float routeDistanceM)
        {
            return routeDistanceM >= routeStartM && routeDistanceM <= routeEndM;
        }

        public float AngularErrorDeg(Vector3 observerPosition)
        {
            if (frontMark == null || rearMark == null) return 0f;
            Vector3 frontDirection = frontMark.position - observerPosition;
            Vector3 rearDirection = rearMark.position - observerPosition;
            frontDirection.y = 0f;
            rearDirection.y = 0f;
            if (frontDirection.sqrMagnitude < 0.01f || rearDirection.sqrMagnitude < 0.01f)
                return 0f;
            return Vector3.SignedAngle(frontDirection, rearDirection, Vector3.up);
        }
    }
}
