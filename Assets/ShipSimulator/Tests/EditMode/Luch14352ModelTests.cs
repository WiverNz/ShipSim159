using System.Linq;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class Luch14352ModelTests
    {
        [Test]
        public void AuthoredVisual_PreservesPhysicsAndUsesThreeImportedLods()
        {
            GameObject prefab = VesselCatalogue.Load().Find("luch-14352").prefab;
            Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<ShipPhysicsController>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<VesselDataLoader>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<BoxCollider>().Length, Is.EqualTo(3));
            Transform visual = prefab.transform.Find("DetailedVisual");
            Assert.That(visual.GetComponentsInChildren<Collider>(), Is.Empty);
            Assert.That(visual.GetComponent<MeshFilter>(), Is.Null, "The old procedural renderer must be removed.");
            LOD[] lods = visual.GetComponent<LODGroup>().GetLODs();
            Assert.That(lods.Length, Is.EqualTo(3));
            int[] budgets = { 80000, 30000, 8000 };
            int previous = int.MaxValue;
            for (int i = 0; i < lods.Length; i++)
            {
                Assert.That(lods[i].renderers.Length, Is.EqualTo(1));
                Mesh mesh = lods[i].renderers[0].GetComponent<MeshFilter>().sharedMesh;
                Assert.That(AssetDatabase.GetAssetPath(mesh), Does.EndWith("Luch14352.fbx"));
                int triangles = mesh.triangles.Length / 3;
                Assert.That(triangles, Is.LessThanOrEqualTo(budgets[i]));
                Assert.That(triangles, Is.LessThan(previous));
                previous = triangles;
                Assert.That(lods[i].renderers[0].sharedMaterials.All(m =>
                    m != null && m.shader.name == "Universal Render Pipeline/Lit"), Is.True);
            }
        }

        [Test]
        public void ImportedLandmarks_AreMetresAtWaterlineWithoutReflection()
        {
            GameObject prefab = VesselCatalogue.Load().Find("luch-14352").prefab;
            Transform visual = prefab.transform.Find("DetailedVisual");
            Vector3 Point(string name) => prefab.transform.InverseTransformPoint(
                visual.GetComponentsInChildren<Transform>().Single(t => t.name == name).position);
            Assert.That(Point("Waterline").magnitude, Is.LessThan(.001f));
            Assert.That(Point("BowReference").z, Is.EqualTo(11.86f).Within(.001f));
            Assert.That(Point("StarboardReference").x, Is.EqualTo(2.265f).Within(.001f));
            Assert.That(Point("UpReference").y, Is.EqualTo(1f).Within(.001f));
            Assert.That(Point("WaterJetPoint").z, Is.LessThan(-11f));
            Assert.That(prefab.GetComponent<VesselLayout>().NavigatorEye,
                Is.EqualTo(Point("CameraNavigator")));
            foreach (Transform transform in visual.GetComponentsInChildren<Transform>())
                Assert.That(transform.localScale.x * transform.localScale.y * transform.localScale.z, Is.GreaterThan(0f));
        }
    }
}
