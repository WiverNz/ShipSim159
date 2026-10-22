using System;
using System.Collections.Generic;
using System.IO;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Editor
{
    public static class VolgoDonHandlingReview
    {
        private const string Folder = "Art/Source/VolgoDon507B/";
        [Serializable] public sealed class Row
        {
            public string vessel;
            public float depthM;
            public float speedMps, asternMps, advanceM, tacticalM, steadyTurnM;
            public float stopM, stopSeconds, firstOvershoot, secondOvershoot;
            public float differentialYawDeg, lowSpeedYawDeg, thrusterYawDegPerMin;
            public float portRpm, shaftLoad, secondsTo90PercentSpeed;
        }
        [Serializable] private sealed class Report { public Row[] rows; }

        public static void Before() => Run("before");
        public static void After() => Run("after");

        private static void Run(string stage)
        {
            try
            {
                if (stage == "before")
                {
                    string backup = Folder + "part6-baseline-vessel.json";
                    if (File.Exists(backup)) throw new InvalidOperationException("Part 6 baseline already exists.");
                    File.Copy("Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json", backup);
                }
                var rows = new List<Row>();
                foreach (string vessel in new[] { "VolgoDon507B", "VolgoBalt295AR", "Volgoneft1577", "Meteor342U", "Luch14352", "KVLCC2_MMG_Benchmark" })
                {
                    VesselData data = JsonUtility.FromJson<VesselData>(File.ReadAllText("Assets/ShipSimulator/Data/Vessels/" + vessel + ".json"));
                    rows.Add(Measure(vessel, data, float.PositiveInfinity));
                    if (vessel == "VolgoDon507B") rows.Add(Measure(vessel, data, 4.6f));
                }
                if (stage == "after")
                {
                    Report before = JsonUtility.FromJson<Report>(File.ReadAllText(Folder + "part6-before.json"));
                    for (int i = 2; i < rows.Count; i++)
                        if (JsonUtility.ToJson(before.rows[i]) != JsonUtility.ToJson(rows[i]))
                            throw new InvalidOperationException("Handling regression: " + rows[i].vessel);
                }
                File.WriteAllText(Folder + "part6-" + stage + ".json", JsonUtility.ToJson(new Report { rows = rows.ToArray() }, true));
                Debug.Log("VOLGODON_HANDLING|PASS|" + stage);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static Row Measure(string vessel, VesselData data, float depth)
        {
            var p = VesselParameters.Create(data, 1);
            var full = ManoeuvringTrials.Approach(p, 1, depth);
            var turn = ManoeuvringTrials.TurningCircle(p, 1, depth);
            var stop = ManoeuvringTrials.CrashStop(p, depth);
            var zig = ManoeuvringTrials.ZigZag(p, 10, depth);
            var twenty = ManoeuvringTrials.ZigZag(p, 20, depth);
            if (!turn.Completed || !stop.Completed || !zig.Completed || !twenty.Completed)
                throw new InvalidOperationException("Incomplete handling trial: " + vessel + " depth=" + depth);
            var differential = new ManoeuvringSimulator(p) { DepthM = depth };
            differential.EngineCommands[0] = 1;
            if (differential.EngineCommands.Length > 1) differential.EngineCommands[1] = -1;
            var slow = new ManoeuvringSimulator(p) { DepthM = depth, RudderCommand = 1 };
            slow.SetEngineCommands(.32f);
            for (int i = 0; i < 3600; i++) { differential.Step(.05f); slow.Step(.05f); }
            var accelerating = new ManoeuvringSimulator(p) { DepthM = depth };
            accelerating.SetEngineCommands(1);
            while (accelerating.TimeS < 2400 && accelerating.SurgeSpeed < .9f * full.SurgeSpeed) accelerating.Step(.1f);
            return new Row
            {
                vessel = vessel, depthM = float.IsInfinity(depth) ? -1 : depth,
                speedMps = full.SurgeSpeed, asternMps = ManoeuvringTrials.SteadySpeed(p, -1, depth),
                advanceM = turn.AdvanceM, tacticalM = turn.TacticalDiameterM, steadyTurnM = turn.SteadyTurningDiameterM,
                stopM = stop.TrackReachM, stopSeconds = stop.TimeS,
                firstOvershoot = zig.FirstOvershootDeg, secondOvershoot = zig.SecondOvershootDeg,
                differentialYawDeg = differential.HeadingRad * Mathf.Rad2Deg,
                lowSpeedYawDeg = slow.HeadingRad * Mathf.Rad2Deg,
                thrusterYawDegPerMin = ManoeuvringTrials.BowThrusterTurnRateDegPerMin(p, depth),
                portRpm = full.Model.Shafts[0].Rps * 60,
                shaftLoad = full.Model.Shafts[0].LoadFraction,
                secondsTo90PercentSpeed = accelerating.TimeS
            };
        }
    }
}
