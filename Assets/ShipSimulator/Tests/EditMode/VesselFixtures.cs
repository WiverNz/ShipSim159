using ShipSimulator.Physics;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Tests
{
    internal static class VesselFixtures
    {
        public const string VolgoDonFile = "VolgoDon507B.json";
        public const string VolgoBaltFile = "VolgoBalt295AR.json";
        public const string VolgoneftFile = "Volgoneft1577.json";
        public const string Kvlcc2File = "KVLCC2_MMG_Benchmark.json";

        public static VesselData Load(string file)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/ShipSimulator/Data/Vessels/" + file);
            return JsonUtility.FromJson<VesselData>(json.text);
        }

        public static VesselParameters Parameters(string file) => VesselParameters.Create(Load(file));
        public static VesselParameters VolgoDon() => Parameters(VolgoDonFile);
        public static VesselParameters Kvlcc2() => Parameters(Kvlcc2File);
    }
}
