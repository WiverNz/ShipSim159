using System;
using ShipSimulator.Audio;
using UnityEngine;

namespace ShipSimulator.UI
{
    public static class VoyageSettings
    {
        private const string Prefix = "ShipSim159.Settings.";
        public static event Action Changed;
        public static string Language
        {
            get
            {
                string language = PlayerPrefs.GetString(Prefix + "Language", "en");
                return Array.IndexOf(Localization.Languages, language) >= 0 ? language : "en";
            }
        }
        public static void SetLanguage(string language)
        {
            if (Array.IndexOf(Localization.Languages, language) < 0) language = "en";
            bool changed = Language != language;
            PlayerPrefs.SetString(Prefix + "Language", language);
            Save();
            if (!changed) return;
            Localization.NotifyChanged();
            Changed?.Invoke();
        }
        // Stable panel names also identify the runtime controls and their saved preferences.
        public static readonly string[] HudPanels = { "TelegraphBlock", "RudderBlock", "LoadingBlock",
            "MachineryBlock", "GroundTackleBlock", "CameraBlock", "TimeScaleBlock", "WeatherBlock",
            "Objective", "MiniMap" };
        public static readonly string[] HudPanelLabels = { "Engines", "Helm", "Cargo", "Deck machinery",
            "Anchors and mooring", "Camera", "Simulation speed", "Weather", "Passage objective", "Map" };
        // 0: hidden, 1: in its tab, 2: pinned beside the active tab.
        public static int PanelMode(string panel) => Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Panel." + panel,
            panel == "MiniMap" || panel == "TimeScaleBlock" ? 2 : 1), 0, 2);
        public static void SetPanelMode(string panel, int mode)
        {
            if (Array.IndexOf(HudPanels, panel) < 0) return;
            PlayerPrefs.SetInt(Prefix + "Panel." + panel, Mathf.Clamp(mode, 0, 2));
            Save();
            Changed?.Invoke();
        }
        public static float PanelScale => Read("PanelScale", .85f, .75f, 1.15f);
        public static void SetPanelScale(float value) => Write("PanelScale", value, .85f, .75f, 1.15f);
        public static void RestorePanelDefaults()
        {
            foreach (string panel in HudPanels) PlayerPrefs.DeleteKey(Prefix + "Panel." + panel);
            SetPanelScale(.85f);
            Save();
        }
        public static float UiScale => Read("UiScale", 1f, .8f, 1.5f);
        public static float CameraSensitivity => Read("RotationSensitivity",
            Read("Sensitivity", 1f, .25f, 2f), .25f, 2f);
        public static float ZoomSensitivity => Read("ZoomSensitivity", 1f, .25f, 3f);

        private static float Valid(float value, float fallback, float min, float max) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

        private static float Read(string key, float fallback, float min, float max) =>
            Valid(PlayerPrefs.GetFloat(Prefix + key, fallback), fallback, min, max);

        private static void Write(string key, float value, float fallback, float min, float max)
        {
            PlayerPrefs.SetFloat(Prefix + key, Valid(value, fallback, min, max));
            Changed?.Invoke();
        }

        public static void SetUiScale(float value) => Write("UiScale", value, 1f, .8f, 1.5f);
        public static void SetZoomSensitivity(float value) => Write("ZoomSensitivity", value, 1f, .25f, 3f);
        public static void Save() => PlayerPrefs.Save();

        public static void RestoreControlDefaults()
        {
            SetUiScale(1f);
            SetSensitivity(1f);
            SetZoomSensitivity(1f);
            Save();
        }
        public static float Volume => PlayerPrefs.GetFloat(Prefix + "Volume", 0.8f);
        public static bool Muted => PlayerPrefs.GetInt(Prefix + "Mute", 0) == 1;
        // A standalone player falls silent while another window has focus; the editor keeps its own audio controls.
        public static bool MuteInBackground => PlayerPrefs.GetInt(Prefix + "MuteInBackground", 1) == 1;
        private static bool focused = true;

        public static float BusVolume(AudioBus bus) => PlayerPrefs.GetFloat(Prefix + "Bus." + AudioMix.BusName(bus), 1f);

        public static void Apply()
        {
            if (!PlayerPrefs.HasKey(Prefix + "RotationSensitivity"))
                PlayerPrefs.SetFloat(Prefix + "RotationSensitivity", CameraSensitivity);
            ApplyListenerVolume();
            for (int i = 0; i < AudioMix.BusCount; i++) AudioMix.SetUserVolume((AudioBus)i, BusVolume((AudioBus)i));
            if (PlayerPrefs.HasKey(Prefix + "Quality"))
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Quality"), 0, QualitySettings.names.Length - 1));
            QualitySettings.vSyncCount = PlayerPrefs.GetInt(Prefix + "VSync", 1);
            if (!Application.isEditor && PlayerPrefs.HasKey(Prefix + "Fullscreen"))
                Screen.fullScreen = PlayerPrefs.GetInt(Prefix + "Fullscreen") == 1;
        }

        public static void SetVolume(float value)
        {
            PlayerPrefs.SetFloat(Prefix + "Volume", Mathf.Clamp01(value));
            ApplyListenerVolume();
        }

        public static void SetMuted(bool muted)
        {
            PlayerPrefs.SetInt(Prefix + "Mute", muted ? 1 : 0);
            ApplyListenerVolume();
        }

        public static void SetMuteInBackground(bool mute)
        {
            PlayerPrefs.SetInt(Prefix + "MuteInBackground", mute ? 1 : 0);
            ApplyListenerVolume();
        }

        public static void SetBusVolume(AudioBus bus, float value)
        {
            PlayerPrefs.SetFloat(Prefix + "Bus." + AudioMix.BusName(bus), Mathf.Clamp01(value));
            AudioMix.SetUserVolume(bus, value);
        }

        public static void SetFocused(bool hasFocus)
        {
            focused = hasFocus;
            ApplyListenerVolume();
        }

        private static void ApplyListenerVolume()
        {
            bool silent = Muted || (!focused && MuteInBackground && !Application.isEditor);
            AudioListener.volume = silent ? 0f : Mathf.Clamp01(Volume);
        }

        public static void SetSensitivity(float value) =>
            Write("RotationSensitivity", value, 1f, .25f, 2f);

        public static void CycleQuality()
        {
            int quality = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
            QualitySettings.SetQualityLevel(quality);
            PlayerPrefs.SetInt(Prefix + "Quality", quality);
            ShipSimulator.Visuals.RiverLighting.ConfigureCameraQuality();
        }

        public static void ToggleVSync()
        {
            QualitySettings.vSyncCount = QualitySettings.vSyncCount == 0 ? 1 : 0;
            PlayerPrefs.SetInt(Prefix + "VSync", QualitySettings.vSyncCount);
        }

        public static void ToggleFullscreen()
        {
            if (Application.isEditor) return;
            bool fullscreen = !Screen.fullScreen;
            Screen.fullScreen = fullscreen;
            PlayerPrefs.SetInt(Prefix + "Fullscreen", fullscreen ? 1 : 0);
        }
    }
}
