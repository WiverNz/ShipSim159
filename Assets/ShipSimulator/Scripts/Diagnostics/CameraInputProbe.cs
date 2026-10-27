using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using ShipSimulator.CameraSystem;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace ShipSimulator.Diagnostics
{
    // Opt-in native-player input trace. Does not touch the voyage save.
    public sealed class CameraInputProbe : MonoBehaviour
    {
        private StreamWriter trace;
        private ShipFollowCamera follow;
        private float finishAt, rotationSetting, zoomSetting;
        private bool settingsCaptured;
        private string folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isEditor || !Environment.GetCommandLineArgs().Contains("--camera-input-probe")) return;
            var go = new GameObject("Camera input probe");
            DontDestroyOnLoad(go);
            go.AddComponent<CameraInputProbe>();
        }

        private IEnumerator Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "--camera-output");
            folder = Path.GetFullPath(option >= 0 && option + 1 < args.Length ? args[option + 1] : "Logs/Camera/Input");
            Directory.CreateDirectory(folder);
            float timeout = Time.realtimeSinceStartup + 60;
            VoyageMenu menu;
            do
            {
                yield return null;
                menu = FindAnyObjectByType<VoyageMenu>();
                if (Time.realtimeSinceStartup > timeout) { Application.Quit(1); yield break; }
            } while (menu == null || !menu.IsReady);
            menu.StartVoyage(SceneManager.GetActiveScene().name);
            yield return null;
            follow = FindAnyObjectByType<ShipFollowCamera>();
            if (follow == null || VoyageMenu.IsOpen) { Application.Quit(1); yield break; }
            rotationSetting = VoyageSettings.CameraSensitivity;
            zoomSetting = VoyageSettings.ZoomSensitivity;
            settingsCaptured = true;
            VoyageSettings.SetSensitivity(1);
            VoyageSettings.SetZoomSensitivity(1);
            follow.Target.ManualStepping = true;
            follow.Target.Body.isKinematic = true;
            foreach (Canvas canvas in FindObjectsByType<Canvas>())
                canvas.gameObject.SetActive(false);
            follow.SetView(0);
            follow.RestoreState(follow.CaptureState());
            trace = new StreamWriter(Path.Combine(folder, "input.csv"));
            trace.WriteLine("time,scrollY,deltaX,deltaY,rmb,view,yaw,pitch,distance,x,y,z");
            File.WriteAllText(Path.Combine(folder, "conditions.txt"),
                "Unity=" + Application.unityVersion + "\nPlatform=" + Application.platform +
                "\nScroll=" + InputSystem.settings.scrollDeltaBehavior +
                "\nVessel=" + VesselSwap.IdOf(follow.Target) + "\nSensitivity=1\nZoom=1\n");
            finishAt = Time.realtimeSinceStartup + 14;
            File.WriteAllText(Path.Combine(folder, "ready.txt"), "READY");
            Debug.Log("CAMERA_INPUT|READY");
        }

        private void LateUpdate()
        {
            if (trace == null || follow == null) return;
            var state = follow.CaptureState();
            Mouse mouse = Mouse.current;
            Vector2 delta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            Vector3 position = follow.transform.position;
            trace.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "{0:F5},{1:R},{2:R},{3:R},{4},{5},{6:R},{7:R},{8:R},{9:R},{10:R},{11:R}",
                Time.realtimeSinceStartup, mouse != null ? mouse.scroll.y.ReadValue() : 0,
                delta.x, delta.y, mouse != null && mouse.rightButton.isPressed ? 1 : 0,
                state.view, state.yaw, state.pitch, state.distance, position.x, position.y, position.z));
            if (Time.realtimeSinceStartup < finishAt) return;
            trace.Dispose(); trace = null;
            RestoreSettings();
            File.WriteAllText(Path.Combine(folder, "done.txt"), "DONE");
            Debug.Log("CAMERA_INPUT|DONE");
            Application.Quit(0);
        }

        private void RestoreSettings()
        {
            if (!settingsCaptured) return;
            VoyageSettings.SetSensitivity(rotationSetting);
            VoyageSettings.SetZoomSensitivity(zoomSetting);
            VoyageSettings.Save();
            settingsCaptured = false;
        }
        private void OnDestroy() { trace?.Dispose(); RestoreSettings(); }
        private void OnApplicationQuit() => RestoreSettings();
    }
}
