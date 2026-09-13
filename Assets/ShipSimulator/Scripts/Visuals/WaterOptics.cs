using UnityEngine;

namespace ShipSimulator.Visuals
{
    public static class WaterOptics
    {
        // Estimated sediment extinction, matching RiverWaterSurface.hlsl for optical checks.
        public static Vector3 Transmittance(float pathMetres, float visibilityMetres, float rain)
        {
            float secchi = Mathf.Max(0.2f, visibilityMetres * (1 - 0.25f * Mathf.Clamp01(rain)));
            float extinction = 1.7f / secchi * Mathf.Max(0, pathMetres);
            return new Vector3(Mathf.Exp(-0.75f * extinction), Mathf.Exp(-extinction), Mathf.Exp(-1.6f * extinction));
        }
    }
}
