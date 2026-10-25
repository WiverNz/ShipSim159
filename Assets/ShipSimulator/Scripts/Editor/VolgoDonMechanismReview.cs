using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // Part 13 machinery review in the Gorodets water with full cargo: runtime durations of every hatch transition,
    // the clearance from every cover to the cargo heaps along the whole operating cycle, and captures of
    // intermediate and end positions, anchors on their chains and the LODs. The scene is not saved.
    // Writes part13-machinery.json and Previews/part13-machinery-*.png, ends with MACHINERY_REVIEW|PASS.
    public static class VolgoDonMechanismReview
    {
        private const string Output = "Art/Source/VolgoDon507B/Previews";

        [Serializable] private sealed class Transition { public string from, to; public float seconds; }
        [Serializable] private sealed class Report
        {
            public Transition[] transitions;
            public float minimumCoverToCargoM, cycleSecondsSampled;
            public int samples;
            public string note;
        }

        private static string Prefix => "part" + (Environment.GetCommandLineArgs().Contains("-finalReview") ? "17" : "13");

        public static void Run()
        {
            try
            {
                Review();
                Debug.Log("MACHINERY_REVIEW|PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Review()
        {
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity");
            var ship = Object.FindAnyObjectByType<ShipPhysicsController>();
            GameObject vessel = ship.gameObject;
            var rig = vessel.GetComponent<VesselMechanisms>();
            var cargo = vessel.GetComponent<HoldInteriorVisual>();
            var group = vessel.GetComponentInChildren<LODGroup>();
            VesselData data = JsonUtility.FromJson<VesselData>(File.ReadAllText("Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json"));
            LoadingState full = LoadingModel.Initial(data, 1f);
            VesselParameters p = VesselParameters.Create(data, full);
            Vector3 origin = vessel.transform.position;
            origin.y = 0f;
            Quaternion heading = Quaternion.Euler(0f, vessel.transform.eulerAngles.y, 0f);
            vessel.transform.SetPositionAndRotation(origin + Vector3.up * (ship.WaterLevel - p.Draft - p.KeelLocalY),
                heading * Quaternion.Euler(Mathf.Atan(p.TrimSlope) * Mathf.Rad2Deg, 0f, 0f));
            cargo.Show(full.massKg);
            rig.RestoreState(null);

            var report = new Report { note = "Editor edit-mode stepping at 1 s; real-time rates from mechanisms.json operating_rates." };
            var transitions = new List<Transition>();
            Renderer[] covers = vessel.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("Panel") ||
                r.name.StartsWith("Seal_") || r.name.StartsWith("Jack")).ToArray();
            float minimum = float.PositiveInfinity, elapsed = 0;
            int samples = 0;
            HatchPosition from = HatchPosition.Closed;
            foreach (HatchPosition to in new[] { HatchPosition.HalfOpen, HatchPosition.ForwardOpen, HatchPosition.AftOpen, HatchPosition.Closed })
            {
                rig.SetHatches(to);
                transitions.Add(new Transition { from = from.ToString(), to = to.ToString(), seconds = rig.HatchSecondsRemaining });
                for (int guard = 0; guard < 4000 && rig.HatchesMoving; guard++)
                {
                    rig.Advance(1, 0, 0, 0);
                    elapsed++;
                    samples++;
                    minimum = Mathf.Min(minimum, CoverToCargo(covers, cargo));
                }
                if (rig.HatchesMoving) throw new InvalidOperationException("Hatches never reached " + to);
                from = to;
            }
            report.transitions = transitions.ToArray();
            report.minimumCoverToCargoM = minimum;
            report.cycleSecondsSampled = elapsed;
            report.samples = samples;
            if (!(minimum > 0.1f)) throw new InvalidOperationException("Covers come within " + minimum + " m of the cargo.");

            Camera camera = CreateCamera();
            Shot(camera, rig, cargo, group, full, HatchPosition.Closed, 0, "closed", new Vector3(32, 30, 0), new Vector3(0, 2, 12), -1);
            float lift = rig.Rates.outerLiftSeconds, inner = rig.Rates.innerLiftSeconds, slide = rig.Rates.innerSlideSeconds;
            Shot(camera, rig, cargo, group, full, HatchPosition.HalfOpen, lift * .5f, "outer-lifting", new Vector3(22, 12, 48), new Vector3(0, 3, 38), -1);
            Shot(camera, rig, cargo, group, full, HatchPosition.HalfOpen, lift + inner, "inner-lifted", new Vector3(22, 12, 48), new Vector3(0, 3, 38), -1);
            Shot(camera, rig, cargo, group, full, HatchPosition.HalfOpen, lift + inner + slide * .5f, "inner-sliding", new Vector3(28, 22, 40), new Vector3(0, 2, 30), -1);
            Shot(camera, rig, cargo, group, full, HatchPosition.HalfOpen, 2000, "half-open", new Vector3(34, 36, -6), new Vector3(0, 1, 8), -1);
            Shot(camera, rig, cargo, group, full, HatchPosition.ForwardOpen, rig.Rates.traverseSeconds * .5f, "traversing", new Vector3(34, 36, -6), new Vector3(0, 1, 8), -1, keep: true);
            Shot(camera, rig, cargo, group, full, HatchPosition.ForwardOpen, 2000, "fore-open", new Vector3(34, 36, -6), new Vector3(0, 1, 8), -1, keep: true);
            Shot(camera, rig, cargo, group, full, HatchPosition.AftOpen, 4000, "aft-open", new Vector3(34, 36, -6), new Vector3(0, 1, 8), -1, keep: true);
            Shot(camera, rig, cargo, group, full, HatchPosition.HalfOpen, lift + inner + slide * .5f, "inner-sliding-lod1", new Vector3(28, 22, 40), new Vector3(0, 2, 30), 1);
            Shot(camera, rig, cargo, group, full, HatchPosition.HalfOpen, lift + inner + slide * .5f, "inner-sliding-lod2", new Vector3(28, 22, 40), new Vector3(0, 2, 30), 2);

            // Anchor payout and holding run on GroundTackle in Play Mode; GroundTackleRuntimeCheck captures them.
            rig.RestoreState(null);
            Render(camera, group, "windlass", vessel.transform, new Vector3(5, 8.5f, 52), new Vector3(0, 4.3f, 60.6f), -1);
            rig.Advance(0, 0, 0, 35);
            Render(camera, group, "nozzles-starboard-limit", vessel.transform, new Vector3(9, -1, -76), new Vector3(0, -2, -64.65f), -1);
            rig.Advance(0, 0, 0, -35);
            Render(camera, group, "nozzles-port-limit", vessel.transform, new Vector3(9, -1, -76), new Vector3(0, -2, -64.65f), -1);
            group.ForceLOD(-1);
            Object.DestroyImmediate(camera.gameObject);
            File.WriteAllText($"Art/Source/VolgoDon507B/{Prefix}-machinery.json", JsonUtility.ToJson(report, true));
        }

        // Smallest height of any cover part above the cargo heap top, over the footprint they share.
        private static float CoverToCargo(Renderer[] covers, HoldInteriorVisual cargo)
        {
            float minimum = float.PositiveInfinity;
            for (int i = 0; i < cargo.HoldCount; i++)
            {
                Transform heap = cargo.Heap(i);
                if (heap == null || !heap.gameObject.activeInHierarchy) continue;
                Bounds pile = heap.GetComponent<Renderer>().bounds;
                foreach (Renderer cover in covers)
                {
                    Bounds b = cover.bounds;
                    bool overlaps = b.max.x > pile.min.x && b.min.x < pile.max.x && b.max.z > pile.min.z && b.min.z < pile.max.z;
                    if (overlaps) minimum = Mathf.Min(minimum, b.min.y - pile.max.y);
                }
            }
            return minimum;
        }

        private static Camera CreateCamera()
        {
            var cameraObject = new GameObject("Part 13 machinery camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 50;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2500;
            camera.allowHDR = true;
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.requiresDepthTexture = true;
            data.requiresColorTexture = true;
            Directory.CreateDirectory(Output);
            return camera;
        }

        private static void Shot(Camera camera, VesselMechanisms rig, HoldInteriorVisual cargo, LODGroup group, LoadingState load,
            HatchPosition target, float seconds, string name, Vector3 eye, Vector3 look, int lod, bool keep = false)
        {
            if (!keep)
            {
                rig.RestoreState(null);
                rig.SetHatches(target);
                rig.Advance(seconds, 0, 0, 0);
            }
            else
            {
                rig.SetHatches(target);
                rig.Advance(seconds, 0, 0, 0);
            }
            cargo.Show(load.massKg);
            Render(camera, group, name, rig.transform, eye, look, lod);
        }

        private static void Render(Camera camera, LODGroup group, string name, Transform vessel, Vector3 eye, Vector3 look, int lod)
        {
            group?.ForceLOD(lod);
            camera.transform.position = vessel.TransformPoint(eye);
            camera.transform.LookAt(vessel.TransformPoint(look));
            LandscapePreview.Render(camera, $"{Output}/{Prefix}-machinery-{name}.png");
        }
    }
}
