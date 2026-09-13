using System;
using System.Collections.Generic;
using System.IO;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    [InitializeOnLoad]
    public static class GraphicsPhaseOneCheck
    {
        private const string Key = "ShipSimulator.PhaseOneCheck";
        private static double deadline;
        private static int lastFrame = -1;
        private static int frames;
        private static double lastTick;
        private static Camera camera;
        private static RenderTexture target;
        private static Vector3 origin;
        private static Vector3 lookAt;
        private static readonly List<double> timings = new List<double>();
        private static bool failed;
        private static float clearSun;
        static GraphicsPhaseOneCheck()
        {
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetInt(Key, 0) > 0 && (type == LogType.Error || type == LogType.Exception)) failed = true;
            };
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch editor.");
            SessionState.SetInt(Key, 1);
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity");
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0) return;
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Phase 1 check timed out.");
                if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
                lastFrame = Time.frameCount;
                Application.runInBackground = true;
                var menu = Object.FindAnyObjectByType<VoyageMenu>();
                if (menu == null || !menu.IsReady) return;
                if (stage == 1)
                {
                    menu.StartVoyage("GorodetsTrainingScene");
                    QualitySettings.SetQualityLevel(1);
                    QualitySettings.realtimeReflectionProbes = true;
                    QualitySettings.vSyncCount = 0;
                    camera = Camera.main;
                    camera.GetComponent<ShipFollowCamera>().enabled = false;
                    var ship = Object.FindAnyObjectByType<ShipPhysicsController>();
                    ship.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                    origin = ship.transform.position + new Vector3(36, 19, -85);
                    lookAt = ship.transform.position + new Vector3(0, 3, 50);
                    target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGBHalf) { name = "Phase 1 capture" };
                    camera.targetTexture = target;
                    camera.allowMSAA = false;
                    camera.transform.position = origin;
                    camera.transform.LookAt(lookAt);
                    Directory.CreateDirectory("Logs/GraphicsPhaseOne");
                    SetStage(2);
                    RiverLighting.Active.enabled = false;
                    camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    Shader.SetGlobalFloat("_RiverCloudShadowDisable", 1);
                    return;
                }
                double now = EditorApplication.timeSinceStartup;
                if (frames > 30) timings.Add((now - lastTick) * 1000);
                lastTick = now;
                frames++;
                if (stage == 2 || stage == 3)
                {
                    camera.transform.position = origin + new Vector3(Mathf.Sin(frames * 0.008f) * 6, 0, 0);
                    camera.transform.LookAt(lookAt);
                }
                if (frames == 114 || frames == 116 || frames == 118)
                    Capture("sequence-" + stage + "-" + frames);
                if (frames < 120) return;
                string label = stage == 2 ? "smaa-baseline" : stage == 3 ? "taa-day" : stage == 4 ? "taa-rain" : "taa-night";
                Capture(label);
                timings.Sort();
                Debug.Log($"PHASE_ONE_TIMING|{label}|editor frame interval median={timings[timings.Count / 2]:F2} ms p95={timings[(int)(timings.Count * 0.95)]:F2} ms|1600x900; includes editor and readback scheduling, not isolated GPU time");
                if (stage == 2)
                {
                    Object.FindAnyObjectByType<WeatherController>().GetComponent<RiverLighting>().enabled = true;
                    camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.TemporalAntiAliasing;
                    Shader.SetGlobalFloat("_RiverCloudShadowDisable", 0);
                }
                else
                {
                    RiverLighting light = RiverLighting.Active;
                    if (light == null || light.MeterSamples < 2 || light.ProbeUpdates < 1 || RenderSettings.customReflectionTexture == null)
                        throw new InvalidOperationException($"Exposure meter or sky probe did not run: meter={light?.MeterSamples}, probes={light?.ProbeUpdates}, reflection={RenderSettings.customReflectionTexture}, quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
                    Debug.Log($"PHASE_ONE_STATE|{label}|exposure={light.ExposureEV:F2} EV|meter={light.MeterSamples}|probes={light.ProbeUpdates}");
                    if (stage == 3)
                    {
                        if (RiverTemporalFeature.RenderedFrames < 30) throw new InvalidOperationException("Water motion vectors were not rendered.");
                        Debug.Log($"PHASE_ONE_TEMPORAL|water motion frames={RiverTemporalFeature.RenderedFrames}|GPU={SystemInfo.graphicsDeviceName}");
                        clearSun = RenderSettings.sun.intensity;
                        Object.FindAnyObjectByType<WeatherController>().Configure(315, 12, 0.7f, 0.35f);
                    }
                    if (stage == 4)
                    {
                        if (RenderSettings.sun.intensity >= clearSun * 0.7f) throw new InvalidOperationException("Overcast did not dim direct light.");
                        Object.FindAnyObjectByType<DayNightController>().Apply(true);
                    }
                    if (stage == 5)
                    {
                        SessionState.SetInt(Key, 0);
                        camera.targetTexture = null;
                        Object.Destroy(target);
                        Debug.Log("GRAPHICS_PHASE_ONE|" + (failed ? "FAIL" : "PASS") + ": exposure, live sky, TAA fly-through, rain and night");
                        EditorApplication.Exit(failed ? 1 : 0);
                        return;
                    }
                }
                SetStage(stage + 1);
            }
            catch (Exception error)
            {
                SessionState.SetInt(Key, 0);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
        private static void SetStage(int stage)
        {
            SessionState.SetInt(Key, stage);
            frames = 0;
            timings.Clear();
            if (camera != null) camera.GetUniversalAdditionalCameraData().resetHistory = true;
        }
        private static void Capture(string label)
        {
            var previous = RenderTexture.active;
            var ldr = RenderTexture.GetTemporary(target.width, target.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(target, ldr);
            RenderTexture.active = ldr;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/GraphicsPhaseOne/" + label + ".png", image.EncodeToPNG());
            Object.Destroy(image);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(ldr);
        }
    }
}
