using System;
using System.IO;
using System.Linq;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShipSimulator.Editor
{
    // Runs outside the test runner so Single scene loads cannot unload its coroutine.
    [InitializeOnLoad]
    public static class VoyageMenuSmokeCheck
    {
        private const string ActiveKey = "ShipSimulator.MenuSmoke.Active";
        private const string StageKey = "ShipSimulator.MenuSmoke.Stage";
        private static double deadline;
        private static string previousStatus;

        static VoyageMenuSmokeCheck()
        {
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Run this smoke check in a dedicated batch-mode editor.");
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(StageKey, 0);
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/RiverTrainingScene.unity");
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("The menu smoke check did not finish within 120 seconds.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                Application.runInBackground = true;
                VoyageMenu menu = UnityEngine.Object.FindAnyObjectByType<VoyageMenu>();
                string status = $"scene={SceneManager.GetActiveScene().name}, menu={menu != null}, ready={menu != null && menu.IsReady}, open={VoyageMenu.IsOpen}";
                if (status != previousStatus) { Debug.Log("MENU_SMOKE|" + status); previousStatus = status; }
                if (menu == null || !menu.IsReady) return;
                int stage = SessionState.GetInt(StageKey, 0);
                string scene = SceneManager.GetActiveScene().name;
                switch (stage)
                {
                    case 0:
                        if (!VoyageMenu.IsOpen) return;
                        menu.SavePath = Path.GetFullPath("Logs/menu-smoke-voyage.json");
                        menu.StartVoyage("RiverTrainingScene");
                        if (!menu.HasVoyage || VoyageMenu.IsOpen) return;
                        menu.OpenPause();
                        ReviewMechanisms(UnityEngine.Object.FindAnyObjectByType<ShipPhysicsController>());
                        ReviewLoading(UnityEngine.Object.FindAnyObjectByType<ShipPhysicsController>());
                        var save = menu.CaptureSave();
                        save.position = new Vector3(2, save.position.y, 35);
                        save.throttle = 0.5f;
                        save.actualThrottle = 0.35f;
                        // Exercises the shared-throttle fallback used by saves older than per-engine state.
                        save.engineCommands = null;
                        save.shaftRps = null;
                        save.simulationScale = 2;
                        menu.ApplySave(save);
                        menu.SaveVoyage();
                        if (!File.Exists(menu.SavePath)) throw new Exception("Save button did not write a voyage.");
                        Debug.Log("MENU_SMOKE|Saved river voyage");
                        SessionState.SetInt(StageKey, 1);
                        VesselSelection.SelectedId = "volgoneft-1577";
                        menu.StartVoyage("GorodetsTrainingScene");
                        break;
                    case 1:
                        if (scene != "GorodetsTrainingScene" || VoyageMenu.IsOpen) return;
                        if (!menu.HasVoyage) throw new Exception("New scenario has no active voyage.");
                        ShipPhysicsController tanker = UnityEngine.Object.FindAnyObjectByType<ShipPhysicsController>();
                        if (VesselSwap.IdOf(tanker) != "volgoneft-1577" || tanker.Data == null || tanker.Data.identity.project != "1577")
                            throw new Exception("The chosen vessel was not placed in the scenario.");
                        Debug.Log("MENU_SMOKE|Started Gorodets passage in the Volgoneft 1577");
                        SessionState.SetInt(StageKey, 2);
                        menu.OpenPause();
                        menu.LoadVoyage();
                        break;
                    case 2:
                        if (scene != "RiverTrainingScene" || VoyageMenu.IsOpen) return;
                        menu.OpenPause();
                        ShipPhysicsController ship = UnityEngine.Object.FindAnyObjectByType<ShipPhysicsController>();
                        if (VesselSwap.IdOf(ship) != VesselCatalogue.DefaultVesselId)
                            throw new Exception("Loading the saved voyage did not restore its vessel.");
                        if (Mathf.Abs(ship.Body.position.z - 35) > 1f || Mathf.Abs(ship.ThrottleCommand - 0.5f) > 0.001f)
                            throw new Exception("Cross-scene load did not restore the saved vessel.");
                        if (Mathf.Abs(ship.ActualThrottle - 0.35f) > 0.05f)
                            throw new Exception("Cross-scene load lost engine response state.");
                        if (!ship.HasLoading || Mathf.Abs(ship.CurrentMassKg - SessionState.GetFloat("ShipSim.Part7Mass", 0)) > 1 ||
                            ship.CaptureLoading().commands[2] != 1)
                            throw new Exception("Cross-scene load lost compartment inventory or transfer command.");
                        if (ship.Mechanisms == null || ship.Mechanisms.Target != HatchPosition.ForwardOpen ||
                            Mathf.Abs(ship.Mechanisms.Opening - SessionState.GetFloat("ShipSim.Part13Opening", -1)) > .02f ||
                            ship.GroundTackle == null ||
                            Mathf.Abs(ship.GroundTackle.ChainOutM(0) - SessionState.GetFloat("ShipSim.Part13Chain", -1)) > .05f ||
                            ship.GroundTackle.Phase(0) != AnchorPhase.Bottom)
                            throw new Exception("Cross-scene load lost deck machinery state.");
                        menu.HandleEscape();
                        if (Time.timeScale != 2) throw new Exception("Resume lost saved simulation speed.");
                        Debug.Log("MENU_SMOKE|PASS: start, save, change scenario and vessel, load, pause and resume");
                        SessionState.SetBool(ActiveKey, false);
                        EditorApplication.Exit(0);
                        break;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                SessionState.SetBool(ActiveKey, false);
                EditorApplication.Exit(1);
            }
        }

        private static void ReviewLoading(ShipPhysicsController ship)
        {
            ship.ConfigureLoading(.5f);
            var hud = UnityEngine.Object.FindAnyObjectByType<ShipTelemetryUI>();
            hud.GetComponent<HudLayout>().Select(1);
            var buttons = hud.GetComponentsInChildren<HudButton>();
            buttons.Single(b => b.Label.text == "EMPTY").OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            float mass = ship.CurrentMassKg;
            ship.Simulate(.02f);
            if (ship.CurrentMassKg != mass) throw new Exception("Cargo transfer must wait for accessible hatches.");
            buttons.Single(b => b.Label.text == "STOP ALL").OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            buttons.Single(b => b.Label.text == "SELECT COMPARTMENT").OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            buttons.Single(b => b.Label.text == "SELECT COMPARTMENT").OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            buttons.Single(b => b.Label.text == "FILL").OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            ship.Simulate(.02f);
            if (Mathf.Abs(ship.CurrentMassKg - mass - 1) > .5f)
                throw new Exception("HUD ballast command did not add the rate-limited mass.");
            SessionState.SetFloat("ShipSim.Part7Mass", ship.CurrentMassKg);
            Debug.Log("VOLGODON_LOADING_UI|PASS: hatch interlock, compartment selection and ballast transfer");
        }

        private static void ReviewMechanisms(ShipPhysicsController ship)
        {
            var rig = ship.Mechanisms;
            if (rig == null || !rig.IsConfigured) throw new Exception("Playable vessel has no mechanism bindings.");
            Vector3 position = ship.transform.position;
            Quaternion rotation = ship.transform.rotation;
            var camera = new GameObject("Mechanism runtime review").AddComponent<Camera>();
            var hud = UnityEngine.Object.FindAnyObjectByType<ShipTelemetryUI>().GetComponent<Canvas>();
            hud.GetComponent<HudLayout>().Select(1);
            CanvasGroup hudGroup = hud.GetComponent<CanvasGroup>();
            float previousAlpha = hudGroup != null ? hudGroup.alpha : 1;
            try
            {
                if (hudGroup != null) hudGroup.alpha = 1;
                camera.farClipPlane = 3000;
                camera.orthographic = true;
                camera.orthographicSize = 55;
                camera.transform.position = ship.transform.TransformPoint(new Vector3(115, 115, 65));
                camera.transform.LookAt(ship.transform.TransformPoint(new Vector3(0, 0, 5)));
                hud.renderMode = RenderMode.ScreenSpaceCamera;
                hud.worldCamera = camera;
                hud.planeDistance = 1;
                foreach (Text text in hud.GetComponentsInChildren<Text>())
                {
                    text.font?.RequestCharactersInTexture(text.text, Mathf.RoundToInt(text.fontSize * text.pixelsPerUnit), text.fontStyle);
                    text.SetAllDirty();
                }
                Canvas.ForceUpdateCanvases();
                Directory.CreateDirectory("Logs/MenuScreenshots");
                foreach (HatchPosition target in new[] { HatchPosition.ForwardOpen, HatchPosition.AftOpen, HatchPosition.Closed })
                {
                    string caption = target == HatchPosition.ForwardOpen ? "FORE OPEN" : target == HatchPosition.AftOpen ? "AFT OPEN" : "CLOSE";
                    hud.GetComponentsInChildren<HudButton>().Single(b => b.Label.text == caption).OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                    if (rig.Target != target) throw new Exception("HUD machinery command did not reach the vessel.");
                    for (int i = 0; i < 1000; i++) rig.Advance(1, 0, 0, 0);
                    if (rig.IsMoving) throw new Exception("Hatches never reached " + target);
                    LandscapePreview.Render(camera, "Logs/MenuScreenshots/volgodon-machinery-" + target + ".png");
                }
                var lights = ship.GetComponent<NavigationLightRig>();
                lights.EnsureCreated();
                lights.SetNight(true);
                int enabledLights = 0;
                foreach (Light light in ship.GetComponentsInChildren<Light>())
                    if (light.enabled && light.cookie != null) enabledLights++;
                if (enabledLights != 5) throw new Exception("Expected five sector navigation lights.");
                UnityEngine.Object.FindAnyObjectByType<DayNightController>().Apply(true);
                LandscapePreview.Render(camera, "Logs/MenuScreenshots/volgodon-machinery-night.png");
                UnityEngine.Object.FindAnyObjectByType<DayNightController>().Apply(false);
                ship.SetHatchPosition(HatchPosition.ForwardOpen);
                hud.GetComponentsInChildren<HudButton>().Single(b => b.Label.text == "LET GO").OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
                rig.Advance(25, 0, 0, 0);
                // One long step lets the port anchor run out to the bottom, where the brake is set.
                ship.GroundTackle.Step(25);
                if (ship.GroundTackle.Phase(0) != AnchorPhase.Bottom) throw new Exception("The port anchor did not reach the bottom.");
                SessionState.SetFloat("ShipSim.Part13Opening", rig.Opening);
                SessionState.SetFloat("ShipSim.Part13Chain", ship.GroundTackle.ChainOutM(0));
                if (Vector3.Distance(position, ship.transform.position) > .001f ||
                    Quaternion.Angle(rotation, ship.transform.rotation) > .01f)
                    throw new Exception("Machinery changed vessel root pose.");
                Debug.Log("VOLGODON_RUNTIME|PASS: hatch states, anchor release, root invariance, sector lights and captures");
            }
            finally
            {
                hud.renderMode = RenderMode.ScreenSpaceOverlay;
                hud.worldCamera = null;
                if (hudGroup != null) hudGroup.alpha = previousAlpha;
                UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
