using Unity.Profiling;
using System;
using System.Collections.Generic;
using ShipSimulator.Persistence;
using UnityEngine;

namespace ShipSimulator.Physics
{
    public enum AnchorPhase { Stowed, Hanging, Bottom, Lost }
    public enum ChainCommand { Hold, LetGo, Veer, Heave }
    public enum LineCommand { Hold, Haul, Slack }
    public enum LineState { Slack, Taut, Overloaded, Parted }
    public enum TackleEvent
    {
        AnchorLetGo, AnchorOnBottom, AnchorDragging, AnchorAweigh, AnchorStowed, ChainParted, WindlassStalled,
        LinePassed, LineParted, LineCastOff
    }

    // Anchors, chains and mooring lines as forces on the vessel. Each line is a quasi-static elastic catenary
    // (Catenary) that carries tension only. An anchor on the bottom holds up to a soil-dependent multiple of its
    // submerged weight and slides towards the ship when the load reaching it exceeds that, so it never pins the
    // vessel to a world point. Horizontal forces go through the manoeuvring solve with the other external
    // forces; vertical forces and the heel and trim moments of the line leads act on the Rigidbody directly.
    // Stepped by ShipPhysicsController.Simulate, so pause and simulation speed reach it through the physics
    // step only. A prototype holding model with estimated soil and line values, not a mooring safety analysis.
    public sealed class GroundTackle : MonoBehaviour
    {
        private static readonly ProfilerMarker PerformanceMarker = new ProfilerMarker(ProfilerCategory.Scripts, "ShipSim.Tackle");

        // Chain paid out while the anchor leaves its hawse pocket (the Part 13 unseat clip).
        public const float UnseatChainM = 0.35f;
        private const float Gravity = 9.81f;
        // Steel in fresh water keeps this fraction of its weight.
        private const float SubmergedFraction = 1 - 1000f / 7850f;
        private const float SlipMemorySeconds = 2;
        private const int DragIterations = 30;

        // Per anchor, in the order of the vessel data's anchors: id, hawse in root coordinates, and the height
        // from the chain shackle down to the anchor's crown, which reaches the bottom first.
        [SerializeField] private string[] anchorIds = Array.Empty<string>();
        [SerializeField] private Vector3[] hawsesLocal = Array.Empty<Vector3>();
        [SerializeField] private float[] anchorDrops = Array.Empty<float>();

        private ShipPhysicsController ship;
        private VesselGroundTackle spec;
        private Anchor[] anchors = Array.Empty<Anchor>();
        private readonly List<Line> lines = new List<Line>();
        private MooringPoint[] stations = Array.Empty<MooringPoint>();
        private VesselMooringStation[] stationSpecs = Array.Empty<VesselMooringStation>();
        // Station leads in vessel-root coordinates, so the physics step can place them from the Rigidbody pose.
        private Vector3[] stationLeads = Array.Empty<Vector3>();
        private readonly List<MooringPoint> shorePoints = new List<MooringPoint>();
        private bool initialised;

        public event Action<TackleEvent, string> Changed;
        // The same events with the anchor index (anchor events) or the station index (line events), for listeners
        // that act at the place where it happened.
        public event Action<TackleEvent, int> ChangedAt;
        // Power of the last step's forces at the attachment points' velocities, for energy checks: with the
        // brakes and winches holding, a positive running total would mean the lines created energy.
        public float PowerW { get; private set; }
        public string Notice { get; private set; } = string.Empty;
        public float NoticeTime { get; private set; } = float.NegativeInfinity;

        private sealed class Anchor
        {
            public string Id;
            public int Index;
            public VesselAnchor Spec;
            public Vector3 HawseLocal;
            public float Drop;
            public AnchorPhase Phase;
            public ChainCommand Command;
            public float Chain;
            public Vector3 Shackle;
            public float Embedment;
            public float SinceSlip = float.PositiveInfinity;
            public CatenaryShape Shape;
            public float RateMps;
            public float TensionN;
            public float HorizontalN;
            public bool Stalled;
            public bool NoBottom;
            public RiverBottomType Soil;
            public float ChainWeightN => Spec.chainMassPerMetreKg * Gravity * SubmergedFraction;
            public float AnchorWeightN => Spec.massKg * Gravity * SubmergedFraction;
        }

        private sealed class Line
        {
            public MooringPoint Ship;
            public MooringPoint Shore;
            public VesselMooringStation Station;
            public int StationIndex;
            public float Length;
            public LineCommand Command;
            public LineState State;
            public float RateMps;
            public CatenaryShape Shape;
            public bool ShipHigh;
            public float TensionN;
        }

        public bool IsFitted => EnsureReady();
        public int AnchorCount => EnsureReady() ? anchors.Length : 0;
        public int StationCount => EnsureReady() ? stations.Length : 0;
        public int LineCount => lines.Count;
        public VesselGroundTackle Spec => EnsureReady() ? spec : null;

        public void Configure(string[] ids, Vector3[] hawses, float[] drops)
        {
            if (ids == null || hawses == null || drops == null || ids.Length != hawses.Length || ids.Length != drops.Length)
                throw new ArgumentException("Every anchor needs an id, a hawse and a drop.");
            anchorIds = ids;
            hawsesLocal = hawses;
            anchorDrops = drops;
            initialised = false;
        }

        private void Awake() => ship = GetComponent<ShipPhysicsController>();

        private bool EnsureReady()
        {
            if (initialised) return spec != null;
            if (ship == null) ship = GetComponent<ShipPhysicsController>();
            VesselGroundTackle data = ship != null ? ship.Data?.groundTackle : null;
            if (data == null) return false;
            initialised = true;
            if (!data.fitted) return false;
            spec = data;
            var built = new List<Anchor>();
            for (int i = 0; i < anchorIds.Length; i++)
            {
                VesselAnchor anchorSpec = Array.Find(spec.anchors, a => a.id == anchorIds[i]);
                if (anchorSpec == null) continue;
                built.Add(new Anchor
                {
                    Id = anchorIds[i], Index = built.Count, Spec = anchorSpec, HawseLocal = hawsesLocal[i], Drop = anchorDrops[i]
                });
            }
            anchors = built.ToArray();
            var points = new List<MooringPoint>();
            var specs = new List<VesselMooringStation>();
            var found = new List<MooringPoint>(GetComponentsInChildren<MooringPoint>(true));
            found.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (MooringPoint point in found)
            {
                VesselMooringStation station = point.Kind == MooringPointKind.Ship
                    ? Array.Find(spec.stations, s => s.id == point.Id) : null;
                if (station == null) continue;
                points.Add(point);
                specs.Add(station);
            }
            stations = points.ToArray();
            stationSpecs = specs.ToArray();
            stationLeads = new Vector3[stations.Length];
            for (int i = 0; i < stations.Length; i++) stationLeads[i] = transform.InverseTransformPoint(stations[i].LeadWorld);
            return true;
        }

        // ---- Anchor queries ----

        public string AnchorLabel(int i) => anchors[i].Spec.label;
        public string AnchorId(int i) => anchors[i].Id;
        public AnchorPhase Phase(int i) => anchors[i].Phase;
        public ChainCommand Command(int i) => anchors[i].Command;
        public float ChainOutM(int i) => anchors[i].Chain;
        public float ChainLimitM(int i) => anchors[i].Spec.chainLengthM;
        public float ChainTensionN(int i) => anchors[i].TensionN;
        // Chain paid out per second in the last step, negative while heaving; includes the brake rendering.
        public float ChainRateMps(int i) => anchors[i].RateMps;
        public float AnchorHorizontalN(int i) => anchors[i].HorizontalN;
        public bool Dragging(int i) => anchors[i].Phase == AnchorPhase.Bottom && anchors[i].SinceSlip < SlipMemorySeconds;
        public bool Stalled(int i) => anchors[i].Stalled;
        public bool NoBottom(int i) => anchors[i].NoBottom;
        public float Embedment(int i) => anchors[i].Embedment;
        public CatenaryShape ChainShape(int i) => anchors[i].Shape;
        public Vector3 HawseLocal(int i) => anchors[i].HawseLocal;
        public Vector3 HawseWorld(int i) => transform.TransformPoint(anchors[i].HawseLocal);
        public Vector3 ShackleWorld(int i) => anchors[i].Shackle;
        public float HoldingCapacityN(int i) => Capacity(anchors[i], anchors[i].Shape);

        // What the bridge needs to know about an anchor, in the order of significance.
        public string AnchorStatus(int i)
        {
            Anchor a = anchors[i];
            switch (a.Phase)
            {
                case AnchorPhase.Lost: return "CHAIN PARTED";
                case AnchorPhase.Stowed: return "STOWED";
                case AnchorPhase.Hanging:
                    if (a.Command == ChainCommand.Heave) return a.Stalled ? "WINDLASS STALLED" : "HEAVING, AWEIGH";
                    if (a.NoBottom) return "NO BOTTOM DATA";
                    return a.Command == ChainCommand.Hold ? "HANGING" : "LETTING GO";
            }
            // Heaving drags a lighter anchor home on purpose, so the order wins over the drag.
            if (a.Command == ChainCommand.Heave) return a.Stalled ? "WINDLASS STALLED" : "HEAVING";
            if (Dragging(i)) return a.Embedment < 1 ? "SETTING" : "DRAGGING";
            if (a.Command == ChainCommand.Veer) return "VEERING";
            return a.Shape.Slack || a.HorizontalN < 0.05f * Capacity(a, a.Shape) ? "ON BOTTOM, SLACK" : "HOLDING";
        }

        // ---- Anchor commands ----

        // Let go runs the chain out freely until the anchor reaches the bottom, then the brake is set. Veer pays out
        // on the brake, heave brings chain in at the windlass rate, hold sets the brake.
        public bool SetChainCommand(int i, ChainCommand command)
        {
            if (!EnsureReady() || i < 0 || i >= anchors.Length) return false;
            Anchor a = anchors[i];
            if (a.Phase == AnchorPhase.Lost && command != ChainCommand.Hold) return Refuse(a.Spec.label + ": chain parted");
            if (command == ChainCommand.LetGo && a.Phase == AnchorPhase.Bottom) command = ChainCommand.Veer;
            if (command == ChainCommand.Veer && a.Phase == AnchorPhase.Stowed) command = ChainCommand.LetGo;
            if (command == ChainCommand.Heave && a.Phase == AnchorPhase.Stowed) return false;
            a.Command = command;
            a.Stalled = false;
            if (command == ChainCommand.LetGo && a.Phase == AnchorPhase.Stowed) Raise(TackleEvent.AnchorLetGo, a.Spec.label + " anchor let go", i);
            return true;
        }

        // ---- Mooring queries and commands ----

        public MooringPoint Station(int i) => stations[i];
        public VesselMooringStation StationSpec(int i) => stationSpecs[i];
        public int LineAt(int station)
        {
            for (int k = 0; k < lines.Count; k++) if (lines[k].Ship == stations[station]) return k;
            return -1;
        }
        public MooringPoint LineShip(int k) => lines[k].Ship;
        public MooringPoint LineShore(int k) => lines[k].Shore;
        public float LineLengthM(int k) => lines[k].Length;
        public float LineTensionN(int k) => lines[k].TensionN;
        // Line paid out per second at the winch in the last step, negative while hauling.
        public float LineRateMps(int k) => lines[k].RateMps;
        public float LineBreakingLoadN => spec?.lineBreakingLoadN ?? 0;
        public LineState LineStateAt(int k) => lines[k].State;
        public LineCommand LineCommandAt(int k) => lines[k].Command;
        public CatenaryShape LineShape(int k) => lines[k].Shape;
        public bool LineShipHigh(int k) => lines[k].ShipHigh;

        // Shore points in this vessel's scene, nearest first to the given station's lead.
        public IReadOnlyList<MooringPoint> ShorePointsNear(int station)
        {
            RefreshShorePoints();
            if (station >= 0 && station < stations.Length)
            {
                Vector3 lead = World(stationLeads[station]);
                shorePoints.Sort((a, b) => Flat(a.LeadWorld - lead).CompareTo(Flat(b.LeadWorld - lead)));
            }
            return shorePoints;
        }

        // Null when the line has been passed, otherwise why not. A line can be passed only to a shore point
        // within heaving-line reach, from a station without a line, with the vessel nearly stopped.
        public string PassLine(int station, MooringPoint shore)
        {
            if (!EnsureReady() || station < 0 || station >= stations.Length) return "NO MOORING STATION";
            if (shore == null || shore.Kind != MooringPointKind.Shore || shore.gameObject.scene != gameObject.scene)
                return "NO SHORE BOLLARD SELECTED";
            if (LineAt(station) >= 0) return stations[station].Label + " ALREADY HAS A LINE";
            int onBollard = 0;
            foreach (Line line in lines) if (line.Shore == shore) onBollard++;
            if (onBollard >= spec.linesPerBollard) return shore.Label + " IS FULL";
            Vector3 lead = World(stationLeads[station]);
            float reach = Flat(shore.LeadWorld - lead);
            if (reach > spec.passReachM) return $"{shore.Label} OUT OF REACH ({reach:0} m, max {spec.passReachM:0} m)";
            Vector3 velocity = ship.Body.linearVelocity;
            velocity.y = 0;
            if (velocity.magnitude > spec.passMaxSpeedMps) return "TOO MUCH WAY ON TO PASS A LINE";
            float length = Mathf.Clamp(Vector3.Distance(lead, shore.LeadWorld) + 1, spec.lineMinLengthM, spec.lineMaxLengthM);
            lines.Add(new Line
            {
                Ship = stations[station], Shore = shore, Station = stationSpecs[station], StationIndex = station, Length = length
            });
            Raise(TackleEvent.LinePassed, $"Line passed {stations[station].Label} to {shore.Label}", station);
            return null;
        }

        public bool SetLineCommand(int station, LineCommand command)
        {
            int k = station >= 0 && station < StationCount ? LineAt(station) : -1;
            if (k < 0) return false;
            lines[k].Command = command;
            return true;
        }

        public bool CastOff(int station)
        {
            int k = station >= 0 && station < StationCount ? LineAt(station) : -1;
            if (k < 0) return false;
            string label = lines[k].Ship.Label;
            lines.RemoveAt(k);
            Raise(TackleEvent.LineCastOff, label + " line cast off", station);
            return true;
        }

        // ---- Physics step ----

        public void Step(float dt)
        {
            using var measurement = PerformanceMarker.Auto();
            PowerW = 0;
            if (dt <= 0 || !EnsureReady() || ship.Body == null || ship.Parameters == null) return;
            foreach (Anchor a in anchors) StepAnchor(a, dt);
            for (int k = lines.Count - 1; k >= 0; k--) StepLine(k, dt);
        }

        private void StepAnchor(Anchor a, float dt)
        {
            float before = a.Chain;
            StepAnchorForces(a, dt);
            a.RateMps = (a.Chain - before) / dt;
        }

        private void StepAnchorForces(Anchor a, float dt)
        {
            a.HorizontalN = 0;
            if (a.Phase == AnchorPhase.Lost || a.Phase == AnchorPhase.Stowed && a.Command == ChainCommand.Hold)
            {
                a.TensionN = 0;
                return;
            }
            Vector3 hawse = World(a.HawseLocal);
            MoveChain(a, dt);
            if (a.Phase == AnchorPhase.Stowed) return;

            if (a.Phase == AnchorPhase.Hanging)
            {
                float hang = Mathf.Max(0, a.Chain - UnseatChainM);
                a.Shackle = hawse + Vector3.down * hang;
                a.NoBottom = !Bed(a.Shackle, out float bedY, out a.Soil);
                if (a.NoBottom || a.Shackle.y - a.Drop > bedY)
                {
                    a.Shape = default;
                    a.TensionN = a.AnchorWeightN + a.ChainWeightN * hang;
                    Apply(hawse, Vector3.up * (WeightInAir(a) - a.TensionN));
                    return;
                }
                a.Phase = AnchorPhase.Bottom;
                a.Shackle = new Vector3(hawse.x, bedY + a.Drop, hawse.z);
                a.Embedment = 0;
                a.SinceSlip = float.PositiveInfinity;
                if (a.Command == ChainCommand.LetGo) a.Command = ChainCommand.Hold;
                Raise(TackleEvent.AnchorOnBottom, a.Spec.label + " anchor on the bottom", a.Index);
            }

            if (Bed(a.Shackle, out float bed, out a.Soil)) a.Shackle.y = bed + a.Drop;
            Vector3 flat = hawse - a.Shackle;
            float height = flat.y;
            flat.y = 0;
            float span = flat.magnitude;
            Vector3 towardsHawse = span > 1e-4f ? flat / span : -transform.forward;
            CatenaryShape shape = Solve(a, a.Chain, span, height);
            // Short of the vertical height, or lifting the crown harder than the anchor weighs, the chain breaks it out.
            if (a.Chain <= height + 0.01f || shape.GroundedM <= 0 && shape.LowVerticalN > spec.breakoutWeightFactor * a.AnchorWeightN)
            {
                a.Phase = AnchorPhase.Hanging;
                a.Shape = default;
                Raise(TackleEvent.AnchorAweigh, a.Spec.label + " anchor aweigh", a.Index);
                return;
            }

            if (AtAnchor(a, shape) > Capacity(a, shape))
            {
                // The anchor slides towards the ship until the load reaching it equals what it holds; the work
                // that takes is lost to the soil.
                float low = 0, high = span;
                for (int n = 0; n < DragIterations; n++)
                {
                    float middle = 0.5f * (low + high);
                    CatenaryShape trial = Solve(a, a.Chain, span - middle, height);
                    if (AtAnchor(a, trial) > Capacity(a, trial)) low = middle;
                    else high = middle;
                }
                bool wasHolding = a.SinceSlip >= SlipMemorySeconds && a.Embedment >= 1;
                a.Shackle += towardsHawse * high;
                span -= high;
                a.Embedment = Mathf.Min(1, a.Embedment + high / spec.setDistanceM);
                a.SinceSlip = 0;
                shape = Solve(a, a.Chain, span, height);
                if (wasHolding) Raise(TackleEvent.AnchorDragging, a.Spec.label + " anchor dragging", a.Index);
            }
            else a.SinceSlip += dt;

            // The brake renders above its holding load, paying out just enough chain to bring the tension down.
            float brake = spec.brakeHoldingFraction * a.Spec.chainBreakingLoadN;
            if (shape.HighTensionN > brake)
            {
                float room = Mathf.Min(a.Spec.chainLengthM - a.Chain, spec.renderMaxMps * dt);
                if (room > 0)
                {
                    float low = 0, high = room;
                    for (int n = 0; n < DragIterations; n++)
                    {
                        float middle = 0.5f * (low + high);
                        if (Solve(a, a.Chain + middle, span, height).HighTensionN > brake) low = middle;
                        else high = middle;
                    }
                    a.Chain += high;
                    shape = Solve(a, a.Chain, span, height);
                }
                if (shape.HighTensionN > a.Spec.chainBreakingLoadN)
                {
                    a.Phase = AnchorPhase.Lost;
                    a.Command = ChainCommand.Hold;
                    a.Shape = default;
                    a.TensionN = 0;
                    Raise(TackleEvent.ChainParted, a.Spec.label + " chain parted", a.Index);
                    return;
                }
            }

            a.Shape = shape;
            a.TensionN = (float)shape.HighTensionN;
            float horizontal = Damped(shape, a.Chain, a.ChainWeightN, a.Spec.chainAxialStiffnessN, hawse, towardsHawse);
            a.HorizontalN = horizontal;
            Apply(hawse, -towardsHawse * horizontal + Vector3.up * (WeightInAir(a) - (float)shape.HighVerticalN));
        }

        private void MoveChain(Anchor a, float dt)
        {
            float limit = a.Spec.chainLengthM;
            switch (a.Command)
            {
                case ChainCommand.LetGo:
                    a.Chain = Mathf.Min(limit, a.Chain + spec.letGoMps * dt);
                    if (a.Phase == AnchorPhase.Stowed) a.Phase = AnchorPhase.Hanging;
                    if (a.Chain >= limit) a.Command = ChainCommand.Hold;
                    break;
                case ChainCommand.Veer:
                    a.Chain = Mathf.Min(limit, a.Chain + spec.veerMps * dt);
                    if (a.Chain >= limit) a.Command = ChainCommand.Hold;
                    break;
                case ChainCommand.Heave:
                    // Full speed up to the nominal pull, slowing to a stall at the overload pull.
                    float load = Mathf.InverseLerp(a.Spec.stallPullN, a.Spec.nominalPullN, a.TensionN);
                    bool stalled = load <= 0;
                    if (stalled && !a.Stalled) Raise(TackleEvent.WindlassStalled, a.Spec.label + " windlass stalled", a.Index);
                    a.Stalled = stalled;
                    a.Chain = Mathf.Max(0, a.Chain - a.Spec.heaveMps * load * dt);
                    if (a.Chain <= 0)
                    {
                        a.Phase = AnchorPhase.Stowed;
                        a.Command = ChainCommand.Hold;
                        a.Shape = default;
                        a.TensionN = 0;
                        Raise(TackleEvent.AnchorStowed, a.Spec.label + " anchor stowed", a.Index);
                    }
                    break;
            }
        }

        private void StepLine(int k, float dt)
        {
            Line line = lines[k];
            if (line.Shore == null || line.Ship == null)
            {
                lines.RemoveAt(k);
                return;
            }
            float rated = line.Station.winchPullN;
            float before = line.Length;
            if (line.Command == LineCommand.Haul)
                line.Length = Mathf.Max(spec.lineMinLengthM, line.Length -
                    line.Station.haulMps * Mathf.InverseLerp(rated * spec.stallPullFactor, rated, line.TensionN) * dt);
            else if (line.Command == LineCommand.Slack)
                line.Length = Mathf.Min(spec.lineMaxLengthM, line.Length + spec.slackMps * dt);
            line.RateMps = (line.Length - before) / dt;

            Vector3 lead = World(stationLeads[line.StationIndex]), shore = line.Shore.LeadWorld;
            Vector3 flat = shore - lead;
            float rise = flat.y;
            flat.y = 0;
            float span = flat.magnitude;
            Vector3 towardsShore = span > 1e-4f ? flat / span : Vector3.zero;
            float weight = spec.lineMassPerMetreKg * Gravity;
            line.ShipHigh = rise <= 0;
            line.Shape = Catenary.Solve(line.Length, span, Mathf.Abs(rise), weight, spec.lineAxialStiffnessN, false);
            line.TensionN = (float)line.Shape.HighTensionN;
            if (line.TensionN > spec.lineBreakingLoadN)
            {
                string label = line.Ship.Label;
                lines.RemoveAt(k);
                Raise(TackleEvent.LineParted, label + " line parted", line.StationIndex);
                return;
            }
            line.State = line.TensionN > spec.overloadFraction * spec.lineBreakingLoadN ? LineState.Overloaded :
                line.Shape.HorizontalN > 0.02f * rated ? LineState.Taut : LineState.Slack;
            float horizontal = Damped(line.Shape, line.Length, weight, spec.lineAxialStiffnessN, lead, -towardsShore);
            float vertical = (float)(line.ShipHigh ? -line.Shape.HighVerticalN : line.Shape.LowVerticalN);
            Apply(lead, towardsShore * horizontal + Vector3.up * vertical);
        }

        // Horizontal tension plus damping on the rate the span opens, scaled by how taut the line is so a
        // slack line passes nothing; never below zero, so a line only pulls.
        private float Damped(in CatenaryShape shape, float length, float weight, float stiffness, Vector3 point, Vector3 away)
        {
            if (shape.Slack) return 0;
            double k = Catenary.HorizontalStiffness(shape, length, weight, stiffness);
            double c = 2 * spec.dampingRatio * Math.Sqrt(k * ship.Body.mass);
            Vector3 velocity = ship.Body.GetPointVelocity(point);
            velocity.y = 0;
            double opening = Vector3.Dot(velocity, away);
            return (float)Math.Max(0, shape.HorizontalN + c * opening);
        }

        // The horizontal force acts in the manoeuvring solve, which places all lateral force at half draft;
        // the heel and trim moment of the lead's height above that is added here, the vertical force directly.
        private void Apply(Vector3 point, Vector3 force)
        {
            Rigidbody body = ship.Body;
            PowerW += Vector3.Dot(force, body.GetPointVelocity(point));
            Vector3 horizontal = new Vector3(force.x, 0, force.z);
            if (horizontal.sqrMagnitude > 0)
            {
                ship.AddExternalForce(horizontal, point);
                VesselParameters p = ship.Parameters;
                float reference = World(new Vector3(0, p.KeelLocalY + 0.5f * p.Draft, 0)).y;
                body.AddTorque(Vector3.Cross(Vector3.up * (point.y - reference), horizontal), ForceMode.Force);
            }
            if (force.y != 0) body.AddForceAtPosition(Vector3.up * force.y, point, ForceMode.Force);
        }

        // The Rigidbody pose is current inside the physics step even right after a restore, when the transform
        // still holds the previous position until the next simulation.
        private Vector3 World(Vector3 local) => ship.Body.position + ship.Body.rotation * local;

        private CatenaryShape Solve(Anchor a, float chain, float span, float height) =>
            Catenary.Solve(chain, span, height, a.ChainWeightN, a.Spec.chainAxialStiffnessN, true);

        // Load reaching the anchor: the horizontal tension less the friction of the chain lying on the bed.
        private float AtAnchor(Anchor a, in CatenaryShape shape) =>
            (float)Math.Max(0, shape.HorizontalN - spec.chainBedFriction * a.ChainWeightN * shape.GroundedM);

        // Holding grows from the unset value to full over the setting drag, and falls away as the chain lifts the shank.
        private float Capacity(Anchor a, in CatenaryShape shape)
        {
            float ratio = a.Soil == RiverBottomType.Silt ? spec.siltHoldingRatio :
                a.Soil == RiverBottomType.Rock ? spec.rockHoldingRatio : spec.sandHoldingRatio;
            float uplift = shape.GroundedM > 0 || shape.Slack ? 0 :
                Mathf.Atan2((float)Math.Max(0, shape.LowVerticalN), (float)shape.HorizontalN) * Mathf.Rad2Deg;
            return ratio * a.AnchorWeightN * Mathf.Lerp(spec.initialSetFraction, 1, a.Embedment) *
                Mathf.Clamp01(1 - uplift / spec.maxUpliftDeg);
        }

        // Anchor and chain are part of the lightship mass; outboard, the water and the bed carry part of them.
        private static float WeightInAir(Anchor a) => (a.Spec.massKg + a.Spec.chainMassPerMetreKg * a.Chain) * Gravity;

        private bool Bed(Vector3 position, out float bedY, out RiverBottomType soil)
        {
            BathymetrySample sample = ship.SampleBottom(position);
            soil = sample.BottomType;
            bedY = ship.WaterLevel - sample.DepthM;
            return float.IsFinite(sample.DepthM) && sample.DepthM > 0;
        }

        private void RefreshShorePoints()
        {
            shorePoints.Clear();
            foreach (MooringPoint point in FindObjectsByType<MooringPoint>())
                if (point.Kind == MooringPointKind.Shore && point.gameObject.scene == gameObject.scene) shorePoints.Add(point);
        }

        private static float Flat(Vector3 value) => new Vector2(value.x, value.z).magnitude;

        private bool Refuse(string message)
        {
            Notice = message.ToUpperInvariant();
            NoticeTime = Time.unscaledTime;
            return false;
        }

        private void Raise(TackleEvent change, string message, int index)
        {
            Notice = message.ToUpperInvariant();
            NoticeTime = Time.unscaledTime;
            Changed?.Invoke(change, message);
            ChangedAt?.Invoke(change, index);
        }

        // ---- Lifecycle ----

        public void ResetState()
        {
            foreach (Anchor a in anchors)
            {
                a.Phase = AnchorPhase.Stowed;
                a.Command = ChainCommand.Hold;
                a.Chain = a.Embedment = a.TensionN = a.HorizontalN = a.RateMps = 0;
                a.Shape = default;
                a.Stalled = a.NoBottom = false;
                a.SinceSlip = float.PositiveInfinity;
            }
            lines.Clear();
            Notice = string.Empty;
            NoticeTime = float.NegativeInfinity;
        }

        public GroundTackleSave CaptureState()
        {
            var save = new GroundTackleSave { present = true };
            if (!EnsureReady()) return save;
            save.anchors = new AnchorSave[anchors.Length];
            for (int i = 0; i < anchors.Length; i++)
            {
                Anchor a = anchors[i];
                save.anchors[i] = new AnchorSave
                {
                    id = a.Id, phase = (int)a.Phase, command = (int)a.Command, chainOutM = a.Chain,
                    position = a.Shackle, embedment = a.Embedment
                };
            }
            save.lines = new MooringLineSave[lines.Count];
            for (int k = 0; k < lines.Count; k++)
                save.lines[k] = new MooringLineSave
                {
                    shipPointId = lines[k].Ship.Id, shorePointId = lines[k].Shore.Id, lengthM = lines[k].Length,
                    command = (int)lines[k].Command
                };
            return save;
        }

        // Restores the saved positions and lengths exactly, so the next step finds the same tension. Saves from
        // before ground tackle carry only the chain paid out, port, starboard and stern; those anchors hang from
        // the hawse and settle on the bottom below it, as the Part 13 visual showed them. Returns a message when
        // part of the save could not be restored here, otherwise null.
        public string RestoreState(GroundTackleSave save, float[] legacyChainM)
        {
            ResetState();
            if (!EnsureReady()) return null;
            var missing = new List<string>();
            if (save != null && save.present)
            {
                save.Validate();
                foreach (AnchorSave saved in save.anchors)
                {
                    Anchor a = Array.Find(anchors, x => x.Id == saved.id);
                    if (a == null) continue;
                    a.Phase = (AnchorPhase)saved.phase;
                    a.Command = (ChainCommand)saved.command;
                    a.Chain = Mathf.Clamp(saved.chainOutM, 0, a.Spec.chainLengthM);
                    a.Shackle = saved.position;
                    a.Embedment = saved.embedment;
                    if (a.Phase == AnchorPhase.Stowed && a.Chain > 0) a.Phase = AnchorPhase.Hanging;
                }
                RefreshShorePoints();
                foreach (MooringLineSave saved in save.lines)
                {
                    int station = Array.FindIndex(stations, s => s.Id == saved.shipPointId);
                    MooringPoint shore = shorePoints.Find(p => p.Id == saved.shorePointId);
                    if (station < 0 || shore == null)
                    {
                        missing.Add(saved.shorePointId);
                        continue;
                    }
                    lines.Add(new Line
                    {
                        Ship = stations[station], Shore = shore, Station = stationSpecs[station], StationIndex = station,
                        Length = Mathf.Clamp(saved.lengthM, spec.lineMinLengthM, spec.lineMaxLengthM),
                        Command = (LineCommand)saved.command
                    });
                }
            }
            else if (legacyChainM != null)
                for (int i = 0; i < anchors.Length && i < legacyChainM.Length; i++)
                {
                    anchors[i].Chain = Mathf.Clamp(legacyChainM[i], 0, anchors[i].Spec.chainLengthM);
                    if (anchors[i].Chain > 0) anchors[i].Phase = AnchorPhase.Hanging;
                }
            if (missing.Count == 0) return null;
            string message = "Mooring point " + string.Join(", ", missing) + " is not in this scenario; that line was not restored";
            Refuse(message);
            return message;
        }
    }
}
