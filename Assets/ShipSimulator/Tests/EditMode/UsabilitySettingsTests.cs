using System.Collections.Generic;
using NUnit.Framework;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShipSimulator.Tests
{
    public sealed class UsabilitySettingsTests
    {
        private const string Prefix = "ShipSim159.Settings.";
        private readonly Dictionary<string, float> previous = new Dictionary<string, float>();
        private readonly string[] keys = { "UiScale", "Sensitivity", "RotationSensitivity", "ZoomSensitivity", "PanelScale" };

        [SetUp]
        public void SetUp()
        {
            foreach (string key in keys)
            {
                if (PlayerPrefs.HasKey(Prefix + key)) previous[key] = PlayerPrefs.GetFloat(Prefix + key);
                PlayerPrefs.DeleteKey(Prefix + key);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string key in keys)
            {
                PlayerPrefs.DeleteKey(Prefix + key);
                if (previous.TryGetValue(key, out float value)) PlayerPrefs.SetFloat(Prefix + key, value);
            }
            previous.Clear();
            VoyageSettings.Save();
        }

        [Test]
        public void PanelSettings_PersistClampAndNotifyWithoutChangingControls()
        {
            const string key = Prefix + "Panel.WeatherBlock";
            bool existed = PlayerPrefs.HasKey(key);
            int before = PlayerPrefs.GetInt(key);
            float sensitivity = VoyageSettings.CameraSensitivity;
            int notifications = 0;
            void Changed() => notifications++;
            VoyageSettings.Changed += Changed;
            try
            {
                VoyageSettings.SetPanelMode("WeatherBlock", 2);
                Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(2));
                Assert.That(VoyageSettings.PanelMode("WeatherBlock"), Is.EqualTo(2));
                VoyageSettings.SetPanelMode("WeatherBlock", -10);
                Assert.That(VoyageSettings.PanelMode("WeatherBlock"), Is.Zero);
                VoyageSettings.SetPanelScale(float.NaN);
                Assert.That(VoyageSettings.PanelScale, Is.EqualTo(.85f));
                Assert.That(notifications, Is.EqualTo(3));
                Assert.That(VoyageSettings.CameraSensitivity, Is.EqualTo(sensitivity));
            }
            finally
            {
                VoyageSettings.Changed -= Changed;
                if (existed) PlayerPrefs.SetInt(key, before); else PlayerPrefs.DeleteKey(key);
            }
        }

        [Test]
        public void LegacySensitivity_IsPreservedUntilExplicitlyChanged()
        {
            PlayerPrefs.SetFloat(Prefix + "Sensitivity", 1.37f);
            Assert.That(VoyageSettings.CameraSensitivity, Is.EqualTo(1.37f));
            VoyageSettings.SetSensitivity(.6f);
            Assert.That(VoyageSettings.CameraSensitivity, Is.EqualTo(.6f));
            Assert.That(PlayerPrefs.GetFloat(Prefix + "Sensitivity"), Is.EqualTo(1.37f));
        }

        [Test]
        public void InvalidPreferences_AreFiniteAndBounded()
        {
            VoyageSettings.SetUiScale(float.NaN);
            VoyageSettings.SetZoomSensitivity(float.PositiveInfinity);
            VoyageSettings.SetSensitivity(-10);
            Assert.That(VoyageSettings.UiScale, Is.EqualTo(1));
            Assert.That(VoyageSettings.ZoomSensitivity, Is.EqualTo(1));
            Assert.That(VoyageSettings.CameraSensitivity, Is.EqualTo(.25f));
            PlayerPrefs.SetFloat(Prefix + "UiScale", 99);
            Assert.That(VoyageSettings.UiScale, Is.EqualTo(1.5f));
        }

        [Test]
        public void Preferences_PersistIndependentlyAndNotifyConsumers()
        {
            int notifications = 0;
            void Changed() => notifications++;
            VoyageSettings.Changed += Changed;
            try
            {
                VoyageSettings.SetUiScale(1.4f);
                VoyageSettings.SetSensitivity(.7f);
                VoyageSettings.SetZoomSensitivity(2.5f);
                VoyageSettings.Save();
                Assert.That(notifications, Is.EqualTo(3));
                Assert.That(PlayerPrefs.GetFloat(Prefix + "UiScale"), Is.EqualTo(1.4f));
                Assert.That(PlayerPrefs.GetFloat(Prefix + "RotationSensitivity"), Is.EqualTo(.7f));
                Assert.That(PlayerPrefs.GetFloat(Prefix + "ZoomSensitivity"), Is.EqualTo(2.5f));
            }
            finally { VoyageSettings.Changed -= Changed; }
        }

        [Test]
        public void RestoreDefaults_DoesNotChangeSoundPreferences()
        {
            float volume = VoyageSettings.Volume;
            VoyageSettings.SetUiScale(1.5f);
            VoyageSettings.SetSensitivity(2);
            VoyageSettings.SetZoomSensitivity(3);
            VoyageSettings.RestoreControlDefaults();
            Assert.That(VoyageSettings.UiScale, Is.EqualTo(1));
            Assert.That(VoyageSettings.CameraSensitivity, Is.EqualTo(1));
            Assert.That(VoyageSettings.ZoomSensitivity, Is.EqualTo(1));
            Assert.That(VoyageSettings.Volume, Is.EqualTo(volume));
        }

        [Test]
        public void HudButton_OnlyLeftClickActivatesAndDisabledNeverActivates()
        {
            var system = new GameObject("UI test events", typeof(EventSystem));
            var go = new GameObject("UI test button", typeof(RectTransform), typeof(Image), typeof(HudButton));
            try
            {
                int calls = 0;
                var button = go.GetComponent<HudButton>();
                button.Initialise(go.GetComponent<Image>(), null, null, null, () => calls++);
                var pointer = new PointerEventData(system.GetComponent<EventSystem>());
                pointer.button = PointerEventData.InputButton.Right;
                button.OnPointerClick(pointer);
                pointer.button = PointerEventData.InputButton.Middle;
                button.OnPointerClick(pointer);
                Assert.That(calls, Is.Zero);
                pointer.button = PointerEventData.InputButton.Left;
                button.OnPointerClick(pointer);
                Assert.That(calls, Is.EqualTo(1));
                button.SetInteractable(false);
                button.OnPointerClick(pointer);
                Assert.That(calls, Is.EqualTo(1));
                button.SetActive(true);
                Assert.That(go.transform.localScale, Is.EqualTo(Vector3.one), "Selection must not overlap neighbouring buttons.");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(system); }
        }


    }
}
