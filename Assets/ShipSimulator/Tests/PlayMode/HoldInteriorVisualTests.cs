using System.Collections;
using System.IO;
using NUnit.Framework;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShipSimulator.Tests
{
    public sealed class HoldInteriorVisualTests
    {
        private GameObject shipObject;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (shipObject != null) Object.Destroy(shipObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CargoHeaps_FollowCompartmentMassWithoutAddingMassOrColliders()
        {
            string path = Path.Combine(Application.dataPath, "ShipSimulator/Data/Vessels/VolgoDon507B.json");
            VesselData data = JsonUtility.FromJson<VesselData>(File.ReadAllText(path));
            shipObject = new GameObject("Hold test ship");
            shipObject.SetActive(false);
            shipObject.AddComponent<Rigidbody>();
            shipObject.AddComponent<VesselDataLoader>().Configure(new TextAsset(JsonUtility.ToJson(data)));
            var ship = shipObject.AddComponent<ShipPhysicsController>();
            ship.ManualStepping = true;
            var visual = shipObject.AddComponent<HoldInteriorVisual>();
            var mask = new Material(Shader.Find("ShipSimulator/HullWaterMask"));
            visual.Configure(null, mask, 1600f, new[]
            {
                new HoldInteriorVisual.Hold { name = "Fore", compartment = 0, floorCentre = new Vector3(0, -2.6f, 31.72f),
                    footprint = new Vector2(11.38f, 43.2f), maxHeightM = 4.9f, maskHeight = 2.55f },
                new HoldInteriorVisual.Hold { name = "Aft", compartment = 1, floorCentre = new Vector3(0, -2.6f, -18.49f),
                    footprint = new Vector2(11.38f, 44.56f), maxHeightM = 4.9f, maskHeight = 2.55f }
            });
            shipObject.SetActive(true);
            yield return null;

            float mass = ship.Body.mass;
            Assert.That(visual.HeightM(0), Is.EqualTo(visual.RequiredHeightM(0, ship.CompartmentMassKg(0))).Within(1e-3f));
            Assert.That(visual.HeightM(0), Is.InRange(2.5f, 4.9f));
            Assert.That(shipObject.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(visual.WaterMask(0).transform.parent, Is.EqualTo(shipObject.transform), "The mask moves with the hull.");
            Assert.That(visual.WaterMask(1).activeSelf, Is.True);

            LoadingState state = ship.CaptureLoading();
            state.massKg[0] = 0f;
            state.massKg[1] = 1200000f;
            ship.RestoreVoyage(new VoyageSave { loading = state });
            yield return null;
            Assert.That(visual.Heap(0).gameObject.activeSelf, Is.False, "An empty hold shows its dry floor.");
            Assert.That(visual.HeightM(1), Is.EqualTo(visual.RequiredHeightM(1, 1200000f)).Within(1e-3f));
            Assert.That(ship.Body.mass, Is.EqualTo(mass - 3600000f).Within(1f), "The visual carries no mass of its own.");

            Assert.That(visual.RequiredHeightM(1, 20000000f), Is.GreaterThan(4.9f));
            state.massKg[1] = 20000000f;
            visual.Show(state.massKg);
            Assert.That(visual.HeightM(1), Is.EqualTo(4.9f).Within(1e-4f), "Overfill is drawn full, never above the covers.");
            Assert.That(visual.Overfilled(1), Is.True);
            Object.Destroy(mask);
        }
    }
}
