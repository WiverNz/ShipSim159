using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShipSimulator.Persistence;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class VesselCatalogueTests
    {
        [Test]
        public void Catalogue_ListsEveryVesselWithAPrefabValidDataAndMatchingLayout()
        {
            VesselCatalogue catalogue = VesselCatalogue.Load();
            Assert.That(catalogue, Is.Not.Null, "Resources/VesselCatalogue is missing.");
            Assert.That(catalogue.Find(VesselCatalogue.DefaultVesselId), Is.Not.Null);
            Assert.That(catalogue.Entries.Count, Is.EqualTo(5));

            var ids = new HashSet<string>();
            foreach (VesselCatalogue.Entry entry in catalogue.Entries)
            {
                Assert.That(ids.Add(entry.id), Is.True, "Duplicate vessel id " + entry.id);
                Assert.That(entry.prefab, Is.Not.Null, entry.id);
                Assert.That(entry.prefab.GetComponent<ShipPhysicsController>(), Is.Not.Null, entry.id);
                Assert.That(VesselDataValidator.TryValidate(entry.LoadData(), out string error), Is.True, entry.id + ": " + error);
                VesselLayout layout = entry.prefab.GetComponent<VesselLayout>();
                Assert.That(layout, Is.Not.Null, entry.id);
                Assert.That(layout.VesselId, Is.EqualTo(entry.id));
                Assert.That(entry.prefab.GetComponentsInChildren<BoxCollider>().Length, Is.GreaterThanOrEqualTo(3), entry.id);
                Assert.That(entry.menuName, Is.Not.Empty);
                Assert.That(entry.vesselClass, Is.Not.Empty);
            }
        }

        [TestCase("volgobalt-295ar")]
        [TestCase("volgoneft-1577")]
        [TestCase("meteor-342u")]
        [TestCase("luch-14352")]
        public void GeneratedModel_MatchesTheVesselDimensions(string id)
        {
            VesselCatalogue.Entry entry = VesselCatalogue.Load().Find(id);
            VesselData data = entry.LoadData();
            VesselDimensions dimensions = data.dimensions;
            Bounds bounds = ModelBounds(entry.prefab);
            // Foils and skegs reach below the hull; displacement ships end at the keel.
            float appendage = SupportModel.Lifts(data.support) ? data.support.supportedDraftM : 0f;

            Assert.That(bounds.size.z, Is.EqualTo(dimensions.lengthOverallM).Within(0.01f * dimensions.lengthOverallM));
            Assert.That(bounds.size.x, Is.InRange(dimensions.beamMouldedM - 0.05f, dimensions.beamOverallM + 0.05f));
            Assert.That(bounds.min.y, Is.InRange(-dimensions.loadedDraftM - appendage - 0.1f, -dimensions.loadedDraftM + 0.05f),
                "The keel sits at the loaded draft, with foils or skegs below it.");
            Assert.That(bounds.center.z, Is.EqualTo(0f).Within(3f), "The model is centred on midship.");
        }

        [TestCase("volgobalt-295ar")]
        [TestCase("volgoneft-1577")]
        [TestCase("meteor-342u")]
        [TestCase("luch-14352")]
        public void GeneratedLights_AftMastheadAboveForwardAndSideLightsOnTheBeam(string id)
        {
            VesselCatalogue.Entry entry = VesselCatalogue.Load().Find(id);
            VesselLayout layout = entry.prefab.GetComponent<VesselLayout>();
            VesselDimensions dimensions = entry.LoadData().dimensions;

            Assert.That(layout.AftMastheadLight.y, Is.GreaterThan(layout.ForwardMastheadLight.y + 1f));
            Assert.That(layout.AftMastheadLight.z, Is.LessThan(layout.ForwardMastheadLight.z));
            Assert.That(layout.PortSideLight.x, Is.LessThan(0f));
            Assert.That(-layout.PortSideLight.x, Is.LessThanOrEqualTo(0.5f * dimensions.beamOverallM));
            Assert.That(layout.CameraScale, Is.EqualTo(dimensions.lengthOverallM / 138.3f).Within(1e-3f));
        }

        [TestCase("volgobalt-295ar")]
        [TestCase("volgoneft-1577")]
        [TestCase("meteor-342u")]
        [TestCase("luch-14352")]
        public void GeneratedCameraViews_StandClearOfTheModel(string id)
        {
            VesselCatalogue.Entry entry = VesselCatalogue.Load().Find(id);
            VesselLayout layout = entry.prefab.GetComponent<VesselLayout>();
            Bounds bounds = ModelBounds(entry.prefab);

            Assert.That(layout.CameraViews.Length, Is.EqualTo(8), "One offset per orbit view.");
            foreach (Vector3 view in layout.CameraViews)
                Assert.That(Vector3.Distance(bounds.ClosestPoint(view), view), Is.GreaterThan(1f),
                    $"{id}: the view at {view} is inside or against its own model.");
            Assert.That(layout.CameraViews[5].z, Is.GreaterThan(bounds.max.z), "The bow view clears the stem.");
            Assert.That(layout.CameraViews[6].z, Is.LessThan(bounds.min.z), "The stern view clears the transom.");
            Assert.That(layout.CameraViews[1].y, Is.GreaterThan(bounds.max.y), "The bridge view clears the mast.");
        }

        [TestCase("meteor-342u")]
        [TestCase("luch-14352")]
        public void NavigatorView_ClearsTheOpaqueWheelhouse(string id)
        {
            var prefab = VesselCatalogue.Load().Find(id).prefab;
            var layout = prefab.GetComponent<VesselLayout>();
            var probe = new GameObject("Navigator obstruction probe");
            bool backfaces = UnityEngine.Physics.queriesHitBackfaces;
            Mesh opaque = OpaqueModelMesh(prefab);
            try
            {
                var collider = probe.AddComponent<MeshCollider>();
                collider.sharedMesh = opaque;
                UnityEngine.Physics.queriesHitBackfaces = true;
                UnityEngine.Physics.SyncTransforms();
                var direction = (layout.NavigatorLookAt - layout.NavigatorEye).normalized;
                Assert.That(collider.Raycast(new Ray(layout.NavigatorEye, direction), out _, 3f), Is.False,
                    "The view must not look through the cabin wall or roof.");
            }
            finally
            {
                UnityEngine.Physics.queriesHitBackfaces = backfaces;
                UnityEngine.Object.DestroyImmediate(probe);
                UnityEngine.Object.DestroyImmediate(opaque);
            }
        }

        private static IEnumerable<MeshFilter> ModelMeshes(GameObject prefab)
        {
            Transform visual = prefab.transform.Find("DetailedVisual");
            LODGroup group = visual.GetComponent<LODGroup>();
            if (group != null)
            {
                foreach (Renderer renderer in group.GetLODs()[0].renderers)
                    yield return renderer.GetComponent<MeshFilter>();
            }
            else
            {
                foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>())
                    yield return filter;
            }
        }

        private static Bounds ModelBounds(GameObject prefab)
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (MeshFilter filter in ModelMeshes(prefab))
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    bounds.Encapsulate(prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            return bounds;
        }

        private static Mesh OpaqueModelMesh(GameObject prefab)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (MeshFilter filter in ModelMeshes(prefab))
            {
                int offset = vertices.Count;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    vertices.Add(prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
                Material[] materials = filter.GetComponent<Renderer>().sharedMaterials;
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                {
                    // Authored glazing is transparent; a real window is not an opaque obstruction.
                    if (materials[submesh].renderQueue >= (int)UnityEngine.Rendering.RenderQueue.Transparent) continue;
                    foreach (int index in filter.sharedMesh.GetTriangles(submesh)) triangles.Add(offset + index);
                }
            }
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        [TestCase("meteor-342u")]
        [TestCase("luch-14352")]
        public void BridgeView_LooksAlongTheRiverRatherThanDownAtTheRoof(string id)
        {
            var cameraObject = new GameObject("Bridge view probe", typeof(Camera));
            try
            {
                var follow = cameraObject.AddComponent<ShipFollowCamera>();
                follow.SetTarget(VesselCatalogue.Load().Find(id).prefab.GetComponent<ShipPhysicsController>());
                follow.SetView(1);
                follow.RestoreState(follow.CaptureState());
                Assert.That(Vector3.Dot(cameraObject.transform.forward, Vector3.forward), Is.GreaterThan(0.9f));
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
        }

        [Test]
        public void SaveWithoutVesselId_RestoresTheOriginalVessel()
        {
            Assert.That(new VoyageSave().VesselIdOrDefault, Is.EqualTo(VesselCatalogue.DefaultVesselId));
            Assert.That(new VoyageSave { vesselId = "volgoneft-1577" }.VesselIdOrDefault, Is.EqualTo("volgoneft-1577"));
        }

        [Test]
        public void Save_RejectsAnOverlongVesselId()
        {
            var save = new VoyageSave
            {
                scene = "RiverTrainingScene",
                savedUtc = DateTime.UtcNow.ToString("O"),
                vesselId = new string('x', 65)
            };
            Assert.Throws<InvalidDataException>(save.Validate);
            save.vesselId = "volgobalt-295ar";
            Assert.DoesNotThrow(save.Validate);
        }
    }
}
