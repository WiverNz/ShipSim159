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
        public void Moorings_HaveSolidCollidersAndOneRadarFootprintPerObject()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform root = GameObject.Find("Laid-up craft").transform;
            ScenarioGeometry geometry = ScenarioGeometry.Load(GeometryPath);
            Assert.That(root.GetComponentsInChildren<RadarObstacle>().Length,
                Is.EqualTo(geometry.moorings.Length));
            foreach (Transform part in root)
            {
                BoxCollider collider = part.GetComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null, part.name);
                Assert.That(collider.enabled && !collider.isTrigger, Is.True, part.name);
                Assert.That(collider.attachedRigidbody, Is.Null, "Moored objects stay fixed.");
            }
            foreach (ScenarioGeometry.Mooring mooring in geometry.moorings)
            {
                Transform hull = root.Find(mooring.name);
                Assert.That(hull.GetComponent<RadarObstacle>(), Is.Not.Null, mooring.name);
                Assert.That(Vector3.Distance(hull.position, mooring.Center), Is.LessThan(1f));
                Assert.That(hull.localScale.z, Is.EqualTo(mooring.lengthM).Within(0.01f));
                Assert.That(hull.localScale.x, Is.EqualTo(Mathf.Max(2.5f, mooring.widthM)).Within(0.01f));
            }
        }

        [TestCase(0f, 90f, 82f, -82f, -90f)]
        [TestCase(90f, 90f, 0f, 0f, 0f)]
        [TestCase(180f, 90f, -82f, -82f, 90f)]
        public void RadarFootprint_UsesHullDimensionsAndHeadUpPositionAndHeading(
            float heading, float hullHeading, float x, float y, float angle)
        {
            var hull = new GameObject("Radar test hull");
            try
            {
                hull.transform.position = new Vector3(110f, 0f, 20f);
                hull.transform.rotation = Quaternion.Euler(0f, hullHeading, 0f);
                hull.transform.localScale = new Vector3(12f, 3f, 70f);
                RadarObstacle obstacle = hull.AddComponent<RadarObstacle>();
                obstacle.Project(new Vector3(10f, 0f, 20f), heading, 0.82f, -82f,
                    out Vector2 position, out Vector2 size, out float rotation);
                Assert.That(position.x, Is.EqualTo(x).Within(0.001f));
                Assert.That(position.y, Is.EqualTo(y).Within(0.001f));
                Assert.That(size.x, Is.EqualTo(9.84f).Within(0.001f));
                Assert.That(size.y, Is.EqualTo(57.4f).Within(0.001f));
                Assert.That(rotation, Is.EqualTo(angle).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(hull);
            }
        }

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
        public void Passages_AdmitEveryVesselToTheOpenRiverAndOnlyTheLuchToTheZaton()
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

            Assert.That(admitted, Is.EquivalentTo(new[] { "luch-14352" }));
        }

        [Test]
        public void PassageDraft_CountsTheFoilsAndSkegsThatTouchTheBottomFirst()
        {
            VesselCatalogue catalogue = VesselCatalogue.Load();
            VesselData meteor = catalogue.Find("meteor-342u").LoadData();
            VesselData luch = catalogue.Find("luch-14352").LoadData();
            VesselData cargo = catalogue.Find(VesselCatalogue.DefaultVesselId).LoadData();

            // A Meteor with her foils down reaches 2.35 m, not the 1.15 m of her hull, so a waterway
            // guaranteed to 1.00 m is not hers whatever the hull draft says.
            Assert.That(VoyagePassage.DeepestDraftM(meteor), Is.EqualTo(2.35f).Within(0.05f));
            Assert.That(VoyagePassage.DeepestDraftM(luch), Is.EqualTo(1.11f).Within(0.05f));
            Assert.That(VoyagePassage.DeepestDraftM(cargo),
                Is.EqualTo(cargo.dimensions.loadedDraftM).Within(1e-4f), "A displacement hull has no appendage.");
            Assert.That(VoyagePassage.Find("SerpukhovZatonScene").Restriction(meteor), Does.Contain("Too deep"));
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
        public void Geometry_CarriesRealReliefMappedLandCoverAndTracedMoorings()
        {
            ScenarioGeometry geometry = ScenarioGeometry.Load(GeometryPath);

            ScenarioGeometry.ElevationBlock relief = geometry.elevation;
            Assert.That(relief, Is.Not.Null);
            Assert.That(relief.heightsM, Has.Length.EqualTo(relief.columns * relief.rows));
            // The Nara mouth stands about 107 m above sea level; the grid is levelled to the river.
            Assert.That(relief.demWaterLevelM, Is.InRange(100f, 115f));
            Assert.That(geometry.ElevationAbove(0f, 0f), Is.InRange(-8f, 8f), "The river surface is the datum.");
            float highest = float.MinValue;
            foreach (float height in relief.heightsM) highest = Mathf.Max(highest, height);
            Assert.That(highest, Is.GreaterThan(15f), "The valley sides have to rise above the floodplain.");

            var covers = new System.Collections.Generic.HashSet<string>();
            foreach (ScenarioGeometry.LandCover cover in geometry.landcover) covers.Add(cover.cover);
            Assert.That(covers, Does.Contain("wood"));
            Assert.That(covers, Does.Contain("built"));
            Assert.That(geometry.buildings, Has.Length.GreaterThan(40));

            Assert.That(geometry.moorings, Has.Length.GreaterThan(12));
            foreach (ScenarioGeometry.Mooring craft in geometry.moorings)
            {
                Assert.That(geometry.SignedShoreDistance(craft.x, craft.z), Is.LessThan(0f),
                    craft.name + " has to be afloat.");
                Assert.That(craft.lengthM, Is.InRange(5f, 130f), craft.name);
                Assert.That(craft.widthM, Is.InRange(1f, 30f), craft.name);
            }
        }

        [Test]
        public void ZatonScene_ShowsTheTownAndTheLaidUpCraftThatMakeTheWayInLegible()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject craft = GameObject.Find("Laid-up craft");
            GameObject town = GameObject.Find("Shore buildings");

            Assert.That(craft, Is.Not.Null);
            Assert.That(craft.transform.childCount, Is.GreaterThan(20));
            Assert.That(town, Is.Not.Null);
            Assert.That(town.transform.childCount, Is.GreaterThan(80));
        }

        [Test]
        public void Geometry_MarksItsEstimatedValuesAndKeepsThePublishedChannelWidths()
        {
            ScenarioGeometry geometry = ScenarioGeometry.Load(GeometryPath);

            Assert.That(geometry.provenance.depths, Does.Contain("ESTIMATED"));
            Assert.That(geometry.provenance.hazards, Does.Contain("ESTIMATED"));
            Assert.That(geometry.provenance.shorelines, Does.Contain("OpenStreetMap"));
            Assert.That(geometry.provenance.moorings, Does.Contain("APPROXIMATE"));
            Assert.That(geometry.provenance.elevation, Does.Contain("Not a survey"));
            foreach (ScenarioGeometry.RouteSample sample in geometry.route)
            {
                // Published guaranteed widths: 30 m on the Oka, 20 m on the Nara.
                Assert.That(sample.leftWidthM + sample.rightWidthM, Is.LessThanOrEqualTo(120f));
                Assert.That(sample.speedLimitMps, Is.InRange(0.5f, 5f));
            }
        }
    }
}
