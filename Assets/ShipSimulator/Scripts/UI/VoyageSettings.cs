using ShipSimulator.Audio;
using UnityEngine;

namespace ShipSimulator.UI
{
    public static class VoyageSettings
    {
        private const string Prefix = "ShipSim159.Settings.";
        public static float CameraSensitivity => PlayerPrefs.GetFloat(Prefix + "Sensitivity", 1f);
        public static float Volume => PlayerPrefs.GetFloat(Prefix + "Volume", 0.8f);
        public static bool Muted => PlayerPrefs.GetInt(Prefix + "Mute", 0) == 1;
        // A standalone player falls silent while another window has focus; the editor keeps its own audio controls.
        public static bool MuteInBackground => PlayerPrefs.GetInt(Prefix + "MuteInBackground", 1) == 1;
        private static bool focused = true;

        public static float BusVolume(AudioBus bus) => PlayerPrefs.GetFloat(Prefix + "Bus." + AudioMix.BusName(bus), 1f);

        public static void Apply()
        {
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
            PlayerPrefs.SetFloat(Prefix + "Sensitivity", Mathf.Clamp(value, 0.25f, 2f));

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
