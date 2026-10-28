using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShipSimulator.Diagnostics
{
    // Opt-in standalone check of real OS window transitions, without touching voyage saves.
    public sealed class HudWindowProbe : MonoBehaviour
    {
        private readonly Dictionary<string, int> settings = new();
        private readonly string[] keys = { "Fullscreen", "WindowWidth", "WindowHeight" };
        private bool captured;
        private string folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isEditor || Array.IndexOf(Environment.GetCommandLineArgs(), "--hud-window-probe") < 0) return;
            var go = new GameObject("HUD window probe");
            DontDestroyOnLoad(go);
            go.AddComponent<HudWindowProbe>();
        }

        private IEnumerator Start()
        {
            var run = Run();
            while (true)
            {
                bool more = false;
                Exception failure = null;
                try { more = run.MoveNext(); }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    Debug.LogException(failure);
                    Restore();
                    Application.Quit(1);
                    yield break;
                }
                if (!more) break;
                yield return run.Current;
            }
            Restore();
            Debug.Log("HUD_WINDOW|PASS");
            Application.Quit(0);
        }

        private IEnumerator Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "--hud-window-output");
            folder = Path.GetFullPath(option >= 0 && option + 1 < args.Length ? args[option + 1] : "Logs/HudWindows");
            Directory.CreateDirectory(folder);
            foreach (string key in keys)
                if (PlayerPrefs.HasKey("ShipSim159.Settings." + key))
                    settings[key] = PlayerPrefs.GetInt("ShipSim159.Settings." + key);
            captured = true;
            float deadline = Time.realtimeSinceStartup + 60;
            VoyageMenu menu;
            do
            {
                yield return null;
                menu = FindAnyObjectByType<VoyageMenu>();
                Require(Time.realtimeSinceStartup < deadline, "Voyage menu startup timed out.");
            } while (menu == null || !menu.IsReady);
            menu.StartVoyage(SceneManager.GetActiveScene().name);
            yield return null;
            var hud = FindAnyObjectByType<ShipTelemetryUI>();
            Require(hud != null && !VoyageMenu.IsOpen, "Voyage did not start.");
            hud.Ship.ManualStepping = true;
            hud.Ship.Body.isKinematic = true;
            var layout = hud.GetComponent<HudLayout>();
            foreach (var size in new[] { new Vector2Int(800, 600), new Vector2Int(1024, 768),
                new Vector2Int(1280, 720), new Vector2Int(1920, 1080), Vector2Int.zero, new Vector2Int(800, 600) })
            {
                bool fullscreen = size == Vector2Int.zero;
                if (fullscreen) VoyageSettings.ToggleFullscreen();
                else VoyageSettings.SetWindowSize(size.x, size.y);
                deadline = Time.realtimeSinceStartup + 15;
                var monitor = Screen.mainWindowDisplayInfo;
                Vector2Int expected = fullscreen ? new Vector2Int(monitor.width, monitor.height) :
                    VoyageSettings.FitWindow(size, new Vector2Int(monitor.workArea.width, monitor.workArea.height));
                do
                {
                    yield return null;
                    Require(Time.realtimeSinceStartup < deadline, "Window transition timed out: " + size);
                } while (Screen.width != expected.x || Screen.height != expected.y || Screen.fullScreen != fullscreen);
                for (int group = 0; group < 6; group++)
                {
                    layout.Select(group);
                    for (int frame = 0; frame < 5; frame++) yield return null;
                    Check(hud);
                }
                layout.Select(0);
                for (int frame = 0; frame < 5; frame++) yield return null;
                var buttons = hud.transform.Find("TelegraphBlock").GetComponentsInChildren<HudButton>();
                var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                buttons[0].OnPointerClick(click);
                Require(hud.Ship.EngineCommand(0) < 0, "Astern command failed.");
                buttons[6].OnPointerClick(click);
                Require(hud.Ship.EngineCommand(0) > 0, "Ahead command failed.");
                buttons[3].OnPointerClick(click);
                Require(hud.Ship.EngineCommand(0) == 0, "Stop command failed.");
                string name = fullscreen ? "fullscreen" : "window-" + size.x + "x" + size.y;
                yield return new WaitForEndOfFrame();
                var image = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
                Destroy(image);
                File.AppendAllText(Path.Combine(folder, "results.txt"), name + ": " + Screen.width + "x" + Screen.height +
                    " " + Screen.fullScreenMode + " PASS\n");
            }
        }

        private static Rect BoundsIn(RectTransform root, RectTransform item)
        {
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            Vector3 min = root.InverseTransformPoint(corners[0]), max = min;
            foreach (var corner in corners)
            {
                Vector3 point = root.InverseTransformPoint(corner);
                min = Vector3.Min(min, point); max = Vector3.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void Check(ShipTelemetryUI hud)
        {
            var root = (RectTransform)hud.transform;
            var engine = root.Find("TelegraphBlock") as RectTransform;
            var map = root.Find("MiniMap") as RectTransform;
            Require(engine != null && map != null, "Essential panels left the HUD root.");
            Rect a = BoundsIn(root, engine), b = BoundsIn(root, map);
            foreach (var panel in new[] { engine, map })
            {
                Require(panel.gameObject.activeInHierarchy, "Essential panel hidden.");
                Rect bounds = BoundsIn(root, panel);
                Require(root.rect.Contains(bounds.min) && root.rect.Contains(bounds.max), "Essential panel off screen.");
            }
            Require(!a.Overlaps(b), "Essential panels overlap.");
            var drawer = root.Find("Control drawer") as RectTransform;
            if (drawer.gameObject.activeInHierarchy)
            {
                Rect c = BoundsIn(root, drawer);
                Require(!c.Overlaps(a) && !c.Overlaps(b), "Tools overlap essential controls.");
                Require(!c.Contains(root.rect.center), "Tools cover the centre of the view.");
            }
            Require(map.Find("Viewport/Vessel/Hull").gameObject.activeInHierarchy, "Own-vessel marker hidden.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Restore()
        {
            if (!captured) return;
            captured = false;
            foreach (string key in keys)
            {
                if (settings.TryGetValue(key, out int value)) PlayerPrefs.SetInt("ShipSim159.Settings." + key, value);
                else PlayerPrefs.DeleteKey("ShipSim159.Settings." + key);
            }
            VoyageSettings.Save();
        }
        private void OnApplicationQuit() => Restore();
        private void OnDestroy() => Restore();
    }
}
