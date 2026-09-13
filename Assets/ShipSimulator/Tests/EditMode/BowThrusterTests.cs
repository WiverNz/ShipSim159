using NUnit.Framework;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class BowThrusterTests
    {
        [Test]
        public void BollardThrust_FollowsActuatorDiscTheoryWithFigureOfMerit()
        {
            float thrust = BowThrusterModel.BollardThrust(1000f, 160000f, 1f, 0.62f);

            Assert.That(thrust, Is.EqualTo(21241f).Within(100f));
            // Tunnel thrusters typically deliver 10 to 15 kgf per kW at bollard.
            Assert.That(thrust / 9.81f / 160f, Is.InRange(10f, 15f));
        }

        [Test]
        public void StarboardThrust_FromRest_TurnsTheBowToStarboard()
        {
            var ship = new ManoeuvringSimulator(VesselFixtures.VolgoDon()) { BowThrusterCommand = 1f };
            for (float t = 0f; t < 120f; t += 0.05f) ship.Step(0.05f);

            Assert.That(ship.YawRate, Is.GreaterThan(0f));
            Assert.That(ship.HeadingRad * Mathf.Rad2Deg, Is.GreaterThan(3f));
            Assert.That(ship.Last.ThrusterN, Is.GreaterThan(0f));
        }

        [Test]
        public void VesselWithoutThruster_IgnoresTheCommand()
        {
            var ship = new ManoeuvringSimulator(VesselFixtures.Kvlcc2()) { BowThrusterCommand = 1f };
            for (float t = 0f; t < 60f; t += 0.05f) ship.Step(0.05f);

            Assert.That(ship.Model.BowThruster, Is.Null);
            Assert.That(ship.HeadingRad, Is.EqualTo(0f));
        }

        [Test]
        public void Effectiveness_FallsAsTheShipGathersWay()
        {
            VesselBowThruster thruster = VesselFixtures.Load(VesselFixtures.VolgoDonFile).bowThruster;

            Assert.That(BowThrusterModel.SpeedEffectiveness(thruster, 0f), Is.EqualTo(1f));
            float oneKnot = BowThrusterModel.SpeedEffectiveness(thruster, 0.51f);
            float threeKnots = BowThrusterModel.SpeedEffectiveness(thruster, 1.54f);
            float sixKnots = BowThrusterModel.SpeedEffectiveness(thruster, 3.09f);
            Assert.That(oneKnot, Is.GreaterThan(threeKnots));
            Assert.That(threeKnots, Is.GreaterThan(sixKnots));
            Assert.That(sixKnots, Is.GreaterThanOrEqualTo(thruster.minimumSpeedEffectiveness));
        }

        [Test]
        public void Lightship_TunnelOutOfTheWater_GivesNoThrust()
        {
            VesselData data = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            var loaded = new BowThrusterModel(VesselParameters.Create(data, 1f));
            var light = new BowThrusterModel(VesselParameters.Create(data, 0f));
            loaded.Restore(1f);
            light.Restore(1f);
            loaded.Step(1f, 0f, 0.02f);
            light.Step(1f, 0f, 0.02f);

            Assert.That(loaded.ThrustN, Is.EqualTo(loaded.BollardThrustN).Within(1f));
            Assert.That(light.ThrustN, Is.EqualTo(0f));
        }

        [Test]
        public void Output_RampsTowardTheCommand()
        {
            var thruster = new BowThrusterModel(VesselFixtures.VolgoDon());
            thruster.Step(1f, 0f, 1f);

            Assert.That(thruster.Output, Is.EqualTo(0.2f).Within(1e-4f));
        }

        [Test]
        public void Validator_AcceptsNoThrusterAndRejectsInvalidFittedValues()
        {
            VesselData data = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            data.bowThruster = null;
            Assert.That(VesselDataValidator.TryValidate(data, out string missingError), Is.True, missingError);

            data = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            data.bowThruster.powerW = -1f;
            Assert.That(VesselDataValidator.TryValidate(data, out string error), Is.False);
            Assert.That(error, Is.EqualTo("Bow thruster configuration is invalid."));
        }

        [TestCase(false, 1f, "")]
        [TestCase(true, 0f, "   BOW THRUSTER OFF")]
        [TestCase(true, -0.5f, "   BOW THRUSTER PORT 50%")]
        [TestCase(true, 1f, "   BOW THRUSTER STBD 100%")]
        public void HudStatus_ShowsSideAndPower(bool fitted, float output, string expected)
        {
            Assert.That(ShipTelemetryUI.FormatBowThrusterStatus(fitted, output), Is.EqualTo(expected));
        }
    }
}
