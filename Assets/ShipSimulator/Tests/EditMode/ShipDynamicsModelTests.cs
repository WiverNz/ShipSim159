using NUnit.Framework;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class ShipDynamicsModelTests
    {
        [TestCase(VesselFixtures.Kvlcc2File)]
        [TestCase(VesselFixtures.VolgoBaltFile)]
        [TestCase(VesselFixtures.VolgoneftFile)]
        public void VesselJson_IsValid(string file)
        {
            Assert.That(VesselDataValidator.TryValidate(VesselFixtures.Load(file), out string error), Is.True, error);
        }

        [Test]
        public void ClarkeEstimate_ReproducesKvlcc2LinearDerivativesWithinAQuarter()
        {
            HullDerivativeEstimate estimate = HullDerivativeEstimate.Clarke(320f, 58f, 20.8f, 0.81f);

            Assert.That(estimate.Yv, Is.EqualTo(-0.315f).Within(0.25f * 0.315f));
            Assert.That(estimate.Yr, Is.EqualTo(0.083f).Within(0.25f * 0.083f));
            Assert.That(estimate.Nv, Is.EqualTo(-0.137f).Within(0.25f * 0.137f));
            Assert.That(estimate.Nr, Is.EqualTo(-0.049f).Within(0.25f * 0.049f));
            Assert.That(estimate.SwayAddedMass, Is.EqualTo(0.223f).Within(0.25f * 0.223f));
            Assert.That(estimate.YawAddedInertia, Is.EqualTo(0.011f).Within(0.25f * 0.011f));
        }

        [TestCase(VesselFixtures.VolgoDonFile)]
        [TestCase(VesselFixtures.VolgoBaltFile)]
        [TestCase(VesselFixtures.VolgoneftFile)]
        public void ProjectVessel_LinearCoefficientsTraceToClarkeAndSoding(string file)
        {
            VesselData data = VesselFixtures.Load(file);
            VesselDimensions d = data.dimensions;
            HullDerivativeEstimate estimate = HullDerivativeEstimate.Clarke(d.lengthBetweenPerpendicularsM, d.beamMouldedM,
                d.loadedDraftM, data.hydrostatics.blockCoefficient);
            float scale = 0.5f * data.hydrostatics.waterDensityKgM3 * d.lengthBetweenPerpendicularsM *
                d.lengthBetweenPerpendicularsM * d.loadedDraftM;
            float soding = HullDerivativeEstimate.SodingSurgeAddedMassKg(data.hydrostatics.waterDensityKgM3,
                data.massProperties.loadedMassKg / data.hydrostatics.waterDensityKgM3, d.lengthBetweenPerpendicularsM) / scale;

            Assert.That(data.hull.yV, Is.EqualTo(estimate.Yv).Within(0.01f * Mathf.Abs(estimate.Yv)));
            Assert.That(data.hull.yR, Is.EqualTo(estimate.Yr).Within(0.01f * Mathf.Abs(estimate.Yr)));
            Assert.That(data.hull.nV, Is.EqualTo(estimate.Nv).Within(0.01f * Mathf.Abs(estimate.Nv)));
            Assert.That(data.hull.nR, Is.EqualTo(estimate.Nr).Within(0.01f * Mathf.Abs(estimate.Nr)));
            Assert.That(data.hull.swayAddedMassPrime, Is.EqualTo(estimate.SwayAddedMass).Within(0.01f * estimate.SwayAddedMass));
            Assert.That(data.hull.yawAddedInertiaPrime, Is.EqualTo(estimate.YawAddedInertia).Within(0.01f * estimate.YawAddedInertia));
            Assert.That(data.hull.surgeAddedMassPrime, Is.EqualTo(soding).Within(0.01f * soding));
        }

        [Test]
        public void FrictionLine_MatchesIttc1957()
        {
            Assert.That(ResistanceModel.FrictionCoefficient(6.09e8f), Is.EqualTo(0.0016293f).Within(0.000002f));
        }

        [Test]
        public void WindLoads_FollowBlendermannCoefficientsAndSigns()
        {
            VesselParameters p = VesselFixtures.VolgoDon();

            WindLoadModel.Coefficients(p, new Vector2(-10f, 0f), out float headX, out float headY, out _);
            Assert.That(headX, Is.EqualTo(-p.Data.windage.longitudinalDragBow).Within(1e-4f));
            Assert.That(headY, Is.EqualTo(0f).Within(1e-4f));

            WindLoadModel.Coefficients(p, new Vector2(0f, 10f), out _, out float portBeamY, out _);
            Assert.That(portBeamY, Is.EqualTo(p.Data.windage.transverseDragCoefficient).Within(1e-4f));

            WindLoadModel.Evaluate(p, new Vector2(-7f, 7f), out _, out float bowY, out float bowN);
            WindLoadModel.Evaluate(p, new Vector2(-7f, -7f), out _, out float mirroredY, out float mirroredN);
            Assert.That(bowY, Is.GreaterThan(0f), "Wind from the port bow pushes the ship to starboard.");
            Assert.That(bowN, Is.GreaterThan(0f), "Wind from the port bow turns the bow away, to starboard.");
            Assert.That(mirroredY, Is.EqualTo(-bowY).Within(1e-3f));
            Assert.That(mirroredN, Is.EqualTo(-bowN).Within(1e-2f));
        }

        [Test]
        public void BollardPull_IsAPlausibleFractionOfTheActuatorDiscIdeal()
        {
            VesselParameters p = VesselFixtures.VolgoDon();
            var shaft = new EngineShaft(p);
            float thrust = 0f, torque = 0f;
            for (float t = 0f; t < 120f; t += 0.02f)
            {
                thrust = PropellerModel.Evaluate(p.Data.propeller, p.Rho, shaft.Rps, 0f, out torque, out _);
                shaft.Step(1f, torque, 0.02f);
            }
            float power = torque * 2f * Mathf.PI * shaft.Rps;
            float diameter = p.Data.propeller.diameterM;
            float ideal = Mathf.Pow(2f * p.Rho * Mathf.PI * diameter * diameter * 0.25f * power * power, 1f / 3f);

            Assert.That(thrust / ideal, Is.InRange(0.5f, 0.85f));
            Assert.That(2f * thrust, Is.InRange(150000f, 200000f));
        }

        [Test]
        public void OpenWaterEfficiency_PeaksInTheUsualRange()
        {
            VesselPropeller propeller = VesselFixtures.Load(VesselFixtures.VolgoDonFile).propeller;
            float peak = 0f;
            for (float j = 0f; j <= 1f; j += 0.01f) peak = Mathf.Max(peak, PropellerModel.OpenWaterEfficiency(propeller, j));
            Assert.That(peak, Is.InRange(0.45f, 0.7f));
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void RudderCommand_TurnsTheBowTheSameWay(float command)
        {
            ManoeuvringSimulator ship = ManoeuvringTrials.Approach(VesselFixtures.VolgoDon(), 1f, float.PositiveInfinity);
            ship.RudderCommand = command;
            for (float t = 0f; t < 60f; t += 0.05f) ship.Step(0.05f);

            Assert.That(Mathf.Sign(ship.YawRate), Is.EqualTo(command));
            Assert.That(Mathf.Sign(ship.HeadingRad), Is.EqualTo(command));
        }

        [Test]
        public void SplitEngines_PortAheadStarboardAstern_TurnStarboardFromRest()
        {
            var ship = new ManoeuvringSimulator(VesselFixtures.VolgoDon());
            ship.EngineCommands[0] = 0.65f;
            ship.EngineCommands[1] = -0.65f;
            for (float t = 0f; t < 300f; t += 0.05f) ship.Step(0.05f);

            Assert.That(ship.HeadingRad * Mathf.Rad2Deg, Is.GreaterThan(5f));
        }

        [Test]
        public void RudderInPropellerSlipstream_SteersFromRest()
        {
            var ship = new ManoeuvringSimulator(VesselFixtures.VolgoDon());
            ship.SetEngineCommands(0.65f);
            ship.RudderCommand = 1f;
            for (float t = 0f; t < 60f; t += 0.05f) ship.Step(0.05f);

            Assert.That(ship.HeadingRad * Mathf.Rad2Deg, Is.GreaterThan(3f));
        }

        [Test]
        public void RotationAtZeroSpeed_DecaysWithoutBlowingUp()
        {
            var ship = new ManoeuvringSimulator(VesselFixtures.VolgoDon());
            ship.Restore(0f, 0f, 0.02f, 0f, null);
            float previous = ship.YawRate;
            for (float t = 0f; t < 300f; t += 0.05f)
            {
                ship.Step(0.05f);
                Assert.That(float.IsNaN(ship.YawRate) || float.IsNaN(ship.SurgeSpeed), Is.False);
            }
            Assert.That(Mathf.Abs(ship.YawRate), Is.LessThan(0.5f * previous));
        }

        [Test]
        public void UniformCurrent_OnlyShiftsTheGroundTrack()
        {
            VesselParameters p = VesselFixtures.VolgoDon();
            var still = new ManoeuvringSimulator(p);
            // The air drifts with the water, otherwise the current also changes the apparent wind.
            var drifting = new ManoeuvringSimulator(p) { CurrentMps = new Vector2(0.5f, -0.3f), WindMps = new Vector2(0.5f, -0.3f) };
            foreach (ManoeuvringSimulator ship in new[] { still, drifting })
            {
                ship.SetEngineCommands(1f);
                ship.RudderCommand = 0.5f;
            }
            for (int i = 0; i < 6000; i++)
            {
                still.Step(0.05f);
                drifting.Step(0.05f);
            }

            Assert.That(drifting.SurgeSpeed, Is.EqualTo(still.SurgeSpeed).Within(1e-3f));
            Assert.That(drifting.SwaySpeed, Is.EqualTo(still.SwaySpeed).Within(1e-3f));
            Assert.That(drifting.HeadingRad, Is.EqualTo(still.HeadingRad).Within(1e-3f));
            Vector2 offset = drifting.Position - still.Position;
            Assert.That(Vector2.Distance(offset, drifting.CurrentMps * drifting.TimeS), Is.LessThan(0.5f));
        }

        [Test]
        public void EngineReversal_BrakesBeforeDrivingAstern()
        {
            VesselParameters p = VesselFixtures.VolgoDon();
            var shaft = new EngineShaft(p);
            shaft.Restore(p.RatedRps);
            float reversalRps = p.Data.engine.reversalRpmFraction * p.RatedRps;
            bool reversed = false;
            for (float t = 0f; t < 120f; t += 0.02f)
            {
                PropellerModel.Evaluate(p.Data.propeller, p.Rho, shaft.Rps, 3f, out float torque, out _);
                shaft.Step(-1f, torque, 0.02f);
                if (shaft.Rps > reversalRps)
                    Assert.That(shaft.EngineTorqueNm, Is.LessThanOrEqualTo(0f), "No fuel torque ahead once astern is ordered.");
                reversed |= shaft.Rps < -0.5f * p.RatedRps;
            }
            Assert.That(reversed, Is.True);
        }

        [Test]
        public void Hydrostatics_FloatLoadedMassAtDraftWithPlausibleStability()
        {
            VesselParameters p = VesselFixtures.VolgoDon();
            var hydrostatics = new HydrostaticsModel(p);
            float moment = 0f;
            for (int i = 0; i < hydrostatics.CellCount; i++) moment += hydrostatics.Area(i) * hydrostatics.KeelPoint(i).z;
            float rollPeriod = 2f * Mathf.PI * p.Data.massProperties.rollGyrationRadiusM /
                Mathf.Sqrt(VesselParameters.Gravity * hydrostatics.MetacentricHeightM);

            Assert.That(p.Rho * hydrostatics.WaterplaneArea * p.Draft, Is.EqualTo(p.Mass).Within(0.001f * p.Mass));
            Assert.That(moment / hydrostatics.WaterplaneArea, Is.EqualTo(p.Data.hydrostatics.longitudinalCentreOfBuoyancyM).Within(0.01f));
            Assert.That(hydrostatics.MetacentricHeightM, Is.InRange(2f, 8f));
            Assert.That(rollPeriod, Is.InRange(3f, 12f));
        }

        [Test]
        public void Lightship_FloatsHigherWithMoreWindage()
        {
            VesselData data = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            VesselParameters loaded = VesselParameters.Create(data, 1f);
            VesselParameters light = VesselParameters.Create(data, 0f);

            Assert.That(light.Draft, Is.EqualTo(data.dimensions.loadedDraftM * data.massProperties.lightshipMassKg /
                data.massProperties.loadedMassKg).Within(1e-3f));
            Assert.That(light.WindLateralArea, Is.GreaterThan(loaded.WindLateralArea));
            Assert.That(light.SwayAddedMass, Is.LessThan(loaded.SwayAddedMass));
        }
    }
}
