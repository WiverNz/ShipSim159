using System;
using UnityEngine;

namespace ShipSimulator.Physics
{
    [Serializable]
    public sealed class VesselSteeringNozzle
    {
        public bool enabled;
        public float thrustDeflectionFactor;
        public float passiveAreaM2;
        public float passiveNormalSlope;
        public bool estimated;
    }

    public static class SteeringNozzleModel
    {
        // The shaft stays fixed; this is an estimated jet reaction, not an azimuth drive.
        public static Vector2 Thrust(VesselSteeringNozzle nozzle, float effectiveThrust, float angle)
        {
            float deflection = angle * nozzle.thrustDeflectionFactor;
            return effectiveThrust * new Vector2(Mathf.Cos(deflection), -Mathf.Sin(deflection));
        }

        // Passive cross-flow always removes energy from local motion. No propeller slipstream is added twice.
        public static Vector2 Passive(VesselSteeringNozzle nozzle, float rho, Vector2 localVelocity, float angle)
        {
            var normal = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            return -.5f * rho * nozzle.passiveAreaM2 * nozzle.passiveNormalSlope *
                localVelocity.magnitude * Vector2.Dot(localVelocity, normal) * normal;
        }
    }
}
