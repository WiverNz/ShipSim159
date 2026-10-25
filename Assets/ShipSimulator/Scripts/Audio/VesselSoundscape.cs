using Unity.Profiling;
using System.Collections.Generic;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEngine;

namespace ShipSimulator.Audio
{
    // Engine, propeller, water, wind and deck-machinery sound for any vessel, driven only by what the simulation
    // computed: shaft rpm, fuel (engine torque) and reversal of each engine, propeller thrust and immersion, speed
    // through the water and how much of the hull is still in it, wind relative to the ship, hatch motion and the
    // chain and line rates of GroundTackle. It reads that state once a frame after the physics and machinery have
    // moved and never writes back, so its smoothing cannot change the physics or a command. Pitch follows rpm, not
    // the simulation speed. The profile chooses the clips; the vessel data says what equipment exists. A layer runs
    // only when both allow it, and DisabledLayers says why one does not.
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(ShipPhysicsController))]
    public sealed class VesselSoundscape : MonoBehaviour
    {
        private static readonly ProfilerMarker PerformanceMarker = new ProfilerMarker(ProfilerCategory.Scripts, "ShipSim.Audio");

        public const string PointsName = "AudioPoints";
        private const float EventCooldown = 0.25f;
        private const float RestoreQuiet = 0.3f;
        private const int MaxEventIndex = 16;
        // Shaft turning and blade-rate thresholds as fractions of rated speed: 4, 30, 8 and 50 rpm on the 507B.
        private const float TurningFrom = 4f / 375f, TurningTo = 30f / 375f, BladeFrom = 8f / 375f, BladeTo = 50f / 375f;

        [SerializeField] private VesselSoundProfile profile;
        [SerializeField] private string[] anchorPoints = { "Windlass_Port", "Windlass_Starboard", "Capstan_Stern" };

        private sealed class EngineVoices
        {
            public LoopVoice FiringLow, FiringHigh, Room, Exterior, AirBrake, Blade, Wash;
            public Transform Point;
            public int Direction;
        }

        private sealed class AnchorVoices
        {
            public LoopVoice Run, Heave, Motor;
            public Transform Point;
            public ChainCommand Command;
            public bool MotorOn;
        }

        private sealed class StationVoices
        {
            public LoopVoice Motor, Strain;
            public Transform Point;
            public bool WinchOn;
            public float LastTension, CreakBudget, LastCreak = float.NegativeInfinity;
        }

        // A machinery drive's running loop and its start and stop events, chosen by the drive named in the data.
        private struct Drive
        {
            public VesselSoundProfile.Layer Loop;
            public AudioClip Start, Stop;
        }

        private struct Pending
        {
            public TackleEvent Change;
            public int Index;
        }

        private ShipPhysicsController ship;
        private VesselMechanisms mechanisms;
        private GroundTackle tackle;
        private readonly List<LoopVoice> voices = new List<LoopVoice>();
        private readonly List<string> disabled = new List<string>();
        private EngineVoices[] engines = new EngineVoices[0];
        private AnchorVoices[] anchors = new AnchorVoices[0];
        private StationVoices[] stations = new StationVoices[0];
        private LoopVoice flowPort, flowStarboard, lapBow, lapAft, bowWave, windSteady, windStrong;
        private LoopVoice[] pumps = new LoopVoice[0], winches = new LoopVoice[0], rolling = new LoopVoice[0];
        private Transform[] hatchPoints = new Transform[0];
        private Transform pointsRoot, bowPoint, root;
        private GameObject holder;
        private OneShotPool pool;
        private readonly Pending[] queue = new Pending[32];
        private int queued;
        private readonly float[] lastEvent = new float[16 * MaxEventIndex];
        private System.Random random = new System.Random(507);
        private Transform listener;
        private bool built, subscribed, airStart, hasWheelhouse;
        private Drive windlass, mooringWinch;
        private float engineRpmPerShaftRpm, firingPerEngineRev, bladesPerRev;
        private int seenRestore = int.MinValue;
        private float quietUntil = float.NegativeInfinity;
        private float clock;
        private float thrustReference = 1f;
        private float lastOpening, lastParking;
        private int hatchDrive, hatchDirection;
        private float interior;

        // Every one-shot as it starts, for checks and diagnostics.
        public event System.Action<AudioClip, Transform> Played;
        // Tests drive Tick themselves.
        public bool ManualTicking { get; set; }
        public VesselSoundProfile Profile => profile;
        public bool IsBuilt => built;
        public float Interior => interior;
        public OneShotPool Pool => pool;
        public IReadOnlyList<LoopVoice> Voices => voices;
        // Layers this vessel does not play, each with the reason; filled when the soundscape is built.
        public IReadOnlyList<string> DisabledLayers => disabled;
        public int EventsPlayed => pool != null ? pool.Played : 0;
        public int HatchDrive => hatchDrive;

        public int ActiveVoices
        {
            get
            {
                int count = pool != null ? pool.Active : 0;
                for (int i = 0; i < voices.Count; i++) if (voices[i].Playing) count++;
                return count;
            }
        }

        public void Configure(VesselSoundProfile soundProfile) => profile = soundProfile;

        public LoopVoice EngineLayer(int engine, string layer)
        {
            if (engine < 0 || engine >= engines.Length) return null;
            EngineVoices e = engines[engine];
            return layer switch
            {
                "FiringLow" => e.FiringLow, "FiringHigh" => e.FiringHigh, "Room" => e.Room, "Exterior" => e.Exterior,
                "AirBrake" => e.AirBrake, "Blade" => e.Blade, "Wash" => e.Wash, _ => null
            };
        }

        public LoopVoice WaterLayer(string layer) => layer switch
        {
            "FlowPort" => flowPort, "FlowStarboard" => flowStarboard, "LapBow" => lapBow, "LapAft" => lapAft, "BowWave" => bowWave,
            "WindSteady" => windSteady, "WindStrong" => windStrong, _ => null
        };

        public LoopVoice HatchLayer(int point, string layer)
        {
            if (point < 0 || point >= hatchPoints.Length) return null;
            return layer switch { "Pump" => pumps[point], "Winch" => winches[point], "Rolling" => rolling[point], _ => null };
        }

        public LoopVoice AnchorLayer(int anchor, string layer)
        {
            if (anchor < 0 || anchor >= anchors.Length) return null;
            return layer switch { "Run" => anchors[anchor].Run, "Heave" => anchors[anchor].Heave, "Motor" => anchors[anchor].Motor, _ => null };
        }

        public LoopVoice StationLayer(int station, string layer)
        {
            if (station < 0 || station >= stations.Length || stations[station] == null) return null;
            return layer == "Motor" ? stations[station].Motor : layer == "Strain" ? stations[station].Strain : null;
        }

        private void Awake()
        {
            ship = GetComponent<ShipPhysicsController>();
            mechanisms = GetComponent<VesselMechanisms>();
            for (int i = 0; i < lastEvent.Length; i++) lastEvent[i] = float.NegativeInfinity;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            pool?.StopAll();
            foreach (LoopVoice voice in voices) voice.Silence();
        }

        // The voices hang under the emitter points, so removing only this component must take them down too.
        private void OnDestroy()
        {
            Unsubscribe();
            foreach (LoopVoice voice in voices) voice.Dispose();
            voices.Clear();
            pool?.Dispose();
            if (holder != null) Destroy(holder);
            AudioMix.SetInterior(0f);
        }

        private void LateUpdate()
        {
            if (!ManualTicking) Tick(Time.unscaledDeltaTime, Time.deltaTime);
        }

        // realDt drives the smoothing, so transitions keep their length at any simulation speed; simDt tells
        // whether the simulation moved this frame (zero while paused).
        public void Tick(float realDt, float simDt)
        {
            using var measurement = PerformanceMarker.Auto();
            if (!built && !Build()) return;
            clock += realDt;
            bool restored = ship.RestoreCount != seenRestore;
            seenRestore = ship.RestoreCount;
            if (restored)
            {
                queued = 0;
                quietUntil = clock + RestoreQuiet;
            }
            UpdateInterior(realDt);
            UpdateEngines(restored);
            UpdateWater();
            UpdateHatches(simDt, restored);
            UpdateTackle(simDt, restored);
            DrainEvents();
            for (int i = 0; i < voices.Count; i++)
            {
                if (restored) voices[i].Resync();
            }
            TickVoices(realDt);
        }

        private void TickVoices(float dt)
        {
            foreach (EngineVoices e in engines)
            {
                e.FiringLow?.Tick(dt, 0.25f, 0.4f, 0.12f);
                e.FiringHigh?.Tick(dt, 0.25f, 0.4f, 0.12f);
                e.Room?.Tick(dt, 0.4f, 0.6f, 0.2f);
                e.Exterior?.Tick(dt, 0.4f, 0.6f, 0.2f);
                e.AirBrake?.Tick(dt, 0.05f, 0.35f, 0.1f);
                e.Blade?.Tick(dt, 0.3f, 0.5f, 0.12f);
                e.Wash?.Tick(dt, 0.5f, 0.8f, 0.3f);
            }
            flowPort?.Tick(dt, 0.6f, 0.8f, 0.4f);
            flowStarboard?.Tick(dt, 0.6f, 0.8f, 0.4f);
            lapBow?.Tick(dt, 0.8f, 0.8f, 0.4f);
            lapAft?.Tick(dt, 0.8f, 0.8f, 0.4f);
            bowWave?.Tick(dt, 0.6f, 0.8f, 0.4f);
            windSteady?.Tick(dt, 0.5f, 0.7f, 0.5f);
            windStrong?.Tick(dt, 0.5f, 0.7f, 0.5f);
            for (int i = 0; i < hatchPoints.Length; i++)
            {
                pumps[i]?.Tick(dt, 0.15f, 0.25f, 0.2f);
                winches[i]?.Tick(dt, 0.15f, 0.25f, 0.2f);
                rolling[i]?.Tick(dt, 0.2f, 0.3f, 0.2f);
            }
            foreach (AnchorVoices a in anchors)
            {
                a.Run?.Tick(dt, 0.08f, 0.25f, 0.15f);
                a.Heave?.Tick(dt, 0.15f, 0.25f, 0.15f);
                a.Motor?.Tick(dt, 0.15f, 0.25f, 0.2f);
            }
            foreach (StationVoices s in stations)
            {
                if (s == null) continue;
                s.Motor?.Tick(dt, 0.15f, 0.25f, 0.2f);
                s.Strain?.Tick(dt, 0.4f, 0.6f, 0.3f);
            }
        }

        // ---- Build ----

        private bool Build()
        {
            if (profile == null || ship == null || ship.Model == null || ship.Parameters == null) return false;
            built = true;
            VesselData data = ship.Data;
            VesselEngine engine = data.engine;
            tackle = ship.GroundTackle;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t.name == PointsName) { pointsRoot = t; break; }
            holder = new GameObject("Soundscape") { hideFlags = HideFlags.DontSave };
            holder.transform.SetParent(transform, false);
            root = holder.transform;

            float ratedRps = ship.Parameters.RatedRps;
            thrustReference = Mathf.Max(1f, Mathf.Abs(PropellerModel.Evaluate(data.propeller, ship.Parameters.Rho, ratedRps, 0f,
                out _, out _) * data.calibration.thrustMultiplier));

            // Firing needs the engine's own speed: shaft rpm times a known gear ratio, and a clip made for this engine.
            engineRpmPerShaftRpm = engine.engineRpmPerShaftRpm;
            bool firing = Require(engine.cylinders > 0 && engine.strokesPerCycle > 0, "engine firing", "cylinders or cycle not established in the vessel data") &&
                Require(engineRpmPerShaftRpm > 0f, "engine firing", "engine-to-shaft speed ratio not established in the vessel data") &&
                Require(profile.firingLow.clip != null && profile.firingHigh.clip != null, "engine firing", "no firing clip in the profile") &&
                Require(engine.cylinders == profile.firingClipCylinders && engine.strokesPerCycle == profile.firingClipStrokes, "engine firing",
                    $"the firing clips are for {profile.firingClipCylinders} cylinders, {profile.firingClipStrokes}-stroke");
            firingPerEngineRev = firing ? engine.cylinders * 2f / engine.strokesPerCycle : 0f;
            airStart = Require(engine.directReversingAirStart, "starting air", "the engine is not established as direct-reversing with air start") &&
                Require(profile.startingAirBrake.clip != null || profile.startingAir != null, "starting air", "no starting-air clip in the profile");
            bool blade = Require(data.propeller.bladeCount > 0, "propeller blade rate", "blade count not established in the vessel data") &&
                Require(profile.bladeRate.clip != null, "propeller blade rate", "no blade-rate clip in the profile") &&
                Require(data.propeller.bladeCount == profile.bladeClipBlades, "propeller blade rate", $"the clip is for {profile.bladeClipBlades} blades");
            bladesPerRev = blade ? data.propeller.bladeCount : 0f;

            float halfBeam = 0.5f * data.dimensions.beamOverallM, halfLength = 0.5f * ship.Parameters.Lpp;
            engines = new EngineVoices[ship.EngineCount];
            for (int i = 0; i < engines.Length; i++)
            {
                string side = engines.Length == 2 ? (i == 0 ? "Port" : "Starboard") : i.ToString();
                // Without a model point, an estimate from the vessel data: the propeller's position, at the waterline.
                var shaftLine = new Vector3(data.propeller.lateralPositionsM[i], 0f, data.propeller.longitudinalPositionsM[i]);
                Transform engineAt = Point("Engine_" + side, shaftLine);
                Transform exhaust = Point("Exhaust_" + side, null) ?? engineAt;
                Transform propeller = Point($"Propeller_{side}_Audio", shaftLine + Vector3.down * (0.5f * ship.Parameters.Draft));
                engines[i] = new EngineVoices
                {
                    Point = engineAt,
                    FiringLow = firing ? Voice(profile.firingLow, engineAt, "FiringLow" + side, AudioBus.Engines, 64) : null,
                    FiringHigh = firing ? Voice(profile.firingHigh, engineAt, "FiringHigh" + side, AudioBus.Engines, 64) : null,
                    Room = Voice(profile.engineRoom, engineAt, "EngineRoom" + side, AudioBus.Engines, 70),
                    Exterior = Voice(profile.engineExterior, exhaust, "EngineExterior" + side, AudioBus.Engines, 66),
                    AirBrake = airStart ? Voice(profile.startingAirBrake, engineAt, "StartingAir" + side, AudioBus.Engines, 60) : null,
                    Blade = blade ? Voice(profile.bladeRate, propeller, "BladeRate" + side, AudioBus.Engines, 90) : null,
                    Wash = Voice(profile.propellerWash, propeller, "PropellerWash" + side, AudioBus.Water, 80)
                };
            }
            bowPoint = Point("Water_Bow", new Vector3(0f, 0f, halfLength));
            Transform port = Point("Hull_Port", new Vector3(-halfBeam, 0f, 0f));
            Transform starboard = Point("Hull_Starboard", new Vector3(halfBeam, 0f, 0f));
            flowPort = Voice(profile.hullFlow, port, "HullFlowPort", AudioBus.Water, 100);
            flowStarboard = Voice(profile.hullFlow, starboard, "HullFlowStarboard", AudioBus.Water, 100);
            lapBow = Voice(profile.hullLap, bowPoint, "HullLapBow", AudioBus.Water, 120);
            lapAft = Voice(profile.hullLap, starboard, "HullLapAft", AudioBus.Water, 120);
            bowWave = Voice(profile.bowWave, bowPoint, "BowWave", AudioBus.Water, 100);
            windSteady = Voice(profile.windSteady, root, "WindSteady", AudioBus.Wind, 110, false);
            windStrong = Voice(profile.windStrong, root, "WindStrong", AudioBus.Wind, 110, false);
            hasWheelhouse = Require(profile.wheelhouseHalfExtents.x > 0f && profile.wheelhouseHalfExtents.y > 0f &&
                profile.wheelhouseHalfExtents.z > 0f, "wheelhouse filter", "the model has no wheelhouse interior to listen from");

            // The modelled hatch covers are lifted and lowered by hydraulic jacks and slid by electric winches.
            if (Require(mechanisms != null && mechanisms.IsConfigured, "hatch covers", "no hatch cover mechanism is modelled"))
            {
                hatchPoints = new[] { Point("HatchMachinery_Aft", Vector3.zero), Point("HatchMachinery_Fore", Vector3.zero) };
                pumps = new LoopVoice[2]; winches = new LoopVoice[2]; rolling = new LoopVoice[2];
                for (int i = 0; i < 2; i++)
                {
                    string hold = i == 0 ? "Aft" : "Fore";
                    pumps[i] = Voice(profile.hydraulicPump, hatchPoints[i], "HydraulicPump" + hold, AudioBus.Machinery, 80);
                    winches[i] = Voice(profile.electricMotor, hatchPoints[i], "CoverWinch" + hold, AudioBus.Machinery, 80);
                    rolling[i] = Voice(profile.coverRolling, hatchPoints[i], "CoverRolling" + hold, AudioBus.Machinery, 85);
                }
                lastOpening = mechanisms.Opening;
                lastParking = mechanisms.Parking;
            }

            if (Require(tackle != null, "anchors and mooring", "no ground tackle is fitted in the vessel data"))
            {
                VesselGroundTackle spec = tackle.Spec;
                windlass = DriveFor(spec.windlassDrive, "windlass");
                mooringWinch = DriveFor(spec.mooringWinchDrive, "mooring winches");
                anchors = new AnchorVoices[tackle.AnchorCount];
                for (int i = 0; i < anchors.Length; i++)
                {
                    Transform point = Point(i < anchorPoints.Length ? anchorPoints[i] : "Windlass_Audio",
                        tackle.HawseLocal(i));
                    anchors[i] = new AnchorVoices
                    {
                        Point = point, Command = tackle.Command(i),
                        Run = Voice(profile.chainRun, point, "ChainRun" + i, AudioBus.Machinery, 75),
                        Heave = Voice(profile.chainHeave, point, "ChainHeave" + i, AudioBus.Machinery, 75),
                        Motor = windlass.Loop != null ? Voice(windlass.Loop, point, "Windlass" + i, AudioBus.Machinery, 78) : null
                    };
                }
                stations = new StationVoices[tackle.StationCount];
            }
            pool = new OneShotPool(root, Mathf.Max(1, profile.poolSize), 8f, 40, AudioMix.Group(AudioBus.Machinery));
            AudioMix.Apply(true);
            Subscribe();
            return true;
        }

        private bool Require(bool condition, string layer, string reason)
        {
            if (!condition && !disabled.Contains(layer + ": " + reason)) disabled.Add(layer + ": " + reason);
            return condition;
        }

        private Drive DriveFor(string drive, string machine)
        {
            if (drive == "electric") return new Drive { Loop = profile.electricMotor, Start = profile.electricMotorStart, Stop = profile.electricMotorStop };
            if (drive == "electrohydraulic") return new Drive { Loop = profile.hydraulicPump, Start = profile.hydraulicStart, Stop = profile.hydraulicStop };
            Require(false, machine + " motor", "drive not established in the vessel data");
            return default;
        }

        // A named point under AudioPoints, else one made at the given vessel-root position (an estimate), else null.
        private Transform Point(string name, Vector3? estimate)
        {
            if (pointsRoot != null)
                foreach (Transform child in pointsRoot)
                    if (child.name == name) return child;
            if (estimate == null) return null;
            var point = new GameObject(name) { hideFlags = HideFlags.DontSave };
            point.transform.SetParent(root, false);
            point.transform.localPosition = estimate.Value;
            return point.transform;
        }

        // Null when the profile gives the layer no clip: the layer is off.
        private LoopVoice Voice(VesselSoundProfile.Layer layer, Transform at, string name, AudioBus bus, int priority, bool spatial = true)
        {
            if (layer == null || layer.clip == null)
            {
                Require(false, name, "no clip in the profile");
                return null;
            }
            var voice = new LoopVoice(at, "Loop_" + name, layer.clip, AudioMix.Group(bus), layer.minDistance, layer.spread, priority, spatial);
            voices.Add(voice);
            return voice;
        }

        // ---- Listener ----

        private void UpdateInterior(float dt)
        {
            if (listener == null)
            {
                AudioListener found = FindAnyObjectByType<AudioListener>();
                listener = found != null ? found.transform : null;
            }
            float target = 0f;
            if (listener != null && hasWheelhouse)
            {
                Vector3 local = transform.InverseTransformPoint(listener.position) - profile.wheelhouseCentre;
                Vector3 half = profile.wheelhouseHalfExtents;
                target = Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z ? 1f : 0f;
            }
            interior += (target - interior) * (1f - Mathf.Exp(-dt / 0.12f));
            if (Mathf.Abs(target - interior) < 0.002f) interior = target;
            AudioMix.SetInterior(interior);
        }

        // ---- Engines and propellers ----

        private void UpdateEngines(bool restored)
        {
            float ratedRpm = ship.Parameters.RatedRps * 60f;
            float immersion = Mathf.Clamp01(ship.PropellerImmersion);
            for (int i = 0; i < engines.Length; i++)
            {
                EngineVoices e = engines[i];
                EngineShaft shaft = ship.Model.Shafts[i];
                float rpm = Mathf.Abs(ship.ShaftRpm(i));
                float r = Mathf.Clamp01(rpm / ratedRpm);
                float fuel = shaft.FuelFraction;
                float turning = Smooth(TurningFrom, TurningTo, r);
                // Combustion only while the engine takes fuel; a shaft coasting with the fuel cut is mechanical noise.
                float firing = Smooth(0f, 0.03f, fuel) * (0.45f + 0.55f * Mathf.Sqrt(fuel)) * turning;
                // Firings per second from the engine's own speed: shaft rpm times the gear ratio.
                float firingHz = rpm * engineRpmPerShaftRpm / 60f * firingPerEngineRev;
                float high = Smooth(profile.firingCrossfadeHz.x, profile.firingCrossfadeHz.y, firingHz);
                e.FiringLow?.Set(profile.firingLow.volume * firing * Mathf.Sqrt(1f - high), firingHz / profile.firingLowHz);
                e.FiringHigh?.Set(profile.firingHigh.volume * firing * Mathf.Sqrt(high), firingHz / profile.firingHighHz);
                e.Room?.Set(profile.engineRoom.volume * turning * (0.35f + 0.65f * r), 0.8f + 0.3f * r);
                e.Exterior?.Set(profile.engineExterior.volume * turning * Mathf.Lerp(0.2f, 1f, r) * (0.45f + 0.55f * Mathf.Sqrt(fuel)),
                    0.75f + 0.35f * r);
                e.AirBrake?.Set(shaft.IsAirBraking ? profile.startingAirBrake.volume : 0f, 1f);
                e.Blade?.Set(profile.bladeRate.volume * Smooth(BladeFrom, BladeTo, r) * (0.3f + 0.7f * r),
                    Mathf.Max(0.05f, rpm / 60f * bladesPerRev / profile.bladeClipHz));
                float thrust = Mathf.Abs(ship.PropellerThrustN(i)) / thrustReference;
                // A disc near the surface throws more spray for the same thrust.
                e.Wash?.Set(profile.propellerWash.volume * Mathf.Clamp01(Mathf.Pow(thrust, 0.6f)) * (1f + 0.4f * (1f - immersion)),
                    0.85f + 0.2f * r);
                int direction = shaft.Direction;
                if (airStart && !restored && direction != 0 && direction != e.Direction) Play(profile.startingAir, e.Point, 0.8f, AudioBus.Engines);
                e.Direction = direction;
            }
        }

        // ---- Water and wind ----

        private void UpdateWater()
        {
            Vector3 relative = ship.RelativeWaterVelocity;
            float speed = relative.magnitude;
            float surge = Vector3.Dot(relative, transform.forward);
            float full = profile.fullFlowSpeedMps;
            // The part of the hull still working in the water: all of it afloat, less as foils or a cushion lift it.
            float hull = SupportModel.ImmersionAtFraction(ship.Diagnostics.SupportFraction);
            float flow = Mathf.Pow(Smooth(0.2f, full, speed), 0.8f) * hull;
            float flowPitch = 0.85f + 0.25f * Mathf.Clamp01(speed / full);
            flowPort?.Set(profile.hullFlow.volume * flow, flowPitch);
            flowStarboard?.Set(profile.hullFlow.volume * flow, flowPitch * 1.02f);
            bowWave?.Set(profile.bowWave.volume * Smooth(1f, full, surge) * hull, 0.9f + 0.2f * Mathf.Clamp01(surge / full));

            Vector3 trueWind = ship.WindVelocityMps;
            trueWind.y = 0f;
            float chop = 0.35f + 0.65f * Smooth(2f, 12f, trueWind.magnitude);
            float lap = profile.hullLap.volume * chop * (1f - 0.7f * Smooth(1f, 3f, speed)) * hull;
            lapBow?.Set(lap, 1f);
            lapAft?.Set(lap * 0.8f, 0.95f);

            Vector3 air = ship.WindVelocityMps - (ship.Body != null ? ship.Body.linearVelocity : Vector3.zero);
            air.y = 0f;
            float wind = air.magnitude;
            windSteady?.Set(profile.windSteady.volume * Mathf.Pow(Smooth(1.5f, 14f, wind), 1.3f), 0.85f + 0.3f * Mathf.Clamp01(wind / 20f));
            windStrong?.Set(profile.windStrong.volume * Smooth(9f, 20f, wind), 0.9f + 0.2f * Mathf.Clamp01((wind - 9f) / 12f));
        }

        // ---- Hatches ----

        private void UpdateHatches(float simDt, bool restored)
        {
            if (hatchPoints.Length == 0) return;
            float opening = mechanisms.Opening, parking = mechanisms.Parking;
            float dOpening = opening - lastOpening, dParking = parking - lastParking;
            lastOpening = opening;
            lastParking = parking;
            int drive = hatchDrive, direction = hatchDirection;
            // A paused frame moves nothing, which must not read as the covers stopping.
            if (simDt > 0f || restored)
            {
                bool moving = !restored && Mathf.Abs(dOpening) + Mathf.Abs(dParking) > 1e-7f;
                HatchPhase phase = mechanisms.Phase;
                // The winches pull the stack along and slide the inner covers; the jacks do every vertical move, including
                // leaving Closed or arriving at Stacked, which the phase reports by its end state.
                drive = !moving ? 0 : Mathf.Abs(dParking) > 0f || phase == HatchPhase.SlidingInner ? 2 : 1;
                direction = !moving ? 0 : Mathf.Abs(dParking) > 0f ? 2 * (int)Mathf.Sign(dParking) : (int)Mathf.Sign(dOpening);
                if (!restored) HatchEvents(drive, direction, phase);
            }
            hatchDrive = drive;
            hatchDirection = direction;
            for (int i = 0; i < hatchPoints.Length; i++)
            {
                pumps[i]?.Set(drive == 1 ? profile.hydraulicPump.volume : 0f, 1f);
                winches[i]?.Set(drive == 2 ? profile.electricMotor.volume : 0f, 1f);
                rolling[i]?.Set(drive == 2 ? profile.coverRolling.volume : 0f, 1f);
            }
        }

        private void HatchEvents(int drive, int direction, HatchPhase phase)
        {
            if (drive == hatchDrive && (drive == 0 || direction == hatchDirection)) return;
            for (int i = 0; i < hatchPoints.Length; i++)
            {
                if (hatchDrive != 0 && drive != hatchDrive) Play(hatchDrive == 1 ? profile.hydraulicStop : profile.electricMotorStop, hatchPoints[i], 0.7f);
                if (drive != 0 && drive == hatchDrive) Play(Pick(profile.metalClunk), hatchPoints[i], 0.6f);
                if (drive != 0) Play(drive == 1 ? profile.hydraulicStart : profile.electricMotorStart, hatchPoints[i], 0.7f);
                if (drive == 0 && (phase == HatchPhase.Closed || phase == HatchPhase.Stacked || phase == HatchPhase.HoldOpen))
                    Play(Pick(profile.heavyImpact), hatchPoints[i], 0.9f);
            }
        }

        // ---- Anchors and mooring lines ----

        private void UpdateTackle(float simDt, bool restored)
        {
            if (tackle == null) return;
            VesselGroundTackle spec = tackle.Spec;
            for (int i = 0; i < anchors.Length; i++)
            {
                AnchorVoices a = anchors[i];
                VesselAnchor anchor = spec.anchors[Mathf.Min(i, spec.anchors.Length - 1)];
                float rate = tackle.ChainRateMps(i);
                ChainCommand command = tackle.Command(i);
                float payout = Mathf.Max(0f, rate), heave = Mathf.Max(0f, -rate);
                a.Run?.Set(profile.chainRun.volume * Smooth(0.005f, spec.letGoMps, payout), 0.8f + 0.4f * Mathf.Clamp01(payout / spec.letGoMps));
                float links = heave / Mathf.Max(0.01f, profile.linkPitchDiameters * anchor.chainDiameterMm / 1000f);
                a.Heave?.Set(profile.chainHeave.volume * Smooth(0f, 0.5f * anchor.heaveMps, heave),
                    Mathf.Clamp(links / profile.recordedLinksPerSecond, 0.6f, 1.5f));
                bool motorOn = command == ChainCommand.Heave && !tackle.Stalled(i) && tackle.Phase(i) != AnchorPhase.Stowed &&
                    tackle.Phase(i) != AnchorPhase.Lost;
                a.Motor?.Set(motorOn ? windlass.Loop.volume : 0f, 0.8f + 0.25f * Mathf.Clamp01(heave / anchor.heaveMps));
                if (!restored)
                {
                    if (motorOn && !a.MotorOn) Play(windlass.Start, a.Point, 0.8f);
                    if (!motorOn && a.MotorOn) Play(windlass.Stop, a.Point, 0.8f);
                    bool wasRunning = a.Command == ChainCommand.LetGo || a.Command == ChainCommand.Veer;
                    bool running = command == ChainCommand.LetGo || command == ChainCommand.Veer;
                    // The band brake: released to let go or veer, set when the chain is to stop.
                    if (running != wasRunning && tackle.Phase(i) != AnchorPhase.Lost) Play(Pick(profile.metalClunk), a.Point, 0.9f);
                }
                a.MotorOn = motorOn;
                a.Command = command;
            }

            float breaking = Mathf.Max(1f, tackle.LineBreakingLoadN);
            for (int s = 0; s < stations.Length; s++)
            {
                int k = tackle.LineAt(s);
                StationVoices v = stations[s];
                if (k < 0 && v == null) continue;
                if (v == null) v = stations[s] = CreateStation(s);
                if (k < 0)
                {
                    if (v.WinchOn && !restored) Play(mooringWinch.Stop, v.Point, 0.7f);
                    v.WinchOn = false;
                    v.Motor?.Set(0f, 1f);
                    v.Strain?.Set(0f, 1f);
                    v.LastTension = 0f;
                    continue;
                }
                float rate = tackle.LineRateMps(k);
                LineCommand command = tackle.LineCommandAt(k);
                // The winch drive runs while it is ordered to haul or slack, also when it is held at its stall pull;
                // only its note follows the line speed.
                bool winchOn = command != LineCommand.Hold && mooringWinch.Loop != null;
                VesselMooringStation station = tackle.StationSpec(s);
                v.Motor?.Set(winchOn ? mooringWinch.Loop.volume * 0.8f : 0f, 0.85f + 0.25f * Mathf.Clamp01(Mathf.Abs(rate) / station.haulMps));
                float tension = tackle.LineTensionN(k);
                float load = tension / breaking;
                LineState state = tackle.LineStateAt(k);
                v.Strain?.Set(state == LineState.Slack ? 0f : profile.lineStrain.volume * Smooth(0.02f, 0.5f, load), 0.9f + 0.2f * Mathf.Clamp01(load));
                if (!restored)
                {
                    if (winchOn && !v.WinchOn) Play(mooringWinch.Start, v.Point, 0.7f);
                    if (!winchOn && v.WinchOn) Play(mooringWinch.Stop, v.Point, 0.7f);
                    // Creaks come with changing load and more often under heavy load; at most one each 0.6 s.
                    if (simDt > 0f && state != LineState.Slack)
                    {
                        v.CreakBudget += Mathf.Abs(tension - v.LastTension) / (0.02f * breaking) + simDt * 0.25f * load;
                        if (v.CreakBudget >= 1f && clock - v.LastCreak > 0.6f)
                        {
                            Play(Pick(profile.lineCreak), v.Point, Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(load * 2f)));
                            v.CreakBudget = -(float)random.NextDouble();
                            v.LastCreak = clock;
                        }
                    }
                }
                v.WinchOn = winchOn;
                v.LastTension = tension;
            }
        }

        private StationVoices CreateStation(int s)
        {
            Transform point = tackle.Station(s).transform;
            return new StationVoices
            {
                Point = point,
                Motor = mooringWinch.Loop != null ? Voice(mooringWinch.Loop, point, "MooringWinch" + s, AudioBus.Machinery, 85) : null,
                Strain = Voice(profile.lineStrain, point, "LineStrain" + s, AudioBus.Machinery, 95)
            };
        }

        // ---- Events ----

        private void Subscribe()
        {
            if (subscribed || tackle == null) return;
            tackle.ChangedAt += OnTackleEvent;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (tackle != null) tackle.ChangedAt -= OnTackleEvent;
            subscribed = false;
        }

        // Raised inside the physics step; played once in the next frame.
        private void OnTackleEvent(TackleEvent change, int index)
        {
            if (queued < queue.Length) queue[queued++] = new Pending { Change = change, Index = index };
        }

        private void DrainEvents()
        {
            for (int n = 0; n < queued; n++)
            {
                Pending p = queue[n];
                int slot = (int)p.Change * MaxEventIndex + Mathf.Clamp(p.Index, 0, MaxEventIndex - 1);
                if (slot >= lastEvent.Length || clock - lastEvent[slot] < EventCooldown) continue;
                lastEvent[slot] = clock;
                switch (p.Change)
                {
                    case TackleEvent.ChainParted:
                        if (p.Index >= 0 && p.Index < anchors.Length) Play(profile.chainParted, anchors[p.Index].Point, 1f);
                        break;
                    case TackleEvent.AnchorStowed:
                        if (p.Index >= 0 && p.Index < anchors.Length) Play(Pick(profile.heavyImpact), anchors[p.Index].Point, 0.8f);
                        break;
                    case TackleEvent.LineParted:
                        if (p.Index >= 0 && p.Index < stations.Length)
                            Play(profile.lineParted, stations[p.Index] != null ? stations[p.Index].Point : tackle.Station(p.Index).transform, 1f);
                        break;
                }
            }
            queued = 0;
        }

        private void Play(AudioClip clip, Transform at, float volume, AudioBus bus = AudioBus.Machinery)
        {
            if (clip == null || clock < quietUntil) return;
            pool.Play(clip, at, Vector3.zero, AudioMix.Group(bus), volume * profile.eventVolume,
                0.97f + 0.06f * (float)random.NextDouble(), clock);
            Played?.Invoke(clip, at);
        }

        private AudioClip Pick(AudioClip[] clips) => clips == null || clips.Length == 0 ? null : clips[random.Next(clips.Length)];

        private static float Smooth(float from, float to, float value)
        {
            float t = Mathf.Clamp01((value - from) / Mathf.Max(1e-6f, to - from));
            return t * t * (3f - 2f * t);
        }
    }
}
