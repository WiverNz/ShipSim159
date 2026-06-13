using UnityEngine;

namespace ShipSimulator.Physics
{
    public sealed class RudderController : MonoBehaviour
    {
        public float AngleDeg { get; private set; }
        public Vector3 LastForce { get; private set; }

        public void Step(Rigidbody body, VesselData data, float command, Vector3 waterVelocity, float dt)
        {
            float target = command * data.rudder.maxAngleDeg;
            AngleDeg = Mathf.MoveTowards(AngleDeg, target, data.rudder.rateDegPerSecond * dt);
            Vector3 localWaterVelocity = transform.InverseTransformDirection(waterVelocity);
            float axialSpeed = Mathf.Abs(localWaterVelocity.z);
            float angleRad = AngleDeg * Mathf.Deg2Rad;
            // F = 0.5 rho V^2 A Cl, with a linear lift slope at small rudder angles.
            float lift = 0.5f * data.hydrodynamics.waterDensityKgM3 * axialSpeed * axialSpeed
                * data.rudder.areaPerRudderM2 * data.rudder.count
                * data.rudder.liftCoefficientSlopePerRad * angleRad * data.calibration.rudderMultiplier;
            LastForce = transform.right * lift;
            body.AddForceAtPosition(LastForce, transform.position, ForceMode.Force);
            transform.localRotation = Quaternion.Euler(0f, AngleDeg, 0f);
        }
    }
}

