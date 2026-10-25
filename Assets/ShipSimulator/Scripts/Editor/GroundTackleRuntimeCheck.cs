using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using ShipSimulator.CameraSystem;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace ShipSimulator.Editor
{
    // Dedicated batch check of the Part 14 demonstration in the Gorodets scene: the 507B made fast alongside the
    // demonstration berth with five lines, then at anchor head to the current in the channel, dragged by her
    // engines and hove up. It captures what the bridge and the deck would see and times the line drawing.
    // Ends with GROUND_TACKLE_RUNTIME|PASS.
    [InitializeOnLoad]
    public static class GroundTackleRuntimeCheck
    {
        private const string StageKey = "ShipSimulator.Part14.Stage";
        private const string SceneName = "GorodetsTrainingScene";
        private static string Prefix => Array.IndexOf(Environment.GetCommandLineArgs(), "-finalReview") >= 0 ? "part17" : "part14";
        private const string Output = "Art/Source/VolgoDon507B/Previews";
        private static readonly string[] Lines =
            { "bow-head-starboard", "bow-breast-starboard", "fwd-spring-starboard", "aft-spring-starboard", "stern-starboard" };
        private static readonly string[] Bollards =
            { "gorodets-berth-b7", "gorodets-berth-b6", "gorodets-berth-b4", "gorodets-berth-b3", "gorodets-berth-b1" };

        [Serializable]
        private sealed class Report
        {
            public List<string> lines = new List<string>();
            public List<string> lineRenderers = new List<string>();
            public float alongsideDriftM, maxLineLoadFraction, drawMicroseconds, drawBytesPer1000;
            public string anchorStatusAtAnchor, anchorStatusUnderEngines, anchorStatusHeaving;
            public float chainAtAnchorM, chainTensionAtAnchorN, anchorDragM, depthAtAnchorM;
            public float maxChainTensionN, maxChainTensionUnderEnginesN;
            public string groundingAtAnchor, groundingUnderEngines;
            public bool linesHeld, anchorHeld, dragged, heaving;
        }

        private static double deadline, stageStart;
        private static float simulationStart;
        private static bool failed;
        private static Report report = new Report();
        private static Vector3 mark;
        private static float setting = -1;
        private static Canvas hudCanvas;

        static GroundTackleRuntimeCheck()
        {
            deadline = EditorApplication.timeSinceStartup + 2400;
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetInt(StageKey, 0) != 0 && (type == LogType.Error || type == LogType.Exception)) failed = true;
            };
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use a dedicated batch editor for this check.");
            Debug.Log("GROUND_TACKLE_RUNTIME|START");
            SessionState.SetInt(StageKey, 1);
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + SceneName + ".unity");
            EditorApplication.EnterPlaymode();
        }

        private static void Advance(int stage)
        {
            SessionState.SetInt(StageKey, stage);
            stageStart = EditorApplication.timeSinceStartup;
            simulationStart = Time.time;
        }

        private static float Elapsed => Time.time - simulationStart;

        private static void Tick()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == 0) return;
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Ground tackle check timed out.");
                if (!EditorApplication.isPlaying) return;
                Application.runInBackground = true;
                Time.captureDeltaTime = 1f / 60f;
                var menu = UnityEngine.Object.FindAnyObjectByType<VoyageMenu>();
                if (menu == null || !menu.IsReady) return;
                if (stage == 1)
                {
                    VesselSelection.SelectedId = VesselCatalogue.DefaultVesselId;
                    menu.StartVoyage(SceneName);
                    Advance(2);
                    return;
                }
                if (VoyageMenu.IsOpen) return;
                var ship = UnityEngine.Object.FindAnyObjectByType<ShipPhysicsController>();
                if (ship == null || ship.Data == null || ship.GroundTackle == null) return;
                GroundTackle tackle = ship.GroundTackle;
                var time = UnityEngine.Object.FindAnyObjectByType<SimulationTimeController>();
                switch (stage)
                {
                    case 2:
                        if (EditorApplication.timeSinceStartup - stageStart < 1.5) return;
                        UnityEngine.Object.FindAnyObjectByType<WeatherController>()?.Configure(270, 0, 0, 0);
                        // Laid alongside by a save-style restore: a test setup, not something the physics does.
                        Place(ship, new Vector3(38.8f, ship.Body.position.y, 5f), 0f);
                        for (int i = 0; i < Lines.Length; i++)
                        {
                            string refusal = tackle.PassLine(Station(tackle, Lines[i]), Shore(Bollards[i]));
                            if (refusal != null) throw new InvalidOperationException("Could not pass a line: " + refusal);
                            tackle.SetLineCommand(Station(tackle, Lines[i]), LineCommand.Haul);
                        }
                        time?.SetScale(4f);
                        Advance(3);
                        return;
                    case 3:
                        if (Elapsed < 40f) return;
                        for (int i = 0; i < Lines.Length; i++) tackle.SetLineCommand(Station(tackle, Lines[i]), LineCommand.Hold);
                        mark = ship.Body.position;
                        Advance(4);
                        return;
                    case 4:
                        for (int k = 0; k < tackle.LineCount; k++)
                            report.maxLineLoadFraction = Mathf.Max(report.maxLineLoadFraction, tackle.LineTensionN(k) / tackle.LineBreakingLoadN);
                        if (Elapsed < 240f) return;
                        time?.SetScale(1f);
                        report.alongsideDriftM = Vector3.Distance(mark, ship.Body.position);
                        report.linesHeld = tackle.LineCount == Lines.Length;
                        for (int k = 0; k < tackle.LineCount; k++)
                            report.lines.Add($"{tackle.LineShip(k).Id} > {tackle.LineShore(k).Id}: {tackle.LineLengthM(k):0.0} m, " +
                                $"{tackle.LineTensionN(k) / 1000:0.0} kN, {tackle.LineStateAt(k)}");
                        TimeDrawing(ship);
                        foreach (LineRenderer rope in ship.GetComponentsInChildren<LineRenderer>(true))
                            if (rope.name.StartsWith("MooringLine_") && rope.enabled)
                                report.lineRenderers.Add($"{rope.name}: {rope.positionCount} points, {rope.GetPosition(0)} to " +
                                    $"{rope.GetPosition(rope.positionCount - 1)}, width {rope.widthMultiplier}, {rope.sharedMaterial?.name}, visible {rope.isVisible}");
                        CaptureBerth(ship);
                        foreach (string line in Lines) tackle.CastOff(Station(tackle, line));
                        // In the straight above the berth, head to the stream and a 10 m/s wind, in about 5.3 m of water.
                        UnityEngine.Object.FindAnyObjectByType<WeatherController>()?.Configure(0, 10, 0, 0);
                        Place(ship, new Vector3(0f, ship.Body.position.y, -20f), 180f);
                        setting = -1;
                        tackle.SetChainCommand(0, ChainCommand.LetGo);
                        time?.SetScale(4f);
                        Advance(5);
                        return;
                    case 5:
                        if (tackle.Phase(0) == AnchorPhase.Bottom && tackle.Command(0) == ChainCommand.Hold && tackle.ChainOutM(0) < 30f)
                            tackle.SetChainCommand(0, ChainCommand.Veer);
                        if (tackle.Command(0) == ChainCommand.Veer && tackle.ChainOutM(0) >= 30f)
                        {
                            // Slow astern to set the anchor, as a ship does once the chain is out.
                            tackle.SetChainCommand(0, ChainCommand.Hold);
                            ship.SetThrottleCommand(-0.32f);
                            setting = Elapsed;
                        }
                        if (setting >= 0 && Elapsed > setting + 90f && ship.ThrottleCommand != 0)
                        {
                            ship.SetThrottleCommand(0f);
                        }
                        report.maxChainTensionN = Mathf.Max(report.maxChainTensionN, tackle.ChainTensionN(0));
                        if (Elapsed < 480f) return;
                        time?.SetScale(1f);
                        report.anchorStatusAtAnchor = tackle.AnchorStatus(0);
                        report.anchorHeld = report.anchorStatusAtAnchor == "HOLDING";
                        report.groundingAtAnchor = ship.Grounding != null ? ship.Grounding.State.ToString() : "none";
                        report.chainAtAnchorM = tackle.ChainOutM(0);
                        report.chainTensionAtAnchorN = tackle.ChainTensionN(0);
                        report.depthAtAnchorM = ship.SampleDepth(tackle.ShackleWorld(0));
                        CaptureAnchor(ship, tackle, "at-anchor");
                        mark = tackle.ShackleWorld(0);
                        ship.SetThrottleCommand(-1f);
                        UnityEngine.Object.FindAnyObjectByType<ShipTelemetryUI>()?.RefreshAfterVoyageLoad();
                        time?.SetScale(4f);
                        Advance(6);
                        return;
                    case 6:
                        report.dragged |= tackle.Dragging(0) && tackle.Embedment(0) >= 1;
                        report.maxChainTensionUnderEnginesN = Mathf.Max(report.maxChainTensionUnderEnginesN, tackle.ChainTensionN(0));
                        if (Elapsed < 120f) return;
                        time?.SetScale(1f);
                        report.anchorStatusUnderEngines = tackle.AnchorStatus(0);
                        report.anchorDragM = Vector3.Distance(mark, tackle.ShackleWorld(0));
                        report.groundingUnderEngines = ship.Grounding != null ? ship.Grounding.State.ToString() : "none";
                        Hud(ship, "part14-hud-dragging");
                        ship.SetThrottleCommand(0f);
                        UnityEngine.Object.FindAnyObjectByType<ShipTelemetryUI>()?.RefreshAfterVoyageLoad();
                        tackle.SetChainCommand(0, ChainCommand.Heave);
                        time?.SetScale(4f);
                        Advance(7);
                        return;
                    case 7:
                        if (Elapsed < 60f) return;
                        time?.SetScale(1f);
                        report.anchorStatusHeaving = tackle.AnchorStatus(0);
                        report.heaving = tackle.Command(0) == ChainCommand.Heave && tackle.ChainOutM(0) < report.chainAtAnchorM;
                        CaptureAnchor(ship, tackle, "heaving");
                        Finish();
                        return;
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Stop(1);
            }
        }

        private static void Place(ShipPhysicsController ship, Vector3 position, float heading)
        {
            ship.RestoreVoyage(new VoyageSave
            {
                position = position, rotation = Quaternion.Euler(0, heading, 0), engineCommands = new[] { 0f, 0f },
                shaftRps = new[] { 0f, 0f }
            });
            ship.SetThrottleCommand(0f);
            ship.SetRudderCommand(0f);
            UnityEngine.Object.FindAnyObjectByType<ShipTelemetryUI>()?.RefreshAfterVoyageLoad();
        }

        private static int Station(GroundTackle tackle, string id)
        {
            for (int i = 0; i < tackle.StationCount; i++) if (tackle.Station(i).Id == id) return i;
            throw new InvalidOperationException("No mooring station " + id);
        }

        private static MooringPoint Shore(string id)
        {
            foreach (MooringPoint point in UnityEngine.Object.FindObjectsByType<MooringPoint>())
                if (point.Id == id) return point;
            throw new InvalidOperationException("No shore bollard " + id);
        }

        private static void TimeDrawing(ShipPhysicsController ship)
        {
            var visual = ship.GetComponent<GroundTackleVisual>();
            if (visual == null) throw new InvalidOperationException("No ground tackle visual on the vessel.");
            for (int i = 0; i < 20; i++) visual.Draw();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            var clock = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++) visual.Draw();
            clock.Stop();
            report.drawMicroseconds = (float)clock.Elapsed.TotalMilliseconds;
            report.drawBytesPer1000 = GC.GetAllocatedBytesForCurrentThread() - allocated;
        }

        private static void CaptureBerth(ShipPhysicsController ship)
        {
            Transform t = ship.transform;
            Shot(t.TransformPoint(new Vector3(60, 45, -40)), t.TransformPoint(new Vector3(8, 0, 5)), "part14-berth-overview");
            Shot(t.TransformPoint(new Vector3(22, 9, 92)), t.TransformPoint(new Vector3(7, 3, 66)), "part14-berth-bow-lines");
            Shot(t.TransformPoint(new Vector3(24, 8, -92)), t.TransformPoint(new Vector3(6, 2, -64)), "part14-berth-stern-line");
            Shot(t.TransformPoint(new Vector3(-2, 14, 14)), t.TransformPoint(new Vector3(9, 2, 26)), "part14-berth-springs");
            Hud(ship, "part14-hud-alongside");
        }

        private static void CaptureAnchor(ShipPhysicsController ship, GroundTackle tackle, string name)
        {
            Vector3 hawse = tackle.HawseWorld(0), shackle = tackle.ShackleWorld(0);
            Vector3 middle = 0.5f * (hawse + shackle);
            Vector3 ahead = shackle - hawse;
            ahead.y = 0;
            ahead.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, ahead);
            Shot(hawse + side * 16 + ahead * 10 + Vector3.up * 4, hawse + Vector3.down * 1.5f + ahead * 2, "part14-anchor-" + name);
            Shot(middle + side * 24 + Vector3.down * 1.2f, middle + Vector3.down * 3.5f, "part14-anchor-" + name + "-underwater");
            Hud(ship, "part14-hud-" + name);
        }

        private static void Shot(Vector3 position, Vector3 target, string name)
        {
            Camera main = Camera.main;
            var holder = new GameObject("Part14 capture");
            Camera camera = holder.AddComponent<Camera>();
            if (main != null) camera.CopyFrom(main);
            camera.fieldOfView = 50;
            holder.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            Directory.CreateDirectory(Output);
            LandscapePreview.Render(camera, $"{Output}/{name.Replace("part14", Prefix)}.png");
            UnityEngine.Object.DestroyImmediate(holder);
        }

        // The HUD is an overlay canvas; pointing it at the follow camera puts it in the capture.
        private static void Hud(ShipPhysicsController ship, string name)
        {
            var follow = UnityEngine.Object.FindAnyObjectByType<ShipFollowCamera>();
            ShipTelemetryUI hud = UnityEngine.Object.FindAnyObjectByType<ShipTelemetryUI>();
            if (follow == null || hud == null) throw new InvalidOperationException("No follow camera or HUD.");
            hudCanvas = hud.GetComponent<Canvas>();
            Camera camera = follow.GetComponent<Camera>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            hudCanvas.worldCamera = camera;
            hudCanvas.planeDistance = 1f;
            foreach (Text text in hudCanvas.GetComponentsInChildren<Text>())
            {
                if (text.font == null) continue;
                text.font.RequestCharactersInTexture(text.text, Mathf.RoundToInt(text.fontSize * text.pixelsPerUnit), text.fontStyle);
                text.SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
            LandscapePreview.Render(camera, $"{Output}/{name.Replace("part14", Prefix)}.png");
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.worldCamera = null;
        }

        private static void Finish()
        {
            Directory.CreateDirectory("Logs/Part14");
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText($"Art/Source/VolgoDon507B/{Prefix}-runtime.json", json);
            Debug.Log("GROUND_TACKLE_RUNTIME|REPORT " + json);
            bool pass = !failed && report.linesHeld && report.alongsideDriftM < 3f && report.anchorHeld && report.dragged &&
                report.heaving && report.drawBytesPer1000 < 1024;
            Debug.Log(pass ? "GROUND_TACKLE_RUNTIME|PASS" : "GROUND_TACKLE_RUNTIME|FAIL");
            Stop(pass ? 0 : 1);
        }

        private static void Stop(int code)
        {
            Time.captureDeltaTime = 0f;
            SessionState.SetInt(StageKey, 0);
            if (Application.isBatchMode) EditorApplication.Exit(code);
            else EditorApplication.ExitPlaymode();
        }
    }
}
