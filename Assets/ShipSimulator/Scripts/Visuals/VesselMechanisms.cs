using Unity.Profiling;
using System;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Visuals
{
    public enum HatchPosition { Closed, HalfOpen, ForwardOpen, AftOpen }

    public enum HatchPhase { Closed, LiftingOuter, LiftingInner, SlidingInner, LoweringOuter, Stacked, Traversing, HoldOpen }

    // Real-time machinery rates, in physical units. VolgoDonMechanismIntegration fills them from the
    // operating_rates block of mechanisms.json; that file records the source and uncertainty of each value.
    [Serializable]
    public sealed class MachineryRates
    {

        [Min(1)] public float outerLiftSeconds = 141;
        [Min(1)] public float innerLiftSeconds = 34;
        [Min(1)] public float innerSlideSeconds = 174;
        [Min(1)] public float outerLowerSeconds = 16;
        [Min(1)] public float traverseSeconds = 371;
        [Min(0.1f)] public float chainPerGypsyTurnM = 1.72f;
    }

    // Samples only machinery curves; the physics root is never an animation target. The review clip is an
    // accelerated sequence: its hatch phases are re-timed here to the real-time rates. Anchors follow
    // GroundTackle, which owns the chain paid out and the anchor positions; this only poses them.
    public sealed class VesselMechanisms : MonoBehaviour
    {
        private static readonly ProfilerMarker PerformanceMarker = new ProfilerMarker(ProfilerCategory.Scripts, "ShipSim.Mechanisms");

        // Review-clip frames bounding each opening phase (part4_rig.py): outer lift, inner rolling lift and
        // forward-outer shift, inner slide under, outer lowered onto the stack.
        private static readonly float[] OpeningFrames = { 90, 100, 120, 180, 210 };

        [SerializeField] private GameObject visual;
        [SerializeField] private AnimationClip hatchClip;
        [SerializeField] private AnimationClip anchorClip;
        [SerializeField] private Transform[] propellers;
        [SerializeField] private Transform[] nozzles;
        [SerializeField] private Vector3[] shaftAxes;
        [SerializeField] private Vector3[] steeringAxes;
        [SerializeField] private Quaternion[] shaftRest;
        [SerializeField] private Quaternion[] nozzleRest;
        [Header("Anchors: port, starboard, stern")]
        [SerializeField] private Transform[] anchors = Array.Empty<Transform>();
        [SerializeField] private Transform[] chainEnds = Array.Empty<Transform>();
        // Hawse points in vessel-root coordinates, where the chain leaves the hull.
        [SerializeField] private Vector3[] hawses = Array.Empty<Vector3>();
        [SerializeField] private Transform[] gypsies = Array.Empty<Transform>();
        [SerializeField] private Vector3[] gypsyAxes = Array.Empty<Vector3>();
        [SerializeField] private Quaternion[] gypsyRest = Array.Empty<Quaternion>();
        // Height from each chain shackle down to its anchor's crown, which reaches the bottom first.
        [SerializeField] private float[] anchorDrops = Array.Empty<float>();
        [SerializeField] private MachineryRates rates = new MachineryRates();
        private ShipPhysicsController ship;
        private GroundTackle tackle;
        private MechanismSave state = new MechanismSave();

        public bool IsConfigured => visual != null && hatchClip != null && anchorClip != null;
        public HatchPosition Target => (HatchPosition)state.hatchTarget;
        public float Opening => state.opening;
        public float Parking => state.parking;
        public MachineryRates Rates => rates;
        // Hawses in vessel-root coordinates and shackle-to-crown drops, port, starboard, stern, for GroundTackle.
        public Vector3[] HawsesLocal => (Vector3[])hawses.Clone();
        public float[] AnchorDrops => (float[])anchorDrops.Clone();
        // Editor review captures only; gameplay keeps 1 and simulation speed already scales Time.deltaTime.
        public float PreviewSpeedMultiplier { get; set; } = 1;
        public bool IsMoving => HatchesMoving;
        public bool HatchesMoving => Mathf.Abs(state.parking - DesiredParking) > .0001f ||
            Mathf.Abs(state.opening - (Target == HatchPosition.Closed ? 0 : 1)) > .0001f;
        public Vector3 ChainEndWorld(int anchor) => chainEnds[anchor].position;
        public int AnchorPivotCount => anchors.Length;
        private float DesiredParking => Target == HatchPosition.ForwardOpen ? -1 : Target == HatchPosition.AftOpen ? 1 : 0;

        public HatchPhase Phase
        {
            get
            {
                if (Mathf.Abs(state.parking) > .0001f)
                    return Mathf.Abs(state.parking) >= .9999f ? HatchPhase.HoldOpen : HatchPhase.Traversing;
                if (state.opening <= .0001f) return HatchPhase.Closed;
                if (state.opening >= .9999f) return HatchPhase.Stacked;
                float frame = Mathf.Lerp(OpeningFrames[0], OpeningFrames[4], state.opening);
                return frame < OpeningFrames[1] ? HatchPhase.LiftingOuter : frame < OpeningFrames[2] ? HatchPhase.LiftingInner :
                    frame < OpeningFrames[3] ? HatchPhase.SlidingInner : HatchPhase.LoweringOuter;
            }
        }

        // Seconds of real-time operation left until the hatches reach the target, at the configured rates.
        public float HatchSecondsRemaining
        {
            get
            {
                float parkingTarget = DesiredParking;
                bool crossing = state.parking * parkingTarget < 0 || Target == HatchPosition.Closed;
                float seconds = (crossing ? Mathf.Abs(state.parking) + Mathf.Abs(parkingTarget) :
                    Mathf.Abs(parkingTarget - state.parking)) * rates.traverseSeconds;
                float openingTarget = Target == HatchPosition.Closed ? 0 : 1;
                return seconds + OpeningSeconds(state.opening, openingTarget);
            }
        }

        public void Configure(GameObject model, AnimationClip hatches, AnimationClip anchorsClip,
            Transform[] shafts, Transform[] ducts, Vector3[] shaftDirections, Vector3[] steeringDirections)
        {
            visual = model; hatchClip = hatches; anchorClip = anchorsClip;
            propellers = shafts; nozzles = ducts;
            shaftAxes = shaftDirections; steeringAxes = steeringDirections;
            shaftRest = new[] { shafts[0].localRotation, shafts[1].localRotation };
            nozzleRest = new[] { ducts[0].localRotation, ducts[1].localRotation };
            // A new visual replaces the old one; its anchors are bound again by ConfigureAnchors.
            anchors = chainEnds = gypsies = Array.Empty<Transform>();
            hawses = gypsyAxes = Array.Empty<Vector3>();
            gypsyRest = Array.Empty<Quaternion>();
            anchorDrops = Array.Empty<float>();
            state = new MechanismSave();
            ApplyPose(0);
        }

        // Anchors in the order port, starboard, stern; gypsies port and starboard. Call with the pose at rest.
        public void ConfigureAnchors(Transform[] anchorPivots, Transform[] ends, Transform[] gypsyPivots,
            Vector3[] gypsyDirections, MachineryRates machineryRates)
        {
            anchors = anchorPivots; chainEnds = ends; gypsies = gypsyPivots; gypsyAxes = gypsyDirections;
            rates = machineryRates ?? new MachineryRates();
            hawses = new Vector3[ends.Length];
            for (int i = 0; i < ends.Length; i++) hawses[i] = transform.InverseTransformPoint(ends[i].position);
            gypsyRest = new Quaternion[gypsyPivots.Length];
            for (int i = 0; i < gypsyPivots.Length; i++) gypsyRest[i] = gypsyPivots[i].localRotation;
            anchorDrops = new float[anchorPivots.Length];
            for (int i = 0; i < anchorPivots.Length; i++)
            {
                float lowest = float.PositiveInfinity;
                foreach (Renderer renderer in anchorPivots[i].GetComponentsInChildren<Renderer>(true))
                    lowest = Mathf.Min(lowest, renderer.bounds.min.y);
                anchorDrops[i] = float.IsInfinity(lowest) ? 0 : Mathf.Max(0, ends[i].position.y - lowest);
            }
            ApplyPose(0);
        }

        private void Awake()
        {
            ship = GetComponent<ShipPhysicsController>();
            tackle = GetComponent<GroundTackle>();
        }

        private void LateUpdate()
        {
            if (!IsConfigured || ship == null || ship.ManualStepping) return;
            Advance(Time.deltaTime * Mathf.Max(1, PreviewSpeedMultiplier), ship.EngineCount > 0 ? ship.ShaftRpm(0) : 0,
                ship.EngineCount > 1 ? ship.ShaftRpm(1) : 0, ship.RudderAngleDeg);
        }

        public void SetHatches(HatchPosition target)
        {
            if (!Enum.IsDefined(typeof(HatchPosition), target)) throw new ArgumentOutOfRangeException(nameof(target));
            state.hatchTarget = (int)target;
        }

        public void Advance(float seconds, float portRpm, float starboardRpm, float rudderDegrees)
        {
            using var measurement = PerformanceMarker.Auto();
            if (!IsConfigured || seconds < 0 || !float.IsFinite(seconds)) return;
            AdvanceHatches(seconds);
            state.portAngle = Mathf.Repeat(state.portAngle + portRpm * 6 * seconds, 360);
            state.starboardAngle = Mathf.Repeat(state.starboardAngle + starboardRpm * 6 * seconds, 360);
            ApplyPose(rudderDegrees);
        }

        // Spends the step phase by phase, so a long step lands where many short ones would.
        private void AdvanceHatches(float seconds)
        {
            for (int guard = 0; guard < 16 && seconds > 0; guard++)
            {
                // Return to the half-open stack before closing or crossing to the other hold.
                float parkingTarget = DesiredParking;
                if (state.opening < 1 || state.parking * parkingTarget < 0 || Target == HatchPosition.Closed)
                    parkingTarget = 0;
                if (Mathf.Abs(state.parking - parkingTarget) > 1e-6f)
                {
                    float need = Mathf.Abs(parkingTarget - state.parking) * rates.traverseSeconds;
                    float used = Mathf.Min(need, seconds);
                    state.parking = used >= need ? parkingTarget :
                        Mathf.MoveTowards(state.parking, parkingTarget, used / rates.traverseSeconds);
                    seconds -= used;
                    continue;
                }
                state.parking = parkingTarget;
                if (Mathf.Abs(state.parking) > 1e-6f) return;
                float openingTarget = Target == HatchPosition.Closed ? 0 : 1;
                if (Mathf.Abs(state.opening - openingTarget) <= 1e-6f)
                {
                    state.opening = openingTarget;
                    if (Mathf.Abs(DesiredParking) < 1e-6f || openingTarget == 0) return;
                    continue;
                }
                int phase = PhaseIndex(state.opening, openingTarget > state.opening);
                float lower = PhaseStart(phase), upper = PhaseStart(phase + 1);
                float boundary = openingTarget > state.opening ? upper : lower;
                float rate = (upper - lower) / PhaseSeconds(phase);
                float distance = Mathf.Abs(boundary - state.opening);
                float spent = Mathf.Min(seconds, distance / rate);
                state.opening = spent * rate >= distance - 1e-7f ? boundary :
                    Mathf.MoveTowards(state.opening, boundary, spent * rate);
                seconds -= spent;
            }
        }

        private static float PhaseStart(int phase) =>
            (OpeningFrames[phase] - OpeningFrames[0]) / (OpeningFrames[4] - OpeningFrames[0]);

        // The phase a step moves through: on a boundary, the one ahead in the direction of travel.
        private static int PhaseIndex(float opening, bool opens)
        {
            for (int phase = 0; phase < 4; phase++)
            {
                float lower = PhaseStart(phase), upper = PhaseStart(phase + 1);
                if (opens ? opening >= lower - 1e-6f && opening < upper - 1e-6f :
                    opening > lower + 1e-6f && opening <= upper + 1e-6f)
                    return phase;
            }
            return opens ? 3 : 0;
        }

        private float PhaseSeconds(int phase) => phase switch
        {
            0 => rates.outerLiftSeconds,
            1 => rates.innerLiftSeconds,
            2 => rates.innerSlideSeconds,
            _ => rates.outerLowerSeconds
        };

        private float OpeningSeconds(float from, float to)
        {
            float seconds = 0;
            for (int phase = 0; phase < 4; phase++)
            {
                float lower = PhaseStart(phase), upper = PhaseStart(phase + 1);
                float a = Mathf.Clamp(Mathf.Min(from, to), lower, upper), b = Mathf.Clamp(Mathf.Max(from, to), lower, upper);
                seconds += (b - a) / (upper - lower) * PhaseSeconds(phase);
            }
            return seconds;
        }

        private void ApplyPose(float rudderDegrees)
        {
            if (!IsConfigured) return;
            float frame = state.parking < 0 ? Mathf.Lerp(210, 300, -state.parking) :
                state.parking > 0 ? Mathf.Lerp(390, 480, state.parking) : Mathf.Lerp(90, 210, state.opening);
            hatchClip.SampleAnimation(visual, (frame - 1) / 24f);
            for (int i = 0; i < 2; i++)
            {
                float angle = i == 0 ? state.portAngle : state.starboardAngle;
                propellers[i].localRotation = Quaternion.AngleAxis(angle, shaftAxes[i]) * shaftRest[i];
                nozzles[i].localRotation = Quaternion.AngleAxis(Mathf.Clamp(rudderDegrees, -35, 35), steeringAxes[i]) * nozzleRest[i];
            }
            PoseAnchors();
        }

        // The clip unseats all anchors together over the first chain paid out; beyond that each hangs plumb on its
        // chain or lies where GroundTackle has it on the bottom. Gypsies turn with their own chain.
        private void PoseAnchors()
        {
            if (tackle == null) tackle = GetComponent<GroundTackle>();
            GroundTackle source = tackle != null && tackle.IsFitted ? tackle : null;
            int count = source != null ? Mathf.Min(source.AnchorCount, anchors.Length) : 0;
            float most = 0;
            for (int i = 0; i < count; i++) most = Mathf.Max(most, source.ChainOutM(i));
            anchorClip.SampleAnimation(visual, Mathf.Lerp(709, 739, Mathf.Clamp01(most / GroundTackle.UnseatChainM)) / 24f);
            for (int i = 0; i < count; i++)
            {
                AnchorPhase phase = source.Phase(i);
                Vector3 shackle = chainEnds[i].position;
                Vector3 target = phase == AnchorPhase.Bottom ? source.ShackleWorld(i) :
                    phase == AnchorPhase.Hanging ? shackle + Vector3.down * Mathf.Max(0, source.ChainOutM(i) - GroundTackle.UnseatChainM) :
                    shackle;
                anchors[i].position += target - shackle;
            }
            for (int i = 0; i < gypsies.Length && i < count; i++)
            {
                // Paying out moves each gypsy's top towards the bow on both sides.
                float turns = source.ChainOutM(i) / rates.chainPerGypsyTurnM;
                gypsies[i].localRotation = Quaternion.AngleAxis(Mathf.Repeat(turns * 360, 360), gypsyAxes[i]) * gypsyRest[i];
            }
        }

        public MechanismSave CaptureState() => JsonUtility.FromJson<MechanismSave>(JsonUtility.ToJson(state));

        public void RestoreState(MechanismSave saved)
        {
            saved?.Validate();
            state = saved == null ? new MechanismSave() : JsonUtility.FromJson<MechanismSave>(JsonUtility.ToJson(saved));
            // Anchors are GroundTackle's; this keeps only hatches and shafts.
            state.anchorTarget = false;
            state.anchorRelease = 0;
            state.chainOutM = null;
            ApplyPose(ship != null ? ship.RudderAngleDeg : 0);
        }
    }
}
