using UnityEngine;

namespace ShipSimulator.Physics
{
    // Calm-water resistance R = 0.5 rho S V^2 ((1 + k) C_F + C_A + C_R) with the ITTC-1957 friction line,
    // and Lackenby's (1963) shallow-water speed loss as used in the ITTC trial correction.
    public static class ResistanceModel
    {
        // Lackenby's regression was fitted far from the 507B's h/T; the loss is capped rather than extrapolated.
        public const float MaxShallowSpeedLoss = 0.45f;

        public static float FrictionCoefficient(float reynolds)
        {
            float logarithm = Mathf.Log10(Mathf.Max(reynolds, 1e5f)) - 2f;
            return 0.075f / (logarithm * logarithm);
        }

        public static float MumfordWettedSurface(float lpp, float draft, float displacementVolume)
        {
            return 1.7f * lpp * draft + displacementVolume / draft;
        }

        public static float DeepWaterResistanceN(VesselParameters p, float speed)
        {
            VesselResistance r = p.Data.resistance;
            float reynolds = Mathf.Max(Mathf.Abs(speed), 0.05f) * p.Lpp / r.kinematicViscosityM2PerS;
            float coefficient = r.formFactor * FrictionCoefficient(reynolds) + r.correlationAllowance +
                r.residualResistanceCoefficient;
            return 0.5f * p.Rho * p.WettedSurface * speed * speed * coefficient;
        }

        // Fraction of deep-water speed lost at equal resistance. Evaluated with the shallow-water speed,
        // which slightly underestimates the loss compared with iterating on the deep-water speed.
        public static float LackenbySpeedLoss(float midshipArea, float depth, float speed)
        {
            if (float.IsInfinity(depth) || speed < 1e-3f) return 0f;
            float blockage = 0.1242f * Mathf.Max(0f, midshipArea / (depth * depth) - 0.05f);
            float waveTerm = 1f - Mathf.Sqrt(Mathf.Clamp01((float)System.Math.Tanh(VesselParameters.Gravity * depth / (speed * speed))));
            return Mathf.Clamp(blockage + waveTerm, 0f, MaxShallowSpeedLoss);
        }

        public static float ResistanceN(VesselParameters p, float speed, float depth)
        {
            float loss = LackenbySpeedLoss(p.MidshipArea, depth, speed);
            return DeepWaterResistanceN(p, speed / (1f - loss)) * p.Data.calibration.resistanceMultiplier;
        }
    }
}
