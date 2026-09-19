using System;
using ShipSimulator.Physics;

namespace ShipSimulator.UI
{
    // The passages offered in the start menu, and the largest vessel each one can take. A restricted
    // waterway keeps its own limits here rather than in the menu, so the scene, the save file and the
    // menu all agree on what may sail where.
    public sealed class VoyagePassage
    {
        public string SceneName { get; }
        public string Title { get; }
        public string Detail { get; }
        // Zero means the passage sets no limit of that kind.
        public float MaxLengthOverallM { get; }
        public float MaxBeamOverallM { get; }
        public float MaxDraftM { get; }

        private VoyagePassage(string sceneName, string title, string detail,
            float maxLength = 0f, float maxBeam = 0f, float maxDraft = 0f)
        {
            SceneName = sceneName;
            Title = title;
            Detail = detail;
            MaxLengthOverallM = maxLength;
            MaxBeamOverallM = maxBeam;
            MaxDraftM = maxDraft;
        }

        public static readonly VoyagePassage[] All =
        {
            new VoyagePassage("RiverTrainingScene", "River familiarisation",
                "Open river  /  Learn the vessel and controls"),
            new VoyagePassage("GorodetsTrainingScene", "Gorodets passage",
                "2.27 km  /  Shoals, currents & leading marks"),
            // Nara: 20 m marked channel, 100 m bend radius, a basin entrance about 55 m wide.
            // Nothing of cargo-ship size can be turned in there.
            new VoyagePassage("SerpukhovZatonScene", "Serpukhov zaton",
                "2.42 km  /  Oka entry, 20 m channel, basin berthing", 40f, 10f, 1.6f)
        };

        public static VoyagePassage Find(string sceneName)
        {
            foreach (VoyagePassage passage in All)
                if (passage.SceneName == sceneName) return passage;
            return null;
        }

        public static bool IsPassageScene(string sceneName) => Find(sceneName) != null;

        public static string TitleOf(string sceneName) => Find(sceneName)?.Title ?? sceneName;

        public bool Accepts(VesselData data) => Restriction(data) == null;

        // Null when the vessel fits; otherwise the reason it does not, ready to show in the menu.
        public string Restriction(VesselData data)
        {
            if (data == null) return null;
            VesselDimensions size = data.dimensions;
            if (MaxLengthOverallM > 0f && size.lengthOverallM > MaxLengthOverallM)
                return $"Too long for this passage  /  {size.lengthOverallM:0.0} m, limit {MaxLengthOverallM:0} m";
            if (MaxBeamOverallM > 0f && size.beamOverallM > MaxBeamOverallM)
                return $"Too wide for this passage  /  {size.beamOverallM:0.0} m, limit {MaxBeamOverallM:0.0} m";
            if (MaxDraftM > 0f && size.loadedDraftM > MaxDraftM)
                return $"Too deep for this passage  /  {size.loadedDraftM:0.00} m, limit {MaxDraftM:0.00} m";
            return null;
        }

        // The scene is built with one vessel and the menu swaps in the chosen one, so a vessel that
        // cannot fit the scene it is about to open has to be replaced before anything binds to it.
        public static string FirstAcceptedVesselId(VesselCatalogue catalogue, string sceneName, string preferredId)
        {
            VoyagePassage passage = Find(sceneName);
            if (passage == null || catalogue == null) return preferredId;
            VesselCatalogue.Entry preferred = catalogue.Find(preferredId);
            if (preferred != null && passage.Accepts(preferred.LoadData())) return preferredId;
            foreach (VesselCatalogue.Entry entry in catalogue.Entries)
                if (entry != null && passage.Accepts(entry.LoadData())) return entry.id;
            return preferredId;
        }
    }
}
