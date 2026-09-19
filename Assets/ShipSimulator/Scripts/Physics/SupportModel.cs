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

        // Fixed appendage geometry inferred from the published full-support draft. This is an
        // estimate, shared by lift, clearance and bottom contact rather than a speed-dependent HUD floor.
        public static float AppendageExtensionM(VesselParameters p) => Lifts(p.Data.support)
            ? Mathf.Max(0f, p.Data.support.supportedDraftM -
                p.LoadedDraft * (1f - p.Data.support.supportedWeightFraction)) : 0f;

        public static Vector3 ContactPoint(VesselParameters p, int index) => new Vector3(
            ((index & 1) == 0 ? -0.5f : 0.5f) * p.Data.dimensions.beamOverallM,
            p.KeelLocalY - AppendageExtensionM(p),
            p.Data.support.supportLongitudinalPositionM + ((index & 2) == 0 ? -0.25f : 0.25f) * p.Lpp);

        // Four estimated support patches supply heave, pitch and roll feedback. Lift vanishes when
        // a patch clears the surface, and point-velocity damping cannot lift a dry craft in free fall.
        public static float Apply(VesselParameters p, Rigidbody body, Transform frame,
            float waterLevel, float fraction)
        {
            if (fraction <= 0f) return 0f;
            float targetDepth = p.Draft * (1f - fraction) + AppendageExtensionM(p);
            float nominal = p.Mass * VesselParameters.Gravity * fraction / 4f;
            float stiffness = nominal / Mathf.Max(targetDepth, 0.01f);
            float damping = 2f * p.Data.hydrostatics.heaveDampingRatio *
                Mathf.Sqrt(stiffness * p.Mass / 4f);
            float total = 0f;
            for (int i = 0; i < 4; i++)
            {
                Vector3 point = frame.TransformPoint(ContactPoint(p, i));
                float depth = waterLevel - point.y;
                if (depth <= 0f) continue;
                float force = Mathf.Clamp(stiffness * depth - damping * body.GetPointVelocity(point).y,
                    0f, 2f * nominal);
                body.AddForceAtPosition(Vector3.up * force, point, ForceMode.Force);
                total += force;
            }
            return total;
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
