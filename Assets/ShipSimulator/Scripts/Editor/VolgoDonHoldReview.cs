using System;
using System.IO;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // Part 12 hold captures in the Gorodets river water: hatch states, loading conditions, views and LODs.
    // The vessel is placed at its solved draft and trim without simulating; the scene is not saved.
    // Run with -part12Stage before|after; writes Previews/part12-<stage>-hold-*.png, ends HOLD_REVIEW|PASS.
    public static class VolgoDonHoldReview
    {
        private const string Output = "Art/Source/VolgoDon507B/Previews";

        public static void Run()
        {
            try
            {
                string stage = Argument("-part12Stage") ?? "after";
                Capture(stage);
                Debug.Log("HOLD_REVIEW|PASS " + stage);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture(string stage)
        {
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity");
            var ship = Object.FindAnyObjectByType<ShipPhysicsController>();
            if (ship == null) throw new InvalidOperationException("No vessel in Gorodets.");
            GameObject vessel = ship.gameObject;
            Vector3 start = vessel.transform.position;
            Quaternion startRotation = vessel.transform.rotation;
            var rig = vessel.GetComponent<VesselMechanisms>();
            var group = vessel.GetComponentInChildren<LODGroup>();
            // The baseline has neither masks nor cargo; without a mask the water's stencil test passes everywhere,
            // so it renders exactly as the shader did before the test was added. Edit mode never runs Awake.
            HoldInteriorVisual cargo = stage == "before" ? null : vessel.GetComponent<HoldInteriorVisual>();
            VesselData data = JsonUtility.FromJson<VesselData>(
                File.ReadAllText("Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json"));

            var cameraObject = new GameObject("Part 12 hold camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 50;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2500;
            camera.allowHDR = true;
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.requiresDepthTexture = true;
            cameraData.requiresColorTexture = true;
            Directory.CreateDirectory(Output);

            vessel.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            rig.SetHatches(HatchPosition.Closed);
            rig.Advance(400, 0, 0, 0);
            var bounds = new System.Text.StringBuilder();
            foreach (Renderer renderer in vessel.GetComponentsInChildren<Renderer>())
            {
                string n = renderer.gameObject.name;
                if (!(n.StartsWith("Hold") || n.StartsWith("Hatch") || n.StartsWith("Panel") || n.StartsWith("Jack") ||
                      n.StartsWith("Seal") || n.StartsWith("VD507B_hold") || n.StartsWith("VD507B_deck"))) continue;
                Bounds b = renderer.bounds;
                bounds.AppendLine($"BOUNDS|{n}|{b.min.x:F3}|{b.min.y:F3}|{b.min.z:F3}|{b.max.x:F3}|{b.max.y:F3}|{b.max.z:F3}");
            }
            Debug.Log(bounds.ToString());
            vessel.transform.SetPositionAndRotation(start, startRotation);

            Vector3 origin = vessel.transform.position;
            origin.y = 0f;
            Quaternion heading = Quaternion.Euler(0f, vessel.transform.eulerAngles.y, 0f);
            float waterLevel = ship.WaterLevel;

            Shot(stage, "fore-open-empty", 0f, HatchPosition.ForwardOpen, new Vector3(13, 15, 12), new Vector3(0, -2, 32), -1);
            Shot(stage, "fore-open-half", .5f, HatchPosition.ForwardOpen, new Vector3(13, 15, 12), new Vector3(0, -2, 32), -1);
            Shot(stage, "fore-open-full", 1f, HatchPosition.ForwardOpen, new Vector3(13, 15, 12), new Vector3(0, -2, 32), -1);
            Shot(stage, "fore-open-full-low", 1f, HatchPosition.ForwardOpen, new Vector3(7.5f, 3.6f, 8), new Vector3(-2, -1, 40), -1);
            Shot(stage, "fore-open-empty-low", 0f, HatchPosition.ForwardOpen, new Vector3(7.5f, 3.6f, 8), new Vector3(-2, -1.5f, 40), -1);
            Shot(stage, "aft-open-full", 1f, HatchPosition.AftOpen, new Vector3(-13, 15, -36), new Vector3(0, -2, -18), -1);
            Shot(stage, "aft-open-empty", 0f, HatchPosition.AftOpen, new Vector3(-13, 15, -36), new Vector3(0, -2, -18), -1);
            Shot(stage, "half-open-full", 1f, HatchPosition.HalfOpen, new Vector3(30, 40, -10), new Vector3(0, 0, 10), -1);
            Shot(stage, "half-open-empty", 0f, HatchPosition.HalfOpen, new Vector3(30, 40, -10), new Vector3(0, 0, 10), -1);
            Shot(stage, "closed-full", 1f, HatchPosition.Closed, new Vector3(30, 40, -10), new Vector3(0, 0, 10), -1);
            Shot(stage, "outside-waterline", 1f, HatchPosition.ForwardOpen, new Vector3(22, 3, 30), new Vector3(0, 0, 30), -1);
            Shot(stage, "lod1-fore-open-full", 1f, HatchPosition.ForwardOpen, new Vector3(13, 15, 12), new Vector3(0, -2, 32), 1);
            Shot(stage, "lod2-fore-open-empty", 0f, HatchPosition.ForwardOpen, new Vector3(13, 15, 12), new Vector3(0, -2, 32), 2);
            group?.ForceLOD(-1);
            Object.DestroyImmediate(cameraObject);

            void Shot(string prefix, string name, float load, HatchPosition hatches, Vector3 eye, Vector3 target, int lod)
            {
                LoadingState state = LoadingModel.Initial(data, load);
                VesselParameters p = VesselParameters.Create(data, state);
                // Rest the vessel at its solved floating position: keel depth Draft + TrimSlope z.
                vessel.transform.SetPositionAndRotation(
                    origin + Vector3.up * (waterLevel - p.Draft - p.KeelLocalY),
                    heading * Quaternion.Euler(Mathf.Atan(p.TrimSlope) * Mathf.Rad2Deg, 0f, 0f));
                rig.SetHatches(hatches);
                // Lift and traverse are sequential: the stack must finish opening before it moves.
                for (int i = 0; i < 3; i++) rig.Advance(400, 0, 0, 0);
                if (cargo != null) cargo.Show(state.massKg);
                group?.ForceLOD(lod);
                camera.transform.position = vessel.transform.TransformPoint(eye);
                camera.transform.LookAt(vessel.transform.TransformPoint(target));
                LandscapePreview.Render(camera, $"{Output}/{(prefix == "part17" ? "part17" : "part12-" + prefix)}-hold-{name}.png");
            }
        }

        private static string Argument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
                if (arguments[i] == name) return arguments[i + 1];
            return null;
        }
    }
}
