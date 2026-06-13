using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShipSimulator.Physics;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class RiverCurrentZoneTests
    {
        private GameObject shipObject;
        private readonly List<GameObject> zoneObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (shipObject != null) Object.Destroy(shipObject);
            foreach (GameObject zoneObject in zoneObjects)
                if (zoneObject != null) Object.Destroy(zoneObject);
            zoneObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TriggerZone_OverridesAmbientCurrentAndRestoresItOnExit()
        {
            ShipPhysicsController ship = CreateShip(Vector3.zero);
            RiverCurrentZone zone = CreateZone(Vector3.zero, new Vector3(1.25f, 0f, -0.4f));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            AssertVector(zone.CurrentVelocityMps, ship.EffectiveCurrentMps);

            shipObject.transform.position = new Vector3(100f, 0f, 0f);
            UnityEngine.Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            AssertVector(new Vector3(0f, 0f, 0.35f), ship.EffectiveCurrentMps);
        }

        [UnityTest]
        public IEnumerator OverlappingZones_AreAveraged()
        {
            ShipPhysicsController ship = CreateShip(Vector3.zero);
            RiverCurrentZone first = CreateZone(Vector3.zero, new Vector3(1f, 0f, 0f));
            RiverCurrentZone second = CreateAdditionalZone(
                Vector3.zero, new Vector3(0f, 0f, 2f));

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            AssertVector((first.CurrentVelocityMps + second.CurrentVelocityMps) * 0.5f,
                ship.EffectiveCurrentMps);
        }

        private ShipPhysicsController CreateShip(Vector3 position)
        {
            shipObject = new GameObject("TestShip");
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

            ShipPhysicsController controller = shipObject.AddComponent<ShipPhysicsController>();
            shipObject.SetActive(true);
            return controller;
        }

        private RiverCurrentZone CreateZone(Vector3 position, Vector3 current)
        {
            GameObject zoneObject = CreateZoneObject("CurrentZone", position, current);
            zoneObjects.Add(zoneObject);
            return zoneObject.GetComponent<RiverCurrentZone>();
        }

        private RiverCurrentZone CreateAdditionalZone(Vector3 position, Vector3 current)
        {
            GameObject zoneObject = CreateZoneObject("AdditionalCurrentZone", position, current);
            zoneObjects.Add(zoneObject);
            return zoneObject.GetComponent<RiverCurrentZone>();
        }

        private static GameObject CreateZoneObject(string name, Vector3 position, Vector3 current)
        {
            var zone = new GameObject(name);
            zone.transform.position = position;
            BoxCollider collider = zone.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = Vector3.one * 10f;
            RiverCurrentZone currentZone = zone.AddComponent<RiverCurrentZone>();
            currentZone.Configure(current);
            return zone;
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

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }
    }
}
