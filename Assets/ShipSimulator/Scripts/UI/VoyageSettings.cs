using UnityEngine;

namespace ShipSimulator.UI
{
    public static class VoyageSettings
    {
        private const string Prefix = "ShipSim159.Settings.";
        public static float CameraSensitivity => PlayerPrefs.GetFloat(Prefix + "Sensitivity", 1f);
        public static float Volume => PlayerPrefs.GetFloat(Prefix + "Volume", 0.8f);

        public static void Apply()
        {
            AudioListener.volume = Mathf.Clamp01(Volume);
            if (PlayerPrefs.HasKey(Prefix + "Quality"))
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "Quality"), 0, QualitySettings.names.Length - 1));
            QualitySettings.vSyncCount = PlayerPrefs.GetInt(Prefix + "VSync", 1);
            if (!Application.isEditor && PlayerPrefs.HasKey(Prefix + "Fullscreen"))
                Screen.fullScreen = PlayerPrefs.GetInt(Prefix + "Fullscreen") == 1;
        }

        public static void SetVolume(float value)
        {
            PlayerPrefs.SetFloat(Prefix + "Volume", Mathf.Clamp01(value));
            AudioListener.volume = Mathf.Clamp01(value);
        }

        public static void SetSensitivity(float value) =>
            PlayerPrefs.SetFloat(Prefix + "Sensitivity", Mathf.Clamp(value, 0.25f, 2f));

        public static void CycleQuality()
        {
            int quality = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
            QualitySettings.SetQualityLevel(quality);
            PlayerPrefs.SetInt(Prefix + "Quality", quality);
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
