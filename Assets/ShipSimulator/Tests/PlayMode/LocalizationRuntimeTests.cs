using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShipSimulator.UI;
using ShipSimulator.CameraSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShipSimulator.Tests
{
    public sealed class LocalizationRuntimeTests
    {
        private string language, vessel;
        private Scene original, scene;
        private VoyageMenu menu;
        private float scale;

        [UnityTest]
        public IEnumerator SwitchingLanguagePreservesVoyageControlsAndFocusedSlider()
        {
            language = VoyageSettings.Language;
            scale = VoyageSettings.UiScale;
            vessel = VesselSelection.SelectedId;
            original = SceneManager.GetActiveScene();
            VesselSelection.SelectedId = "volgodon-507b";
            VoyageSettings.SetLanguage("en");
            yield return SceneManager.LoadSceneAsync("GorodetsTrainingScene", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("GorodetsTrainingScene");
            SceneManager.SetActiveScene(scene);
            menu = new GameObject("Localization menu").AddComponent<VoyageMenu>();
            menu.BindScene();
            yield return null;
            menu.StartVoyage("GorodetsTrainingScene");
            yield return null;
            var hud = Object.FindAnyObjectByType<ShipTelemetryUI>();
            var ship = hud.Ship;
            var camera = Object.FindAnyObjectByType<ShipFollowCamera>();
            camera.SetView(3);
            ship.SetEngineCommand(0, .32f);
            ship.SetRudderCommand(.25f);
            hud.GetComponent<HudLayout>().Select(1);
            menu.OpenPause();
            menu.ShowInterface();
            yield return null;
            Slider slider = menu.GetComponentInChildren<Slider>();
            slider.value = 1.25f;
            slider.Select();
            ship.GroundTackle.SetChainCommand(0, ShipSimulator.Physics.ChainCommand.LetGo);
            float noticeTime = ship.GroundTackle.NoticeTime;
            var mission = Object.FindAnyObjectByType<GorodetsScenarioController>();
            var phase = mission.Phase;
            float score = mission.Score;
            float mass = ship.CurrentMassKg;
            Vector3 position = ship.Body.position;
            int roots = scene.rootCount;
            var scroll = menu.GetComponentInChildren<ScrollRect>();
            scroll.verticalNormalizedPosition = .5f;
            float sliderValue = slider.value;
            foreach (string code in new[] { "ru", "de", "es", "en" })
            {
                VoyageSettings.SetLanguage(code);
                yield return null;
                Assert.That(menu.HasVoyage, Is.True);
                Assert.That(VoyageMenu.IsOpen, Is.True);
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(slider.gameObject));
                Assert.That(slider.value, Is.EqualTo(sliderValue));
                Assert.That(ship.CurrentMassKg, Is.EqualTo(mass));
                Assert.That(mission.Phase, Is.EqualTo(phase));
                Assert.That(mission.Score, Is.EqualTo(score));
                Assert.That(ship.GroundTackle.NoticeTime, Is.EqualTo(noticeTime));
                Assert.That(ship.GroundTackle.Notice, Is.EqualTo(Localization.FormatSource(
                    "{0} anchor let go", Localization.Source("PORT")).ToUpper(Localization.Culture)));
                Assert.That(ship.Body.position, Is.EqualTo(position));
                Assert.That(ship.RudderCommand, Is.EqualTo(.25f));
                Assert.That(camera.ViewIndex, Is.EqualTo(3));
                Assert.That(scene.rootCount, Is.EqualTo(roots));
                Assert.That(hud.GetComponent<HudLayout>().SelectedGroup, Is.EqualTo(1));
                Assert.That(menu.GetComponentsInChildren<Text>().Any(t => t.text == Localization.Source("Interface scale")), Is.True);
            }
            menu.ShowLanguage();
            yield return null;
            menu.GetComponentsInChildren<Button>().Single(b =>
                b.GetComponentsInChildren<Text>().Any(t => t.text == "Español")).onClick.Invoke();
            Assert.That(Localization.Language, Is.EqualTo("es"));
            Assert.That(menu.GetComponentsInChildren<Text>().Any(t => t.text == "Seleccionado"), Is.True);
            menu.Resume();
            Time.timeScale = 0;
            foreach (string code in new[] { "ru", "de", "es" })
            {
                VoyageSettings.SetLanguage(code);
                yield return null;
                Assert.That(VoyageMenu.IsOpen, Is.False);
                Assert.That(hud.GetComponentsInChildren<Text>().Any(t => t.text == Localization.Source("SELECT COMPARTMENT")), Is.True);
                Assert.That(camera.ViewIndex, Is.EqualTo(3));
            }
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            VoyageSettings.SetLanguage(language);
            VoyageSettings.SetUiScale(scale);
            if (menu != null) Object.Destroy(menu.gameObject);
            yield return null;
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            VesselSelection.SelectedId = vessel;
            Time.timeScale = 1;
            AudioListener.pause = false;
        }
    }
}
