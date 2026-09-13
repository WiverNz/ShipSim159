using System.Collections;
using System.IO;
using NUnit.Framework;
using ShipSimulator.CameraSystem;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShipSimulator.Tests
{
    public sealed class VoyageMenuTests
    {
        private Scene runnerScene;
        private Scene voyageScene;
        private VoyageMenu menu;
        private float originalVolume;
        private int originalQuality;
        private int originalVSync;
        private Keyboard keyboard;
        private InputTestFixture inputFixture;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            inputFixture = new InputTestFixture();
            inputFixture.Setup();
            runnerScene = SceneManager.GetActiveScene();
            originalVolume = AudioListener.volume;
            originalQuality = QualitySettings.GetQualityLevel();
            originalVSync = QualitySettings.vSyncCount;
            yield return SceneManager.LoadSceneAsync("GorodetsTrainingScene", LoadSceneMode.Additive);
            voyageScene = SceneManager.GetSceneByName("GorodetsTrainingScene");
            SceneManager.SetActiveScene(voyageScene);
            menu = new GameObject("Menu under test").AddComponent<VoyageMenu>();
            menu.SavePath = Path.Combine(Application.temporaryCachePath, "menu-test-voyage.json");
            menu.BindScene();
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (menu != null)
            {
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                    if (File.Exists(menu.SavePath + suffix)) File.Delete(menu.SavePath + suffix);
                Object.Destroy(menu.gameObject);
            }
            yield return null;
            SceneManager.SetActiveScene(runnerScene);
            if (voyageScene.isLoaded) yield return SceneManager.UnloadSceneAsync(voyageScene);
            Time.timeScale = 1;
            AudioListener.pause = false;
            AudioListener.volume = originalVolume;
            QualitySettings.SetQualityLevel(originalQuality);
            QualitySettings.vSyncCount = originalVSync;
            inputFixture.TearDown();
        }

        [UnityTest]
        public IEnumerator Menu_PausesStartsAndResumesPreviousSimulationSpeed()
        {
            Assert.That(VoyageMenu.IsOpen, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(AudioListener.pause, Is.True);
            ShipPhysicsController ship = Object.FindAnyObjectByType<ShipPhysicsController>();
            Vector3 position = ship.Body.position;
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(ship.Body.position, Is.EqualTo(position));
            yield return Capture("menu-start.png");
            menu.StartVoyage("GorodetsTrainingScene");
            Assert.That(VoyageMenu.IsOpen, Is.False);
            Assert.That(menu.HasVoyage, Is.True);
            var time = Object.FindAnyObjectByType<SimulationTimeController>();
            time.SetScale(4);
            menu.HandleEscape();
            Assert.That(Time.timeScale, Is.Zero);
            time.SetScale(4);
            Assert.That(Time.timeScale, Is.Zero, "Time controls must not release a menu pause.");
            menu.ShowSettings();
            yield return Capture("menu-settings.png");
            menu.HandleEscape();
            Assert.That(VoyageMenu.IsOpen, Is.True, "Escape leaves settings before resuming.");
            yield return Capture("menu-pause.png");
            menu.HandleEscape();
            Assert.That(Time.timeScale, Is.EqualTo(4));
            Assert.That(AudioListener.pause, Is.False);
        }

        [UnityTest]
        public IEnumerator EscapeKey_OpensMenuAndBlocksVesselShortcuts()
        {
            keyboard = InputSystem.AddDevice<Keyboard>();
            menu.StartVoyage("GorodetsTrainingScene");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(keyboard.escapeKey.isPressed, Is.True, "The injected key must reach the input system.");
            Assert.That(VoyageMenu.IsOpen, Is.True);
            var ship = Object.FindAnyObjectByType<ShipPhysicsController>();
            ship.SetThrottleCommand(0.5f);
            Vector3 position = ship.Body.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.R, Key.T, Key.W));
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(ship.ThrottleCommand, Is.EqualTo(0.5f));
            Assert.That(ship.Body.position, Is.EqualTo(position));
            Assert.That(Time.timeScale, Is.Zero);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(0.05f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(VoyageMenu.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator Save_RestoresVesselDynamicsWeatherCameraAndMission()
        {
            menu.StartVoyage("GorodetsTrainingScene");
            menu.OpenPause();
            VoyageSave state = menu.CaptureSave();
            state.position += new Vector3(3, 0, 12);
            state.velocity = new Vector3(0.1f, 0, 1.4f);
            state.angularVelocity = new Vector3(0, 0.01f, 0);
            var shipBeforeSave = Object.FindAnyObjectByType<ShipPhysicsController>();
            float ratedRps = shipBeforeSave.Model.Shafts[0].RatedRps;
            state.throttle = 0.6f; state.actualThrottle = 0.4f;
            state.engineCommands = new[] { 0.8f, 0.4f };
            state.shaftRps = new[] { 0.5f * ratedRps, 0.3f * ratedRps };
            state.rudder = -0.3f; state.rudderAngle = -8;
            state.night = true; state.windSpeed = 8; state.rain = 0.7f;
            state.simulationScale = 2;
            state.camera.view = 2; state.camera.pitch = 55; state.camera.distance = 70;
            state.mission.phase = 4; state.mission.score = 76; state.mission.outsideSeconds = 20;
            state.grounding.damage = 3;
            menu.ApplySave(state);
            menu.SaveVoyage();
            VoyageSave saved = VoyageSaveStore.Read(menu.SavePath);
            var ship = Object.FindAnyObjectByType<ShipPhysicsController>();
            ship.SetThrottleCommand(-1);
            ship.Body.position = Vector3.zero;
            menu.ApplySave(saved);
            yield return null;
            Assert.That(Vector3.Distance(ship.Body.position, state.position), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(ship.Body.linearVelocity, state.velocity), Is.LessThan(0.0001f));
            Assert.That(ship.ActualThrottle, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(ship.RudderAngleDeg, Is.EqualTo(-8).Within(1e-4f));
            Assert.That(ship.EngineCommand(0), Is.EqualTo(0.8f));
            Assert.That(ship.EngineCommand(1), Is.EqualTo(0.4f));
            Assert.That(ship.ShaftRpm(1), Is.EqualTo(18f * ratedRps).Within(1e-3f));
            Assert.That(ship.Grounding.DamagePoints, Is.EqualTo(3));
            Assert.That(Object.FindAnyObjectByType<DayNightController>().IsNight, Is.True);
            Assert.That(Object.FindAnyObjectByType<WeatherController>().RainIntensity, Is.EqualTo(0.7f));
            Assert.That(Object.FindAnyObjectByType<ShipFollowCamera>().ViewIndex, Is.EqualTo(2));
            var mission = Object.FindAnyObjectByType<GorodetsScenarioController>();
            Assert.That(mission.Score, Is.EqualTo(76));
            Assert.That(mission.CaptureState().outsideSeconds, Is.EqualTo(20));
            Assert.That(Time.timeScale, Is.Zero);
            menu.Resume();
            Assert.That(Time.timeScale, Is.EqualTo(2));
        }

        private IEnumerator Capture(string name)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/MenuScreenshots"));
            Directory.CreateDirectory(directory);
            Canvas canvas = menu.GetComponentInChildren<Canvas>();
            var cameraObject = new GameObject("Menu capture camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10000);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var target = new RenderTexture(1440, 900, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            yield return null;
            yield return null;
            foreach (Text text in canvas.GetComponentsInChildren<Text>())
                text.font.RequestCharactersInTexture(text.text,
                    Mathf.RoundToInt(text.fontSize * text.pixelsPerUnit), text.fontStyle);
            foreach (Text text in canvas.GetComponentsInChildren<Text>()) text.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            RenderTexture.active = previous;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            camera.targetTexture = null;
            Object.Destroy(image);
            Object.Destroy(target);
            Object.Destroy(cameraObject);
            Assert.That(File.Exists(Path.Combine(directory, name)), Is.True);
        }
    }
}
