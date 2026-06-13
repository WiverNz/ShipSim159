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

        private readonly Color panelColor = new Color(0.025f, 0.055f, 0.075f, 0.92f);
        private readonly Color accentColor = new Color(0.15f, 0.68f, 0.82f);
        private readonly Color warningColor = new Color(1f, 0.62f, 0.12f);
        private readonly Color activeColor = new Color(0.12f, 0.62f, 0.72f);
        private readonly Color idleColor = new Color(0.055f, 0.18f, 0.22f);
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
        private RectTransform rudderNeedle;
        private RectTransform rudderCommandNeedle;
        private RectTransform mapVessel;
        private RectTransform mapWaypoint;
        private RectTransform miniMap;
        private RectTransform warningPanel;
        private RectTransform mapWorld;
        private RectTransform[] radarDepthTiles;
        private RectTransform radarSweep;
        private RectTransform[] radarTrackSegments;
        private RectTransform[] radarPredictionSegments;
        private AudioSource hornSource;
        private DayNightController dayNight;
        private readonly List<MapContact> mapContacts = new List<MapContact>();
        private readonly List<MapLine> mapLines = new List<MapLine>();
        private readonly List<Vector3> radarTrack = new List<Vector3>();
        private readonly List<Image> telegraphButtons = new List<Image>();
        private readonly List<Image> cameraButtons = new List<Image>();
        private readonly List<Image> rudderButtons = new List<Image>();
        private int telegraphIndex = 3;
        private bool helpVisible;
        private bool mapVisible = true;
        private Vector3 objectivePosition = new Vector3(0f, 0f, 650f);
        private const float MapPixelsPerMeter = 0.82f;
        private const float RadarVesselOffsetY = -82f;
        private const int RadarColumns = 13;
        private const int RadarRows = 13;
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
            if (radarDepthTiles == null ||
                radarDepthTiles.Length != RadarColumns * RadarRows ||
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
            float channelDepth = FairwayModel.DepthAt(ship.transform.position);
            float underKeel = channelDepth - ship.EstimatedDraftM;
            Vector3 localVelocity = ship.transform.InverseTransformDirection(ship.Body.linearVelocity);
            Vector3 current = ship.EffectiveCurrentMps;

            float driftAngle = Mathf.Atan2(localVelocity.x,
                Mathf.Max(Mathf.Abs(localVelocity.z), 0.05f)) * Mathf.Rad2Deg;
            float engineLoad = Mathf.Abs(ship.ActualThrottle) * 100f;
            float rpm = engineLoad < 1f ? 0f : Mathf.Lerp(180f, 620f, engineLoad / 100f);
            speedText.text =
                $"SPEED\n<size=26><b>{speedMps * 1.943844f:F1} kn</b></size>\n" +
                $"<size=16>{speedMps * 3.6f:F1} km/h</size>";
            headingText.text =
                $"COURSE  <size=25><b>{heading:000} deg</b></size>\n" +
                $"<size=15>DRIFT {driftAngle:+0.0;-0.0;0.0} deg   SIDE {localVelocity.x:+0.0;-0.0;0.0} m/s</size>";
            depthText.text =
                $"DEPTH  <size=24><b>{channelDepth:F1} m</b></size>\n" +
                $"<size=15>UNDER KEEL {underKeel:F1} m</size>";
            rudderText.text =
                $"RUDDER  <size=23><b>{ship.RudderAngleDeg:+0.0;-0.0;0.0} deg</b></size>\n" +
                $"<size=14>COMMAND {ship.RudderCommand * 35f:+0;-0;0} deg</size>";
            engineText.text =
                $"TELEGRAPH  <size=23><b>{TelegraphNames[telegraphIndex]}</b></size>\n" +
                $"<size=15>RPM {rpm:F0}   ENGINE LOAD {engineLoad:F0}%</size>";
            currentText.text =
                $"CURRENT  <size=22><b>{current.magnitude:F1} m/s {CurrentArrow(current)}</b></size>\n" +
                $"<size=15>CARGO LOAD {ship.LoadFraction * 100f:F0}%</size>";
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
            objectiveText.text = FormatObjectiveStatus(distance);
            depthText.color = underKeel < 0.8f
                ? new Color(1f, 0.28f, 0.2f)
                : underKeel < 2f ? warningColor : Color.white;

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
                new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(1300f, 84f));
            speedText = CompactInstrument(instruments, 0, 205f);
            headingText = CompactInstrument(instruments, 1, 285f);
            depthText = CompactInstrument(instruments, 2, 255f);
            currentText = CompactInstrument(instruments, 3, 270f);

            RectTransform tape = Panel("HeadingTape",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(700f, 54f));
            headingTapeText = Label(tape, string.Empty, 20, TextAnchor.UpperCenter,
                new Vector2(8f, 16f), new Vector2(-8f, -3f));
            ImageRect(tape, "CourseMarkerStem", warningColor,
                new Vector2(0f, -15f), new Vector2(4f, 14f));
            ImageRect(tape, "CourseMarkerHead", warningColor,
                new Vector2(0f, -6f), new Vector2(14f, 5f));

            BuildRudderControls(transform as RectTransform);
            BuildTelegraph(transform as RectTransform);
            BuildCameraControls(transform as RectTransform);

            warningPanel = Panel("Warnings",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(620f, 54f));
            warningText = Label(warningPanel, string.Empty, 20, TextAnchor.MiddleCenter,
                new Vector2(12f, 4f), new Vector2(-12f, -4f));
            warningPanel.gameObject.SetActive(false);

            RectTransform objective = Panel("Objective",
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(22f, -116f), new Vector2(420f, 154f));
            objectiveText = Label(objective, string.Empty, 17, TextAnchor.UpperLeft,
                new Vector2(18f, 12f), new Vector2(-18f, -12f));
            objectiveText.supportRichText = true;

            BuildMiniMap();
            helpText = Label(transform, string.Empty, 18, TextAnchor.MiddleCenter,
                new Vector2(320f, 12f), new Vector2(-320f, -1016f));
            helpText.text =
                "A/D  RUDDER     W/S  TELEGRAPH     SPACE  STOP     1-9  CAMERAS\n" +
                "RMB  ORBIT     WHEEL  ZOOM     H  HORN     M  MAP     N  DAY/NIGHT     F1  CLOSE";
            helpText.gameObject.SetActive(false);
            Text helpPrompt = Label(transform, "F1  CONTROLS", 16, TextAnchor.LowerCenter,
                new Vector2(820f, 16f), new Vector2(-820f, -1034f));
            helpPrompt.color = accentColor;
            if (readout != null) readout.gameObject.SetActive(false);
        }

        private void BuildRudderControls(RectTransform parent)
        {
            RectTransform block = AnchoredPanel("RudderBlock", new Vector2(0f, 0f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(22f, 28f),
                new Vector2(460f, 154f));
            rudderText = Label(block, "RUDDER  0.0 deg", 19, TextAnchor.UpperCenter,
                new Vector2(8f, 108f), new Vector2(-8f, -8f));
            RectTransform dial = ImageRect(block, "RudderScale", new Color(0.04f, 0.09f, 0.12f),
                new Vector2(0f, 12f), new Vector2(350f, 32f));
            Label(dial, "PORT 35     20     10      0      10     20     STBD 35",
                14, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
            rudderNeedle = ImageRect(dial, "Needle", warningColor,
                new Vector2(0f, -4f), new Vector2(5f, 36f));
            rudderCommandNeedle = ImageRect(dial, "CommandNeedle", accentColor,
                new Vector2(0f, 9f), new Vector2(9f, 6f));
            rudderButtons.Add(Button(block, "PORT  [A]", new Vector2(-118f, -43f),
                new Vector2(108f, 38f), () => ship.SetRudderCommand(-1f)).image);
            rudderButtons.Add(Button(block, "MIDSHIPS [C]", new Vector2(0f, -43f),
                new Vector2(122f, 38f), ship.CenterRudder).image);
            rudderButtons.Add(Button(block, "STBD  [D]", new Vector2(118f, -43f),
                new Vector2(108f, 38f), () => ship.SetRudderCommand(1f)).image);
        }

        private void BuildTelegraph(RectTransform parent)
        {
            RectTransform block = AnchoredPanel("TelegraphBlock", new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f),
                new Vector2(680f, 154f));
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
                Button button = Button(block, labels[i], new Vector2(-270f + i * 90f, -37f),
                    new Vector2(84f, 48f), () => SetTelegraph(command));
                telegraphButtons.Add(button.image);
            }
        }

        private void BuildCameraControls(RectTransform parent)
        {
            RectTransform block = AnchoredPanel("CameraBlock", new Vector2(1f, 0f),
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 28f),
                new Vector2(350f, 128f));
            block.GetComponent<Image>().color = new Color(0.025f, 0.055f, 0.075f, 0.82f);
            cameraText = Label(block, "CAMERA", 18, TextAnchor.UpperCenter,
                new Vector2(8f, 70f), new Vector2(-8f, -8f));
            cameraButtons.Add(Button(block, "PREV  [V]", new Vector2(-112f, -38f),
                new Vector2(104f, 38f), PreviousCamera).image);
            cameraButtons.Add(Button(block, "NEXT  [V]", new Vector2(0f, -38f),
                new Vector2(104f, 38f), NextCamera).image);
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

        private void BuildMiniMap()
        {
            mapContacts.Clear();
            mapLines.Clear();
            radarTrack.Clear();
            miniMap = Panel("MiniMap",
                new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(420f, 420f));
            Label(miniMap, "RIVER RADAR / DEPTH     HEAD UP     RANGE 200 m",
                18, TextAnchor.UpperLeft, new Vector2(16f, 378f), new Vector2(-16f, -8f));
            RectTransform viewport = ImageRect(miniMap, "Viewport", new Color(0.08f, 0.1f, 0.1f),
                new Vector2(0f, -18f), new Vector2(382f, 332f));
            viewport.gameObject.AddComponent<RectMask2D>();
            mapWorld = ImageRect(viewport, "MovingWorld", Color.clear, Vector2.zero,
                new Vector2(382f, 332f));

            radarDepthTiles = new RectTransform[RadarColumns * RadarRows];
            for (int row = 0; row < RadarRows; row++)
            for (int column = 0; column < RadarColumns; column++)
            {
                int index = row * RadarColumns + column;
                radarDepthTiles[index] = ImageRect(mapWorld, $"DepthCell{index}",
                    new Color(0.04f, 0.22f, 0.28f),
                    new Vector2(-168f + column * 28f, -140f + row * 25f),
                    new Vector2(29f, 26f));
            }

            for (int i = 0; i < 21; i++)
            {
                float startZ = -180f + i * 48f;
                float endZ = startZ + 32f;
                Vector3 routeStart = FairwayPoint(startZ, 0f);
                Vector3 routeEnd = FairwayPoint(endZ, 0f);
                AddMapLine($"FairwayRoute{i}", warningColor,
                    routeStart, routeEnd, 3f);
                AddMapLine($"PortEdge{i}", new Color(1f, 0.3f, 0.12f, 0.48f),
                    FairwayPoint(startZ, -1f), FairwayPoint(endZ, -1f), 2f);
                AddMapLine($"StarboardEdge{i}", new Color(0.2f, 1f, 0.35f, 0.48f),
                    FairwayPoint(startZ, 1f), FairwayPoint(endZ, 1f), 2f);
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
                AddMapContact($"LeftWhiteBuoy{i}", new Color(0.88f, 0.94f, 0.88f),
                    new Vector3(centerX + normal.x * width, 0f, z + normal.y * width),
                    new Vector2(10f, 10f));
                AddMapContact($"RightRedBuoy{i}", new Color(1f, 0.12f, 0.08f),
                    new Vector3(centerX - normal.x * width, 0f, z - normal.y * width),
                    new Vector2(10f, 10f));
            }
            mapWaypoint = AddMapContact("Waypoint", warningColor,
                objectivePosition, new Vector2(20f, 20f));
            ImageRect(mapWaypoint, "WaypointCenter", new Color(0.08f, 0.1f, 0.1f),
                Vector2.zero, new Vector2(8f, 8f));
            mapVessel = ImageRect(viewport, "Vessel", new Color(1f, 0.78f, 0.18f),
                new Vector2(0f, RadarVesselOffsetY), new Vector2(15f, 34f));
            ImageRect(mapVessel, "Bow", Color.white,
                new Vector2(0f, 13f), new Vector2(8f, 8f));

            ImageRect(viewport, "HorizontalGrid", new Color(0.3f, 0.72f, 0.74f, 0.28f),
                new Vector2(0f, RadarVesselOffsetY), new Vector2(382f, 2f));
            for (int i = 0; i < 8; i++)
                ImageRect(viewport, $"HeadingDash{i}", new Color(0.28f, 0.9f, 1f, 0.8f),
                    new Vector2(0f, RadarVesselOffsetY + 20f + i * 24f),
                    new Vector2(2f, 13f));
            RadarFrame(viewport, "Range100m", new Vector2(0f, RadarVesselOffsetY + 82f),
                new Vector2(164f, 164f), new Color(0.3f, 0.72f, 0.74f, 0.22f));
            RadarFrame(viewport, "Range200m", new Vector2(0f, RadarVesselOffsetY + 164f),
                new Vector2(328f, 328f), new Color(0.3f, 0.72f, 0.74f, 0.18f));
            radarSweep = ImageRect(viewport, "RadarSweep",
                new Color(0.3f, 1f, 0.66f, 0.38f),
                new Vector2(0f, RadarVesselOffsetY + 74f), new Vector2(2f, 148f));
            radarSweep.pivot = new Vector2(0.5f, 0f);

            radarTrackSegments = BuildRadarSegments(viewport, "Track",
                RadarTrackPointCount - 1, new Color(0.82f, 0.85f, 0.86f, 0.62f), 2f);
            radarPredictionSegments = BuildRadarSegments(viewport, "Prediction",
                RadarPredictionPointCount - 1, new Color(0.93f, 0.96f, 1f, 0.82f), 2f);

            depthRadarText = Label(miniMap, string.Empty, 16, TextAnchor.LowerLeft,
                new Vector2(16f, 10f), new Vector2(-16f, -385f));
            depthRadarText.supportRichText = true;
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
            UpdateRadarTrack(sin, cos);
            UpdateRadarPrediction();
            mapVessel.anchoredPosition = new Vector2(0f, RadarVesselOffsetY);
            mapVessel.localRotation = Quaternion.identity;
            if (radarSweep != null)
                radarSweep.localRotation = Quaternion.Euler(
                    0f, 0f, -Time.unscaledTime * 38f);
        }

        private void UpdateWarnings(float speedMps, float underKeel, float currentSpeed)
        {
            string warning = string.Empty;
            if (underKeel < 0.8f) warning += "SHALLOW WATER\n";
            if (Mathf.Abs(FairwayModel.LateralOffset(
                    ship.transform.position.x, ship.transform.position.z)) >
                FairwayModel.MarkedHalfWidth(ship.transform.position.z))
                warning += "OUTSIDE FAIRWAY\n";
            if (ship.Data != null && speedMps > ship.Data.controlLimits.maxLoadedSpeedMps * 1.05f)
                warning += "OVERSPEED\n";
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
            if (warningPanel != null) warningPanel.gameObject.SetActive(hasWarning);
            warningText.text = hasWarning ? "WARNING  |  " + warning.TrimEnd().Replace("\n", "  |  ") : string.Empty;
            warningText.color = warningColor;
        }

        private RectTransform AddMapContact(
            string name, Color color, Vector3 worldPosition, Vector2 size)
        {
            RectTransform rect = ImageRect(mapWorld, name, color, Vector2.zero, size);
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

        private void UpdateDepthRadar(float currentDepth)
        {
            if (depthRadarText == null || radarDepthTiles == null) return;
            float minimumAhead = currentDepth;
            for (int row = 0; row < RadarRows; row++)
            for (int column = 0; column < RadarColumns; column++)
            {
                int index = row * RadarColumns + column;
                float localX = (column - (RadarColumns - 1) * 0.5f) * 34f;
                float localZ = (row - 3.3f) * 30.5f;
                Vector3 samplePosition = ship.transform.position +
                    ship.transform.right * localX + ship.transform.forward * localZ;
                float depth = FairwayModel.DepthAt(samplePosition);
                if (localZ >= 0f && Mathf.Abs(localX) < 22f)
                    minimumAhead = Mathf.Min(minimumAhead, depth);
                RectTransform tile = radarDepthTiles[index];
                if (tile != null)
                    tile.GetComponent<Image>().color = DepthChartColor(depth);
            }
            depthRadarText.text =
                $"DEPTH <size=23><b>{currentDepth:F1} m</b></size>   " +
                $"MIN AHEAD <b>{minimumAhead:F1} m</b>   DRAFT {ship.EstimatedDraftM:F1} m\n" +
                "<size=12><color=#52DCEC>CYAN: HEADING</color>  " +
                "<color=#FFB52E>YELLOW: ROUTE</color>  <color=#D0D6D8>GRAY: TRACK</color>\n" +
                "<color=#EDF5FF>WHITE: PREDICTED PATH</color>  " +
                "<color=#17343B>DEEP</color>  <color=#1F7180>SAFE</color>  " +
                "<color=#C58B22>CAUTION</color>  <color=#C73522>SHALLOW</color></size>";
        }

        public static string FormatCameraStatus(string viewName, int viewIndex, int viewCount)
        {
            return $"CAMERA: <size=20><b>{viewName}</b></size>  " +
                $"<size=14>{viewIndex + 1}/{viewCount}</size>";
        }

        public static string FormatObjectiveStatus(float distanceMeters)
        {
            return "<size=15><color=#50CBE1>OBJECTIVE</color></size>\n" +
                "<size=20><b>Proceed to training waypoint</b></size>\n\n" +
                $"Distance: <b>{distanceMeters:F0} m</b>\n" +
                "Speed limit: <b>max 8 km/h</b>";
        }

        private Color DepthChartColor(float depth)
        {
            float clearance = depth - ship.EstimatedDraftM;
            if (clearance < 0.8f) return new Color(0.78f, 0.12f, 0.07f, 0.94f);
            if (clearance < 2f) return new Color(0.72f, 0.43f, 0.08f, 0.92f);
            if (depth < 6f) return new Color(0.07f, 0.36f, 0.42f, 0.9f);
            return new Color(0.025f, 0.16f, 0.22f, 0.9f);
        }

        private void RadarFrame(
            Transform parent, string name, Vector2 center, Vector2 size, Color color)
        {
            RectTransform frame = ImageRect(parent, name, Color.clear, center, size);
            ImageRect(frame, "Top", color,
                new Vector2(0f, size.y * 0.5f), new Vector2(size.x, 2f));
            ImageRect(frame, "Bottom", color,
                new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, 2f));
            ImageRect(frame, "Left", color,
                new Vector2(-size.x * 0.5f, 0f), new Vector2(2f, size.y));
            ImageRect(frame, "Right", color,
                new Vector2(size.x * 0.5f, 0f), new Vector2(2f, size.y));
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

        private Text Instrument(RectTransform parent, string name, int index)
        {
            float x = -337.5f + index * 225f;
            RectTransform panel = SubPanel(parent, name, new Vector2(x, 0f), new Vector2(210f, 112f));
            return Label(panel, name.ToUpperInvariant(), 21, TextAnchor.MiddleCenter,
                new Vector2(6f, 6f), new Vector2(-6f, -6f));
        }

        private Text CompactInstrument(RectTransform parent, int index, float width)
        {
            float[] widths = { 205f, 285f, 255f, 270f };
            float start = -485f;
            float x = start;
            for (int i = 0; i < index; i++) x += widths[i];
            RectTransform panel = SubPanel(parent, $"Instrument{index}",
                new Vector2(x + width * 0.5f, 0f), new Vector2(width - 5f, 72f));
            Text text = Label(panel, string.Empty, 17, TextAnchor.MiddleCenter,
                new Vector2(6f, 2f), new Vector2(-6f, -2f));
            text.supportRichText = true;
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
                SetButtonState(telegraphButtons[i], i == telegraphIndex);

            int rudderState = ship.RudderCommand < -0.1f ? 0 :
                ship.RudderCommand > 0.1f ? 2 : 1;
            for (int i = 0; i < rudderButtons.Count; i++)
                SetButtonState(rudderButtons[i], i == rudderState);
        }

        private void SetButtonState(Image image, bool active)
        {
            image.color = active ? activeColor : idleColor;
            Text label = image.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.color = active ? Color.white : new Color(0.72f, 0.84f, 0.87f);
                label.fontStyle = active ? FontStyle.Bold : FontStyle.Normal;
            }
            image.rectTransform.localScale = active ? Vector3.one * 1.06f : Vector3.one;
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
            GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = panelColor;
            return rect;
        }

        private RectTransform SubPanel(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rect = ImageRect(parent, name, new Color(0.02f, 0.08f, 0.11f, 0.9f),
                position, size);
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
            label.color = Color.white;
            label.alignment = alignment;
            label.text = value;
            return label;
        }

        private Button Button(Transform parent, string caption, Vector2 position,
            Vector2 size, Action action)
        {
            GameObject buttonObject = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.25f, 0.31f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Text text = Label(buttonObject.transform, caption, 17, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.zero);
            text.raycastTarget = false;
            return button;
        }
    }
}
