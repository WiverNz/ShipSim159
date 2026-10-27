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
        private readonly string title;
        public string Title => Localization.Source(title);
        private readonly string detail;
        public string Detail => Localization.Source(detail);
        // Zero means the passage sets no limit of that kind.
        public float MaxLengthOverallM { get; }
        public float MaxBeamOverallM { get; }
        public float MaxDraftM { get; }

        private VoyagePassage(string sceneName, string title, string detail,
            float maxLength = 0f, float maxBeam = 0f, float maxDraft = 0f)
        {
            SceneName = sceneName;
            this.title = title;
            this.detail = detail;
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

        public string FormationRestriction(VesselCatalogue.Entry entry)
        {
            if (entry == null) return null;
            if (MaxLengthOverallM > 0 && entry.formationSizeM.z > MaxLengthOverallM)
                return Localization.Get("text.push.unit.too.long.0.0.0.m.limit.1.0.m.c91f9391", entry.formationSizeM.z, MaxLengthOverallM);
            if (MaxBeamOverallM > 0 && entry.formationSizeM.x > MaxBeamOverallM)
                return Localization.Get("text.push.unit.too.wide.for.this.passage.074f823c");
            if (MaxDraftM > 0 && entry.formationSizeM.y > MaxDraftM)
                return Localization.Get("text.push.unit.too.deep.for.this.passage.79e9603a");
            return Restriction(entry.LoadData());
        }

        // Foils and skegs reach below the keel and touch the bottom first, so the draft that matters
        // to a shallow waterway is the deepest point, not the hull's.
        public static float DeepestDraftM(VesselData data) =>
            data == null
                ? 0f
                : data.dimensions.loadedDraftM +
                  (SupportModel.Lifts(data.support) ? data.support.supportedDraftM : 0f);

        // Null when the vessel fits; otherwise the reason it does not, ready to show in the menu.
        public string Restriction(VesselData data)
        {
            if (data == null) return null;
            VesselDimensions size = data.dimensions;
            if (MaxLengthOverallM > 0f && size.lengthOverallM > MaxLengthOverallM)
                return Localization.Get("text.too.long.for.this.passage.0.0.0.m.limit.1.0.m.5d3c0df4", size.lengthOverallM, MaxLengthOverallM);
            if (MaxBeamOverallM > 0f && size.beamOverallM > MaxBeamOverallM)
                return Localization.Get("text.too.wide.for.this.passage.0.0.0.m.limit.1.0.0.m.76a223bd", size.beamOverallM, MaxBeamOverallM);
            float draft = DeepestDraftM(data);
            if (MaxDraftM > 0f && draft > MaxDraftM)
                return Localization.Get("text.too.deep.for.this.passage.0.0.00.m.limit.1.0.00.m.dc1f11fd", draft, MaxDraftM);
            return null;
        }

        // The scene is built with one vessel and the menu swaps in the chosen one, so a vessel that
        // cannot fit the scene it is about to open has to be replaced before anything binds to it.
        public static string FirstAcceptedVesselId(VesselCatalogue catalogue, string sceneName, string preferredId)
        {
            VoyagePassage passage = Find(sceneName);
            if (passage == null || catalogue == null) return preferredId;
            VesselCatalogue.Entry preferred = catalogue.Find(preferredId);
            if (preferred != null && passage.FormationRestriction(preferred) == null) return preferredId;
            foreach (VesselCatalogue.Entry entry in catalogue.Entries)
                if (entry != null && passage.FormationRestriction(entry) == null) return entry.id;
            return preferredId;
        }
    }
}
