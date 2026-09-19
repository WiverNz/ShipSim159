using NUnit.Framework;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Tests
{
    // Air cushion and hydrofoil craft: the hull is unloaded as they gather way.
    public sealed class SupportModelTests
    {
        [TestCase(VesselFixtures.MeteorFile)]
        [TestCase(VesselFixtures.LuchFile)]
        public void Support_BuildsBetweenTakeoffAndFullSupportSpeed(string file)
        {
            VesselSupport support = VesselFixtures.Load(file).support;

            Assert.That(SupportModel.Lifts(support), Is.True);
            Assert.That(SupportModel.Fraction(support, 0f), Is.EqualTo(0f));
            Assert.That(SupportModel.Fraction(support, support.takeoffSpeedMps), Is.EqualTo(0f).Within(1e-4f));
            float half = SupportModel.Fraction(support, 0.5f * (support.takeoffSpeedMps + support.fullSupportSpeedMps));
            Assert.That(half, Is.GreaterThan(0f).And.LessThan(support.supportedWeightFraction));
            Assert.That(SupportModel.Fraction(support, support.fullSupportSpeedMps),
                Is.EqualTo(support.supportedWeightFraction).Within(1e-4f));
            Assert.That(SupportModel.Fraction(support, 3f * support.fullSupportSpeedMps),
                Is.EqualTo(support.supportedWeightFraction).Within(1e-4f));
        }

        [TestCase(VesselFixtures.MeteorFile)]
        [TestCase(VesselFixtures.LuchFile)]
        public void Resistance_PeaksAtTheHumpAndFallsOnceSupported(string file)
        {
            VesselSupport support = VesselFixtures.Load(file).support;

            Assert.That(SupportModel.ResistanceFactor(support, 0f), Is.EqualTo(1f));
            float middle = 0.5f * (support.takeoffSpeedMps + support.fullSupportSpeedMps);
            float hump = SupportModel.ResistanceFactor(support, middle);
            // Without the hump the factor would slide straight from 1 to the supported value.
            float withoutHump = Mathf.Lerp(1f, support.supportedResistanceFactor, 0.5f);
            Assert.That(hump, Is.GreaterThan(withoutHump), "Drag peaks while the hull is still wet.");
            Assert.That(hump, Is.GreaterThan(SupportModel.ResistanceFactor(support, support.fullSupportSpeedMps)));
            Assert.That(SupportModel.ResistanceFactor(support, support.fullSupportSpeedMps),
                Is.EqualTo(support.supportedResistanceFactor).Within(1e-4f));
        }

        [TestCase(VesselFixtures.MeteorFile)]
        [TestCase(VesselFixtures.LuchFile)]
        public void SupportedDraft_ReportsTheFoilsOrSkegsOnceUp(string file)
        {
            VesselSupport support = VesselFixtures.Load(file).support;

            Assert.That(SupportModel.SupportedDraftM(support, 0f), Is.EqualTo(0f));
            Assert.That(SupportModel.SupportedDraftM(support, support.fullSupportSpeedMps),
                Is.EqualTo(support.supportedDraftM));
        }

        [Test]
        public void DisplacementVessel_HasNoSupport()
        {
            VesselData data = VesselFixtures.Load(VesselFixtures.VolgoDonFile);

            Assert.That(SupportModel.Lifts(data.support), Is.False);
            Assert.That(SupportModel.Fraction(data.support, 10f), Is.EqualTo(0f));
            Assert.That(SupportModel.ResistanceFactor(data.support, 10f), Is.EqualTo(1f));
            Assert.That(SupportModel.Immersion(data.support, 10f), Is.EqualTo(1f));
        }

        [Test]
        public void Validator_RejectsSupportThatNeverReachesFullLift()
        {
            VesselData data = VesselFixtures.Load(VesselFixtures.MeteorFile);
            data.support.fullSupportSpeedMps = data.support.takeoffSpeedMps;

            Assert.That(VesselDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Is.EqualTo("Cushion or hydrofoil support configuration is invalid."));
        }

        [Test]
        public void SupportedCraft_RunLighterAndFasterThanTheirHullResistanceAlone()
        {
            VesselParameters p = VesselFixtures.Parameters(VesselFixtures.MeteorFile);
            float speed = p.Data.support.fullSupportSpeedMps + 3f;
            float supported = ResistanceModel.ResistanceN(p, speed, float.PositiveInfinity);
            float hullOnly = ResistanceModel.DeepWaterResistanceN(p, speed);

            Assert.That(supported, Is.LessThan(hullOnly));
            Assert.That(supported / hullOnly, Is.EqualTo(p.Data.support.supportedResistanceFactor).Within(0.02f));
        }
    }
}
