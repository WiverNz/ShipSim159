using System;
using System.Collections;
using System.IO;
using ShipSimulator.Audio;
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
        private RectTransform pageViewport;
        private RectTransform shell;
        private ScrollRect pageScroll;
        private float nextRow;
        private Vector2 shellSize;
        private float shellScale;
        private bool pageLayoutDirty = true;
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
            Localization.Changed += InvalidatePageLayout;
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
                    ShowFailure("Could not restore voyage.", error);
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
            RefreshLayout();
            if (loading) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                HandleEscape();
        }

        public void HandleEscape()
        {
            if (loading) return;
            if (!open) OpenPause();
            else if (page != "home") { VoyageSettings.Save(); ShowHome(); }
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
                ShowFailure("Could not open scenario.", error);
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
                ShowFailure("Could not load voyage.", error);
            }
        }

        public void SaveVoyage()
        {
            if (!hasVoyage || loading) return;
            try
            {
                VoyageSaveStore.Write(SavePath, CaptureSave());
                ShowHome();
                LocalizedLabel.BindSource(status, "Voyage saved. Safe to leave the bridge.");
            }
            catch (Exception error) { ShowFailure("Could not save voyage.", error); }
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
                mechanisms = ship.Mechanisms?.CaptureState(),
                groundTackle = ship.GroundTackle?.CaptureState() ?? new GroundTackleSave(),
                loading = ship.CaptureLoading(),
                pushUnit = ship.GetComponent<PushUnit>()?.Capture(),
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
            ship.Mechanisms?.RestoreState(save.mechanisms);
            ship.GroundTackle?.RestoreState(save.groundTackle, save.mechanisms?.LegacyChainM());
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
            Func<string> savedDescription = () => Localization.Source("No saved voyage yet");
            bool valid = false;
            if (exists)
            {
                try
                {
                    VoyageSave save = VoyageSaveStore.Read(SavePath);
                    savedDescription = () => ScenarioName(save.scene) + "  /  " + DateTime.Parse(save.savedUtc, System.Globalization.CultureInfo.InvariantCulture).ToLocalTime().ToString("dd MMM, HH:mm", Localization.Culture);
                    valid = true;
                }
                catch (Exception) { savedDescription = () => Localization.Source("Save unavailable. Start a new voyage to continue."); }
            }
            Button savedButton = ActionButton(hasVoyage ? "Load saved voyage" : "Continue voyage", savedDescription(), y,
                () => { if (hasVoyage) Confirm("Load saved voyage?", "Unsaved progress in this passage will be lost.", LoadVoyage); else LoadVoyage(); }, false, valid); y += 80;
            LocalizedLabel.Bind(savedButton.GetComponentsInChildren<Text>()[1], savedDescription);
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
                Button vesselButton = ActionButton(entry.DisplayName, entry.Description, y, () =>
                {
                    VesselSelection.SelectedId = id;
                    UpdateVesselLabels();
                    ShowVoyages();
                }, id == VesselSelection.SelectedId, true, catalogue.Entries.Count > 6 ? 60 : 70);
                LocalizedLabel.Bind(vesselButton.GetComponentsInChildren<Text>()[0], () => entry.DisplayName);
                LocalizedLabel.Bind(vesselButton.GetComponentsInChildren<Text>()[1], () => entry.Description);
                y += catalogue.Entries.Count > 6 ? 64 : catalogue.Entries.Count > 5 ? 76 : 94;
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
                string restriction = passage.FormationRestriction(vessel);
                string scene = passage.SceneName;
                Button passageButton = ActionButton(passage.Title, restriction ?? passage.Detail, y,
                    () => StartVoyage(scene), first && restriction == null, restriction == null);
                LocalizedLabel.Bind(passageButton.GetComponentsInChildren<Text>()[0], () => passage.Title);
                LocalizedLabel.Bind(passageButton.GetComponentsInChildren<Text>()[1], () => passage.FormationRestriction(vessel) ?? passage.Detail);
                first &= restriction != null;
                y += 88;
            }
            string vesselText = data != null
                ? vessel.DisplayName + "\n" + vessel.Description
                : "Volgo-Don Project 507B";
            Text vesselHelp = Label(pageRoot, "", 18, Muted, 0, y + 10, 450, 180);
            LocalizedLabel.Bind(vesselHelp, () => Localization.Get("text.your.vessel.0.w.s.engines.a.d.rudder.v.camera.right.mous.49facf55", data != null ? vessel.DisplayName + "\n" + vessel.Description : "Volgo-Don Project 507B"));
            ActionButton("Back", "Choose another vessel", 462, ShowVessels);
            SelectFirst();
        }

        private VesselCatalogue.Entry SelectedVessel() => catalogue != null ? catalogue.Find(VesselSelection.SelectedId) : null;

        private void UpdateVesselLabels()
        {
            VesselCatalogue.Entry vessel = SelectedVessel();
            VesselData data = vessel?.LoadData();
            if (vesselNameLabel != null) LocalizedLabel.Bind(vesselNameLabel, () => Localization.Source(vessel != null ? vessel.menuName : "VOLGO-DON\n507B"));
            if (vesselClassLabel != null) LocalizedLabel.Bind(vesselClassLabel, () => Localization.Source(vessel != null ? vessel.vesselClass : "RIVER CLASS\nCARGO VESSEL"));
            if (vesselLengthLabel != null)
                LocalizedLabel.Bind(vesselLengthLabel, () => Localization.Get("text.0.m.length.overall.8c7c6b48", (data != null ? data.dimensions.lengthOverallM : 138.3f).ToString("0.0", Localization.Culture)));
            projectName = data != null ? data.identity.project : "507B";
            if (status != null && !hasVoyage && !loading)
                LocalizedLabel.Bind(status, () => Localization.Get("text.project.0.river.navigation.simulator.822c1ddb", projectName));
        }

        public void ShowSettings()
        {
            ShowPage("settings", "MAKE YOURSELF AT HOME", "Bridge settings");
            ActionButton("Language", "Choose the interface language", 0, ShowLanguage);
            ActionButton("Interface & camera", "Scale and independent camera sensitivities", 0, ShowInterface);
            ActionButton("HUD panels", "Choose visible and pinned controls", 0, ShowPanels);
            ActionButton("Sound", "Master volume, mix and mute", 0, ShowSound);
            ActionButton("Display", "Quality, synchronisation and fullscreen", 0, ShowDisplay);
            ActionButton("Done", "Settings are saved automatically", 0, () => { VoyageSettings.Save(); ShowHome(); }, true);
            SelectFirst();
        }

        public void ShowLanguage()
        {
            ShowPage("language", "MAKE YOURSELF AT HOME", "Language");
            for (int i = 0; i < Localization.Languages.Length; i++)
            {
                string code = Localization.Languages[i];
                Button choice = ActionButton(Localization.NativeNames[i], code, 0, () => VoyageSettings.SetLanguage(code));
                LocalizedLabel.Bind(choice.GetComponentsInChildren<Text>()[1], () =>
                    code == Localization.Language ? Localization.Get("language.selected") : code);
            }
            ActionButton("Back", "Return to settings", 0, ShowSettings);
            SelectFirst();
        }

        public void ShowInterface()
        {
            ShowPage("interface", "INTERFACE & CAMERA", "Comfort settings");
            SettingSlider("Interface scale", VoyageSettings.UiScale, .8f, 1.5f, 0,
                VoyageSettings.SetUiScale, value => Mathf.RoundToInt(value * 100) + "%");
            SettingSlider("Camera rotation", VoyageSettings.CameraSensitivity, .25f, 2f, 0,
                VoyageSettings.SetSensitivity, value => value.ToString("0.00", Localization.Culture) + "x");
            SettingSlider("Camera zoom", VoyageSettings.ZoomSensitivity, .25f, 3f, 0,
                VoyageSettings.SetZoomSensitivity, value => value.ToString("0.00", Localization.Culture) + "x");
            ActionButton("Restore defaults", "Interface scale and camera sensitivities only", 0,
                () => { VoyageSettings.RestoreControlDefaults(); ShowInterface(); });
            ActionButton("Back", "Save and return to settings", 0, () => { VoyageSettings.Save(); ShowSettings(); }, true);
            SelectFirst();
        }

        public void ShowPanels()
        {
            ShowPage("panels", "HUD panels", "Choose visible and pinned controls");
            SettingSlider("Panel size", VoyageSettings.PanelScale, .75f, 1.15f, 0,
                VoyageSettings.SetPanelScale, value => Mathf.RoundToInt(value * 100) + "%");
            for (int i = 0; i < VoyageSettings.HudPanels.Length; i++)
            {
                string panel = VoyageSettings.HudPanels[i];
                string label = VoyageSettings.HudPanelLabels[i];
                Button button = ActionButton(label, "", 0,
                    () => VoyageSettings.SetPanelMode(panel, (VoyageSettings.PanelMode(panel) + 1) % 3),
                    false, !VoyageSettings.EssentialPanel(panel));
                LocalizedLabel.Bind(button.GetComponentsInChildren<Text>()[1], () =>
                    Localization.Get(VoyageSettings.EssentialPanel(panel) ? "hud.essential" : "hud.mode." + VoyageSettings.PanelMode(panel)));
                VoyageSettings.Changed += RefreshMode;
                void RefreshMode() { if (button != null) button.GetComponentsInChildren<Text>()[1].text =
                    Localization.Get(VoyageSettings.EssentialPanel(panel) ? "hud.essential" : "hud.mode." + VoyageSettings.PanelMode(panel)); }
                button.gameObject.AddComponent<HudSettingBinding>().OnDisposed = () => VoyageSettings.Changed -= RefreshMode;
            }
            ActionButton("Restore defaults", "Restore panel visibility and size", 0,
                () => { VoyageSettings.RestorePanelDefaults(); ShowPanels(); });
            ActionButton("Back", "Save and return to settings", 0, () => { VoyageSettings.Save(); ShowSettings(); }, true);
            SelectFirst();
        }

        public void ShowDisplay()
        {
            ShowPage("display", "DISPLAY", "Graphics settings");
            ActionButton(() => Localization.Get("text.graphics.0.381c6bbe", Localization.Source(QualitySettings.names[QualitySettings.GetQualityLevel()])), "Select the next quality preset", 0, () => { VoyageSettings.CycleQuality(); ShowDisplay(); });
            ActionButton(() => Localization.Get("text.vsync.0.8a9fda47", Localization.Source((QualitySettings.vSyncCount > 0 ? "On" : "Off"))), "Synchronise frames with your display", 0, () => { VoyageSettings.ToggleVSync(); ShowDisplay(); });
            ActionButton(() => Localization.Get("text.display.0.4a5c3dbd", Localization.Source(((Application.isEditor ? VoyageSettings.Fullscreen : Screen.fullScreen) ? "Fullscreen" : "Windowed"))), Application.isEditor ? "Available in the standalone player" : "Toggle fullscreen display", 0,
                () => { VoyageSettings.ToggleFullscreen(); StartCoroutine(RefreshDisplay()); }, false, !Application.isEditor);
            ActionButton(() => Localization.Get("display.window.size", VoyageSettings.WindowSize.x, VoyageSettings.WindowSize.y),
                "Choose a window size", 0, ShowWindowSizes, false, !Application.isEditor);
            ActionButton("Back", "Save and return to settings", 0, () => { VoyageSettings.Save(); ShowSettings(); }, true);
            SelectFirst();
        }

        public void ShowWindowSizes()
        {
            ShowPage("window-size", "DISPLAY", "Choose a window size");
            foreach (var size in VoyageSettings.WindowSizes)
            {
                Vector2Int chosen = size;
                ActionButton(size.x + " × " + size.y, "Use windowed mode", 0,
                    () => { VoyageSettings.SetWindowSize(chosen.x, chosen.y); StartCoroutine(RefreshDisplay()); },
                    false, !Application.isEditor);
            }
            ActionButton("Back", "Return to settings", 0, ShowDisplay);
            SelectFirst();
        }

        private IEnumerator RefreshDisplay() { yield return null; ShowDisplay(); }

        public void ShowSound()
        {
            ShowPage("sound", "MAKE YOURSELF AT HOME", "Sound");
            Func<float, string> percent = value => Mathf.RoundToInt(value * 100) + "%";
            SettingSlider("Master volume", VoyageSettings.Volume, 0f, 1f, 0, VoyageSettings.SetVolume, percent);
            SettingSlider("Engines and propellers", VoyageSettings.BusVolume(AudioBus.Engines), 0f, 1f, 74,
                value => VoyageSettings.SetBusVolume(AudioBus.Engines, value), percent);
            SettingSlider("Water", VoyageSettings.BusVolume(AudioBus.Water), 0f, 1f, 148,
                value => VoyageSettings.SetBusVolume(AudioBus.Water, value), percent);
            SettingSlider("Wind", VoyageSettings.BusVolume(AudioBus.Wind), 0f, 1f, 222,
                value => VoyageSettings.SetBusVolume(AudioBus.Wind, value), percent);
            SettingSlider("Deck machinery", VoyageSettings.BusVolume(AudioBus.Machinery), 0f, 1f, 296,
                value => VoyageSettings.SetBusVolume(AudioBus.Machinery, value), percent);
            ActionButton(() => Localization.Get("text.mute.0.b91012a8", Localization.Source((VoyageSettings.Muted ? "On" : "Off"))), "Silence all sound; the mix is kept", 382,
                () => { VoyageSettings.SetMuted(!VoyageSettings.Muted); ShowSound(); });
            ActionButton("Back", "Return to settings", 462, () => { PlayerPrefs.Save(); ShowSettings(); }, true);
            SelectFirst();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            VoyageSettings.SetFocused(hasFocus);
            if (!hasFocus) VoyageSettings.Save();
        }
        private void OnApplicationPause(bool paused) { if (paused) VoyageSettings.Save(); }
        private void OnApplicationQuit() => VoyageSettings.Save();

        private void ShowFailure(string summary, Exception error)
        {
            Debug.LogException(error);
            LocalizedLabel.Bind(status, () => Localization.Source(summary) + "\n" +
                Localization.Source("Check the save file and available disk space. Details are in the log."));
        }

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
            pageLayoutDirty = true;
            nextRow = 0;
            if (pageScroll != null) { pageScroll.StopMovement(); pageScroll.verticalNormalizedPosition = 1; }
            foreach (Transform child in pageRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            LocalizedLabel.BindSource(eyebrow, kicker);
            LocalizedLabel.BindSource(heading, title);
            LocalizedLabel.Bind(status, () => hasVoyage ? Localization.Get("text.passage.paused.your.vessel.is.holding.position.96f59fc0") : Localization.Get("text.project.0.river.navigation.simulator.822c1ddb", projectName));
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
            shell = layout;
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
            pageViewport = Rect(layout, "Menu page", 885, 206, 480, 560);
            pageRoot = Rect(pageViewport, "Content", 0, 0, 480, 560);
            pageScroll = UiLayout.Scroll(pageViewport, pageRoot);
            status = Label(layout, "", 16, Muted, 885, 787, 478, 72);
            Text version = Label(layout, "v" + Application.version, 14, Muted, 885, 859, 478, 25);
            version.name = "Game version";
            version.alignment = TextAnchor.MiddleRight;
            RefreshLayout();
        }

        private Button ActionButton(Func<string> title, string detail, float y, Action action,
            bool primary = false, bool enabled = true, float height = 84)
        {
            Button button = ActionButton(title(), detail, y, action, primary, enabled, height);
            LocalizedLabel.Bind(button.GetComponentsInChildren<Text>()[0], title);
            return button;
        }

        private Button ActionButton(string title, string detail, float y, Action action, bool primary = false, bool enabled = true, float height = 70)
        {
            height = Mathf.Max(84, height);
            float detailHeight = Measure(Localization.Source(detail), 16, 425);
            height = Mathf.Max(height, 45 + detailHeight + 12);
            y = nextRow;
            nextRow += height + 12;
            RectTransform rect = Rect(pageRoot, title, 0, y, 478, height);
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
            Label(rect, detail, 16, primary ? Navy : Muted, 20, 42, 425, detailHeight);
            button.onClick.AddListener(() => action());
            return button;
        }

        private void SettingSlider(string title, float value, float min, float max, float y, Action<float> setter, Func<float, string> format)
        {
            y = nextRow;
            nextRow += 98;
            RectTransform row = Rect(pageRoot, title, 0, y, 478, 86);
            Label(row, title, 22, White, 0, 0, 330, 32);
            Text number = Label(row, format(value), 20, Brass, 360, 0, 116, 32);
            number.alignment = TextAnchor.MiddleRight;
            RectTransform control = Rect(row, title + " slider", 0, 42, 478, 36);
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
            LocalizedLabel.Bind(number, () => format(slider.value));
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
            if (parent == pageRoot)
            {
                y = nextRow;
                height = Mathf.Max(height, Measure(Localization.Source(text), size, width));
                nextRow += height + 16;
            }
            Text label = Rect(parent, text, x, y, width, height).gameObject.AddComponent<Text>();
            label.font = font; LocalizedLabel.BindSource(label, text); label.fontSize = size; label.color = tint;
            label.fontStyle = style; label.alignment = TextAnchor.UpperLeft;
            label.raycastTarget = false;
            return label;
        }

        private float Measure(string text, int size, float width)
        {
            var settings = new TextGenerationSettings { font = font, fontSize = size,
                generationExtents = new Vector2(width, 0), scaleFactor = 1,
                lineSpacing = 1, horizontalOverflow = HorizontalWrapMode.Wrap,
                verticalOverflow = VerticalWrapMode.Overflow, richText = true };
            return Mathf.Max(size + 6, new TextGenerator().GetPreferredHeight(text, settings) + 6);
        }

        private void InvalidatePageLayout() => pageLayoutDirty = true;

        private void ReflowPage()
        {
            if (!pageLayoutDirty) return;
            pageLayoutDirty = false;
            float y = 0;
            foreach (RectTransform row in pageRoot)
            {
                if (!row.gameObject.activeSelf) continue;
                float height = row.sizeDelta.y;
                var labels = row.GetComponentsInChildren<Text>();
                if (row.GetComponent<Button>() != null && labels.Length >= 2)
                {
                    float titleHeight = Mathf.Max(29, labels[0].preferredHeight + 4);
                    float detailHeight = Mathf.Max(24, labels[1].preferredHeight + 4);
                    UiLayout.Place(labels[0].rectTransform, 20, 9, 425, titleHeight);
                    UiLayout.Place(labels[1].rectTransform, 20, 13 + titleHeight, 425, detailHeight);
                    height = Mathf.Max(84, 25 + titleHeight + detailHeight);
                }
                else if (row.GetComponent<Text>() is Text text)
                    height = Mathf.Max(height, text.preferredHeight + 8);
                row.anchoredPosition = new Vector2(row.anchoredPosition.x, -y);
                row.sizeDelta = new Vector2(row.sizeDelta.x, height);
                y += height + 12;
            }
            nextRow = y;
        }

        private void RefreshLayout()
        {
            if (overlay == null || shell == null || pageRoot == null) return;
            ReflowPage();
            Vector2 size = UiLayout.Apply(overlay.GetComponent<Canvas>());
            pageRoot.sizeDelta = new Vector2(480, Mathf.Max(nextRow, pageViewport.rect.height));
            if (size == shellSize && shellScale == VoyageSettings.UiScale) return;
            shellSize = size;
            shellScale = VoyageSettings.UiScale;
            bool wide = size.x >= 1440 && size.y >= 850;
            UiLayout.Place(shell, 0, 0, size.x, size.y);
            float x = wide ? size.x * .58f : (size.x - 480) * .5f;
            x = Mathf.Min(x, size.x - 510);
            foreach (Transform child in shell)
            {
                if (child == pageViewport || child == heading.transform || child == eyebrow.transform ||
                    child == status.transform || child.name == "Game version") continue;
                child.gameObject.SetActive(wide);
            }
            UiLayout.Place(eyebrow.rectTransform, x, 24, 480, 28);
            UiLayout.Place(heading.rectTransform, x, 62, 500, 96);
            UiLayout.Place(pageViewport, x, 168, 500, Mathf.Max(150, size.y - 280));
            UiLayout.Place(status.rectTransform, x, size.y - 100, 478, 64);
            UiLayout.Place((RectTransform)shell.Find("Game version"), x, size.y - 32, 478, 25);
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
            Localization.Changed -= InvalidatePageLayout;
            if (instance != this) return;
            instance = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
