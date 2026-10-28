using System.Collections.Generic;
using NUnit.Framework;
using ShipSimulator.UI;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class DisplaySettingsTests
    {
        private const string Prefix = "ShipSim159.Settings.";
        private readonly Dictionary<string, int> saved = new();
        private readonly string[] keys = { "Fullscreen", "WindowWidth", "WindowHeight", "Panel.MiniMap", "Panel.TelegraphBlock" };
        [SetUp]
        public void Setup()
        {
            foreach (string key in keys)
            {
                if (PlayerPrefs.HasKey(Prefix + key)) saved[key] = PlayerPrefs.GetInt(Prefix + key);
                PlayerPrefs.DeleteKey(Prefix + key);
            }
        }
        [TearDown]
        public void Cleanup()
        {
            foreach (string key in keys)
            {
                if (saved.TryGetValue(key, out int value)) PlayerPrefs.SetInt(Prefix + key, value);
                else PlayerPrefs.DeleteKey(Prefix + key);
            }
            saved.Clear();
            VoyageSettings.Save();
        }
        [Test]
        public void WindowChoicePersistsAcrossFullscreenRoundTrip()
        {
            Assert.That(VoyageSettings.Fullscreen, Is.True);
            VoyageSettings.SetWindowSize(1600, 900);
            Assert.That(VoyageSettings.Fullscreen, Is.False);
            Assert.That(PlayerPrefs.GetInt(Prefix + "WindowWidth"), Is.EqualTo(1600));
            VoyageSettings.ToggleFullscreen();
            Assert.That(VoyageSettings.Fullscreen, Is.True);
            VoyageSettings.ToggleFullscreen();
            Assert.That(VoyageSettings.WindowSize, Is.EqualTo(new Vector2Int(1600, 900)));
            Assert.That(VoyageSettings.Fullscreen, Is.False);
        }
        [Test]
        public void WindowFitsSmallerMonitorAndExplicitLaunchArgumentsAreRespected()
        {
            Assert.That(VoyageSettings.FitWindow(new Vector2Int(2560, 1440), new Vector2Int(1366, 768)),
                Is.EqualTo(new Vector2Int(1334, 704)));
            Assert.That(VoyageSettings.HasDisplayArguments(new[] { "game.exe", "-screen-width", "800" }), Is.True);
            Assert.That(VoyageSettings.HasDisplayArguments(new[] { "game.exe" }), Is.False);
        }
        [Test]
        public void OldHiddenPreferencesCannotHideEssentialControls()
        {
            foreach (string panel in new[] { "MiniMap", "TelegraphBlock" })
            {
                PlayerPrefs.SetInt(Prefix + "Panel." + panel, 0);
                VoyageSettings.SetPanelMode(panel, 1);
                Assert.That(VoyageSettings.PanelMode(panel), Is.EqualTo(2));
            }
        }
    }
}
