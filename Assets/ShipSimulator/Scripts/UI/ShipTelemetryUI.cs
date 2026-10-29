using System;
using System.Collections.Generic;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    public sealed class ShipTelemetryUI : MonoBehaviour
    {
        [SerializeField] private ShipPhysicsController ship;
        [SerializeField] private Text readout;

        private readonly Color panelColor = HudTheme.PanelFill;
        private readonly Color accentColor = HudTheme.Accent;
        private readonly Color warningColor = HudTheme.Warning;
        private Font font;
        private ShipFollowCamera followCamera;
        private Text speedText;
        private Text headingText;
        private Text depthText;
        private Text rudderText;
        private Text engineText;
        private Text currentText;
        private Text warningText;
        private Text cameraText;
        private Text headingTapeText;
        private Text objectiveText;
        private Text helpText;
        private Text depthRadarText;
        private Text timeOfDayText;
        private Text timeScaleText;
        private Text weatherText;
        private RectTransform rudderNeedle;
        private RectTransform rudderCommandNeedle;
        private RectTransform mapVessel;
        private RectTransform mapWaypoint;
        private RectTransform miniMap;
        private RectTransform warningPanel;
        private RectTransform mapWorld;
        private RectTransform mapViewport;
        private Text mapTitle;
        private MapChartData mapChart;
        private RiverShoreProfile mapShoreProfile;
        private bool mapShoresBuilt;
        private readonly List<(Button Button, Text Text, string Key)> mapButtons = new();
        private MapProjection mapProjection;
        private bool mapFollowing = true;
        private Vector3 mapCenter;
        private int mapRestoreCount;
        public bool MapFollowing => mapFollowing;
        public MapProjection CurrentMapProjection => mapProjection;
        public RectTransform MapViewport => mapViewport;
        public void ZoomMap(float ticks) => VoyageSettings.SetMapRange(VoyageSettings.MapRange * Mathf.Exp(-Mathf.Clamp(ticks, -10, 10) * .2f));
        public void RecenterMap() { mapFollowing = true; }
        public void PanMap(Vector2 pixels)
        {
            if (!VoyageSettings.MapExpanded) return;
            if (mapFollowing) mapCenter = mapProjection.Center;
            mapCenter += mapProjection.Unproject(Vector2.zero) - mapProjection.Unproject(pixels);
            mapFollowing = false;
        }

        private RadarChannel radarChannel;
        private readonly List<RadarChannel.Section> radarSections =
            new List<RadarChannel.Section>();
        private float radarMinAheadM;
        private RectTransform[] radarTrackSegments;
        private RectTransform[] radarPredictionSegments;
        private AudioSource hornSource;
        private DayNightController dayNight;
        private SimulationTimeController simulationTime;
        private WeatherController weather;
        private FairwayRoute scenarioRoute;
        private ScenarioBathymetry scenarioBathymetry;
        private GorodetsScenarioController scenario;
        private readonly List<MapContact> mapContacts = new List<MapContact>();
        private readonly List<(RadarObstacle Obstacle, RectTransform Rect)> radarObstacles =
            new List<(RadarObstacle, RectTransform)>();
        private readonly List<MapLine> mapLines = new List<MapLine>();
        private readonly List<Vector3> radarTrack = new List<Vector3>();
        private readonly List<HudButton> telegraphButtons = new List<HudButton>();
        private readonly List<HudButton> cameraButtons = new List<HudButton>();
        private readonly List<HudButton> rudderButtons = new List<HudButton>();
        private RectTransform radarWarningRing;
        // One telegraph lever per engine: port first, then starboard.
        private readonly int[] telegraphIndices = { 3, 3 };

        public ShipPhysicsController Ship => ship;
        private Text machineryText;
        private Text tackleText;
        private int selectedAnchor;
        private int selectedStation;
        private MooringPoint selectedShore;
        private Text loadingText;
        private int selectedLoadCompartment;
        private float MaxRudderDeg => ship.Data != null ? ship.Data.rudder.maxAngleDeg : 35f;

        // Effective only before Start, which builds the HUD for the vessel it has then.
        public void SetShip(ShipPhysicsController value) => ship = value;
        private Vector3 objectivePosition = new Vector3(0f, 0f, 650f);
        private const int RadarTrackPointCount = 18;
        private const int RadarPredictionPointCount = 11;
        private float nextTrackSampleTime;

        private sealed class MapContact
        {
            public RectTransform Rect;
            public Vector3 WorldPosition;
            public Transform Target;
        }

        private sealed class MapLine
        {
            public RectTransform Rect;
            public Vector3 WorldStart;
            public Vector3 WorldEnd;
        }

        private static readonly float[] TelegraphValues =
            { -1f, -0.65f, -0.32f, 0f, 0.32f, 0.65f, 1f };

        private static readonly string[] TelegraphNames =
        {
            "FULL ASTERN", "HALF ASTERN", "SLOW ASTERN", "STOP",
            "SLOW AHEAD", "HALF AHEAD", "FULL AHEAD"
        };

        private T InScene<T>() where T : Component
        {
            foreach (var item in FindObjectsByType<T>())
                if (item.gameObject.scene == gameObject.scene) return item;
            return null;
        }

        private void Start()
        {
            if (ship == null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            followCamera = Camera.main != null ? Camera.main.GetComponent<ShipFollowCamera>() : null;
            if (ship.GetComponent<ShipWakeController>() == null)
                ship.gameObject.AddComponent<ShipWakeController>();
            if (ship.GetComponent<NavigationLightRig>() == null)
                ship.gameObject.AddComponent<NavigationLightRig>();
            dayNight = FindAnyObjectByType<DayNightController>();
            simulationTime = GetComponent<SimulationTimeController>();
            if (simulationTime == null)
                simulationTime = gameObject.AddComponent<SimulationTimeController>();
            weather = FindAnyObjectByType<WeatherController>();
            if (weather == null)
                weather = gameObject.AddComponent<WeatherController>();
            scenarioRoute = InScene<FairwayRoute>();
            scenarioBathymetry = InScene<ScenarioBathymetry>();
            scenario = InScene<GorodetsScenarioController>();
            if (dayNight == null)
                dayNight = gameObject.AddComponent<DayNightController>();
            GameObject navigation = GameObject.Find("Navigation");
            if (navigation != null)
                foreach (Transform marker in navigation.transform)
                    marker.localScale = Vector3.one * 1.45f;
            BuildInterface();
            EnsureEventSystem();
            CreateHorn();
        }

        private void Update() => RefreshTelemetry(false);

        private void RefreshLanguage() => RefreshTelemetry(true);
        private void OnEnable() => Localization.Changed += RefreshLanguage;
        private void OnDisable() => Localization.Changed -= RefreshLanguage;

        private void RefreshTelemetry(bool languageChange)
        {
            if (VoyageMenu.IsOpen && !languageChange) return;
            if (ship == null || ship.Body == null) return;
            if (speedText == null || headingTapeText == null || objectiveText == null)
            {
                font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                BuildInterface();
                return;
            }
            if (radarChannel == null ||
                radarTrackSegments == null ||
                radarTrackSegments.Length != RadarTrackPointCount - 1 ||
                radarPredictionSegments == null ||
                radarPredictionSegments.Length != RadarPredictionPointCount - 1)
            {
                BuildInterface();
                return;
            }
            if (!languageChange && !GameplayInput.KeyboardBlocked) UpdateKeyboardShortcuts();
            if (loadingText != null && ship.HasLoading)
            {
                var c = ship.Data.loading.compartments[selectedLoadCompartment];
                loadingText.text = string.Format(Localization.Culture, "{0}\n{1:F1} / {2:F0} t", Localization.Source(c.id).ToUpper(Localization.Culture), ship.CompartmentMassKg(selectedLoadCompartment) / 1000, c.capacityKg / 1000) +
                    Localization.Get("text.total.0.f0.t.draft.1.f2.m.trim.2.0.00.0.00.m.3.ddb53b5d", ship.CurrentMassKg / 1000, ship.EstimatedDraftM, ship.EstimatedTrimM, Localization.Source(ship.LoadingStatus)) +
                    (ship.HydrostaticRange == HydrostaticRange.Supported ? "" : Localization.Get("text.outside.tables.00bd8182"));
            }
            if (machineryText != null && ship.Mechanisms != null)
            {
                var machinery = ship.Mechanisms;
                float left = machinery.HatchSecondsRemaining;
                machineryText.text = Localization.Get("text.deck.machinery.hatches.aa3bd14f") + Localization.Source(machinery.Target.ToString()) + " | " +
                    (machinery.HatchesMoving ? string.Format(Localization.Culture, "{0} {1}:{2:00}", Localization.Source(machinery.Phase.ToString()), Mathf.FloorToInt(left / 60f), Mathf.FloorToInt(left % 60f)) : Localization.Get("text.ready.c2e3ac47"));
            }
            if (tackleText != null && ship.GroundTackle != null)
            {
                tackleText.text = FormatGroundTackle(ship.GroundTackle);
            }

            float speedMps = ship.Body.linearVelocity.magnitude;
            float heading = ship.transform.eulerAngles.y;
            float channelDepth = scenarioBathymetry != null
                ? scenarioBathymetry.Sample(ship.transform.position).DepthM
                : FairwayModel.DepthAt(ship.transform.position);
            float underKeel = channelDepth - ship.EffectiveDraftM;
            Vector3 localVelocity = ship.transform.InverseTransformDirection(ship.Body.linearVelocity);
            Vector3 current = ship.EffectiveCurrentMps;

            float driftAngle = Mathf.Atan2(localVelocity.x,
                Mathf.Max(Mathf.Abs(localVelocity.z), 0.05f)) * Mathf.Rad2Deg;
            string support = FormatSupportStatus(ship.Data != null ? ship.Data.support : null,
                Vector3.Dot(ship.RelativeWaterVelocity, ship.transform.forward));
            speedText.text =
                Localization.Get("text.speed.578a37de") +
                string.Format(Localization.Culture, "<size=30><b>{0:F1}</b></size><size=15> kn</size>\n", speedMps * 1.943844f) +
                string.Format(Localization.Culture, "<size=14><color=#8AA0AD>{0:F1} km/h</color>{1}</size>", speedMps * 3.6f, support);
            headingText.text =
                Localization.Get("text.course.7e96dd8f") +
                string.Format(Localization.Culture, "<size=30><b>{0:000}°</b></size>\n", heading) +
                Localization.Get("text.drift.0.0.0.0.0.0.0.41af5bbc", driftAngle);
            depthText.text =
                Localization.Get("text.depth.d967b71b") +
                string.Format(Localization.Culture, "<size=30><b>{0:F1}</b></size><size=15> m</size>\n", channelDepth) +
                Localization.Get("text.under.keel.0.f1.m.1ead3d0a", underKeel);
            rudderText.text =
                Localization.Get("text.rudder.0.0.0.0.0.0.0.deg.987e7920", ship.RudderAngleDeg) +
                Localization.Get("text.command.0.0.0.0.deg.58ec31a7", ship.RudderCommand * MaxRudderDeg) +
                string.Format(Localization.Culture, "{0}</size>", FormatBowThrusterStatus(ship.HasBowThruster, ship.BowThrusterOutput));
            engineText.text = FormatEngineStatus();
            currentText.text =
                Localization.Get("text.current.53d2ded5") +
                string.Format(Localization.Culture, "<size=28><b>{0:F1}</b></size><size=15> m/s {1}</size>\n", current.magnitude, CurrentArrow(current)) +
                string.Format(Localization.Culture, "<size=14><color=#8AA0AD>{0} {1:F0}%</color></size>", (ship.HasLoading ? Localization.Get("text.deadweight.86491b04") : Localization.Get("text.cargo.3322532d")), ship.LoadFraction * 100f);
            cameraText.text = followCamera == null
                ? Localization.Get("text.camera.582ff0d0")
                : FormatCameraStatus(
                    followCamera.ViewName,
                    followCamera.ViewIndex,
                    followCamera.ViewCount);
            headingTapeText.text = BuildHeadingTape(heading);
            if (scenario != null) objectivePosition = scenario.NavigationTarget;
            float distance = Vector3.Distance(
                new Vector3(ship.transform.position.x, 0f, ship.transform.position.z),
                objectivePosition);
            objectiveText.text = scenario != null
                ? Localization.Get("text.objective.51082de9") +
                  string.Format(Localization.Culture, "<size=23><b>{0}</b></size>\n", scenario.PhaseLabel) +
                  string.Format(Localization.Culture, "<size=15>{0}</size>\n\n", scenario.Instruction) +
                  Localization.Get("text.score.0.f0.100.3b296d0f", scenario.Score) +
                  Localization.Get("text.limit.0.f0.km.h.216c3448", scenario.LocalSpeedLimitMps * 3.6f)
                : FormatObjectiveStatus(distance,
                    ship.Data != null ? ship.Data.controlLimits.maxLoadedSpeedMps : 0f);
            depthText.color = underKeel < 0.8f
                ? HudTheme.Danger
                : underKeel < 2f ? HudTheme.Warning : HudTheme.TextPrimary;

            if (rudderNeedle != null)
                rudderNeedle.anchoredPosition = new Vector2(
                    Mathf.Clamp(ship.RudderAngleDeg / MaxRudderDeg, -1f, 1f) * 145f, -4f);
            if (rudderCommandNeedle != null)
                rudderCommandNeedle.anchoredPosition = new Vector2(
                    Mathf.Clamp(ship.RudderCommand, -1f, 1f) * 145f, 9f);
            UpdateActiveStates();
            UpdateMiniMap();
            UpdateDepthRadar(channelDepth);
            UpdateWarnings(speedMps, underKeel, current.magnitude);
            UpdateTimeScaleText();
            if (timeOfDayText != null && dayNight != null) timeOfDayText.text = dayNight.TimeLabel;
            if (weatherText != null && weather != null)
                weatherText.text = weather.StatusText;
        }

        private void BuildInterface()
        {
            telegraphButtons.Clear(); rudderButtons.Clear(); cameraButtons.Clear();
            // Instruments only. Other components park their own rigs on this object, because the HUD
            // creates the weather controller when a scene has none, and that adds the lighting, which
            // parents a sky capture camera here. Destroying those leaves them throwing for the rest of
            // the session with no sky reflection.
            foreach (Transform child in transform)
                if (child is RectTransform) Destroy(child.gameObject);

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            RectTransform instruments = Panel("TopStatusBar",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(1320f, 98f));
            speedText = CompactInstrument(instruments, 0, 205f, HudIcon.Speed);
            headingText = CompactInstrument(instruments, 1, 285f, HudIcon.Compass);
            depthText = CompactInstrument(instruments, 2, 255f, HudIcon.Depth);
            currentText = CompactInstrument(instruments, 3, 270f, HudIcon.Current);

            RectTransform tape = Panel("HeadingTape",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(700f, 54f));
            headingTapeText = Label(tape, string.Empty, 20, TextAnchor.UpperCenter,
                new Vector2(8f, 16f), new Vector2(-8f, -3f));
            ImageRect(tape, "CourseMarkerStem", warningColor,
                new Vector2(0f, -15f), new Vector2(4f, 14f));
            ImageRect(tape, "CourseMarkerHead", warningColor,
                new Vector2(0f, -6f), new Vector2(14f, 5f));

            BuildRudderControls(transform as RectTransform);
            BuildTelegraph(transform as RectTransform);
            HudPanelFlow.Configure(engineText, 4, 3);
            foreach (Transform child in engineText.transform.parent)
                if (child.name.StartsWith("Icon_")) child.gameObject.SetActive(false);
            BuildCameraControls(transform as RectTransform);
            BuildTimeControls();
            BuildWeatherControls();
            BuildMachineryControls();
            BuildGroundTackleControls();
            BuildLoadingControls();
            HudPanelFlow.Configure(loadingText, 1, 3);
            HudPanelFlow.Configure(machineryText, 2, 2);
            HudPanelFlow.Configure(tackleText, 4, 3, 4);

            warningPanel = Panel("Warnings",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -186f), new Vector2(640f, 50f));
            warningText = Label(warningPanel, string.Empty, 19, TextAnchor.MiddleCenter,
                new Vector2(12f, 4f), new Vector2(-12f, -4f));
            warningPanel.gameObject.SetActive(false);

            RectTransform objective = Panel("Objective",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(22f, -124f), new Vector2(440f, 172f));
            objectiveText = Label(objective, string.Empty, 18, TextAnchor.UpperLeft,
                new Vector2(20f, 14f), new Vector2(-20f, -14f));
            objectiveText.supportRichText = true;
            objectiveText.lineSpacing = 1.12f;

            BuildMiniMap();
            helpText = Label(transform, string.Empty, 18, TextAnchor.MiddleCenter,
                new Vector2(320f, 12f), new Vector2(-320f, -1016f));
            LocalizedLabel.Bind(helpText, () => (FormatEngineControls(ship.EngineCount) + Localization.Get("text.space.stop.1.9.cameras.2cb2e691") +
                Localization.Get("text.j.l.bow.thruster.k.off.h.horn.m.map.n.day.night.t.time.f.d83a23c0") +
                (ship.GroundTackle != null ? Localization.Get("text.g.anchor.b.let.go.veer.y.hold.u.heave.o.station.p.bollar.38b7d802") : "")).Replace("   ", "\n"));
            helpText.gameObject.SetActive(false);
            Text helpPrompt = Label(transform, "F1  CONTROLS", 16, TextAnchor.LowerCenter,
                new Vector2(820f, 16f), new Vector2(-820f, -1034f));
            helpPrompt.color = accentColor;
            helpPrompt.gameObject.SetActive(false);
            var layout = GetComponent<HudLayout>();
            if (layout == null) layout = gameObject.AddComponent<HudLayout>();
            layout.Build(helpText);
            foreach (Text dynamicLabel in new[] { speedText, headingText, depthText, rudderText,
                engineText, currentText, warningText, cameraText, headingTapeText, objectiveText,
                depthRadarText, timeOfDayText, timeScaleText, weatherText, machineryText, tackleText, loadingText })
                LocalizedLabel.Clear(dynamicLabel);
            if (readout != null) readout.gameObject.SetActive(false);
        }

        private void BuildRudderControls(RectTransform parent)
        {
            RectTransform block = AnchoredPanel("RudderBlock", new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 24f),
                new Vector2(420f, 180f));
            Icon(block, HudIcon.Rudder, new Vector2(-185f, 76f), 20f, HudTheme.Accent);
            rudderText = Label(block, "RUDDER  0.0 deg", 19, TextAnchor.UpperCenter,
                new Vector2(8f, 108f), new Vector2(-8f, -8f));
            RectTransform dial = ImageRect(block, "RudderScale", new Color(0.03f, 0.07f, 0.10f, 0.95f),
                new Vector2(0f, -4f), new Vector2(350f, 38f));
            Image dialImage = dial.GetComponent<Image>();
            dialImage.sprite = HudTheme.Rounded(10);
            dialImage.type = Image.Type.Sliced;
            // Scale tick marks: centre (0) emphasised, symmetrical port/stbd graduations.
            for (int t = -3; t <= 3; t++)
            {
                bool centre = t == 0;
                RectTransform tick = ImageRect(dial, string.Format(Localization.Culture, "Tick{0}", t),
                    centre ? HudTheme.AccentSoft : new Color(0.4f, 0.6f, 0.72f, 0.5f),
                    new Vector2(t * 48f, 0f), new Vector2(centre ? 2.4f : 1.6f, centre ? 26f : 16f));
                tick.GetComponent<Image>().raycastTarget = false;
            }
            Label(dial, "P35    20    10    0    10    20    S35",
                13, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero).color = HudTheme.TextSecondary;
            rudderCommandNeedle = ImageRect(dial, "CommandNeedle", HudTheme.Accent,
                new Vector2(0f, 11f), new Vector2(12f, 8f));
            Image commandImage = rudderCommandNeedle.GetComponent<Image>();
            commandImage.sprite = HudTheme.Disc();
            commandImage.raycastTarget = false;
            rudderNeedle = ImageRect(dial, "Needle", new Color(1f, 0.82f, 0.22f),
                new Vector2(0f, -4f), new Vector2(5f, 34f));
            Image needleImage = rudderNeedle.GetComponent<Image>();
            needleImage.sprite = HudTheme.Rounded(2);
            needleImage.type = Image.Type.Sliced;
            needleImage.raycastTarget = false;
            rudderButtons.Add(Button(block, "< PORT  [A]", new Vector2(-118f, -60f),
                new Vector2(108f, 38f), () => ship.SetRudderCommand(-1f)));
            rudderButtons.Add(Button(block, "MIDSHIPS [C]", new Vector2(0f, -60f),
                new Vector2(122f, 38f), ship.CenterRudder));
            rudderButtons.Add(Button(block, "STBD [D] >", new Vector2(118f, -60f),
                new Vector2(108f, 38f), () => ship.SetRudderCommand(1f)));
        }

        private void BuildTelegraph(RectTransform parent)
        {
            RectTransform block = AnchoredPanel("TelegraphBlock", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f),
                new Vector2(680f, 154f));
            Icon(block, HudIcon.Speed, new Vector2(-310f, 50f), 22f, HudTheme.Accent);
            engineText = Label(block, "TELEGRAPH  STOP", 19, TextAnchor.UpperCenter,
                new Vector2(8f, 80f), new Vector2(-8f, -8f));
            string[] labels =
            {
                "FULL\nASTERN", "HALF\nASTERN", "SLOW\nASTERN", "STOP",
                "SLOW\nAHEAD", "HALF\nAHEAD", "FULL\nAHEAD"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                int command = i;
                HudButton button = Button(block, labels[i], new Vector2(-282f + i * 94f, -37f),
                    new Vector2(92f, 48f), () => SetTelegraph(command));
                telegraphButtons.Add(button);
            }
        }

        private void BuildCameraControls(RectTransform parent)
        {
            RectTransform block = AnchoredPanel("CameraBlock", new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 28f),
                new Vector2(350f, 128f));
            Icon(block, HudIcon.Camera, new Vector2(-150f, 50f), 22f, HudTheme.Accent);
            cameraText = Label(block, "CAMERA", 18, TextAnchor.UpperCenter,
                new Vector2(8f, 70f), new Vector2(-8f, -8f));
            cameraButtons.Add(Button(block, "PREV", new Vector2(-112f, -38f),
                new Vector2(104f, 38f), PreviousCamera));
            cameraButtons.Add(Button(block, "NEXT  [V]", new Vector2(0f, -38f),
                new Vector2(104f, 38f), NextCamera));
            Button(block, "DOCK  [8]", new Vector2(112f, -38f),
                new Vector2(104f, 38f), () => SetCamera(7));
            Button(block, "NAV  [9]", new Vector2(112f, 5f),
                new Vector2(104f, 34f), () => SetCamera(8));
            Button(block, "DAY [N]", new Vector2(-112f, 5f),
                new Vector2(104f, 34f), ToggleDayNight);
            timeOfDayText = Label(block, "DAY", 14, TextAnchor.MiddleCenter,
                new Vector2(135f, 52f), new Vector2(-135f, -42f));
            timeOfDayText.color = accentColor;
        }

        private void BuildTimeControls()
        {
            RectTransform block = AnchoredPanel("TimeScaleBlock",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-22f, 170f), new Vector2(350f, 58f));
            Icon(block, HudIcon.Time, new Vector2(-150f, 0f), 20f, HudTheme.Accent);
            timeScaleText = Label(block, "TIME 1x", 16, TextAnchor.MiddleLeft,
                new Vector2(46f, 8f), new Vector2(-230f, -8f));
            timeScaleText.color = accentColor;
            Button(block, "1x", new Vector2(-30f, 0f), new Vector2(58f, 34f),
                () => SetTimeScale(1f));
            Button(block, "2x", new Vector2(40f, 0f), new Vector2(58f, 34f),
                () => SetTimeScale(2f));
            Button(block, "4x", new Vector2(110f, 0f), new Vector2(58f, 34f),
                () => SetTimeScale(4f));
        }

        private void BuildMachineryControls()
        {
            machineryText = null;
            if (ship.Mechanisms == null || !ship.Mechanisms.IsConfigured) return;
            RectTransform block = AnchoredPanel("MachineryBlock", Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(20, 326), new Vector2(330, 174));
            machineryText = Label(block, "DECK MACHINERY", 14, TextAnchor.UpperCenter,
                new Vector2(8, 76), new Vector2(-8, -8));
            Button(block, "CLOSE", new Vector2(-80, -4), new Vector2(145, 30),
                () => ship.SetHatchPosition(HatchPosition.Closed));
            Button(block, "HALF OPEN", new Vector2(80, -4), new Vector2(145, 30),
                () => ship.SetHatchPosition(HatchPosition.HalfOpen));
            Button(block, "FORE OPEN", new Vector2(-80, -39), new Vector2(145, 30),
                () => ship.SetHatchPosition(HatchPosition.ForwardOpen));
            Button(block, "AFT OPEN", new Vector2(80, -39), new Vector2(145, 30),
                () => ship.SetHatchPosition(HatchPosition.AftOpen));
        }

        private void BuildGroundTackleControls()
        {
            tackleText = null;
            GroundTackle tackle = ship.GroundTackle;
            if (tackle == null) return;
            selectedAnchor = Mathf.Clamp(selectedAnchor, 0, Mathf.Max(0, tackle.AnchorCount - 1));
            selectedStation = Mathf.Clamp(selectedStation, 0, Mathf.Max(0, tackle.StationCount - 1));
            RectTransform block = AnchoredPanel("GroundTackleBlock", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(-22f, 240f), new Vector2(440f, 384f));
            // Keys: G B Y U for the selected anchor; O P I , / . F for the selected mooring station (F1 lists them).
            tackleText = Label(block, "ANCHORS", 16, TextAnchor.UpperLeft, new Vector2(14, 118), new Vector2(-10, -8));
            tackleText.supportRichText = true;
            Button(block, "ANCHOR", new Vector2(-138, -78), new Vector2(92, 32), CycleAnchor);
            Button(block, "LET GO", new Vector2(-46, -78), new Vector2(92, 32), () => ChainOrder(ChainCommand.LetGo));
            Button(block, "HOLD", new Vector2(46, -78), new Vector2(92, 32), () => ChainOrder(ChainCommand.Hold));
            Button(block, "HEAVE", new Vector2(138, -78), new Vector2(92, 32), () => ChainOrder(ChainCommand.Heave));
            Button(block, "STATION", new Vector2(-110, -116), new Vector2(102, 28), CycleStation);
            Button(block, "BOLLARD", new Vector2(0, -116), new Vector2(102, 28), CycleShore);
            Button(block, "PASS LINE", new Vector2(110, -116), new Vector2(102, 28), PassLine);
            Button(block, "HAUL", new Vector2(-126, -154), new Vector2(64, 28), () => LineOrder(LineCommand.Haul));
            Button(block, "HOLD", new Vector2(-56, -154), new Vector2(64, 28), () => LineOrder(LineCommand.Hold));
            Button(block, "SLACK", new Vector2(14, -154), new Vector2(64, 28), () => LineOrder(LineCommand.Slack));
            Button(block, "CAST OFF", new Vector2(110, -154), new Vector2(116, 28),
                () => ship.GroundTackle?.CastOff(selectedStation));
            foreach (HudButton button in block.GetComponentsInChildren<HudButton>())
            {
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.anchoredPosition += Vector2.up * 192;
            }
        }

        private void CycleAnchor()
        {
            GroundTackle tackle = ship.GroundTackle;
            if (tackle != null && tackle.AnchorCount > 0) selectedAnchor = (selectedAnchor + 1) % tackle.AnchorCount;
        }

        private void ChainOrder(ChainCommand command) => ship.GroundTackle?.SetChainCommand(selectedAnchor, command);

        private void CycleStation()
        {
            GroundTackle tackle = ship.GroundTackle;
            if (tackle == null || tackle.StationCount == 0) return;
            selectedStation = (selectedStation + 1) % tackle.StationCount;
            selectedShore = null;
        }

        // Shore bollards nearest the selected station first; the selection steps outwards and wraps round.
        private void CycleShore()
        {
            GroundTackle tackle = ship.GroundTackle;
            if (tackle == null) return;
            IReadOnlyList<MooringPoint> near = tackle.ShorePointsNear(selectedStation);
            if (near.Count == 0) { selectedShore = null; return; }
            int index = -1;
            for (int i = 0; i < near.Count; i++) if (near[i] == selectedShore) index = i;
            selectedShore = near[(index + 1) % Mathf.Min(near.Count, 6)];
        }

        private void PassLine()
        {
            GroundTackle tackle = ship.GroundTackle;
            if (tackle == null) return;
            if (selectedShore == null) CycleShore();
            tackle.PassLine(selectedStation, selectedShore);
        }

        private void LineOrder(LineCommand command) => ship.GroundTackle?.SetLineCommand(selectedStation, command);

        // Anchors with their state and chain out, then the selected mooring station and every line made fast.
        public string FormatGroundTackle(GroundTackle tackle)
        {
            var text = new System.Text.StringBuilder(Localization.Get("text.anchors.91d9258b"));
            for (int i = 0; i < tackle.AnchorCount; i++)
            {
                string mark = i == selectedAnchor ? "<color=#F2C14E>>" : " ";
                string status = tackle.AnchorStatus(i);
                string colour = status == "DRAGGING" || status == "CHAIN PARTED" || status == "WINDLASS STALLED" ? "#FF6B5B" :
                    status == "HOLDING" ? "#7BD88F" : "#D8E2E8";
                text.Append(string.Format(Localization.Culture, "{0} {1,-6}{2} <color={3}>{4}</color>", mark, Localization.Source(tackle.AnchorLabel(i)), (i == selectedAnchor ? "</color>" : ""), colour, Localization.Source(status)) +
                    string.Format(Localization.Culture, "  {0:0} m\n", tackle.ChainOutM(i)));
            }
            text.Append(Localization.Get("text.mooring.2759d016"));
            if (tackle.StationCount > 0)
            {
                MooringPoint station = tackle.Station(selectedStation);
                text.Append(Localization.Get("text.station.0.09f17174", station.Label));
                if (selectedShore != null)
                {
                    Vector3 gap = selectedShore.LeadWorld - station.LeadWorld;
                    float reach = new Vector2(gap.x, gap.z).magnitude;
                    text.Append(Localization.Get("text.to.0.1.0.m.24278e3a", selectedShore.Label, reach) +
                        (reach > tackle.Spec.passReachM ? Localization.Get("text.out.of.reach.830e5952") : ""));
                }
                text.Append('\n');
            }
            for (int k = 0; k < tackle.LineCount; k++)
            {
                LineState state = tackle.LineStateAt(k);
                string colour = state == LineState.Overloaded ? "#FF6B5B" : state == LineState.Taut ? "#7BD88F" : "#D8E2E8";
                float load = tackle.LineTensionN(k) / tackle.LineBreakingLoadN;
                LineCommand command = tackle.LineCommandAt(k);
                text.Append(string.Format(Localization.Culture, "{0} > {1} <color={2}>{3}</color>", tackle.LineShip(k).Label, tackle.LineShore(k).Label, colour, Localization.Get("line.state." + state.ToString().ToLowerInvariant())) +
                    string.Format(Localization.Culture, " {0:0}%{1}\n", load * 100, (command == LineCommand.Hold ? "" : " " + (command == LineCommand.Haul ? Localization.Get("text.hauling.d89d72c5") : Localization.Get("text.slacking.9f19976f")))));
            }
            if (Time.unscaledTime - tackle.NoticeTime < 8f) text.Append(string.Format(Localization.Culture, "<color=#F2C14E>{0}</color>", tackle.Notice));
            return text.ToString();
        }

        private void BuildLoadingControls()
        {
            loadingText = null;
            if (!ship.HasLoading) return;
            selectedLoadCompartment = 0;
            RectTransform block = AnchoredPanel("LoadingBlock", Vector2.zero, Vector2.zero, Vector2.zero,
                new Vector2(20, 512), new Vector2(330, 168));
            loadingText = Label(block, "LOADING", 16, TextAnchor.UpperCenter,
                new Vector2(8, 76), new Vector2(-8, -8));
            Button(block, "SELECT COMPARTMENT", new Vector2(0, -28), new Vector2(305, 28),
                () => selectedLoadCompartment = (selectedLoadCompartment + 1) % ship.Data.loading.compartments.Length);
            Button(block, "FILL", new Vector2(-105, -63), new Vector2(95, 28),
                () => ship.SetLoadingCommand(selectedLoadCompartment, 1));
            Button(block, "EMPTY", new Vector2(0, -63), new Vector2(95, 28),
                () => ship.SetLoadingCommand(selectedLoadCompartment, -1));
            Button(block, "STOP ALL", new Vector2(105, -63), new Vector2(95, 28), ship.StopLoading);
            foreach (HudButton button in block.GetComponentsInChildren<HudButton>())
            {
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.anchoredPosition += Vector2.up * 84;
            }
        }

        private void BuildWeatherControls()
        {
            RectTransform block = AnchoredPanel("WeatherBlock",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 182f), new Vector2(330f, 174f));
            Icon(block, HudIcon.Wind, new Vector2(-142f, 44f), 18f, HudTheme.Accent);
            weatherText = Label(block, "WEATHER", 16, TextAnchor.UpperCenter,
                new Vector2(26f, 76f), new Vector2(-10f, -6f));
            weatherText.color = HudTheme.AccentSoft;
            Button(block, "DIR -", new Vector2(-112f, -26f), new Vector2(92f, 32f),
                () => ChangeWindDirection(-45f));
            Button(block, "DIR +", new Vector2(-8f, -26f), new Vector2(92f, 32f),
                () => ChangeWindDirection(45f));
            Button(block, "WIND [F2]", new Vector2(100f, -26f), new Vector2(104f, 32f),
                CycleWind);
            Button(block, "RAIN [F4]", new Vector2(-58f, -64f), new Vector2(112f, 32f),
                CycleRain);
            Button(block, "FOG [F5]", new Vector2(64f, -64f), new Vector2(112f, 32f),
                CycleFog);
        }

        private void BuildMiniMap()
        {
            radarObstacles.Clear();
            mapContacts.Clear();
            mapLines.Clear();
            radarTrack.Clear();
            miniMap = Panel("MiniMap",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(420f, 464f));

            mapButtons.Clear();
            mapTitle = Label(miniMap, string.Empty, 17, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero);
            mapTitle.color = HudTheme.AccentSoft;
            AddMapButton("map.zoomIn", () => ZoomMap(1));
            AddMapButton("map.zoomOut", () => ZoomMap(-1));
            AddMapButton("map.expand", () => { VoyageSettings.SetMapExpanded(!VoyageSettings.MapExpanded); RecenterMap(); });
            AddMapButton("map.orientation", () => VoyageSettings.SetMapNorthUp(!VoyageSettings.MapNorthUp));
            AddMapButton("map.follow", RecenterMap);
            AddMapButton("map.legend", () => VoyageSettings.SetMapLegend(!VoyageSettings.MapLegend));
            RectTransform viewport = ImageRect(miniMap, "Viewport",
                new Color(0.035f, 0.058f, 0.094f, 0.98f), // calm dark navy
                new Vector2(0f, 36f), new Vector2(384f, 300f));
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = HudTheme.Rounded(12);
            viewportImage.type = Image.Type.Sliced;
            viewportImage.raycastTarget = true;
            mapViewport = viewport;
            viewport.gameObject.AddComponent<MapInteraction>().Initialise(this);
            viewport.gameObject.AddComponent<RectMask2D>();
            mapWorld = ImageRect(viewport, "MovingWorld", Color.clear, Vector2.zero,
                new Vector2(382f, 332f));

            // Filled navigable-channel ribbon (vertex-coloured mesh) is the first and
            // lowest layer, so route lines, buoys and the ship draw on top of it.
            GameObject channelObject = new GameObject("ChannelFill",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(RadarChannel));
            channelObject.transform.SetParent(mapWorld, false);
            radarChannel = channelObject.GetComponent<RadarChannel>();
            radarChannel.raycastTarget = false;
            RectTransform channelRect = radarChannel.rectTransform;
            channelRect.anchorMin = Vector2.zero;
            channelRect.anchorMax = Vector2.one;
            channelRect.offsetMin = Vector2.zero;
            channelRect.offsetMax = Vector2.zero;

            float length = scenarioRoute != null ? scenarioRoute.LengthM : 1100;
            int sections = Mathf.Clamp(Mathf.CeilToInt(length / 20), 2, 256);
            for (int i = 0; i < sections; i++)
            {
                float start = length * i / sections, end = length * (i + 1) / sections;
                AddMapLine("FairwayRoute" + i, new Color(.85f, .68f, .32f, .55f), ChartPoint(start, 0), ChartPoint(end, 0), 2);
                AddMapLine("PortEdge" + i, new Color(.75f, .88f, .8f, .75f), ChartPoint(start, -1), ChartPoint(end, -1), 2);
                AddMapLine("StarboardEdge" + i, new Color(.84f, .28f, .22f, .75f), ChartPoint(start, 1), ChartPoint(end, 1), 2);
            }
            // Builders use stable buoy root names, including the zaton's unlit marks.
            foreach (Transform mark in FindObjectsByType<Transform>())
            {
                if (mark.gameObject.scene != gameObject.scene || !mark.name.Contains("Buoy") ||
                    (mark.parent != null && mark.parent.name.Contains("Buoy")) || mark.GetComponentInChildren<Renderer>() == null) continue;
                Color color = mark.name.Contains("Red") || mark.name.Contains("Right") ? new Color(.92f, .30f, .24f) : new Color(.75f, .88f, .8f);
                AddMapContact(mark.name, color, mark.position, new Vector2(12, 12));
                mapContacts[mapContacts.Count - 1].Target = mark;
            }
            mapChart = Resources.Load<MapChartData>("Charts/" + gameObject.scene.name);
            mapShoresBuilt = false; mapShoreProfile = null;
            AddReadyMapShores();
            foreach (RadarObstacle obstacle in FindObjectsByType<RadarObstacle>())
            {
                if (obstacle.gameObject.scene != gameObject.scene) continue;
                RectTransform footprint = ImageRect(mapWorld, "Obstacle " + obstacle.name,
                    new Color(0.88f, 0.76f, 0.95f), Vector2.zero, Vector2.one);
                footprint.GetComponent<Image>().raycastTarget = false;
                radarObstacles.Add((obstacle, footprint));
            }

            mapWaypoint = AddMapContact("Waypoint", warningColor,
                objectivePosition, new Vector2(22f, 22f));
            RectTransform waypointRing = ImageRect(mapWaypoint, "WaypointRing",
                warningColor, Vector2.zero, new Vector2(34f, 34f));
            Image waypointRingImage = waypointRing.GetComponent<Image>();
            waypointRingImage.sprite = HudTheme.Outline(14);
            waypointRingImage.type = Image.Type.Sliced;
            waypointRingImage.raycastTarget = false;

            mapVessel = ImageRect(viewport, "Vessel", HudTheme.Accent,
                Vector2.zero, new Vector2(40f, 40f));
            Image vesselGlow = mapVessel.GetComponent<Image>();
            vesselGlow.sprite = HudTheme.Soft(10);
            vesselGlow.color = new Color(0.26f, 0.78f, 0.95f, 0.30f);
            vesselGlow.raycastTarget = false;
            RectTransform vesselIcon = ImageRect(mapVessel, "Hull",
                new Color(1f, 0.82f, 0.22f), Vector2.zero, new Vector2(22f, 30f));
            Image vesselIconImage = vesselIcon.GetComponent<Image>();
            vesselIconImage.sprite = HudTheme.Triangle();
            vesselIconImage.raycastTarget = false;

            // Concentric range rings (100 m / 200 m) centred on the ship.
            RangeRing(viewport, "Range100m", 164f, new Color(0.34f, 0.62f, 0.78f, 0.22f));
            RangeRing(viewport, "Range200m", 320f, new Color(0.34f, 0.62f, 0.78f, 0.16f));

            // Clean thin cyan heading line straight up from the ship (head-up).
            ImageRect(viewport, "HeadingLine", new Color(0.30f, 0.82f, 0.96f, 0.55f),
                new Vector2(0f, 116f), new Vector2(2f, 232f))
                .GetComponent<Image>().raycastTarget = false;

            radarWarningRing = ImageRect(viewport, "WarningRing", HudTheme.Danger,
                Vector2.zero, new Vector2(372f, 322f));
            Image warningRingImage = radarWarningRing.GetComponent<Image>();
            warningRingImage.sprite = HudTheme.Outline(12);
            warningRingImage.type = Image.Type.Sliced;
            warningRingImage.raycastTarget = false;
            radarWarningRing.gameObject.SetActive(false);

            radarTrackSegments = BuildRadarSegments(viewport, "Track",
                RadarTrackPointCount - 1, new Color(0.74f, 0.78f, 0.82f, 0.5f), 2f);
            radarPredictionSegments = BuildRadarSegments(viewport, "Prediction",
                RadarPredictionPointCount - 1, new Color(0.30f, 0.82f, 0.96f, 0.9f), 2.4f);

            depthRadarText = Label(miniMap, string.Empty, 15, TextAnchor.LowerLeft,
                new Vector2(16f, 12f), new Vector2(-16f, -348f));
            depthRadarText.supportRichText = true;
            depthRadarText.lineSpacing = 1.05f;
            RecenterMap();
            mapRestoreCount = ship.RestoreCount;
        }

        private void AddReadyMapShores()
        {
            if (mapShoresBuilt) return;
            if (mapChart == null)
            {
                // ShipWakeController may create its bank profile after the HUD's Start.
                if (mapShoreProfile == null) mapShoreProfile = ship.GetComponent<RiverShoreProfile>();
                if (mapShoreProfile == null || !mapShoreProfile.IsReady) return;
            }
            var shores = new List<Vector3>();
            if (mapChart != null) shores.AddRange(mapChart.shoreSegments);
            else mapShoreProfile.CopyChartEdges(shores);
            for (int i = 0; i + 1 < shores.Count; i += 2)
            {
                AddMapLine("Shore" + i, new Color(.72f, .72f, .55f, .8f), shores[i], shores[i + 1], 2);
                mapLines[mapLines.Count - 1].Rect.SetSiblingIndex(1);
            }
            mapShoresBuilt = true;
        }

        private void AddMapButton(string key, UnityEngine.Events.UnityAction action)
        {
            var rect = UiLayout.Rect(miniMap, key, 0, 0, 120, 30);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = HudTheme.Rounded(6); image.type = Image.Type.Sliced;
            image.color = HudTheme.ButtonIdle;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var text = Label(rect, string.Empty, 15, TextAnchor.MiddleCenter, new Vector2(2, 2), new Vector2(-2, -2));
            text.raycastTarget = false;
            mapButtons.Add((button, text, key));
        }

        private Vector3 ChartPoint(float distance, float side)
        {
            if (scenarioRoute == null) return FairwayPoint(distance - 220, side);
            var query = scenarioRoute.QueryDistance(distance);
            return query.Position + query.Right * (side < 0 ? -query.Sample.leftWidthM : side * query.Sample.rightWidthM);
        }

        private void UpdateMiniMap()
        {
            if (mapVessel == null || mapWorld == null) return;
            AddReadyMapShores();
            if (mapRestoreCount != ship.RestoreCount) { radarTrack.Clear(); RecenterMap(); mapRestoreCount = ship.RestoreCount; }
            float width = miniMap.rect.width, height = miniMap.rect.height;
            UiLayout.Place(mapTitle.rectTransform, 16, 6, width - 32, 26);
            float buttonWidth = (width - 40) / 3;
            for (int i = 0; i < mapButtons.Count; i++)
            {
                var item = mapButtons[i];
                UiLayout.Place((RectTransform)item.Button.transform, 16 + i % 3 * (buttonWidth + 4), 36 + i / 3 * 34, buttonWidth, 30);
                string key = item.Key;
                if (key == "map.expand" && VoyageSettings.MapExpanded) key = "map.collapse";
                if (key == "map.orientation") key = VoyageSettings.MapNorthUp ? "map.northUp" : "map.headingUp";
                item.Text.text = Localization.Get(key);
                item.Button.interactable = item.Key != "map.zoomIn" || VoyageSettings.MapRange > 100;
                if (item.Key == "map.zoomOut") item.Button.interactable = VoyageSettings.MapRange < 4000;
                item.Button.GetComponent<Image>().color = item.Key == "map.follow" && mapFollowing || item.Key == "map.legend" && VoyageSettings.MapLegend
                    ? HudTheme.ButtonActive : HudTheme.ButtonIdle;
            }
            float footer = VoyageSettings.MapLegend ? 148 : 64;
            // Keep the chart origin at its centre; every chart layer shares this projection.
            mapViewport.anchorMin = mapViewport.anchorMax = mapViewport.pivot = new Vector2(.5f, .5f);
            mapViewport.sizeDelta = new Vector2(width - 32, Mathf.Max(100, height - 108 - footer));
            mapViewport.anchoredPosition = new Vector2(0, (footer - 108) * .5f);
            UiLayout.Place(depthRadarText.rectTransform, 16, height - footer + 4, width - 32, footer - 8);
            mapWorld.sizeDelta = mapViewport.sizeDelta;
            float heading = VoyageSettings.MapNorthUp ? (mapChart != null ? mapChart.northHeading : 0) : ship.transform.eulerAngles.y;
            if (mapFollowing) mapCenter = ship.transform.position;
            Vector2 offset = mapFollowing && !VoyageSettings.MapNorthUp ? new Vector2(0, -mapViewport.rect.height * .18f) : Vector2.zero;
            float scale = Mathf.Min(mapViewport.rect.width, mapViewport.rect.height) * .5f / VoyageSettings.MapRange;
            mapProjection = new MapProjection(mapCenter, heading, scale, offset);
            mapTitle.text = Localization.Get("map.title", VoyageSettings.MapRange);
            foreach (var contact in mapContacts)
            {
                if (contact.Target != null) contact.WorldPosition = contact.Target.position;
                Vector2 point = mapProjection.Project(contact.WorldPosition);
                bool waypoint = contact.Rect == mapWaypoint;
                if (waypoint) point = mapProjection.Project(objectivePosition);
                contact.Rect.anchoredPosition = waypoint ? ClampMapPoint(point, 18) : point;
                contact.Rect.gameObject.SetActive(waypoint || mapViewport.rect.Contains(point));
            }
            foreach (var line in mapLines)
                SetRadarLine(line.Rect, mapProjection.Project(line.WorldStart), mapProjection.Project(line.WorldEnd));
            foreach (var contact in radarObstacles)
            {
                bool visible = contact.Obstacle != null && contact.Obstacle.isActiveAndEnabled;
                contact.Rect.gameObject.SetActive(visible);
                if (!visible) continue;
                contact.Obstacle.Project(mapProjection.Center, heading, scale, offset.y, out var position, out var size, out var angle);
                contact.Rect.anchoredPosition = position;
                contact.Rect.sizeDelta = size;
                contact.Rect.localRotation = Quaternion.Euler(0, 0, angle);
            }
            UpdateRadarChannel();
            UpdateRadarTrack();
            UpdateRadarPrediction();
            Vector2 own = mapProjection.Project(ship.transform.position);
            mapVessel.anchoredPosition = ClampMapPoint(own, 22);
            mapVessel.localRotation = Quaternion.Euler(0, 0, mapProjection.IconAngle(ship.transform.eulerAngles.y));
            mapVessel.SetAsLastSibling();
            for (int i = 1; i <= 2; i++)
            {
                var ring = mapViewport.Find(i == 1 ? "Range100m" : "Range200m") as RectTransform;
                ring.anchoredPosition = own;
                ring.sizeDelta = Vector2.one * (VoyageSettings.MapRange * scale * i);
            }
            var headingLine = mapViewport.Find("HeadingLine") as RectTransform;
            Vector3 ahead = ship.transform.position + Quaternion.Euler(0, ship.transform.eulerAngles.y, 0) * Vector3.forward * VoyageSettings.MapRange;
            SetRadarLine(headingLine, own, mapProjection.Project(ahead));
            radarWarningRing.sizeDelta = mapViewport.sizeDelta - Vector2.one * 6;
        }

        private Vector2 ClampMapPoint(Vector2 point, float margin)
        {
            Vector2 half = mapViewport.rect.size * .5f - Vector2.one * margin;
            float factor = Mathf.Max(1, Mathf.Abs(point.x) / half.x, Mathf.Abs(point.y) / half.y);
            return point / factor;
        }

        private void UpdateRadarChannel()
        {
            radarSections.Clear();
            float length = scenarioRoute != null ? scenarioRoute.LengthM : 1100;
            int count = Mathf.Clamp(Mathf.CeilToInt(length / 20), 2, 256);
            for (int i = 0; i <= count; i++)
            {
                float distance = length * i / count;
                Vector3 center = ChartPoint(distance, 0);
                Vector3 left = ChartPoint(distance, -1), right = ChartPoint(distance, 1);
                float Depth(Vector3 point) => scenarioBathymetry != null ? scenarioBathymetry.Sample(point).DepthM : FairwayModel.DepthAt(point);
                radarSections.Add(new RadarChannel.Section { Left = mapProjection.Project(left), Right = mapProjection.Project(right),
                    Center = mapProjection.Project(center), Color = ChannelColor(Depth(center) - ship.EffectiveDraftM),
                    LeftColor = ChannelColor(Depth(left) - ship.EffectiveDraftM), RightColor = ChannelColor(Depth(right) - ship.EffectiveDraftM) });
            }
            radarChannel.SetSections(radarSections);
            // The safety readout stays ahead of the vessel when the player pans elsewhere.
            radarMinAheadM = float.MaxValue;
            for (int i = 1; i <= 20; i++)
            {
                Vector3 point = ship.transform.position + Quaternion.Euler(0, ship.transform.eulerAngles.y, 0) * Vector3.forward * (i * VoyageSettings.MapRange / 20);
                float depth = scenarioBathymetry != null ? scenarioBathymetry.Sample(point).DepthM : FairwayModel.DepthAt(point);
                radarMinAheadM = Mathf.Min(radarMinAheadM, depth);
            }
        }

        private void UpdateWarnings(float speedMps, float underKeel, float currentSpeed)
        {
            string warning = string.Empty;
            if (underKeel < 0.8f) warning += Localization.Get("text.shallow.water.fcde8e1d");
            bool outsideFairway;
            if (scenarioRoute != null)
            {
                FairwayQuery query = scenarioRoute.Query(ship.transform.position);
                float width = query.LateralOffsetM >= 0f
                    ? query.Sample.rightWidthM
                    : query.Sample.leftWidthM;
                outsideFairway = Mathf.Abs(query.LateralOffsetM) > width;
            }
            else
            {
                outsideFairway = Mathf.Abs(FairwayModel.LateralOffset(
                    ship.transform.position.x, ship.transform.position.z)) >
                    FairwayModel.MarkedHalfWidth(ship.transform.position.z);
            }
            if (outsideFairway) warning += Localization.Get("text.outside.fairway.0955a9f6");
            float speedLimit = scenario != null
                ? scenario.LocalSpeedLimitMps
                : ship.Data != null ? ship.Data.controlLimits.maxLoadedSpeedMps : float.MaxValue;
            if (speedMps > speedLimit * 1.05f)
                warning += Localization.Get("text.overspeed.0665e89e");
            if (ship.Grounding != null &&
                (ship.Grounding.State == GroundingState.Touching ||
                 ship.Grounding.State == GroundingState.HardGrounding))
            {
                // The holding force is what astern thrust has to beat to work her off, so it belongs
                // on the warning line rather than only in the physics.
                warning += (ship.Grounding.State == GroundingState.Touching
                    ? Localization.Get("text.bottom.contact.17e952a7") : Localization.Get("text.hard.grounding.4a457f8d")) +
                    Localization.Get("text.0.f0.kn.holding.7b83d5ff", ship.Grounding.HoldingForceN / 1000f);
            }
            if (currentSpeed > 1.2f) warning += Localization.Get("text.strong.current.6ace2f40");
            if (currentSpeed > 0.65f)
            {
                float sideCurrent = ship.transform.InverseTransformDirection(
                    ship.EffectiveCurrentMps).x;
                if (Mathf.Abs(sideCurrent) > 0.35f)
                    warning += sideCurrent > 0f
                        ? Localization.Get("text.current.from.port.side.227441c8") : Localization.Get("text.current.from.starboard.side.3da4bf7d");
            }
            bool hasWarning = !string.IsNullOrEmpty(warning);
            bool danger = underKeel < 0.8f || outsideFairway ||
                (ship.Grounding != null &&
                 (ship.Grounding.State == GroundingState.Touching ||
                  ship.Grounding.State == GroundingState.HardGrounding));
            if (warningPanel != null) warningPanel.gameObject.SetActive(hasWarning);
            warningText.text = hasWarning
                ? Localization.Get("text.warning.39485faa") + warning.TrimEnd().Replace("\n", "    ·    ")
                : string.Empty;
            warningText.color = danger ? HudTheme.Danger : HudTheme.Warning;

            if (radarWarningRing != null)
            {
                radarWarningRing.gameObject.SetActive(danger);
                if (danger)
                {
                    float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
                    Image ringImage = radarWarningRing.GetComponent<Image>();
                    Color ringColor = underKeel < 0.8f ? HudTheme.Danger : HudTheme.Warning;
                    ringColor.a = pulse;
                    ringImage.color = ringColor;
                }
            }
        }

        private RectTransform AddMapContact(
            string name, Color color, Vector3 worldPosition, Vector2 size)
        {
            RectTransform rect = ImageRect(mapWorld, name, color, Vector2.zero, size);
            Image image = rect.GetComponent<Image>();
            image.sprite = HudTheme.Disc();
            image.raycastTarget = false;
            mapContacts.Add(new MapContact
            {
                Rect = rect,
                WorldPosition = worldPosition
            });
            return rect;
        }

        private void AddMapLine(
            string name, Color color, Vector3 worldStart, Vector3 worldEnd, float width)
        {
            RectTransform rect = ImageRect(mapWorld, name, color, Vector2.zero,
                new Vector2(width, 1f));
            Image image = rect.GetComponent<Image>();
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            rect.pivot = new Vector2(0.5f, 0.5f);
            mapLines.Add(new MapLine
            {
                Rect = rect,
                WorldStart = worldStart,
                WorldEnd = worldEnd
            });
        }

        private static Vector3 FairwayPoint(float z, float edgeSide)
        {
            float centerX = FairwayModel.CenterX(z);
            if (Mathf.Approximately(edgeSide, 0f))
                return new Vector3(centerX, 0f, z);

            float nextX = FairwayModel.CenterX(z + 4f);
            Vector2 tangent = new Vector2(nextX - centerX, 4f).normalized;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            float width = FairwayModel.MarkedHalfWidth(z) * edgeSide;
            return new Vector3(centerX - normal.x * width, 0f, z - normal.y * width);
        }

        private Vector2 RadarPosition(Vector3 worldPosition)
        {
            return mapProjection.Project(worldPosition);
        }

        private static void SetRadarLine(RectTransform line, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            line.anchoredPosition = (start + end) * 0.5f;
            line.sizeDelta = new Vector2(line.sizeDelta.x, delta.magnitude);
            line.localRotation = Quaternion.Euler(
                0f, 0f, -Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg);
            line.gameObject.SetActive(true);
        }

        private RectTransform[] BuildRadarSegments(
            Transform parent, string prefix, int count, Color color, float width)
        {
            var segments = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                segments[i] = ImageRect(parent, string.Format(Localization.Culture, "{0}{1}", prefix, i), color,
                    Vector2.zero, new Vector2(width, 1f));
                Image image = segments[i].GetComponent<Image>();
                image.sprite = null;
                image.type = Image.Type.Simple;
                image.raycastTarget = false;
                segments[i].gameObject.SetActive(false);
            }
            return segments;
        }

        private void UpdateRadarTrack()
        {
            if (radarTrackSegments == null) return;
            if (Time.unscaledTime >= nextTrackSampleTime)
            {
                Vector3 position = ship.transform.position;
                if (radarTrack.Count == 0 ||
                    Vector3.Distance(radarTrack[radarTrack.Count - 1], position) > 3f)
                {
                    radarTrack.Add(position);
                    if (radarTrack.Count > RadarTrackPointCount)
                        radarTrack.RemoveAt(0);
                }
                nextTrackSampleTime = Time.unscaledTime + 0.8f;
            }

            for (int i = 0; i < radarTrackSegments.Length; i++)
            {
                bool visible = i < radarTrack.Count - 1;
                radarTrackSegments[i].gameObject.SetActive(visible);
                if (visible)
                    SetRadarLine(radarTrackSegments[i],
                        RadarPosition(radarTrack[i]),
                        RadarPosition(radarTrack[i + 1]));
            }
        }

        private void UpdateRadarPrediction()
        {
            if (radarPredictionSegments == null) return;
            Vector3 localVelocity = ship.transform.InverseTransformDirection(
                ship.Body.linearVelocity);
            float speed = localVelocity.z;
            float sideSpeed = localVelocity.x;
            float yawRate = ship.Body.angularVelocity.y;
            Vector2 previous = mapProjection.Project(ship.transform.position);
            float predictedHeading = 0f;
            Vector2 predictedMeters = Vector2.zero;

            float stepSeconds = MapProjection.PredictionStep(
                new Vector2(speed, sideSpeed).magnitude, radarPredictionSegments.Length, VoyageSettings.MapRange * .55f);
            for (int i = 0; i < radarPredictionSegments.Length; i++)
            {
                predictedHeading += yawRate * stepSeconds;
                Vector2 forward = new Vector2(
                    Mathf.Sin(predictedHeading), Mathf.Cos(predictedHeading));
                Vector2 right = new Vector2(forward.y, -forward.x);
                predictedMeters +=
                    (forward * speed + right * sideSpeed) * stepSeconds;
                Vector3 world = ship.transform.position + Quaternion.Euler(0, ship.transform.eulerAngles.y, 0) * new Vector3(predictedMeters.x, 0, predictedMeters.y);
                Vector2 next = mapProjection.Project(world);
                SetRadarLine(radarPredictionSegments[i], previous, next);
                radarPredictionSegments[i].gameObject.SetActive(i % 2 == 0);
                previous = next;
            }
        }

        private void UpdateKeyboardShortcuts()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.upArrowKey.wasPressedThisFrame) TelegraphUp();
            if (keyboard.downArrowKey.wasPressedThisFrame) TelegraphDown();
            if (keyboard.wKey.wasPressedThisFrame) TelegraphUp();
            if (keyboard.sKey.wasPressedThisFrame) TelegraphDown();
            if (keyboard.qKey.wasPressedThisFrame) SetEngineTelegraph(0, telegraphIndices[0] + 1);
            if (keyboard.zKey.wasPressedThisFrame) SetEngineTelegraph(0, telegraphIndices[0] - 1);
            if (keyboard.eKey.wasPressedThisFrame) SetEngineTelegraph(1, telegraphIndices[1] + 1);
            if (keyboard.xKey.wasPressedThisFrame) SetEngineTelegraph(1, telegraphIndices[1] - 1);
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                ship.SetRudderCommand(-1f);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                ship.SetRudderCommand(1f);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame)
                ship.CenterRudder();
            if (keyboard.spaceKey.wasPressedThisFrame) SetTelegraph(3);
            if (keyboard.jKey.wasPressedThisFrame) ship.SetBowThrusterCommand(ship.BowThrusterCommand - 0.5f);
            if (keyboard.lKey.wasPressedThisFrame) ship.SetBowThrusterCommand(ship.BowThrusterCommand + 0.5f);
            if (keyboard.kKey.wasPressedThisFrame) ship.SetBowThrusterCommand(0f);
            if (keyboard.hKey.wasPressedThisFrame) PlayHorn();
            if (keyboard.mKey.wasPressedThisFrame)
            {
                GetComponent<HudLayout>()?.ToggleMap();
            }
            if (keyboard.nKey.wasPressedThisFrame) ToggleDayNight();
            if (keyboard.f2Key.wasPressedThisFrame) CycleWind();
            if (keyboard.f3Key.wasPressedThisFrame) ChangeWindDirection(45f);
            if (keyboard.f4Key.wasPressedThisFrame) CycleRain();
            if (keyboard.f5Key.wasPressedThisFrame) CycleFog();
            if (keyboard.tKey.wasPressedThisFrame)
            {
                if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                    SetTimeScale(1f);
                else
                {
                    simulationTime?.Cycle();
                    UpdateTimeScaleText();
                }
            }
            if (ship.GroundTackle != null)
            {
                if (keyboard.gKey.wasPressedThisFrame) CycleAnchor();
                if (keyboard.bKey.wasPressedThisFrame) ChainOrder(ChainCommand.LetGo);
                if (keyboard.yKey.wasPressedThisFrame) ChainOrder(ChainCommand.Hold);
                if (keyboard.uKey.wasPressedThisFrame) ChainOrder(ChainCommand.Heave);
                if (keyboard.oKey.wasPressedThisFrame) CycleStation();
                if (keyboard.pKey.wasPressedThisFrame) CycleShore();
                if (keyboard.iKey.wasPressedThisFrame) PassLine();
                if (keyboard.commaKey.wasPressedThisFrame) LineOrder(LineCommand.Haul);
                if (keyboard.slashKey.wasPressedThisFrame) LineOrder(LineCommand.Hold);
                if (keyboard.periodKey.wasPressedThisFrame) LineOrder(LineCommand.Slack);
                if (keyboard.fKey.wasPressedThisFrame) ship.GroundTackle.CastOff(selectedStation);
            }
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                GetComponent<HudLayout>()?.ToggleHelp();
            }
            Key[] viewKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
                Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
            for (int i = 0; i < viewKeys.Length; i++)
                if (keyboard[viewKeys[i]].wasPressedThisFrame) SetCamera(i);
        }

        public void RefreshAfterVoyageLoad()
        {
            if (ship == null) return;
            for (int engine = 0; engine < telegraphIndices.Length; engine++)
            {
                float command = ship.EngineCommand(Mathf.Min(engine, ship.EngineCount - 1));
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < TelegraphValues.Length; i++)
                {
                    float difference = Mathf.Abs(TelegraphValues[i] - command);
                    if (difference >= nearest) continue;
                    nearest = difference;
                    telegraphIndices[engine] = i;
                }
            }
            radarTrack.Clear();
            nextTrackSampleTime = Time.unscaledTime;
            RecenterMap();
        }

        private void TelegraphUp() => SetTelegraph(Mathf.Max(telegraphIndices[0], telegraphIndices[1]) + 1);
        private void TelegraphDown() => SetTelegraph(Mathf.Min(telegraphIndices[0], telegraphIndices[1]) - 1);

        private void SetTelegraph(int index)
        {
            SetEngineTelegraph(0, index);
            SetEngineTelegraph(1, index);
        }

        private void SetEngineTelegraph(int engine, int index)
        {
            int clamped = Mathf.Clamp(index, 0, TelegraphValues.Length - 1);
            if (ship.EngineCount < 2)
            {
                telegraphIndices[0] = telegraphIndices[1] = clamped;
                ship.SetThrottleCommand(TelegraphValues[clamped]);
                return;
            }
            telegraphIndices[engine] = clamped;
            ship.SetEngineCommand(engine, TelegraphValues[clamped]);
        }

        // A fast craft handles differently once the foils or the cushion carry it, and the lift is not
        // visible from the bridge, so the speed block says which state the vessel is in.
        public static string FormatEngineControls(int engineCount) => engineCount == 1
            ? Localization.Get("text.a.d.rudder.w.s.engine.q.z.or.e.x.telegraph.003759e7")
            : Localization.Get("text.a.d.rudder.w.s.engines.q.z.port.e.x.stbd.946ac490");

        public static string FormatSupportStatus(VesselSupport support, float speedThroughWaterMps)
        {
            if (!SupportModel.Lifts(support)) return string.Empty;
            float progress = SupportModel.Fraction(support, speedThroughWaterMps) /
                support.supportedWeightFraction;
            if (progress < 0.02f) return Localization.Get("text.hullborne.5a951f70");
            string mode = support.mode == "hydrofoil" ? Localization.Get("text.foilborne.635f50bc") : Localization.Get("text.cushionborne.83fb35a6");
            string color = progress > 0.98f ? "#4DC76B" : "#E8B24D";
            return string.Format(Localization.Culture, "   <color={0}>{1} {2:F0}%</color>", color, mode, progress * 100f);
        }

        public static float RadarPredictionStepSeconds(float speedMps, int steps) =>
            MapProjection.PredictionStep(speedMps, steps, 300f * .55f);

        public static string FormatBowThrusterStatus(bool fitted, float output)
        {
            if (!fitted) return string.Empty;
            if (Mathf.Abs(output) < 0.01f) return Localization.Get("text.bow.thruster.off.68916baa");
            return Localization.Get("text.bow.thruster.0.1.f0.eaea03d2", (output < 0f ? Localization.Get("text.port.6acc99af") : Localization.Get("text.stbd.96228e9d")), Mathf.Abs(output) * 100f);
        }

        private string FormatEngineStatus()
        {
            string order = telegraphIndices[0] == telegraphIndices[1]
                ? Localization.Source(TelegraphNames[telegraphIndices[0]])
                : string.Format(Localization.Culture, "P {0}  S {1}", Localization.Source(TelegraphNames[telegraphIndices[0]]), Localization.Source(TelegraphNames[telegraphIndices[1]]));
            var shafts = new System.Text.StringBuilder();
            for (int i = 0; i < ship.EngineCount; i++)
            {
                string side = ship.EngineCount < 2 ? string.Empty : i == 0 ? "P " : "S ";
                shafts.Append(Localization.Get("text.0.rpm.1.0.0.0.load.2.f0.f5097c97", side, ship.ShaftRpm(i), ship.EngineLoadFraction(i) * 100f));
            }
            return Localization.Get("text.telegraph.0.1.ce01c452", order, shafts.ToString().TrimEnd());
        }

        private void SetCamera(int index)
        {
            if (followCamera != null) followCamera.SetView(index);
        }

        private void PreviousCamera()
        {
            if (followCamera == null || followCamera.ViewCount == 0) return;
            SetCamera((followCamera.ViewIndex - 1 + followCamera.ViewCount) % followCamera.ViewCount);
        }

        private void NextCamera()
        {
            if (followCamera != null) followCamera.NextView();
        }

        private void ToggleDayNight()
        {
            if (dayNight == null) return;
            dayNight.Toggle();
            if (timeOfDayText != null) timeOfDayText.text = dayNight.TimeLabel;
        }

        private void SetTimeScale(float scale)
        {
            simulationTime?.SetScale(scale);
            UpdateTimeScaleText();
        }

        private void UpdateTimeScaleText()
        {
            if (timeScaleText != null && simulationTime != null)
                timeScaleText.text = Localization.Get("text.time.0.feff9f63", simulationTime.DisplayText);
        }

        private void ChangeWindDirection(float degrees)
        {
            weather?.RotateWind(degrees);
            UpdateWeatherText();
        }

        private void CycleWind()
        {
            weather?.CycleWindSpeed();
            UpdateWeatherText();
        }

        private void CycleRain()
        {
            weather?.CycleRain();
            UpdateWeatherText();
        }

        private void CycleFog()
        {
            weather?.CycleFog();
            UpdateWeatherText();
        }

        private void UpdateWeatherText()
        {
            if (weatherText != null && weather != null)
                weatherText.text = weather.StatusText;
        }

        private void UpdateDepthRadar(float currentDepth)
        {
            if (depthRadarText == null) return;
            float minimumAhead = Mathf.Min(currentDepth, radarMinAheadM);
            depthRadarText.text = Localization.Get("map.depth", currentDepth, minimumAhead, ship.EffectiveDraftM) +
                (VoyageSettings.MapLegend ? "\n" + Localization.Get("map.legendText") : "");
        }

        public static string FormatCameraStatus(string viewName, int viewIndex, int viewCount)
        {
            return Localization.Get("text.camera.0.2ef9a5eb", viewName) +
                string.Format(Localization.Culture, "<size=14>{0}/{1}</size>", viewIndex + 1, viewCount);
        }

        // Outside a scenario the only limit is the vessel's own service speed, which differs by a factor
        // of eight across the catalogue.
        public static string FormatObjectiveStatus(float distanceMeters, float speedLimitMps)
        {
            return Localization.Get("text.objective.51082de9") +
                Localization.Get("text.proceed.to.waypoint.e02bc46d") +
                Localization.Get("text.distance.0.f0.m.981e8dab", distanceMeters) +
                Localization.Get("text.service.speed.0.f0.km.h.959ae7bd", speedLimitMps * 3.6f);
        }

        // Calm nautical depth shading: muted, low-alpha zones that blend over the
        // dark chart instead of a saturated red grid. Danger reads clearly without
        // shouting because the soft tiles overlap into smooth bands.
        // Channel fill colour by under-keel clearance: calm teal where safe, amber
        // for shallow caution, red ONLY where genuinely too shallow to pass.
        private static Color32 ChannelColor(float clearance)
        {
            if (clearance < 0.8f) return new Color32(168, 54, 46, 168);   // danger
            if (clearance < 2.0f) return new Color32(166, 120, 52, 150);  // shallow caution
            return new Color32(32, 98, 112, 148);                         // safe water
        }

        private void RangeRing(Transform parent, string name, float diameter, Color color)
        {
            var ring = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(MapRangeRing)).GetComponent<MapRangeRing>();
            ring.transform.SetParent(parent, false);
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = ring.rectTransform.pivot = new Vector2(.5f, .5f);
            ring.rectTransform.sizeDelta = Vector2.one * diameter;
            ring.color = color; ring.raycastTarget = false;
        }

        // A buoy is a soft glow behind a crisp coloured dot; both are world-anchored
        // contacts so they track and rotate with the head-up chart.
        private void AddBuoy(string name, Color color, Vector3 worldPosition)
        {
            RectTransform glow = AddMapContact(name + "Glow",
                new Color(color.r, color.g, color.b, 0.35f), worldPosition,
                new Vector2(26f, 26f));
            glow.GetComponent<Image>().sprite = HudTheme.Soft(8);
            AddMapContact(name, color, worldPosition, new Vector2(12f, 12f));
        }

        private void CreateHorn()
        {
            hornSource = gameObject.AddComponent<AudioSource>();
            hornSource.outputAudioMixerGroup = ShipSimulator.Audio.AudioMix.Group(ShipSimulator.Audio.AudioBus.Signals);
            hornSource.spatialBlend = 0f;
            hornSource.volume = 0.32f;
            const int sampleRate = 22050;
            const float duration = 1.2f;
            float[] samples = new float[Mathf.RoundToInt(sampleRate * duration)];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)sampleRate;
                float envelope = Mathf.Min(1f, time * 8f) *
                    Mathf.Min(1f, (duration - time) * 8f);
                samples[i] = (Mathf.Sin(time * 2f * Mathf.PI * 165f) * 0.7f +
                    Mathf.Sin(time * 2f * Mathf.PI * 220f) * 0.3f) * envelope;
            }
            AudioClip clip = AudioClip.Create("ShipHorn", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            hornSource.clip = clip;
        }

        private void PlayHorn()
        {
            if (hornSource != null) hornSource.Play();
        }

        private void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private Text CompactInstrument(RectTransform parent, int index, float width, HudIcon icon)
        {
            float[] widths = { 205f, 285f, 255f, 270f };
            float start = -485f;
            float x = start;
            for (int i = 0; i < index; i++) x += widths[i];
            RectTransform panel = SubPanel(parent, string.Format(Localization.Culture, "Instrument{0}", index),
                new Vector2(x + width * 0.5f, 0f), new Vector2(width - 6f, 86f));
            Icon(panel, icon, new Vector2(-width * 0.5f + 26f, 20f), 22f, HudTheme.AccentSoft);
            Text text = Label(panel, string.Empty, 16, TextAnchor.MiddleCenter,
                new Vector2(40f, 2f), new Vector2(-10f, -2f));
            text.supportRichText = true;
            text.lineSpacing = 0.92f;
            if (index < 3)
            {
                RectTransform divider = ImageRect(parent, string.Format(Localization.Culture, "Divider{0}", index),
                    new Color(0.40f, 0.60f, 0.72f, 0.16f),
                    new Vector2(x + width, 0f), new Vector2(1.5f, 56f));
                divider.GetComponent<Image>().raycastTarget = false;
            }
            return text;
        }

        private RectTransform AnchoredPanel(string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            return Panel(name, anchorMin, anchorMax, pivot, position, size);
        }

        private void UpdateActiveStates()
        {
            for (int i = 0; i < telegraphButtons.Count; i++)
                telegraphButtons[i].SetActive(i == telegraphIndices[0] || i == telegraphIndices[1]);

            int rudderState = ship.RudderCommand < -0.1f ? 0 :
                ship.RudderCommand > 0.1f ? 2 : 1;
            for (int i = 0; i < rudderButtons.Count; i++)
                rudderButtons[i].SetActive(i == rudderState);

            if (followCamera != null)
                for (int i = 0; i < cameraButtons.Count; i++)
                    cameraButtons[i].SetActive(false);
        }

        private static string BuildHeadingTape(float heading)
        {
            string[] marks = new string[7];
            for (int i = -3; i <= 3; i++)
            {
                int value = Mathf.RoundToInt(heading / 10f) * 10 + i * 10;
                value = (value % 360 + 360) % 360;
                marks[i + 3] = value switch
                {
                    0 => "N",
                    90 => "E",
                    180 => "S",
                    270 => "W",
                    _ => value.ToString("000")
                };
            }
            return string.Join("      ", marks);
        }

        private string CurrentArrow(Vector3 current)
        {
            Vector3 local = ship.transform.InverseTransformDirection(current);
            if (Mathf.Abs(local.x) > Mathf.Abs(local.z))
                return local.x >= 0f ? ">" : "<";
            return local.z >= 0f ? "^" : "v";
        }

        private RectTransform Panel(string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            AddGlassBackground(rect, panelColor, true);
            return rect;
        }

        // Builds a modern glass-panel backing: soft drop shadow, rounded translucent
        // fill, and a subtle border. Content is added to the panel afterwards so it
        // always renders on top of these background layers.
        private void AddGlassBackground(RectTransform root, Color fill, bool border)
        {
            RectTransform shadow = Stretch(root, "Shadow", new Vector2(-10f, -16f),
                new Vector2(10f, 4f));
            Image shadowImage = shadow.gameObject.AddComponent<Image>();
            shadowImage.sprite = HudTheme.Soft(HudTheme.PanelRadius);
            shadowImage.type = Image.Type.Sliced;
            shadowImage.color = HudTheme.PanelShadow;
            shadowImage.raycastTarget = false;

            RectTransform fillRect = Stretch(root, "Fill", Vector2.zero, Vector2.zero);
            Image fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = HudTheme.Rounded(HudTheme.PanelRadius);
            fillImage.type = Image.Type.Sliced;
            fillImage.color = fill;
            fillImage.raycastTarget = true;

            if (!border) return;
            RectTransform outline = Stretch(root, "Border", Vector2.zero, Vector2.zero);
            Image outlineImage = outline.gameObject.AddComponent<Image>();
            outlineImage.sprite = HudTheme.Outline(HudTheme.PanelRadius);
            outlineImage.type = Image.Type.Sliced;
            outlineImage.color = HudTheme.PanelBorder;
            outlineImage.raycastTarget = false;
        }

        private RectTransform Stretch(Transform parent, string name,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        private RectTransform SubPanel(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = ImageRect(parent, name, HudTheme.PanelFillSoft, position, size);
            Image image = rect.GetComponent<Image>();
            image.sprite = HudTheme.Rounded(12);
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            return rect;
        }

        private RectTransform ImageRect(Transform parent, string name, Color color,
            Vector2 position, Vector2 size)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            imageObject.GetComponent<Image>().color = color;
            return rect;
        }

        private Image Icon(Transform parent, HudIcon icon, Vector2 position,
            float size, Color color)
        {
            RectTransform rect = ImageRect(parent, string.Format(Localization.Culture, "Icon_{0}", icon), color, position,
                new Vector2(size, size));
            Image image = rect.GetComponent<Image>();
            image.sprite = HudTheme.Icon(icon);
            image.raycastTarget = false;
            return image;
        }

        private Text Label(Transform parent, string value, int size, TextAnchor alignment,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Text label = labelObject.GetComponent<Text>();
            label.font = font;
            label.fontSize = Mathf.Max(16, size);
            label.color = HudTheme.TextPrimary;
            label.alignment = alignment;
            LocalizedLabel.BindSource(label, value);
            label.supportRichText = true;
            label.raycastTarget = false;
            Shadow shadow = labelObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.01f, 0.02f, 0.6f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return label;
        }

        private HudButton Button(Transform parent, string caption, Vector2 position,
            Vector2 size, Action action)
        {
            GameObject buttonObject = new GameObject(caption,
                typeof(RectTransform), typeof(Image), typeof(HudButton));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            // Root image is a transparent raycast catcher; visible layers are ordered
            // children (glow behind fill behind border behind label).
            Image raycastImage = buttonObject.GetComponent<Image>();
            raycastImage.color = new Color(0f, 0f, 0f, 0f);
            raycastImage.raycastTarget = true;

            RectTransform glow = Stretch(buttonObject.transform, "Glow",
                new Vector2(-9f, -9f), new Vector2(9f, 9f));
            Image glowImage = glow.gameObject.AddComponent<Image>();
            glowImage.sprite = HudTheme.Soft(HudTheme.ButtonRadius);
            glowImage.type = Image.Type.Sliced;
            glowImage.color = new Color(0f, 0f, 0f, 0f);
            glowImage.raycastTarget = false;

            RectTransform fill = Stretch(buttonObject.transform, "Fill",
                Vector2.zero, Vector2.zero);
            Image fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.sprite = HudTheme.Rounded(HudTheme.ButtonRadius);
            fillImage.type = Image.Type.Sliced;
            fillImage.color = HudTheme.ButtonIdle;
            fillImage.raycastTarget = false;

            RectTransform outline = Stretch(buttonObject.transform, "Border",
                Vector2.zero, Vector2.zero);
            Image outlineImage = outline.gameObject.AddComponent<Image>();
            outlineImage.sprite = HudTheme.Outline(HudTheme.ButtonRadius);
            outlineImage.type = Image.Type.Sliced;
            outlineImage.color = HudTheme.PanelBorder;
            outlineImage.raycastTarget = false;

            Text text = Label(buttonObject.transform, caption, 16, TextAnchor.MiddleCenter,
                new Vector2(4f, 2f), new Vector2(-4f, -2f));
            text.color = HudTheme.ButtonText;
            text.raycastTarget = false;

            HudButton button = buttonObject.GetComponent<HudButton>();
            button.Initialise(fillImage, outlineImage, glowImage, text, action);
            return button;
        }
    }
}
