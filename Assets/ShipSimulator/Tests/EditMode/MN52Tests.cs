using System.IO;
using System.Linq;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.Persistence;
using ShipSimulator.UI;
using UnityEngine;
using UnityEditor;

namespace ShipSimulator.Tests
{
    public sealed class MN52Tests
    {
        private static GameObject Prefab => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ShipSimulator/Prefabs/Vessels/MN52_T77.prefab");
        private static VesselData Data => JsonUtility.FromJson<VesselData>(Prefab.GetComponent<VesselDataLoader>().Json.text);

        [Test]
        public void PassiveData_HasNoActuatorsAndCannotGenerateThrust()
        {
            var data = Data;
            Assert.That(VesselDataValidator.TryValidate(data, out string error), Is.True, error);
            Assert.That(data.engine.engineCount, Is.Zero);
            var model = new ManoeuvringModel(VesselParameters.Create(data));
            foreach (float speed in new[] { -3f, 0f, 3f })
            {
                var result = model.Step(new ManoeuvringInput { SurgeSpeed = speed, EngineCommands = new[] { 1f },
                    RudderCommand = 1, DepthM = 20 }, .02f);
                Assert.That(result.PropellerX, Is.Zero);
                Assert.That(result.RudderY, Is.Zero);
                Assert.That(result.ResistanceX * speed, Is.LessThanOrEqualTo(0));
            }
            data.engine.engineCount = 1;
            Assert.That(VesselDataValidator.TryValidate(data, out _), Is.False);
        }

        [Test]
        public void Barge_UsesIndependentPhysicsAndScaledLods()
        {
            Assert.That(Prefab.GetComponent<PassiveVessel>(), Is.Not.Null);
            Assert.That(Prefab.GetComponent<ShipPhysicsController>(), Is.Null);
            Assert.That(Prefab.GetComponentsInChildren<Collider>().Length, Is.EqualTo(9));
            Assert.That(Prefab.GetComponentsInChildren<MeshCollider>(), Is.Empty);
            Vector3 Point(string name) => Prefab.transform.InverseTransformPoint(Prefab.GetComponentsInChildren<Transform>()
                .Single(t => t.name == "MN52_" + name).position);
            Assert.That(Point("Waterline").magnitude, Is.LessThan(.001f));
            Assert.That(Point("BowReference").z, Is.EqualTo(14.4f).Within(.001f));
            Assert.That(Point("PushConnection").z, Is.LessThan(-14.4f));
            int previous = int.MaxValue;
            foreach (var lod in Prefab.GetComponentInChildren<LODGroup>().GetLODs())
            {
                int triangles = 0;
                foreach (Renderer renderer in lod.renderers)
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    triangles += mesh.triangles.Length / 3;
                    Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                    foreach (Material material in renderer.sharedMaterials)
                        Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                }
                Assert.That(triangles, Is.LessThan(previous)); previous = triangles;
            }
        }

        [Test]
        public void Hydrostatics_UseManySectionsWithStableLoadedAndEmptyEquilibria()
        {
            var loaded = VesselParameters.Create(Data, 1);
            var empty = VesselParameters.Create(Data, 0);
            Assert.That(loaded.Mass, Is.EqualTo(153000));
            Assert.That(empty.Mass, Is.EqualTo(52500));
            Assert.That(loaded.Draft, Is.EqualTo(1.2f).Within(.025f));
            Assert.That(empty.Draft, Is.EqualTo(.5f).Within(.025f));
            Assert.That(new HydrostaticsModel(loaded).CellCount, Is.GreaterThan(100));
            Assert.That(new HydrostaticsModel(loaded).MetacentricHeightM, Is.GreaterThan(0));
            TestContext.WriteLine($"MN52 equilibrium loaded={loaded.Draft:F3} empty={empty.Draft:F3}");
        }

        [Test]
        public void Formation_KeepsOriginalTugAndRespectsPassageLength()
        {
            var catalogue = VesselCatalogue.Load();
            var solo = catalogue.Find("serpukhov-r96"); var unit = catalogue.Find("serpukhov-mn52");
            Assert.That(solo.prefab.GetComponent<PushUnit>(), Is.Null);
            var fallback = new Vector3(0, 3, 10);
            Assert.That(solo.prefab.GetComponent<ShipSimulator.Visuals.VesselLayout>().ExternalLookAtOr(fallback), Is.EqualTo(fallback));
            Assert.That(unit.prefab.GetComponent<ShipSimulator.Visuals.VesselLayout>().ExternalLookAtOr(fallback), Is.EqualTo(new Vector3(0, 2, 14)));
            Assert.That(unit.prefab.GetComponent<PushUnit>().BargePrefab, Is.EqualTo(Prefab));
            Assert.That(VoyagePassage.Find("RiverTrainingScene").FormationRestriction(unit), Is.Null);
            Assert.That(VoyagePassage.Find("SerpukhovZatonScene").FormationRestriction(unit), Is.Not.Null);
            Assert.That(VoyagePassage.Find("SerpukhovZatonScene").FormationRestriction(solo), Is.Null);
            var save = new PushUnitSave { present = true, bargeId = "mn52-t77", position = new Vector3(float.NaN, 0, 0) };
            Assert.Throws<InvalidDataException>(() => save.Validate());
        }
    }
}
