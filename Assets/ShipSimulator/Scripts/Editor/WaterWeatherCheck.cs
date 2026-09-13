using System;
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
    public static class WaterWeatherCheck
    {
        private const string Key = "ShipSimulator.WaterWeatherCheck";
        private static Camera camera;
        private static RenderTexture target;
        private static ShipPhysicsController ship;
        private static WeatherController weather;
        private static int frames, lastFrame;
        private static double deadline;
        private static bool failed;
        private static float clearContrast;
        static WaterWeatherCheck()
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
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Water/weather check timed out.");
                if (!EditorApplication.isPlaying || lastFrame == Time.frameCount) return;
                lastFrame = Time.frameCount;
                Application.runInBackground = true;
                var menu = Object.FindAnyObjectByType<VoyageMenu>();
                if (menu == null || !menu.IsReady) return;
                if (stage == 1)
                {
                    menu.StartVoyage("GorodetsTrainingScene");
                    QualitySettings.SetQualityLevel(1);
                    QualitySettings.vSyncCount = 0;
                    ship = Object.FindAnyObjectByType<ShipPhysicsController>();
                    ship.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                    camera = Camera.main;
                    camera.GetComponent<ShipFollowCamera>().enabled = false;
                    target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGBHalf);
                    camera.targetTexture = target;
                    weather = Object.FindAnyObjectByType<WeatherController>();
                    Object.FindAnyObjectByType<DayNightController>().Apply(false);
                    weather.Configure(300, 3, 0, 0);
                    camera.transform.position = ship.transform.position + new Vector3(18, 5, 80);
                    camera.transform.LookAt(ship.transform.position + new Vector3(10, 0, 300));
                    Directory.CreateDirectory("Logs/WaterWeather");
                    Next(2);
                    return;
                }
                if (stage >= 6)
                {
                    foreach (Light light in ship.GetComponentInChildren<NavigationLightRig>().GetComponentsInChildren<Light>())
                        if (!light.enabled) throw new InvalidOperationException("Running light blinked off at night.");
                }
                if (++frames < 100) return;
                string label = stage == 2 ? "clear" : stage == 3 ? "fog" : stage == 4 ? "rain"
                    : stage == 5 ? "bank-wake" : "night-" + (stage - 6);
                Color32[] pixels = Capture(label);
                if (stage >= 6)
                {
                    Color32 sky = pixels[800 * 1600 + 800];
                    if (Mathf.Max(sky.r, sky.g, sky.b) > 150)
                        throw new InvalidOperationException("Night sky is covered by an oversized light lens.");
                }
                if (stage == 2)
                {
                    clearContrast = WaterContrast(pixels);
                    weather.Configure(300, 3, 0, 1);
                    RenderSettings.fogDensity = 0.04f;
                }
                else if (stage == 3)
                {
                    float fogContrast = WaterContrast(pixels);
                    Debug.Log($"WATER_FOG|clear contrast={clearContrast:F4}|fog contrast={fogContrast:F4}");
                    if (fogContrast >= clearContrast * 0.6f) throw new InvalidOperationException("Distant water retains contrast in dense fog.");
                    weather.Configure(300, 5, 1, 0.1f);
                    camera.transform.position = ship.transform.position + new Vector3(20, 2, 85);
                    camera.transform.LookAt(camera.transform.position + new Vector3(0, -1, 8));
                }
                else if (stage == 4)
                {
                    var splashes = GameObject.Find("Rain Surface Splashes").GetComponent<ParticleSystem>();
                    if (splashes.particleCount < 5) throw new InvalidOperationException("Rain impacts are missing.");
                    if (Shader.GetGlobalFloat("_RiverRain") < 0.9f) throw new InvalidOperationException("Rain ripple input is missing.");
                    Debug.Log($"RAIN_IMPACTS|particles={splashes.particleCount}");
                    weather.Configure(300, 2, 0, 0);
                    // Synthetic render fixture isolates bank response without changing vessel physics.
                    ship.GetComponent<ShipWakeController>().enabled = false;
                    Texture2D profile = (Texture2D)Shader.GetGlobalTexture("_RiverShoreProfile");
                    Vector4 range = Shader.GetGlobalVector("_RiverShoreRange");
                    float z = ship.transform.position.z;
                    float left = profile.GetPixelBilinear((z - range.x) / range.y, 0.5f).r;
                    var track = new ShipWakeTrack();
                    Vector2 bow = new Vector2(left + 23, z + 100);
                    track.Record(bow, bow - Vector2.up * 138, 5, 0.6f, Vector2.zero, 0.1f);
                    var points = new Vector4[ShipWakeTrack.Capacity];
                    var info = new Vector4[ShipWakeTrack.Capacity];
                    int count = track.Write(138, points, info, out Vector4 bounds);
                    Shader.SetGlobalVectorArray("_WakePoints", points);
                    Shader.SetGlobalVectorArray("_WakeInfo", info);
                    Shader.SetGlobalFloat("_WakeCount", count);
                    Shader.SetGlobalVector("_WakeBounds", bounds);
                    Shader.SetGlobalVector("_WakeShip", new Vector4(bow.x, bow.y - 69, 0, 1));
                    Shader.SetGlobalVector("_WakeHull", new Vector4(69, 8, 5, 0.6f));
                    Shader.SetGlobalFloat("_WakeAmplitude", 0.07f);
                    camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().antialiasing =
                        UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    camera.transform.position = new Vector3(left + 8, 6, z - 30);
                    camera.transform.LookAt(new Vector3(left + 4, 0, z + 40));
                }
                else if (stage >= 5)
                {
                    Shader.SetGlobalFloat("_WakeCount", 0);
                    Object.FindAnyObjectByType<DayNightController>().Apply(true);
                    weather.Configure(300, 0, 0, 0);
                    var rig = ship.GetComponentInChildren<NavigationLightRig>();
                    if (rig == null) throw new InvalidOperationException("Missing running light rig.");
                    foreach (Light light in rig.GetComponentsInChildren<Light>())
                        if (!light.enabled) throw new InvalidOperationException("Running light blinked off at night.");
                    if (stage == 9)
                    {
                        SessionState.SetInt(Key, 0);
                        Debug.Log("WATER_WEATHER|" + (failed ? "FAIL" : "PASS"));
                        EditorApplication.Exit(failed ? 1 : 0);
                        return;
                    }
                    float angle = (stage - 5) * 90 * Mathf.Deg2Rad;
                    camera.fieldOfView = stage % 2 == 0 ? 120 : 60;
                    camera.transform.position = ship.transform.position + new Vector3(Mathf.Sin(angle) * 45, 22, Mathf.Cos(angle) * 150);
                    camera.transform.LookAt(ship.transform.position + Vector3.up * 10);
                }
                Next(stage + 1);
            }
            catch (Exception error)
            {
                SessionState.SetInt(Key, 0);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
        private static void Next(int stage) { SessionState.SetInt(Key, stage); frames = 0; }
        private static Color32[] Capture(string name)
        {
            RenderTexture previous = RenderTexture.active;
            var ldr = RenderTexture.GetTemporary(target.width, target.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(target, ldr);
            RenderTexture.active = ldr;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(ldr);
            File.WriteAllBytes("Logs/WaterWeather/" + name + ".png", image.EncodeToPNG());
            Color32[] pixels = image.GetPixels32();
            Object.DestroyImmediate(image);
            return pixels;
        }
        private static float WaterContrast(Color32[] pixels)
        {
            double sum = 0;
            int count = 0;
            for (int y = 330; y < 420; y++)
            for (int x = 740; x < 860; x++)
            {
                Color32 p = pixels[y * 1600 + x];
                Color32 q = pixels[y * 1600 + x + 3];
                sum += Mathf.Abs((p.r + p.g + p.b) - (q.r + q.g + q.b)) / 765f;
                count++;
            }
            return (float)(sum / count);
        }
    }
}
