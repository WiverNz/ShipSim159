using UnityEngine;

namespace ShipSimulator.Physics
{
    // Station-based buoyancy: the hull is a grid of vertical prisms from the keel to the deck, shaped in plan
    // by a parabolic end taper that matches the waterplane coefficient. Each prism is pushed up by the weight of
    // water it displaces at its own immersion, so draft, trim, heel and the metacentric height follow from the
    // geometry. Prisms are sized to float the loaded mass at the loaded draft (wall-sided, an estimate).
    // A vessel with a section table replaces the prisms with its tabulated hull columns instead.
    public sealed class HydrostaticsModel
    {
        private readonly VesselParameters p;
        private readonly SectionHydrostatics sections;
        private readonly Vector3[] keelPoints;
        private readonly float[] areas;
        private readonly float[] damping;
        private readonly float heaveDampingPerArea;

        public float WaterplaneArea { get; }
        // Solid-mass value: no free-surface correction is applied.
        public float MetacentricHeightM { get; }
        public float RollDampingNmsPerRad { get; }
        public int CellCount => sections != null ? sections.StationCount * sections.StripCount : areas.Length;

        public HydrostaticsModel(VesselParameters parameters)
        {
            p = parameters;
            VesselHydrostatics h = parameters.Data.hydrostatics;
            if (parameters.Sections != null)
            {
                sections = parameters.Sections;
                HydrostaticState state = parameters.Equilibrium;
                WaterplaneArea = Mathf.Max(state.WaterplaneAreaM2, 1f);
                MetacentricHeightM = state.KbM + state.TransverseInertiaM4 / Mathf.Max(state.VolumeM3, 1e-3f) -
                    parameters.CentreOfGravityAboveKeel;
                heaveDampingPerArea = HeaveDamping(parameters, h, WaterplaneArea) / WaterplaneArea;
                RollDampingNmsPerRad = RollDamping(parameters, h, MetacentricHeightM);
                return;
            }
            int stations = h.stationCount;
            int strips = h.stripsAcross;
            keelPoints = new Vector3[stations * strips];
            areas = new float[keelPoints.Length];
            damping = new float[keelPoints.Length];

            float taper = Mathf.Clamp(1.5f * (1f - h.waterplaneCoefficient), 0.001f, 0.5f);
            float stationLength = parameters.Lpp / stations;
            float total = 0f;
            for (int i = 0; i < stations; i++)
            {
                float position = (i + 0.5f) / stations - 0.5f;
                float s = Mathf.Max(0f, (Mathf.Abs(position) - (0.5f - taper)) / taper);
                float width = parameters.Beam * (1f - s * s);
                for (int j = 0; j < strips; j++)
                {
                    int index = i * strips + j;
                    keelPoints[index] = new Vector3((j + 0.5f) / strips * width - 0.5f * width,
                        parameters.KeelLocalY, position * parameters.Lpp);
                    areas[index] = width / strips * stationLength;
                    total += areas[index];
                }
            }
            // A linear weighting along the length moves the centre of buoyancy to its configured position
            // without changing the total area; the plan shape is symmetric, so its first moment is zero.
            float longitudinalMoment = 0f;
            for (int i = 0; i < areas.Length; i++) longitudinalMoment += areas[i] * keelPoints[i].z * keelPoints[i].z;
            float slope = h.longitudinalCentreOfBuoyancyM * total / longitudinalMoment;
            for (int i = 0; i < areas.Length; i++) areas[i] *= 1f + slope * keelPoints[i].z;
            float sizing = parameters.Data.massProperties.loadedMassKg / (parameters.Rho * parameters.LoadedDraft * total);
            float secondMoment = 0f;
            for (int i = 0; i < areas.Length; i++)
            {
                areas[i] *= sizing;
                secondMoment += areas[i] * keelPoints[i].x * keelPoints[i].x;
            }
            WaterplaneArea = total * sizing;

            MetacentricHeightM = 0.5f * parameters.Draft + secondMoment / (WaterplaneArea * parameters.Draft) -
                parameters.CentreOfGravityAboveKeel;
            float heaveDamping = HeaveDamping(parameters, h, WaterplaneArea);
            for (int i = 0; i < areas.Length; i++) damping[i] = heaveDamping * areas[i] / WaterplaneArea;
            RollDampingNmsPerRad = RollDamping(parameters, h, MetacentricHeightM);
        }

        private static float HeaveDamping(VesselParameters parameters, VesselHydrostatics h, float waterplaneArea)
        {
            float heaveStiffness = parameters.Rho * VesselParameters.Gravity * waterplaneArea;
            return 2f * h.heaveDampingRatio * Mathf.Sqrt(heaveStiffness * parameters.Mass * (1f + h.heaveAddedMassFraction));
        }

        private static float RollDamping(VesselParameters parameters, VesselHydrostatics h, float metacentricHeight)
        {
            float rollStiffness = parameters.Mass * VesselParameters.Gravity * Mathf.Max(metacentricHeight, 0.1f);
            return 2f * h.rollDampingRatio * Mathf.Sqrt(rollStiffness * parameters.RollInertia);
        }

        public Vector3 KeelPoint(int index)
        {
            if (sections == null) return keelPoints[index];
            int station = index / sections.StripCount;
            return new Vector3(sections.StripX(index % sections.StripCount), p.KeelLocalY, sections.StationZ(station));
        }

        // Waterplane area of a cell in the still-water floating position.
        public float Area(int index)
        {
            if (sections == null) return areas[index];
            int station = index / sections.StripCount;
            sections.Lookup(station, index % sections.StripCount, p.DraftAt(sections.StationZ(station)), out _, out _,
                out float breadth);
            return sections.Weight(station) * breadth;
        }

        public float SquatAt(float localZ, float bowSquatM, float sternSquatM)
        {
            return Mathf.Lerp(sternSquatM, bowSquatM, Mathf.Clamp01(localZ / p.Lpp + 0.5f));
        }

        // Squat lowers the effective water surface, which is the hydrostatic equivalent of the pressure drop
        // around a moving hull in shallow water: the ship sinks and trims instead of reporting a number.
        public void Apply(Rigidbody body, Transform frame, float waterLevel, float bowSquatM, float sternSquatM)
        {
            if (sections != null)
            {
                ApplySections(body, frame, waterLevel, bowSquatM, sternSquatM);
                return;
            }
            float weightDensity = p.Rho * VesselParameters.Gravity;
            Vector3 up = frame.up;
            for (int i = 0; i < areas.Length; i++)
            {
                Vector3 keel = frame.TransformPoint(keelPoints[i]);
                float surface = waterLevel - SquatAt(keelPoints[i].z, bowSquatM, sternSquatM);
                float immersion = Mathf.Clamp(surface - keel.y, 0f, p.DepthMoulded);
                if (immersion <= 0f) continue;
                Vector3 centroid = keel + up * (0.5f * immersion);
                float force = weightDensity * areas[i] * immersion - damping[i] * body.GetPointVelocity(centroid).y;
                body.AddForceAtPosition(Vector3.up * Mathf.Max(0f, force), centroid, ForceMode.Force);
            }
            float rollRate = Vector3.Dot(body.angularVelocity, frame.forward);
            body.AddTorque(-frame.forward * (RollDampingNmsPerRad * rollRate), ForceMode.Force);
        }

        // Columns are vertical in the body frame and immersed to the local water surface. Their forces are
        // summed here and applied once, which is the same resultant as one force per column at a fraction of
        // the engine calls. Immersion above the cap saturates; a dry or non-finite immersion adds nothing.
        private void ApplySections(Rigidbody body, Transform frame, float waterLevel, float bowSquatM, float sternSquatM)
        {
            float weightDensity = p.Rho * VesselParameters.Gravity;
            Vector3 origin = frame.position, right = frame.right, up = frame.up, forward = frame.forward;
            Vector3 centre = body.worldCenterOfMass, velocity = body.linearVelocity, spin = body.angularVelocity;
            float lift = 0f, torqueX = 0f, torqueZ = 0f;
            // Scalar arithmetic: this loop visits every column each physics step.
            float reach = Mathf.Abs(right.y) * sections.Table.stripHalfWidthM;
            for (int i = 0; i < sections.StationCount; i++)
            {
                float z = sections.StationZ(i);
                float surface = waterLevel - SquatAt(z, bowSquatM, sternSquatM);
                float baseX = origin.x + forward.x * z + up.x * p.KeelLocalY - centre.x;
                float baseY = origin.y + forward.y * z + up.y * p.KeelLocalY;
                float baseZ = origin.z + forward.z * z + up.z * p.KeelLocalY - centre.z;
                if (surface <= baseY - reach) continue;
                float weight = sections.Weight(i);
                for (int j = 0; j < sections.StripCount; j++)
                {
                    float x = sections.StripX(j);
                    float immersion = surface - (baseY + right.y * x);
                    if (!(immersion > 0f)) continue;
                    sections.Lookup(i, j, immersion, out float area, out float moment, out float breadth);
                    if (area <= 0f) continue;
                    float rise = moment / area;
                    float rx = baseX + right.x * x + up.x * rise;
                    float rz = baseZ + right.z * x + up.z * rise;
                    float verticalVelocity = velocity.y + spin.z * rx - spin.x * rz;
                    float force = weight * (weightDensity * area - heaveDampingPerArea * breadth * verticalVelocity);
                    if (force <= 0f) continue;
                    lift += force;
                    torqueX -= rz * force;
                    torqueZ += rx * force;
                }
            }
            body.AddForce(new Vector3(0f, lift, 0f), ForceMode.Force);
            body.AddTorque(new Vector3(torqueX, 0f, torqueZ), ForceMode.Force);
            float rollRate = Vector3.Dot(spin, forward);
            body.AddTorque(-forward * (RollDampingNmsPerRad * rollRate), ForceMode.Force);
        }
    }
}
