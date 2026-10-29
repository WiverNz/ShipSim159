using UnityEngine;

namespace ShipSimulator.Physics
{
    public static class HullCollisionDetection
    {
        // Predict contacts for the large moving hull, including small stationary marks and rotation.
        public const CollisionDetectionMode Mode = CollisionDetectionMode.ContinuousSpeculative;
    }
}
