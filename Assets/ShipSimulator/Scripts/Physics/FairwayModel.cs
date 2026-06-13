using UnityEngine;

namespace ShipSimulator.Physics
{
    public static class FairwayModel
    {
        public const float DeepWaterDepthM = 8.2f;
        public const float ShoreDepthM = 0.7f;

        public static float CenterX(float z)
        {
            float firstBend = 24f * Mathf.Sin(
                Mathf.Clamp01((z - 170f) / 300f) * Mathf.PI);
            float secondBend = -18f * Mathf.Sin(
                Mathf.Clamp01((z - 470f) / 260f) * Mathf.PI);
            return firstBend + secondBend;
        }

        public static float ShoreDistance(float z)
        {
            return 75f + Mathf.Sin(z * 0.018f) * 4.5f +
                Mathf.Sin(z * 0.047f + 1.7f) * 2.2f +
                Mathf.PerlinNoise(2.31f, z * 0.008f) * 7f - 3.5f;
        }

        public static float MarkedHalfWidth(float z)
        {
            return Mathf.Lerp(53f, 45f, Mathf.Clamp01((z - 380f) / 300f));
        }

        public static float LateralOffset(float x, float z)
        {
            return x - CenterX(z);
        }

        public static float DepthAt(Vector3 worldPosition)
        {
            return DepthAt(worldPosition.x, worldPosition.z);
        }

        public static float DepthAt(float x, float z)
        {
            float normalized = Mathf.Clamp01(
                Mathf.Abs(LateralOffset(x, z)) / Mathf.Max(ShoreDistance(z), 1f));
            float profile = normalized * normalized * (3f - 2f * normalized);
            return Mathf.Lerp(DeepWaterDepthM, ShoreDepthM, profile);
        }
    }
}
