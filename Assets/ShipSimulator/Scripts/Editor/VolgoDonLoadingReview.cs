using System;
using System.Collections.Generic;
using System.IO;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Editor
{
    public static class VolgoDonLoadingReview
    {
        [Serializable] private sealed class Condition
        {
            public string name;
            public float massKg, cargoKg, ballastKg, storesKg, draftM, propellerImmersion, estimatedGmM;
            public Vector3 centreLocalM, inertiaKgM2;
            public float fullSpeedMps, stopM, stopSeconds;
        }
        [Serializable] private sealed class Report
        {
            public bool unchangedOtherVessels;
            public Condition[] conditions;
        }
        [Serializable] private sealed class Previous { public VolgoDonHandlingReview.Row[] rows; }

        public static void Run()
        {
            try
            {
                VesselData data = Read("VolgoDon507B");
                var rows = new List<Condition>();
                foreach (float fraction in new[] { 0f, .5f, 1f })
                    rows.Add(Measure("fraction " + fraction, data, LoadingModel.Initial(data, fraction)));
                var ballast = LoadingModel.Initial(data, 0);
                ballast.massKg[2] = ballast.massKg[3] = 1100000;
                ballast.massKg[4] = 200000;
                rows.Add(Measure("ballast plus stores", data, ballast));
                var fore = LoadingModel.Initial(data, .5f);
                fore.massKg[0] += 500000; fore.massKg[1] -= 500000;
                rows.Add(Measure("forward-biased cargo", data, fore));

                var before = JsonUtility.FromJson<Previous>(File.ReadAllText("Art/Source/VolgoDon507B/part6-after.json"));
                for (int i = 2; i < before.rows.Length; i++)
                {
                    var row = before.rows[i];
                    var after = VolgoDonHandlingReview.Measure(row.vessel, Read(row.vessel), float.PositiveInfinity);
                    if (JsonUtility.ToJson(row) != JsonUtility.ToJson(after))
                        throw new InvalidOperationException("Handling regression: " + row.vessel);
                }
                File.WriteAllText("Art/Source/VolgoDon507B/part7-loading.json", JsonUtility.ToJson(new Report
                    { unchangedOtherVessels = true, conditions = rows.ToArray() }, true));
                Debug.Log("VOLGODON_LOADING|PASS|other vessels unchanged");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static VesselData Read(string vessel) => JsonUtility.FromJson<VesselData>(
            File.ReadAllText("Assets/ShipSimulator/Data/Vessels/" + vessel + ".json"));

        private static Condition Measure(string name, VesselData data, LoadingState state)
        {
            var p = VesselParameters.Create(data, state);
            var stop = ManoeuvringTrials.CrashStop(p, float.PositiveInfinity);
            if (!stop.Completed) throw new InvalidOperationException("Stopping trial incomplete: " + name);
            return new Condition
            {
                name = name, massKg = p.Mass, cargoKg = state.massKg[0] + state.massKg[1],
                ballastKg = state.massKg[2] + state.massKg[3], storesKg = state.massKg[4],
                draftM = p.Draft, centreLocalM = p.CentreOfMassLocalM,
                inertiaKgM2 = new Vector3(p.PitchInertia, p.YawInertiaAtG, p.RollInertia),
                propellerImmersion = p.PropellerImmersion, estimatedGmM = new HydrostaticsModel(p).MetacentricHeightM,
                fullSpeedMps = ManoeuvringTrials.SteadySpeed(p, 1, float.PositiveInfinity),
                stopM = stop.TrackReachM, stopSeconds = stop.TimeS
            };
        }
    }
}
