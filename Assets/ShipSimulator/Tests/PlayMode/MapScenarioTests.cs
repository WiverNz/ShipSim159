using System.Collections;
using System.IO;
using NUnit.Framework;
using ShipSimulator.UI;
using ShipSimulator.Physics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class MapScenarioTests
    {
        private Scene original, voyage;
        private GameObject cameraObject;
        private float range;
        private bool expanded, north, legend;
        private string language;
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (cameraObject != null) Object.Destroy(cameraObject);
            if (original.IsValid()) SceneManager.SetActiveScene(original);
            if (voyage.IsValid() && voyage.isLoaded) yield return SceneManager.UnloadSceneAsync(voyage);
            VoyageSettings.SetMapRange(range); VoyageSettings.SetMapExpanded(expanded);
            VoyageSettings.SetMapNorthUp(north); VoyageSettings.SetMapLegend(legend); VoyageSettings.SetLanguage(language);
        }
        [UnityTest]
        public IEnumerator ChartsMatchEachScenarioAndKeepEnginesAccessible()
        {
            original = SceneManager.GetActiveScene();
            range = VoyageSettings.MapRange; expanded = VoyageSettings.MapExpanded;
            north = VoyageSettings.MapNorthUp; legend = VoyageSettings.MapLegend; language = VoyageSettings.Language;
            Directory.CreateDirectory("Logs/MapPart4");
            foreach (string name in new[] { "RiverTrainingScene", "GorodetsTrainingScene", "SerpukhovZatonScene" })
            {
                yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                voyage = SceneManager.GetSceneByName(name); SceneManager.SetActiveScene(voyage);
                yield return null; yield return null;
                ShipTelemetryUI hud = null; FairwayRoute route = null; GorodetsScenarioController mission = null;
                foreach (var root in voyage.GetRootGameObjects())
                {
                    if (hud == null) hud = root.GetComponentInChildren<ShipTelemetryUI>();
                    if (route == null) route = root.GetComponentInChildren<FairwayRoute>();
                    if (mission == null) mission = root.GetComponentInChildren<GorodetsScenarioController>();
                }
                Assert.That(hud, Is.Not.Null);
                Assert.That(hud.MapFollowing, Is.True, "A new scene must not inherit a panned centre.");
                VoyageSettings.SetMapExpanded(true); VoyageSettings.SetMapLegend(true);
                VoyageSettings.SetMapNorthUp(true); VoyageSettings.SetMapRange(name == "SerpukhovZatonScene" ? 1200 : 600);
                VoyageSettings.SetLanguage("ru");
                yield return null; yield return null;
                Transform world = hud.MapViewport.Find("MovingWorld");
                if (route != null)
                {
                    var startLine = (RectTransform)world.Find("FairwayRoute0");
                    int count = Mathf.Clamp(Mathf.CeilToInt(route.LengthM / 20), 2, 256);
                    Vector2 a = hud.CurrentMapProjection.Project(route.QueryDistance(0).Position);
                    Vector2 b = hud.CurrentMapProjection.Project(route.QueryDistance(route.LengthM / count).Position);
                    Assert.That(Vector2.Distance(startLine.anchoredPosition, (a + b) * .5f), Is.LessThan(.01f));
                    Assert.That(Vector2.Distance(startLine.sizeDelta, new Vector2(2, Vector2.Distance(a, b))), Is.LessThan(.01f));
                }
                Assert.That(world.Find("Shore0"), Is.Not.Null, "The actual scene waterline must be present.");
                if (mission != null)
                {
                    Vector2 actual = ((RectTransform)world.Find("Waypoint")).anchoredPosition;
                    Vector2 target = hud.CurrentMapProjection.Project(mission.NavigationTarget);
                    Assert.That(Vector2.Dot(actual.normalized, target.normalized), Is.GreaterThan(.999f), "An off-chart goal keeps its bearing.");
                }
                cameraObject = new GameObject("Map scenario review camera", typeof(Camera));
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = hud.Ship.transform.position + new Vector3(60, 85, -130);
                camera.transform.LookAt(hud.Ship.transform.position + Vector3.forward * 40);
                camera.fieldOfView = 55; camera.farClipPlane = 3500;
                Canvas canvas = hud.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .5f;
                foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1920, 1080) })
                {
                    var target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
                    camera.targetTexture = target;
                    yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(HudBounds(canvas, "MiniMap").Overlaps(HudBounds(canvas, "TelegraphBlock")), Is.False);
                    camera.Render();
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                    File.WriteAllBytes($"Logs/MapPart4/{name}-{size.x}x{size.y}.png", image.EncodeToPNG());
                    RenderTexture.active = previous; camera.targetTexture = null;
                    Object.Destroy(image); Object.Destroy(target);
                }
                Object.Destroy(cameraObject); cameraObject = null;
                SceneManager.SetActiveScene(original); yield return SceneManager.UnloadSceneAsync(voyage);
            }
        }
        private static Rect HudBounds(Canvas canvas, string name)
        {
            var rect = (RectTransform)canvas.transform.Find(name);
            Assert.That(rect.gameObject.activeInHierarchy, Is.True);
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector3 min = canvas.transform.InverseTransformPoint(corners[0]), max = min;
            foreach (var corner in corners)
            { var p = canvas.transform.InverseTransformPoint(corner); min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
