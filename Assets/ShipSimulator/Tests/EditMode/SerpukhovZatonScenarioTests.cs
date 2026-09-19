using NUnit.Framework;
using ShipSimulator.Editor;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class SerpukhovZatonScenarioTests
    {
        private const string ScenePath = "Assets/ShipSimulator/Scenes/SerpukhovZatonScene.unity";
        private const string GeometryPath = "Assets/ShipSimulator/Data/Scenarios/SerpukhovZaton.json";

        [Test]
        public void ZatonScene_IsEnabledInBuildSettingsBehindTheTrainingScenes()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int index = System.Array.FindIndex(scenes, scene => scene.path == ScenePath);

            Assert.That(index, Is.GreaterThanOrEqualTo(2), "Gorodets and the river scene keep the first two slots.");
            Assert.That(scenes[index].enabled, Is.True);
        }

        [Test]
        public void ZatonScene_HasRoutePhysicsMissionUnlitBuoysAndAVesselThatFits()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            FairwayRoute route = Object.FindAnyObjectByType<FairwayRoute>();
            ShipPhysicsController ship = Object.FindAnyObjectByType<ShipPhysicsController>();
            GorodetsScenarioController mission = Object.FindAnyObjectByType<GorodetsScenarioController>();

            Assert.That(scene.IsValid(), Is.True);
            Assert.That(route, Is.Not.Null);
            Assert.That(route.LengthM, Is.InRange(2300f, 2500f));
            Assert.That(Object.FindAnyObjectByType<ScenarioBathymetry>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<CurrentFieldProvider>(), Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<WeatherController>(), Is.Not.Null);
            Assert.That(mission, Is.Not.Null);
            Assert.That(ship, Is.Not.Null);
            Assert.That(ship.gameObject.name, Is.EqualTo("TrainingVessel"));
            Assert.That(ship.GetComponent<GroundingController>(), Is.Not.Null);

            VesselCatalogue catalogue = VesselCatalogue.Load();
            string builtId = VesselSwap.IdOf(ship);
            VoyagePassage passage = VoyagePassage.Find("SerpukhovZatonScene");
            Assert.That(passage, Is.Not.Null);
            Assert.That(passage.Accepts(catalogue.Find(builtId).LoadData()), Is.True,
                "The scene must be built with a vessel its own passage admits.");

            GameObject water = GameObject.Find("RiverWater");
            Assert.That(water, Is.Not.Null);
            Assert.That(water.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Assert.That(water.GetComponent<Renderer>().sharedMaterial.shader.name,
                Is.EqualTo("ShipSimulator/RiverWater"));

            Transform navigation = GameObject.Find("Navigation").transform;
            int buoys = 0;
            foreach (Transform mark in navigation)
            {
                if (!mark.name.Contains("Buoy")) continue;
                buoys++;
                // The reach is a third-category waterway: marks are painted, never lit.
                Assert.That(mark.name, Does.Contain("Unlit"), mark.name);
            }
            Assert.That(buoys, Is.GreaterThan(15));
        }

        [Test]
        public void Passages_AdmitEveryVesselToTheOpenRiverAndOnlyFastCraftToTheZaton()
        {
            VesselCatalogue catalogue = VesselCatalogue.Load();
            VoyagePassage zaton = VoyagePassage.Find("SerpukhovZatonScene");
            VoyagePassage river = VoyagePassage.Find("RiverTrainingScene");
            var admitted = new System.Collections.Generic.List<string>();

            foreach (VesselCatalogue.Entry entry in catalogue.Entries)
            {
                VesselData data = entry.LoadData();
                Assert.That(river.Accepts(data), Is.True, entry.id);
                if (zaton.Accepts(data)) admitted.Add(entry.id);
                else Assert.That(zaton.Restriction(data), Is.Not.Empty, entry.id);
            }

            Assert.That(admitted, Is.EquivalentTo(new[] { "meteor-342u", "luch-14352" }));
        }

        [Test]
        public void ZatonSave_IsAcceptedAndNamedByTheMenu()
        {
            Assert.That(VoyageSave.IsVoyageScene("SerpukhovZatonScene"), Is.True);
            Assert.That(VoyageSave.IsVoyageScene("NoSuchScene"), Is.False);
            Assert.That(VoyagePassage.TitleOf("SerpukhovZatonScene"), Is.EqualTo("Serpukhov zaton"));
        }

        [Test]
        public void NightLighting_SkipsMarksNamedUnlitAndStillLightsTheRest()
        {
            GameObject existing = GameObject.Find("Navigation");
            if (existing != null) existing.name = "Existing Navigation";
            var navigation = new GameObject("Navigation");
            var unlit = new GameObject("Unlit Left White Buoy 0100");
            var lit = new GameObject("Left White Buoy 0100");
            unlit.transform.SetParent(navigation.transform, false);
            lit.transform.SetParent(navigation.transform, false);
            var controllerObject = new GameObject("Day Night");

            try
            {
                controllerObject.AddComponent<DayNightController>().Apply(true);

                Assert.That(unlit.GetComponentInChildren<Light>(), Is.Null,
                    "A third-category mark carries no light.");
                Assert.That(unlit.GetComponent<BuoyVisualRig>(), Is.Not.Null,
                    "It still gets the detailed buoy body.");
                Assert.That(lit.GetComponentInChildren<Light>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(navigation);
                if (existing != null) existing.name = "Navigation";
            }
        }

        [Test]
        public void Geometry_KeepsEveryFairwaySampleInsideTheChartedWater()
        {
            ScenarioGeometry geometry = ScenarioGeometry.Load(GeometryPath);

            Assert.That(geometry.route.Length, Is.GreaterThan(10));
            foreach (ScenarioGeometry.RouteSample sample in geometry.route)
            {
                float shore = geometry.SignedShoreDistance(sample.x, sample.z);
                // Negative is water, and the sample has to clear its own half-width of marked channel.
                Assert.That(shore, Is.LessThan(-sample.leftWidthM * 0.5f),
                    $"Fairway sample at {sample.x:0}, {sample.z:0} runs too close to the bank.");
            }
        }

        [Test]
        public void Geometry_MarksItsEstimatedValuesAndKeepsThePublishedChannelWidths()
        {
            ScenarioGeometry geometry = ScenarioGeometry.Load(GeometryPath);

            Assert.That(geometry.provenance.depths, Does.Contain("ESTIMATED"));
            Assert.That(geometry.provenance.hazards, Does.Contain("ESTIMATED"));
            Assert.That(geometry.provenance.shorelines, Does.Contain("OpenStreetMap"));
            foreach (ScenarioGeometry.RouteSample sample in geometry.route)
            {
                // Published guaranteed widths: 30 m on the Oka, 20 m on the Nara.
                Assert.That(sample.leftWidthM + sample.rightWidthM, Is.LessThanOrEqualTo(120f));
                Assert.That(sample.speedLimitMps, Is.InRange(0.5f, 5f));
            }
        }
    }
}
