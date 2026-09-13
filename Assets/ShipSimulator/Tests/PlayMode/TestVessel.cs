using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Tests
{
    internal static class TestVessel
    {
        // Kinematic 10 m by 3 m vessel with valid data, for tests that place it by hand.
        public static GameObject Create(Vector3 position)
        {
            var shipObject = new GameObject("TestShip");
            shipObject.SetActive(false);
            shipObject.transform.position = position;

            Rigidbody body = shipObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            shipObject.AddComponent<BoxCollider>().size = Vector3.one;

            VesselDataLoader loader = shipObject.AddComponent<VesselDataLoader>();
            loader.Configure(new TextAsset(JsonUtility.ToJson(CreateData())));
            shipObject.AddComponent<ShipPhysicsController>();
            shipObject.SetActive(true);
            return shipObject;
        }

        public static VesselData CreateData()
        {
            float[] aheadThrust = { 0.33f, -0.25f, -0.12f };
            float[] aheadTorque = { 0.042f, -0.022f, -0.012f };
            float[] asternThrust = { 0.23f, -0.20f, -0.10f };
            float[] asternTorque = { 0.036f, -0.018f, -0.010f };
            return new VesselData
            {
                identity = new VesselIdentity { displayName = "Test vessel" },
                dimensions = new VesselDimensions
                {
                    lengthOverallM = 10f, lengthBetweenPerpendicularsM = 9.6f, beamOverallM = 3f,
                    beamMouldedM = 3f, depthMouldedM = 2f, loadedDraftM = 1f
                },
                massProperties = new VesselMassProperties
                {
                    lightshipMassKg = 4000f, loadedMassKg = 7000f, loadFraction = 1f,
                    centerOfMassLocalM = new Vector3(0f, -0.3f, 0f),
                    rollGyrationRadiusM = 1.1f, pitchGyrationRadiusM = 2.4f, yawGyrationRadiusM = 2.4f
                },
                hydrostatics = new VesselHydrostatics
                {
                    waterDensityKgM3 = 1000f, blockCoefficient = 0.75f, midshipCoefficient = 0.95f,
                    waterplaneCoefficient = 0.85f, stationCount = 8, stripsAcross = 2, heaveDampingRatio = 0.3f,
                    heaveAddedMassFraction = 0.5f, rollDampingRatio = 0.1f
                },
                resistance = new VesselResistance
                {
                    kinematicViscosityM2PerS = 1.14e-6f, formFactor = 1.2f, residualResistanceCoefficient = 0.002f
                },
                hull = new VesselHull
                {
                    surgeAddedMassPrime = 0.02f, swayAddedMassPrime = 0.2f, yawAddedInertiaPrime = 0.01f,
                    yV = -0.3f, yR = 0.05f, nV = -0.1f, nR = -0.05f, yVVV = -1.5f, maxYawRatePrime = 1.2f,
                    lowSpeedBlendStartMps = 0.1f, lowSpeedBlendEndMps = 0.5f, crossFlowStations = 8
                },
                engine = new VesselEngine
                {
                    engineCount = 1, powerPerEngineW = 20000f, gearEfficiency = 0.95f, shaftEfficiency = 0.98f,
                    ratedPropellerRpm = 900f, shaftInertiaKgM2 = 2f, governorBandFraction = 0.02f,
                    rpmRampSecondsFullRange = 3f, reversalRpmFraction = 0.25f, reversalDelaySeconds = 1f,
                    reversalBrakeTorqueFraction = 0.5f
                },
                propeller = new VesselPropeller
                {
                    count = 1, diameterM = 0.6f, longitudinalPositionsM = new[] { -4.5f }, lateralPositionsM = new[] { 0f },
                    aheadThrustCoefficients = aheadThrust, aheadTorqueCoefficients = aheadTorque,
                    asternThrustCoefficients = asternThrust, asternTorqueCoefficients = asternTorque,
                    wakeFraction = 0.2f, thrustDeduction = 0.2f, wakeDriftC1 = 2f, wakeDriftC2 = 1.3f
                },
                rudder = new VesselRudder
                {
                    count = 1, maxAngleDeg = 35f, rateDegPerSecond = 5f, areaPerRudderM2 = 0.4f, spanM = 0.8f,
                    longitudinalPositionM = -4.8f, lateralPositionsM = new[] { 0f }, stallAngleDeg = 32f,
                    postStallNormalCoefficient = 1.1f, steeringResistanceDeduction = 0.39f, hullInteractionFactor = 0.3f,
                    hullInteractionPositionPrime = -0.45f, wakeRatio = 1f, slipstreamFactor = 0.5f,
                    flowStraightening = 0.5f, flowStraighteningLeverPrime = -0.8f
                },
                windage = new VesselWindage
                {
                    airDensityKgM3 = 1.225f, frontalAreaM2 = 4f, lateralAreaM2 = 12f, transverseDragCoefficient = 0.85f,
                    longitudinalDragBow = 0.65f, longitudinalDragStern = 0.55f, crossForceParameter = 0.4f
                },
                restrictedWater = new VesselRestrictedWater
                {
                    bankSamplingWidthBeams = 4f, bankSuctionCoefficient = 0.5f, bankMomentLeverPrime = -0.25f
                },
                controlLimits = new VesselControlLimits
                {
                    maxLoadedSpeedMps = 3f, throttleCommandRatePerSecond = 1f, rudderCommandRatePerSecond = 1f
                },
                calibration = new VesselCalibration { thrustMultiplier = 1f, resistanceMultiplier = 1f, rudderMultiplier = 1f }
            };
        }
    }
}
