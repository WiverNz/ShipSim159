using UnityEngine;

namespace ShipSimulator.UI
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RadarObstacle : MonoBehaviour
    {
        // Use the collision footprint, not the axis-aligned renderer bounds of a rotated hull.
        public void Project(Vector3 observer, float headingDeg, float pixelsPerMeter, float offsetY,
            out Vector2 position, out Vector2 size, out float angleDeg)
        {
            BoxCollider hull = GetComponent<BoxCollider>();
            Vector3 delta = Quaternion.Euler(0f, -headingDeg, 0f) *
                (transform.TransformPoint(hull.center) - observer);
            position = new Vector2(delta.x * pixelsPerMeter, delta.z * pixelsPerMeter + offsetY);
            Vector3 dimensions = Vector3.Scale(hull.size, transform.lossyScale);
            size = new Vector2(Mathf.Abs(dimensions.x), Mathf.Abs(dimensions.z)) * pixelsPerMeter;
            angleDeg = Mathf.DeltaAngle(transform.eulerAngles.y, headingDeg);
        }
    }
}
