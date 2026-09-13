using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.UI
{
    // The vessel chosen for the next passage. Kept for the session only, so automated checks never change
    // what a player sees next time.
    public static class VesselSelection
    {
        private static string selectedId = VesselCatalogue.DefaultVesselId;

        public static string SelectedId
        {
            get => selectedId;
            set => selectedId = string.IsNullOrEmpty(value) ? VesselCatalogue.DefaultVesselId : value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSelection() => selectedId = VesselCatalogue.DefaultVesselId;
    }
}
