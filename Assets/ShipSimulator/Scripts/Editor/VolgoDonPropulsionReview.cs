using System;
using System.Collections.Generic;
using System.IO;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Editor
{
    // Part 12 propulsion, stopping and steering review on the pure simulator. Runs matched trial conditions,
    // decomposes the crash stop into its phases, sweeps estimated parameters within documented bounds and
    // checks the step size. Writes part12-trials-<stage>.json; -part12Vessel selects the vessel JSON (default
    // the current one). Ends with PROPULSION_REVIEW|PASS.
    public static class VolgoDonPropulsionReview
    {
        private const string Folder = "Art/Source/VolgoDon507B/";
        private const float Knot = 0.514444f;

        [Serializable] public sealed class Trial
        {
            public string condition;
            public float massT, draftM, depthM, dt;
            public float fullSpeedMps, fullSpeedKmh, shaftRpm, loadFraction;
            public float accelerationTo90S, accelerationTo90M;
            public float halfAheadStopTrackM, halfAheadStopTimeS, halfAheadSpeedMps;
            public float slowAheadStopTrackM, slowAheadStopTimeS, slowAheadSpeedMps;
            public float stopTrackM, stopHeadReachM, stopTimeS, reversalTimeS, reversalTrackM, fullAsternThrustKn;
            public float advanceM, tacticalDiameterM, steadyDiameterM, turnSpeedMps;
            public float zigzag10FirstDeg, zigzag10SecondDeg, zigzag20FirstDeg;
            public float splitAtRestYawDegPerMin, splitWithNozzlesYawDegPerMin, splitTurnDiameterM;
            public float bowThrusterYawDegPerMin;
        }

        [Serializable] public sealed class Sweep { public string parameter; public float value; public Trial result; }

        [Serializable] public sealed class Report
        {
            public string stage, vessel, generated, classification;
            public Trial[] trials;
            public Sweep[] sweeps;
        }

        public static void Run()
        {
            try
            {
                string stage = Argument("-part12Stage") ?? "after";
                string vesselPath = Argument("-part12Vessel") ?? "Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json";
                string json = File.ReadAllText(vesselPath);
                var report = new Report
                {
                    stage = stage, vessel = vesselPath, generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    classification = "Prototype simulation with estimated coefficients; not validated trial data.",
                    trials = Conditions(json).ToArray(),
                    sweeps = Argument("-part12Sweeps") == "false" ? Array.Empty<Sweep>() : Sweeps(json).ToArray()
                };
                File.WriteAllText(Folder + $"part12-trials-{stage}.json", JsonUtility.ToJson(report, true));
                Debug.Log("PROPULSION_REVIEW|PASS " + stage);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static List<Trial> Conditions(string json)
        {
            VesselData data = JsonUtility.FromJson<VesselData>(json);
            var list = new List<Trial>
            {
                Measure("full load, deep (calibration: archive 20 km/h; stop 510 m / 210 s; circulation 320 m)",
                    data, LoadingModel.Initial(data, 1f), float.PositiveInfinity, 0.05f),
                Measure("full load, deep, half step", data, LoadingModel.Initial(data, 1f), float.PositiveInfinity, 0.025f),
                Measure("full load, deep, double step", data, LoadingModel.Initial(data, 1f), float.PositiveInfinity, 0.1f),
                Measure("archive ballast 3828 t, deep (validation: archive 21.2 km/h)", data, ArchiveBallast(data),
                    float.PositiveInfinity, 0.05f),
                Measure("full load, 8.2 m river channel", data, LoadingModel.Initial(data, 1f), 8.2f, 0.05f),
                Measure("full load, 4.6 m Gorodets reach", data, LoadingModel.Initial(data, 1f), 4.6f, 0.05f),
                Measure("archive ballast 3828 t, 4.6 m", data, ArchiveBallast(data), 4.6f, 0.05f),
                Measure("lightship, deep", data, LoadingModel.Initial(data, 0f), float.PositiveInfinity, 0.05f)
            };
            return list;
        }

        // Archive p3 ballast condition: 3828 t. The prototype lightship is 1750 t, so ballast is split between
        // both groups with full stores to reach the same displacement; tank layout is estimated.
        public static LoadingState ArchiveBallast(VesselData data)
        {
            LoadingState state = LoadingModel.Initial(data, 0f);
            state.massKg[4] = data.loading.compartments[4].capacityKg;
            float ballast = 3828000f - data.massProperties.lightshipMassKg - state.massKg[4];
            state.massKg[2] = 0.5f * ballast;
            state.massKg[3] = 0.5f * ballast;
            return state;
        }

        private static List<Sweep> Sweeps(string json)
        {
            var sweeps = new List<Sweep>();
            void Add(string parameter, float value, Action<VesselData> change)
            {
                VesselData data = JsonUtility.FromJson<VesselData>(json);
                change(data);
                sweeps.Add(new Sweep { parameter = parameter, value = value,
                    result = Measure(parameter + " = " + value, data, LoadingModel.Initial(data, 1f), float.PositiveInfinity, 0.05f) });
            }
            foreach (float v in new[] { 4f, 8f, 12f }) Add("engine.reversalDelaySeconds", v, d => d.engine.reversalDelaySeconds = v);
            foreach (float v in new[] { 0.3f, 0.5f, 1f }) Add("engine.reversalBrakeTorqueFraction", v, d => d.engine.reversalBrakeTorqueFraction = v);
            foreach (float v in new[] { 0.15f, 0.25f, 0.4f }) Add("engine.reversalRpmFraction", v, d => d.engine.reversalRpmFraction = v);
            foreach (float v in new[] { 8f, 15f, 25f }) Add("engine.rpmRampSecondsFullRange", v, d => d.engine.rpmRampSecondsFullRange = v);
            foreach (float v in new[] { 0.8f, 1f, 1.2f })
                Add("propeller.asternThrustCoefficients scale", v, d => Scale(d.propeller.asternThrustCoefficients, v));
            foreach (float v in new[] { 0.8f, 1f, 1.2f })
                Add("propeller.asternTorqueCoefficients scale", v, d => Scale(d.propeller.asternTorqueCoefficients, v));
            foreach (float v in new[] { 0.1f, 0.2f, 0.3f }) Add("propeller.thrustDeduction", v, d => d.propeller.thrustDeduction = v);
            foreach (float v in new[] { 0.00005f, 0.0001005f, 0.00015f })
                Add("resistance.residualResistanceCoefficient", v, d => d.resistance.residualResistanceCoefficient = v);
            foreach (float v in new[] { 0.007f, 0.011104f, 0.02f, 0.04f }) Add("hull.surgeAddedMassPrime", v, d => d.hull.surgeAddedMassPrime = v);
            foreach (float v in new[] { 2f, 3f, 4f, 5f }) Add("steeringNozzle.passiveNormalSlope", v, d => d.steeringNozzle.passiveNormalSlope = v);
            foreach (float v in new[] { 0.7f, 0.85f, 1f }) Add("steeringNozzle.thrustDeflectionFactor", v, d => d.steeringNozzle.thrustDeflectionFactor = v);
            foreach (float v in new[] { 0.8f, 1f, 1.25f }) Add("hull.nR scale", v, d => d.hull.nR *= v);
            return sweeps;
        }

        private static void Scale(float[] values, float factor)
        {
            for (int i = 0; i < values.Length; i++) values[i] *= factor;
        }

        public static Trial Measure(string condition, VesselData data, LoadingState loading, float depth, float dt)
        {
            VesselParameters p = VesselParameters.Create(data, loading);
            var trial = new Trial
            {
                condition = condition, massT = p.Mass / 1000f, draftM = p.Draft,
                depthM = float.IsInfinity(depth) ? -1f : depth, dt = dt
            };

            ManoeuvringSimulator full = Approach(p, 1f, depth);
            trial.fullSpeedMps = full.SurgeSpeed;
            trial.fullSpeedKmh = full.SurgeSpeed * 3.6f;
            trial.shaftRpm = full.Model.Shafts[0].Rps * 60f;
            trial.loadFraction = full.Model.Shafts[0].LoadFraction;

            var start = new ManoeuvringSimulator(p) { DepthM = depth };
            start.SetEngineCommands(1f);
            trial.accelerationTo90S = float.NaN;
            while (start.TimeS < 3000f)
            {
                start.Step(dt);
                if (start.SurgeSpeed >= 0.9f * trial.fullSpeedMps)
                {
                    trial.accelerationTo90S = start.TimeS;
                    trial.accelerationTo90M = start.PathLengthM;
                    break;
                }
            }

            CrashStop(p, depth, dt, 0.65f, out trial.halfAheadSpeedMps, out trial.halfAheadStopTrackM, out trial.halfAheadStopTimeS);
            CrashStop(p, depth, dt, 0.32f, out trial.slowAheadSpeedMps, out trial.slowAheadStopTrackM, out trial.slowAheadStopTimeS);
            ManoeuvringSimulator stop = Approach(p, 1f, depth);
            stop.SetEngineCommands(-1f);
            trial.reversalTimeS = float.NaN;
            while (stop.TimeS < 3000f && stop.SurgeSpeed > 0f)
            {
                stop.Step(dt);
                if (float.IsNaN(trial.reversalTimeS) && stop.Model.Shafts[0].Rps < 0f)
                {
                    trial.reversalTimeS = stop.TimeS;
                    trial.reversalTrackM = stop.PathLengthM;
                }
            }
            trial.stopTrackM = stop.PathLengthM;
            trial.stopHeadReachM = stop.Position.y;
            trial.stopTimeS = stop.TimeS;
            trial.fullAsternThrustKn = (stop.Model.PropellerThrustN[0] + stop.Model.PropellerThrustN[1]) / 1000f;

            ManoeuvringSimulator turn = Approach(p, 1f, depth);
            turn.RudderCommand = 1f;
            bool quarter = false, half = false;
            trial.advanceM = trial.tacticalDiameterM = float.NaN;
            while (turn.TimeS < 3000f && Mathf.Abs(turn.HeadingRad) < 4f * Mathf.PI)
            {
                turn.Step(dt);
                float turned = Mathf.Abs(turn.HeadingRad);
                if (!quarter && turned >= 0.5f * Mathf.PI) { quarter = true; trial.advanceM = turn.Position.y; }
                if (!half && turned >= Mathf.PI) { half = true; trial.tacticalDiameterM = Mathf.Abs(turn.Position.x); }
            }
            float speed = Mathf.Sqrt(turn.SurgeSpeed * turn.SurgeSpeed + turn.SwaySpeed * turn.SwaySpeed);
            trial.turnSpeedMps = speed;
            trial.steadyDiameterM = 2f * speed / Mathf.Max(Mathf.Abs(turn.YawRate), 1e-6f);

            ZigZag(p, depth, dt, 10f, out trial.zigzag10FirstDeg, out trial.zigzag10SecondDeg);
            ZigZag(p, depth, dt, 20f, out trial.zigzag20FirstDeg, out _);

            trial.splitAtRestYawDegPerMin = Split(p, depth, dt, 0f, out _);
            trial.splitWithNozzlesYawDegPerMin = Split(p, depth, dt, 1f, out trial.splitTurnDiameterM);
            var thruster = new ManoeuvringSimulator(p) { DepthM = depth, BowThrusterCommand = 1f };
            for (float t = 0f; t < 180f; t += dt) thruster.Step(dt);
            trial.bowThrusterYawDegPerMin = thruster.YawRate * Mathf.Rad2Deg * 60f;
            return trial;
        }

        private static void CrashStop(VesselParameters p, float depth, float dt, float command, out float speed,
            out float track, out float time)
        {
            ManoeuvringSimulator ship = Approach(p, command, depth);
            speed = ship.SurgeSpeed;
            ship.SetEngineCommands(-1f);
            while (ship.TimeS < 3000f && ship.SurgeSpeed > 0f) ship.Step(dt);
            track = ship.PathLengthM;
            time = ship.TimeS;
        }

        // Port ahead, starboard astern from rest for 300 s, optionally with the nozzles hard over to starboard.
        private static float Split(VesselParameters p, float depth, float dt, float rudder, out float diameter)
        {
            var ship = new ManoeuvringSimulator(p) { DepthM = depth, RudderCommand = rudder };
            ship.EngineCommands[0] = 1f;
            ship.EngineCommands[1] = -1f;
            for (float t = 0f; t < 300f; t += dt) ship.Step(dt);
            float speed = Mathf.Sqrt(ship.SurgeSpeed * ship.SurgeSpeed + ship.SwaySpeed * ship.SwaySpeed);
            diameter = 2f * speed / Mathf.Max(Mathf.Abs(ship.YawRate), 1e-6f);
            return ship.YawRate * Mathf.Rad2Deg * 60f;
        }

        private static void ZigZag(VesselParameters p, float depth, float dt, float angle, out float first, out float second)
        {
            ManoeuvringSimulator ship = Approach(p, 1f, depth);
            float command = angle / p.Data.rudder.maxAngleDeg;
            ship.RudderCommand = command;
            int phase = 0;
            float extreme = 0f;
            first = second = float.NaN;
            while (ship.TimeS < 2000f && phase < 3)
            {
                ship.Step(dt);
                float heading = ship.HeadingRad * Mathf.Rad2Deg;
                if (phase == 0 && heading >= angle) { ship.RudderCommand = -command; phase = 1; extreme = heading; }
                else if (phase == 1)
                {
                    extreme = Mathf.Max(extreme, heading);
                    if (heading <= -angle) { first = extreme - angle; ship.RudderCommand = command; phase = 2; extreme = heading; }
                }
                else if (phase == 2)
                {
                    extreme = Mathf.Min(extreme, heading);
                    if (heading >= angle) { second = -extreme - angle; phase = 3; }
                }
            }
        }

        private static ManoeuvringSimulator Approach(VesselParameters p, float command, float depth) =>
            ManoeuvringTrials.Approach(p, command, depth);

        private static string Argument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
                if (arguments[i] == name) return arguments[i + 1];
            return null;
        }
    }
}
