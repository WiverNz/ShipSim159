using System;
using System.Collections;
using System.IO;
using System.Linq;
using ShipSimulator.CameraSystem;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Diagnostics
{
    // Explicit command-line visual acceptance fixture; never runs during ordinary play.
    public sealed class BowWaterReview : MonoBehaviour
    {
        public static int Result { get; private set; } = -1;
        private string output;
        private bool failed;
        private float deadline;
        private Camera camera;
        private RenderTexture target;
        private Texture2D image;
        private SimulationMode previousSimulation;

        [Serializable] private sealed class Condition
        {
            public string name, unity, backend, grounding;
            public float load, waterLevelOffset, waterDatum, contactForceN, clearanceM;
            public Vector3 position, euler, velocity;
            public float[] compartmentMassKg;
        }
        [Serializable] private sealed class View
        {
            public string name;
            public Vector3 cameraPosition, lookAt;
            public int lod;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Environment.GetCommandLineArgs().Contains("--shipsim-bow-review")) return;
            Result = -1;
            var root = new GameObject("Bow water acceptance");
            DontDestroyOnLoad(root);
            root.AddComponent<BowWaterReview>();
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) failed = true;
        }
        private void Update()
        {
            if (failed) Finish(false);
            if (deadline > 0 && Time.realtimeSinceStartup > deadline) Finish(false);
        }

        private IEnumerator Start()
        {
            output = Argument("--bow-output") ?? "Logs/BowWater/" + (Application.isEditor ? "Editor" : "Player");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "result.txt"), "RUNNING\n");
            deadline = Time.realtimeSinceStartup + 600;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            VoyageMenu menu;
            do { menu = FindAnyObjectByType<VoyageMenu>(); yield return null; } while (menu == null || !menu.IsReady);
            menu.StartVoyage("GorodetsTrainingScene");
            yield return null; yield return null;
            var ship = FindAnyObjectByType<ShipPhysicsController>();
            if (VesselSwap.IdOf(ship) != VesselCatalogue.DefaultVesselId) { Finish(false); yield break; }
            foreach (Canvas canvas in FindObjectsByType<Canvas>()) canvas.enabled = false;
            FindAnyObjectByType<ShipFollowCamera>().enabled = false;
            ship.ManualStepping = true;
            Time.timeScale = 0;
            previousSimulation = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
            foreach (MooredBuoy buoy in FindObjectsByType<MooredBuoy>())
            {
                buoy.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                buoy.Body.isKinematic = true;
            }
            var body = ship.Body;
            body.interpolation = RigidbodyInterpolation.None;
            body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationY;
            var grounding = ship.Grounding ?? ship.gameObject.AddComponent<GroundingController>();
            grounding.Configure(ship, null);
            var rig = ship.GetComponent<VesselMechanisms>();
            var lods = ship.GetComponentInChildren<LODGroup>();
            var clock = FindAnyObjectByType<DayNightController>();
            var bathymetry = FindAnyObjectByType<ScenarioBathymetry>();
            FindAnyObjectByType<WeatherController>()?.Configure(0, 0, 0, 0);
            camera = new GameObject("Bow acceptance camera", typeof(Camera), typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
            camera.CopyFrom(Camera.main);
            camera.fieldOfView = 45; camera.nearClipPlane = 0.1f;
            var cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;
            cameraData.antialiasing = AntialiasingMode.None;
            target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            int captures = 0;
            for (int scenario = 0; scenario < 14; scenario++)
            {
                bool trim = scenario >= 12;
                float load = trim ? 0.5f : new[] { 0f, 0.5f, 1f }[scenario / 4];
                bool grounded = trim || scenario % 4 >= 2;
                bool night = scenario % 2 == 1;
                float level = night ? 0.4f : -0.4f;
                bool open = (scenario / 2 + scenario) % 2 == 1;
                var loading = LoadingModel.Initial(ship.Data, load);
                if (trim) { loading.massKg[0] *= 1.5f; loading.massKg[1] *= 0.5f; }
                var parameters = VesselParameters.Create(ship.Data, loading);
                Vector3 origin = new Vector3(body.position.x, 0, body.position.z);
                ship.RestoreVoyage(new VoyageSave { loading = loading,
                    position = origin + Vector3.up * (-parameters.Draft - parameters.KeelLocalY),
                    rotation = Quaternion.Euler(Mathf.Atan(parameters.TrimSlope) * Mathf.Rad2Deg, 0, 0) });
                grounding.ResetState();
                bathymetry.SetWaterLevelOffset(level);
                ship.SetDepthProvider(point => grounded ? Mathf.Max(0.05f,
                    parameters.Draft + 0.8f + level - Mathf.Max(0, point.z - origin.z - 5) * 0.07f) : 20 + level);
                for (int i = 0; i < 1000; i++) { ship.Simulate(0.02f); UnityEngine.Physics.Simulate(0.02f); }
                if (grounded && grounding.ContactNormalForceN < 1) throw new InvalidOperationException("Grounded fixture did not make contact.");
                if (!grounded && grounding.ContactNormalForceN > 0) throw new InvalidOperationException("Afloat fixture touched bottom.");
                clock.Apply(night);
                rig.SetHatches(open ? HatchPosition.ForwardOpen : HatchPosition.Closed);
                for (int i = 0; i < 3; i++) rig.Advance(400, 0, 0, 0);
                string name = $"{scenario:00}-{(grounded ? "grounded" : "afloat")}-{load:0.0}-{(trim ? "trim-" : "")}{(night ? "night" : "day")}-{(open ? "open" : "closed")}";
                var condition = new Condition { name = name, unity = Application.unityVersion,
                    backend = SystemInfo.graphicsDeviceType.ToString(), load = load, waterLevelOffset = level,
                    waterDatum = ship.WaterLevel, grounding = grounding.State.ToString(),
                    contactForceN = grounding.ContactNormalForceN, clearanceM = grounding.MinimumClearanceM,
                    position = body.position, euler = body.rotation.eulerAngles, velocity = body.linearVelocity,
                    compartmentMassKg = loading.massKg };
                File.WriteAllText(Path.Combine(output, name + ".json"), JsonUtility.ToJson(condition, true));
                for (int lod = 0; lod < 3; lod++)
                {
                    lods.ForceLOD(lod);
                    for (int view = 0; view < 4; view++)
                    {
                        Vector3 eye = new[] { new Vector3(18, 13, 34), new Vector3(-22, 5, 57),
                            new Vector3(0, 34, 54), new Vector3(12, 0, 84) }[view];
                        Vector3 aim = new[] { new Vector3(0, 2.5f, 57), new Vector3(0, 1, 57),
                            new Vector3(0, 0, 55), new Vector3(0, 0, 63) }[view];
                        Vector3 worldEye = ship.transform.TransformPoint(eye);
                        if (view == 3) worldEye.y = ship.WaterLevel + 0.7f;
                        camera.transform.position = worldEye;
                        Vector3 worldAim = ship.transform.TransformPoint(aim);
                        camera.transform.LookAt(worldAim);
                        yield return null; yield return null;
                        string shot = $"{name}-lod{lod}-view{view}";
                        Capture(Path.Combine(output, shot + ".png"));
                        File.WriteAllText(Path.Combine(output, shot + ".json"), JsonUtility.ToJson(new View
                            { name = shot, cameraPosition = worldEye, lookAt = worldAim, lod = lod }, true));
                        captures++;
                    }
                }
            }
            File.WriteAllText(Path.Combine(output, "result.txt"), $"{(failed ? "FAIL" : "PASS")} {captures} captures\n");
            Finish(!failed);
        }

        private void Capture(string path)
        {
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply(); RenderTexture.active = previous;
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        private void Finish(bool success)
        {
            if (Result >= 0) return;
            Result = success ? 0 : 1;
            if (!success && output != null) File.WriteAllText(Path.Combine(output, "result.txt"), "FAIL\n");
            UnityEngine.Physics.simulationMode = previousSimulation;
            Debug.Log("BOW_WATER_REVIEW|" + (success ? "PASS" : "FAIL"));
            if (!Application.isEditor) Application.Quit(Result);
        }
        private static string Argument(string flag)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
