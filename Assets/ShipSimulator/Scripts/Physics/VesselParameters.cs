using UnityEngine;

namespace ShipSimulator.Physics
{
    // Dimensional quantities for one loading condition, derived once from the vessel data.
    public sealed class VesselParameters
    {
        public const float Gravity = 9.81f;

        public VesselData Data { get; }
        public float LoadFraction { get; }
        public float Rho { get; }
        public float Lpp { get; }
        public float Loa { get; }
        public float Beam { get; }
        public float BeamOverall { get; }
        public float DepthMoulded { get; }
        public float LoadedDraft { get; }
        // Still-water draft at body z = 0; with section hydrostatics the ship may also trim about it.
        public float Draft { get; }
        // Bow-down positive: immersion grows by TrimSlope per metre forward. Zero for wall-sided prisms.
        public float TrimSlope { get; }
        public float TrimM => TrimSlope * Lpp;
        // Null for vessels on wall-sided prisms.
        public SectionHydrostatics Sections { get; }
        public HydrostaticRange HydrostaticRange { get; } = HydrostaticRange.Supported;
        public HydrostaticState Equilibrium { get; }
        public float BlockCoefficient { get; }
        public float Mass { get; }
        public float DisplacementVolume { get; }
        public float MidshipArea { get; }
        public float WettedSurface { get; }
        public float KeelLocalY { get; }
        public float CentreOfGravityX { get; }
        public Vector3 CentreOfMassLocalM { get; }
        public float PropellerImmersion { get; } = 1f;
        public float CentreOfGravityAboveKeel { get; }
        public float YawInertiaAtG { get; }
        public float RollInertia { get; }
        public float PitchInertia { get; }
        public float SurgeAddedMass { get; }
        public float SwayAddedMass { get; }
        public float YawAddedInertia { get; }
        public float HullYv { get; }
        public float HullYr { get; }
        public float HullNv { get; }
        public float HullNr { get; }
        public float WindFrontalArea { get; }
        public float WindLateralArea { get; }
        public float WindCentroidX { get; }
        public float WindCentroidHeight { get; }
        public float RatedRps { get; }
        public float DeliveredPowerPerShaftW { get; }

        private VesselParameters(VesselData data, float loadFraction, LoadingState loading = null,
            VesselParameters previous = null)
        {
            Data = data;
            LoadFraction = Mathf.Clamp01(loadFraction);
            Rho = data.hydrostatics.waterDensityKgM3;
            Lpp = data.dimensions.lengthBetweenPerpendicularsM;
            Loa = data.dimensions.lengthOverallM;
            Beam = data.dimensions.beamMouldedM;
            BeamOverall = data.dimensions.beamOverallM;
            DepthMoulded = data.dimensions.depthMouldedM;
            LoadedDraft = data.dimensions.loadedDraftM;
            BlockCoefficient = data.hydrostatics.blockCoefficient;

            VesselMassProperties mass = data.massProperties;
            Mass = Mathf.Lerp(mass.lightshipMassKg, mass.loadedMassKg, LoadFraction);
            if (loading != null) Mass = mass.lightshipMassKg + LoadingModel.Payload(loading);
            DisplacementVolume = Mass / Rho;
            KeelLocalY = data.hydrostatics.waterlineLocalY - LoadedDraft;
            CentreOfGravityX = mass.centerOfMassLocalM.z;
            CentreOfGravityAboveKeel = mass.centerOfMassLocalM.y - KeelLocalY;
            YawInertiaAtG = Mass * mass.yawGyrationRadiusM * mass.yawGyrationRadiusM;
            RollInertia = Mass * mass.rollGyrationRadiusM * mass.rollGyrationRadiusM;
            PitchInertia = Mass * mass.pitchGyrationRadiusM * mass.pitchGyrationRadiusM;
            CentreOfMassLocalM = mass.centerOfMassLocalM;
            if (loading != null)
            {
                LoadingModel.MassProperties(data, loading, out Vector3 centre, out Vector3 inertia);
                CentreOfMassLocalM = centre;
                CentreOfGravityX = centre.z;
                CentreOfGravityAboveKeel = centre.y - KeelLocalY;
                PitchInertia = inertia.x;
                YawInertiaAtG = inertia.y;
                RollInertia = inertia.z;
            }
            if (SectionHydrostatics.Supported(data))
            {
                // Mean draft and trim come from the hull's own sections, at this mass and centre of gravity.
                Sections = SectionHydrostatics.For(data);
                bool warm = previous != null && previous.Sections == Sections &&
                    previous.HydrostaticRange != HydrostaticRange.Unsolved;
                HydrostaticRange = Sections.Solve(DisplacementVolume, CentreOfGravityX, CentreOfGravityAboveKeel, Lpp,
                    out float draft, out float slope, out HydrostaticState state,
                    warm ? previous.Draft : float.NaN, warm ? previous.TrimSlope : 0f);
                Draft = draft;
                TrimSlope = slope;
                Equilibrium = state;
            }
            else
            {
                // Station hydrostatics are wall-sided prisms sized to float the loaded mass at the loaded
                // draft, so draft is proportional to mass.
                Draft = LoadedDraft * Mass / mass.loadedMassKg;
            }
            MidshipArea = data.hydrostatics.midshipCoefficient * Beam * Draft;
            if (loading != null)
            {
                float propellerZ = data.propeller.longitudinalPositionsM[0];
                PropellerImmersion = Mathf.Clamp01((DraftAt(propellerZ) - data.loading.propellerAxisHeightAboveKeelM +
                    .5f * data.propeller.diameterM) / data.propeller.diameterM);
            }

            float loadedWetted = data.resistance.wettedSurfaceM2 > 0f
                ? data.resistance.wettedSurfaceM2
                : ResistanceModel.MumfordWettedSurface(Lpp, LoadedDraft, mass.loadedMassKg / Rho);
            WettedSurface = loadedWetted * ResistanceModel.MumfordWettedSurface(Lpp, Draft, DisplacementVolume) /
                ResistanceModel.MumfordWettedSurface(Lpp, LoadedDraft, mass.loadedMassKg / Rho);

            // Loaded coefficients follow the Clarke draft dependence to other loading conditions.
            VesselHull hull = data.hull;
            HullDerivativeEstimate loaded = HullDerivativeEstimate.Clarke(Lpp, Beam, LoadedDraft, BlockCoefficient);
            HullDerivativeEstimate current = HullDerivativeEstimate.Clarke(Lpp, Beam, Draft, BlockCoefficient);
            HullYv = hull.yV * Ratio(current.Yv, loaded.Yv);
            HullYr = hull.yR * Ratio(current.Yr, loaded.Yr);
            HullNv = hull.nV * Ratio(current.Nv, loaded.Nv);
            HullNr = hull.nR * Ratio(current.Nr, loaded.Nr);
            float forceScale = 0.5f * Rho * Lpp * Lpp * Draft;
            SwayAddedMass = hull.swayAddedMassPrime * Ratio(current.SwayAddedMass, loaded.SwayAddedMass) * forceScale;
            YawAddedInertia = hull.yawAddedInertiaPrime * Ratio(current.YawAddedInertia, loaded.YawAddedInertia) *
                forceScale * Lpp * Lpp;
            SurgeAddedMass = hull.surgeAddedMassPrime * 0.5f * Rho * Lpp * Lpp * LoadedDraft *
                Mathf.Pow(Mass / mass.loadedMassKg, 5f / 3f);

            // A lighter ship exposes a freeboard strip of height (loaded draft - draft).
            VesselWindage wind = data.windage;
            float rise = Mathf.Max(0f, LoadedDraft - Draft);
            WindLateralArea = wind.lateralAreaM2 + Loa * rise;
            WindFrontalArea = wind.frontalAreaM2 + BeamOverall * rise;
            WindCentroidX = wind.lateralAreaM2 * wind.lateralCentroidLongitudinalM / WindLateralArea;
            WindCentroidHeight = (wind.lateralAreaM2 * (wind.lateralCentroidHeightM + rise) +
                Loa * rise * rise * 0.5f) / WindLateralArea;

            RatedRps = data.engine.ratedPropellerRpm / 60f;
            // Lift fans take their share before the propellers on a cushion craft.
            float liftShare = data.support != null ? Mathf.Clamp01(data.support.liftPowerFraction) : 0f;
            DeliveredPowerPerShaftW = data.engine.powerPerEngineW * data.engine.gearEfficiency *
                data.engine.shaftEfficiency * (1f - liftShare);
        }

        public float DraftAt(float localZ) => Draft + TrimSlope * localZ;

        public static VesselParameters Create(VesselData data, float loadFraction)
        {
            return !LoadingModel.Supported(data) ? new VesselParameters(data, loadFraction) :
                Create(data, LoadingModel.Initial(data, loadFraction));
        }

        public static VesselParameters Create(VesselData data)
        {
            return Create(data, data.massProperties.loadFraction);
        }

        // previous, when given, only seeds the floating-position solve; the result does not depend on it
        // beyond the solver tolerance.
        public static VesselParameters Create(VesselData data, LoadingState loading, VesselParameters previous = null)
        {
            LoadingModel.Validate(data, loading);
            return new VesselParameters(data, LoadingModel.Payload(loading) / LoadingModel.Budget(data), loading, previous);
        }

        private static float Ratio(float value, float reference)
        {
            return Mathf.Abs(reference) > 1e-9f ? value / reference : 1f;
        }
    }
}
