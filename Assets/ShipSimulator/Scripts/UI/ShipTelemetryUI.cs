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
        private readonly List<MapLine> mapLines = new List<MapLine>();
        private readonly List<Vector3> radarTrack = new List<Vector3>();
        private readonly List<HudButton> telegraphButtons = new List<HudButton>();
        private readonly List<HudButton> cameraButtons = new List<HudButton>();
        private readonly List<HudButton> rudderButtons = new List<HudButton>();
        private RectTransform radarWarningRing;
        private int telegraphIndex = 3;
        private bool helpVisible;
        private bool mapVisible = true;
        private Vector3 objectivePosition = new Vector3(0f, 0f, 650f);
        private const float MapPixelsPerMeter = 0.82f;
        private const float RadarVesselOffsetY = -82f;
        private const int RadarTrackPointCount = 18;
        private const int RadarPredictionPointCount = 11;
        private float nextTrackSampleTime;

        private sealed class MapContact
        {
            public RectTransform Rect;
            public Vector3 WorldPosition;
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

        private void Start()
        {
            if (ship == null) return;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            followCamera = Camera.main != null ? Camera.main.GetComponent<ShipFollowCamera>() : null;
            if (ship.GetComponent<ShipWakeController>() == null)
                ship.gameObject.AddComponent<ShipWakeController>();
            if (ship.GetComponent<NavigationLightRig>() == null)
                ship.gameObject.AddComponent<NavigationLightRig>();
            dayNight = FindFirstObjectByType<DayNightController>();
            simulationTime = GetComponent<SimulationTimeController>();
            if (simulationTime == null)
                simulationTime = gameObject.AddComponent<SimulationTimeController>();
            weather = FindAnyObjectByType<WeatherController>();
            if (weather == null)
                weather = gameObject.AddComponent<WeatherController>();
            scenarioRoute = FindFirstObjectByType<FairwayRoute>();
            scenarioBathymetry = FindFirstObjectByType<ScenarioBathymetry>();
            scenario = FindFirstObjectByType<GorodetsScenarioController>();
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

        private void Update()
        {
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
            UpdateKeyboardShortcuts();

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
            float engineLoad = Mathf.Abs(ship.ActualThrottle) * 100f;
            float rpm = engineLoad < 1f ? 0f : Mathf.Lerp(180f, 620f, engineLoad / 100f);
            speedText.text =
                $"<size=14><color=#8AA0AD>SPEED</color></size>\n" +
                $"<size=30><b>{speedMps * 1.943844f:F1}</b></size><size=15> kn</size>\n" +
                $"<size=14><color=#8AA0AD>{speedMps * 3.6f:F1} km/h</color></size>";
            headingText.text =
                $"<size=14><color=#8AA0AD>COURSE</color></size>\n" +
                $"<size=30><b>{heading:000}°</b></size>\n" +
                $"<size=14><color=#8AA0AD>drift {driftAngle:+0.0;-0.0;0.0}°</color></size>";
            depthText.text =
                $"<size=14><color=#8AA0AD>DEPTH</color></size>\n" +
                $"<size=30><b>{channelDepth:F1}</b></size><size=15> m</size>\n" +
                $"<size=14><color=#8AA0AD>under keel {underKeel:F1} m</color></size>";
            rudderText.text =
                $"RUDDER  <size=23><b>{ship.RudderAngleDeg:+0.0;-0.0;0.0} deg</b></size>\n" +
                $"<size=14>COMMAND {ship.RudderCommand * 35f:+0;-0;0} deg</size>";
            engineText.text =
                $"TELEGRAPH  <size=23><b>{TelegraphNames[telegraphIndex]}</b></size>\n" +
                $"<size=15>RPM {rpm:F0}   ENGINE LOAD {engineLoad:F0}%</size>";
            currentText.text =
                $"<size=14><color=#8AA0AD>CURRENT</color></size>\n" +
                $"<size=28><b>{current.magnitude:F1}</b></size><size=15> m/s {CurrentArrow(current)}</size>\n" +
                $"<size=14><color=#8AA0AD>cargo {ship.LoadFraction * 100f:F0}%</color></size>";
            cameraText.text = followCamera == null
                ? "CAMERA"
                : FormatCameraStatus(
                    followCamera.ViewName,
                    followCamera.ViewIndex,
                    followCamera.ViewCount);
            headingTapeText.text = BuildHeadingTape(heading);
            float distance = Vector3.Distance(
                new Vector3(ship.transform.position.x, 0f, ship.transform.position.z),
                objectivePosition);
            objectiveText.text = scenario != null
                ? $"<size=14><color=#56C7E6>OBJECTIVE</color></size>\n" +
                  $"<size=23><b>{scenario.Phase}</b></size>\n" +
                  $"<size=15>{scenario.Instruction}</size>\n\n" +
                  $"<color=#8AA0AD>Score</color> <b>{scenario.Score:F0}/100</b>     " +
                  $"<color=#8AA0AD>Limit</color> <b>{scenario.LocalSpeedLimitMps * 3.6f:F0} km/h</b>"
                : FormatObjectiveStatus(distance);
            depthText.color = underKeel < 0.8f
                ? HudTheme.Danger
                : underKeel < 2f ? HudTheme.Warning : HudTheme.TextPrimary;

            if (rudderNeedle != null)
                rudderNeedle.anchoredPosition = new Vector2(
                    Mathf.Clamp(ship.RudderAngleDeg / 35f, -1f, 1f) * 145f, -4f);
            if (rudderCommandNeedle != null)
                rudderCommandNeedle.anchoredPosition = new Vector2(
                    Mathf.Clamp(ship.RudderCommand, -1f, 1f) * 145f, 9f);
            UpdateActiveStates();
            UpdateMiniMap();
            UpdateDepthRadar(channelDepth);
            UpdateWarnings(speedMps, underKeel, current.magnitude);
            if (weatherText != null && weather != null)
                weatherText.text = weather.StatusText;
        }

        private void BuildInterface()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

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
            BuildCameraControls(transform as RectTransform);
            BuildTimeControls();
            BuildWeatherControls();

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
            helpText.text =
                "A/D  RUDDER     W/S  TELEGRAPH     SPACE  STOP     1-9  CAMERAS\n" +
                "RMB ORBIT   H HORN   M MAP   N DAY/NIGHT   T TIME   F2 WIND   F3 DIR   F4 RAIN   F5 FOG";
            helpText.gameObject.SetActive(false);
            Text helpPrompt = Label(transform, "F1  CONTROLS", 16, TextAnchor.LowerCenter,
                new Vector2(820f, 16f), new Vector2(-820f, -1034f));
            helpPrompt.color = accentColor;
            if (readout != null) readout.gameObject.SetActive(false);
        }

        private void BuildRudderControls(RectTransform parent)
        {
            RectTransform block = AnchoredPanel("RudderBlock", new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 24f),
                new Vector2(420f, 146f));
            Icon(block, HudIcon.Rudder, new Vector2(-176f, 112f), 20f, HudTheme.Accent);
            rudderText = Label(block, "RUDDER  0.0 deg", 19, TextAnchor.UpperCenter,
                new Vector2(8f, 108f), new Vector2(-8f, -8f));
            RectTransform dial = ImageRect(block, "RudderScale", new Color(0.03f, 0.07f, 0.10f, 0.95f),
                new Vector2(0f, 12f), new Vector2(350f, 38f));
            Image dialImage = dial.GetComponent<Image>();
            dialImage.sprite = HudTheme.Rounded(10);
            dialImage.type = Image.Type.Sliced;
            // Scale tick marks: centre (0) emphasised, symmetrical port/stbd graduations.
            for (int t = -3; t <= 3; t++)
            {
                bool centre = t == 0;
                RectTransform tick = ImageRect(dial, $"Tick{t}",
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
            rudderButtons.Add(Button(block, "< PORT  [A]", new Vector2(-118f, -43f),
                new Vector2(108f, 38f), () => ship.SetRudderCommand(-1f)));
            rudderButtons.Add(Button(block, "MIDSHIPS [C]", new Vector2(0f, -43f),
                new Vector2(122f, 38f), ship.CenterRudder));
            rudderButtons.Add(Button(block, "STBD [D] >", new Vector2(118f, -43f),
                new Vector2(108f, 38f), () => ship.SetRudderCommand(1f)));
        }

        private void BuildTelegraph(RectTransform parent)
        {
            RectTransform block = AnchoredPanel("TelegraphBlock", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f),
                new Vector2(680f, 154f));
            Icon(block, HudIcon.Speed, new Vector2(-300f, 118f), 22f, HudTheme.Accent);
            engineText = Label(block, "TELEGRAPH  STOP", 19, TextAnchor.UpperCenter,
                new Vector2(8f, 103f), new Vector2(-8f, -8f));
            string[] labels =
            {
                "FULL\nASTERN", "HALF\nASTERN", "SLOW\nASTERN", "STOP",
                "SLOW\nAHEAD", "HALF\nAHEAD", "FULL\nAHEAD"
            };
            for (int i = 0; i < labels.Length; i++)
            {
                int command = i;
                HudButton button = Button(block, labels[i], new Vector2(-270f + i * 90f, -37f),
                    new Vector2(84f, 48f), () => SetTelegraph(command));
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
            cameraButtons.Add(Button(block, "PREV  [V]", new Vector2(-112f, -38f),
                new Vector2(104f, 38f), PreviousCamera));
            cameraButtons.Add(Button(block, "NEXT  [V]", new Vector2(0f, -38f),
                new Vector2(104f, 38f), NextCamera));
            Button(block, "DOCK  [8]", new Vector2(112f, -38f),
                new Vector2(104f, 38f), () => SetCamera(7));
            Button(block, "NAV  [9]", new Vector2(112f, 5f),
                new Vector2(104f, 34f), () => SetCamera(8));
            Button(block, "DAY/NIGHT [N]", new Vector2(-112f, 5f),
                new Vector2(104f, 34f), ToggleDayNight);
            timeOfDayText = Label(block, "DAY", 14, TextAnchor.MiddleCenter,
                new Vector2(145f, 72f), new Vector2(-145f, -40f));
            timeOfDayText.color = accentColor;
        }

        private void BuildTimeControls()
        {
            RectTransform block = AnchoredPanel("TimeScaleBlock",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-22f, 170f), new Vector2(350f, 58f));
            Icon(block, HudIcon.Time, new Vector2(-150f, 0f), 20f, HudTheme.Accent);
            timeScaleText = Label(block, "TIME 1x", 16, TextAnchor.MiddleLeft,
                new Vector2(30f, 8f), new Vector2(-230f, -8f));
            timeScaleText.color = accentColor;
            Button(block, "1x", new Vector2(-30f, 0f), new Vector2(58f, 34f),
                () => SetTimeScale(1f));
            Button(block, "2x", new Vector2(40f, 0f), new Vector2(58f, 34f),
                () => SetTimeScale(2f));
            Button(block, "4x", new Vector2(110f, 0f), new Vector2(58f, 34f),
                () => SetTimeScale(4f));
        }

        private void BuildWeatherControls()
        {
            RectTransform block = AnchoredPanel("WeatherBlock",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 182f), new Vector2(330f, 132f));
            Icon(block, HudIcon.Wind, new Vector2(-142f, 44f), 18f, HudTheme.Accent);
            weatherText = Label(block, "WEATHER", 13, TextAnchor.UpperCenter,
                new Vector2(26f, 56f), new Vector2(-10f, -6f));
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
            mapContacts.Clear();
            mapLines.Clear();
            radarTrack.Clear();
            miniMap = Panel("MiniMap",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(420f, 420f));
            Icon(miniMap, HudIcon.Compass, new Vector2(-178f, 181f), 20f, HudTheme.Accent);
            Text radarTitle = Label(miniMap, "RIVER RADAR  ·  HEAD-UP  ·  200 m",
                17, TextAnchor.UpperLeft, new Vector2(52f, 380f), new Vector2(-16f, -10f));
            radarTitle.color = HudTheme.AccentSoft;
            RectTransform viewport = ImageRect(miniMap, "Viewport",
                new Color(0.035f, 0.058f, 0.094f, 0.98f), // calm dark navy
                new Vector2(0f, 14f), new Vector2(384f, 300f));
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.sprite = HudTheme.Rounded(12);
            viewportImage.type = Image.Type.Sliced;
            viewportImage.raycastTarget = false;
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

            for (int i = 0; i < 21; i++)
            {
                float startZ = -180f + i * 48f;
                float endZ = startZ + 32f;
                Vector3 routeStart = FairwayPoint(startZ, 0f);
                Vector3 routeEnd = FairwayPoint(endZ, 0f);
                if (i % 2 == 0)
                    AddMapLine($"FairwayRoute{i}", new Color(0.85f, 0.68f, 0.32f, 0.55f),
                        routeStart, routeEnd, 2.4f);
                AddMapLine($"PortEdge{i}", new Color(0.84f, 0.28f, 0.22f, 0.75f),
                    FairwayPoint(startZ, -1f), FairwayPoint(endZ, -1f), 3f);
                AddMapLine($"StarboardEdge{i}", new Color(0.26f, 0.70f, 0.36f, 0.75f),
                    FairwayPoint(startZ, 1f), FairwayPoint(endZ, 1f), 3f);
            }
            float[] stations = { -55f, 45f, 145f, 240f, 330f, 415f, 495f, 570f, 640f, 705f };
            for (int i = 0; i < stations.Length; i++)
            {
                float z = stations[i];
                float centerX = FairwayModel.CenterX(z);
                float nextX = FairwayModel.CenterX(z + 4f);
                Vector2 tangent = new Vector2(nextX - centerX, 4f).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float width = FairwayModel.MarkedHalfWidth(z);
                AddBuoy($"LeftGreenBuoy{i}", new Color(0.32f, 0.82f, 0.45f),
                    new Vector3(centerX + normal.x * width, 0f, z + normal.y * width));
                AddBuoy($"RightRedBuoy{i}", new Color(0.92f, 0.30f, 0.24f),
                    new Vector3(centerX - normal.x * width, 0f, z - normal.y * width));
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
                new Vector2(0f, RadarVesselOffsetY), new Vector2(40f, 40f));
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
                new Vector2(0f, RadarVesselOffsetY + 116f), new Vector2(2f, 232f))
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
        }

        private void UpdateMiniMap()
        {
            if (mapVessel == null || mapWorld == null) return;
            float heading = ship.transform.eulerAngles.y * Mathf.Deg2Rad;
            float sin = Mathf.Sin(heading);
            float cos = Mathf.Cos(heading);
            for (int i = 0; i < mapContacts.Count; i++)
            {
                MapContact contact = mapContacts[i];
                Vector3 delta = contact.WorldPosition - ship.transform.position;
                float localX = delta.x * cos - delta.z * sin;
                float localZ = delta.x * sin + delta.z * cos;
                contact.Rect.anchoredPosition = new Vector2(
                    localX * MapPixelsPerMeter,
                    localZ * MapPixelsPerMeter + RadarVesselOffsetY);
                contact.Rect.gameObject.SetActive(
                    Mathf.Abs(contact.Rect.anchoredPosition.x) < 195f &&
                    contact.Rect.anchoredPosition.y > -170f &&
                    contact.Rect.anchoredPosition.y < 170f);
            }
            for (int i = 0; i < mapLines.Count; i++)
            {
                MapLine line = mapLines[i];
                Vector2 start = RadarPosition(line.WorldStart, sin, cos);
                Vector2 end = RadarPosition(line.WorldEnd, sin, cos);
                SetRadarLine(line.Rect, start, end);
            }
            UpdateRadarChannel(sin, cos);
            UpdateRadarTrack(sin, cos);
            UpdateRadarPrediction();
            mapVessel.anchoredPosition = new Vector2(0f, RadarVesselOffsetY);
            mapVessel.localRotation = Quaternion.identity;
        }

        // Rebuilds the filled channel ribbon each frame from fairway cross-sections
        // around the ship, coloured by under-keel clearance.
        private void UpdateRadarChannel(float sin, float cos)
        {
            if (radarChannel == null) return;
            radarSections.Clear();
            float shipZ = ship.transform.position.z;
            float draft = ship.EstimatedDraftM;
            radarMinAheadM = float.MaxValue;

            for (float z = shipZ - 120f; z <= shipZ + 320f; z += 11f)
            {
                Vector3 leftWorld = FairwayPoint(z, 1f);
                Vector3 rightWorld = FairwayPoint(z, -1f);
                Vector3 centerWorld = FairwayPoint(z, 0f);
                float depth = scenarioBathymetry != null
                    ? scenarioBathymetry.Sample(centerWorld).DepthM
                    : FairwayModel.DepthAt(centerWorld);
                float clearance = depth - draft;

                Vector2 left = RadarPosition(leftWorld, sin, cos);
                Vector2 right = RadarPosition(rightWorld, sin, cos);
                radarSections.Add(new RadarChannel.Section
                {
                    Left = left,
                    Right = right,
                    Color = ChannelColor(clearance)
                });

                Vector2 center = RadarPosition(centerWorld, sin, cos);
                if (center.y > RadarVesselOffsetY && center.y < 168f &&
                    Mathf.Abs(center.x) < 150f)
                    radarMinAheadM = Mathf.Min(radarMinAheadM, depth);
            }

            if (radarMinAheadM == float.MaxValue) radarMinAheadM = 0f;
            radarChannel.SetSections(radarSections);
        }

        private void UpdateWarnings(float speedMps, float underKeel, float currentSpeed)
        {
            string warning = string.Empty;
            if (underKeel < 0.8f) warning += "SHALLOW WATER\n";
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
            if (outsideFairway) warning += "OUTSIDE FAIRWAY\n";
            float speedLimit = scenario != null
                ? scenario.LocalSpeedLimitMps
                : ship.Data != null ? ship.Data.controlLimits.maxLoadedSpeedMps : float.MaxValue;
            if (speedMps > speedLimit * 1.05f)
                warning += "OVERSPEED\n";
            if (ship.Grounding != null)
            {
                if (ship.Grounding.State == GroundingState.Touching)
                    warning += "BOTTOM CONTACT\n";
                if (ship.Grounding.State == GroundingState.HardGrounding)
                    warning += "HARD GROUNDING\n";
            }
            if (currentSpeed > 1.2f) warning += "STRONG CURRENT\n";
            if (currentSpeed > 0.65f)
            {
                float sideCurrent = ship.transform.InverseTransformDirection(
                    ship.EffectiveCurrentMps).x;
                if (Mathf.Abs(sideCurrent) > 0.35f)
                    warning += sideCurrent > 0f
                        ? "CURRENT FROM PORT SIDE\n" : "CURRENT FROM STARBOARD SIDE\n";
            }
            bool hasWarning = !string.IsNullOrEmpty(warning);
            bool danger = underKeel < 0.8f || outsideFairway ||
                (ship.Grounding != null &&
                 (ship.Grounding.State == GroundingState.Touching ||
                  ship.Grounding.State == GroundingState.HardGrounding));
            if (warningPanel != null) warningPanel.gameObject.SetActive(hasWarning);
            warningText.text = hasWarning
                ? "WARNING    " + warning.TrimEnd().Replace("\n", "    ·    ")
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
            image.sprite = HudTheme.Soft(6);
            image.type = Image.Type.Sliced;
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

        private Vector2 RadarPosition(Vector3 worldPosition, float sin, float cos)
        {
            Vector3 delta = worldPosition - ship.transform.position;
            return new Vector2(
                (delta.x * cos - delta.z * sin) * MapPixelsPerMeter,
                (delta.x * sin + delta.z * cos) * MapPixelsPerMeter +
                RadarVesselOffsetY);
        }

        private static void SetRadarLine(RectTransform line, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            line.anchoredPosition = (start + end) * 0.5f;
            line.sizeDelta = new Vector2(line.sizeDelta.x, delta.magnitude);
            line.localRotation = Quaternion.Euler(
                0f, 0f, -Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg);
            line.gameObject.SetActive(
                line.anchoredPosition.x > -220f && line.anchoredPosition.x < 220f &&
                line.anchoredPosition.y > -205f && line.anchoredPosition.y < 205f);
        }

        private RectTransform[] BuildRadarSegments(
            Transform parent, string prefix, int count, Color color, float width)
        {
            var segments = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                segments[i] = ImageRect(parent, $"{prefix}{i}", color,
                    Vector2.zero, new Vector2(width, 1f));
                Image image = segments[i].GetComponent<Image>();
                image.sprite = HudTheme.Soft(6);
                image.type = Image.Type.Sliced;
                image.raycastTarget = false;
                segments[i].gameObject.SetActive(false);
            }
            return segments;
        }

        private void UpdateRadarTrack(float sin, float cos)
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
                        RadarPosition(radarTrack[i], sin, cos),
                        RadarPosition(radarTrack[i + 1], sin, cos));
            }
        }

        private void UpdateRadarPrediction()
        {
            if (radarPredictionSegments == null) return;
            Vector3 localVelocity = ship.transform.InverseTransformDirection(
                ship.Body.linearVelocity);
            float speed = Mathf.Max(0f, localVelocity.z);
            float sideSpeed = localVelocity.x;
            float rudderRadians = ship.RudderAngleDeg * Mathf.Deg2Rad;
            float yawRate = speed < 0.2f
                ? 0f
                : speed / 230f * Mathf.Tan(rudderRadians);
            Vector2 previous = new Vector2(0f, RadarVesselOffsetY);
            float predictedHeading = 0f;
            Vector2 predictedMeters = Vector2.zero;

            for (int i = 0; i < radarPredictionSegments.Length; i++)
            {
                float stepSeconds = 2.5f;
                predictedHeading += yawRate * stepSeconds;
                Vector2 forward = new Vector2(
                    Mathf.Sin(predictedHeading), Mathf.Cos(predictedHeading));
                Vector2 right = new Vector2(forward.y, -forward.x);
                predictedMeters +=
                    (forward * speed + right * sideSpeed) * stepSeconds;
                Vector2 next = predictedMeters * MapPixelsPerMeter +
                    new Vector2(0f, RadarVesselOffsetY);
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
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                ship.SetRudderCommand(-1f);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                ship.SetRudderCommand(1f);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.cKey.wasPressedThisFrame)
                ship.CenterRudder();
            if (keyboard.spaceKey.wasPressedThisFrame) SetTelegraph(3);
            if (keyboard.hKey.wasPressedThisFrame) PlayHorn();
            if (keyboard.mKey.wasPressedThisFrame)
            {
                mapVisible = !mapVisible;
                if (miniMap != null) miniMap.gameObject.SetActive(mapVisible);
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
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                helpVisible = !helpVisible;
                helpText.gameObject.SetActive(helpVisible);
            }
            Key[] viewKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
                Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };
            for (int i = 0; i < viewKeys.Length; i++)
                if (keyboard[viewKeys[i]].wasPressedThisFrame) SetCamera(i);
        }

        private void TelegraphUp() => SetTelegraph(Mathf.Min(6, telegraphIndex + 1));
        private void TelegraphDown() => SetTelegraph(Mathf.Max(0, telegraphIndex - 1));

        private void SetTelegraph(int index)
        {
            telegraphIndex = Mathf.Clamp(index, 0, TelegraphValues.Length - 1);
            ship.SetThrottleCommand(TelegraphValues[telegraphIndex]);
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
                timeScaleText.text = $"TIME {simulationTime.DisplayText}";
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
            float minimumAhead = radarMinAheadM > 0f
                ? Mathf.Min(currentDepth, radarMinAheadM)
                : currentDepth;
            depthRadarText.text =
                $"DEPTH <size=24><b>{currentDepth:F1} m</b></size>    " +
                $"<color=#8AA0AD>MIN AHEAD</color> <b>{minimumAhead:F1} m</b>    " +
                $"<color=#8AA0AD>DRAFT</color> {ship.EstimatedDraftM:F1} m\n" +
                "<size=13><b><color=#46A6BC>SAFE</color>   " +
                "<color=#D29B45>SHALLOW</color>   <color=#E04539>DANGER</color>   " +
                "<color=#E8B24D>ROUTE</color></b></size>\n" +
                "<size=12><color=#56C7E6>heading / predicted path</color>    " +
                "<color=#E86054>red mark</color>   " +
                "<color=#4DC76B>green mark</color></size>";
        }

        public static string FormatCameraStatus(string viewName, int viewIndex, int viewCount)
        {
            return $"CAMERA: <size=20><b>{viewName}</b></size>  " +
                $"<size=14>{viewIndex + 1}/{viewCount}</size>";
        }

        public static string FormatObjectiveStatus(float distanceMeters)
        {
            return "<size=14><color=#56C7E6>OBJECTIVE</color></size>\n" +
                "<size=23><b>Proceed to waypoint</b></size>\n\n" +
                $"<color=#8AA0AD>Distance</color>  <b>{distanceMeters:F0} m</b>\n" +
                "<color=#8AA0AD>Speed limit</color>  <b>8 km/h</b>";
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
            RectTransform rect = ImageRect(parent, name, color,
                new Vector2(0f, RadarVesselOffsetY), new Vector2(diameter, diameter));
            Image image = rect.GetComponent<Image>();
            image.sprite = HudTheme.Ring();
            image.raycastTarget = false;
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
            if (FindFirstObjectByType<EventSystem>() != null) return;
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
            RectTransform panel = SubPanel(parent, $"Instrument{index}",
                new Vector2(x + width * 0.5f, 0f), new Vector2(width - 6f, 86f));
            Icon(panel, icon, new Vector2(-width * 0.5f + 26f, 20f), 22f, HudTheme.AccentSoft);
            Text text = Label(panel, string.Empty, 16, TextAnchor.MiddleCenter,
                new Vector2(40f, 2f), new Vector2(-10f, -2f));
            text.supportRichText = true;
            text.lineSpacing = 0.92f;
            if (index < 3)
            {
                RectTransform divider = ImageRect(parent, $"Divider{index}",
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
                telegraphButtons[i].SetActive(i == telegraphIndex);

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
            RectTransform rect = ImageRect(parent, $"Icon_{icon}", color, position,
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
            label.fontSize = size;
            label.color = HudTheme.TextPrimary;
            label.alignment = alignment;
            label.text = value;
            label.supportRichText = true;
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
