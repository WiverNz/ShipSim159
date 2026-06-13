using UnityEngine;

namespace ShipSimulator.Physics
{
    public enum GroundingState
    {
        Clear,
        Shallow,
        Touching,
        HardGrounding,
        Recovered
    }

    public sealed class GroundingController : MonoBehaviour
    {
        [SerializeField] private ShipPhysicsController ship;
        [SerializeField] private ScenarioBathymetry bathymetry;
        [SerializeField] private float warningClearanceM = 1f;
        [SerializeField] private float hardContactSpeedMps = 1.2f;
        [SerializeField] private float softDragNPerMps = 900000f;
        [SerializeField] private float rockDragNPerMps = 2400000f;

        private GroundingState previousState;

        public GroundingState State { get; private set; }
        public float MinimumClearanceM { get; private set; } = float.PositiveInfinity;
        public float DamagePoints { get; private set; }
        public RiverBottomType ContactBottomType { get; private set; }

        public void Configure(ShipPhysicsController targetShip, ScenarioBathymetry provider)
        {
            ship = targetShip;
            bathymetry = provider;
        }

        private void FixedUpdate()
        {
            if (ship == null || ship.Body == null || bathymetry == null) return;
            float halfLength = ship.Data != null
                ? ship.Data.dimensions.lengthOverallM * 0.42f
                : 45f;
            Vector3[] localSamples =
            {
                new Vector3(0f, 0f, halfLength),
                Vector3.zero,
                new Vector3(0f, 0f, -halfLength)
            };

            MinimumClearanceM = float.PositiveInfinity;
            BathymetrySample worst = default;
            foreach (Vector3 local in localSamples)
            {
                BathymetrySample sample = bathymetry.Sample(
                    ship.transform.TransformPoint(local));
                float clearance = sample.DepthM - ship.EffectiveDraftM;
                if (clearance >= MinimumClearanceM) continue;
                MinimumClearanceM = clearance;
                worst = sample;
            }

            float speed = ship.Body.linearVelocity.magnitude;
            previousState = State;
            if (MinimumClearanceM > warningClearanceM)
                State = previousState == GroundingState.Touching
                    ? GroundingState.Recovered
                    : GroundingState.Clear;
            else if (MinimumClearanceM > 0f)
                State = GroundingState.Shallow;
            else
            {
                ContactBottomType = worst.BottomType;
                bool hard = worst.BottomType == RiverBottomType.Rock ||
                    speed >= hardContactSpeedMps ||
                    MinimumClearanceM < -0.35f;
                State = hard ? GroundingState.HardGrounding : GroundingState.Touching;
                float drag = worst.BottomType == RiverBottomType.Rock
                    ? rockDragNPerMps
                    : softDragNPerMps;
                ship.Body.AddForce(-ship.Body.linearVelocity * drag, ForceMode.Force);
                DamagePoints += (hard ? 8f : 1.5f) * Time.fixedDeltaTime *
                    Mathf.Max(0.25f, speed);
            }
        }

        public void ResetState()
        {
            State = GroundingState.Clear;
            previousState = GroundingState.Clear;
            MinimumClearanceM = float.PositiveInfinity;
            DamagePoints = 0f;
        }
    }
}
