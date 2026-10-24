#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using ShipSimulator.Audio;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ShipSimulator.Tests
{
    // The 507B soundscape against the simulation it listens to, stepped by hand: layers follow the computed
    // machinery state, events sound once, restores do not replay them, and nothing outlives the vessel.
    // Measurements go to Logs/Part15/soundscape-cases.jsonl for the Part 15 report.
    public sealed class VesselSoundscapeTests
    {
        private const string ResultPath = "Logs/Part15/soundscape-cases.jsonl";
        private const float Dt = 0.02f;
        private SimulationMode previousMode;
        private readonly List<GameObject> created = new List<GameObject>();

        [Serializable]
        private sealed class Result
        {
            public string name;
            public float portRpm, starboardRpm, portFiringPitch, starboardFiringPitch, maxVolumeStep, maxPitchStep;
            public int events, activeVoices, maxActiveVoices;
            public long bytesPer1000Ticks;
            public float microsecondsPerTick;
            public string notes;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousMode = UnityEngine.Physics.simulationMode;
            UnityEngine.Physics.simulationMode = SimulationMode.Script;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in created) if (item != null) Object.Destroy(item);
            created.Clear();
            UnityEngine.Physics.simulationMode = previousMode;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Engines_FollowEachShaftsComputedRpm_NotTheLever()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn();
            ship.SetEngineCommand(0, 0.9f);
            ship.SetEngineCommand(1, 0.35f);
            Tick(sound, Dt);
            Assert.That(sound.EngineLayer(0, "FiringHigh").TargetVolume + sound.EngineLayer(0, "FiringLow").TargetVolume, Is.Zero,
                "A lever moved without a physics step changes no engine sound.");
            Run(ship, sound, 40f);
            float port = Mathf.Abs(ship.ShaftRpm(0)), starboard = Mathf.Abs(ship.ShaftRpm(1));
            Assert.That(port, Is.GreaterThan(starboard + 100f));
            LoopVoice portHigh = sound.EngineLayer(0, "FiringHigh"), starboardLow = sound.EngineLayer(1, "FiringLow");
            VesselSoundProfile profile = sound.Profile;
            // Six cylinders, four-stroke, direct drive: rpm / 20 firings per second, over each clip's own rate.
            Assert.That(portHigh.Pitch, Is.EqualTo(port / 20f / profile.firingHighHz).Within(0.02f), "Port firing rate is its own rpm / 20 Hz.");
            Assert.That(starboardLow.Pitch, Is.EqualTo(starboard / 20f / profile.firingLowHz).Within(0.02f));
            Assert.That(portHigh.Volume, Is.GreaterThan(0.05f));
            Assert.That(sound.EngineLayer(1, "FiringHigh").Volume, Is.LessThan(sound.EngineLayer(1, "FiringLow").Volume),
                "Below the crossfade band the low layer carries the firing.");
            Assert.That(sound.EngineLayer(0, "Wash").Volume, Is.GreaterThan(sound.EngineLayer(1, "Wash").Volume),
                "More thrust, more wash.");
            Assert.That(sound.WaterLayer("FlowPort").Volume, Is.GreaterThan(0f), "Under way the hull flow sounds.");
            Record(new Result { name = "independent engines", portRpm = port, starboardRpm = starboard,
                portFiringPitch = portHigh.Pitch, starboardFiringPitch = starboardLow.Pitch });
            yield return null;
        }

        // Full ahead to full astern: the fuel is cut, starting air brakes the shafts, the engines restart astern.
        // Every layer glides; the restart sounds once per engine.
        [UnityTest]
        public IEnumerator Reversal_GlidesAndSoundsStartingAirOncePerEngine()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn();
            ship.SetThrottleCommand(1f);
            Run(ship, sound, 30f);
            int before = sound.EventsPlayed;
            ship.SetThrottleCommand(-1f);
            float maxVolumeStep = 0, maxPitchStep = 0;
            bool airBraked = false, firingCut = false;
            var previous = new Dictionary<LoopVoice, (float v, float p)>();
            foreach (LoopVoice voice in sound.Voices) previous[voice] = (voice.Volume, voice.Pitch);
            for (float t = 0; t < 60f; t += Dt)
            {
                Step(ship, sound, Dt);
                airBraked |= sound.EngineLayer(0, "AirBrake").TargetVolume > 0f;
                firingCut |= ship.Model.Shafts[0].IsReversing && sound.EngineLayer(0, "FiringHigh").TargetVolume == 0f;
                foreach (LoopVoice voice in sound.Voices)
                {
                    (float v, float p) = previous[voice];
                    maxVolumeStep = Mathf.Max(maxVolumeStep, Mathf.Abs(voice.Volume - v));
                    if (voice.Volume > 0.01f && v > 0.01f) maxPitchStep = Mathf.Max(maxPitchStep, Mathf.Abs(voice.Pitch - p));
                    previous[voice] = (voice.Volume, voice.Pitch);
                }
            }
            Assert.That(ship.ShaftRpm(0), Is.LessThan(-100f), "Running astern.");
            Assert.That(firingCut, Is.True, "No combustion sound while the fuel is cut.");
            Assert.That(airBraked, Is.True, "Starting air is heard while it brakes the shaft.");
            Assert.That(sound.EventsPlayed - before, Is.EqualTo(2), "One starting-air blast per engine.");
            Assert.That(maxVolumeStep, Is.LessThan(0.2f), "No volume jumps between 20 ms ticks.");
            Assert.That(maxPitchStep, Is.LessThan(0.03f), "No pitch jumps between 20 ms ticks.");
            Record(new Result { name = "full ahead to full astern", maxVolumeStep = maxVolumeStep, maxPitchStep = maxPitchStep,
                events = sound.EventsPlayed - before });
            yield return null;
        }

        [UnityTest]
        public IEnumerator Anchor_EventsSoundOnce_AndChainLayersFollowTheChainRate()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            int before = sound.EventsPlayed;
            tackle.SetChainCommand(0, ChainCommand.LetGo);
            bool ran = false;
            for (int i = 0; i < 3000 && tackle.Phase(0) != AnchorPhase.Bottom; i++)
            {
                Step(ship, sound, Dt);
                ran |= sound.AnchorLayer(0, "Run").Volume > 0.1f;
            }
            Run(ship, sound, 5f);
            Assert.That(ran, Is.True, "The chain is heard running out.");
            Assert.That(sound.AnchorLayer(0, "Run").TargetVolume, Is.Zero, "The brake holds; the chain is silent.");
            Assert.That(sound.EventsPlayed - before, Is.EqualTo(2), "Brake released once, set once, not every step.");
            Assert.That(sound.AnchorLayer(1, "Run").TargetVolume, Is.Zero, "The other anchor stays quiet.");

            tackle.SetChainCommand(0, ChainCommand.Heave);
            bool heaved = false, motor = false;
            for (int i = 0; i < 20000 && tackle.Phase(0) != AnchorPhase.Stowed; i++)
            {
                Step(ship, sound, Dt);
                heaved |= sound.AnchorLayer(0, "Heave").Volume > 0.1f;
                motor |= sound.AnchorLayer(0, "Motor").Volume > 0.1f;
            }
            Run(ship, sound, 3f);
            Assert.That(heaved && motor, Is.True, "The windlass and the links over the gypsy are heard while heaving.");
            Assert.That(sound.AnchorLayer(0, "Motor").TargetVolume + sound.AnchorLayer(0, "Heave").TargetVolume, Is.Zero,
                "Stowed: the windlass stops and so does its sound.");
            Assert.That(sound.AnchorLayer(0, "Motor").Playing, Is.False);
            // Windlass start, windlass stop and the anchor seating in its pocket.
            Assert.That(sound.EventsPlayed - before, Is.EqualTo(5));
            Record(new Result { name = "let go and heave", events = sound.EventsPlayed - before });
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hatches_SoundOnlyWhileMoving_PauseIsNotAStop()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn();
            VesselMechanisms rig = ship.Mechanisms;
            int before = sound.EventsPlayed;
            rig.SetHatches(HatchPosition.HalfOpen);
            var drives = new List<int>();
            for (int i = 0; i < 20000 && rig.HatchesMoving; i++)
            {
                rig.Advance(0.1f, 0, 0, 0);
                sound.Tick(0.1f, 0.1f);
                if (drives.Count == 0 || drives[drives.Count - 1] != sound.HatchDrive) drives.Add(sound.HatchDrive);
                if (i == 50)
                {
                    int events = sound.EventsPlayed, drive = sound.HatchDrive;
                    for (int k = 0; k < 20; k++) sound.Tick(0.1f, 0f);
                    Assert.That(sound.HatchDrive, Is.EqualTo(drive), "A paused frame is not a stop.");
                    Assert.That(sound.EventsPlayed, Is.EqualTo(events));
                }
            }
            for (int k = 0; k < 30; k++)
            {
                rig.Advance(0.1f, 0, 0, 0);
                sound.Tick(0.1f, 0.1f);
                if (drives[drives.Count - 1] != sound.HatchDrive) drives.Add(sound.HatchDrive);
            }
            Assert.That(drives, Is.EqualTo(new[] { 1, 2, 1, 0 }), "Hydraulic lift, winch slide, hydraulic lowering, stopped.");
            for (int p = 0; p < 2; p++)
                foreach (string layer in new[] { "Pump", "Winch", "Rolling" })
                    Assert.That(sound.HatchLayer(p, layer).Playing, Is.False, layer + " stops with the covers.");
            // Per hold: pump start; pump stop and winch start; winch stop and pump start; pump stop and seating.
            Assert.That(sound.EventsPlayed - before, Is.EqualTo(14));
            Record(new Result { name = "hatches half open", events = sound.EventsPlayed - before, notes = string.Join(",", drives) });
            yield return null;
        }

        [UnityTest]
        public IEnumerator MooringLine_WinchStrainAndParting_FollowTheLine()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            int stern = -1;
            for (int i = 0; i < tackle.StationCount; i++) if (tackle.Station(i).Id == "stern-starboard") stern = i;
            var shore = new GameObject("Shore x");
            created.Add(shore);
            shore.transform.position = new Vector3(12, 1.7f, -95);
            MooringPoint point = shore.AddComponent<MooringPoint>();
            point.Configure("x", "X", MooringPointKind.Shore, Vector3.zero);
            Assert.That(tackle.PassLine(stern, point), Is.Null);
            tackle.SetLineCommand(stern, LineCommand.Haul);
            int pumpStarts = 0;
            // The data names the 507B's mooring winches electrohydraulic, so the hydraulic pack is what starts.
            sound.Played += (clip, _) => { if (clip == sound.Profile.hydraulicStart) pumpStarts++; };
            Run(ship, sound, 4f);
            Assert.That(sound.StationLayer(stern, "Motor").Volume, Is.GreaterThan(0.1f), "The winch is heard hauling.");
            Assert.That(pumpStarts, Is.EqualTo(1), "One pump start while hauling, however the load varies.");
            int before;
            tackle.SetLineCommand(stern, LineCommand.Hold);
            Run(ship, sound, 2f);
            Assert.That(sound.StationLayer(stern, "Motor").TargetVolume, Is.Zero);
            before = sound.EventsPlayed;
            ship.SetThrottleCommand(1f);
            float strainWhenLoaded = 0;
            for (int i = 0; i < 20000 && tackle.LineCount > 0; i++)
            {
                Step(ship, sound, Dt);
                if (tackle.LineCount > 0 && tackle.LineTensionN(0) > 0.4f * tackle.LineBreakingLoadN)
                    strainWhenLoaded = Mathf.Max(strainWhenLoaded, sound.StationLayer(stern, "Strain").TargetVolume);
            }
            Run(ship, sound, 2f);
            Assert.That(tackle.LineCount, Is.Zero, "The line parted.");
            Assert.That(strainWhenLoaded, Is.GreaterThan(0.3f), "A heavily loaded line strains audibly.");
            Assert.That(sound.StationLayer(stern, "Strain").TargetVolume, Is.Zero, "A parted line is silent.");
            Assert.That(sound.EventsPlayed - before, Is.GreaterThanOrEqualTo(2), "Creaks under load and the parting snap.");
            Record(new Result { name = "stern line hauled, then parted", events = sound.EventsPlayed - before });
            yield return null;
        }

        // A load moves the vessel and her machinery without simulating the way there: the layers glide to the
        // restored state without being cut, and none of the events between the two states are replayed.
        [UnityTest]
        public IEnumerator Restore_ReplaysNoEvents_AndGlidesToTheRestoredState()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn(8f);
            ship.SetThrottleCommand(1f);
            Run(ship, sound, 30f);
            VoyageSave save = State(ship);
            GroundTackleSave tackleSave = ship.GroundTackle.CaptureState();
            ship.SetThrottleCommand(0f);
            ship.GroundTackle.SetChainCommand(0, ChainCommand.LetGo);
            Run(ship, sound, 40f);
            int before = sound.EventsPlayed;
            float rpmAtSave = save.shaftRps[0] * 60f;
            LoopVoice exterior = sound.EngineLayer(0, "Exterior");
            float levelBefore = exterior.Volume;
            bool playingBefore = exterior.Playing;
            ship.RestoreVoyage(save);
            ship.GroundTackle.RestoreState(tackleSave, null);
            Tick(sound, Dt);
            float oneStep = 1f - Mathf.Exp(-Dt / 0.4f);
            Assert.That(exterior.Volume - levelBefore, Is.GreaterThan(0f).And.LessThanOrEqualTo(oneStep * (exterior.TargetVolume - levelBefore) + 1e-4f),
                "The level glides from where it was towards the restored state, not cut or jumped.");
            Assert.That(exterior.Playing, Is.EqualTo(playingBefore || exterior.Volume > 0f), "A playing layer is not stopped by a load.");
            Assert.That(exterior.Pitch, Is.EqualTo(0.75f + 0.35f * Mathf.Clamp01(rpmAtSave / (ship.Parameters.RatedRps * 60f))).Within(0.01f),
                "At the restored rpm at once, without gliding from the old state.");
            Run(ship, sound, 3f);
            Assert.That(sound.EventsPlayed, Is.EqualTo(before), "Brake, let go and anchor events are not replayed.");
            Assert.That(sound.AnchorLayer(0, "Run").TargetVolume, Is.Zero, "The restored anchor is stowed again.");
            Assert.That(exterior.Volume, Is.EqualTo(exterior.TargetVolume).Within(0.05f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Interior_FollowsTheListenerIntoTheWheelhouse()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn();
            var listener = new GameObject("Listener", typeof(AudioListener));
            created.Add(listener);
            listener.transform.position = ship.transform.TransformPoint(ship.GetComponent<VesselLayout>().NavigatorEye);
            for (int i = 0; i < 60; i++) Tick(sound, Dt);
            Assert.That(sound.Interior, Is.EqualTo(1f));
            Assert.That(AudioMix.Interior, Is.EqualTo(1f));
            AudioMix.Mixer.GetFloat("WaterCutoff", out float muffled);
            Assert.That(muffled, Is.LessThan(1000f), "Outside water is muffled in the wheelhouse.");
            listener.transform.position = ship.transform.TransformPoint(new Vector3(28, 18, -58));
            for (int i = 0; i < 60; i++) Tick(sound, Dt);
            Assert.That(sound.Interior, Is.Zero);
            AudioMix.Mixer.GetFloat("WaterCutoff", out float open);
            Assert.That(open, Is.GreaterThan(20000f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sources_AreSpatialWithoutDoppler_AndPitchIgnoresSimulationSpeed()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn();
            ship.SetThrottleCommand(0.7f);
            Run(ship, sound, 30f);
            foreach (AudioSource source in ship.GetComponentsInChildren<AudioSource>(true))
            {
                Assert.That(source.dopplerLevel, Is.Zero, source.name);
                Assert.That(source.spatialBlend, Is.EqualTo(source.name == "Loop_WindSteady" || source.name == "Loop_WindStrong" ? 0f : 1f), source.name);
                Assert.That(source.outputAudioMixerGroup, Is.Not.Null, source.name + " is routed to a mixer bus.");
            }
            float pitch = sound.EngineLayer(0, "FiringHigh").Pitch;
            Time.timeScale = 4f;
            sound.ManualTicking = false;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(sound.EngineLayer(0, "FiringHigh").Pitch, Is.EqualTo(pitch).Within(0.01f),
                "Four times the simulation speed is not four times the pitch.");
        }

        [UnityTest]
        public IEnumerator Busy_VoicesStayBounded_AndTicksDoNotAllocate()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn(8f);
            GroundTackle tackle = ship.GroundTackle;
            ship.SetThrottleCommand(0.6f);
            ship.SetWindVelocity(new Vector3(14, 0, 0));
            ship.Mechanisms.SetHatches(HatchPosition.HalfOpen);
            tackle.SetChainCommand(0, ChainCommand.LetGo);
            tackle.SetChainCommand(1, ChainCommand.LetGo);
            tackle.SetChainCommand(2, ChainCommand.LetGo);
            int most = 0;
            for (int i = 0; i < 1500; i++)
            {
                ship.Mechanisms.Advance(Dt, 0, 0, 0);
                Step(ship, sound, Dt);
                most = Mathf.Max(most, sound.ActiveVoices);
            }
            Assert.That(sound.Pool.Active, Is.LessThanOrEqualTo(sound.Pool.Size));
            Assert.That(most, Is.LessThanOrEqualTo(AudioSettings.GetConfiguration().numRealVoices), "Within the real voice budget.");
            for (int i = 0; i < 100; i++) Tick(sound, Dt);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) sound.Tick(Dt, Dt);
            watch.Stop();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Assert.That(bytes, Is.Zero, "Ticking allocates nothing.");
            Record(new Result { name = "busy deck under way", maxActiveVoices = most, activeVoices = sound.ActiveVoices,
                bytesPer1000Ticks = bytes, microsecondsPerTick = (float)(watch.Elapsed.TotalMilliseconds * 1000.0 / 1000) });
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemovingTheSoundscape_LeavesNoSourcesOrSubscriptions()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn(8f);
            ship.SetThrottleCommand(0.5f);
            ship.Mechanisms.SetHatches(HatchPosition.HalfOpen);
            ship.GroundTackle.SetChainCommand(0, ChainCommand.LetGo);
            Run(ship, sound, 5f);
            Assert.That(ship.GetComponentsInChildren<AudioSource>(true).Length, Is.GreaterThan(10));
            Object.Destroy(sound);
            yield return null;
            Assert.That(ship.GetComponentsInChildren<AudioSource>(true), Is.Empty, "Every voice goes with the component.");
            ship.GroundTackle.SetChainCommand(1, ChainCommand.LetGo);
            Assert.DoesNotThrow(() => { for (int i = 0; i < 200; i++) Step(ship, null, Dt); }, "No stale subscription.");
            int sources = Object.FindObjectsByType<AudioSource>().Length;
            Object.Destroy(ship.gameObject);
            yield return null;
            Assert.That(Object.FindObjectsByType<AudioSource>().Length, Is.LessThanOrEqualTo(sources));
        }

        // Every catalogue vessel plays the shared library through its own profile, but only the layers its data and
        // model support: no firing rhythm without known cylinders and gearing, no starting air without a
        // direct-reversing engine, no machinery that is not modelled.
        [UnityTest]
        public IEnumerator EveryVessel_PlaysOnlyWhatItsDataAndModelSupport(
            [Values("volgodon-507b", "volgobalt-295ar", "volgoneft-1577", "meteor-342u", "luch-14352")] string id)
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn(20f, id);
            ship.SetThrottleCommand(0.7f);
            ship.SetWindVelocity(new Vector3(8, 0, 0));
            Run(ship, sound, 20f);
            bool volgoDon = id == "volgodon-507b";
            Assert.That(sound.Profile.name, Is.EqualTo(id));
            for (int i = 0; i < ship.EngineCount; i++)
            {
                Assert.That(sound.EngineLayer(i, "Room").Volume, Is.GreaterThan(0.05f), id + ": its diesels are heard.");
                if (id == "luch-14352") Assert.That(sound.EngineLayer(i, "Wash"), Is.Null, "Luch: a waterjet, not the recorded propeller wash.");
                else Assert.That(sound.EngineLayer(i, "Wash").Volume, Is.GreaterThan(0.01f), id + ": its propeller churns the water.");
                Assert.That(sound.EngineLayer(i, "FiringLow") != null, Is.EqualTo(volgoDon), id + " firing layer");
                Assert.That(sound.EngineLayer(i, "AirBrake") != null, Is.EqualTo(volgoDon), id + " starting air");
                Assert.That(sound.EngineLayer(i, "Blade") != null, Is.EqualTo(volgoDon), id + " blade rate");
            }
            Assert.That(sound.WaterLayer("WindSteady").Volume, Is.GreaterThan(0.01f), id + " hears the wind.");
            Assert.That(sound.HatchLayer(0, "Pump") != null, Is.EqualTo(volgoDon), id + " hatch covers");
            Assert.That(sound.AnchorLayer(0, "Run") != null, Is.EqualTo(volgoDon), id + " ground tackle");
            if (!volgoDon)
            {
                Assert.That(sound.DisabledLayers, Has.Some.StartsWith("engine firing"));
                Assert.That(sound.DisabledLayers, Has.Some.StartsWith("hatch covers"));
                Assert.That(sound.DisabledLayers, Has.Some.StartsWith("anchors and mooring"));
                foreach (AudioSource source in ship.GetComponentsInChildren<AudioSource>(true))
                    Assert.That(source.name, Does.Not.StartWith("Loop_Firing").And.Not.StartWith("Loop_StartingAir")
                        .And.Not.StartWith("Loop_Chain").And.Not.StartWith("Loop_HydraulicPump"), id);
            }
            int played = sound.EventsPlayed;
            ship.SetThrottleCommand(-0.7f);
            Run(ship, sound, 30f);
            if (!volgoDon) Assert.That(sound.EventsPlayed, Is.EqualTo(played), id + ": a reversal plays no starting air.");
            Record(new Result { name = "vessel " + id, activeVoices = sound.ActiveVoices, notes = string.Join("; ", sound.DisabledLayers) });
            yield return null;
        }

        // On foils the hull leaves the water; the soundscape follows the support the physics computed.
        [UnityTest]
        public IEnumerator Hydrofoil_HullWaterFadesAsTheFoilsLiftHer()
        {
            (ShipPhysicsController ship, VesselSoundscape sound) = Spawn(20f, "meteor-342u");
            ship.SetThrottleCommand(0.35f);
            float slowFlow = 0f, slowSupport = 1f;
            for (int i = 0; i < 1500 && ship.Diagnostics.SupportFraction < 0.02f; i++)
            {
                Step(ship, sound, Dt);
                if (ship.RelativeWaterVelocity.magnitude > 3f) { slowFlow = sound.WaterLayer("FlowPort").TargetVolume; slowSupport = ship.Diagnostics.SupportFraction; }
            }
            ship.SetThrottleCommand(1f);
            Run(ship, sound, 90f);
            float support = ship.Diagnostics.SupportFraction;
            Assert.That(slowSupport, Is.LessThan(0.05f));
            Assert.That(support, Is.GreaterThan(0.8f), "Foilborne at full ahead.");
            float hull = SupportModel.ImmersionAtFraction(support);
            float fullFlow = sound.Profile.hullFlow.volume * hull;
            Assert.That(sound.WaterLayer("FlowPort").TargetVolume, Is.LessThanOrEqualTo(fullFlow + 1e-4f), "Hull flow scales with the hull still in the water.");
            Assert.That(sound.WaterLayer("FlowPort").TargetVolume, Is.LessThan(slowFlow), "Foilborne, less hull in the water than when floating.");
            Record(new Result { name = "meteor foilborne", notes = $"support {support:0.00}, hull flow {sound.WaterLayer("FlowPort").TargetVolume:0.000} vs {slowFlow:0.000} floating" });
            yield return null;
        }

        // The same commands give the same trajectory with the soundscape working or removed: it never feeds back.
        [UnityTest]
        public IEnumerator Soundscape_DoesNotChangeTheSimulation()
        {
            Vector3[] withSound = Trajectory(true), without = Trajectory(false);
            for (int i = 0; i < withSound.Length; i++) Assert.That(withSound[i], Is.EqualTo(without[i]), "step " + i);
            yield return null;
        }

        private Vector3[] Trajectory(bool sound)
        {
            (ShipPhysicsController ship, VesselSoundscape soundscape) = Spawn(8f);
            if (!sound) Object.DestroyImmediate(soundscape);
            ship.GroundTackle.SetChainCommand(0, ChainCommand.LetGo);
            ship.Mechanisms.SetHatches(HatchPosition.HalfOpen);
            ship.SetEngineCommand(0, 0.8f);
            ship.SetEngineCommand(1, -0.4f);
            var path = new Vector3[60];
            for (int i = 0; i < 3000; i++)
            {
                ship.Mechanisms.Advance(Dt, 0, 0, 0);
                Step(ship, sound ? soundscape : null, Dt);
                if (i % 50 == 0) path[i / 50] = ship.Body.position;
            }
            Object.DestroyImmediate(ship.gameObject);
            return path;
        }

        // ---- Helpers ----

        private (ShipPhysicsController, VesselSoundscape) Spawn(float depth = 20f, string id = "volgodon-507b")
        {
            GameObject vessel = Object.Instantiate(VesselCatalogue.Load().Find(id).prefab);
            created.Add(vessel);
            var ship = vessel.GetComponent<ShipPhysicsController>();
            var sound = vessel.GetComponent<VesselSoundscape>();
            Assert.That(sound, Is.Not.Null, id + " carries its soundscape.");
            sound.ManualTicking = true;
            ship.ManualStepping = true;
            ship.SetDepthProvider(_ => depth);
            ship.SetAmbientCurrent(Vector3.zero);
            ship.SetWindVelocity(Vector3.zero);
            ship.RestoreVoyage(new VoyageSave { position = Vector3.zero, engineCommands = new float[ship.EngineCount], shaftRps = new float[ship.EngineCount] });
            Run(ship, sound, 2f);
            Assert.That(sound.IsBuilt, Is.True);
            return (ship, sound);
        }

        private static VoyageSave State(ShipPhysicsController ship) => new VoyageSave
        {
            position = ship.Body.position, rotation = ship.Body.rotation, velocity = ship.Body.linearVelocity,
            angularVelocity = ship.Body.angularVelocity, engineCommands = ship.CaptureEngineCommands(),
            shaftRps = ship.CaptureShaftRps(), rudder = ship.RudderCommand, rudderAngle = ship.RudderAngleDeg
        };

        private static void Run(ShipPhysicsController ship, VesselSoundscape sound, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++) Step(ship, sound, Dt);
        }

        private static void Step(ShipPhysicsController ship, VesselSoundscape sound, float dt)
        {
            ship.Simulate(dt);
            UnityEngine.Physics.Simulate(dt);
            if (sound != null) sound.Tick(dt, dt);
        }

        private static void Tick(VesselSoundscape sound, float dt) => sound.Tick(dt, dt);

        private static void Record(Result result)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
            File.AppendAllText(ResultPath, JsonUtility.ToJson(result) + "\n");
            Debug.Log("SOUNDSCAPE_CASE|" + JsonUtility.ToJson(result));
        }
    }
}
#endif
