using UnityEngine;

namespace ShipSimulator.Physics
{
    // Quadratic open-water characteristics. Opposing quadrants (crash stop, backing down) extrapolate
    // the polynomials over a clamped advance ratio; this is not four-quadrant series data.
    public static class PropellerModel
    {
        public const float MinAdvanceRatio = -1.5f;
        public const float MaxAdvanceRatio = 1.3f;

        // Returns thrust along +forward. Torque is the load on the shaft in its rotation sense.
        public static float Evaluate(VesselPropeller propeller, float waterDensity, float rps, float advanceSpeed,
            out float torqueNm, out float thrustCoefficient)
        {
            if (Mathf.Abs(rps) < 1e-4f)
            {
                torqueNm = 0f;
                thrustCoefficient = 0f;
                return 0f;
            }
            float diameter = propeller.diameterM;
            float tip = Mathf.Abs(rps) * diameter;
            bool ahead = rps > 0f;
            float advance = Mathf.Clamp((ahead ? advanceSpeed : -advanceSpeed) / tip, MinAdvanceRatio, MaxAdvanceRatio);
            thrustCoefficient = Polynomial(ahead ? propeller.aheadThrustCoefficients : propeller.asternThrustCoefficients, advance);
            float torqueCoefficient = Polynomial(ahead ? propeller.aheadTorqueCoefficients : propeller.asternTorqueCoefficients, advance);
            float sign = ahead ? 1f : -1f;
            torqueNm = sign * waterDensity * tip * tip * diameter * diameter * diameter * torqueCoefficient;
            return sign * waterDensity * tip * tip * diameter * diameter * thrustCoefficient;
        }

        public static float OpenWaterEfficiency(VesselPropeller propeller, float advanceRatio)
        {
            float kq = Polynomial(propeller.aheadTorqueCoefficients, advanceRatio);
            return kq > 0f
                ? advanceRatio * Polynomial(propeller.aheadThrustCoefficients, advanceRatio) / (2f * Mathf.PI * kq)
                : 0f;
        }

        public static float Polynomial(float[] c, float x)
        {
            return c[0] + c[1] * x + c[2] * x * x;
        }
    }
}
