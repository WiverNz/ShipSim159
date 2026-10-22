using NUnit.Framework;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class SteeringNozzleTests
    {
        [TestCase(100000f, 35f)]
        [TestCase(-100000f, 35f)]
        [TestCase(100000f, -35f)]
        public void JetReaction_PreservesMagnitudeAndReversesSteeringWithThrust(float thrust, float degrees)
        {
            var nozzle = VesselFixtures.VolgoDon().Data.steeringNozzle;
            Vector2 force = SteeringNozzleModel.Thrust(nozzle, thrust, degrees * Mathf.Deg2Rad);
            Assert.That(force.magnitude, Is.EqualTo(Mathf.Abs(thrust)).Within(.02f));
            Assert.That(Mathf.Sign(force.y), Is.EqualTo(-Mathf.Sign(thrust * degrees)));
            Assert.That(Mathf.Sign(-64.65f * force.y), Is.EqualTo(Mathf.Sign(thrust * degrees)));
        }

        [Test]
        public void PassiveForce_NeverAddsEnergy_InForwardReverseAndCrossFlow()
        {
            var nozzle = VesselFixtures.VolgoDon().Data.steeringNozzle;
            for (int degrees = -35; degrees <= 35; degrees += 5)
            for (int u = -6; u <= 6; u++)
            for (int v = -3; v <= 3; v++)
            {
                var velocity = new Vector2(u, v);
                Vector2 force = SteeringNozzleModel.Passive(nozzle, 1000, velocity, degrees * Mathf.Deg2Rad);
                Assert.That(Vector2.Dot(force, velocity), Is.LessThanOrEqualTo(.001f));
            }
            Assert.That(SteeringNozzleModel.Passive(nozzle, 1000, Vector2.zero, .5f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void Nozzles_ReplaceGenericRudderAndApplyJetLateralForceToTotal()
        {
            VesselData data = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            ManoeuvringOutput first = Forces(data);
            data.rudder.areaPerRudderM2 *= 10;
            data.rudder.hullInteractionFactor *= 10;
            ManoeuvringOutput changed = Forces(data);
            Assert.That(changed.TotalY, Is.EqualTo(first.TotalY));
            Assert.That(changed.TotalN, Is.EqualTo(first.TotalN));
            Assert.That(first.PropellerY, Is.LessThan(0));
            Assert.That(first.TotalY, Is.EqualTo(first.Hull.Y + first.PropellerY + first.RudderY).Within(.01f));
            Assert.That(first.PropellerN, Is.EqualTo(-64.65f * first.PropellerY).Within(1f));
        }

        [Test]
        public void OptionalNozzle_DisabledMatchesAbsentOnLegacyVessel()
        {
            VesselData data = VesselFixtures.Load(VesselFixtures.VolgoBaltFile);
            ManoeuvringOutput absent = Forces(data);
            data.steeringNozzle = new VesselSteeringNozzle { enabled = false };
            ManoeuvringOutput disabled = Forces(data);
            Assert.That(disabled.TotalX, Is.EqualTo(absent.TotalX));
            Assert.That(disabled.TotalY, Is.EqualTo(absent.TotalY));
            Assert.That(disabled.TotalN, Is.EqualTo(absent.TotalN));
            Assert.That(disabled.PropellerY, Is.Zero);
        }

        [Test]
        public void Validator_RejectsInvalidNozzleCoefficientAndMisalignedShaft()
        {
            VesselData data = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            Assert.That(VesselDataValidator.TryValidate(data, out _), Is.True);
            data.steeringNozzle.thrustDeflectionFactor = float.NaN;
            Assert.That(VesselDataValidator.TryValidate(data, out _), Is.False);
            data.steeringNozzle.thrustDeflectionFactor = .8f;
            data.propeller.lateralPositionsM[0] += 1;
            Assert.That(VesselDataValidator.TryValidate(data, out _), Is.False);
        }

        [Test]
        public void OppositeEngineCommands_DevelopOppositeShaftRotationAndYaw()
        {
            var ship = new ManoeuvringSimulator(VesselFixtures.VolgoDon()) { DepthM = float.PositiveInfinity };
            ship.EngineCommands[0] = 1;
            ship.EngineCommands[1] = -1;
            for (int i = 0; i < 3600; i++) ship.Step(.05f);
            Assert.That(ship.Model.Shafts[0].Rps, Is.GreaterThan(0));
            Assert.That(ship.Model.Shafts[1].Rps, Is.LessThan(0));
            Assert.That(ship.HeadingRad, Is.GreaterThan(.1f));
        }

        private static ManoeuvringOutput Forces(VesselData data)
        {
            var model = new ManoeuvringModel(VesselParameters.Create(data));
            model.RestoreActuators(.3f, new[] { 4f, 4f });
            return model.Step(new ManoeuvringInput
            {
                SurgeSpeed = 3, RudderCommand = .3f / (data.rudder.maxAngleDeg * Mathf.Deg2Rad),
                EngineCommands = new[] { 1f, 1f }, DepthM = float.PositiveInfinity
            }, 0);
        }
    }
}
