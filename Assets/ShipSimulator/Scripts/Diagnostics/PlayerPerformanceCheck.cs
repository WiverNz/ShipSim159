using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ShipSimulator.Audio;
using ShipSimulator.CameraSystem;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShipSimulator.Diagnostics
{
    // Opt-in standalone measurement. Normal play never creates this object or changes its settings.
    public sealed class PlayerPerformanceCheck : MonoBehaviour
    {
        private string folder;
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private readonly List<Sample> samples = new List<Sample>(65536);
        private readonly List<ProfilerRecorder> counters = new List<ProfilerRecorder>();
        private readonly string[] names = { "GC Allocated In Frame", "Total Used Memory", "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Physics.Simulate", "ShipSim.Physics", "ShipSim.Lines", "ShipSim.Mechanisms", "ShipSim.Audio", "ShipSim.Tackle", "ShipSim.WaterReflectionSubmission" };
        private bool failed;
        private ShipPhysicsController ship;
        private ShipFollowCamera cameraRig;
        private VoyageSave initial;
        private VoyageMenu menu;
        private string oldSelection;
        private bool oldMute, oldBackgroundMute, settingsCaptured;
        private float oldVolume;
        private float deadline;

        [Serializable] private sealed class Conditions
        {
            public string unity, cpu, gpu, api, os, quality, scenario, vessel, camera, build;
            public int ramMB, gpuMB, width, height, vSync, targetFrameRate;
            public long workingSetBytes, privateBytes;
            public float warmupS, measurementS, loadFraction, massKg, fixedDt, simulationScale;
            public Vector3 cameraPosition, shipPosition;
            public bool night;
            public float windMps, rain, fog;
            public int sources, listeners, soundscapes, lines, voices;
            public bool[] counterAvailable;
        }
        private struct Sample
        {
            public double frame, cpu, gpu;
            public long alloc, memory, draws, passes, triangles, physics, ship, lines, mechanisms, audio, tackle, water;
            public int collections;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isEditor || !Environment.GetCommandLineArgs().Contains("--shipsim-profile")) return;
            var go = new GameObject("Standalone performance check");
            DontDestroyOnLoad(go); go.AddComponent<PlayerPerformanceCheck>();
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception) failed = true;
        }

        private IEnumerator Start()
        {
            folder = Path.GetFullPath(Argument("--profile-output") ?? "Logs/PlayerPerformance");
            Directory.CreateDirectory(folder);
            oldSelection = VesselSelection.SelectedId;
            oldMute = VoyageSettings.Muted; oldBackgroundMute = VoyageSettings.MuteInBackground; oldVolume = VoyageSettings.Volume;
            settingsCaptured = true; deadline = Time.realtimeSinceStartup + 1800;
            VoyageSettings.SetMuted(false); VoyageSettings.SetVolume(0.8f); VoyageSettings.SetMuteInBackground(false);
            Application.runInBackground = true;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            yield return Open("volgodon-507b", "GorodetsTrainingScene");
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            foreach (string name in names)
            {
                ProfilerCategory category = name.Contains("Memory") || name.StartsWith("GC") ? ProfilerCategory.Memory :
                    name == "Physics.Simulate" ? ProfilerCategory.Physics : name.StartsWith("ShipSim") ? ProfilerCategory.Scripts : ProfilerCategory.Render;
                counters.Add(ProfilerRecorder.StartNew(category, name, 1));
            }
            initial = menu.CaptureSave();
            string[] cases = { "near-day-full", "navigator", "far-auto-lod", "lod-transition", "empty-open", "full-open", "mechanisms", "night", "heavy-weather", "moving-audio", "quay-lines", "anchor-grounding" };
            bool smoke = Environment.GetCommandLineArgs().Contains("--profile-smoke");
            string selectedCase = Argument("--profile-case");
            if (selectedCase != null && !cases.Contains(selectedCase))
                throw new ArgumentException("Unknown profile case " + selectedCase);
            string[] selected = selectedCase == null ? cases : new[] { selectedCase };
            int repeats = int.TryParse(Argument("--profile-repeat"), out int count) ? Mathf.Clamp(count, 1, 10) : 1;
            foreach (int repeat in Enumerable.Range(0, repeats))
            foreach (string name in smoke ? selected.Take(1) : selected)
            {
                menu.ApplySave(initial); menu.Resume(); ship.SetDepthProvider(null);
                FindAnyObjectByType<SimulationTimeController>()?.SetScale(1);
                FindAnyObjectByType<DayNightController>()?.Apply(name == "night");
                FindAnyObjectByType<WeatherController>()?.Configure(90, name == "heavy-weather" ? 15 : 3,
                    name == "heavy-weather" ? 1 : 0, name == "heavy-weather" ? 0.8f : 0);
                cameraRig.RestoreState(new CameraSave { view = name == "navigator" ? 8 : 0, yaw = 140, pitch = 22,
                    distance = name == "far-auto-lod" ? 550 : 120 });
                if (name.Contains("open"))
                {
                    ship.ConfigureLoading(name == "empty-open" ? 0 : 1);
                    ship.Mechanisms.RestoreState(new MechanismSave { opening = 1, parking = 1, hatchTarget = (int)HatchPosition.ForwardOpen });
                }
                if (name == "mechanisms")
                {
                    ship.Mechanisms.RestoreState(new MechanismSave { opening = 0.6f, hatchTarget = (int)HatchPosition.HalfOpen });
                    ship.SetEngineCommand(0, 0.7f); ship.SetEngineCommand(1, -0.4f); ship.SetRudderCommand(1);
                }
                if (name == "moving-audio") { ship.SetThrottleCommand(1); yield return new WaitForSecondsRealtime(20); }
                if (name == "quay-lines")
                {
                    var save = menu.CaptureSave(); save.position = new Vector3(39.5f, save.position.y, 0); save.rotation = Quaternion.identity;
                    menu.ApplySave(save); yield return null;
                    MooringPoint[] shore = FindObjectsByType<MooringPoint>().Where(p => p.Kind == MooringPointKind.Shore).ToArray();
                    for (int i = 0; i < ship.GroundTackle.StationCount; i++)
                    {
                        var station = ship.GroundTackle.Station(i);
                        if (!station.Id.Contains("starboard")) continue;
                        var nearest = shore.OrderBy(p => (p.LeadWorld - station.LeadWorld).sqrMagnitude).FirstOrDefault();
                        if (nearest != null && ship.GroundTackle.PassLine(i, nearest) == null) ship.GroundTackle.SetLineCommand(i, LineCommand.Haul);
                    }
                    yield return new WaitForSecondsRealtime(30);
                    if (ship.GroundTackle.LineCount < 3) { failed = true; Debug.LogError("PLAYER_PROFILE|fewer than three mooring lines"); }
                }
                if (name == "anchor-grounding")
                {
                    ship.Grounding.Configure(ship, null);
                    ship.SetDepthProvider(_ => 3.48f);
                    ship.GroundTackle.SetChainCommand(0, ChainCommand.LetGo);
                    ship.SetWindVelocity(new Vector3(12,0,0));
                    yield return new WaitForSecondsRealtime(15);
                    ship.GroundTackle.SetChainCommand(0, ChainCommand.Veer);
                    yield return new WaitForSecondsRealtime(15);
                    ship.GroundTackle.SetChainCommand(0, ChainCommand.Hold);
                }
                yield return Measure(repeats == 1 ? name : name + "-repeat-" + repeat, smoke ? 2 : 10, smoke ? 5 : 30, name == "lod-transition");
            }
            if (smoke || selectedCase != null)
            {
                foreach (ProfilerRecorder counter in counters) counter.Dispose();
                File.WriteAllText(Path.Combine(folder, "result.txt"), smoke ? "SMOKE PASS\n" : "CASE PASS\n");
                Application.Quit(0); yield break;
            }
            // Exercise the real menu and persistent scene transitions, with a dedicated save.
            menu.SavePath = Path.Combine(folder, "profile-voyage.json");
            menu.SaveVoyage();
            Vector3 pausePosition = ship.Body.position;
            menu.OpenPause(); yield return new WaitForSecondsRealtime(1);
            if ((ship.Body.position - pausePosition).sqrMagnitude > 0.0001f || !AudioListener.pause)
                throw new InvalidOperationException("Pause did not suspend physics/audio");
            menu.Resume();
            VoyageSettings.SetVolume(0.25f);
            if (Mathf.Abs(AudioListener.volume - 0.25f) > 0.001f) throw new InvalidOperationException("Volume did not apply");
            VoyageSettings.SetMuted(true);
            if (AudioListener.volume != 0) throw new InvalidOperationException("Mute did not apply");
            yield return new WaitForSecondsRealtime(1);
            VoyageSettings.SetMuted(false); VoyageSettings.SetVolume(0.8f);
            FindAnyObjectByType<SimulationTimeController>()?.SetScale(4);
            yield return new WaitForSecondsRealtime(2);
            menu.LoadVoyage(); yield return new WaitForSecondsRealtime(3);
            string[] ids = { "volgodon-507b", "volgobalt-295ar", "volgoneft-1577", "meteor-342u", "luch-14352" };
            for (int round = 0; round < 3; round++)
                for (int i = 0; i < ids.Length; i++)
                {
                    yield return Open(ids[i], (round + i) % 2 == 0 ? "GorodetsTrainingScene" : "RiverTrainingScene");
                    QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
                    FindAnyObjectByType<SimulationTimeController>()?.SetScale(1);
                    ship.SetThrottleCommand(0.5f);
                    yield return Measure($"soak-{round}-{ids[i]}", 5, 15, false);
                }
            foreach (ProfilerRecorder counter in counters) counter.Dispose();
            VesselSelection.SelectedId = oldSelection;
            File.WriteAllText(Path.Combine(folder, "result.txt"), failed ? "FAIL\n" : "PASS\n");
            Debug.Log("PLAYER_PROFILE|" + (failed ? "FAIL" : "PASS"));
            Application.Quit(failed ? 1 : 0);
        }

        private IEnumerator Open(string id, string scene)
        {
            float began = Time.realtimeSinceStartup;
            float deadline = began + 90;
            while ((menu = FindAnyObjectByType<VoyageMenu>()) == null || !menu.IsReady)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Menu did not become ready");
                yield return null;
            }
            VesselSelection.SelectedId = id; menu.StartVoyage(scene);
            yield return null;
            while ((menu = FindAnyObjectByType<VoyageMenu>()) == null || !menu.IsReady || VoyageMenu.IsOpen || SceneManager.GetActiveScene().name != scene)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Voyage did not start");
                yield return null;
            }
            ship = FindAnyObjectByType<ShipPhysicsController>(); cameraRig = FindAnyObjectByType<ShipFollowCamera>();
            if (VesselSwap.IdOf(ship) != id) throw new InvalidOperationException("Wrong vessel " + VesselSwap.IdOf(ship));
            menu.SavePath = Path.Combine(folder, "profile-voyage.json");
            File.AppendAllText(Path.Combine(folder, "transitions.csv"), FormattableString.Invariant($"{id},{scene},{Time.realtimeSinceStartup-began}\n"));
        }

        private IEnumerator Measure(string name, float warmup, float seconds, bool transition)
        {
            yield return new WaitForSecondsRealtime(warmup);
            var sound = ship.GetComponent<VesselSoundscape>();
            var weather = FindAnyObjectByType<WeatherController>();
            var conditions = new Conditions
            {
                unity = Application.unityVersion, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                api = SystemInfo.graphicsDeviceVersion, os = SystemInfo.operatingSystem, ramMB = SystemInfo.systemMemorySize,
                gpuMB = SystemInfo.graphicsMemorySize, width = Screen.width, height = Screen.height,
                workingSetBytes = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64,
                privateBytes = System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64,
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()], vSync = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate, scenario = SceneManager.GetActiveScene().name,
                vessel = VesselSwap.IdOf(ship), camera = cameraRig.ViewName, cameraPosition = cameraRig.transform.position,
                shipPosition = ship.Body.position, massKg = ship.Body.mass, loadFraction = ship.Parameters.LoadFraction,
                fixedDt = Time.fixedDeltaTime, simulationScale = Time.timeScale, warmupS = warmup, measurementS = seconds,
                build = Debug.isDebugBuild ? "Development, no deep profiling" : "Release",
                night = FindAnyObjectByType<DayNightController>()?.IsNight ?? false,
                windMps = weather != null ? weather.WindSpeedMps : 0, rain = weather != null ? weather.RainIntensity : 0,
                fog = weather != null ? weather.FogIntensity : 0,
                sources = FindObjectsByType<AudioSource>().Length,
                listeners = FindObjectsByType<AudioListener>().Length,
                soundscapes = FindObjectsByType<VesselSoundscape>().Length,
                lines = ship.GroundTackle != null ? ship.GroundTackle.LineCount : 0, voices = sound != null ? sound.ActiveVoices : 0,
                counterAvailable = counters.Select(c => c.Valid).ToArray()
            };
            if (conditions.listeners != 1 || conditions.soundscapes != 1) { failed = true; Debug.LogError("PLAYER_PROFILE|duplicate audio lifecycle"); }
            samples.Clear();
            yield return null; yield return null;
            ulong lastTimestamp = 0;
            float start = Time.realtimeSinceStartup;
            int collections = GC.CollectionCount(0);
            while (Time.realtimeSinceStartup - start < seconds)
            {
                if (transition)
                {
                    cameraRig.enabled = false;
                    float distance = Mathf.Lerp(100, 550, Mathf.PingPong((Time.realtimeSinceStartup - start) / seconds * 2, 1));
                    cameraRig.transform.position = ship.transform.TransformPoint(Quaternion.Euler(22,140,0) * Vector3.back * distance);
                    cameraRig.transform.LookAt(ship.transform.TransformPoint(new Vector3(0,3.5f,10)));
                }
                FrameTimingManager.CaptureFrameTimings();
                yield return null;
                double cpu = -1, gpu = -1;
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].frameStartTimestamp != lastTimestamp)
                {
                    lastTimestamp = timing[0].frameStartTimestamp;
                    cpu = timing[0].cpuFrameTime > 0 ? timing[0].cpuFrameTime : -1;
                    gpu = timing[0].gpuFrameTime > 0 ? timing[0].gpuFrameTime : -1;
                }
                samples.Add(new Sample { frame = Time.unscaledDeltaTime * 1000, cpu = cpu, gpu = gpu,
                    alloc = Value(0), memory = Value(1), draws = Value(2), passes = Value(3), triangles = Value(4),
                    physics = Value(5), ship = Value(6), lines = Value(7), mechanisms = Value(8), audio = Value(9), tackle = Value(10), water = Value(11),
                    collections = GC.CollectionCount(0) - collections });
            }
            if (transition) cameraRig.enabled = true;
            var csv = new StringBuilder("frame_ms,cpu_frame_ms,gpu_ms,gc_alloc_bytes,memory_bytes,draw_calls,setpass,triangles,physics_ns,ship_ns,lines_ns,mechanisms_ns,audio_ns,tackle_ns,water_submission_ns,gc_collections\n");
            foreach (Sample s in samples) csv.AppendLine(FormattableString.Invariant($"{s.frame},{s.cpu},{s.gpu},{s.alloc},{s.memory},{s.draws},{s.passes},{s.triangles},{s.physics},{s.ship},{s.lines},{s.mechanisms},{s.audio},{s.tackle},{s.water},{s.collections}"));
            File.WriteAllText(Path.Combine(folder, name + ".json"), JsonUtility.ToJson(conditions, true));
            File.WriteAllText(Path.Combine(folder, name + ".csv"), csv.ToString());
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
            Debug.Log($"PLAYER_PROFILE|CASE|{name}|{samples.Count} frames");
            yield return new WaitForSecondsRealtime(0.5f);
        }
        private void Update()
        {
            if (!settingsCaptured) return;
            if (!failed && Time.realtimeSinceStartup < deadline) return;
            File.WriteAllText(Path.Combine(folder, "result.txt"), "FAIL: runtime error or watchdog timeout\n");
            Application.Quit(1);
        }
        private void OnDestroy()
        {
            if (!settingsCaptured) return;
            VoyageSettings.SetMuted(oldMute); VoyageSettings.SetVolume(oldVolume); VoyageSettings.SetMuteInBackground(oldBackgroundMute);
            VesselSelection.SelectedId = oldSelection;
        }
        private long Value(int index) => counters[index].Valid && counters[index].Count > 0 ? counters[index].LastValue : -1;
        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
