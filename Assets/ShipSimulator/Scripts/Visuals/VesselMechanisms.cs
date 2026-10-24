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
        [Min(0.01f)] public float letGoMetresPerSecond = 1;
        [Min(0.01f)] public float bowHeaveMetresPerSecond = 10f / 60f;
        [Min(0.01f)] public float sternHeaveMetresPerSecond = 18f / 60f;
        [Min(1)] public float bowChainLimitM = 150;
        [Min(1)] public float sternChainLimitM = 75;
        [Min(0.1f)] public float chainPerGypsyTurnM = 1.72f;
    }

    // Samples only machinery curves; the physics root is never an animation target. The review clip is an
    // accelerated sequence: its hatch phases are re-timed here to the real-time rates, and the anchors' chain
    // payout is added on top of the clip's short unseat. Visual only: no anchor holding force (Part 14).
    public sealed class VesselMechanisms : MonoBehaviour
    {
        // Review-clip frames bounding each opening phase (part4_rig.py): outer lift, inner rolling lift and
        // forward-outer shift, inner slide under, outer lowered onto the stack.
        private static readonly float[] OpeningFrames = { 90, 100, 120, 180, 210 };
        // Chain paid out over the clip's unseat motion before the anchor hangs clear of the hawse.
        private const float UnseatChainM = 0.35f;
        // One texture tile shows two links of 4 d each for the archive 43 mm bow chain.
        private const float ChainTileM = 2 * 4 * 0.043f;

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
        [SerializeField] private Material chainMaterial;
        [SerializeField, Min(0.01f)] private float chainWidthM = 0.15f;
        [SerializeField] private MachineryRates rates = new MachineryRates();
        private ShipPhysicsController ship;
        private MechanismSave state = new MechanismSave();
        private LineRenderer[] chains;

        public bool IsConfigured => visual != null && hatchClip != null && anchorClip != null;
        public HatchPosition Target => (HatchPosition)state.hatchTarget;
        public float Opening => state.opening;
        public float Parking => state.parking;
        public float AnchorRelease => state.anchorRelease;
        public bool AnchorsReleased => state.anchorTarget;
        public MachineryRates Rates => rates;
        // Editor review captures only; gameplay keeps 1 and simulation speed already scales Time.deltaTime.
        public float PreviewSpeedMultiplier { get; set; } = 1;
        // Water depth below a point for editor previews without a running ship; the ship's own depth wins.
        public Func<Vector3, float> PreviewDepth { get; set; }
        public bool IsMoving => HatchesMoving || AnchorsMoving;
        public bool HatchesMoving => Mathf.Abs(state.parking - DesiredParking) > .0001f ||
            Mathf.Abs(state.opening - (Target == HatchPosition.Closed ? 0 : 1)) > .0001f;
        public bool AnchorsMoving { get; private set; }
        public float ChainOutM(int anchor) => Chain()[anchor];
        public float ChainLimitM(int anchor) => anchor < 2 ? rates.bowChainLimitM : rates.sternChainLimitM;
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
            chains = null;
            state = new MechanismSave();
            ApplyPose(0);
        }

        // Anchors in the order port, starboard, stern; gypsies port and starboard. Call with the pose at rest.
        public void ConfigureAnchors(Transform[] anchorPivots, Transform[] ends, Transform[] gypsyPivots,
            Vector3[] gypsyDirections, Material chain, MachineryRates machineryRates)
        {
            anchors = anchorPivots; chainEnds = ends; gypsies = gypsyPivots; gypsyAxes = gypsyDirections;
            chainMaterial = chain;
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

        private void Awake() => ship = GetComponent<ShipPhysicsController>();

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

        public void SetAnchorsReleased(bool released) => state.anchorTarget = released;

        public void Advance(float seconds, float portRpm, float starboardRpm, float rudderDegrees)
        {
            if (!IsConfigured || seconds < 0 || !float.IsFinite(seconds)) return;
            AdvanceHatches(seconds);
            AdvanceAnchors(seconds);
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

        private float[] Chain()
        {
            if (state.chainOutM == null || state.chainOutM.Length != 3)
            {
                // Saves from before chain payout kept only the unseat fraction.
                float unseat = state.anchorRelease * UnseatChainM;
                state.chainOutM = new[] { unseat, unseat, unseat };
            }
            return state.chainOutM;
        }

        // Let go pays chain out until the anchor rests on the bottom or the chain is all out; heave up brings
        // it in at the windlass (bow) or capstan (stern) rate. The brake holds the chain wherever it stops: in
        // shallower water the anchor rests on the bottom with slack, it is not hove in by itself.
        private void AdvanceAnchors(float seconds)
        {
            float[] chain = Chain();
            AnchorsMoving = false;
            for (int i = 0; i < chain.Length; i++)
            {
                float target = state.anchorTarget ? Mathf.Max(chain[i], Mathf.Min(ChainLimitM(i), ChainToBottom(i))) : 0;
                float rate = state.anchorTarget ? rates.letGoMetresPerSecond :
                    i < 2 ? rates.bowHeaveMetresPerSecond : rates.sternHeaveMetresPerSecond;
                chain[i] = Mathf.MoveTowards(chain[i], target, rate * seconds);
                AnchorsMoving |= Mathf.Abs(chain[i] - target) > 1e-4f;
            }
            float most = Mathf.Max(chain[0], Mathf.Max(chain[1], chain[2]));
            state.anchorRelease = Mathf.Clamp01(most / UnseatChainM);
        }

        // Chain needed for the anchor to reach the bottom below its hawse; unbounded without bathymetry.
        private float ChainToBottom(int anchor)
        {
            bool running = ship != null && ship.Data != null;
            if ((!running && PreviewDepth == null) || anchor >= hawses.Length) return float.PositiveInfinity;
            Vector3 hawse = transform.TransformPoint(hawses[anchor]);
            float depth = running ? ship.SampleDepth(hawse) : PreviewDepth(hawse);
            if (float.IsInfinity(depth) || float.IsNaN(depth)) return float.PositiveInfinity;
            float bottom = (running ? ship.WaterLevel : 0) - Mathf.Max(depth, 0);
            float drop = anchor < anchorDrops.Length ? anchorDrops[anchor] : 0;
            return Mathf.Max(UnseatChainM, UnseatChainM + hawse.y - bottom - drop);
        }

        private void ApplyPose(float rudderDegrees)
        {
            if (!IsConfigured) return;
            float frame = state.parking < 0 ? Mathf.Lerp(210, 300, -state.parking) :
                state.parking > 0 ? Mathf.Lerp(390, 480, state.parking) : Mathf.Lerp(90, 210, state.opening);
            hatchClip.SampleAnimation(visual, (frame - 1) / 24f);
            float[] chain = Chain();
            float unseat = Mathf.Clamp01(Mathf.Max(chain[0], Mathf.Max(chain[1], chain[2])) / UnseatChainM);
            anchorClip.SampleAnimation(visual, Mathf.Lerp(709, 739, unseat) / 24f);
            for (int i = 0; i < 2; i++)
            {
                float angle = i == 0 ? state.portAngle : state.starboardAngle;
                propellers[i].localRotation = Quaternion.AngleAxis(angle, shaftAxes[i]) * shaftRest[i];
                nozzles[i].localRotation = Quaternion.AngleAxis(Mathf.Clamp(rudderDegrees, -35, 35), steeringAxes[i]) * nozzleRest[i];
            }
            for (int i = 0; i < anchors.Length; i++)
            {
                // Beyond the unseat the anchor hangs plumb under the hawse on the chain paid out, down to the bottom.
                float drop = Mathf.Max(0, Mathf.Min(chain[i], ChainToBottom(i)) - UnseatChainM);
                if (drop > 0) anchors[i].localPosition += anchors[i].parent.InverseTransformVector(Vector3.down * drop);
            }
            for (int i = 0; i < gypsies.Length && i < chain.Length; i++)
            {
                // Each gypsy turns with its own chain; paying out moves its top towards the bow on both sides.
                float turns = chain[i] / rates.chainPerGypsyTurnM;
                gypsies[i].localRotation = Quaternion.AngleAxis(Mathf.Repeat(turns * 360, 360), gypsyAxes[i]) * gypsyRest[i];
            }
            UpdateChains(chain);
        }

        private void UpdateChains(float[] chain)
        {
            if (chainEnds.Length == 0) return;
            if (chains == null || chains.Length != chainEnds.Length)
            {
                chains = new LineRenderer[chainEnds.Length];
                for (int i = 0; i < chains.Length; i++)
                {
                    var line = new GameObject("AnchorChain_" + anchors[i].name, typeof(LineRenderer));
                    line.hideFlags = HideFlags.DontSave;
                    line.transform.SetParent(transform, false);
                    chains[i] = line.GetComponent<LineRenderer>();
                    chains[i].useWorldSpace = true;
                    chains[i].positionCount = 2;
                    chains[i].sharedMaterial = chainMaterial;
                    chains[i].textureMode = LineTextureMode.Tile;
                    chains[i].textureScale = new Vector2(1 / ChainTileM, 1);
                    chains[i].widthMultiplier = chainWidthM;
                    chains[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
            for (int i = 0; i < chains.Length; i++)
            {
                bool visible = chain[i] > UnseatChainM && chainMaterial != null;
                chains[i].enabled = visible;
                if (!visible) continue;
                chains[i].SetPosition(0, transform.TransformPoint(hawses[i]));
                chains[i].SetPosition(1, chainEnds[i].position);
            }
        }

        public MechanismSave CaptureState()
        {
            Chain();
            return JsonUtility.FromJson<MechanismSave>(JsonUtility.ToJson(state));
        }

        public void RestoreState(MechanismSave saved)
        {
            saved?.Validate();
            state = saved == null ? new MechanismSave() : JsonUtility.FromJson<MechanismSave>(JsonUtility.ToJson(saved));
            float[] chain = Chain();
            for (int i = 0; i < chain.Length; i++) chain[i] = Mathf.Clamp(chain[i], 0, ChainLimitM(i));
            ApplyPose(ship != null ? ship.RudderAngleDeg : 0);
        }
    }
}
