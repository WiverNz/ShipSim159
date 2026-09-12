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
            shipObject = TestVessel.Create(position);
            return shipObject.GetComponent<ShipPhysicsController>();
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

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0001f));
        }
    }
}
