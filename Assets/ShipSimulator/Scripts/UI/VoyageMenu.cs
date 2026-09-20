using System;
using System.Collections;
using System.IO;
using ShipSimulator.CameraSystem;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShipSimulator.UI
{
    [DefaultExecutionOrder(-1000)]
    public sealed class VoyageMenu : MonoBehaviour
    {
        private static VoyageMenu instance;
        private static bool warnedAboutScene;
        public static bool IsOpen => instance != null && instance.open;
        public bool HasVoyage => hasVoyage;
        public bool IsReady => !loading;
        public string SavePath { get; set; }
        private bool open;
        private bool hasVoyage;
        private bool loading;
        private bool launchAfterLoad;
        private bool canStartCurrent;
        private float resumeScale = 1f;
        private VoyageSave pendingSave;
        private VesselCatalogue catalogue;
        private Text vesselNameLabel;
        private Text vesselLengthLabel;
        private Text vesselClassLabel;
        private string projectName = "507B";
        private GameObject overlay;
        private RectTransform pageRoot;
        private Text heading;
        private Text eyebrow;
        private Text status;
        private Font font;
        private string page = "home";
        private CanvasGroup hud;
        private float hudAlpha;
        private bool hudInteractable;
        private bool hudRaycasts;
        private static readonly Color Navy = new Color(0.027f, 0.071f, 0.10f);
        private static readonly Color Brass = new Color(0.87f, 0.73f, 0.47f);
        private static readonly Color Muted = new Color(0.52f, 0.67f, 0.70f);
        private static readonly Color White = new Color(0.92f, 0.94f, 0.90f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            warnedAboutScene = false;
            SceneManager.sceneLoaded -= Bootstrap;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install() => SceneManager.sceneLoaded += Bootstrap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OpenInitialMenu()
        {
            if (instance == null) Bootstrap(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void Bootstrap(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (!VoyageSave.IsVoyageScene(scene.name))
            {
                // Silence here reads as a broken build: no menu, no vessel, no HUD, just a black screen.
                if (!warnedAboutScene)
                {
                    warnedAboutScene = true;
                    Debug.LogWarning("VoyageMenu: active scene '" +
                        (string.IsNullOrEmpty(scene.name) ? "untitled" : scene.name) +
                        "' is not a voyage scene, so the menu stays closed. Open RiverTrainingScene or " +
                        "GorodetsTrainingScene before entering Play Mode.");
                }
                return;
            }
            warnedAboutScene = false;
            if (instance == null) new GameObject("Voyage Menu").AddComponent<VoyageMenu>();
            instance.BindScene();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            SavePath = VoyageSaveStore.DefaultPath;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            VoyageSettings.Apply();
            catalogue = VesselCatalogue.Load();
            BuildShell();
            UpdateVesselLabels();
            overlay.SetActive(false);
        }

        public void BindScene()
        {
            loading = true;
            VesselSelection.SelectedId = VoyagePassage.FirstAcceptedVesselId(
                catalogue, SceneManager.GetActiveScene().name, VesselSelection.SelectedId);
            VesselSwap.Apply(catalogue, VesselSelection.SelectedId);
            UpdateVesselLabels();
            canStartCurrent = pendingSave == null && !launchAfterLoad;
            hud = null;
            open = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            overlay.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            StartCoroutine(FinishBinding());
        }

        private IEnumerator FinishBinding()
        {
            // HUD and environment components are created by the scene's Start methods.
            yield return null;
            ShipTelemetryUI telemetry = FindAnyObjectByType<ShipTelemetryUI>();
            if (telemetry != null)
            {
                hud = telemetry.GetComponent<CanvasGroup>();
                if (hud == null) hud = telemetry.gameObject.AddComponent<CanvasGroup>();
                hudAlpha = hud.alpha;
                hudInteractable = hud.interactable;
                hudRaycasts = hud.blocksRaycasts;
                HideHud();
            }
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            loading = false;
            if (pendingSave != null)
            {
                try
                {
                    ApplySave(pendingSave);
                    pendingSave = null;
                    hasVoyage = true;
                    Resume();
                }
                catch (Exception error)
                {
                    pendingSave = null;
                    hasVoyage = false;
                    ShowHome();
                    status.text = "Could not restore voyage: " + error.Message;
                }
            }
            else if (launchAfterLoad)
            {
                launchAfterLoad = false;
                resumeScale = 1f;
                hasVoyage = true;
                Resume();
            }
            else
            {
                hasVoyage = false;
                ShowHome();
            }
        }

        private void Update()
        {
            if (loading) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                HandleEscape();
        }

        public void HandleEscape()
        {
            if (loading) return;
            if (!open) OpenPause();
            else if (page != "home") ShowHome();
            else if (hasVoyage) Resume();
        }

        public void OpenPause()
        {
            if (open || !hasVoyage) return;
            resumeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            open = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            overlay.SetActive(true);
            HideHud();
            ShowHome();
        }

        public void Resume()
        {
            if (!hasVoyage || loading) return;
            PlayerPrefs.Save();
            open = false;
            overlay.SetActive(false);
            if (hud != null)
            {
                hud.alpha = hudAlpha;
                hud.interactable = hudInteractable;
                hud.blocksRaycasts = hudRaycasts;
            }
            Time.timeScale = resumeScale;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void HideHud()
        {
            if (hud == null) return;
            hud.alpha = 0f;
            hud.interactable = false;
            hud.blocksRaycasts = false;
        }

        public void StartVoyage(string scene)
        {
            if (!VoyageSave.IsVoyageScene(scene) || loading) return;
            // A different vessel needs a fresh scene, because the HUD is already built for this one.
            if (canStartCurrent && !hasVoyage && SceneManager.GetActiveScene().name == scene &&
                VesselSwap.IdOf(FindAnyObjectByType<ShipPhysicsController>()) == VesselSelection.SelectedId)
            {
                canStartCurrent = false;
                hasVoyage = true;
                resumeScale = 1f;
                Resume();
                return;
            }
            launchAfterLoad = true;
            LoadScene(scene);
        }

        private void LoadScene(string scene)
        {
            loading = true;
            ShowPage("loading", "PREPARING YOUR PASSAGE", "Stand by");
            Label(pageRoot, "Loading vessel and river conditions...", 21, Muted, 0, 20, 440, 90);
            try { SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single); }
            catch (Exception error)
            {
                loading = false;
                launchAfterLoad = false;
                pendingSave = null;
                ShowHome();
                status.text = "Could not open scenario: " + error.Message;
            }
        }

        public void LoadVoyage()
        {
            if (loading) return;
            try
            {
                pendingSave = VoyageSaveStore.Read(SavePath);
                VesselSelection.SelectedId = pendingSave.VesselIdOrDefault;
                UpdateVesselLabels();
                LoadScene(pendingSave.scene);
            }
            catch (Exception error)
            {
                pendingSave = null;
                status.text = "Could not load voyage: " + error.Message;
            }
        }

        public void SaveVoyage()
        {
            if (!hasVoyage || loading) return;
            try
            {
                VoyageSaveStore.Write(SavePath, CaptureSave());
                ShowHome();
                status.text = "Voyage saved. Safe to leave the bridge.";
            }
            catch (Exception error) { status.text = "Could not save voyage: " + error.Message; }
        }

        public VoyageSave CaptureSave()
        {
            ShipPhysicsController ship = FindAnyObjectByType<ShipPhysicsController>();
            if (ship == null || ship.Body == null || !ship.enabled)
                throw new InvalidOperationException("The vessel is not ready.");
            WeatherController weather = FindAnyObjectByType<WeatherController>();
            var save = new VoyageSave
            {
                scene = ship.gameObject.scene.name, savedUtc = DateTime.UtcNow.ToString("O"),
                vesselId = VesselSwap.IdOf(ship),
                position = ship.Body.position, rotation = ship.Body.rotation.normalized,
                velocity = ship.Body.linearVelocity, angularVelocity = ship.Body.angularVelocity,
                throttle = ship.ThrottleCommand, actualThrottle = ship.ActualThrottle,
                rudder = ship.RudderCommand, rudderAngle = ship.RudderAngleDeg,
                engineCommands = ship.CaptureEngineCommands(), shaftRps = ship.CaptureShaftRps(),
                bowThrusterCommand = ship.BowThrusterCommand, bowThrusterOutput = ship.BowThrusterOutput,
                simulationScale = open ? resumeScale : Time.timeScale,
                night = FindAnyObjectByType<DayNightController>()?.IsNight ?? false,
                windDirection = weather?.WindDirectionDeg ?? 0f, windSpeed = weather?.WindSpeedMps ?? 0f,
                rain = weather?.RainIntensity ?? 0f, fog = weather?.FogIntensity ?? 0f,
                waterLevel = FindAnyObjectByType<ScenarioBathymetry>()?.WaterLevelOffsetM ?? 0f,
                discharge = FindAnyObjectByType<CurrentFieldProvider>()?.DischargeMultiplier ?? 1f,
                camera = FindAnyObjectByType<ShipFollowCamera>()?.CaptureState() ?? new CameraSave(),
                mission = FindAnyObjectByType<GorodetsScenarioController>()?.CaptureState() ?? new MissionSave(),
                grounding = ship.Grounding?.CaptureState() ?? new GroundingSave()
            };
            save.Validate();
            return save;
        }

        public void ApplySave(VoyageSave save)
        {
            save.Validate();
            ShipPhysicsController ship = FindAnyObjectByType<ShipPhysicsController>();
            if (ship == null || !ship.enabled || ship.gameObject.scene.name != save.scene)
                throw new InvalidOperationException("The saved vessel's scenario is not loaded.");
            if (VesselSwap.IdOf(ship) != save.VesselIdOrDefault)
                throw new InvalidOperationException("The saved vessel is not the one in this scenario.");
            ship.RestoreVoyage(save);
            ship.Grounding?.RestoreState(save.grounding);
            FindAnyObjectByType<GorodetsScenarioController>()?.RestoreState(save.mission);
            FindAnyObjectByType<WeatherController>()?.Configure(save.windDirection, save.windSpeed, save.rain, save.fog);
            FindAnyObjectByType<DayNightController>()?.Apply(save.night);
            FindAnyObjectByType<ScenarioBathymetry>()?.SetWaterLevelOffset(save.waterLevel);
            FindAnyObjectByType<CurrentFieldProvider>()?.SetDischargeMultiplier(save.discharge);
            FindAnyObjectByType<ShipFollowCamera>()?.RestoreState(save.camera);
            FindAnyObjectByType<ShipTelemetryUI>()?.RefreshAfterVoyageLoad();
            FindAnyObjectByType<SimulationTimeController>()?.SetScale(save.simulationScale);
            resumeScale = save.simulationScale;
        }

        public void ShowHome()
        {
            ShowPage("home", hasVoyage ? "VESSEL ON STANDBY" : "WELCOME ABOARD", hasVoyage ? "At anchor" : "Your next passage");
            float y = 0;
            if (hasVoyage)
            {
                ActionButton("Resume voyage", "Return to the bridge  /  ESC", y, Resume, true); y += 80;
                ActionButton("Save voyage", "Record your current passage", y, () =>
                {
                    if (File.Exists(SavePath)) Confirm("Replace saved voyage?", "Your previous saved passage will be replaced.", SaveVoyage);
                    else SaveVoyage();
                }); y += 80;
            }
            else
            {
                ActionButton("New voyage", "Choose a vessel and a river passage", y, ShowVessels, true); y += 80;
            }
            bool exists = File.Exists(SavePath);
            string savedDescription = "No saved voyage yet";
            bool valid = false;
            if (exists)
            {
                try
                {
                    VoyageSave save = VoyageSaveStore.Read(SavePath);
                    savedDescription = ScenarioName(save.scene) + "  /  " + DateTime.Parse(save.savedUtc).ToLocalTime().ToString("dd MMM, HH:mm");
                    valid = true;
                }
                catch (Exception) { savedDescription = "Save unavailable. Start a new voyage to continue."; }
            }
            ActionButton(hasVoyage ? "Load saved voyage" : "Continue voyage", savedDescription, y,
                () => { if (hasVoyage) Confirm("Load saved voyage?", "Unsaved progress in this passage will be lost.", LoadVoyage); else LoadVoyage(); }, false, valid); y += 80;
            if (hasVoyage) { ActionButton("New voyage", "Choose a different vessel or passage", y, () => Confirm("Leave this passage?", "Save first if you want to keep your current progress.", ShowVessels)); y += 80; }
            ActionButton("Settings", "Display, audio & camera", y, ShowSettings); y += 80;
            ActionButton("Leave the bridge", Application.isEditor ? "Exit Play Mode" : "Quit to desktop", y,
                () => Confirm("Leave the bridge?", hasVoyage ? "Unsaved progress will be lost. You can go back and save." : "Your saved voyage will be kept for your return.", Quit));
            SelectFirst();
        }

        public void ShowVessels()
        {
            if (catalogue == null || catalogue.Entries.Count == 0) { ShowVoyages(); return; }
            ShowPage("vessels", "CHOOSE YOUR COMMAND", "Select a vessel");
            float y = 0;
            foreach (VesselCatalogue.Entry entry in catalogue.Entries)
            {
                VesselData data = entry.LoadData();
                string id = entry.id;
                ActionButton(data != null ? data.identity.displayName : id, VesselCatalogue.Entry.Describe(data), y, () =>
                {
                    VesselSelection.SelectedId = id;
                    UpdateVesselLabels();
                    ShowVoyages();
                }, id == VesselSelection.SelectedId);
                y += 94;
            }
            ActionButton("Back", "Return to the menu", 462, ShowHome);
            SelectFirst();
        }

        public void ShowVoyages()
        {
            ShowPage("voyages", "CHART YOUR COURSE", "Select a passage");
            VesselCatalogue.Entry vessel = SelectedVessel();
            VesselData data = vessel?.LoadData();
            float y = 0;
            bool first = true;
            foreach (VoyagePassage passage in VoyagePassage.All)
            {
                string restriction = passage.Restriction(data);
                string scene = passage.SceneName;
                ActionButton(passage.Title, restriction ?? passage.Detail, y,
                    () => StartVoyage(scene), first && restriction == null, restriction == null);
                first &= restriction != null;
                y += 88;
            }
            string vesselText = data != null
                ? data.identity.displayName + "\n" + VesselCatalogue.Entry.Describe(data)
                : "Volgo-Don Project 507B";
            Label(pageRoot, "YOUR VESSEL\n\n" + vesselText + "\n\nW / S  Engines     A / D  Rudder\nV  Camera     Right mouse  Look around\nESC  Pause, settings & save", 18, Muted, 0, y + 10, 450, 180);
            ActionButton("Back", "Choose another vessel", 462, ShowVessels);
            SelectFirst();
        }

        private VesselCatalogue.Entry SelectedVessel() => catalogue != null ? catalogue.Find(VesselSelection.SelectedId) : null;

        private void UpdateVesselLabels()
        {
            VesselCatalogue.Entry vessel = SelectedVessel();
            VesselData data = vessel?.LoadData();
            if (vesselNameLabel != null) vesselNameLabel.text = vessel != null ? vessel.menuName : "VOLGO-DON\n507B";
            if (vesselClassLabel != null) vesselClassLabel.text = vessel != null ? vessel.vesselClass : "RIVER CLASS\nCARGO VESSEL";
            if (vesselLengthLabel != null)
                vesselLengthLabel.text = (data != null ? data.dimensions.lengthOverallM.ToString("0.0") : "138.3") + " m\nLENGTH OVERALL";
            projectName = data != null ? data.identity.project : "507B";
            if (status != null && !hasVoyage && !loading)
                status.text = "PROJECT " + projectName + "  /  RIVER NAVIGATION SIMULATOR";
        }

        public void ShowSettings()
        {
            ShowPage("settings", "MAKE YOURSELF AT HOME", "Bridge settings");
            SettingSlider("Master volume", VoyageSettings.Volume, 0f, 1f, 0, VoyageSettings.SetVolume, value => Mathf.RoundToInt(value * 100) + "%");
            SettingSlider("Camera sensitivity", VoyageSettings.CameraSensitivity, 0.25f, 2f, 92, VoyageSettings.SetSensitivity, value => value.ToString("0.00") + "x");
            ActionButton("Graphics  /  " + QualitySettings.names[QualitySettings.GetQualityLevel()], "Select the next quality preset", 200, () => { VoyageSettings.CycleQuality(); ShowSettings(); });
            ActionButton("VSync  /  " + (QualitySettings.vSyncCount > 0 ? "On" : "Off"), "Synchronise frames with your display", 280, () => { VoyageSettings.ToggleVSync(); ShowSettings(); });
            ActionButton("Display  /  " + (Screen.fullScreen ? "Fullscreen" : "Windowed"), Application.isEditor ? "Available in the standalone player" : "Toggle fullscreen display", 360,
                () => { VoyageSettings.ToggleFullscreen(); StartCoroutine(RefreshDisplay()); }, false, !Application.isEditor);
            ActionButton("Done", "Settings are saved automatically", 462, () => { PlayerPrefs.Save(); ShowHome(); }, true);
            SelectFirst();
        }

        private IEnumerator RefreshDisplay() { yield return null; ShowSettings(); }

        private void Confirm(string title, string description, Action action)
        {
            ShowPage("confirm", "BEFORE YOU CONTINUE", title);
            Label(pageRoot, description, 23, Muted, 0, 6, 440, 130);
            ActionButton("Go back", "Keep this passage", 170, ShowHome, true);
            ActionButton("Confirm", "Continue with this action", 260, action);
            SelectFirst();
        }

        private void ShowPage(string name, string kicker, string title)
        {
            page = name;
            foreach (Transform child in pageRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            eyebrow.text = kicker;
            heading.text = title;
            status.text = hasVoyage ? "PASSAGE PAUSED  /  Your vessel is holding position." : "PROJECT " + projectName + "  /  RIVER NAVIGATION SIMULATOR";
        }

        private void BuildShell()
        {
            overlay = new GameObject("Maritime Menu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform, false);
            Canvas canvas = overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var fill = new GameObject("Ocean", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(overlay.transform, false);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one; fillRect.sizeDelta = Vector2.zero;
            fill.GetComponent<Image>().color = Navy;
            RectTransform layout = Rect(overlay.transform, "Bridge layout", 0, 0, 1440, 900);
            layout.anchorMin = layout.anchorMax = layout.pivot = new Vector2(0.5f, 0.5f);
            layout.anchoredPosition = Vector2.zero;
            RectTransform chart = Rect(layout, "River chart", 0, 0, 820, 900);
            chart.gameObject.AddComponent<NauticalChartGraphic>().raycastTarget = false;
            Box(layout, "Panel", 820, 0, 620, 900, new Color(0.035f, 0.085f, 0.115f));
            Box(layout, "Brass rule", 820, 60, 2, 780, new Color(Brass.r, Brass.g, Brass.b, 0.4f));
            Label(layout, "S H I P S I M   /   1 5 9", 20, Brass, 64, 55, 680, 35);
            Label(layout, "INLAND\nPASSAGES", 76, White, 60, 117, 700, 184, FontStyle.Bold);
            Label(layout, "Take the helm. Read the river.", 25, Muted, 65, 313, 680, 45);
            Label(layout, "N", 22, Brass, 382, 354, 30, 34);
            vesselNameLabel = Label(layout, "VOLGO-DON\n507B", 18, Brass, 442, 487, 210,  60);
            Box(layout, "Vessel rule", 64, 749, 685, 1, Muted * new Color(1, 1, 1, 0.4f));
            vesselLengthLabel = Label(layout, "138.3 m\nLENGTH OVERALL", 19, White, 64, 771, 210, 65);
            vesselClassLabel = Label(layout, "RIVER CLASS\nCARGO VESSEL", 19, White, 318, 771, 210, 65);
            Label(layout, "YOUR BRIDGE.\nYOUR PASSAGE.", 19, Brass, 576, 771, 210, 65);
            Label(layout, "Engineering & gameplay prototype. Estimated navigation data.", 14, Muted, 64, 851, 720, 25);
            eyebrow = Label(layout, "", 16, Brass, 885, 64, 495, 30);
            heading = Label(layout, "", 38, White, 882, 108, 500, 64, FontStyle.Bold);
            pageRoot = Rect(layout, "Menu page", 885, 206, 480, 560);
            status = Label(layout, "", 16, Muted, 885, 787, 478, 72);
            Text version = Label(layout, "v" + Application.version, 14, Muted, 885, 859, 478, 25);
            version.name = "Game version";
            version.alignment = TextAnchor.MiddleRight;
        }

        private Button ActionButton(string title, string detail, float y, Action action, bool primary = false, bool enabled = true)
        {
            RectTransform rect = Rect(pageRoot, title, 0, y, 478, 70);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = HudTheme.Rounded(8); image.type = Image.Type.Sliced;
            image.color = primary ? Brass : new Color(0.075f, 0.145f, 0.175f);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.17f, 1.17f, 1.17f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.7f, 0.85f, 0.88f);
            colors.disabledColor = new Color(0.48f, 0.48f, 0.48f, 0.65f);
            button.colors = colors;
            Label(rect, title, 22, primary ? Navy : enabled ? White : Muted, 20, 9, 425, 29, FontStyle.Bold);
            Label(rect, detail, 15, primary ? Navy : Muted, 20, 39, 425, 24);
            button.onClick.AddListener(() => action());
            return button;
        }

        private void SettingSlider(string title, float value, float min, float max, float y, Action<float> setter, Func<float, string> format)
        {
            Label(pageRoot, title, 22, White, 0, y, 330, 32);
            Text number = Label(pageRoot, format(value), 20, Brass, 360, y, 116, 32);
            number.alignment = TextAnchor.MiddleRight;
            RectTransform control = Rect(pageRoot, title + " slider", 0, y + 37, 478, 32);
            control.gameObject.AddComponent<Image>().color = Color.clear;
            Slider slider = control.gameObject.AddComponent<Slider>();
            slider.minValue = min; slider.maxValue = max;
            Box(control, "Track", 0, 13, 478, 5, new Color(0.16f, 0.26f, 0.28f));
            RectTransform area = Rect(control, "Handle area", 10, 0, 458, 32);
            RectTransform handle = Rect(area, "Handle", 0, 0, 20, 0);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = HudTheme.Rounded(6); handleImage.type = Image.Type.Sliced; handleImage.color = Brass;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.value = value;
            slider.onValueChanged.AddListener(next => { setter(next); number.text = format(next); });
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static void Box(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            Image image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
        }

        private Text Label(Transform parent, string text, int size, Color tint, float x, float y, float width, float height, FontStyle style = FontStyle.Normal)
        {
            Text label = Rect(parent, text, x, y, width, height).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = tint;
            label.fontStyle = style; label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
            return label;
        }

        private void SelectFirst()
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(null);
            foreach (Selectable selectable in pageRoot.GetComponentsInChildren<Selectable>())
                if (selectable.IsInteractable()) { selectable.Select(); break; }
        }

        private static string ScenarioName(string scene) => VoyagePassage.TitleOf(scene);

        private static void Quit()
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
