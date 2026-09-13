using UnityEngine;

namespace ShipSimulator.Physics
{
    // Blendermann (1994) wind loads as implemented in Fossen's MSS blendermann94.m. The relative wind
    // angle follows Fossen: gamma = -atan2(v_rw, u_rw) with (u_rw, v_rw) the ship velocity relative to
    // the air, so 0 is a head wind and +90 degrees is wind from port. The coefficient set is written
    // for gamma in [0, pi]; negative angles are mirrored.
    public static class WindLoadModel
    {
        // airVelocityBody: air velocity relative to the ship, x forward and y starboard.
        public static void Evaluate(VesselParameters p, Vector2 airVelocityBody, out float x, out float y, out float n)
        {
            float speedSquared = airVelocityBody.sqrMagnitude;
            if (speedSquared < 1e-6f)
            {
                x = y = n = 0f;
                return;
            }
            Coefficients(p, airVelocityBody, out float cx, out float cy, out float cn);
            float dynamicPressure = 0.5f * p.Data.windage.airDensityKgM3 * speedSquared;
            x = dynamicPressure * p.WindFrontalArea * cx;
            y = dynamicPressure * p.WindLateralArea * cy;
            n = dynamicPressure * p.WindLateralArea * p.Loa * cn;
        }

        public static void Coefficients(VesselParameters p, Vector2 airVelocityBody, out float cx, out float cy, out float cn)
        {
            VesselWindage windage = p.Data.windage;
            float gamma = -Mathf.Atan2(-airVelocityBody.y, -airVelocityBody.x);
            float angle = Mathf.Abs(gamma);
            float side = gamma >= 0f ? 1f : -1f;
            float longitudinal = angle <= Mathf.PI * 0.5f ? windage.longitudinalDragBow : windage.longitudinalDragStern;
            float lateralFromLongitudinal = longitudinal * p.WindFrontalArea / p.WindLateralArea;
            float doubleSine = Mathf.Sin(2f * angle);
            float denominator = 1f - 0.5f * windage.crossForceParameter *
                (1f - lateralFromLongitudinal / windage.transverseDragCoefficient) * doubleSine * doubleSine;
            cx = -longitudinal * Mathf.Cos(angle) / denominator;
            float lateral = windage.transverseDragCoefficient * Mathf.Sin(angle) / denominator;
            cy = side * lateral;
            cn = side * (p.WindCentroidX / p.Loa - 0.18f * (angle - Mathf.PI * 0.5f)) * lateral;
        }
    }
}
