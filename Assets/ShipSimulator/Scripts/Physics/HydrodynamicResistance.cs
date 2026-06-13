using UnityEngine;

namespace ShipSimulator.Physics
{
    public sealed class HydrodynamicResistance : MonoBehaviour
    {
        public Vector3 RelativeWaterVelocity { get; private set; }

        public void Apply(Rigidbody body, VesselData data, Vector3 currentVelocity,
            float resistanceMultiplier = 1f)
        {
            RelativeWaterVelocity = body.linearVelocity - currentVelocity;
            Vector3 local = transform.InverseTransformDirection(RelativeWaterVelocity);
            float multiplier = data.calibration.resistanceMultiplier *
                Mathf.Max(0.1f, resistanceMultiplier);
            float surge = Oppose(local.z, data.hydrodynamics.surgeLinearNPerMps, data.hydrodynamics.surgeQuadraticNPerMps2);
            float sway = Oppose(local.x, data.hydrodynamics.swayLinearNPerMps, data.hydrodynamics.swayQuadraticNPerMps2);
            body.AddForce(transform.TransformDirection(new Vector3(sway, 0f, surge)) * multiplier);

            float yaw = body.angularVelocity.y;
            float yawDrag = Oppose(yaw, data.hydrodynamics.yawLinearNmPerRadps, data.hydrodynamics.yawQuadraticNmPerRadps2);
            body.AddTorque(Vector3.up * yawDrag * multiplier);
        }

        private static float Oppose(float value, float linear, float quadratic)
        {
            return -value * linear - Mathf.Sign(value) * value * value * quadratic;
        }
    }
}
