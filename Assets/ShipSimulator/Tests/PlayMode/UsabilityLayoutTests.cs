using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShipSimulator.Tests
{
    public sealed class UsabilityLayoutTests
    {
        private static readonly List<string> layoutFailures = new();
        private Scene original;
        private Scene scene;
        private string vessel;
        private float originalScale, originalPanelScale;
        private string originalLanguage;
        private VoyageMenu menu;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            VoyageSettings.SetLanguage(originalLanguage);
            VoyageSettings.SetUiScale(originalScale);
            VoyageSettings.SetPanelScale(originalPanelScale);
            VoyageSettings.Save();
            if (menu != null) Object.Destroy(menu.gameObject);
            foreach (Camera camera in Object.FindObjectsByType<Camera>())
                if (camera.name == "UI capture camera")
                {
                    if (camera.targetTexture != null) Object.Destroy(camera.targetTexture);
                    Object.Destroy(camera.gameObject);
                }
            yield return null;
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            VesselSelection.SelectedId = vessel;
            Time.timeScale = 1;
            AudioListener.pause = false;
        }

        [UnityTest]
        public IEnumerator CaptureLayouts()
        {
            layoutFailures.Clear();
            original = SceneManager.GetActiveScene();
            originalScale = VoyageSettings.UiScale;
            originalPanelScale = VoyageSettings.PanelScale;
            originalLanguage = VoyageSettings.Language;
            vessel = VesselSelection.SelectedId;
            VesselSelection.SelectedId = "volgodon-507b";
            yield return SceneManager.LoadSceneAsync("GorodetsTrainingScene", LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByName("GorodetsTrainingScene");
            SceneManager.SetActiveScene(scene);
            menu = new GameObject("Usability menu").AddComponent<VoyageMenu>();
            menu.BindScene();
            yield return null;
            yield return null;
            var hud = Object.FindAnyObjectByType<ShipTelemetryUI>();
            int[,] sizes = { {800,600}, {960,540}, {1024,768}, {1280,720}, {1920,1080}, {2560,1440}, {3440,1440}, {1280,1024} };
            foreach (string language in Localization.Languages)
            foreach (float scale in new[] { .8f, 1f, 1.5f })
            for (int i = 0; i < sizes.GetLength(0); i++)
            {
                VoyageSettings.SetLanguage(language);
                VoyageSettings.SetUiScale(scale);
                VoyageSettings.SetPanelScale(scale == .8f ? .75f : scale == 1f ? .85f : 1.15f);
                int w = sizes[i,0], h = sizes[i,1];
                menu.ShowHome();
                yield return Capture(menu.GetComponentInChildren<Canvas>(), w, h, "menu");
                menu.ShowVessels();
                yield return Capture(menu.GetComponentInChildren<Canvas>(), w, h, "vessels");
                menu.ShowSound();
                yield return Capture(menu.GetComponentInChildren<Canvas>(), w, h, "sound");
                menu.ShowLanguage();
                yield return Capture(menu.GetComponentInChildren<Canvas>(), w, h, "language");
                menu.ShowDisplay();
                yield return Capture(menu.GetComponentInChildren<Canvas>(), w, h, "display");
                menu.ShowWindowSizes();
                yield return Capture(menu.GetComponentInChildren<Canvas>(), w, h, "window-sizes");
                menu.ShowVoyages();
                yield return Capture(menu.GetComponentInChildren<Canvas>(), w, h, "passages");
                menu.ShowPanels();
                yield return Capture(menu.GetComponentInChildren<Canvas>(), w, h, "panels");
                menu.ShowInterface();
                yield return Capture(menu.GetComponentInChildren<Canvas>(), w, h, "settings");
                if (!menu.HasVoyage) menu.StartVoyage("GorodetsTrainingScene"); else menu.Resume();
                Time.timeScale = 0;
                yield return null;
                yield return Capture(hud.GetComponent<Canvas>(), w, h, "hud");
                int engineMode = VoyageSettings.PanelMode("TelegraphBlock");
                try
                {
                    VoyageSettings.SetPanelMode("TelegraphBlock", 2);
                    hud.GetComponent<HudLayout>().Select(5);
                    yield return Capture(hud.GetComponent<Canvas>(), w, h, "help");
                }
                finally { VoyageSettings.SetPanelMode("TelegraphBlock", engineMode); }
                hud.GetComponent<HudLayout>().Select(1);
                yield return Capture(hud.GetComponent<Canvas>(), w, h, "deck");
                hud.Ship.ConfigureLoading(0);
                hud.Ship.SetLoadingCommand(0, -1);
                hud.Ship.Simulate(.02f);
                yield return Capture(hud.GetComponent<Canvas>(), w, h, "deck-empty");
                hud.Ship.StopLoading();
                hud.Ship.ConfigureLoading(1);
                hud.GetComponent<HudLayout>().Select(2);
                yield return Capture(hud.GetComponent<Canvas>(), w, h, "camera");
                hud.GetComponent<HudLayout>().Select(3);
                yield return Capture(hud.GetComponent<Canvas>(), w, h, "weather");
                hud.GetComponent<HudLayout>().Select(4);
                yield return Capture(hud.GetComponent<Canvas>(), w, h, "map");
                hud.GetComponent<HudLayout>().Select(0);
                menu.OpenPause();
            }
            File.WriteAllLines("Logs/Localization/layout-failures.txt", layoutFailures);
            Assert.That(layoutFailures, Is.Empty, string.Join("\n", layoutFailures));
        }

        private static Rect BoundsIn(Canvas canvas, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 min = canvas.transform.InverseTransformPoint(corners[0]);
            Vector3 max = min;
            foreach (var corner in corners)
            {
                var point = canvas.transform.InverseTransformPoint(corner);
                min = Vector3.Min(min, point); max = Vector3.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AssertPermanentControls(Canvas canvas)
        {
            var root = (RectTransform)canvas.transform;
            var engine = root.Find("TelegraphBlock") as RectTransform;
            var map = root.Find("MiniMap") as RectTransform;
            Assert.That(engine, Is.Not.Null, "Engines must be outside every tab and scroll viewport.");
            Assert.That(map, Is.Not.Null, "Map must be outside every tab and scroll viewport.");
            foreach (var panel in new[] { engine, map })
            {
                Assert.That(panel.gameObject.activeInHierarchy, Is.True);
                var bounds = BoundsIn(canvas, panel);
                Assert.That(bounds.xMin, Is.GreaterThanOrEqualTo(root.rect.xMin - 1));
                Assert.That(bounds.xMax, Is.LessThanOrEqualTo(root.rect.xMax + 1));
                Assert.That(bounds.yMin, Is.GreaterThanOrEqualTo(root.rect.yMin - 1));
                Assert.That(bounds.yMax, Is.LessThanOrEqualTo(root.rect.yMax + 1));
                Assert.That(panel.GetComponentInParent<ScrollRect>(), Is.Null);
            }
            var engineBounds = BoundsIn(canvas, engine);
            var mapBounds = BoundsIn(canvas, map);
            Assert.That(engineBounds.Overlaps(mapBounds), Is.False);
            var drawer = root.Find("Control drawer") as RectTransform;
            if (drawer.gameObject.activeInHierarchy)
            {
                var bounds = BoundsIn(canvas, drawer);
                Assert.That(bounds.Overlaps(engineBounds), Is.False, "Tools overlap engine controls.");
                Assert.That(bounds.Overlaps(mapBounds), Is.False, "Tools overlap map.");
                Assert.That(bounds.Contains(root.rect.center), Is.False, "Keep the centre of the view clear.");
            }
            var marker = map.Find("Viewport/Vessel/Hull") as RectTransform;
            Assert.That(marker.gameObject.activeInHierarchy, Is.True);
            var markerBounds = BoundsIn(canvas, marker);
            var chart = BoundsIn(canvas, map.Find("Viewport") as RectTransform);
            Assert.That(chart.Contains(markerBounds.min) && chart.Contains(markerBounds.max), Is.True);
            Assert.That(engine.GetComponentsInChildren<HudButton>().Length, Is.EqualTo(7));
        }

        private static IEnumerator Capture(Canvas canvas, int width, int height, string name)
        {
            string folder = Path.GetFullPath("Logs/Localization/" + Localization.Language + "/" + Mathf.RoundToInt(VoyageSettings.UiScale * 100));
            Directory.CreateDirectory(folder);
            var cameraObject = new GameObject("UI capture camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10000);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.03f, .06f, .08f);
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (Text text in canvas.GetComponentsInChildren<Text>())
            {
                text.font.RequestCharactersInTexture(text.text, Mathf.RoundToInt(text.fontSize * text.pixelsPerUnit), text.fontStyle);
                text.SetAllDirty();
            }
            yield return null;
            Canvas.ForceUpdateCanvases();
            foreach (Text label in canvas.GetComponentsInChildren<Text>())
                if (label.rectTransform.rect.height > 0 && label.preferredHeight > label.rectTransform.rect.height + 2)
                    layoutFailures.Add(Localization.Language + " " + VoyageSettings.UiScale + " " + name + " " + width + "x" + height + " " + label.transform.parent.name + "/" + label.name + " clipped: " + label.preferredHeight + ">" + label.rectTransform.rect.height + " " + label.text);
            foreach (HudButton button in canvas.GetComponentsInChildren<HudButton>())
            {
                var rect = (RectTransform)button.transform;
                var parent = (RectTransform)rect.parent;
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Bounds bounds = new Bounds(parent.InverseTransformPoint(corners[0]), Vector3.zero);
                foreach (Vector3 corner in corners) bounds.Encapsulate(parent.InverseTransformPoint(corner));
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(parent.rect.xMin - 1), button.name);
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(parent.rect.xMax + 1), button.name);
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(parent.rect.yMin - 1), button.name);
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(parent.rect.yMax + 1), button.name);
            }
            if (canvas.GetComponent<HudLayout>() != null) AssertPermanentControls(canvas);
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, $"{name}-{width}x{height}.png"), image.EncodeToPNG());
            RenderTexture.active = previous;
            foreach (ScrollRect scroll in canvas.GetComponentsInChildren<ScrollRect>())
            {
                if (scroll.content.rect.height <= scroll.viewport.rect.height) continue;
                scroll.verticalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases();
                var corners = new Vector3[4];
                scroll.content.GetWorldCorners(corners);
                Bounds bounds = new Bounds(scroll.viewport.InverseTransformPoint(corners[0]), Vector3.zero);
                foreach (Vector3 corner in corners) bounds.Encapsulate(scroll.viewport.InverseTransformPoint(corner));
                Assert.That(bounds.min.y, Is.EqualTo(scroll.viewport.rect.yMin).Within(2), "The bottom controls must be reachable.");
            }
            if (name == "deck" || name == "settings")
            {
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(folder, $"{name}-bottom-{width}x{height}.png"), image.EncodeToPNG());
                RenderTexture.active = previous;
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            camera.targetTexture = null;
            Object.Destroy(image);
            Object.Destroy(target);
            Object.Destroy(cameraObject);
            Assert.That(File.Exists(Path.Combine(folder, $"{name}-{width}x{height}.png")), Is.True);
        }
    }
}
