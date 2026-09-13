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

    // Keel contact with the bottom: a penetration spring-damper pushes the hull up at each contact point,
    // and Coulomb friction limited by that normal force resists horizontal motion. Friction goes through
    // the manoeuvring solve so it acts against added mass. ShipPhysicsController steps it before its solve.
    public sealed class GroundingController : MonoBehaviour
    {
        [SerializeField] private ShipPhysicsController ship;
        [SerializeField] private ScenarioBathymetry bathymetry;
        [SerializeField] private float warningClearanceM = 1f;
        [SerializeField] private float hardContactSpeedMps = 1.2f;
        [Header("Bottom contact per point (estimated)")]
        [SerializeField] private float siltStiffnessNPerM = 2000000f;
        [SerializeField] private float sandStiffnessNPerM = 6000000f;
        [SerializeField] private float rockStiffnessNPerM = 30000000f;
        [SerializeField] private float siltFriction = 0.25f;
        [SerializeField] private float sandFriction = 0.45f;
        [SerializeField] private float rockFriction = 0.6f;
        [SerializeField] private float contactDampingRatio = 0.6f;

        private static readonly Vector2[] ContactLayout =
        {
            new Vector2(-0.15f, 0.46f), new Vector2(0.15f, 0.46f),
            new Vector2(-0.35f, 0.25f), new Vector2(0.35f, 0.25f),
            new Vector2(-0.35f, 0f), new Vector2(0.35f, 0f),
            new Vector2(-0.35f, -0.25f), new Vector2(0.35f, -0.25f),
            new Vector2(-0.3f, -0.46f), new Vector2(0.3f, -0.46f)
        };

        private GroundingState previousState;
        private Vector3[] contactPoints;

        public GroundingState State { get; private set; }
        public float MinimumClearanceM { get; private set; } = float.PositiveInfinity;
        public float DamagePoints { get; private set; }
        public float ContactNormalForceN { get; private set; }
        public RiverBottomType ContactBottomType { get; private set; }
        public ScenarioBathymetry Bathymetry => bathymetry;

        public void Configure(ShipPhysicsController targetShip, ScenarioBathymetry provider)
        {
            ship = targetShip;
            bathymetry = provider;
            contactPoints = null;
        }

        public void Step(float dt)
        {
            if (ship == null || ship.Body == null || ship.Parameters == null) return;
            if (contactPoints == null) BuildContactPoints();
            Rigidbody body = ship.Body;

            MinimumClearanceM = float.PositiveInfinity;
            ContactNormalForceN = 0f;
            float deepestPenetration = 0f;
            RiverBottomType worst = RiverBottomType.Sand;
            foreach (Vector3 local in contactPoints)
            {
                Vector3 world = ship.transform.TransformPoint(local);
                BathymetrySample bottom = bathymetry != null
                    ? bathymetry.Sample(world)
                    : new BathymetrySample(ship.SampleDepth(world), RiverBottomType.Sand);
                float clearance = bottom.DepthM - (ship.WaterLevel - world.y);
                if (clearance < MinimumClearanceM)
                {
                    MinimumClearanceM = clearance;
                    worst = bottom.BottomType;
                }
                if (clearance >= 0f) continue;

                float penetration = -clearance;
                deepestPenetration = Mathf.Max(deepestPenetration, penetration);
                Stiffness(bottom.BottomType, out float stiffness, out float friction);
                float damping = 2f * contactDampingRatio * Mathf.Sqrt(stiffness * body.mass / contactPoints.Length);
                Vector3 pointVelocity = body.GetPointVelocity(world);
                float normal = Mathf.Max(0f, stiffness * penetration - damping * pointVelocity.y);
                body.AddForceAtPosition(Vector3.up * normal, world, ForceMode.Force);
                ContactNormalForceN += normal;

                var sliding = new Vector3(pointVelocity.x, 0f, pointVelocity.z);
                // Regularised below 5 cm/s so the friction force does not chatter around zero speed.
                ship.AddExternalForce(-friction * normal * sliding / Mathf.Max(sliding.magnitude, 0.05f), world);
            }

            Vector3 horizontal = body.linearVelocity;
            horizontal.y = 0f;
            float speed = horizontal.magnitude;
            previousState = State;
            if (MinimumClearanceM > warningClearanceM)
                State = previousState == GroundingState.Touching ? GroundingState.Recovered : GroundingState.Clear;
            else if (MinimumClearanceM > 0f)
                State = GroundingState.Shallow;
            else
            {
                ContactBottomType = worst;
                bool hard = worst == RiverBottomType.Rock || speed >= hardContactSpeedMps || deepestPenetration > 0.35f;
                State = hard ? GroundingState.HardGrounding : GroundingState.Touching;
                DamagePoints += (hard ? 8f : 1.5f) * dt * Mathf.Max(0.25f, speed);
            }
        }

        private void BuildContactPoints()
        {
            VesselParameters p = ship.Parameters;
            contactPoints = new Vector3[ContactLayout.Length];
            for (int i = 0; i < ContactLayout.Length; i++)
                contactPoints[i] = new Vector3(ContactLayout[i].x * p.Beam, p.KeelLocalY, ContactLayout[i].y * p.Lpp);
        }

        private void Stiffness(RiverBottomType bottom, out float stiffness, out float friction)
        {
            switch (bottom)
            {
                case RiverBottomType.Silt:
                    stiffness = siltStiffnessNPerM;
                    friction = siltFriction;
                    break;
                case RiverBottomType.Rock:
                    stiffness = rockStiffnessNPerM;
                    friction = rockFriction;
                    break;
                default:
                    stiffness = sandStiffnessNPerM;
                    friction = sandFriction;
                    break;
            }
        }

        public ShipSimulator.Persistence.GroundingSave CaptureState() =>
            new ShipSimulator.Persistence.GroundingSave { state = (int)State, damage = DamagePoints };

        public void RestoreState(ShipSimulator.Persistence.GroundingSave save)
        {
            State = (GroundingState)save.state;
            previousState = State;
            DamagePoints = save.damage;
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
