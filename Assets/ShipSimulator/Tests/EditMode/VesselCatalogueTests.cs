using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ShipSimulator.Persistence;
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
            Bounds bounds = entry.prefab.transform.Find("DetailedVisual").GetComponent<MeshFilter>().sharedMesh.bounds;
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
