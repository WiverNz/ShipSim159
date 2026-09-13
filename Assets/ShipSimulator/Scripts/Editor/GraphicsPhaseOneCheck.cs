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
        private const int Width = 1600;
        private const int Height = 900;
        // Pixel rows count from the bottom. Open river and the far tree line in the fixed check view.
        private static readonly RectInt WaterRegion = new RectInt(760, 110, 340, 190);
        private static readonly RectInt TreeLineRegion = new RectInt(0, 470, 1600, 110);
        private static double deadline;
        private static int lastFrame = -1;
        private static int frames;
        private static double lastTick;
        private static Camera camera;
        private static RenderTexture target;
        private static RenderTexture motionTarget;
        private static Vector3 origin;
        private static Vector3 lookAt;
        private static readonly List<double> timings = new List<double>();
        private static bool failed;
        private static float clearSun;
        private static (float water, float trees) animatedMotion;
        // Low view over open water ahead of the vessel, and its lower-middle water pixels.
        private static readonly RectInt WindRegion = new RectInt(400, 40, 800, 260);
        private static Vector3 windOrigin;
        private static Vector3 windLookAt;
        private static Color32[] windFrame;
        static GraphicsPhaseOneCheck()
        {
            deadline = EditorApplication.timeSinceStartup + 300;
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
                    windOrigin = ship.transform.position + new Vector3(18, 5, 80);
                    windLookAt = ship.transform.position + new Vector3(10, 0, 300);
                    target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGBHalf) { name = "Phase 1 capture" };
                    motionTarget = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGBHalf) { name = "Phase 1 motion vectors" };
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
                    camera.transform.position = origin + new Vector3(Mathf.Sin(frames * 0.008f) * 6, 0, 0);
                else if (stage == 5)
                    camera.transform.position = windOrigin;
                else
                    camera.transform.position = origin;
                camera.transform.LookAt(stage == 5 ? windLookAt : lookAt);
                if (stage == 5 && frames == 40) windFrame = Capture("wind-early");
                if (stage == 4 && frames == 100)
                {
                    animatedMotion = AnalyseMotion("taa-motion-vectors");
                    Shader.SetGlobalFloat("_RiverMotionProbe", 1);
                }
                if (frames == 114 || frames == 116 || frames == 118)
                    Capture("sequence-" + stage + "-" + frames);
                if (frames < 120) return;
                string label = stage == 2 ? "smaa-baseline" : stage == 3 ? "taa-day" : stage == 4 ? "taa-motion"
                    : stage == 5 ? "wind-late" : stage == 6 ? "taa-rain" : "taa-night";
                Color32[] frame = Capture(label);
                timings.Sort();
                Debug.Log($"PHASE_ONE_TIMING|{label}|editor frame interval median={timings[timings.Count / 2]:F2} ms p95={timings[(int)(timings.Count * 0.95)]:F2} ms|1600x900; includes editor and readback scheduling, not isolated GPU time");
                if (stage == 2)
                {
                    Object.FindAnyObjectByType<WeatherController>().GetComponent<RiverLighting>().enabled = true;
                    camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.TemporalAntiAliasing;
                    Shader.SetGlobalFloat("_RiverCloudShadowDisable", 0);
                    SetStage(3);
                    return;
                }

                RiverLighting light = RequireLighting(label);
                if (stage == 3)
                {
                    RequireDaylightAmbient(light);
                    if (RenderSettings.sun.cookie == null) throw new InvalidOperationException("The cloud shadow cookie was not assigned.");
                    RiverTemporalFeature.MotionCaptureTarget = motionTarget;
                }
                else if (stage == 4)
                {
                    Shader.SetGlobalFloat("_RiverMotionProbe", 0);
                    RiverTemporalFeature.MotionCaptureTarget = null;
                    float probe = AnalyseMotion("taa-motion-probe").water;
                    if (probe < 0.9f)
                        throw new InvalidOperationException($"The water motion pass does not reach the motion texture: probe covers {probe:P1}.");
                    if (animatedMotion.water < 0.3f)
                        throw new InvalidOperationException($"Water motion vectors are missing or below half precision: {animatedMotion.water:P1} of open water moves.");
                    if (animatedMotion.trees < 0.02f)
                        throw new InvalidOperationException($"Foliage motion vectors are missing: {animatedMotion.trees:P1} of the tree line moves.");
                    Object.FindAnyObjectByType<WeatherController>().Configure(250, 8, 0f, 0f);
                }
                else if (stage == 5)
                {
                    // Vessel and camera are still, so any change in the water comes from wind and ripples.
                    float change = MeanDifference(windFrame, frame, WindRegion);
                    var weather = Object.FindAnyObjectByType<WeatherController>();
                    Debug.Log($"PHASE_ONE_WIND|still water mean change={change:F4}|visual wind={weather.VisualWindMps}|configured={weather.WindVelocityMps}");
                    if (change < 0.004f) throw new InvalidOperationException($"Still water looks frozen: mean change {change:F4}.");
                    clearSun = RenderSettings.sun.intensity;
                    weather.Configure(315, 12, 0.7f, 0.35f);
                }
                else if (stage == 6)
                {
                    if (RenderSettings.sun.intensity >= clearSun * 0.7f) throw new InvalidOperationException("Overcast did not dim direct light.");
                    Object.FindAnyObjectByType<DayNightController>().Apply(true);
                }
                else
                {
                    // The clock is on the HUD object, not the weather object that carries RiverLighting.
                    if (!light.IsNight || RenderSettings.sun.intensity > 0.2801f)
                        throw new InvalidOperationException($"Night lighting was not applied: night={light.IsNight}, sun={RenderSettings.sun.intensity:F3}");
                    SessionState.SetInt(Key, 0);
                    camera.targetTexture = null;
                    Object.Destroy(target);
                    Object.Destroy(motionTarget);
                    Debug.Log("GRAPHICS_PHASE_ONE|" + (failed ? "FAIL" : "PASS") + ": exposure, sky ambient, cloud cookie, motion vectors, TAA, still water, rain and night");
                    EditorApplication.Exit(failed ? 1 : 0);
                    return;
                }
                SetStage(stage + 1);
            }
            catch (Exception error)
            {
                SessionState.SetInt(Key, 0);
                RiverTemporalFeature.MotionCaptureTarget = null;
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
        private static RiverLighting RequireLighting(string label)
        {
            RiverLighting light = RiverLighting.Active;
            if (light == null || light.MeterSamples < 2 || light.ProbeUpdates < 1 || RenderSettings.customReflectionTexture == null)
                throw new InvalidOperationException($"Exposure meter or sky probe did not run: meter={light?.MeterSamples}, probes={light?.ProbeUpdates}, reflection={RenderSettings.customReflectionTexture}, quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            Debug.Log($"PHASE_ONE_STATE|{label}|exposure={light.ExposureEV:F2} EV|meter={light.MeterSamples}|probes={light.ProbeUpdates}|ambient={light.AmbientUpdates}|night={light.IsNight}|sun={RenderSettings.sun.intensity:F3}");
            return light;
        }
        private static void RequireDaylightAmbient(RiverLighting light)
        {
            var colors = new Color[2];
            RenderSettings.ambientProbe.Evaluate(new[] { Vector3.up, Vector3.down }, colors);
            Debug.Log($"PHASE_ONE_AMBIENT|up={colors[0]}|down={colors[1]}|updates={light.AmbientUpdates}");
            if (light.AmbientUpdates < 1 || colors[0].grayscale <= colors[1].grayscale)
                throw new InvalidOperationException("Sky ambient was not projected from the sky capture.");
        }
        private static (float water, float trees) AnalyseMotion(string label)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = motionTarget;
            var motion = new Texture2D(Width, Height, TextureFormat.RGBAHalf, false);
            motion.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            motion.Apply();
            RenderTexture.active = previous;
            Color[] pixels = motion.GetPixels();
            Object.Destroy(motion);

            // Logarithmic preview: black is still, white is 1e-3 of the screen per frame or more.
            var preview = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            var levels = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                float magnitude = new Vector2(pixels[i].r, pixels[i].g).magnitude;
                byte level = (byte)(Mathf.Clamp01((Mathf.Log10(Mathf.Max(magnitude, 1e-9f)) + 7) / 4) * 255);
                levels[i] = new Color32(level, level, level, 255);
            }
            preview.SetPixels32(levels);
            preview.Apply();
            File.WriteAllBytes("Logs/GraphicsPhaseOne/" + label + ".png", preview.EncodeToPNG());
            Object.Destroy(preview);

            float water = MovingFraction(pixels, WaterRegion, out float waterPeak);
            float trees = MovingFraction(pixels, TreeLineRegion, out float treePeak);
            Debug.Log($"PHASE_ONE_MOTION|{label}|static camera|water moving={water:P1} peak={waterPeak:E2}|tree line moving={trees:P1} peak={treePeak:E2}");
            return (water, trees);
        }
        private static float MovingFraction(Color[] pixels, RectInt region, out float peak)
        {
            int moving = 0;
            peak = 0;
            for (int y = region.yMin; y < region.yMax; y++)
            for (int x = region.xMin; x < region.xMax; x++)
            {
                Color value = pixels[y * Width + x];
                float squared = value.r * value.r + value.g * value.g;
                peak = Mathf.Max(peak, Mathf.Sqrt(squared));
                if (squared > 1e-14f) moving++;
            }
            return moving / (float)(region.width * region.height);
        }
        private static void SetStage(int stage)
        {
            SessionState.SetInt(Key, stage);
            frames = 0;
            timings.Clear();
            if (camera != null) camera.GetUniversalAdditionalCameraData().resetHistory = true;
        }
        private static float MeanDifference(Color32[] first, Color32[] second, RectInt region)
        {
            double sum = 0;
            for (int y = region.yMin; y < region.yMax; y++)
            for (int x = region.xMin; x < region.xMax; x++)
            {
                int i = y * Width + x;
                sum += Mathf.Abs(first[i].r + first[i].g + first[i].b - second[i].r - second[i].g - second[i].b) / 765f;
            }
            return (float)(sum / (region.width * region.height));
        }
        private static Color32[] Capture(string label)
        {
            var previous = RenderTexture.active;
            var ldr = RenderTexture.GetTemporary(target.width, target.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(target, ldr);
            RenderTexture.active = ldr;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes("Logs/GraphicsPhaseOne/" + label + ".png", image.EncodeToPNG());
            Color32[] pixels = image.GetPixels32();
            Object.Destroy(image);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(ldr);
            return pixels;
        }
    }
}
