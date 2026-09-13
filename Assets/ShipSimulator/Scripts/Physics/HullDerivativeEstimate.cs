using UnityEngine;

namespace ShipSimulator.Physics
{
    // Empirical hull estimates from principal dimensions, expressed in the MMG prime system
    // (0.5 rho L d normalisation). Clarke, Gedling and Hine (1983) as implemented in Fossen's MSS
    // clarke83.m use 0.5 rho L^2 for forces, so every value converts by L/d.
    public readonly struct HullDerivativeEstimate
    {
        public readonly float Yv;
        public readonly float Yr;
        public readonly float Nv;
        public readonly float Nr;
        public readonly float SwayAddedMass;
        public readonly float YawAddedInertia;

        private HullDerivativeEstimate(float yv, float yr, float nv, float nr, float swayAddedMass, float yawAddedInertia)
        {
            Yv = yv;
            Yr = yr;
            Nv = nv;
            Nr = nr;
            SwayAddedMass = swayAddedMass;
            YawAddedInertia = yawAddedInertia;
        }

        public static HullDerivativeEstimate Clarke(float lpp, float beam, float draft, float blockCoefficient)
        {
            float s = Mathf.PI * (draft / lpp) * (draft / lpp);
            float toMmg = lpp / draft;
            float bt = beam / draft;
            float bl = beam / lpp;
            float cb = blockCoefficient;
            return new HullDerivativeEstimate(
                -s * (1f + 0.4f * cb * bt) * toMmg,
                -s * (-0.5f + 2.2f * bl - 0.08f * bt) * toMmg,
                -s * (0.5f + 2.4f * draft / lpp) * toMmg,
                -s * (0.25f + 0.039f * bt - 0.56f * bl) * toMmg,
                s * (1f + 0.16f * cb * bt - 5.1f * bl * bl) * toMmg,
                s * (1f / 12f + 0.017f * cb * bt - 0.33f * bl) * toMmg);
        }

        // Soding's surge added mass approximation, in kilograms.
        public static float SodingSurgeAddedMassKg(float waterDensity, float displacementVolume, float lpp)
        {
            return 2.7f * waterDensity * Mathf.Pow(displacementVolume, 5f / 3f) / (lpp * lpp);
        }
    }
}
