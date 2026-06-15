using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.CameraSystem;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class VesselDataValidationTests
    {
        private const string VesselJsonPath =
            "Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json";

        [Test]
        public void ProjectVesselJson_IsValid()
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(VesselJsonPath);
            Assert.That(json, Is.Not.Null);

            VesselData data = JsonUtility.FromJson<VesselData>(json.text);

            Assert.That(VesselDataValidator.TryValidate(data, out string error), Is.True, error);
        }

        [Test]
        public void Validator_RejectsDraftGreaterThanDepth()
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(VesselJsonPath);
            VesselData data = JsonUtility.FromJson<VesselData>(json.text);
            data.dimensions.loadedDraftM = data.dimensions.depthMouldedM;

            bool valid = VesselDataValidator.TryValidate(data, out string error);

            Assert.That(valid, Is.False);
            StringAssert.Contains("draft", error.ToLowerInvariant());
        }

        [Test]
        public void Validator_RejectsMismatchedPropellerPositions()
        {
            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(VesselJsonPath);
            VesselData data = JsonUtility.FromJson<VesselData>(json.text);
            data.propeller.lateralPositionsM = new[] { 0f };

            bool valid = VesselDataValidator.TryValidate(data, out string error);

            Assert.That(valid, Is.False);
            StringAssert.Contains("propeller", error.ToLowerInvariant());
        }

        [Test]
        public void TrainingScene_IsSecondAndEnabledInBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            Assert.That(scenes, Has.Length.GreaterThanOrEqualTo(2));
            Assert.That(
                scenes[1].path,
                Is.EqualTo("Assets/ShipSimulator/Scenes/RiverTrainingScene.unity"));
            Assert.That(scenes[1].enabled, Is.True);
        }

        [Test]
        public void TrainingScene_HasOperationalHudAndCamera()
        {
            var scene = EditorSceneManager.OpenScene(
                "Assets/ShipSimulator/Scenes/RiverTrainingScene.unity",
                OpenSceneMode.Single);

            ShipTelemetryUI hud = Object.FindFirstObjectByType<ShipTelemetryUI>();
            ShipPhysicsController ship = Object.FindFirstObjectByType<ShipPhysicsController>();
            ShipFollowCamera camera = Object.FindFirstObjectByType<ShipFollowCamera>();

            Assert.That(scene.IsValid(), Is.True);
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.isActiveAndEnabled, Is.True);
            Assert.That(ship, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
        }

        [Test]
        public void FairwayDepth_FollowsCurvedCenterlineAndShallowsAtBank()
        {
            const float z = 520f;
            float center = FairwayModel.CenterX(z);
            float centerDepth = FairwayModel.DepthAt(center, z);
            float bankDepth = FairwayModel.DepthAt(
                center + FairwayModel.ShoreDistance(z), z);

            Assert.That(centerDepth, Is.EqualTo(FairwayModel.DeepWaterDepthM).Within(0.01f));
            Assert.That(bankDepth, Is.EqualTo(FairwayModel.ShoreDepthM).Within(0.01f));
            Assert.That(FairwayModel.DepthAt(0f, z), Is.LessThan(centerDepth));
        }

        [Test]
        public void VesselPrefab_UsesCompoundCollisionHull()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab");

            Transform collisionHull = prefab.transform.Find("CollisionHull");
            BoxCollider[] sections = collisionHull.GetComponentsInChildren<BoxCollider>();

            Assert.That(collisionHull, Is.Not.Null);
            Assert.That(sections, Has.Length.EqualTo(3));
            Assert.That(collisionHull.Find("BowCollision"), Is.Not.Null);
            Assert.That(collisionHull.Find("MidshipCollision"), Is.Not.Null);
            Assert.That(collisionHull.Find("SternCollision"), Is.Not.Null);
        }

        [Test]
        public void NavigationLightRig_CreatesAndTogglesRequiredShipLights()
        {
            GameObject vessel = new GameObject("Navigation Light Test Vessel");

            try
            {
                NavigationLightRig rig = vessel.AddComponent<NavigationLightRig>();
                rig.EnsureCreated();
                Light[] lights = vessel.GetComponentsInChildren<Light>(true);

                Assert.That(lights, Has.Length.EqualTo(6));
                Assert.That(lights, Has.All.Matches<Light>(light => !light.enabled));
                Assert.That(vessel.transform.Find(
                    "Bow Navigation Light Fixture/Support"), Is.Not.Null);
                Assert.That(vessel.transform.Find(
                    "Forward Masthead Light Fixture/Lantern Housing"), Is.Not.Null);

                rig.SetNight(true);

                Assert.That(lights, Has.All.Matches<Light>(light => light.enabled));
            }
            finally
            {
                Object.DestroyImmediate(vessel);
            }
        }

        [Test]
        public void NavigationBeaconFlasher_UsesShortNightFlash()
        {
            GameObject beacon = new GameObject("Beacon");
            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lens.transform.SetParent(beacon.transform, false);

            try
            {
                Light light = beacon.AddComponent<Light>();
                NavigationBeaconFlasher flasher =
                    beacon.AddComponent<NavigationBeaconFlasher>();
                flasher.Configure(
                    light, lens.GetComponent<Renderer>(), 1.5f, 0.32f, 0.2f);

                Assert.That(flasher.PeriodSeconds, Is.EqualTo(1.5f).Within(0.001f));
                Assert.That(
                    flasher.FlashDurationSeconds, Is.EqualTo(0.32f).Within(0.001f));
                Assert.That(flasher.EvaluateLit(0f), Is.False);

                flasher.SetNight(true);

                Assert.That(flasher.EvaluateLit(0f), Is.True);
                Assert.That(flasher.EvaluateLit(0.2f), Is.False);
                Assert.That(flasher.EvaluateLit(1.3f), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(beacon);
            }
        }

        [Test]
        public void HudStatusText_UsesReadableObjectiveAndCompactCameraLayout()
        {
            string objective = ShipTelemetryUI.FormatObjectiveStatus(770f);
            string camera = ShipTelemetryUI.FormatCameraStatus("NAVIGATOR", 8, 9);

            Assert.That(objective, Does.Contain("Speed limit"));
            Assert.That(objective, Does.Contain("<b>8 km/h</b>"));
            Assert.That(objective, Does.Not.Contain("&lt;="));
            Assert.That(camera, Does.StartWith("CAMERA:"));
            Assert.That(camera, Does.Contain("NAVIGATOR"));
            Assert.That(camera, Does.Contain("9/9"));
            Assert.That(camera, Does.Not.Contain("\n"));
        }
    }
}
