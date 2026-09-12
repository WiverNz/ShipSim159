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
            loader.Configure(new TextAsset(CreateValidJson()));
            shipObject.AddComponent<HydrodynamicResistance>();

            GameObject propulsion = new GameObject("Propulsion");
            propulsion.transform.SetParent(shipObject.transform);
            propulsion.AddComponent<PropulsionController>();

            GameObject rudder = new GameObject("Rudder");
            rudder.transform.SetParent(shipObject.transform);
            rudder.AddComponent<RudderController>();

            GameObject buoyancy = new GameObject("Buoyancy");
            buoyancy.transform.SetParent(shipObject.transform);
            buoyancy.AddComponent<BuoyancyPoint>();

            shipObject.AddComponent<ShipPhysicsController>();
            shipObject.SetActive(true);
            return shipObject;
        }

        private static string CreateValidJson()
        {
            var data = new VesselData
            {
                identity = new VesselIdentity { displayName = "Test vessel" },
                dimensions = new VesselDimensions
                {
                    lengthOverallM = 10f,
                    beamOverallM = 3f,
                    depthMouldedM = 2f,
                    loadedDraftM = 1f
                },
                massProperties = new VesselMassProperties
                {
                    lightshipMassKg = 1000f,
                    loadedMassKg = 2000f,
                    loadFraction = 0.5f,
                    inertiaTensorKgM2 = Vector3.one * 100f
                },
                engine = new VesselEngine
                {
                    engineCount = 1,
                    powerPerEngineW = 1000f,
                    aheadResponseSeconds = 1f,
                    asternResponseSeconds = 1f
                },
                propeller = new VesselPropeller
                {
                    count = 1,
                    maxAheadThrustN = 100f,
                    maxAsternThrustN = 50f,
                    longitudinalPositionsM = new[] { -1f },
                    lateralPositionsM = new[] { 0f }
                },
                rudder = new VesselRudder
                {
                    count = 1,
                    maxAngleDeg = 30f,
                    rateDegPerSecond = 5f,
                    areaPerRudderM2 = 1f
                },
                hydrodynamics = new VesselHydrodynamics
                {
                    waterDensityKgM3 = 1000f
                },
                buoyancy = new VesselBuoyancy
                {
                    pointCount = 1,
                    maxPointDepthM = 1f,
                    reserveBuoyancyFactor = 1f
                },
                controlLimits = new VesselControlLimits
                {
                    maxLoadedSpeedMps = 5f,
                    throttleCommandRatePerSecond = 1f,
                    rudderCommandRatePerSecond = 1f
                },
                calibration = new VesselCalibration
                {
                    thrustMultiplier = 1f,
                    resistanceMultiplier = 1f,
                    rudderMultiplier = 1f,
                    buoyancyMultiplier = 1f
                }
            };
            return JsonUtility.ToJson(data);
        }
    }
}
