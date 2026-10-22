using System.IO;
using System.Linq;
using NUnit.Framework;
using ShipSimulator.Editor;
using ShipSimulator.Persistence;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class VesselMechanismTests
    {
        private GameObject vessel;
        private VesselMechanisms rig;
        [SetUp] public void SetUp()
        {
            vessel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VolgoDonMechanismIntegration.PrefabPath));
            rig = vessel.GetComponent<VesselMechanisms>();
            Assert.That(rig, Is.Not.Null);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(vessel);

        [Test]
        public void ShaftMotion_UsesIndependentSignedRpm_AndSteeringUsesActualAngle()
        {
            Transform port = Find("Propeller_Port"), starboard = Find("Propeller_Starboard");
            Quaternion p = port.rotation, s = starboard.rotation;
            rig.Advance(.1f, 60, 0, 20);
            Assert.That(Quaternion.Angle(p, port.rotation), Is.EqualTo(36).Within(.01));
            Assert.That(Quaternion.Angle(s, starboard.rotation), Is.LessThan(.01));
            Transform nozzle = Find("Nozzle_Port");
            Quaternion turn = nozzle.rotation;
            rig.Advance(.1f, -60, 0, 0);
            Assert.That(Quaternion.Angle(p, port.rotation), Is.LessThan(.01));
            Assert.That(Quaternion.Angle(turn, nozzle.rotation), Is.EqualTo(20).Within(.01));
        }

        [Test]
        public void HatchCommand_StartsLiftingWithoutTheReviewClipIdleLeadIn()
        {
            float y = Find("Hatch_AftOuter").position.y;
            rig.SetHatches(HatchPosition.HalfOpen);
            rig.Advance(1, 0, 0, 0);
            Assert.That(Find("Hatch_AftOuter").position.y, Is.GreaterThan(y + .01f));
        }

        [Test]
        public void SwitchingHolds_ReturnsThroughHalfOpenStack_AndNeverMovesRoot()
        {
            Vector3 position = vessel.transform.position;
            Quaternion rotation = vessel.transform.rotation;
            Reach(HatchPosition.ForwardOpen);
            rig.SetHatches(HatchPosition.AftOpen);
            rig.Advance(1, 0, 0, 0);
            Assert.That(rig.Parking, Is.LessThan(0));
            Assert.That(rig.Opening, Is.EqualTo(1));
            Reach(HatchPosition.AftOpen);
            Reach(HatchPosition.Closed);
            Assert.That(rig.Opening, Is.Zero);
            Assert.That(rig.Parking, Is.Zero);
            Assert.That(vessel.transform.position, Is.EqualTo(position));
            Assert.That(vessel.transform.rotation, Is.EqualTo(rotation));
        }

        [Test]
        public void SaveRestore_ContinuesMidTransition_AndOldSaveStowsEverything()
        {
            rig.SetHatches(HatchPosition.ForwardOpen);
            rig.SetAnchorsReleased(true);
            rig.Advance(12, 10, -20, 0);
            MechanismSave save = rig.CaptureState();
            Vector3 panel = Find("Hatch_AftOuter").position;
            rig.Advance(5, 0, 0, 0);
            rig.RestoreState(JsonUtility.FromJson<MechanismSave>(JsonUtility.ToJson(save)));
            Assert.That(Vector3.Distance(panel, Find("Hatch_AftOuter").position), Is.LessThan(.001));
            Assert.That(rig.Target, Is.EqualTo(HatchPosition.ForwardOpen));
            Assert.That(rig.AnchorsReleased, Is.True);
            rig.RestoreState(null);
            Assert.That(rig.Opening, Is.Zero);
            Assert.That(rig.AnchorRelease, Is.Zero);
        }

        [Test]
        public void NavigationLights_UseLocalSpill_AndKeepSectorCookies()
        {
            var lights = vessel.GetComponent<NavigationLightRig>() ?? vessel.AddComponent<NavigationLightRig>();
            lights.EnsureCreated();
            var layout = vessel.GetComponent<VesselLayout>();
            Assert.That(layout.AftMastheadLight.y - layout.ForwardMastheadLight.y, Is.GreaterThanOrEqualTo(3));
            lights.SetNight(true);
            foreach (Light light in vessel.GetComponentsInChildren<Light>())
            {
                Assert.That(light.enabled, Is.True);
                Assert.That(light.range, Is.LessThan(10));
                Assert.That(light.intensity, Is.LessThan(1));
                Assert.That(light.cookie, Is.Not.Null);
            }
            lights.SetNight(false);
            Assert.That(vessel.GetComponentsInChildren<Light>().All(l => !l.enabled), Is.True);
        }

        [Test]
        public void InvalidMachinerySave_IsRejected()
        {
            Assert.Throws<InvalidDataException>(() => new MechanismSave { opening = float.NaN }.Validate());
            Assert.Throws<InvalidDataException>(() => new MechanismSave { parking = .5f }.Validate());
            Assert.Throws<InvalidDataException>(() => new MechanismSave { hatchTarget = 4 }.Validate());
        }

        [Test]
        public void FilteredClips_CannotAnimateRootOrPropulsion()
        {
            foreach (string name in new[] { "Hatches", "Anchors" })
                foreach (var curve in AnimationUtility.GetCurveBindings(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    VolgoDonMechanismIntegration.Folder + "/" + name + ".anim")))
                {
                    Assert.That(curve.path, Is.Not.Empty);
                    Assert.That(curve.path, Does.Not.Contain("Propeller"));
                    Assert.That(curve.path, Does.Not.Contain("Nozzle"));
                }
        }

        private Transform Find(string name) => vessel.GetComponentsInChildren<Transform>().Single(t => t.name == name);
        private void Reach(HatchPosition target)
        {
            rig.SetHatches(target);
            for (int i = 0; i < 240; i++) rig.Advance(1, 0, 0, 0);
            Assert.That(rig.IsMoving, Is.False);
        }
    }
}
