using UnityEngine;

namespace ShipSimulator.Physics
{
    // Station-based buoyancy: the hull is a grid of vertical prisms from the keel to the deck, shaped in plan
    // by a parabolic end taper that matches the waterplane coefficient. Each prism is pushed up by the weight of
    // water it displaces at its own immersion, so draft, trim, heel and the metacentric height follow from the
    // geometry. Prisms are sized to float the loaded mass at the loaded draft (wall-sided, an estimate).
    public sealed class HydrostaticsModel
    {
        private readonly VesselParameters p;
        private readonly Vector3[] keelPoints;
        private readonly float[] areas;
        private readonly float[] damping;

        public float WaterplaneArea { get; }
        public float MetacentricHeightM { get; }
        public float RollDampingNmsPerRad { get; }
        public int CellCount => areas.Length;

        public HydrostaticsModel(VesselParameters parameters)
        {
            p = parameters;
            VesselHydrostatics h = parameters.Data.hydrostatics;
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
            float heaveStiffness = parameters.Rho * VesselParameters.Gravity * WaterplaneArea;
            float heaveDamping = 2f * h.heaveDampingRatio *
                Mathf.Sqrt(heaveStiffness * parameters.Mass * (1f + h.heaveAddedMassFraction));
            for (int i = 0; i < areas.Length; i++) damping[i] = heaveDamping * areas[i] / WaterplaneArea;
            float rollStiffness = parameters.Mass * VesselParameters.Gravity * Mathf.Max(MetacentricHeightM, 0.1f);
            RollDampingNmsPerRad = 2f * h.rollDampingRatio * Mathf.Sqrt(rollStiffness * parameters.RollInertia);
        }

        public Vector3 KeelPoint(int index) => keelPoints[index];
        public float Area(int index) => areas[index];

        public float SquatAt(float localZ, float bowSquatM, float sternSquatM)
        {
            return Mathf.Lerp(sternSquatM, bowSquatM, Mathf.Clamp01(localZ / p.Lpp + 0.5f));
        }

        // Squat lowers the effective water surface, which is the hydrostatic equivalent of the pressure drop
        // around a moving hull in shallow water: the ship sinks and trims instead of reporting a number.
        public void Apply(Rigidbody body, Transform frame, float waterLevel, float bowSquatM, float sternSquatM)
        {
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
    }
}
