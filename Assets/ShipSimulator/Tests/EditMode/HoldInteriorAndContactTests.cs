using NUnit.Framework;
using ShipSimulator.Editor;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class HoldInteriorAndContactTests
    {
        private const string PrefabPath = "Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab";

        [Test]
        public void ForecastleWall_FacesAftAndCoversTheStepInEveryLod()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var vessel = Object.Instantiate(prefab);
            try { VolgoDonBowReview.Validate(vessel); }
            finally { Object.DestroyImmediate(vessel); }
        }

        // The old keel-level contact layout put the bow pair 1.5 m and the stern pair 2.2 m below the hull.
        [Test]
        public void SectionBottom_RisesAtTheForefootAndCutUpStern()
        {
            var sections = SectionHydrostatics.For(VesselFixtures.Load(VesselFixtures.VolgoDonFile));
            Assert.That(sections.BottomHeightAt(0f, 5.78f), Is.EqualTo(0f).Within(0.05f), "Flat midship bottom.");
            Assert.That(sections.BottomHeightAt(62.1f, 2.48f), Is.GreaterThan(1.0f));
            Assert.That(sections.BottomHeightAt(-62.1f, 4.95f), Is.GreaterThan(1.8f));
            Assert.That(sections.BottomHeightAt(62.1f, 7.5f), Is.NaN, "No hull that far out at the bow.");
        }

        [Test]
        public void Prefab_HoldVisualMatchesLoadingCompartmentsAndFitsTheHolds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var visual = prefab.GetComponent<HoldInteriorVisual>();
            Assert.That(visual, Is.Not.Null);
            Assert.That(visual.HoldCount, Is.EqualTo(2));
            VesselData data = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            Assert.That(visual.BulkDensityKgM3, Is.EqualTo(VolgoDonHoldIntegration.NominalBulkDensityKgM3));
            for (int i = 0; i < visual.HoldCount; i++)
            {
                HoldInteriorVisual.Hold hold = visual.Definition(i);
                LoadCompartment compartment = data.loading.compartments[hold.compartment];
                Assert.That(compartment.kind, Is.EqualTo("cargo"));
                Assert.That(Mathf.Abs(hold.floorCentre.z - compartment.centreLocalM.z), Is.LessThan(0.1f),
                    "The heap stands where the loading model puts the cargo.");
                Assert.That(visual.RequiredHeightM(i, compartment.capacityKg), Is.LessThan(hold.maxHeightM),
                    "Full capacity at the nominal density fits under the closed covers.");
                Assert.That(hold.maskHeight, Is.GreaterThan(hold.floorCentre.y + hold.maxHeightM));
            }
        }
    }
}
