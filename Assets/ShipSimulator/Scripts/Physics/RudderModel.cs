using UnityEngine;

namespace ShipSimulator.Physics
{
    public struct RudderForces
    {
        public float X;
        public float Y;
        public float N;
        public float NormalForceN;
        public float InflowSpeedMps;
        public float AttackAngleRad;
    }

    // MMG rudder model (Yasukawa and Yoshimura 2015) for one rudder behind one propeller, with a
    // smooth post-stall reduction of the normal force coefficient.
    public static class RudderModel
    {
        private const float StallBlendRad = 6f * Mathf.Deg2Rad;

        // Fujii's lift gradient for the rudder aspect ratio.
        public static float FujiiNormalForceSlope(float aspectRatio)
        {
            return 6.13f * aspectRatio / (aspectRatio + 2.25f);
        }

        public static float NormalCoefficient(float attackAngleRad, float slope, float stallAngleRad, float postStallCoefficient)
        {
            float sine = Mathf.Sin(attackAngleRad);
            float folded = Mathf.Asin(Mathf.Min(1f, Mathf.Abs(sine)));
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(stallAngleRad, stallAngleRad + StallBlendRad, folded));
            return sine * Mathf.Lerp(slope, postStallCoefficient, blend);
        }

        // u, v, r are water-relative at midship; advanceSpeed is the propeller inflow u (1 - w_P).
        public static RudderForces Evaluate(VesselParameters p, float lateralPosition, float u, float v, float r,
            float deltaRad, float propellerRps, float propellerThrustCoefficient, float advanceSpeed)
        {
            VesselRudder rudder = p.Data.rudder;
            float diameter = p.Data.propeller.diameterM;
            float eta = Mathf.Min(1f, diameter / rudder.spanM);

            // Written without 1/J so the slipstream stays finite at zero ship speed.
            float jetBase = advanceSpeed;
            if (propellerRps > 0f && propellerThrustCoefficient > 0f)
            {
                float tip = propellerRps * diameter;
                float jet = Mathf.Sqrt(advanceSpeed * advanceSpeed + 8f * propellerThrustCoefficient * tip * tip / Mathf.PI);
                jetBase = advanceSpeed + rudder.slipstreamFactor * (jet - advanceSpeed);
            }
            float magnitude = Mathf.Sqrt(eta * jetBase * jetBase + (1f - eta) * advanceSpeed * advanceSpeed);
            float uR = rudder.wakeRatio * (jetBase >= 0f ? magnitude : -magnitude);
            float vR = -rudder.flowStraightening * (v + rudder.flowStraighteningLeverPrime * p.Lpp * r);
            float attack = Mathf.DeltaAngle(0f, (deltaRad - Mathf.Atan2(vR, uR)) * Mathf.Rad2Deg) * Mathf.Deg2Rad;

            float slope = rudder.normalForceSlope > 0f
                ? rudder.normalForceSlope
                : FujiiNormalForceSlope(rudder.spanM * rudder.spanM / rudder.areaPerRudderM2);
            float coefficient = NormalCoefficient(attack, slope, rudder.stallAngleDeg * Mathf.Deg2Rad,
                rudder.postStallNormalCoefficient);
            float normal = 0.5f * p.Rho * rudder.areaPerRudderM2 * (uR * uR + vR * vR) * coefficient *
                p.Data.calibration.rudderMultiplier;

            float x = -(1f - rudder.steeringResistanceDeduction) * normal * Mathf.Sin(deltaRad);
            float y = -(1f + rudder.hullInteractionFactor) * normal * Mathf.Cos(deltaRad);
            return new RudderForces
            {
                X = x,
                Y = y,
                N = -(rudder.longitudinalPositionM + rudder.hullInteractionFactor * rudder.hullInteractionPositionPrime * p.Lpp) *
                    normal * Mathf.Cos(deltaRad) - lateralPosition * x,
                NormalForceN = normal,
                InflowSpeedMps = Mathf.Sqrt(uR * uR + vR * vR),
                AttackAngleRad = attack
            };
        }
    }
}
