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
            rig.SetAnchorsReleased(true);
            rig.Advance(420, 0, 0, 0);
            float opening = rig.Opening, parking = rig.Parking, chain = rig.ChainOutM(0);
            Vector3 panel = Find("Hatch_ForeOuter").position;
            rig.RestoreState(null);
            rig.SetHatches(HatchPosition.ForwardOpen);
            rig.SetAnchorsReleased(true);
            for (int i = 0; i < 21000; i++) rig.Advance(.02f, 0, 0, 0);
            Assert.That(rig.Opening, Is.EqualTo(opening).Within(1e-4f));
            Assert.That(rig.Parking, Is.EqualTo(parking).Within(1e-3f));
            Assert.That(rig.ChainOutM(0), Is.EqualTo(chain).Within(1e-2f));
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

        // Archive p8: bow chains 150 m, stern 75 m; windlass 10 m/min, stern capstan 18 m/min.
        [Test]
        public void Anchors_PayOutToTheChainLimitsAndHeaveUpAtTheArchiveRates()
        {
            MachineryRates r = rig.Rates;
            rig.SetAnchorsReleased(true);
            rig.Advance(20, 0, 0, 0);
            Assert.That(rig.ChainOutM(0), Is.EqualTo(20 * r.letGoMetresPerSecond).Within(1e-3f));
            Transform gypsy = Find("Windlass_Port");
            Quaternion before = gypsy.localRotation;
            rig.Advance(r.chainPerGypsyTurnM * .25f / r.letGoMetresPerSecond, 0, 0, 0);
            Assert.That(Quaternion.Angle(before, gypsy.localRotation), Is.EqualTo(90).Within(.5f), "A quarter turn per quarter of the chain per turn.");
            rig.Advance(1000, 0, 0, 0);
            Assert.That(rig.ChainOutM(0), Is.EqualTo(150));
            Assert.That(rig.ChainOutM(2), Is.EqualTo(75));
            Assert.That(rig.AnchorsMoving, Is.False);
            float anchorY = Find("Anchor_Port").position.y;
            Assert.That(anchorY, Is.LessThan(-140), "The anchor hangs on its chain below the hawse.");
            rig.SetAnchorsReleased(false);
            rig.Advance(60, 0, 0, 0);
            Assert.That(rig.ChainOutM(0), Is.EqualTo(150 - 10).Within(1e-2f));
            Assert.That(rig.ChainOutM(2), Is.EqualTo(75 - 18).Within(1e-2f));
            rig.SetAnchorsReleased(true);
            rig.SetAnchorsReleased(false);
            rig.Advance(2000, 0, 0, 0);
            Assert.That(rig.ChainOutM(0), Is.Zero);
            Assert.That(rig.AnchorRelease, Is.Zero, "Fully hove in is stowed in the hawse.");
        }

        [Test]
        public void SaveRestore_MidPayout_AndOldSaveDerivesChainFromUnseat()
        {
            rig.SetAnchorsReleased(true);
            rig.Advance(33, 0, 0, 0);
            MechanismSave save = rig.CaptureState();
            Vector3 anchor = Find("Anchor_Starboard").position;
            rig.Advance(40, 0, 0, 0);
            rig.RestoreState(JsonUtility.FromJson<MechanismSave>(JsonUtility.ToJson(save)));
            Assert.That(rig.ChainOutM(1), Is.EqualTo(33).Within(1e-3f));
            Assert.That(Vector3.Distance(anchor, Find("Anchor_Starboard").position), Is.LessThan(.001f));
            var old = JsonUtility.FromJson<MechanismSave>("{\"anchorTarget\":true,\"anchorRelease\":0.5}");
            rig.RestoreState(old);
            Assert.That(rig.ChainOutM(0), Is.EqualTo(.175f).Within(1e-4f));
            Assert.Throws<InvalidDataException>(() => new MechanismSave { chainOutM = new[] { 1f, -1f, 0f } }.Validate());
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
