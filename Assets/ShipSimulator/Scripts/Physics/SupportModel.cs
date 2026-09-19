using UnityEngine;

namespace ShipSimulator.Physics
{
    // Fast craft that stop floating on their hull as they gather way: an air cushion between skegs, or
    // hydrofoils. One speed-dependent fraction says how much of the weight the cushion or foils carry.
    // That fraction lifts the hull, so draft follows from hydrostatics, weakens every force the immersed
    // hull produces, and reshapes resistance: a hump around takeoff, then far less than displacement drag.
    // The curve shape is an estimate of published trends, not measured data for these craft.
    public static class SupportModel
    {
        public static bool Lifts(VesselSupport support) =>
            support != null && support.supportedWeightFraction > 0f && support.fullSupportSpeedMps > support.takeoffSpeedMps;

        // Fraction of the vessel's weight carried by the cushion or the foils at this speed.
        public static float Fraction(VesselSupport support, float speed)
        {
            if (!Lifts(support)) return 0f;
            float t = Mathf.InverseLerp(support.takeoffSpeedMps, support.fullSupportSpeedMps, Mathf.Abs(speed));
            return support.supportedWeightFraction * Mathf.SmoothStep(0f, 1f, t);
        }

        // How much hull is still working, for hull forces, squat and bank suction.
        public static float Immersion(VesselSupport support, float speed) => 1f - 0.9f * Fraction(support, speed);

        public static float ResistanceFactor(VesselSupport support, float speed)
        {
            if (!Lifts(support)) return 1f;
            float progress = Fraction(support, speed) / support.supportedWeightFraction;
            // Drag peaks midway through takeoff, where the hull is still wet and the wave making is worst.
            float hump = Mathf.Sin(Mathf.PI * Mathf.Clamp01(progress));
            return Mathf.Lerp(1f, support.supportedResistanceFactor, progress) * Mathf.Lerp(1f, support.humpResistanceFactor, hump);
        }

        // Depth of the deepest part below the waterline: the hull when floating, the foils or skegs once up.
        public static float SupportedDraftM(VesselSupport support, float speed)
        {
            if (!Lifts(support)) return 0f;
            float progress = Fraction(support, speed) / support.supportedWeightFraction;
            return progress > 0.05f ? support.supportedDraftM : 0f;
        }
    }
}
