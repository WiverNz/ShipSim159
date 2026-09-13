using System;
using System.Globalization;
using System.IO;
using System.Text;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Editor
{
    // Runs standard manoeuvres on the pure simulator and writes Logs/SeaTrials/sea-trials.md. Deep-water loaded
    // results are compared with the IMO MSC.137(76) envelope; river depths are reported for comparison only.
    public static class VirtualSeaTrials
    {
        private const string VesselFolder = "Assets/ShipSimulator/Data/Vessels/";
        private const string ReportPath = "Logs/SeaTrials/sea-trials.md";
        private const float Knot = 0.514444f;

        [MenuItem("Ship Simulator/Run Virtual Sea Trials")]
        public static void RunFromMenu()
        {
            bool withinEnvelope = WriteReport();
            Debug.Log($"SEA_TRIALS|{(withinEnvelope ? "PASS" : "OUTSIDE_IMO_ENVELOPE")}: {Path.GetFullPath(ReportPath)}");
        }

        public static void Run()
        {
            try
            {
                bool withinEnvelope = WriteReport();
                Debug.Log($"SEA_TRIALS|{(withinEnvelope ? "PASS" : "OUTSIDE_IMO_ENVELOPE")}: {Path.GetFullPath(ReportPath)}");
                EditorApplication.Exit(withinEnvelope ? 0 : 1);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static bool WriteReport()
        {
            VesselData volgoDon = Load("VolgoDon507B.json");
            VesselData kvlcc2 = Load("KVLCC2_MMG_Benchmark.json");
            var report = new StringBuilder();
            report.AppendLine("# Virtual Sea Trials");
            report.AppendLine();
            report.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}. Pure three-degree-of-freedom simulation of the");
            report.AppendLine("manoeuvring model. Project 507B coefficients are estimates, not trial data; this is not");
            report.AppendLine("validated for maritime training. Distances are in ship lengths (Lpp).");
            report.AppendLine();
            report.AppendLine("| Vessel and condition | Depth m | Slow / half / full kn | Advance | Transfer | Tactical D | Steady D | Turn speed loss | 10/10 1st / 2nd deg | 20/20 1st deg | Initial turn | Crash stop track | Stop time s | Full-speed squat bow / stern m |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");

            bool withinEnvelope = true;
            withinEnvelope &= Row(report, "507B loaded, deep water", VesselParameters.Create(volgoDon, 1f), float.PositiveInfinity, true);
            Row(report, "507B loaded, river familiarisation channel", VesselParameters.Create(volgoDon, 1f), 8.2f, false);
            Row(report, "507B loaded, Gorodets reach", VesselParameters.Create(volgoDon, 1f), 4.6f, false);
            Row(report, "507B lightship, deep water", VesselParameters.Create(volgoDon, 0f), float.PositiveInfinity, false);
            withinEnvelope &= Row(report, "KVLCC2 MMG benchmark, deep water", VesselParameters.Create(kvlcc2, 1f), float.PositiveInfinity, true);

            report.AppendLine();
            report.AppendLine("IMO MSC.137(76) reference envelope (deep, unrestricted water, full load): advance <= 4.5 L,");
            report.AppendLine("tactical diameter <= 5 L, initial turning <= 2.5 L, 10/10 and 20/20 overshoot limits by L/V,");
            report.AppendLine("crash stop track reach <= 15 L. The criteria do not apply to a river vessel in shallow water.");
            report.AppendLine();
            report.AppendLine(withinEnvelope
                ? "Result: deep-water loaded conditions are inside the IMO envelope."
                : "Result: at least one deep-water loaded condition is outside the IMO envelope.");

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, report.ToString());
            return withinEnvelope;
        }

        private static bool Row(StringBuilder report, string name, VesselParameters p, float depth, bool checkImo)
        {
            float slow = ManoeuvringTrials.SteadySpeed(p, 0.32f, depth);
            float half = ManoeuvringTrials.SteadySpeed(p, 0.65f, depth);
            ManoeuvringSimulator full = ManoeuvringTrials.Approach(p, 1f, depth);
            TurningCircleResult turn = ManoeuvringTrials.TurningCircle(p, 1f, depth);
            ZigZagResult ten = ManoeuvringTrials.ZigZag(p, 10f, depth);
            ZigZagResult twenty = ManoeuvringTrials.ZigZag(p, 20f, depth);
            StoppingResult stop = ManoeuvringTrials.CrashStop(p, depth);
            float lpp = p.Lpp;

            report.AppendLine(string.Join(" | ",
                "| " + name,
                float.IsInfinity(depth) ? "deep" : F(depth, 1),
                $"{F(slow / Knot, 1)} / {F(half / Knot, 1)} / {F(full.SurgeSpeed / Knot, 1)}",
                F(turn.AdvanceM / lpp, 2), F(turn.TransferM / lpp, 2), F(turn.TacticalDiameterM / lpp, 2),
                F(turn.SteadyTurningDiameterM / lpp, 2),
                F(100f * (1f - turn.SteadySpeedMps / turn.ApproachSpeedMps), 0) + " %",
                $"{F(ten.FirstOvershootDeg, 1)} / {F(ten.SecondOvershootDeg, 1)}",
                F(twenty.FirstOvershootDeg, 1),
                F(ten.InitialTurningDistanceM / lpp, 2),
                F(stop.TrackReachM / lpp, 2),
                F(stop.TimeS, 0),
                $"{F(full.Last.BowSquatM, 2)} / {F(full.Last.SternSquatM, 2)} |"));

            if (!checkImo) return true;
            return turn.AdvanceM <= 4.5f * lpp && turn.TacticalDiameterM <= 5f * lpp &&
                ten.InitialTurningDistanceM <= 2.5f * lpp &&
                ten.FirstOvershootDeg <= ManoeuvringTrials.ImoFirstOvershootLimitDeg(ten.LengthOverSpeedS) &&
                ten.SecondOvershootDeg <= ManoeuvringTrials.ImoSecondOvershootLimitDeg(ten.LengthOverSpeedS) &&
                twenty.FirstOvershootDeg <= 25f && stop.TrackReachM <= 15f * lpp;
        }

        private static VesselData Load(string file)
        {
            return JsonUtility.FromJson<VesselData>(File.ReadAllText(VesselFolder + file));
        }

        private static string F(float value, int decimals)
        {
            return float.IsNaN(value) ? "not reached" : value.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }
    }
}
