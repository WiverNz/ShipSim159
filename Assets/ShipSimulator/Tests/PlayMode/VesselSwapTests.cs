using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class VesselSwapTests
    {
        private readonly List<GameObject> created = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in created)
                if (item != null) Object.Destroy(item);
            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Replace_MovesCameraGroundingAndMissionToTheChosenVessel()
        {
            VesselCatalogue catalogue = VesselCatalogue.Load();
            Vector3 position = new Vector3(10f, 0f, 20f);
            GameObject original = Object.Instantiate(catalogue.Find(VesselCatalogue.DefaultVesselId).prefab, position,
                Quaternion.Euler(0f, 30f, 0f));
            original.name = "TrainingVessel";
            created.Add(original);
            var ship = original.GetComponent<ShipPhysicsController>();
            GroundingController grounding = original.AddComponent<GroundingController>();
            grounding.Configure(ship, null);

            var cameraObject = new GameObject("Swap test camera");
            created.Add(cameraObject);
            var camera = cameraObject.AddComponent<ShipFollowCamera>();
            camera.SetTarget(ship);
            var missionObject = new GameObject("Swap test mission");
            missionObject.SetActive(false);
            created.Add(missionObject);
            var mission = missionObject.AddComponent<GorodetsScenarioController>();
            mission.SetShip(ship, grounding);
            yield return null;

            ShipPhysicsController replacement = VesselSwap.Replace(ship, catalogue.Find("volgoneft-1577").prefab);
            created.Add(replacement.gameObject);

            Assert.That(original == null, Is.True, "The replaced vessel must be destroyed.");
            Assert.That(VesselSwap.IdOf(replacement), Is.EqualTo("volgoneft-1577"));
            Assert.That(replacement.name, Is.EqualTo("TrainingVessel"));
            Assert.That(Vector3.Distance(replacement.transform.position, position), Is.LessThan(0.01f));
            Assert.That(camera.Target, Is.EqualTo(replacement));
            Assert.That(mission.Ship, Is.EqualTo(replacement));
            Assert.That(replacement.Grounding, Is.Not.Null, "Grounding must exist before the new vessel's Awake reads it.");
            Assert.That(replacement.Data.identity.project, Is.EqualTo("1577"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Apply_KeepsTheSceneVesselWhenItIsAlreadyTheChosenOne()
        {
            VesselCatalogue catalogue = VesselCatalogue.Load();
            GameObject original = Object.Instantiate(catalogue.Find("volgobalt-295ar").prefab);
            created.Add(original);
            yield return null;

            ShipPhysicsController result = VesselSwap.Apply(catalogue, "volgobalt-295ar");

            Assert.That(result.gameObject, Is.EqualTo(original));
        }
    }
}
