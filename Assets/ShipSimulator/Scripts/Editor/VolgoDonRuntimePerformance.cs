using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Editor
{
    [InitializeOnLoad]
    public static class VolgoDonRuntimePerformance
    {
        private const string Key = "ShipSimulator.Part8Performance";
        private static int lastFrame = -1;
        private static double previous;
        private static readonly List<double> intervals = new();
        private static readonly List<double> cpu = new();
        private static readonly List<double> gpu = new();
        private static readonly FrameTiming[] timing = new FrameTiming[1];

        static VolgoDonRuntimePerformance() { EditorApplication.update += Tick; }

        public static void Run()
        {
            SessionState.SetBool(Key, true);
            VesselShakedownCheck.Run();
        }

        [Serializable] private sealed class Report
        {
            public string device, processor;
            public string method = "First 600 advancing player frames after 60 warmups during full five-vessel shakedown. River scenario, editor wall intervals, fixed simulation capture step. CPU/GPU timing zero means unavailable. Not an RTX 3060 acceptance test.";
            public int width, height, samples, cpuSamples, gpuSamples, drawCalls, setPassCalls;
            public double wallMedianMs, wallP95Ms, cpuMedianMs, gpuMedianMs;
        }

        private static double Percentile(List<double> data, double fraction)
        {
            if (data.Count == 0) return 0;
            data.Sort();
            return data[Math.Min(data.Count - 1, (int)(data.Count * fraction))];
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            var ship = UnityEngine.Object.FindAnyObjectByType<ShipPhysicsController>();
            if (ship == null || ship.ThrottleCommand < .9f) { previous = 0; return; }
            double now = EditorApplication.timeSinceStartup;
            if (previous > 0) intervals.Add((now - previous) * 1000);
            previous = now;
            FrameTimingManager.CaptureFrameTimings();
            if (intervals.Count > 60 && FrameTimingManager.GetLatestTimings(1, timing) > 0)
            {
                if (timing[0].cpuFrameTime > 0) cpu.Add(timing[0].cpuFrameTime);
                if (timing[0].gpuFrameTime > 0) gpu.Add(timing[0].gpuFrameTime);
            }
            if (intervals.Count < 660) return;
            var samples = intervals.Skip(60).ToList();
            var report = new Report { device = SystemInfo.graphicsDeviceName, processor = SystemInfo.processorType,
                width = Screen.width, height = Screen.height, samples = samples.Count, cpuSamples = cpu.Count, gpuSamples = gpu.Count,
                drawCalls = UnityStats.drawCalls, setPassCalls = UnityStats.setPassCalls,
                wallMedianMs = Percentile(samples, .5), wallP95Ms = Percentile(samples, .95),
                cpuMedianMs = Percentile(cpu, .5), gpuMedianMs = Percentile(gpu, .5) };
            File.WriteAllText("Art/Source/VolgoDon507B/part8-runtime-performance.json", JsonUtility.ToJson(report, true));
            SessionState.SetBool(Key, false);
            Debug.Log("VOLGODON_PERFORMANCE|RECORDED");
        }
    }
}
