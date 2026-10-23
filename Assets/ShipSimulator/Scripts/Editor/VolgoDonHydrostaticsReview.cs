using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // Part 11 load cases, out-of-table probes, other-vessel regression and loading-rebuild cost.
    // Writes part11-loading.json and part11-loading-cost.json and ends with VOLGODON_HYDROSTATICS|PASS.
    public static class VolgoDonHydrostaticsReview
    {
        private const string Folder = "Art/Source/VolgoDon507B/";

        [Serializable] private sealed class Condition
        {
            public string name, range;
            public float massKg, cargoKg, ballastKg, storesKg;
            public Vector3 centreLocalM, inertiaKgM2;
            public float draftM, trimM, forwardDraftM, aftDraftM, kbM, bmtM, gmSolidM, waterplaneM2, lcfM;
            public float propellerImmersion, wallSidedDraftM, wallSidedGmM;
            public float fullSpeedMps, stopM, stopSeconds;
        }
        [Serializable] private sealed class Probe { public string name, range; public float draftM, trimM, volumeM3; }
        [Serializable] private sealed class Report
        {
            public string units, classification;
            public bool unchangedOtherVessels;
            public Condition[] conditions;
            public Probe[] outOfRange;
        }
        [Serializable] private sealed class Cost
        {
            public string scope, allocationMeasure;
            public int iterations;
            public float geometryBuildMs, coldParameterSolveMs, warmParameterSolveMs, cachedRebuildMs, wallSidedRebuildMs;
            public int geometryBuildAllocations, cachedRebuildAllocations, wallSidedRebuildAllocations;
            public float sectionApplyMicroseconds, wallSidedApplyMicroseconds;
            public int sectionApplyAllocations, wallSidedApplyAllocations;
            public int cells, wallSidedCells;
        }

        public static void Run()
        {
            try
            {
                VesselData data = Read("VolgoDon507B");
                var rows = new List<Condition>
                {
                    Measure("full load", data, LoadingModel.Initial(data, 1)),
                    Measure("half deadweight", data, LoadingModel.Initial(data, .5f)),
                    Measure("lightship", data, LoadingModel.Initial(data, 0))
                };
                var ballast = LoadingModel.Initial(data, 0);
                ballast.massKg[2] = ballast.massKg[3] = 1100000;
                ballast.massKg[4] = 200000;
                rows.Add(Measure("ballast, both groups, stores", data, ballast));
                var forwardBallast = LoadingModel.Initial(data, 0);
                forwardBallast.massKg[2] = 1100000;
                forwardBallast.massKg[4] = 200000;
                rows.Add(Measure("forward ballast group, stores", data, forwardBallast));
                var fore = LoadingModel.Initial(data, 0);
                fore.massKg[0] = 2400000; fore.massKg[4] = 200000;
                rows.Add(Measure("forward hold only", data, fore));
                var aft = LoadingModel.Initial(data, 0);
                aft.massKg[1] = 2400000; aft.massKg[4] = 200000;
                rows.Add(Measure("aft hold only", data, aft));
                var biased = LoadingModel.Initial(data, .5f);
                biased.massKg[0] += 500000; biased.massKg[1] -= 500000;
                rows.Add(Measure("half deadweight, 1000 t moved forward", data, biased));

                SectionHydrostatics sections = SectionHydrostatics.For(data);
                var probes = new List<Probe>
                {
                    Solve("overload beyond capped volume", sections, 1.2f * sections.Capped.VolumeM3, 0, 2.5f),
                    Solve("100 t", sections, 100, 0, 3),
                    Solve("LCG 30 m forward, lightship mass", sections, 1750, 30, 3.53f),
                    Solve("NaN centre", sections, 4000, float.NaN, 3),
                    Solve("zero volume", sections, 0, 0, 3)
                };
                foreach (Probe probe in probes)
                    if (!Finite(probe.draftM) || !Finite(probe.trimM) || !Finite(probe.volumeM3) || probe.volumeM3 < 0)
                        throw new InvalidOperationException("Non-finite out-of-range result: " + probe.name);

                var previous = JsonUtility.FromJson<HandlingReport>(File.ReadAllText(Folder + "part6-after.json"));
                for (int i = 2; i < previous.rows.Length; i++)
                {
                    VolgoDonHandlingReview.Row row = previous.rows[i];
                    var after = VolgoDonHandlingReview.Measure(row.vessel, Read(row.vessel), float.PositiveInfinity);
                    if (JsonUtility.ToJson(row) != JsonUtility.ToJson(after))
                        throw new InvalidOperationException("Handling regression: " + row.vessel);
                }

                File.WriteAllText(Folder + "part11-loading.json", JsonUtility.ToJson(new Report
                {
                    units = "kg, m, m2, kg m2, m/s, s; drafts above keel; trim over Lpp, bow down positive; " +
                        "longitudinal positions forward positive",
                    classification = "Prototype estimates from a derived hull surface and estimated inventory; " +
                        "solid-mass GM without free-surface correction; not a stability booklet",
                    unchangedOtherVessels = true, conditions = rows.ToArray(), outOfRange = probes.ToArray()
                }, true));
                File.WriteAllText(Folder + "part11-loading-cost.json", JsonUtility.ToJson(Profile(data), true));
                Debug.Log("VOLGODON_HYDROSTATICS|PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [Serializable] private sealed class HandlingReport { public VolgoDonHandlingReview.Row[] rows; }

        private static VesselData Read(string vessel) => JsonUtility.FromJson<VesselData>(
            File.ReadAllText("Assets/ShipSimulator/Data/Vessels/" + vessel + ".json"));

        private static VesselData WallSided(VesselData data)
        {
            VesselData copy = JsonUtility.FromJson<VesselData>(JsonUtility.ToJson(data));
            copy.hydrostatics.sectionTable.enabled = false;
            return copy;
        }

        private static Condition Measure(string name, VesselData data, LoadingState state)
        {
            var p = VesselParameters.Create(data, state);
            var legacy = VesselParameters.Create(WallSided(data), state);
            var hydrostatics = new HydrostaticsModel(p);
            var stop = ManoeuvringTrials.CrashStop(p, float.PositiveInfinity);
            if (!stop.Completed) throw new InvalidOperationException("Stopping trial incomplete: " + name);
            HydrostaticState e = p.Equilibrium;
            if (Mathf.Abs(e.VolumeM3 * p.Rho - p.Mass) > 1e-4f * p.Mass)
                throw new InvalidOperationException("Equilibrium does not displace the mass: " + name);
            return new Condition
            {
                name = name, range = p.HydrostaticRange.ToString(), massKg = p.Mass,
                cargoKg = state.massKg[0] + state.massKg[1], ballastKg = state.massKg[2] + state.massKg[3],
                storesKg = state.massKg[4], centreLocalM = p.CentreOfMassLocalM,
                inertiaKgM2 = new Vector3(p.PitchInertia, p.YawInertiaAtG, p.RollInertia),
                draftM = p.Draft, trimM = p.TrimM, forwardDraftM = p.DraftAt(.5f * p.Lpp), aftDraftM = p.DraftAt(-.5f * p.Lpp),
                kbM = e.KbM, bmtM = e.TransverseInertiaM4 / e.VolumeM3, gmSolidM = hydrostatics.MetacentricHeightM,
                waterplaneM2 = e.WaterplaneAreaM2, lcfM = e.LcfM, propellerImmersion = p.PropellerImmersion,
                wallSidedDraftM = legacy.Draft, wallSidedGmM = new HydrostaticsModel(legacy).MetacentricHeightM,
                fullSpeedMps = ManoeuvringTrials.SteadySpeed(p, 1, float.PositiveInfinity),
                stopM = stop.TrackReachM, stopSeconds = stop.TimeS
            };
        }

        private static Probe Solve(string name, SectionHydrostatics sections, float volume, float lcg, float kg)
        {
            HydrostaticRange range = sections.Solve(volume, lcg, kg, 135, out float draft, out float slope, out HydrostaticState s);
            return new Probe { name = name, range = range.ToString(), draftM = draft, trimM = slope * 135, volumeM3 = s.VolumeM3 };
        }

        private static Cost Profile(VesselData data)
        {
            const int iterations = 1000;
            var cost = new Cost
            {
                iterations = iterations,
                scope = "Editor thread, warmed. Rebuild = parameters (warm-started equilibrium solve), manoeuvring model, " +
                    "hydrostatics model and station array, as ShipPhysicsController.ApplyLoading does per transfer step; " +
                    "Rigidbody assignment excluded. Apply = one hydrostatic force evaluation on a Rigidbody.",
                allocationMeasure = "GC.Alloc profiler samples on this thread (Recorder), per operation"
            };
            var timer = new Stopwatch();

            timer.Restart();
            for (int i = 0; i < 20; i++) GC.KeepAlive(new SectionHydrostatics(data));
            cost.geometryBuildMs = (float)timer.Elapsed.TotalMilliseconds / 20;
            cost.geometryBuildAllocations = Allocations(() => GC.KeepAlive(new SectionHydrostatics(data)));

            LoadingState state = LoadingModel.Initial(data, .5f);
            VesselParameters p = VesselParameters.Create(data, state);
            timer.Restart();
            for (int i = 0; i < iterations; i++)
            {
                state.massKg[2] = i % 2 == 0 ? 1000 : 0;
                GC.KeepAlive(VesselParameters.Create(data, state));
            }
            cost.coldParameterSolveMs = (float)timer.Elapsed.TotalMilliseconds / iterations;
            timer.Restart();
            for (int i = 0; i < iterations; i++)
            {
                state.massKg[2] = i % 2 == 0 ? 1000 : 0;
                p = VesselParameters.Create(data, state, p);
            }
            cost.warmParameterSolveMs = (float)timer.Elapsed.TotalMilliseconds / iterations;

            cost.cachedRebuildMs = Rebuild(data, state, iterations, out cost.cachedRebuildAllocations);
            VesselData legacy = WallSided(data);
            cost.wallSidedRebuildMs = Rebuild(legacy, state, iterations, out cost.wallSidedRebuildAllocations);

            cost.sectionApplyMicroseconds = ApplyCost(VesselParameters.Create(data, state), iterations,
                out cost.sectionApplyAllocations, out cost.cells);
            cost.wallSidedApplyMicroseconds = ApplyCost(VesselParameters.Create(legacy, state), iterations,
                out cost.wallSidedApplyAllocations, out cost.wallSidedCells);
            if (Allocations(() => GC.KeepAlive(new float[64])) == 0)
                cost.allocationMeasure = "unavailable: the GC.Alloc recorder did not register a known allocation";
            return cost;
        }

        private static float Rebuild(VesselData data, LoadingState state, int iterations, out int allocations)
        {
            ManoeuvringModel model = new ManoeuvringModel(VesselParameters.Create(data, state));
            void Once(int i)
            {
                state.massKg[2] = i % 2 == 0 ? 1000 : 0;
                VesselParameters parameters = VesselParameters.Create(data, state, model.Parameters);
                model = new ManoeuvringModel(parameters, model);
                GC.KeepAlive(new HydrostaticsModel(parameters));
                GC.KeepAlive(new float[model.Hull.StationCount]);
            }
            for (int i = 0; i < 20; i++) Once(i);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) Once(i);
            float ms = (float)timer.Elapsed.TotalMilliseconds / iterations;
            allocations = Allocations(() => Once(1));
            return ms;
        }

        private static float ApplyCost(VesselParameters p, int iterations, out int allocations, out int cells)
        {
            var ship = new GameObject("Part11 apply probe");
            try
            {
                Rigidbody body = ship.AddComponent<Rigidbody>();
                body.mass = p.Mass;
                body.centerOfMass = p.CentreOfMassLocalM;
                ship.transform.position = new Vector3(0f, p.LoadedDraft - p.Draft, 0f);
                var hydrostatics = new HydrostaticsModel(p);
                cells = hydrostatics.CellCount;
                for (int i = 0; i < 20; i++) hydrostatics.Apply(body, ship.transform, 0f, 0f, 0f);
                var timer = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++) hydrostatics.Apply(body, ship.transform, 0f, 0f, 0f);
                float microseconds = (float)timer.Elapsed.TotalMilliseconds * 1000f / iterations;
                allocations = Allocations(() => hydrostatics.Apply(body, ship.transform, 0f, 0f, 0f));
                return microseconds;
            }
            finally { Object.DestroyImmediate(ship); }
        }

        private static int Allocations(Action action)
        {
            Recorder recorder = Recorder.Get("GC.Alloc");
            recorder.enabled = false;
            recorder.FilterToCurrentThread();
            recorder.enabled = true;
            try { action(); }
            finally
            {
                recorder.enabled = false;
                recorder.CollectFromAllThreads();
            }
            return recorder.sampleBlockCount;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
