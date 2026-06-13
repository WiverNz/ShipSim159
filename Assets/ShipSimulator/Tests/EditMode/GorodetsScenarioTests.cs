using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class GorodetsScenarioTests
    {
        private const string ScenePath =
            "Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity";

        [Test]
        public void GorodetsScene_IsEnabledAfterPrimaryTrainingScene()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            Assert.That(scenes, Has.Length.GreaterThanOrEqualTo(2));
            Assert.That(scenes[0].path, Is.EqualTo(
                "Assets/ShipSimulator/Scenes/RiverTrainingScene.unity"));
            int scenarioIndex = System.Array.FindIndex(
                scenes, scene => scene.path == ScenePath);
            Assert.That(scenarioIndex, Is.GreaterThan(0));
            Assert.That(scenes[scenarioIndex].enabled, Is.True);
        }

        [Test]
        public void GorodetsScene_HasRoutePhysicsMissionAndExistingVessel()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            FairwayRoute route = Object.FindAnyObjectByType<FairwayRoute>();
            ScenarioBathymetry bathymetry =
                Object.FindAnyObjectByType<ScenarioBathymetry>();
            CurrentFieldProvider current =
                Object.FindAnyObjectByType<CurrentFieldProvider>();
            WeatherController weather =
                Object.FindAnyObjectByType<WeatherController>();
            GorodetsScenarioController mission =
                Object.FindAnyObjectByType<GorodetsScenarioController>();
            ShipPhysicsController ship =
                Object.FindAnyObjectByType<ShipPhysicsController>();

            Assert.That(scene.IsValid(), Is.True);
            Assert.That(route, Is.Not.Null);
            Assert.That(route.SampleCount, Is.EqualTo(11));
            Assert.That(route.LengthM, Is.InRange(2200f, 2350f));
            Assert.That(bathymetry, Is.Not.Null);
            Assert.That(current, Is.Not.Null);
            Assert.That(weather, Is.Not.Null);
            Assert.That(mission, Is.Not.Null);
            Assert.That(ship, Is.Not.Null);
            Assert.That(ship.gameObject.name, Is.EqualTo("TrainingVessel"));
            Assert.That(ship.GetComponent<GroundingController>(), Is.Not.Null);
            GameObject water = GameObject.Find("RiverWater");
            Assert.That(water, Is.Not.Null);
            Assert.That(water.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
            Assert.That(
                water.GetComponent<Renderer>().sharedMaterial.shader.name,
                Is.EqualTo("ShipSimulator/RiverWater"));
        }

        [Test]
        public void FairwayRoute_QueryIsContinuousAndTracksLateralSide()
        {
            GameObject gameObject = new GameObject("Route Test");
            try
            {
                FairwayRoute route = gameObject.AddComponent<FairwayRoute>();
                route.Configure(new[]
                {
                    RouteSample(new Vector3(0f, 0f, 0f)),
                    RouteSample(new Vector3(20f, 0f, 100f)),
                    RouteSample(new Vector3(0f, 0f, 200f))
                });

                FairwayQuery first = route.QueryDistance(99f);
                FairwayQuery second = route.QueryDistance(101f);
                FairwayQuery right = route.Query(first.Position + first.Right * 12f);

                Assert.That(Vector3.Distance(first.Position, second.Position),
                    Is.LessThan(3f));
                Assert.That(first.Tangent.magnitude, Is.EqualTo(1f).Within(0.001f));
                Assert.That(right.LateralOffsetM, Is.GreaterThan(10f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Bathymetry_UsesAsymmetricEdgesAndRockHazard()
        {
            GameObject routeObject = new GameObject("Route");
            GameObject depthObject = new GameObject("Depth");
            try
            {
                FairwayRoute route = routeObject.AddComponent<FairwayRoute>();
                route.Configure(new[]
                {
                    RouteSample(new Vector3(0f, 0f, 0f)),
                    RouteSample(new Vector3(0f, 0f, 200f))
                });
                ScenarioBathymetry bathymetry =
                    depthObject.AddComponent<ScenarioBathymetry>();
                bathymetry.Configure(route, new[]
                {
                    new BathymetryHazard
                    {
                        center = new Vector3(0f, 0f, 100f),
                        sizeM = new Vector2(20f, 40f),
                        depthReductionM = 2f,
                        bottomType = RiverBottomType.Rock
                    }
                });

                BathymetrySample left = bathymetry.Sample(
                    new Vector3(-40f, 0f, 40f));
                BathymetrySample right = bathymetry.Sample(
                    new Vector3(40f, 0f, 40f));
                BathymetrySample center = bathymetry.Sample(
                    new Vector3(0f, 0f, 40f));
                BathymetrySample hazard = bathymetry.Sample(
                    new Vector3(0f, 0f, 100f));

                Assert.That(left.DepthM, Is.Not.EqualTo(right.DepthM).Within(0.05f));
                Assert.That(hazard.BottomType, Is.EqualTo(RiverBottomType.Rock));
                Assert.That(hazard.DepthM, Is.LessThan(center.DepthM));
            }
            finally
            {
                Object.DestroyImmediate(routeObject);
                Object.DestroyImmediate(depthObject);
            }
        }

        [Test]
        public void CurrentField_ComposesAdditiveAndOverrideRegions()
        {
            GameObject gameObject = new GameObject("Current");
            try
            {
                CurrentFieldProvider provider =
                    gameObject.AddComponent<CurrentFieldProvider>();
                provider.Configure(new Vector3(0f, 0f, 0.2f), new[]
                {
                    new CurrentRegionData
                    {
                        center = Vector3.zero,
                        size = Vector3.one * 20f,
                        velocityMps = new Vector3(0.5f, 0f, 0f),
                        compositionMode = CurrentCompositionMode.Additive
                    },
                    new CurrentRegionData
                    {
                        center = new Vector3(30f, 0f, 0f),
                        size = Vector3.one * 20f,
                        velocityMps = new Vector3(-0.3f, 0f, 1.1f),
                        compositionMode = CurrentCompositionMode.Override,
                        priority = 2
                    }
                });

                AssertVector(new Vector3(0.5f, 0f, 0.2f),
                    provider.Sample(Vector3.zero));
                AssertVector(new Vector3(-0.3f, 0f, 1.1f),
                    provider.Sample(new Vector3(30f, 0f, 0f)));
                AssertVector(new Vector3(0f, 0f, 0.2f),
                    provider.Sample(new Vector3(100f, 0f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void SimulationTimeController_SupportsOneTwoAndFourTimes()
        {
            GameObject gameObject = new GameObject("Simulation Time");
            try
            {
                SimulationTimeController controller =
                    gameObject.AddComponent<SimulationTimeController>();

                controller.SetScale(2f);
                Assert.That(Time.timeScale, Is.EqualTo(2f));
                controller.SetScale(4f);
                Assert.That(controller.DisplayText, Is.EqualTo("4x"));
                controller.Cycle();
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Time.timeScale = 1f;
            }
        }

        [Test]
        public void DayNightController_CreatesLongRangeLeadingMarkLightsAtBoardHeight()
        {
            GameObject existingNavigation = GameObject.Find("Navigation");
            if (existingNavigation != null)
                existingNavigation.name = "Existing Navigation";
            GameObject navigation = new GameObject("Navigation");
            GameObject mark = new GameObject("Test Rear Mark");
            mark.transform.SetParent(navigation.transform, false);
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(mark.transform, false);
            board.transform.localPosition = Vector3.up * 18f;
            GameObject controllerObject = new GameObject("Day Night");

            try
            {
                DayNightController controller =
                    controllerObject.AddComponent<DayNightController>();
                controller.Apply(true);

                Transform lightTransform = mark.transform.Find(
                    "Leading Mark Night Light");
                Assert.That(lightTransform, Is.Not.Null);
                Assert.That(lightTransform.localPosition.y,
                    Is.EqualTo(18.5f).Within(0.01f));
                Light light = lightTransform.GetComponent<Light>();
                Assert.That(light.range, Is.GreaterThanOrEqualTo(400f));
                Assert.That(light.enabled, Is.True);
                Assert.That(lightTransform.Find("Beacon Lens"), Is.Not.Null);
                Renderer glowBoard = mark.transform.Find("Night Board Glow")
                    .GetComponent<Renderer>();
                Renderer stripe = mark.transform.Find("Night Alignment Stripe")
                    .GetComponent<Renderer>();
                Assert.That(glowBoard.enabled, Is.True);
                Assert.That(stripe.enabled, Is.True);
                Assert.That(glowBoard.transform.localScale.x,
                    Is.GreaterThan(board.transform.localScale.x));
            }
            finally
            {
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(navigation);
                if (existingNavigation != null)
                    existingNavigation.name = "Navigation";
            }
        }

        [Test]
        public void WeatherController_CalculatesConfiguredWindDirectionAndSpeed()
        {
            Vector3 westWind = WeatherController.CalculateWindVelocity(270f, 8f);
            Vector3 northWind = WeatherController.CalculateWindVelocity(0f, 12f);

            Assert.That(westWind.x, Is.EqualTo(-8f).Within(0.001f));
            Assert.That(westWind.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(northWind.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(northWind.z, Is.EqualTo(12f).Within(0.001f));
        }

        private static FairwayRouteSample RouteSample(Vector3 position)
        {
            return new FairwayRouteSample
            {
                position = position,
                leftWidthM = 30f,
                rightWidthM = 50f,
                centerDepthM = 5f,
                leftEdgeDepthM = 2.2f,
                rightEdgeDepthM = 3.2f,
                speedLimitMps = 3f
            };
        }

        private static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }
    }
}
