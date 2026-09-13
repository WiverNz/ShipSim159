using UnityEngine;

namespace ShipSimulator.Visuals
{
    // Deterministic variation around the configured wind: speed gusts, a slow meander in direction
    // and an occasional shift. It animates water, clouds and trees only; the vessel keeps feeling
    // the configured mean wind.
    public static class WindGustModel
    {
        public const float ShiftPeriodS = 110f;
        public const float ShiftDurationS = 25f;
        public const float MaxShiftDeg = 20f;
        public const float MeanderDeg = 10f;
        public const float MinSpeedFactor = 0.5f;
        public const float MaxSpeedFactor = 1.6f;
        // Even a calm reach has light airs, so the surface never looks frozen.
        public const float LightAirMps = 0.8f;

        public static Vector3 VisualWind(double time, float directionDeg, float speedMps)
        {
            float lightAir = LightAirMps * (1f + 0.3f * Noise((float)(time / 6.1), 51));
            float speed = Mathf.Max(Mathf.Max(0f, speedMps) * SpeedFactor(time), lightAir);
            return WeatherController.CalculateWindVelocity(directionDeg + DirectionOffsetDeg(time), speed);
        }

        public static float DirectionOffsetDeg(double time)
        {
            long period = (long)System.Math.Floor(time / ShiftPeriodS);
            float into = (float)(time - period * (double)ShiftPeriodS);
            // Each period eases from the previous heading offset to a new one, starting at a
            // random moment so the shifts do not arrive on a visible beat.
            float start = Hash01(period, 3) * (ShiftPeriodS - ShiftDurationS);
            float blend = Mathf.SmoothStep(0f, 1f, (into - start) / ShiftDurationS);
            float shift = Mathf.Lerp(ShiftTarget(period - 1), ShiftTarget(period), blend);
            return shift + MeanderDeg * Noise((float)(time / 47.0), 11);
        }

        public static float SpeedFactor(double time)
        {
            float gusts = 0.16f * Noise((float)(time / 8.3), 21) + 0.09f * Noise((float)(time / 2.9), 31);
            float burst = Mathf.Max(0f, Noise((float)(time / 19.0), 41));
            return Mathf.Clamp(1f + gusts + 0.35f * burst * burst, MinSpeedFactor, MaxSpeedFactor);
        }

        private static float ShiftTarget(long period)
        {
            return (Hash01(period, 7) * 2f - 1f) * MaxShiftDeg;
        }

        // Smooth value noise in [-1, 1].
        private static float Noise(float x, int seed)
        {
            float cell = Mathf.Floor(x);
            float f = x - cell;
            f = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash01((long)cell, seed), Hash01((long)cell + 1, seed), f) * 2f - 1f;
        }

        private static float Hash01(long value, int seed)
        {
            unchecked
            {
                ulong h = (ulong)value * 0x9E3779B97F4A7C15UL + (ulong)seed * 0xBF58476D1CE4E5B9UL;
                h ^= h >> 31;
                h *= 0x94D049BB133111EBUL;
                h ^= h >> 29;
                return (h & 0xFFFFFFUL) / (float)0x1000000;
            }
        }
    }
}
