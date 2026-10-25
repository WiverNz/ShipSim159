using UnityEngine;

namespace ShipSimulator.Physics
{
    // This surface is already represented by scenario depth. Quays and unrelated obstacles stay in PhysX.
    public sealed class BathymetryContactSurface : MonoBehaviour
    {
        private void OnEnable() => Refresh(true);
        private void Start() => Refresh(true);
        private void OnDisable() => Refresh(false);

        private void Refresh(bool active)
        {
            foreach (GroundingController grounding in FindObjectsByType<GroundingController>(FindObjectsSortMode.None))
                SetPairs(grounding, active && grounding.Bathymetry != null && grounding.isActiveAndEnabled);
        }

        private void SetPairs(GroundingController grounding, bool ignore)
        {
            foreach (Collider bed in GetComponentsInChildren<Collider>())
                foreach (Collider part in grounding.GetComponentsInChildren<Collider>())
                    if (part != bed && !part.isTrigger && !bed.isTrigger)
                        UnityEngine.Physics.IgnoreCollision(part, bed, ignore);
        }

        public static void Apply(GroundingController grounding)
        {
            if (grounding == null) return;
            foreach (BathymetryContactSurface surface in FindObjectsByType<BathymetryContactSurface>(FindObjectsSortMode.None))
                surface.SetPairs(grounding, surface.isActiveAndEnabled && grounding.Bathymetry != null && grounding.isActiveAndEnabled);
        }
    }
}
