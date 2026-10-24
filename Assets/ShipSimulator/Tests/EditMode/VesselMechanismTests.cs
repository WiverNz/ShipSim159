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
            rig.Advance(5, 0, 0, 0);
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
            rig.Advance(12, 10, -20, 0);
            MechanismSave save = rig.CaptureState();
            Vector3 panel = Find("Hatch_AftOuter").position;
            rig.Advance(5, 0, 0, 0);
            rig.RestoreState(JsonUtility.FromJson<MechanismSave>(JsonUtility.ToJson(save)));
            Assert.That(Vector3.Distance(panel, Find("Hatch_AftOuter").position), Is.LessThan(.001));
            Assert.That(rig.Target, Is.EqualTo(HatchPosition.ForwardOpen));
            Assert.That(save.LegacyChainM(), Is.Null, "Anchor state is saved by GroundTackle, not here.");
            rig.RestoreState(null);
            Assert.That(rig.Opening, Is.Zero);
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

        // Archive p4 hydraulics and winch, see mechanisms.json operating_rates: outer lift 141 s, inner lift 34 s,
        // slide 174 s, lowering 16 s, then 371 s to move a stack over the other hold.
        [Test]
        public void HatchPhases_TakeTheirRealTimeDurationsInOrder()
        {
            MachineryRates r = rig.Rates;
            float open = r.outerLiftSeconds + r.innerLiftSeconds + r.innerSlideSeconds + r.outerLowerSeconds;
            Assert.That(open, Is.EqualTo(365).Within(1), "Rates come from mechanisms.json, not the review clip.");
            rig.SetHatches(HatchPosition.HalfOpen);
            Assert.That(rig.HatchSecondsRemaining, Is.EqualTo(open).Within(.01f));
            rig.Advance(r.outerLiftSeconds - 1, 0, 0, 0);
            Assert.That(rig.Phase, Is.EqualTo(HatchPhase.LiftingOuter));
            rig.Advance(2, 0, 0, 0);
            Assert.That(rig.Phase, Is.EqualTo(HatchPhase.LiftingInner));
            rig.Advance(r.innerLiftSeconds, 0, 0, 0);
            Assert.That(rig.Phase, Is.EqualTo(HatchPhase.SlidingInner));
            rig.Advance(r.innerSlideSeconds, 0, 0, 0);
            Assert.That(rig.Phase, Is.EqualTo(HatchPhase.LoweringOuter));
            rig.Advance(r.outerLowerSeconds - 2, 0, 0, 0);
            Assert.That(rig.HatchesMoving, Is.True);
            rig.Advance(1.01f, 0, 0, 0);
            Assert.That(rig.Phase, Is.EqualTo(HatchPhase.Stacked));
            Assert.That(rig.HatchesMoving, Is.False);
            rig.SetHatches(HatchPosition.ForwardOpen);
            Assert.That(rig.HatchSecondsRemaining, Is.EqualTo(r.traverseSeconds).Within(.01f));
            rig.Advance(r.traverseSeconds * .5f, 0, 0, 0);
            Assert.That(rig.Phase, Is.EqualTo(HatchPhase.Traversing));
            rig.Advance(r.traverseSeconds, 0, 0, 0);
            Assert.That(rig.Phase, Is.EqualTo(HatchPhase.HoldOpen));
        }

        [Test]
        public void OneLongStep_LandsWhereManyShortStepsDo()
        {
            rig.SetHatches(HatchPosition.ForwardOpen);
            rig.Advance(420, 0, 0, 0);
            float opening = rig.Opening, parking = rig.Parking;
            Vector3 panel = Find("Hatch_ForeOuter").position;
            rig.RestoreState(null);
            rig.SetHatches(HatchPosition.ForwardOpen);
            for (int i = 0; i < 21000; i++) rig.Advance(.02f, 0, 0, 0);
            Assert.That(rig.Opening, Is.EqualTo(opening).Within(1e-4f));
            Assert.That(rig.Parking, Is.EqualTo(parking).Within(1e-3f));
            Assert.That(Vector3.Distance(panel, Find("Hatch_ForeOuter").position), Is.LessThan(.01f));
        }

        [Test]
        public void InterruptedOpening_ReversesAlongTheSamePathAndTakesTheTimeItRan()
        {
            rig.SetHatches(HatchPosition.HalfOpen);
            rig.Advance(250, 0, 0, 0);
            Assert.That(rig.Phase, Is.EqualTo(HatchPhase.SlidingInner));
            Vector3 mid = Find("Hatch_AftInner").position;
            rig.SetHatches(HatchPosition.Closed);
            Assert.That(rig.HatchSecondsRemaining, Is.EqualTo(250).Within(.01f));
            rig.SetHatches(HatchPosition.Closed);
            rig.Advance(249, 0, 0, 0);
            Assert.That(rig.HatchesMoving, Is.True);
            rig.Advance(1.01f, 0, 0, 0);
            Assert.That(rig.Phase, Is.EqualTo(HatchPhase.Closed));
            rig.SetHatches(HatchPosition.HalfOpen);
            rig.Advance(250, 0, 0, 0);
            Assert.That(Vector3.Distance(mid, Find("Hatch_AftInner").position), Is.LessThan(.001f));
            rig.Advance(10000, 0, 0, 0);
            Assert.That(rig.Opening, Is.EqualTo(1), "The end position is a limit, not overshot.");
        }

        // Part 13 saves kept the chain paid out here; earlier ones only the unseat fraction. GroundTackle restores both.
        [Test]
        public void LegacyAnchorSaves_GiveTheirChainToGroundTackle()
        {
            var part13 = JsonUtility.FromJson<MechanismSave>("{\"anchorTarget\":true,\"chainOutM\":[12.5,0,3]}");
            Assert.That(part13.LegacyChainM(), Is.EqualTo(new[] { 12.5f, 0f, 3f }));
            var older = JsonUtility.FromJson<MechanismSave>("{\"anchorTarget\":true,\"anchorRelease\":0.5}");
            Assert.That(older.LegacyChainM()[0], Is.EqualTo(.175f).Within(1e-4f));
            rig.RestoreState(part13);
            Assert.That(rig.CaptureState().LegacyChainM(), Is.Null, "Restoring hands the chain over and keeps no copy.");
            Assert.Throws<InvalidDataException>(() => new MechanismSave { chainOutM = new[] { 1f, -1f, 0f } }.Validate());
        }

        [Test]
        public void Prefab_HasGroundTackleAtTheModelHawsesAndBollards()
        {
            var tackle = vessel.GetComponent<ShipSimulator.Physics.GroundTackle>();
            Assert.That(tackle, Is.Not.Null);
            Assert.That(vessel.GetComponent<GroundTackleVisual>(), Is.Not.Null);
            Vector3[] hawses = rig.HawsesLocal;
            Assert.That(hawses.Length, Is.EqualTo(3));
            Assert.That(hawses[0].x, Is.LessThan(-1), "Port hawse to port.");
            Assert.That(hawses[1].x, Is.GreaterThan(1));
            Assert.That(hawses[0].z, Is.GreaterThan(60).And.LessThan(70));
            Assert.That(hawses[2].z, Is.LessThan(-60));
            var points = vessel.GetComponentsInChildren<ShipSimulator.Physics.MooringPoint>();
            Assert.That(points.Length, Is.EqualTo(10));
            var data = JsonUtility.FromJson<ShipSimulator.Physics.VesselData>(File.ReadAllText(
                "Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json"));
            foreach (var point in points)
            {
                Assert.That(point.Kind, Is.EqualTo(ShipSimulator.Physics.MooringPointKind.Ship));
                Assert.That(data.groundTackle.stations.Any(s => s.id == point.Id), Is.True, point.Id);
                Vector3 local = vessel.transform.InverseTransformPoint(point.LeadWorld);
                Assert.That(Mathf.Abs(local.x), Is.GreaterThan(Mathf.Abs(vessel.transform.InverseTransformPoint(point.transform.position).x) - .01f),
                    point.Id + " leads outboard of its bollard.");
                Assert.That(Mathf.Abs(local.x), Is.LessThan(8.4f), point.Id + " lead within the beam.");
                Assert.That(local.y, Is.GreaterThan(1.9f), point.Id + " lead above the deck at side.");
            }
        }

        [Test]
        public void PreviewMultiplier_IsNotSavedAndDoesNotChangeExplicitSteps()
        {
            rig.PreviewSpeedMultiplier = 20;
            rig.SetHatches(HatchPosition.HalfOpen);
            rig.Advance(10, 0, 0, 0);
            Assert.That(rig.HatchSecondsRemaining, Is.EqualTo(355).Within(.5f));
            Assert.That(JsonUtility.ToJson(rig.CaptureState()), Does.Not.Contain("Preview"));
        }

        private Transform Find(string name) => vessel.GetComponentsInChildren<Transform>().Single(t => t.name == name);
        private void Reach(HatchPosition target)
        {
            rig.SetHatches(target);
            for (int i = 0; i < 1000; i++) rig.Advance(1, 0, 0, 0);
            Assert.That(rig.IsMoving, Is.False);
        }
    }
}
