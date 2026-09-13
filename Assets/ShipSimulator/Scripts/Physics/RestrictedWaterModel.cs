using UnityEngine;

namespace ShipSimulator.Physics
{
    public static class RestrictedWaterModel
    {
        // The regressions below are not defined as h/T approaches 1; deeper sections are clamped here.
        public const float MinimumDepthToDraft = 1.1f;
        private const float MaxDepthFroude = 0.95f;

        // Kijima-type depth corrections as used by Taimuri et al. (2020) and transcribed in pymaneuvering.
        // Estimates: the K2 term uses the B/T <= 4 branch for every hull, because the B/T > 4 branch gives
        // added masses thirty times the deep value for the 507B, and cross terms whose published
        // polynomials diverge for this geometry use the nearest geometric factor.
        public static DepthFactors DepthFactors(float lpp, float beam, float draft, float blockCoefficient, float depth)
        {
            if (float.IsInfinity(depth) || depth <= 0f) return Physics.DepthFactors.Deep;
            float h = Mathf.Max(depth, draft * MinimumDepthToDraft);
            float ht = h / draft - 1f;
            float th = draft / h;
            float k0 = 1f + 0.0775f / Pow(ht, 2) - 0.011f / Pow(ht, 3) + 0.000068f / Pow(ht, 5);
            float k1 = -0.0643f / ht + 0.0724f / Pow(ht, 2) - 0.0113f / Pow(ht, 3) + 0.0000767f / Pow(ht, 5);
            float k2 = 0.0342f / ht;
            float b1 = blockCoefficient * beam * (1f + beam / lpp) * (1f + beam / lpp) / draft;
            float gv = k0 + 2f / 3f * k1 * b1 + 8f / 15f * k2 * b1 * b1;
            float gnr = k0 + 8f / 15f * k1 * b1 + 40f / 105f * k2 * b1 * b1;
            float fyr = k0 + 2f / 5f * k1 * b1 + 24f / 105f * k2 * b1 * b1;
            float fnr = k0 + 0.5f * k1 * b1 + 1f / 3f * k2 * b1 * b1;
            float fnv = k0 + k1 * b1 + k2 * b1 * b1;
            float fyv = 1.5f * fnr - 0.5f;
            float kijimaYv = -th + 1f / Mathf.Pow(1f - th, 0.4f * blockCoefficient * beam / draft);
            float kijimaNr = -th + 1f / Mathf.Pow(1f - th, -14.28f * draft / lpp + 1.5f);
            return new DepthFactors(AtLeastOne(gv), AtLeastOne(gnr), AtLeastOne(kijimaYv), AtLeastOne(kijimaNr),
                AtLeastOne(fnv), AtLeastOne(fyr), AtLeastOne(fyv), AtLeastOne(gnr), AtLeastOne(fnr));
        }

        // ICORELS bow squat (PIANC), with the Finnish Maritime Administration block-coefficient classes
        // unless a coefficient is configured.
        public static float IcorelsBowSquatM(float displacementVolume, float lpp, float blockCoefficient,
            float speed, float depth, float coefficient = 0f)
        {
            if (float.IsInfinity(depth) || depth <= 0f || speed <= 0f) return 0f;
            float cs = coefficient > 0f ? coefficient : blockCoefficient < 0.7f ? 1.7f : blockCoefficient < 0.8f ? 2.0f : 2.4f;
            float froude = Mathf.Min(speed / Mathf.Sqrt(VesselParameters.Gravity * depth), MaxDepthFroude);
            return cs * displacementVolume / (lpp * lpp) * froude * froude / Mathf.Sqrt(1f - froude * froude);
        }

        // Barrass's blockage factor K = 5.74 S^0.76, 1 for wide rivers (S < 0.1).
        public static float BlockageFactor(float blockage)
        {
            return blockage < 0.1f ? 1f : 5.74f * Mathf.Pow(Mathf.Min(blockage, 0.5f), 0.76f);
        }

        // Barrass: full forms squat by the bow, fine forms by the stern; [1 - 40 (0.7 - C_B)^2] at the other end.
        public static void Squat(float displacementVolume, float lpp, float blockCoefficient, float speed,
            float depth, float blockage, float coefficient, out float bowM, out float sternM)
        {
            float maximum = IcorelsBowSquatM(displacementVolume, lpp, blockCoefficient, speed, depth, coefficient) *
                BlockageFactor(blockage);
            float otherEnd = Mathf.Clamp01(1f - 40f * (0.7f - blockCoefficient) * (0.7f - blockCoefficient)) * maximum;
            bool byBow = blockCoefficient >= 0.7f;
            bowM = byBow ? maximum : otherEnd;
            sternM = byBow ? otherEnd : maximum;
        }

        // Estimated bank effect: one-dimensional continuity puts return flow into the water beside the hull,
        // Bernoulli turns the side-to-side difference into suction toward the nearer bank, and an estimated
        // lever acting aft of midship turns the bow away. Not the Lataire and Vantorre regressions.
        public static void BankForces(VesselParameters p, float speed, float depth, float portFlowAreaM2,
            float starboardFlowAreaM2, out float y, out float n)
        {
            y = n = 0f;
            if (float.IsInfinity(portFlowAreaM2) || float.IsInfinity(starboardFlowAreaM2) || float.IsInfinity(depth)) return;
            VesselRestrictedWater data = p.Data.restrictedWater;
            float underKeel = 0.5f * p.Beam * Mathf.Max(depth - p.Draft, 0.1f);
            float halfSection = 0.5f * p.MidshipArea;
            float portRatio = halfSection / (Mathf.Max(portFlowAreaM2, 0f) + underKeel);
            float starboardRatio = halfSection / (Mathf.Max(starboardFlowAreaM2, 0f) + underKeel);
            float portDrop = (1f + portRatio) * (1f + portRatio) - 1f;
            float starboardDrop = (1f + starboardRatio) * (1f + starboardRatio) - 1f;
            y = data.bankSuctionCoefficient * 0.5f * p.Rho * speed * speed * p.Lpp * p.Draft * (starboardDrop - portDrop);
            n = data.bankMomentLeverPrime * p.Lpp * y;
        }

        public static float Blockage(VesselParameters p, float depth, float portFlowAreaM2, float starboardFlowAreaM2)
        {
            if (float.IsInfinity(portFlowAreaM2) || float.IsInfinity(starboardFlowAreaM2) || float.IsInfinity(depth)) return 0f;
            float channel = p.MidshipArea + p.Beam * Mathf.Max(depth - p.Draft, 0f) +
                Mathf.Max(portFlowAreaM2, 0f) + Mathf.Max(starboardFlowAreaM2, 0f);
            return channel > 0f ? p.MidshipArea / channel : 0f;
        }

        private static float Pow(float value, int exponent)
        {
            return Mathf.Pow(value, exponent);
        }

        private static float AtLeastOne(float value)
        {
            return float.IsNaN(value) ? 1f : Mathf.Max(1f, value);
        }
    }
}
