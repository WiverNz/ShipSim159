using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ShipSimulator.Audio;
using ShipSimulator.CameraSystem;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // Dedicated batch check and demonstration of the Part 15 soundscape in Gorodets, in real time: the final mix at
    // the AudioListener is recorded to Logs/Part15/demo/*.wav while the 507B idles, accelerates, runs her engines
    // differently, reverses, is heard from the bridge, works her hatches, anchors, moors, pauses, saves and loads;
    // then a vessel change must give the new vessel its own profile without the 507B's equipment, and a scenario
    // change must leave one soundscape and one listener. Ends with SHIP_AUDIO_RUNTIME|PASS.
    // Recording uses OnAudioFilterRead on the listener, because AudioRenderer returns no samples in batch mode.
    [InitializeOnLoad]
    public static class ShipAudioRuntimeCheck
    {
        private const string StageKey = "ShipSimulator.Part15.Stage";
        private const string Gorodets = "GorodetsTrainingScene";
        private const string River = "RiverTrainingScene";
        private const string Folder = "Logs/Part15/demo";

        [Serializable]
        private sealed class Segment
        {
            public string name;
            public float seconds, peak, rmsDb, maxActiveVoices, maxSources;
            public int eventsPlayed, listeners, soundscapes;
            public List<string> windows = new List<string>();
        }

        [Serializable]
        private sealed class Report
        {
            public List<Segment> segments = new List<Segment>();
            public float tickMicroseconds;
            public long tickBytesPer1000;
            public long clipMemoryBytes;
            public int clipCount;
            public int eventsReplayedAfterLoad = -1, eventsAfterHatchLoad = -1;
            public float pausedRmsDb = 0, mutedRmsDb = 0;
            public int otherVesselSoundscapes = -1, otherVesselForbiddenSources = -1, otherVesselActiveVoices = -1;
            public int riverSoundscapes = -1, riverListeners = -1;
            public string otherVessel, otherVesselProfile;
            public List<string> otherVesselDisabled = new List<string>();
            public List<string> failures = new List<string>();
        }

        private sealed class Step
        {
            public string Segment;
            public float Seconds;
            public Action Enter;
            public Func<bool> Done;
            public string Window;
        }

        private static readonly List<Step> steps = new List<Step>();
        private static readonly float[] drain = new float[1 << 18];
        private static Report report = new Report();
        private static double deadline, stepStart;
        private static int cursor = -1;
        private static bool failed, entered;
        private static BinaryWriter wav;
        private static long wavSamples;
        private static Segment current;
        private static double windowSum;
        private static long windowCount, windowSkip;
        private static string window;
        private static VoyageSave saved;
        private static int eventsAtLoad;
        private static float volumeBefore = 0.8f;
        private static bool otherUnderWay;

        static ShipAudioRuntimeCheck()
        {
            deadline = EditorApplication.timeSinceStartup + 3000;
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetInt(StageKey, 0) != 0 && (type == LogType.Error || type == LogType.Exception)) failed = true;
            };
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch editor for this check.");
            Debug.Log("SHIP_AUDIO_RUNTIME|START");
            SessionState.SetInt(StageKey, 1);
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + Gorodets + ".unity");
            EditorApplication.EnterPlaymode();
        }

        // ---- The demonstration ----

        private static ShipPhysicsController Ship => Object.FindAnyObjectByType<ShipPhysicsController>();
        private static VesselSoundscape Sound => Object.FindAnyObjectByType<VesselSoundscape>();
        private static VoyageMenu Menu => Object.FindAnyObjectByType<VoyageMenu>();

        private static void Add(string segment, float seconds, Action enter = null, Func<bool> done = null, string window = null) =>
            steps.Add(new Step { Segment = segment, Seconds = seconds, Enter = enter, Done = done, Window = window });

        private static void Script()
        {
            steps.Clear();
            Add("01-stopped-then-dead-slow", 6, () => { Weather(270, 4); View(0); Ship.ResetToStart(); Speed(1); });
            Add("01-stopped-then-dead-slow", 14, () => Ship.SetThrottleCommand(0.27f));
            Add("02-acceleration-then-full-ahead", 15, () => { Ship.SetThrottleCommand(1f); Speed(4); });
            Add("02-acceleration-then-full-ahead", 10, () => Speed(1));
            Add("03-port-full-starboard-half-from-astern", 15, () => { View(6); Ship.SetEngineCommand(0, 1f); Ship.SetEngineCommand(1, 0.5f); });
            Add("04-full-ahead-to-full-astern", 35, () => { View(0); Ship.SetThrottleCommand(-1f); });
            Add("05-bridge-then-outside", 8, () => { Ship.ResetToStart(); Ship.SetThrottleCommand(0.8f); Speed(4); });
            Add("05-bridge-then-outside", 10, () => { Speed(1); View(FollowCamera.ViewCount - 1); }, window: "bridge");
            Add("05-bridge-then-outside", 8, () => View(0), window: "outside");
            Add("06-wind-15ms-outside-then-bridge", 10, () => { Ship.ResetToStart(); Weather(270, 15); View(0); }, window: "outside");
            Add("06-wind-15ms-outside-then-bridge", 6, () => View(FollowCamera.ViewCount - 1), window: "bridge");
            Add("07-hatches", 14, () => { Weather(270, 3); View(7); Ship.Mechanisms.SetHatches(HatchPosition.HalfOpen); Speed(4); });
            Add("07-hatches", 20, () => Ship.Mechanisms.SetHatches(HatchPosition.Closed), () => !Ship.Mechanisms.HatchesMoving);
            Add("07-hatches", 3);
            Add("07-hatches", 14, () => LoadHatches(0.24f), window: "slide after load");
            Add("07-hatches", 18, () => LoadHatches(0.74f), () => !Ship.Mechanisms.HatchesMoving);
            Add("07-hatches", 3, () => Speed(1));
            Add("08-anchor", 25, () =>
            {
                Place(new Vector3(0f, Ship.Body.position.y, -20f), 180f);
                View(5);
                Ship.GroundTackle.SetChainCommand(0, ChainCommand.LetGo);
            }, () => Ship.GroundTackle.Phase(0) == AnchorPhase.Bottom);
            Add("08-anchor", 3);
            Add("08-anchor", 4, () => Ship.GroundTackle.SetChainCommand(0, ChainCommand.Veer));
            Add("08-anchor", 4, () => Ship.GroundTackle.SetChainCommand(0, ChainCommand.Hold));
            Add("08-anchor", 70, () => { Ship.GroundTackle.SetChainCommand(0, ChainCommand.Heave); Speed(4); },
                () => Ship.GroundTackle.Phase(0) == AnchorPhase.Stowed);
            Add("08-anchor", 3, () => Speed(1));
            Add("09-mooring", 15, () =>
            {
                Place(new Vector3(38.8f, Ship.Body.position.y, 5f), 0f);
                Weather(90, 3);
                View(4);
                Pass("bow-head-starboard", "gorodets-berth-b7");
                Pass("stern-starboard", "gorodets-berth-b1");
            });
            Add("09-mooring", 15, () =>
            {
                Line("bow-head-starboard", LineCommand.Hold);
                Line("stern-starboard", LineCommand.Hold);
                Weather(90, 14);
                Speed(4);
            });
            Add("09-mooring", 6, () => { Speed(1); Line("bow-head-starboard", LineCommand.Slack); Line("stern-starboard", LineCommand.Slack); });
            Add("09-mooring", 3, () =>
            {
                GroundTackle tackle = Ship.GroundTackle;
                tackle.CastOff(Station(tackle, "bow-head-starboard"));
                tackle.CastOff(Station(tackle, "stern-starboard"));
                Weather(270, 4);
            });
            Add("10-pause-speed-save-load-mute", 6, () => { Ship.ResetToStart(); Ship.SetThrottleCommand(0.8f); View(0); });
            Add("10-pause-speed-save-load-mute", 4, () => Menu.OpenPause(), window: "paused");
            Add("10-pause-speed-save-load-mute", 4, () => Menu.Resume());
            Add("10-pause-speed-save-load-mute", 5, () => Speed(4));
            Add("10-pause-speed-save-load-mute", 6, () => { Speed(1); saved = Menu.CaptureSave(); Ship.SetThrottleCommand(-0.6f); });
            Add("10-pause-speed-save-load-mute", 6, () =>
            {
                eventsAtLoad = Sound.EventsPlayed;
                Menu.ApplySave(saved);
            }, window: "after load");
            Add("10-pause-speed-save-load-mute", 3, () =>
            {
                report.eventsReplayedAfterLoad = Sound.EventsPlayed - eventsAtLoad;
                VoyageSettings.SetMuted(true);
            }, window: "muted");
            Add("10-pause-speed-save-load-mute", 3, () => VoyageSettings.SetMuted(false));
        }

        private static ShipFollowCamera FollowCamera => Object.FindAnyObjectByType<ShipFollowCamera>();
        private static void View(int index) => FollowCamera.SetView(index);
        private static void Speed(float scale) => Object.FindAnyObjectByType<SimulationTimeController>()?.SetScale(scale);
        private static void Weather(float direction, float speed) => Object.FindAnyObjectByType<WeatherController>()?.Configure(direction, speed, 0, 0);

        private static void Place(Vector3 position, float heading)
        {
            ShipPhysicsController ship = Ship;
            ship.RestoreVoyage(new VoyageSave
            {
                position = position, rotation = Quaternion.Euler(0, heading, 0), engineCommands = new[] { 0f, 0f },
                shaftRps = new[] { 0f, 0f }
            });
            ship.SetThrottleCommand(0f);
            ship.SetRudderCommand(0f);
            Object.FindAnyObjectByType<ShipTelemetryUI>()?.RefreshAfterVoyageLoad();
        }

        // A save taken now with the covers at the given opening, applied through the menu as a load would be.
        private static void LoadHatches(float opening)
        {
            VoyageSave save = Menu.CaptureSave();
            save.mechanisms.opening = opening;
            save.mechanisms.parking = 0;
            save.mechanisms.hatchTarget = (int)HatchPosition.HalfOpen;
            int before = Sound.EventsPlayed;
            Menu.ApplySave(save);
            Speed(4);
            EditorApplication.delayCall += () => { if (report.eventsAfterHatchLoad < 0) report.eventsAfterHatchLoad = Sound.EventsPlayed - before; };
        }

        private static int Station(GroundTackle tackle, string id)
        {
            for (int i = 0; i < tackle.StationCount; i++) if (tackle.Station(i).Id == id) return i;
            throw new InvalidOperationException("No mooring station " + id);
        }

        private static void Pass(string station, string bollard)
        {
            GroundTackle tackle = Ship.GroundTackle;
            MooringPoint shore = null;
            foreach (MooringPoint point in Object.FindObjectsByType<MooringPoint>()) if (point.Id == bollard) shore = point;
            string refusal = tackle.PassLine(Station(tackle, station), shore);
            if (refusal != null) throw new InvalidOperationException("Could not pass a line: " + refusal);
            tackle.SetLineCommand(Station(tackle, station), LineCommand.Haul);
        }

        private static void Line(string station, LineCommand command) =>
            Ship.GroundTackle.SetLineCommand(Station(Ship.GroundTackle, station), command);

        // ---- Driver ----

        private static void Tick()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0) return;
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Ship audio check timed out.");
                if (!EditorApplication.isPlaying) return;
                Application.runInBackground = true;
                VoyageMenu menu = Menu;
                if (menu == null || !menu.IsReady) return;
                switch (stage)
                {
                    case 1:
                        VesselSelection.SelectedId = VesselCatalogue.DefaultVesselId;
                        menu.StartVoyage(Gorodets);
                        SessionState.SetInt(StageKey, 2);
                        stepStart = EditorApplication.timeSinceStartup;
                        return;
                    case 2:
                        if (VoyageMenu.IsOpen || Ship == null || Sound == null || !Sound.IsBuilt) return;
                        if (EditorApplication.timeSinceStartup - stepStart < 2) return;
                        volumeBefore = VoyageSettings.Volume;
                        VoyageSettings.SetVolume(1f);
                        AudioListener listener = Object.FindAnyObjectByType<AudioListener>();
                        if (listener.GetComponent<ShipAudioListenerTap>() == null) listener.gameObject.AddComponent<ShipAudioListenerTap>();
                        Measure();
                        Script();
                        SessionState.SetInt(StageKey, 3);
                        return;
                    case 3:
                        RunScript();
                        return;
                    case 4:
                        if (VoyageMenu.IsOpen || Ship == null) return;
                        if (EditorApplication.timeSinceStartup - stepStart < 2) return;
                        if (!otherUnderWay)
                        {
                            otherUnderWay = true;
                            Ship.SetThrottleCommand(0.6f);
                            return;
                        }
                        if (EditorApplication.timeSinceStartup - stepStart < 10) return;
                        // The other vessel plays the shared library through its own profile, and nothing of the 507B's
                        // equipment: no firing rhythm, starting air, hatch or ground-tackle sound.
                        report.otherVessel = VesselSelection.SelectedId;
                        report.otherVesselSoundscapes = Object.FindObjectsByType<VesselSoundscape>().Length;
                        report.otherVesselProfile = Sound != null && Sound.Profile != null ? Sound.Profile.name : "";
                        report.otherVesselActiveVoices = Sound != null ? Sound.ActiveVoices : 0;
                        if (Sound != null) report.otherVesselDisabled.AddRange(Sound.DisabledLayers);
                        report.otherVesselForbiddenSources = 0;
                        foreach (AudioSource source in Ship.GetComponentsInChildren<AudioSource>(true))
                            foreach (string forbidden in new[] { "Loop_Firing", "Loop_StartingAir", "Loop_BladeRate", "Loop_HydraulicPump",
                                "Loop_CoverWinch", "Loop_CoverRolling", "Loop_Chain", "Loop_Windlass", "Loop_MooringWinch", "Loop_LineStrain" })
                                if (source.name.StartsWith(forbidden)) report.otherVesselForbiddenSources++;
                        Check(report.otherVesselSoundscapes == 1, "the other vessel has no single soundscape");
                        Check(report.otherVesselProfile == report.otherVessel, "the other vessel plays profile " + report.otherVesselProfile);
                        Check(report.otherVesselForbiddenSources == 0, "the other vessel plays equipment it does not have");
                        Check(report.otherVesselActiveVoices > 0, "the other vessel is silent under way");
                        Check(Object.FindObjectsByType<AudioListener>().Length == 1, "more than one AudioListener after the vessel change");
                        VesselSelection.SelectedId = VesselCatalogue.DefaultVesselId;
                        menu.StartVoyage(River);
                        SessionState.SetInt(StageKey, 5);
                        stepStart = EditorApplication.timeSinceStartup;
                        return;
                    case 5:
                        if (VoyageMenu.IsOpen || Ship == null || Sound == null || !Sound.IsBuilt) return;
                        if (EditorApplication.timeSinceStartup - stepStart < 3) return;
                        report.riverSoundscapes = Object.FindObjectsByType<VesselSoundscape>().Length;
                        report.riverListeners = Object.FindObjectsByType<AudioListener>().Length;
                        Check(report.riverSoundscapes == 1, "the scenario change left other than one soundscape");
                        Check(report.riverListeners == 1, "more than one AudioListener after the scenario change");
                        Finish();
                        return;
                }
            }
            catch (Exception error)
            {
                Debug.LogError("SHIP_AUDIO_RUNTIME|FAIL|" + error);
                End(1);
            }
        }

        private static void RunScript()
        {
            Drain();
            if (cursor >= 0 && cursor < steps.Count)
            {
                Step step = steps[cursor];
                double elapsed = EditorApplication.timeSinceStartup - stepStart;
                Sample();
                bool finished = elapsed >= step.Seconds || (step.Done != null && elapsed > 1 && step.Done());
                if (!finished) return;
            }
            CloseWindow();
            cursor++;
            if (cursor >= steps.Count)
            {
                CloseSegment();
                // Vessel change: a fast craft in the same passage.
                VesselCatalogue catalogue = VesselCatalogue.Load();
                foreach (VesselCatalogue.Entry entry in catalogue.Entries)
                    if (entry.id != VesselCatalogue.DefaultVesselId && VoyagePassage.FirstAcceptedVesselId(catalogue, Gorodets, entry.id) == entry.id)
                    {
                        VesselSelection.SelectedId = entry.id;
                        break;
                    }
                Menu.StartVoyage(Gorodets);
                SessionState.SetInt(StageKey, 4);
                stepStart = EditorApplication.timeSinceStartup;
                return;
            }
            Step next = steps[cursor];
            if (current == null || current.name != next.Segment) OpenSegment(next.Segment);
            window = next.Window;
            windowSum = 0;
            windowCount = 0;
            windowSkip = (long)(0.25f * AudioSettings.outputSampleRate * ShipAudioListenerTap.Channels);
            next.Enter?.Invoke();
            stepStart = EditorApplication.timeSinceStartup;
        }

        private static void OpenSegment(string name)
        {
            CloseSegment();
            Directory.CreateDirectory(Folder);
            current = new Segment { name = name };
            wav = new BinaryWriter(File.Create(Path.Combine(Folder, name + ".wav")));
            WriteHeader(0);
            wavSamples = 0;
            ShipAudioListenerTap.Skip();
        }

        private static void Drain()
        {
            if (wav == null) { ShipAudioListenerTap.Skip(); return; }
            int count;
            while ((count = ShipAudioListenerTap.Drain(drain)) > 0)
                for (int i = 0; i < count; i++)
                {
                    float v = drain[i];
                    wav.Write(v);
                    wavSamples++;
                    current.peak = Mathf.Max(current.peak, Mathf.Abs(v));
                    // The listener ramps a volume or pause change over a few milliseconds; measure the state after it.
                    if (windowSkip > 0) { windowSkip--; continue; }
                    windowSum += v * v;
                    windowCount++;
                }
        }

        private static void Sample()
        {
            if (current == null) return;
            VesselSoundscape sound = Sound;
            if (sound != null) current.maxActiveVoices = Mathf.Max(current.maxActiveVoices, sound.ActiveVoices);
            int playing = 0;
            ShipPhysicsController ship = Ship;
            if (ship != null)
                foreach (AudioSource source in ship.GetComponentsInChildren<AudioSource>()) if (source.isPlaying) playing++;
            current.maxSources = Mathf.Max(current.maxSources, playing);
            current.listeners = Mathf.Max(current.listeners, Object.FindObjectsByType<AudioListener>().Length);
            current.soundscapes = Mathf.Max(current.soundscapes, Object.FindObjectsByType<VesselSoundscape>().Length);
            if (sound != null) current.eventsPlayed = sound.EventsPlayed;
        }

        private static void CloseWindow()
        {
            if (window == null || current == null) return;
            // The listener delivers nothing at all while paused; that is silence too.
            float db = windowCount == 0 ? -200f : 10f * Mathf.Log10((float)(windowSum / windowCount) + 1e-20f);
            current.windows.Add($"{window}: {db:0.0} dBFS RMS");
            if (window == "paused") report.pausedRmsDb = db;
            if (window == "muted") report.mutedRmsDb = db;
            window = null;
        }

        private static void CloseSegment()
        {
            if (wav == null) return;
            Drain();
            current.seconds = wavSamples / (float)(ShipAudioListenerTap.Channels * AudioSettings.outputSampleRate);
            WriteHeader(wavSamples);
            wav.Close();
            wav = null;
            report.segments.Add(current);
            Check(current.peak < 1f, current.name + " clipped");
            Check(current.listeners == 1, current.name + ": AudioListener count " + current.listeners);
            Check(current.soundscapes == 1, current.name + ": soundscape count " + current.soundscapes);
        }

        // IEEE float WAV, so anything above full scale stays visible to the analysis.
        private static void WriteHeader(long samples)
        {
            int channels = ShipAudioListenerTap.Channels, rate = AudioSettings.outputSampleRate;
            long bytes = samples * 4;
            wav.Seek(0, SeekOrigin.Begin);
            wav.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            wav.Write((int)(36 + bytes));
            wav.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            wav.Write(16);
            wav.Write((short)3);
            wav.Write((short)channels);
            wav.Write(rate);
            wav.Write(rate * channels * 4);
            wav.Write((short)(channels * 4));
            wav.Write((short)32);
            wav.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            wav.Write((int)bytes);
            wav.Seek(0, SeekOrigin.End);
        }

        // Update cost and clip memory, measured once the soundscape is built.
        private static void Measure()
        {
            VesselSoundscape sound = Sound;
            for (int i = 0; i < 50; i++) sound.Tick(0f, 0f);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) sound.Tick(0f, 0f);
            watch.Stop();
            report.tickBytesPer1000 = GC.GetAllocatedBytesForCurrentThread() - allocated;
            report.tickMicroseconds = (float)watch.Elapsed.TotalMilliseconds;
            var clips = new HashSet<AudioClip>();
            foreach (LoopVoice voice in sound.Voices) if (voice.Source != null && voice.Source.clip != null) clips.Add(voice.Source.clip);
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ShipAudioBuilder.LibraryFolder }))
                clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)));
            foreach (AudioClip clip in clips) report.clipMemoryBytes += Profiler.GetRuntimeMemorySizeLong(clip);
            report.clipCount = clips.Count;
        }

        private static void Finish()
        {
            Check(report.eventsReplayedAfterLoad == 0, "events replayed after load: " + report.eventsReplayedAfterLoad);
            Check(report.eventsAfterHatchLoad == 0, "events on the hatch load frame: " + report.eventsAfterHatchLoad);
            Check(report.pausedRmsDb < -80f, "sound while paused: " + report.pausedRmsDb);
            Check(report.mutedRmsDb < -80f, "sound while muted: " + report.mutedRmsDb);
            Check(report.tickBytesPer1000 == 0, "soundscape tick allocates");
            foreach (Segment segment in report.segments) Check(segment.seconds > 5f, segment.name + " was not recorded");
            File.WriteAllText("Logs/Part15/audio-runtime.json", JsonUtility.ToJson(report, true));
            File.Copy("Logs/Part15/audio-runtime.json", "Art/Source/VolgoDon507B/part15-runtime.json", true);
            if (report.failures.Count > 0 || failed)
            {
                Debug.LogError("SHIP_AUDIO_RUNTIME|FAIL|" + string.Join("; ", report.failures) + (failed ? " (errors logged)" : ""));
                End(1);
                return;
            }
            Debug.Log("SHIP_AUDIO_RUNTIME|PASS");
            End(0);
        }

        private static void Check(bool condition, string failure)
        {
            if (!condition) report.failures.Add(failure);
        }

        private static void End(int code)
        {
            SessionState.SetInt(StageKey, 0);
            if (wav != null) { wav.Close(); wav = null; }
            VoyageSettings.SetVolume(volumeBefore);
            VoyageSettings.SetMuted(false);
            EditorApplication.Exit(code);
        }
    }

    // Filled on the audio thread, drained on the main thread.
    public sealed class ShipAudioListenerTap : MonoBehaviour
    {
        private static readonly float[] Ring = new float[1 << 21];
        private static long written, read;
        public static int Channels = 2;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            Channels = channels;
            long w = Interlocked.Read(ref written);
            for (int i = 0; i < data.Length; i++) Ring[(w + i) & (Ring.Length - 1)] = data[i];
            Interlocked.Add(ref written, data.Length);
        }

        public static int Drain(float[] into)
        {
            long w = Interlocked.Read(ref written);
            int count = (int)Math.Min(into.Length, w - read);
            for (int i = 0; i < count; i++) into[i] = Ring[(read + i) & (Ring.Length - 1)];
            read += count;
            return count;
        }

        public static void Skip() => read = Interlocked.Read(ref written);
    }
}
