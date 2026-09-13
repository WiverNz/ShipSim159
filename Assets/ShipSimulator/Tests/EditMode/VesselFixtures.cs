using ShipSimulator.Physics;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Tests
{
    internal static class VesselFixtures
    {
        public const string VolgoDonFile = "VolgoDon507B.json";
        public const string Kvlcc2File = "KVLCC2_MMG_Benchmark.json";

        public static VesselData Load(string file)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ShipSimulator/Data/Vessels/" + file);
            return JsonUtility.FromJson<VesselData>(json.text);
        }

        public static VesselParameters VolgoDon() => VesselParameters.Create(Load(VolgoDonFile));
        public static VesselParameters Kvlcc2() => VesselParameters.Create(Load(Kvlcc2File));
    }
}
