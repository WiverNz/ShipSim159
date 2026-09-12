using System;
using System.IO;
using ShipSimulator.Physics;
using ShipSimulator.UI;
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
                        var save = menu.CaptureSave();
                        save.position = new Vector3(2, save.position.y, 35);
                        save.throttle = 0.5f;
                        save.actualThrottle = 0.35f;
                        save.simulationScale = 2;
                        menu.ApplySave(save);
                        menu.SaveVoyage();
                        if (!File.Exists(menu.SavePath)) throw new Exception("Save button did not write a voyage.");
                        Debug.Log("MENU_SMOKE|Saved river voyage");
                        SessionState.SetInt(StageKey, 1);
                        menu.StartVoyage("GorodetsTrainingScene");
                        break;
                    case 1:
                        if (scene != "GorodetsTrainingScene" || VoyageMenu.IsOpen) return;
                        if (!menu.HasVoyage) throw new Exception("New scenario has no active voyage.");
                        Debug.Log("MENU_SMOKE|Started Gorodets passage");
                        SessionState.SetInt(StageKey, 2);
                        menu.OpenPause();
                        menu.LoadVoyage();
                        break;
                    case 2:
                        if (scene != "RiverTrainingScene" || VoyageMenu.IsOpen) return;
                        menu.OpenPause();
                        ShipPhysicsController ship = UnityEngine.Object.FindAnyObjectByType<ShipPhysicsController>();
                        if (Mathf.Abs(ship.Body.position.z - 35) > 1f || Mathf.Abs(ship.ThrottleCommand - 0.5f) > 0.001f)
                            throw new Exception("Cross-scene load did not restore the saved vessel.");
                        if (Mathf.Abs(ship.ActualThrottle - 0.35f) > 0.05f)
                            throw new Exception("Cross-scene load lost engine response state.");
                        menu.HandleEscape();
                        if (Time.timeScale != 2) throw new Exception("Resume lost saved simulation speed.");
                        Debug.Log("MENU_SMOKE|PASS: start, save, change scenario, load, pause and resume");
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
    }
}
