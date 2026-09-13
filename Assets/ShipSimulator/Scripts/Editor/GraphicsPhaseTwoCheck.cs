using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    [InitializeOnLoad]
    public static class GraphicsPhaseTwoCheck
    {
        private const string Key = "ShipSimulator.PhaseTwoCheck";
        private static readonly string[] Conditions = { "clear", "dawn", "fog", "rain", "night" };
        private static readonly string[] Poses = { "hull-bank", "toward-sun", "away-sun", "current-boundary", "bank-bed" };
        private static readonly List<double> cpu = new List<double>(), gpu = new List<double>(), wall = new List<double>();
        private static readonly FrameTiming[] timing = new FrameTiming[1];
        private static Camera camera;
        private static RenderTexture target;
        private static ShipPhysicsController ship;
        private static string directory;
        private static int stage, frames, lastFrame;
        private static double deadline, lastTick;
        private static ulong lastTiming;
        private static bool failed;
        static GraphicsPhaseTwoCheck()
        {
            deadline = EditorApplication.timeSinceStartup + 900;
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetInt(Key, 0) != 0 && (type == LogType.Error || type == LogType.Exception)) failed = true;
            };
        }
        public static void RunBefore() => Run("before");
        public static void RunAfter() => Run("after");
        public static void RunSkyProbe() => Run("sky-probe");
        private static void Run(string label)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated graphics-enabled batch editor.");
            string output = "Logs/GraphicsPhaseTwo/" + label;
            if (Directory.Exists(output)) throw new InvalidOperationException("Preserve or rename the existing comparison directory first: " + output);
            SessionState.SetString(Key + ".Output", output);
            SessionState.SetInt(Key, 1);
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity");
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if (SessionState.GetInt(Key, 0) == 0) return;
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Phase 2 capture timeout.");
                if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
                lastFrame = Time.frameCount;
                Application.runInBackground = true;
                var menu = Object.FindAnyObjectByType<VoyageMenu>();
                if (menu == null || !menu.IsReady) return;
                if (camera == null)
                {
                    menu.StartVoyage("GorodetsTrainingScene");
                    QualitySettings.SetQualityLevel(1);
                    QualitySettings.vSyncCount = 0;
                    Application.targetFrameRate = -1;
                    Time.captureDeltaTime = 1f / 60;
                    UnityEngine.Random.InitState(159);
                    ship = Object.FindAnyObjectByType<ShipPhysicsController>();
                    ship.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                    camera = Camera.main;
                    camera.GetComponent<ShipFollowCamera>().enabled = false;
                    camera.fieldOfView = 60;
                    camera.aspect = 16f / 9;
                    target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGBHalf);
                    camera.targetTexture = target;
                    directory = SessionState.GetString(Key + ".Output", "");
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(directory + "/frame-times.csv",
                        "condition,pose,gpu,api,width,height,frames,cpu_samples,gpu_samples,cpu_median_ms,cpu_p95_ms,gpu_median_ms,gpu_p95_ms,editor_median_ms,editor_p95_ms\n");
                    File.WriteAllText(directory + "/metadata.txt", "Unity " + Application.unityVersion + "\nGPU: " + SystemInfo.graphicsDeviceName +
                        "\nAPI: " + SystemInfo.graphicsDeviceType + "\nResolution: 1920x1080; PC quality; TAA; VSync off\n" +
                        "300 frames per pose after 120 warmup frames. Fixed simulation step 1/60 s. GPU/CPU unavailable fields are NA.\n" +
                        "Editor intervals include editor scheduling; they are not isolated GPU costs. Development-machine comparison only.\n");
                    SetStage(directory.EndsWith("sky-probe") ? 1 : 0);
                    if (directory.EndsWith("sky-probe"))
                    {
                        foreach (RiverPlanarReflection planar in Object.FindObjectsByType<RiverPlanarReflection>()) planar.enabled = false;
                        Shader.SetGlobalFloat("_RiverOpticsProbe", 1);
                    }
                    return;
                }
                double now = EditorApplication.timeSinceStartup;
                FrameTimingManager.CaptureFrameTimings();
                if (frames >= 120 && frames < 420)
                {
                    wall.Add((now - lastTick) * 1000);
                    if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != lastTiming)
                    {
                        lastTiming = timing[0].frameStartTimestamp;
                        if (timing[0].cpuFrameTime > 0) cpu.Add(timing[0].cpuFrameTime);
                        if (timing[0].gpuFrameTime > 0) gpu.Add(timing[0].gpuFrameTime);
                    }
                }
                lastTick = now;
                if (++frames < 420) return;
                if ((stage % 5 == 1 || stage % 5 == 2) && frames < 436)
                {
                    CaptureTemporal(Conditions[stage / 5] + "-" + Poses[stage % 5] + "-" + (frames - 420).ToString("D2"));
                    return;
                }
                string condition = Conditions[stage / 5], pose = Poses[stage % 5];
                Capture(condition + "-" + pose);
                File.AppendAllText(directory + "/frame-times.csv", string.Join(",", condition, pose,
                    "\"" + SystemInfo.graphicsDeviceName + "\"", SystemInfo.graphicsDeviceType, 1920, 1080, wall.Count, cpu.Count, gpu.Count,
                    Percentile(cpu, 0.5), Percentile(cpu, 0.95), Percentile(gpu, 0.5), Percentile(gpu, 0.95), Percentile(wall, 0.5), Percentile(wall, 0.95)) + "\n");
                Debug.Log("PHASE_TWO_CAPTURE|" + condition + "|" + pose + "|CPU=" + Percentile(cpu, 0.5) + "|GPU=" + Percentile(gpu, 0.5));
                if (directory.EndsWith("sky-probe")) stage = 25;
                if (++stage < 25) { SetStage(stage); return; }
                SessionState.SetInt(Key, 0);
                Debug.Log("GRAPHICS_PHASE_TWO|" + (failed ? "FAIL" : "PASS") + "|" + directory);
                EditorApplication.Exit(failed ? 1 : 0);
            }
            catch (Exception error)
            {
                SessionState.SetInt(Key, 0);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
        private static void SetStage(int index)
        {
            stage = index; frames = 0; cpu.Clear(); gpu.Clear(); wall.Clear(); lastTiming = 0;
            int condition = index / 5, pose = index % 5;
            Object.FindAnyObjectByType<DayNightController>().Apply(condition == 4);
            Object.FindAnyObjectByType<WeatherController>().Configure(300, 4, condition == 3 ? 0.8f : 0, condition == 2 ? 0.65f : 0);
            if (condition == 1)
            {
                RenderSettings.sun.transform.rotation = Quaternion.Euler(7, 155, 0);
                RenderSettings.sun.color = new Color(1, 0.72f, 0.45f);
                RenderSettings.sun.intensity = 0.65f;
            }
            else RenderSettings.sun.color = condition == 4 ? new Color(0.65f, 0.76f, 1) : Color.white;
            RiverLighting.Active.Refresh();
            if (condition == 1) RenderSettings.sun.intensity = 0.65f;
            Vector3 center = ship.transform.position;
            Vector3 toward = -RenderSettings.sun.transform.forward; toward.y = 0; toward.Normalize();
            if (pose == 0)
            {
                camera.transform.position = center + new Vector3(23, 3.5f, -48);
                camera.transform.LookAt(center + new Vector3(0, -0.6f, -36));
            }
            else if (pose == 1 || pose == 2)
            {
                camera.transform.position = center + new Vector3(15, 4, 85);
                camera.transform.LookAt(camera.transform.position + toward * (pose == 1 ? 200 : -200) + Vector3.down * 5);
            }
            else if (pose == 3)
            {
                camera.transform.position = new Vector3(0, 110, 360);
                camera.transform.LookAt(new Vector3(0, 0, 360), Vector3.forward);
            }
            else
            {
                Texture2D profile = (Texture2D)Shader.GetGlobalTexture("_RiverShoreProfile");
                Vector4 range = Shader.GetGlobalVector("_RiverShoreRange");
                float z = center.z + 85;
                float left = profile.GetPixelBilinear((z - range.x) / range.y, 0.5f).r;
                camera.transform.position = new Vector3(left + 5, 4, z - 4);
                camera.transform.LookAt(new Vector3(left + 1.5f, -0.25f, z + 3));
            }
            File.AppendAllText(directory + "/poses.txt", Conditions[condition] + " " + Poses[pose] + " position=" + camera.transform.position.ToString("F3") +
                " rotation=" + camera.transform.eulerAngles.ToString("F3") + "\n");
        }
        private static string Percentile(List<double> values, double fraction)
        {
            if (values.Count == 0) return "NA";
            values.Sort();
            return values[Math.Min(values.Count - 1, (int)((values.Count - 1) * fraction))].ToString("F4", CultureInfo.InvariantCulture);
        }
        private static void CaptureTemporal(string name)
        {
            Directory.CreateDirectory(directory + "/temporal");
            RenderTexture previous = RenderTexture.active;
            var ldr = RenderTexture.GetTemporary(target.width, target.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(target, ldr); RenderTexture.active = ldr;
            var image = new Texture2D(480, 160, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(720, 150, 480, 160), 0, 0); image.Apply();
            File.WriteAllBytes(directory + "/temporal/" + name + ".png", image.EncodeToPNG());
            Object.DestroyImmediate(image); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(ldr);
        }
        private static void Capture(string name)
        {
            RenderTexture previous = RenderTexture.active;
            var ldr = RenderTexture.GetTemporary(target.width, target.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(target, ldr); RenderTexture.active = ldr;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            File.WriteAllBytes(directory + "/" + name + ".png", image.EncodeToPNG());
            Object.DestroyImmediate(image); RenderTexture.active = previous; RenderTexture.ReleaseTemporary(ldr);
        }
    }
}
