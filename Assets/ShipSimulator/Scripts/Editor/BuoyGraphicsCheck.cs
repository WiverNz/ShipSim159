using System;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Editor
{
    [InitializeOnLoad]
    public static class BuoyGraphicsCheck
    {
        private const string Key = "ShipSimulator.BuoyCheck";
        private static double nextCapture;
        private static double deadline;
        private static bool failed;
        private static Camera camera;
        private static RenderTexture target;
        private static Transform buoy;

        static BuoyGraphicsCheck()
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

        private static void SetFlash(bool lit)
        {
            var flasher = buoy.GetComponentInChildren<NavigationBeaconFlasher>();
            flasher.enabled = false;
            var lens = flasher.GetComponentInChildren<Renderer>(true);
            flasher.Configure(flasher.GetComponent<Light>(), lens, 2.5f, 0.35f, -Time.time + (lit ? 0.1f : 1f));
            flasher.SetNight(true);
            if (flasher.IsLit != lit || lens.enabled != lit) throw new InvalidOperationException("Flash state mismatch.");
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
                if (camera == null)
                {
                    camera = Camera.main;
                    camera.GetComponent<ShipSimulator.CameraSystem.ShipFollowCamera>().enabled = false;
                    QualitySettings.SetQualityLevel(1);
                    buoy = GameObject.Find("Navigation").transform.GetComponentInChildren<BuoyVisualRig>().transform;
                    camera.transform.position = buoy.position + new Vector3(4, 3.7f, -6);
                    camera.transform.LookAt(buoy.position + Vector3.up * 1.6f);
                    target = new RenderTexture(1200, 900, 24, RenderTextureFormat.ARGBHalf);
                    camera.targetTexture = target;
                    nextCapture = EditorApplication.timeSinceStartup + 2;
                    return;
                }
                if (camera == null) throw new InvalidOperationException("The game camera is missing.");
                System.IO.Directory.CreateDirectory("Logs/Buoys");
                var ldr = RenderTexture.GetTemporary(1200, 900, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(target, ldr);
                var previous = RenderTexture.active;
                RenderTexture.active = ldr;
                var picture = new Texture2D(1200, 900, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); picture.Apply();
                System.IO.File.WriteAllBytes("Logs/Buoys/buoy-" + stage + ".png", picture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(picture);
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(ldr);
                Texture reflection = Shader.GetGlobalTexture("_RiverPlanarReflection");
                if (reflection == null || Shader.GetGlobalFloat("_RiverReflectionAvailable") < 0.5f)
                    throw new InvalidOperationException("Water reflection was not rendered.");
                if (stage == 2)
                {
                    UnityEngine.Object.FindAnyObjectByType<DayNightController>().Apply(true);
                    SetFlash(true);
                    SessionState.SetInt(Key, 3);
                }
                else if (stage == 3)
                {
                    SetFlash(false);
                    SessionState.SetInt(Key, 4);
                }
                else if (stage == 4)
                {
                    camera.transform.position = buoy.position + new Vector3(15, 5, -70);
                    camera.transform.LookAt(buoy.position + Vector3.up * 2);
                    SetFlash(true);
                    SessionState.SetInt(Key, 5);
                }
                else if (stage == 5)
                {
                    SetFlash(false);
                    SessionState.SetInt(Key, 6);
                }
                else
                {
                    SessionState.SetInt(Key, 0);
                    Debug.Log("BUOY_GRAPHICS|" + (failed ? "FAIL" : "PASS") + ": daylight and night flash on/off");
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
