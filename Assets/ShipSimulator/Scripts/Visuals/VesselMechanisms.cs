using System;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Visuals
{
    public enum HatchPosition { Closed, HalfOpen, ForwardOpen, AftOpen }

    // Samples only machinery curves; the physics root is never an animation target.
    public sealed class VesselMechanisms : MonoBehaviour
    {
        [SerializeField] private GameObject visual;
        [SerializeField] private AnimationClip hatchClip;
        [SerializeField] private AnimationClip anchorClip;
        [SerializeField] private Transform[] propellers;
        [SerializeField] private Transform[] nozzles;
        [SerializeField] private Vector3[] shaftAxes;
        [SerializeField] private Vector3[] steeringAxes;
        [SerializeField] private Quaternion[] shaftRest;
        [SerializeField] private Quaternion[] nozzleRest;
        [Header("Estimated machinery rates")]
        [SerializeField, Min(1)] private float openingSeconds = 40;
        [SerializeField, Min(1)] private float traverseSeconds = 45;
        [SerializeField, Min(1)] private float anchorReleaseSeconds = 5;
        private ShipPhysicsController ship;
        private MechanismSave state = new MechanismSave();

        public bool IsConfigured => visual != null && hatchClip != null && anchorClip != null;
        public HatchPosition Target => (HatchPosition)state.hatchTarget;
        public float Opening => state.opening;
        public float Parking => state.parking;
        public float AnchorRelease => state.anchorRelease;
        public bool AnchorsReleased => state.anchorTarget;
        public bool IsMoving => Mathf.Abs(state.parking - DesiredParking) > .0001f ||
            Mathf.Abs(state.opening - (Target == HatchPosition.Closed ? 0 : 1)) > .0001f;
        private float DesiredParking => Target == HatchPosition.ForwardOpen ? -1 : Target == HatchPosition.AftOpen ? 1 : 0;

        public void Configure(GameObject model, AnimationClip hatches, AnimationClip anchors,
            Transform[] shafts, Transform[] ducts, Vector3[] shaftDirections, Vector3[] steeringDirections)
        {
            visual = model; hatchClip = hatches; anchorClip = anchors;
            propellers = shafts; nozzles = ducts;
            shaftAxes = shaftDirections; steeringAxes = steeringDirections;
            shaftRest = new[] { shafts[0].localRotation, shafts[1].localRotation };
            nozzleRest = new[] { ducts[0].localRotation, ducts[1].localRotation };
            state = new MechanismSave();
            ApplyPose(0);
        }

        private void Awake() => ship = GetComponent<ShipPhysicsController>();

        private void LateUpdate()
        {
            if (!IsConfigured || ship == null || ship.ManualStepping) return;
            Advance(Time.deltaTime, ship.EngineCount > 0 ? ship.ShaftRpm(0) : 0,
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
            // Return to the half-open stack before closing or crossing to the other hold.
            float parkingTarget = DesiredParking;
            if (state.opening < 1 || state.parking * parkingTarget < 0 || Target == HatchPosition.Closed)
                parkingTarget = 0;
            state.parking = Mathf.MoveTowards(state.parking, parkingTarget, seconds / traverseSeconds);
            if (Mathf.Abs(state.parking) < .0001f)
                state.opening = Mathf.MoveTowards(state.opening, Target == HatchPosition.Closed ? 0 : 1,
                    seconds / openingSeconds);
            state.anchorRelease = Mathf.MoveTowards(state.anchorRelease, state.anchorTarget ? 1 : 0,
                seconds / anchorReleaseSeconds);
            state.portAngle = Mathf.Repeat(state.portAngle + portRpm * 6 * seconds, 360);
            state.starboardAngle = Mathf.Repeat(state.starboardAngle + starboardRpm * 6 * seconds, 360);
            ApplyPose(rudderDegrees);
        }

        private void ApplyPose(float rudderDegrees)
        {
            if (!IsConfigured) return;
            float frame = state.parking < 0 ? Mathf.Lerp(210, 300, -state.parking) :
                state.parking > 0 ? Mathf.Lerp(390, 480, state.parking) : Mathf.Lerp(90, 210, state.opening);
            hatchClip.SampleAnimation(visual, (frame - 1) / 24f);
            anchorClip.SampleAnimation(visual, Mathf.Lerp(709, 739, state.anchorRelease) / 24f);
            for (int i = 0; i < 2; i++)
            {
                float angle = i == 0 ? state.portAngle : state.starboardAngle;
                propellers[i].localRotation = Quaternion.AngleAxis(angle, shaftAxes[i]) * shaftRest[i];
                nozzles[i].localRotation = Quaternion.AngleAxis(Mathf.Clamp(rudderDegrees, -35, 35), steeringAxes[i]) * nozzleRest[i];
            }
        }

        public MechanismSave CaptureState() => JsonUtility.FromJson<MechanismSave>(JsonUtility.ToJson(state));

        public void RestoreState(MechanismSave saved)
        {
            saved?.Validate();
            state = saved == null ? new MechanismSave() : JsonUtility.FromJson<MechanismSave>(JsonUtility.ToJson(saved));
            ApplyPose(ship != null ? ship.RudderAngleDeg : 0);
        }
    }
}
