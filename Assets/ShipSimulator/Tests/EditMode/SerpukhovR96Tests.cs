using System.Linq;
using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using ShipSimulator.UI;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class SerpukhovR96Tests
    {
        private static VesselCatalogue.Entry Entry => VesselCatalogue.Load().Find("serpukhov-r96");

        [Test]
        public void Serpukhov_UsesOneWaterjetAndFitsItsHistoricalWaterway()
        {
            VesselData data = Entry.LoadData();
            Assert.That(VesselDataValidator.TryValidate(data, out string error), Is.True, error);
            Assert.That(data.engine.engineCount, Is.EqualTo(1));
            Assert.That(data.engine.powerPerEngineW, Is.EqualTo(110000));
            Assert.That(data.propeller.count, Is.EqualTo(1));
            Assert.That(data.propeller.bladeCount, Is.EqualTo(4));
            Assert.That(data.propeller.diameterM, Is.EqualTo(.846f));
            Assert.That(data.steeringNozzle.enabled, Is.True);
            Assert.That(data.massProperties.loadedMassKg, Is.EqualTo(45230));
            Assert.That(data.dimensions.loadedDraftM, Is.EqualTo(.62f));
            Assert.That(data.calibration.trainingValidated, Is.False);
            Assert.That(VoyagePassage.Find("SerpukhovZatonScene").Accepts(data), Is.True);
        }

        [Test]
        public void Serpukhov_HasAlignedWaterlineAndCamerasWithIndependentCollision()
        {
            GameObject root = Entry.prefab;
            Vector3 Point(string name) => root.transform.InverseTransformPoint(
                root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name).position);
            Assert.That(Point("Waterline").magnitude, Is.LessThan(.001f));
            Assert.That(Point("BowReference").z, Is.EqualTo(10.9f).Within(.01f));
            Assert.That(Point("StarboardReference").x, Is.EqualTo(2.65f).Within(.01f));
            Assert.That(Point("WaterjetOutlet").z, Is.LessThan(-10));
            Assert.That(root.GetComponent<VesselLayout>().NavigatorEye, Is.EqualTo(Point("CameraNavigator")));
            Assert.That(root.GetComponent<VesselLayout>().SingleMasthead, Is.True);
            Assert.That(root.GetComponentsInChildren<Rigidbody>().Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<MeshCollider>(), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Collider>().All(c => c is BoxCollider && !c.isTrigger), Is.True);
            Assert.That(root.transform.Find("DetailedVisual").GetComponentsInChildren<Collider>(), Is.Empty);
        }

        [Test]
        public void Serpukhov_LODsRetainGlazingAndValidMaterials()
        {
            LOD[] lods = Entry.prefab.GetComponentInChildren<LODGroup>().GetLODs();
            Assert.That(lods.Length, Is.EqualTo(3));
            int previous = int.MaxValue;
            foreach (LOD lod in lods)
            {
                int triangles = 0;
                foreach (Renderer r in lod.renderers)
                {
                    Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                    triangles += mesh.triangles.Length / 3;
                    Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                    foreach (Material m in r.sharedMaterials)
                    {
                        Assert.That(m.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                        if (m.name == "R96_Glass")
                        {
                            Assert.That(m.renderQueue, Is.EqualTo(3000));
                            Assert.That(m.GetColor("_BaseColor").a, Is.LessThan(.4f));
                        }
                    }
                }
                Assert.That(triangles, Is.LessThan(previous)); previous = triangles;
                Assert.That(lod.renderers.SelectMany(r => r.sharedMaterials).Any(m => m.name == "R96_Glass"), Is.True);
            }
        }

        [Test]
        public void Serpukhov_ThrustTurnsAtLowSpeedAndReverses()
        {
            var p = VesselParameters.Create(Entry.LoadData());
            var starboard = new ManoeuvringModel(p);
            var port = new ManoeuvringModel(p);
            starboard.RestoreActuators(.3f, new[] { p.RatedRps });
            port.RestoreActuators(-.3f, new[] { p.RatedRps });
            var input = new ManoeuvringInput { RudderCommand = .3f / (.25f * Mathf.PI), DepthM = float.PositiveInfinity };
            var a = starboard.Step(input, .02f); input.RudderCommand *= -1;
            var b = port.Step(input, .02f);
            Assert.That(a.PropellerX, Is.GreaterThan(1000));
            Assert.That(a.PropellerN, Is.GreaterThan(0));
            Assert.That(b.PropellerN, Is.EqualTo(-a.PropellerN).Within(.1f));
            starboard.RestoreActuators(0, new[] { -p.RatedRps });
            input.RudderCommand = 0;
            Assert.That(starboard.Step(input, .02f).PropellerX, Is.LessThan(-1000));
        }
    }
}
