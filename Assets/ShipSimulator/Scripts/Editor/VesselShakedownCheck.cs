using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ShipSimulator.Editor
{
    // Dedicated batch check: takes every catalogue vessel down the river reach at full ahead, holding the
    // channel, and captures what a pilot would see with the HUD over it. It supplements manual sailing: the test suites read numbers, the sea trials run the model
    // without Unity, and the wake check sails the default vessel only. Handling feel still needs a pilot.
    [InitializeOnLoad]
    public static class VesselShakedownCheck
    {
        private const string StageKey = "ShipSimulator.Shakedown.Stage";
        private const string IndexKey = "ShipSimulator.Shakedown.Index";
        private const string OutputFolder = "Logs/Shakedown";
        private const string SceneName = "RiverTrainingScene";
        private const float RunSecondsPerVessel = 800f;
        private const int SettleTicks = 8;

        // Chase, bridge, port beam, bow, stern and the navigator's eye: the views that sit closest to the
        // model, where a view authored for another vessel would end up inside this one.
        private static readonly int[] CapturedViews = { 0, 1, 3, 5, 6, 8 };

        private static double deadline;
        private static double stageStart;
        private static double nextProgress;
        private static float voyageStart;
        private static bool failed;
        private static int captureIndex;
        private static int settle;
        private static readonly List<string> report = new List<string>();

        static VesselShakedownCheck()
        {
            deadline = EditorApplication.timeSinceStartup + 2700;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("Ship Simulator/Run Vessel Shakedown")]
        private static void RunFromMenu()
        {
            if (EditorApplication.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + SceneName + ".unity");
            SessionState.SetInt(IndexKey, 0);
            SessionState.SetInt(StageKey, 1);
            EditorApplication.EnterPlaymode();
        }

        public static void Run() => RunAt(0);
        public static void RunFastCrafts() => RunAt(3);

        private static void RunAt(int firstVessel)
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use a dedicated batch editor for this check.");
            Debug.Log("VESSEL_SHAKEDOWN|START");
            SessionState.SetInt(IndexKey, firstVessel);
            SessionState.SetInt(StageKey, 1);
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + SceneName + ".unity");
            EditorApplication.EnterPlaymode();
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (SessionState.GetInt(StageKey, 0) != 0 && (type == LogType.Error || type == LogType.Exception))
                failed = true;
        }

        private static void Tick()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0) return;
            if (EditorApplication.timeSinceStartup >= nextProgress)
            {
                nextProgress = EditorApplication.timeSinceStartup + 30;
                Debug.Log($"VESSEL_SHAKEDOWN|PROGRESS stage={stage} playing={EditorApplication.isPlaying} " +
                    $"frame={Time.frameCount} vessel={SessionState.GetInt(IndexKey, 0)} " +
                    $"menu={UnityEngine.Object.FindAnyObjectByType<VoyageMenu>() != null}");
                var current = UnityEngine.Object.FindAnyObjectByType<ShipPhysicsController>();
                if (current != null && current.Data != null)
                    Debug.Log($"VESSEL_SHAKEDOWN|STATE pos={current.transform.position} " +
                        $"speed={current.Body.linearVelocity} command={current.ThrottleCommand} " +
                        $"rpm={current.ShaftRpm(0)} scale={Time.timeScale} time={Time.time} " +
                        $"manual={current.ManualStepping} kinematic={current.Body.isKinematic}");
            }
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Vessel shakedown timed out.");
                if (!EditorApplication.isPlaying) return;
                Application.runInBackground = true;
                Time.captureDeltaTime = 1f / 60f;
                var menu = UnityEngine.Object.FindAnyObjectByType<VoyageMenu>();
                if (menu == null || !menu.IsReady) return;

                VesselCatalogue catalogue = VesselCatalogue.Load();
                if (catalogue == null || catalogue.Entries.Count == 0)
                    throw new InvalidOperationException("The vessel catalogue is empty.");
                int index = SessionState.GetInt(IndexKey, 0);
                if (index >= catalogue.Entries.Count)
                {
                    Finish();
                    return;
                }

                VesselCatalogue.Entry entry = catalogue.Entries[index];
                double now = EditorApplication.timeSinceStartup;
                if (stage == 1)
                {
                    VesselSelection.SelectedId = entry.id;
                    menu.StartVoyage(SceneName);
                    Advance(2, now);
                    return;
                }

                if (VoyageMenu.IsOpen) return;
                var ship = UnityEngine.Object.FindAnyObjectByType<ShipPhysicsController>();
                if (ship == null || VesselSwap.IdOf(ship) != entry.id) return;

                if (stage == 2)
                {
                    if (now - stageStart < 1.5) return;
                    UnityEngine.Object.FindAnyObjectByType<SimulationTimeController>()?.SetScale(4f);
                    ship.SetThrottleCommand(1f);
                    // The telegraph is a HUD control, so an order given straight to the ship has to be
                    // pushed back into the HUD the way a loaded voyage does, or the captures read STOP.
                    UnityEngine.Object.FindAnyObjectByType<ShipTelemetryUI>()?.RefreshAfterVoyageLoad();
                    captureIndex = 0;
                    settle = 0;
                    voyageStart = Time.time;
                    Advance(3, now);
                    return;
                }

                float speed = Vector3.Dot(ship.RelativeWaterVelocity, ship.transform.forward);
                if (stage == 3)
                {
                    Steer(ship, speed);
                    if (!AtTargetSpeed(ship, speed) && Time.time - voyageStart < RunSecondsPerVessel) return;
                    UnityEngine.Object.FindAnyObjectByType<SimulationTimeController>()?.SetScale(1f);
                    Advance(4, now);
                    return;
                }

                if (stage == 4)
                {
                    Steer(ship, speed);
                    var follow = UnityEngine.Object.FindAnyObjectByType<ShipFollowCamera>();
                    if (follow == null) throw new InvalidOperationException("No follow camera in the scene.");
                    if (captureIndex >= CapturedViews.Length)
                    {
                        RestoreHud();
                        try { Record(entry, ship, speed); }
                        catch (InvalidOperationException error)
                        {
                            failed = true;
                            Debug.LogError("VESSEL_SHAKEDOWN|FAIL: " + error.Message);
                            ReportContacts(ship);
                        }
                        SessionState.SetInt(IndexKey, index + 1);
                        Advance(1, now);
                        return;
                    }
                    int view = CapturedViews[captureIndex];
                    if (view >= follow.ViewCount)
                    {
                        captureIndex++;
                        return;
                    }
                    if (settle == 0)
                    {
                        follow.SetView(view);
                        follow.RestoreState(follow.CaptureState());
                    }
                    if (settle++ < SettleTicks) return;
                    Capture(follow, entry.id, view);
                    settle = 0;
                    captureIndex++;
                }
            }
            catch (Exception error)
            {
                RestoreHud();
                Time.captureDeltaTime = 0f;
                SessionState.SetInt(StageKey, 0);
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else EditorApplication.ExitPlaymode();
            }
        }

        // The modelled reach ends around 630 m; past that the banks stop and the capture would show open
        // water. A loaded cargo ship is still gathering way there, which the report states as a fraction
        // of its published speed.
        private const float ReachEndZ = 600f;

        private static bool AtTargetSpeed(ShipPhysicsController ship, float speed)
        {
            VesselData data = ship.Data;
            if (data == null) return true;
            if (ship.transform.position.z > ReachEndZ) return true;
            if (SupportModel.Lifts(data.support) &&
                SupportModel.Fraction(data.support, speed) > 0.95f * data.support.supportedWeightFraction)
                return true;
            return speed > 0.75f * data.controlLimits.maxLoadedSpeedMps;
        }

        // Line of sight steering onto the marked channel, so a vessel does not simply run onto the bank
        // while it accelerates. The look-ahead grows with the ship, because a 138 m hull chasing a point
        // 70 m away swings across the fairway on every bend.
        private static void Steer(ShipPhysicsController ship, float speed)
        {
            Vector3 position = ship.transform.position;
            float length = ship.Data != null ? ship.Data.dimensions.lengthOverallM : 100f;
            float lookAhead = Mathf.Clamp(Mathf.Max(1.5f * length, 8f * Mathf.Abs(speed)), 100f, 260f);
            // Aim ahead of the bend. Following only the local tangent carries a long bow into the
            // outside bank before the ship's centre reaches the change in curvature.
            float bearing = Mathf.Atan2(FairwayModel.CenterX(position.z + lookAhead) - position.x,
                lookAhead) * Mathf.Rad2Deg;
            float error = Mathf.DeltaAngle(ship.transform.eulerAngles.y, bearing);
            float yawRate = ship.Body.angularVelocity.y * Mathf.Rad2Deg;
            ship.SetRudderCommand(Mathf.Clamp((error - yawRate * 2.5f) / 15f, -1f, 1f));
        }

        private static void Record(VesselCatalogue.Entry entry, ShipPhysicsController ship, float speed)
        {
            VesselData data = ship.Data;
            float depth = ship.SampleDepth(ship.transform.position);
            float underKeel = depth - ship.EffectiveDraftM;
            float amplitude = Shader.GetGlobalFloat("_WakeAmplitude");
            // The shader raises the Kelvin wake by amplitude * speed^2 / g and the bow crest by
            // 0.45 * head, with head = speed^2 / 2g scaled by the same amplitude against its tuned value.
            float kelvinCrestM = amplitude * speed * speed / 9.81f;
            float bowCrestM = 0.45f * speed * speed / (2f * 9.81f) * (amplitude / 0.07f);
            float support = SupportModel.Lifts(data.support)
                ? SupportModel.Fraction(data.support, speed) / data.support.supportedWeightFraction
                : 0f;
            string grounding = ship.Grounding != null ? ship.Grounding.State.ToString() : "no controller";
            float heel = Vector3.Angle(ship.transform.up, Vector3.up);
            float hullRise = ship.transform.position.y - ship.WaterLevel + data.hydrostatics.waterlineLocalY;
            if (SupportModel.Lifts(data.support) &&
                (hullRise > data.dimensions.loadedDraftM + 0.3f || hullRise < -0.3f))
                throw new InvalidOperationException($"{entry.id} has an invalid ride height: {hullRise:0.00} m.");
            float viewClearance = CheckCameraViews(ship);

            report.Add($"| {entry.id} | {speed * 1.943844f:0.0} kn / {speed * 3.6f:0.0} km/h | " +
                $"{speed / Mathf.Max(data.controlLimits.maxLoadedSpeedMps, 0.01f) * 100f:0}% | " +
                $"{(SupportModel.Lifts(data.support) ? $"{support * 100f:0}%" : "n/a")} | " +
                $"{underKeel:0.0} m | {heel:0.0} deg | {kelvinCrestM:0.00} m | {bowCrestM:0.00} m | " +
                $"{(float.IsPositiveInfinity(viewClearance) ? "default set" : $"{viewClearance:0.0} m")} | {grounding} |");
            Debug.Log($"VESSEL_SHAKEDOWN|{entry.id}: {speed:0.00} m/s, support {support * 100f:0}%, " +
                $"under keel {underKeel:0.00} m, heel {heel:0.0} deg, wake crest {kelvinCrestM:0.00} m, " +
                $"bow crest {bowCrestM:0.00} m, hull rise {hullRise:0.00} m, " +
                $"{grounding}");

            // The published speed is verified by the sea trials over a longer run; what this check proves
            // is that the vessel is under way and not held by anything in the scene. The fraction reached
            // inside the reach is reported for the reader.
            if (speed < 1.5f)
                throw new InvalidOperationException(
                    $"{entry.id} is not under way at the end of its run: {speed:0.00} m/s.");
            if (ship.Grounding != null &&
                (ship.Grounding.State == GroundingState.Touching ||
                 ship.Grounding.State == GroundingState.HardGrounding))
                throw new InvalidOperationException($"{entry.id} is aground at the end of its run.");
            if (SupportModel.Lifts(data.support) && support < 0.9f)
                throw new InvalidOperationException($"{entry.id} never rose onto its foils or cushion.");
            // A river craft under way does not sail on its side. Lifting the hull takes its waterplane
            // with it, so a supported craft needs roll stiffness from its foils or skegs.
            if (heel > 15f)
                throw new InvalidOperationException($"{entry.id} is heeled {heel:0.0} deg under way.");
            // A river craft does not raise a metre of water at speed. The estimated visual model grows with
            // speed squared, so without the displacement and lift scaling a fast craft dwarfs a loaded ship.
            if (kelvinCrestM > 0.6f || bowCrestM > 1.2f)
                throw new InvalidOperationException($"{entry.id} raises an implausible wake: " +
                    $"{kelvinCrestM:0.00} m crest, {bowCrestM:0.00} m bow wave.");
        }

        private static void ReportContacts(ShipPhysicsController ship)
        {
            foreach (Collider hull in ship.GetComponentsInChildren<Collider>())
            {
                if (!hull.enabled || hull.isTrigger) continue;
                foreach (Collider other in UnityEngine.Physics.OverlapBox(hull.bounds.center,
                    hull.bounds.extents + Vector3.one * 0.2f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (other.attachedRigidbody == ship.Body) continue;
                    Debug.Log($"VESSEL_SHAKEDOWN|NEAR {other.name} bounds={other.bounds}");
                    if (UnityEngine.Physics.ComputePenetration(hull, hull.transform.position, hull.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out _, out float depth))
                        Debug.Log($"VESSEL_SHAKEDOWN|CONTACT {other.name} penetration={depth:0.000} m");
                }
            }
        }

        // Smallest distance from a model-authored orbit view to the vessel's own silhouette box. Vessels on
        // the default view set report infinity: those offsets are hand-authored over the 507B's own model,
        // where the near views deliberately sit over the cargo deck.
        private static float CheckCameraViews(ShipPhysicsController ship)
        {
            VesselLayout layout = ship.GetComponent<VesselLayout>();
            if (layout == null || layout.CameraViews == null || layout.CameraViews.Length == 0)
                return float.PositiveInfinity;
            Bounds bounds = LocalModelBounds(ship.transform);
            float nearest = float.PositiveInfinity;
            foreach (Vector3 view in layout.CameraViews)
            {
                float outside = OutsideDistance(bounds, view);
                nearest = Mathf.Min(nearest, outside);
                if (outside <= 0f)
                    throw new InvalidOperationException(
                        $"{VesselSwap.IdOf(ship)} has an orbit view inside its own model at {view}.");
            }
            return nearest;
        }

        private static Bounds LocalModelBounds(Transform root)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds local = filter.sharedMesh.bounds;
                foreach (Vector3 corner in Corners(local))
                    bounds.Encapsulate(root.InverseTransformPoint(filter.transform.TransformPoint(corner)));
            }
            return bounds;
        }

        private static IEnumerable<Vector3> Corners(Bounds bounds)
        {
            for (int i = 0; i < 8; i++)
                yield return new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
        }

        private static float OutsideDistance(Bounds bounds, Vector3 point)
        {
            if (!bounds.Contains(point)) return Vector3.Distance(bounds.ClosestPoint(point), point);
            return -Mathf.Min(
                Mathf.Min(point.x - bounds.min.x, bounds.max.x - point.x),
                Mathf.Min(
                    Mathf.Min(point.y - bounds.min.y, bounds.max.y - point.y),
                    Mathf.Min(point.z - bounds.min.z, bounds.max.z - point.z)));
        }

        private static Canvas hudCanvas;

        private static void Capture(ShipFollowCamera follow, string vesselId, int view)
        {
            Camera camera = follow.GetComponent<Camera>();
            if (camera == null) throw new InvalidOperationException("The follow camera has no Camera component.");
            Directory.CreateDirectory(OutputFolder);
            ShowHudOn(camera);
            string name = $"{vesselId}-{view}-{ShipFollowCamera.GetViewName(view).ToLowerInvariant()}.png";
            LandscapePreview.Render(camera, OutputFolder + "/" + name);
        }

        // The HUD is an overlay canvas, which never reaches a render texture. Pointing it at the vessel
        // camera for the capture is the only way to record the instruments the pilot actually reads.
        private static void ShowHudOn(Camera camera)
        {
            if (hudCanvas == null)
            {
                ShipTelemetryUI hud = UnityEngine.Object.FindAnyObjectByType<ShipTelemetryUI>();
                hudCanvas = hud != null ? hud.GetComponent<Canvas>() : null;
                if (hudCanvas == null) return;
            }
            hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            hudCanvas.worldCamera = camera;
            hudCanvas.planeDistance = 1f;
            foreach (Text text in hudCanvas.GetComponentsInChildren<Text>())
            {
                if (text.font == null) continue;
                text.font.RequestCharactersInTexture(text.text,
                    Mathf.RoundToInt(text.fontSize * text.pixelsPerUnit), text.fontStyle);
                text.SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
        }

        private static void RestoreHud()
        {
            if (hudCanvas == null) return;
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.worldCamera = null;
            hudCanvas = null;
        }

        private static void Advance(int stage, double now)
        {
            SessionState.SetInt(StageKey, stage);
            stageStart = now;
        }

        private static void Finish()
        {
            SessionState.SetInt(StageKey, 0);
            Directory.CreateDirectory(OutputFolder);
            var text = new StringBuilder();
            text.AppendLine("# Vessel shakedown");
            text.AppendLine();
            text.AppendLine($"{report.Count} selected catalogue vessels run at full ahead down the river reach, steered to hold the");
            text.AppendLine("channel. Captures with the HUD over them are in this folder. Wake crests are the");
            text.AppendLine("estimated visual model's, not measured wave heights.");
            text.AppendLine();
            text.AppendLine("| Vessel | Speed through water | Of published | Support | Under keel | Heel | Wake crest | Bow crest | View clearance | Bottom |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|");
            foreach (string line in report) text.AppendLine(line);
            File.WriteAllText(OutputFolder + "/shakedown.md", text.ToString());
            Debug.Log($"VESSEL_SHAKEDOWN|{(failed ? "FAIL" : "PASS")}: {report.Count} vessels");
            Time.captureDeltaTime = 0f;
            if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            else EditorApplication.ExitPlaymode();
        }
    }
}
