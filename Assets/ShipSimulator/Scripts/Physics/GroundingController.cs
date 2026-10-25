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

    // Hull contact with the bottom. Penetration of each contact point into the bed produces a normal reaction
    // along the local hull surface normal, and Coulomb friction limited by that reaction resists sliding.
    // Normal reactions act on the Rigidbody directly. Friction is solved after the manoeuvring step as an
    // impulse that can stop the contact points but never drive them backwards, so a ship held below the
    // friction limit stays put instead of creeping, and friction never adds energy.
    // A vessel with a section table touches with its real bottom surface and propeller tip circles; others use
    // a fixed keel-level layout. All soil values are estimates for prototype play, not geotechnical data.
    public sealed class GroundingController : MonoBehaviour
    {
        private const int FrictionIterations = 12;

        [SerializeField] private ShipPhysicsController ship;
        [SerializeField] private ScenarioBathymetry bathymetry;
        [SerializeField] private float warningClearanceM = 1f;
        [SerializeField] private float hardContactSpeedMps = 1.2f;
        [Header("Keel-level layout per point (estimated)")]
        [SerializeField] private float siltStiffnessNPerM = 2000000f;
        [SerializeField] private float sandStiffnessNPerM = 6000000f;
        [SerializeField] private float rockStiffnessNPerM = 30000000f;
        [SerializeField] private float siltFriction = 0.25f;
        [SerializeField] private float sandFriction = 0.45f;
        [SerializeField] private float rockFriction = 0.6f;
        [SerializeField] private float contactDampingRatio = 0.6f;
        [Header("Section-surface contact per unit area (estimated)")]
        // Ship-sized footing moduli: plate values reduced for a footing tens of metres wide.
        [SerializeField] private float siltModulusNPerM3 = 500000f;
        [SerializeField] private float sandModulusNPerM3 = 1500000f;
        [SerializeField] private float rockModulusNPerM3 = 20000000f;
        // Bearing pressure at which the bed yields and the hull sinks into it instead of being lifted.
        [SerializeField] private float siltBearingPa = 30000f;
        [SerializeField] private float sandBearingPa = 150000f;
        [SerializeField] private float rockBearingPa = 5000000f;
        // Static friction of steel on the bed, middle of the U.S. Navy Salvor's Handbook ranges.
        [SerializeField] private float siltSectionFriction = 0.25f;
        [SerializeField] private float sandSectionFriction = 0.35f;
        [SerializeField] private float rockSectionFriction = 1.0f;
        [SerializeField] private float sectionStationSpacingM = 4.5f;
        [SerializeField] private float sectionPointSpacingM = 2f;

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
        // Local hull surface normal pointing out of the bed into the hull, and the plan area each point stands for;
        // zero area marks the keel-level layout.
        private Vector3[] contactNormals;
        private float[] contactAreas;
        private float[] frictionLimits;
        private Vector3[] frictionForces;
        private Vector3[] worldPoints;
        private Vector4[] responses;
        private int activeContacts;
        private int[] active;

        public GroundingState State { get; private set; }
        public float MinimumClearanceM { get; private set; } = float.PositiveInfinity;
        public float DamagePoints { get; private set; }
        public float ContactNormalForceN { get; private set; }
        // What the bottom can hold the vessel with: compare it against the astern thrust to see whether
        // working off is possible at all.
        public float HoldingForceN { get; private set; }
        public float FrictionForceN { get; private set; }
        public int ContactPointCount => contactPoints?.Length ?? 0;
        public bool UsesHullSections { get; private set; }
        // Power of the last step's friction forces at the body's present velocity. Read after the physics
        // step, a positive value means friction overshot and drove the hull the other way.
        public float FrictionPowerW
        {
            get
            {
                if (frictionForces == null || ship == null || ship.Body == null) return 0f;
                float power = 0f;
                for (int k = 0; k < activeContacts; k++)
                {
                    int i = active[k];
                    Vector3 velocity = ship.Body.GetPointVelocity((ship.Body.position + ship.Body.rotation * contactPoints[i]));
                    velocity.y = 0f;
                    power += Vector3.Dot(frictionForces[i], velocity);
                }
                return power;
            }
        }
        public RiverBottomType ContactBottomType { get; private set; }
        public ScenarioBathymetry Bathymetry => bathymetry;

        private void OnEnable() => BathymetryContactSurface.Apply(this);
        private void OnDisable() => BathymetryContactSurface.Apply(this);

        public void Configure(ShipPhysicsController targetShip, ScenarioBathymetry provider)
        {
            ship = targetShip;
            bathymetry = provider;
            contactPoints = null;
            BathymetryContactSurface.Apply(this);
        }

        public Vector3 ContactPoint(int index) => contactPoints[index];

        // Normal reactions; friction limits are kept for ResolveFriction after the manoeuvring step.
        public void Step(float dt)
        {
            if (ship == null || ship.Body == null || ship.Parameters == null) return;
            if (contactPoints == null) BuildContactPoints();
            Rigidbody body = ship.Body;

            MinimumClearanceM = float.PositiveInfinity;
            ContactNormalForceN = 0f;
            HoldingForceN = 0f;
            activeContacts = 0;
            float deepestPenetration = 0f;
            RiverBottomType worst = RiverBottomType.Sand;
            float pointMass = body.mass / contactPoints.Length;
            for (int i = 0; i < contactPoints.Length; i++)
            {
                frictionForces[i] = Vector3.zero;
                Vector3 world = (ship.Body.position + ship.Body.rotation * contactPoints[i]);
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
                Vector3 pointVelocity = body.GetPointVelocity(world);
                float normal;
                float friction;
                Vector3 direction;
                if (contactAreas[i] > 0f)
                {
                    SectionSoil(bottom.BottomType, out float modulus, out float bearing, out friction);
                    direction = (body.rotation * contactNormals[i]);
                    // Explicit penalty contact: the point stiffness is limited to what the step can integrate.
                    float stiffness = Mathf.Min(modulus * contactAreas[i], 0.5f * pointMass / (dt * dt));
                    float damping = 2f * contactDampingRatio * Mathf.Sqrt(stiffness * pointMass);
                    normal = Mathf.Clamp(stiffness * penetration - damping * Vector3.Dot(pointVelocity, direction),
                        0f, bearing * contactAreas[i]);
                }
                else
                {
                    Stiffness(bottom.BottomType, out float stiffness, out friction);
                    direction = Vector3.up;
                    float damping = 2f * contactDampingRatio * Mathf.Sqrt(stiffness * pointMass);
                    // The bottom holds a vessel up; it does not throw her. Without this cap the penalty
                    // spring answers a metre of penetration with tens of meganewtons and launches the hull
                    // clear of the water, which is how a hydrofoil driven onto a bar ended up flying.
                    float maxNormal = 3f * body.mass * 9.81f / contactPoints.Length;
                    normal = Mathf.Min(maxNormal, Mathf.Max(0f, stiffness * penetration - damping * pointVelocity.y));
                }
                if (normal <= 0f) continue;

                // The vertical share lifts the hull; a raked or flared surface also pushes it out sideways or
                // back, and that share goes through the manoeuvring solve with the other horizontal forces.
                Vector3 force = direction * normal;
                body.AddForceAtPosition(Vector3.up * force.y, world, ForceMode.Force);
                force.y = 0f;
                if (force.sqrMagnitude > 0f) ship.AddExternalForce(force, world);
                ContactNormalForceN += normal;
                HoldingForceN += friction * normal * direction.y;
                frictionLimits[i] = friction * normal * direction.y;
                worldPoints[i] = world;
                active[activeContacts++] = i;
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

        // Coulomb friction as an impulse over the step: each contact point receives the horizontal force that
        // would stop it, clamped to its limit, iterated because the points share one rigid hull. Adds the
        // resulting accelerations to the manoeuvring output before the controller applies it.
        public void ResolveFriction(ManoeuvringModel model, ref ManoeuvringOutput output, float dt)
        {
            FrictionForceN = 0f;
            if (activeContacts == 0 || model == null || dt <= 0f) return;
            Rigidbody body = ship.Body;
            Vector3 forward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 midship = body.position;
            Vector3 centre = body.worldCenterOfMass;

            // Velocity at the end of the step without friction.
            Vector3 linear = body.linearVelocity + dt * (forward * output.GravityCentreAccelerationX +
                right * output.GravityCentreAccelerationY);
            linear.y = 0f;
            float yawRate = body.angularVelocity.y + dt * output.YawAcceleration;

            // Velocity change at each point from a unit force there, along each horizontal axis.
            for (int k = 0; k < activeContacts; k++)
            {
                int i = active[k];
                Vector3 forwardResponse = Response(model, midship, centre, forward, right, worldPoints[i], forward, dt);
                Vector3 rightResponse = Response(model, midship, centre, forward, right, worldPoints[i], right, dt);
                responses[i] = new Vector4(Vector3.Dot(forwardResponse, forward), Vector3.Dot(rightResponse, forward),
                    Vector3.Dot(forwardResponse, right), Vector3.Dot(rightResponse, right));
            }

            float frictionX = 0f, frictionY = 0f, frictionN = 0f;
            for (int iteration = 0; iteration < FrictionIterations; iteration++)
            {
                for (int k = 0; k < activeContacts; k++)
                {
                    int i = active[k];
                    float limit = frictionLimits[i];
                    Vector4 r = responses[i];
                    float determinant = r.x * r.w - r.y * r.z;
                    if (limit <= 0f || Mathf.Abs(determinant) < 1e-20f) continue;
                    Vector3 velocity = linear + Vector3.Cross(Vector3.up * yawRate, worldPoints[i] - centre);
                    float vf = Vector3.Dot(velocity, forward), vr = Vector3.Dot(velocity, right);
                    // Force that would bring this point to rest, added to what it already carries.
                    float stopForward = -(r.w * vf - r.y * vr) / determinant;
                    float stopRight = -(-r.z * vf + r.x * vr) / determinant;
                    Vector3 previous = frictionForces[i];
                    Vector3 wanted = previous + forward * stopForward + right * stopRight;
                    if (wanted.sqrMagnitude > limit * limit) wanted = wanted.normalized * limit;
                    frictionForces[i] = wanted;
                    Moment(midship, forward, right, worldPoints[i], wanted - previous, out float x, out float y, out float n);
                    model.ExternalAcceleration(x, y, n, out float ax, out float ay, out float an);
                    linear += dt * (forward * ax + right * ay);
                    yawRate += dt * an;
                    frictionX += x;
                    frictionY += y;
                    frictionN += n;
                }
            }

            model.ExternalAcceleration(frictionX, frictionY, frictionN, out float gx, out float gy, out float yaw);
            output.GravityCentreAccelerationX += gx;
            output.GravityCentreAccelerationY += gy;
            output.YawAcceleration += yaw;
            output.TotalX += frictionX;
            output.TotalY += frictionY;
            output.TotalN += frictionN;
            for (int k = 0; k < activeContacts; k++) FrictionForceN += frictionForces[active[k]].magnitude;
        }

        private static Vector3 Response(ManoeuvringModel model, Vector3 midship, Vector3 centre, Vector3 forward,
            Vector3 right, Vector3 point, Vector3 axis, float dt)
        {
            Moment(midship, forward, right, point, axis, out float x, out float y, out float n);
            model.ExternalAcceleration(x, y, n, out float gx, out float gy, out float yaw);
            Vector3 change = dt * (forward * gx + right * gy + Vector3.Cross(Vector3.up * yaw, point - centre));
            change.y = 0f;
            return change;
        }

        // Same convention as ShipPhysicsController.AddExternalForce: moment about midship, bow to starboard positive.
        private static void Moment(Vector3 midship, Vector3 forward, Vector3 right, Vector3 point, Vector3 force,
            out float x, out float y, out float n)
        {
            x = Vector3.Dot(force, forward);
            y = Vector3.Dot(force, right);
            Vector3 offset = point - midship;
            n = Vector3.Dot(offset, forward) * y - Vector3.Dot(offset, right) * x;
        }

        private void BuildContactPoints()
        {
            VesselParameters p = ship.Parameters;
            var points = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var areas = new System.Collections.Generic.List<float>();
            UsesHullSections = p.Sections != null;
            if (UsesHullSections) AddSectionContacts(p, points, normals, areas);
            else
                foreach (Vector2 layout in ContactLayout)
                {
                    points.Add(new Vector3(layout.x * p.Beam, p.KeelLocalY, layout.y * p.Lpp));
                    normals.Add(Vector3.up);
                    areas.Add(0f);
                }
            if (SupportModel.Lifts(p.Data.support))
                for (int i = 0; i < 4; i++)
                {
                    points.Add(SupportModel.ContactPoint(p, i));
                    normals.Add(Vector3.up);
                    areas.Add(0f);
                }
            contactPoints = points.ToArray();
            contactNormals = normals.ToArray();
            contactAreas = areas.ToArray();
            frictionForces = new Vector3[contactPoints.Length];
            frictionLimits = new float[contactPoints.Length];
            worldPoints = new Vector3[contactPoints.Length];
            responses = new Vector4[contactPoints.Length];
            active = new int[contactPoints.Length];
            activeContacts = 0;
        }

        // A grid over the bottom surface below the selected waterline, at the heights the section table gives,
        // plus the lowest point of each propeller's tip circle, which hangs below the cut-up stern.
        private void AddSectionContacts(VesselParameters p, System.Collections.Generic.List<Vector3> points,
            System.Collections.Generic.List<Vector3> normals, System.Collections.Generic.List<float> areas)
        {
            SectionHydrostatics sections = p.Sections;
            float aft = sections.StationZ(0), fore = sections.StationZ(sections.StationCount - 1);
            int stations = Mathf.Max(2, Mathf.CeilToInt((fore - aft) / sectionStationSpacingM));
            float stationLength = (fore - aft) / stations;
            int across = Mathf.Max(1, Mathf.CeilToInt(sections.Table.stripHalfWidthM / sectionPointSpacingM));
            float width = sections.Table.stripHalfWidthM / across;
            const float delta = 0.25f;
            for (int i = 0; i < stations; i++)
            {
                float z = aft + (i + 0.5f) * stationLength;
                for (int j = 0; j < across; j++)
                {
                    float offset = (j + 0.5f) * width;
                    float h = sections.BottomHeightAt(z, offset);
                    if (float.IsNaN(h) || h > p.LoadedDraft) continue;
                    float hz = Slope(sections, z, offset, delta, true);
                    float hx = Slope(sections, z, offset, delta, false);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        points.Add(new Vector3(side * offset, p.KeelLocalY + h, z));
                        normals.Add(new Vector3(-side * hx, 1f, -hz).normalized);
                        areas.Add(stationLength * width);
                    }
                }
            }
            VesselPropeller propeller = p.Data.propeller;
            float axis = p.Data.loading != null && p.Data.loading.enabled ? p.Data.loading.propellerAxisHeightAboveKeelM : 0.5f * propeller.diameterM;
            for (int i = 0; i < propeller.count; i++)
            {
                points.Add(new Vector3(propeller.lateralPositionsM[i], p.KeelLocalY + axis - 0.5f * propeller.diameterM,
                    propeller.longitudinalPositionsM[i]));
                normals.Add(Vector3.up);
                areas.Add(propeller.diameterM * propeller.diameterM);
            }
        }

        private static float Slope(SectionHydrostatics sections, float z, float offset, float delta, bool longitudinal)
        {
            float centre = sections.BottomHeightAt(z, offset);
            float plus = longitudinal ? sections.BottomHeightAt(z + delta, offset) : sections.BottomHeightAt(z, offset + delta);
            float minus = longitudinal ? sections.BottomHeightAt(z - delta, offset) : sections.BottomHeightAt(z, Mathf.Max(0f, offset - delta));
            if (float.IsNaN(plus)) plus = centre;
            if (float.IsNaN(minus)) minus = centre;
            float span = longitudinal || offset >= delta ? 2f * delta : delta + offset;
            // Steeper than this is the stem or transom face; the normal is then almost horizontal anyway.
            return Mathf.Clamp((plus - minus) / span, -5f, 5f);
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

        private void SectionSoil(RiverBottomType bottom, out float modulus, out float bearing, out float friction)
        {
            switch (bottom)
            {
                case RiverBottomType.Silt:
                    modulus = siltModulusNPerM3;
                    bearing = siltBearingPa;
                    friction = siltSectionFriction;
                    break;
                case RiverBottomType.Rock:
                    modulus = rockModulusNPerM3;
                    bearing = rockBearingPa;
                    friction = rockSectionFriction;
                    break;
                default:
                    modulus = sandModulusNPerM3;
                    bearing = sandBearingPa;
                    friction = sandSectionFriction;
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
            ContactNormalForceN = 0f;
            HoldingForceN = 0f;
            FrictionForceN = 0f;
            activeContacts = 0;
            DamagePoints = 0f;
        }
    }
}
