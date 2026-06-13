using System;
using UnityEngine;

namespace ShipSimulator.Physics
{
    [Serializable] public sealed class VesselIdentity { public string displayName; public string project; public string vesselType; }
    [Serializable] public sealed class VesselDimensions { public float lengthOverallM; public float lengthBetweenPerpendicularsM; public float beamOverallM; public float beamMouldedM; public float depthMouldedM; public float loadedDraftM; }
    [Serializable] public sealed class VesselMassProperties { public float loadedMassKg; public float deadweightKg; public float lightshipMassKg; public float loadFraction; public string loadingCondition; public Vector3 centerOfMassLocalM; public Vector3 inertiaTensorKgM2; public bool estimated; }
    [Serializable] public sealed class VesselEngine { public int engineCount; public string engineType; public float powerPerEngineW; public float aheadResponseSeconds; public float asternResponseSeconds; public float gearEfficiency; public bool estimatedDynamics; }
    [Serializable] public sealed class VesselPropeller { public int count; public float diameterM; public float maxAheadThrustN; public float maxAsternThrustN; public float[] longitudinalPositionsM; public float[] lateralPositionsM; public bool estimated; }
    [Serializable] public sealed class VesselRudder { public int count; public float maxAngleDeg; public float rateDegPerSecond; public float areaPerRudderM2; public float liftCoefficientSlopePerRad; public float longitudinalPositionM; public bool estimated; }
    [Serializable] public sealed class VesselHydrodynamics { public float waterDensityKgM3; public float blockCoefficient; public float surgeLinearNPerMps; public float surgeQuadraticNPerMps2; public float swayLinearNPerMps; public float swayQuadraticNPerMps2; public float yawLinearNmPerRadps; public float yawQuadraticNmPerRadps2; public bool estimatedResistance; }
    [Serializable] public sealed class VesselBuoyancy { public float waterlineLocalY; public int pointCount; public float maxPointDepthM; public float reserveBuoyancyFactor; public float verticalDampingNPerMpsPerPoint; public bool estimated; }
    [Serializable] public sealed class VesselControlLimits { public float maxLoadedSpeedMps; public float throttleCommandRatePerSecond; public float rudderCommandRatePerSecond; }
    [Serializable] public sealed class VesselCalibration { public float thrustMultiplier; public float resistanceMultiplier; public float rudderMultiplier; public float buoyancyMultiplier; public bool trainingValidated; }

    [Serializable]
    public sealed class VesselData
    {
        public VesselIdentity identity;
        public VesselDimensions dimensions;
        public VesselMassProperties massProperties;
        public VesselEngine engine;
        public VesselPropeller propeller;
        public VesselRudder rudder;
        public VesselHydrodynamics hydrodynamics;
        public VesselBuoyancy buoyancy;
        public VesselControlLimits controlLimits;
        public VesselCalibration calibration;
        public string[] sourceReferences;
    }
}
