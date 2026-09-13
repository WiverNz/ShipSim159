using System;
using System.IO;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Editor
{
    // Dedicated batch check: sails Gorodets at full ahead, then under helm, and renders the
    // wake from several viewpoints into Logs/Wake.
    [InitializeOnLoad]
    public static class ShipWakeRuntimeCheck
    {
        private const string Key = "ShipSimulator.WakeCheck";
        private const string OutputFolder = "Logs/Wake";
        private static double deadline;
        private static double stageStart;
        private static Vector3 stageOrigin;
        private static bool failed;

        static ShipWakeRuntimeCheck()
        {
            deadline = EditorApplication.timeSinceStartup + 480;
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
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Wake runtime check timed out.");
                if (!EditorApplication.isPlaying) return;
                Application.runInBackground = true;
                var menu = UnityEngine.Object.FindAnyObjectByType<VoyageMenu>();
                if (menu == null || !menu.IsReady) return;
                double now = EditorApplication.timeSinceStartup;
                if (stage == 1)
                {
                    menu.StartVoyage("GorodetsTrainingScene");
                    Advance(2, now);
                    return;
                }
                if (VoyageMenu.IsOpen) return;
                var ship = UnityEngine.Object.FindAnyObjectByType<ShipPhysicsController>();
                if (ship == null) return;
                float speed = Vector3.Dot(ship.RelativeWaterVelocity, ship.transform.forward);

                if (stage == 2)
                {
                    if (now - stageStart < 2) return;
                    ship.SetThrottleCommand(1f);
                    UnityEngine.Object.FindAnyObjectByType<SimulationTimeController>()?.SetScale(4f);
                    stageOrigin = ship.transform.position;
                    Advance(3, now);
                }
                else if (stage == 3)
                {
                    // A loaded ship gathers way slowly in 4.6 m of water (about 3.5 m/s at full ahead), so the
                    // capture waits for distance: past 380 m the reach bends to starboard and an unsteered ship
                    // runs onto the shoal inside the bend.
                    float run = Vector3.Distance(ship.transform.position, stageOrigin);
                    if (speed < 3f && run < 380f && now - stageStart < 200) return;
                    Capture(ship, "straight", speed);
                    ship.SetRudderCommand(0.5f);
                    Advance(4, now);
                }
                else if (stage == 4)
                {
                    if (now - stageStart < 25) return;
                    Capture(ship, "turning", speed);
                    int count = Mathf.RoundToInt(Shader.GetGlobalFloat("_WakeCount"));
                    if (count < 4) throw new InvalidOperationException("The wake track was not published: " + count);
                    SessionState.SetInt(Key, 0);
                    Debug.Log($"WAKE_RUNTIME|{(failed ? "FAIL" : "PASS")}: {count} wake points");
                    EditorApplication.Exit(failed ? 1 : 0);
                }
            }
            catch (Exception error)
            {
                SessionState.SetInt(Key, 0);
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Advance(int stage, double now)
        {
            SessionState.SetInt(Key, stage);
            stageStart = now;
        }

        private static void Capture(ShipPhysicsController ship, string label, float speed)
        {
            Directory.CreateDirectory(OutputFolder);
            var cameraObject = new GameObject("Wake preview camera");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 50f;
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 2600f;
                camera.allowHDR = true;
                var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.requiresDepthTexture = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

                Vector3 forward = Vector3.ProjectOnPlane(ship.transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector3 center = ship.transform.position;
                Shoot(camera, center - forward * 170f + right * 80f + Vector3.up * 60f,
                    center - forward * 30f, Vector3.up, label + "-quarter");
                Shoot(camera, center + forward * 100f + right * 34f + Vector3.up * 8f,
                    center + forward * 62f, Vector3.up, label + "-bow");
                Shoot(camera, center - forward * 76f + Vector3.up * 15f,
                    center - forward * 260f, Vector3.up, label + "-aft");
                Shoot(camera, center - forward * 150f + Vector3.up * 360f,
                    center - forward * 150f, forward, label + "-overhead");
                GroundingController grounding = ship.Grounding;
                string contact = grounding != null ? $", {grounding.State}, clearance {grounding.MinimumClearanceM:0.00} m" : string.Empty;
                Debug.Log($"WAKE_RUNTIME|{label}: {speed:0.00} m/s through water at {center}{contact}");
                if (speed < 1f)
                    throw new InvalidOperationException($"The vessel is not under way for the {label} capture.");
                if (grounding != null &&
                    (grounding.State == GroundingState.Touching || grounding.State == GroundingState.HardGrounding))
                    throw new InvalidOperationException($"The vessel is aground during the {label} capture.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        private static void Shoot(Camera camera, Vector3 position, Vector3 target, Vector3 up, string name)
        {
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, up));
            LandscapePreview.Render(camera, OutputFolder + "/" + name + ".png");
        }
    }
}
