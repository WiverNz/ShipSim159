using NUnit.Framework;
using ShipSimulator.CameraSystem;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShipSimulator.Tests
{
    public sealed class CameraOrbitTests
    {
        private static CameraOrbit NewOrbit()
        {
            var orbit = new CameraOrbit();
            orbit.Configure(10, 140, 0, 85);
            orbit.Reset(0, 30, 80);
            return orbit;
        }

        [Test]
        public void WheelNormalization_PreservesFractionalTicksWithoutDoubleScaling()
        {
            Assert.That(CameraOrbit.WheelTicks(1, InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms,
                RuntimePlatform.WindowsPlayer), Is.EqualTo(1));
            Assert.That(CameraOrbit.WheelTicks(120, InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange,
                RuntimePlatform.WindowsPlayer), Is.EqualTo(1));
            Assert.That(CameraOrbit.WheelTicks(15, InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange,
                RuntimePlatform.WindowsPlayer), Is.EqualTo(.125f));
            Assert.That(CameraOrbit.WheelTicks(.125f, InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms,
                RuntimePlatform.WindowsPlayer), Is.EqualTo(.125f));
            Assert.That(CameraOrbit.WheelTicks(1, InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange,
                RuntimePlatform.OSXPlayer), Is.EqualTo(1));
        }

        [Test]
        public void Zoom_IsProportionalReversibleAndBounded()
        {
            var orbit = NewOrbit();
            orbit.AddInput(Vector2.zero, 1, .06f, Mathf.Log(1.25f));
            Assert.That(orbit.TargetDistance, Is.EqualTo(64).Within(.001f));
            orbit.AddInput(Vector2.zero, -1, .06f, Mathf.Log(1.25f));
            Assert.That(orbit.TargetDistance, Is.EqualTo(80).Within(.001f));
            for (int i = 0; i < 8; i++) orbit.AddInput(Vector2.zero, .125f, .06f, Mathf.Log(1.25f));
            Assert.That(orbit.TargetDistance, Is.EqualTo(64).Within(.001f));
            orbit.AddInput(Vector2.zero, 10000, .06f, .22314355f);
            orbit.Step(10, .085f);
            Assert.That(orbit.Distance, Is.EqualTo(10).Within(.001f));
            orbit.AddInput(Vector2.zero, -10000, .06f, .22314355f);
            orbit.Step(10, .085f);
            Assert.That(orbit.Distance, Is.EqualTo(140).Within(.001f));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void ResponseUsesElapsedTimeAndPixelGainIsFrameIndependent(int fps)
        {
            var orbit = NewOrbit();
            for (int i = 0; i < fps; i++) orbit.AddInput(new Vector2(100f / fps, -50f / fps), 1f / fps, .06f, .22314355f);
            Assert.That(orbit.TargetYaw, Is.EqualTo(6).Within(.003f));
            Assert.That(orbit.TargetPitch, Is.EqualTo(33).Within(.003f));
            Assert.That(orbit.TargetDistance, Is.EqualTo(64).Within(.003f));
            var reference = NewOrbit();
            reference.AddInput(new Vector2(100, -50), 1, .06f, .22314355f);
            reference.Step(.5f, .085f);
            for (int i = 0; i < fps / 2; i++) orbit.Step(1f / fps, .085f);
            Assert.That(orbit.Yaw, Is.EqualTo(reference.Yaw).Within(.003f));
            Assert.That(orbit.Pitch, Is.EqualTo(reference.Pitch).Within(.003f));
            Assert.That(orbit.Distance, Is.EqualTo(reference.Distance).Within(.003f));
        }

        [Test]
        public void HorizontalOrbitKeepsHeightAndTakesShortestPathAcrossYawWrap()
        {
            var orbit = NewOrbit();
            orbit.Reset(179, 30, 80);
            float height = orbit.Offset.y;
            orbit.AddInput(new Vector2(100, 0), 0, .06f, .22314355f);
            orbit.Step(.04f, .085f);
            Assert.That(Mathf.DeltaAngle(179, orbit.Yaw), Is.InRange(0f, 6f));
            Assert.That(orbit.Offset.y, Is.EqualTo(height).Within(.001f));
            orbit.AddInput(new Vector2(0, -10000), 0, .06f, .22314355f);
            Assert.That(orbit.TargetPitch, Is.EqualTo(85));
            orbit.AddInput(new Vector2(0, 10000), 0, .06f, .22314355f);
            Assert.That(orbit.TargetPitch, Is.Zero);
            orbit.CancelMotion();
            var previous = orbit.Offset;
            orbit.Step(10, .085f);
            Assert.That(Vector3.Distance(previous, orbit.Offset), Is.LessThan(.001f));
        }

        [Test]
        public void InvalidInputCannotPoisonTheCamera()
        {
            var orbit = NewOrbit();
            orbit.AddInput(new Vector2(float.NaN, float.PositiveInfinity), float.NaN, .06f, .22314355f);
            orbit.Step(float.NaN, .085f);
            Assert.That(Vector3.Distance(orbit.Offset, CameraOrbit.ToOffset(0, 30, 80)), Is.LessThan(.001f));
        }

        [Test]
        public void SaveValidationAcceptsLegacyAndCurrentOrbitButRejectsUnknownCoordinates()
        {
            var save = new VoyageSave { scene = "RiverTrainingScene", savedUtc = System.DateTime.UtcNow.ToString("O") };
            save.camera.orbitVersion = 0;
            Assert.DoesNotThrow(save.Validate);
            save.camera.orbitVersion = 1;
            Assert.DoesNotThrow(save.Validate);
            save.camera.orbitVersion = 2;
            Assert.Throws<System.IO.InvalidDataException>(save.Validate);
        }

        [TestCase("volgodon-507b")] [TestCase("volgoneft-1577")] [TestCase("volgobalt-295ar")]
        [TestCase("meteor-342u")] [TestCase("luch-14352")] [TestCase("serpukhov-r96")] [TestCase("serpukhov-mn52")]
        public void EveryVesselSupportsNineStableViewsAndSaveRoundTrips(string id)
        {
            var vessel = VesselCatalogue.Load().Find(id);
            Assert.That(vessel, Is.Not.Null, id);
            var go = new GameObject("Camera catalogue regression", typeof(Camera));
            try
            {
                var follow = go.AddComponent<ShipFollowCamera>();
                var ship = vessel.prefab.GetComponent<ShipPhysicsController>();
                var layout = ship.GetComponent<VesselLayout>();
                follow.SetTarget(ship);
                Assert.That(follow.ViewCount, Is.EqualTo(9));
                for (int view = 0; view < 9; view++)
                {
                    follow.SetView(view);
                    Vector3 position = go.transform.position;
                    Quaternion rotation = go.transform.rotation;
                    follow.Advance(1);
                    Assert.That(Vector3.Distance(position, go.transform.position), Is.LessThan(.001f), id + ":" + view);
                    var save = JsonUtility.FromJson<CameraSave>(JsonUtility.ToJson(follow.CaptureState()));
                    Assert.That(save.orbitVersion, Is.EqualTo(1));
                    Assert.That(save.pitch, Is.InRange(0f, 90f));
                    Assert.That(save.distance, Is.GreaterThan(0));
                    follow.SetView((view + 1) % 9);
                    follow.RestoreState(save);
                    Assert.That(Vector3.Distance(position, go.transform.position), Is.LessThan(.002f), id + ":" + view);
                    Assert.That(Quaternion.Angle(rotation, go.transform.rotation), Is.LessThan(.05f));
                    if (view == 8)
                        Assert.That(Vector3.Distance(position, ship.transform.TransformPoint(layout.NavigatorEye)), Is.LessThan(.001f));
                    else if (layout.CameraViews.Length == 8)
                        Assert.That(Vector3.Distance(position, ship.transform.TransformPoint(layout.CameraViews[view])), Is.LessThan(.01f),
                            "Do not silently raise or displace an authored view: " + id + ":" + view);
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

    }
}
