using System;
using UnityEngine;

namespace ShipSimulator.Physics
{
    [Serializable] public sealed class VesselIdentity { public string displayName; public string project; public string vesselType; }
    [Serializable] public sealed class VesselDimensions { public float lengthOverallM; public float lengthBetweenPerpendicularsM; public float beamOverallM; public float beamMouldedM; public float depthMouldedM; public float loadedDraftM; }
    // The body origin is midship on the loaded design waterline; +z is forward.
    [Serializable] public sealed class VesselMassProperties { public float loadedMassKg; public float deadweightKg; public float lightshipMassKg; public float loadFraction; public string loadingCondition; public Vector3 centerOfMassLocalM; public float rollGyrationRadiusM; public float pitchGyrationRadiusM; public float yawGyrationRadiusM; public bool estimated; }
    [Serializable] public sealed class VesselHydrostatics { public float waterDensityKgM3; public float waterlineLocalY; public float blockCoefficient; public float midshipCoefficient; public float waterplaneCoefficient; public float longitudinalCentreOfBuoyancyM; public int stationCount; public int stripsAcross; public float heaveDampingRatio; public float heaveAddedMassFraction; public float rollDampingRatio; public bool estimated; }
    [Serializable] public sealed class VesselResistance { public float kinematicViscosityM2PerS; public float wettedSurfaceM2; public float formFactor; public float correlationAllowance; public float residualResistanceCoefficient; public bool estimated; }
    // MMG hull coefficients, non-dimensional with 0.5 rho Lpp d U^2 (forces) and 0.5 rho Lpp^2 d U^2 (moments),
    // given at the loaded draft.
    [Serializable] public sealed class VesselHull { public float surgeAddedMassPrime; public float swayAddedMassPrime; public float yawAddedInertiaPrime; public float xVV; public float xVR; public float xRR; public float xVVVV; public float yV; public float yR; public float yVVV; public float yVVR; public float yVRR; public float yRRR; public float nV; public float nR; public float nVVV; public float nVVR; public float nVRR; public float nRRR; public float maxYawRatePrime; public float lowSpeedBlendStartMps; public float lowSpeedBlendEndMps; public int crossFlowStations; public bool estimated; public string derivation; }
    [Serializable] public sealed class VesselEngine { public int engineCount; public string engineType; public float powerPerEngineW; public float gearEfficiency; public float shaftEfficiency; public float ratedPropellerRpm; public float shaftInertiaKgM2; public float governorBandFraction; public float rpmRampSecondsFullRange; public float reversalRpmFraction; public float reversalDelaySeconds; public float reversalBrakeTorqueFraction; public bool estimatedDynamics; }
    // Open-water polynomials K = c0 + c1 J + c2 J^2; astern sets use J = -V_A / (|n| D).
    [Serializable] public sealed class VesselPropeller { public int count; public float diameterM; public float[] longitudinalPositionsM; public float[] lateralPositionsM; public float[] aheadThrustCoefficients; public float[] aheadTorqueCoefficients; public float[] asternThrustCoefficients; public float[] asternTorqueCoefficients; public float wakeFraction; public float thrustDeduction; public float wakeDriftC1; public float wakeDriftC2; public bool estimated; }
    [Serializable] public sealed class VesselRudder { public int count; public float maxAngleDeg; public float rateDegPerSecond; public float areaPerRudderM2; public float spanM; public float longitudinalPositionM; public float[] lateralPositionsM; public float normalForceSlope; public float stallAngleDeg; public float postStallNormalCoefficient; public float steeringResistanceDeduction; public float hullInteractionFactor; public float hullInteractionPositionPrime; public float wakeRatio; public float slipstreamFactor; public float flowStraightening; public float flowStraighteningLeverPrime; public bool estimated; }
    // Tunnel bow thruster; a missing section or fitted = false means none.
    [Serializable] public sealed class VesselBowThruster { public bool fitted; public float powerW; public float tunnelDiameterM; public float longitudinalPositionM; public float axisHeightAboveKeelM; public float figureOfMerit; public float rampSecondsToFull; public float speedLossReferenceMps; public float minimumSpeedEffectiveness; public bool estimated; }
    // Blendermann (1994) wind load parameters; areas and centroids at the loaded draft.
    [Serializable] public sealed class VesselWindage { public float airDensityKgM3; public float frontalAreaM2; public float lateralAreaM2; public float lateralCentroidLongitudinalM; public float lateralCentroidHeightM; public float transverseDragCoefficient; public float longitudinalDragBow; public float longitudinalDragStern; public float crossForceParameter; public bool estimated; }
    [Serializable] public sealed class VesselRestrictedWater { public bool shallowWaterCorrections; public float squatCoefficient; public float bankSamplingWidthBeams; public float bankSuctionCoefficient; public float bankMomentLeverPrime; public bool estimated; }
    [Serializable] public sealed class VesselControlLimits { public float maxLoadedSpeedMps; public float throttleCommandRatePerSecond; public float rudderCommandRatePerSecond; }
    [Serializable] public sealed class VesselCalibration { public float thrustMultiplier; public float resistanceMultiplier; public float rudderMultiplier; public bool trainingValidated; }

    [Serializable]
    public sealed class VesselData
    {
        public VesselIdentity identity;
        public VesselDimensions dimensions;
        public VesselMassProperties massProperties;
        public VesselHydrostatics hydrostatics;
        public VesselResistance resistance;
        public VesselHull hull;
        public VesselEngine engine;
        public VesselPropeller propeller;
        public VesselRudder rudder;
        public VesselBowThruster bowThruster;
        public VesselWindage windage;
        public VesselRestrictedWater restrictedWater;
        public VesselControlLimits controlLimits;
        public VesselCalibration calibration;
        public string[] sourceReferences;
    }
}
