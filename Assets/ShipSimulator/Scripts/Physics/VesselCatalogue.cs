using ShipSimulator.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShipSimulator.Physics
{
    // Vessels the player can choose. Loaded from Resources so every listed prefab ships in a build.
    public sealed class VesselCatalogue : ScriptableObject
    {
        public const string ResourcePath = "VesselCatalogue";
        public const string DefaultVesselId = "volgodon-507b";

        [Serializable]
        public sealed class Entry
        {
            public string id;
            // Short uppercase name for the start menu chart, may contain a line break.
            public string menuName;
            public string vesselClass;
            public GameObject prefab;
            public string displayName;
            public Vector3 formationSizeM;

            public string DisplayName => Localization.Source(string.IsNullOrEmpty(displayName) ? LoadData()?.identity.displayName ?? id : displayName);
            public string Description => formationSizeM.z > 0
                ? Localization.Get("text.0.0.0.m.push.unit.loaded.mn.52.tug.controls.214b8460", formationSizeM.z)
                : Describe(LoadData());

            public VesselData LoadData()
            {
                VesselDataLoader loader = prefab != null ? prefab.GetComponent<VesselDataLoader>() : null;
                return loader != null && loader.Json != null ? JsonUtility.FromJson<VesselData>(loader.Json.text) : null;
            }

            public static string Describe(VesselData data)
            {
                if (data == null) return string.Empty;
                return string.Format(Localization.Culture, "{0:0.0} m  /  {1:0} t dwt  /  {2} x {3:0} kW",
                    data.dimensions.lengthOverallM, data.massProperties.deadweightKg / 1000f,
                    data.engine.engineCount, data.engine.powerPerEngineW / 1000f);
            }
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public void Configure(Entry[] vessels)
        {
            entries = vessels ?? Array.Empty<Entry>();
        }

        public Entry Find(string id)
        {
            foreach (Entry entry in entries)
                if (entry != null && entry.id == id) return entry;
            return null;
        }

        public static VesselCatalogue Load() => Resources.Load<VesselCatalogue>(ResourcePath);
    }
}
