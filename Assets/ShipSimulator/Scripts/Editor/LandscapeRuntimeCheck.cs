using System;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Editor
{
    [InitializeOnLoad]
    public static class LandscapeRuntimeCheck
    {
        private const string Key = "ShipSimulator.LandscapeCheck";
        private static double nextCapture;
        private static double deadline;
        private static bool failed;

        static LandscapeRuntimeCheck()
        {
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch editor for this check.");
            SessionState.SetInt(Key, 1);
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity");
            EditorApplication.EnterPlaymode();
        }

        public static void RunFromEmptyScene()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch editor for this check.");
            SessionState.SetInt(Key, 1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            TrainingSceneStartup.ConfigurePlayScene();
            EditorApplication.EnterPlaymode();
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (SessionState.GetInt(Key, 0) != 0 && (type == LogType.Error || type == LogType.Exception)) failed = true;
        }

        private static void Tick()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0) return;
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Landscape runtime check timed out.");
                if (!EditorApplication.isPlaying) return;
                Application.runInBackground = true;
                var menu = UnityEngine.Object.FindAnyObjectByType<VoyageMenu>();
                if (menu == null || !menu.IsReady) return;
                if (stage == 1)
                {
                    menu.StartVoyage("GorodetsTrainingScene");
                    SessionState.SetInt(Key, 2);
                    nextCapture = EditorApplication.timeSinceStartup + 1;
                    return;
                }
                if (EditorApplication.timeSinceStartup < nextCapture) return;
                Camera camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("The game camera is missing.");
                System.IO.Directory.CreateDirectory("Logs/Landscape");
                LandscapePreview.Render(camera, "Logs/Landscape/runtime-" + stage + ".png");
                Texture reflection = Shader.GetGlobalTexture("_RiverPlanarReflection");
                if (reflection == null || Shader.GetGlobalFloat("_RiverReflectionAvailable") < 0.5f)
                    throw new InvalidOperationException("Water reflection was not rendered.");
                if (stage == 2)
                {
                    UnityEngine.Object.FindAnyObjectByType<WeatherController>().Configure(315, 12, 0.7f, 0.35f);
                    SessionState.SetInt(Key, 3);
                }
                else if (stage == 3)
                {
                    UnityEngine.Object.FindAnyObjectByType<DayNightController>().Apply(true);
                    SessionState.SetInt(Key, 4);
                }
                else
                {
                    SessionState.SetInt(Key, 0);
                    Debug.Log("LANDSCAPE_RUNTIME|" + (failed ? "FAIL" : "PASS") + ": daylight, rain/fog, night and planar reflections");
                    EditorApplication.Exit(failed ? 1 : 0);
                }
                nextCapture = EditorApplication.timeSinceStartup + 1;
            }
            catch (Exception error)
            {
                SessionState.SetInt(Key, 0);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
