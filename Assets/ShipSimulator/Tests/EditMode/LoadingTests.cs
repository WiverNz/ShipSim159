using System.IO;
using NUnit.Framework;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class LoadingTests
    {
        [Test]
        public void DefaultInventory_SeparatesCargoStoresAndBallastWithinDeadweight()
        {
            VesselData d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            LoadingState s = LoadingModel.Initial(d);
            Assert.That(s.massKg[0] + s.massKg[1], Is.EqualTo(4800000));
            Assert.That(s.massKg[2] + s.massKg[3], Is.Zero);
            Assert.That(s.massKg[4], Is.EqualTo(200000));
            var p = VesselParameters.Create(d, s);
            Assert.That(p.Mass, Is.EqualTo(6750000));
            Assert.That(p.Equilibrium.VolumeM3 * p.Rho, Is.EqualTo(p.Mass).Within(1e-4f * p.Mass));
            Assert.That(p.CentreOfMassLocalM.y, Is.EqualTo(d.massProperties.centerOfMassLocalM.y).Within(.001));
            Assert.That(p.CentreOfMassLocalM.z, Is.EqualTo(d.massProperties.centerOfMassLocalM.z).Within(.001));
        }

        [Test]
        public void SharedBudget_BlocksBallastOnFullShipAndTransfersOnlyAvailableMass()
        {
            VesselData d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            LoadingState s = LoadingModel.Initial(d);
            s.commands[2] = 1;
            Assert.That(LoadingModel.Step(d, s, 10, true), Is.False);
            s.commands[0] = -1;
            LoadingModel.Step(d, s, 10, true);
            Assert.That(s.massKg[0], Is.EqualTo(2400000 - 1250));
            Assert.That(s.massKg[2], Is.EqualTo(500));
            Assert.That(LoadingModel.Payload(s), Is.EqualTo(5000000 - 750));
            LoadingModel.Validate(d, s);
        }

        [Test]
        public void ClosedHatches_BlockCargoButAllowBallastAndStores()
        {
            var d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            var s = LoadingModel.Initial(d, 0);
            s.commands[0] = s.commands[2] = s.commands[4] = 1;
            LoadingModel.Step(d, s, 20, false);
            Assert.That(s.massKg[0], Is.Zero);
            Assert.That(s.massKg[2], Is.EqualTo(1000));
            Assert.That(s.massKg[4], Is.EqualTo(200));
        }

        [Test]
        public void BallastAndCargo_EqualMassDifferentCentreAndInertia()
        {
            var d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            var cargo = LoadingModel.Initial(d, 0);
            var ballast = LoadingModel.Initial(d, 0);
            cargo.massKg[0] = cargo.massKg[1] = 1000000;
            ballast.massKg[2] = ballast.massKg[3] = 1000000;
            var a = VesselParameters.Create(d, cargo);
            var b = VesselParameters.Create(d, ballast);
            Assert.That(b.Mass, Is.EqualTo(a.Mass));
            // Same mass and longitudinal centre; only the small KG term in the trimmed equilibrium differs.
            Assert.That(b.Draft, Is.EqualTo(a.Draft).Within(.01f));
            Assert.That(b.CentreOfGravityAboveKeel, Is.LessThan(a.CentreOfGravityAboveKeel));
            Assert.That(b.RollInertia, Is.Not.EqualTo(a.RollInertia));
            cargo.massKg[0] += 500000;
            cargo.massKg[1] -= 500000;
            var asymmetric = VesselParameters.Create(d, cargo);
            Assert.That(asymmetric.Mass, Is.EqualTo(a.Mass));
            Assert.That(asymmetric.CentreOfGravityX, Is.GreaterThan(a.CentreOfGravityX));
            Assert.That(asymmetric.YawInertiaAtG, Is.Not.EqualTo(a.YawInertiaAtG));
        }

        [Test]
        public void EmptyPartialLoaded_UpdateDraftMassAndImmersionTogether()
        {
            var d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            var empty = VesselParameters.Create(d, 0);
            var partial = VesselParameters.Create(d, .5f);
            var full = VesselParameters.Create(d, 1);
            Assert.That(empty.Mass, Is.EqualTo(d.massProperties.lightshipMassKg));
            Assert.That(empty.Draft, Is.LessThan(partial.Draft));
            Assert.That(partial.Draft, Is.LessThan(full.Draft));
            // Immersion follows the local draft at the screws, so the stern-trimmed lightship keeps them wet.
            foreach (var p in new[] { empty, partial, full })
                Assert.That(p.PropellerImmersion, Is.EqualTo(Mathf.Clamp01((p.DraftAt(d.propeller.longitudinalPositionsM[0]) -
                    d.loading.propellerAxisHeightAboveKeelM + .5f * d.propeller.diameterM) / d.propeller.diameterM)));
            Assert.That(full.PropellerImmersion, Is.EqualTo(1));
            var level = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            level.hydrostatics.sectionTable.enabled = false;
            Assert.That(VesselParameters.Create(level, 0).PropellerImmersion, Is.InRange(0, .6f));
            Assert.That(new BowThrusterModel(empty).BollardThrustN, Is.GreaterThan(0));
            var dryThruster = new BowThrusterModel(empty);
            dryThruster.Restore(1); dryThruster.Step(1, 0, 1);
            Assert.That(dryThruster.ThrustN, Is.Zero);
        }

        [Test]
        public void Rebuild_PreservesShaftGovernorAndReversalObjects()
        {
            var d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            var original = new ManoeuvringModel(VesselParameters.Create(d));
            original.RestoreActuators(.2f, new[] { 4f, -3f });
            original.Shafts[0].Step(-1, 1000, .1f);
            var next = new ManoeuvringModel(VesselParameters.Create(d, .5f), original);
            Assert.That(next.Shafts[0], Is.SameAs(original.Shafts[0]));
            Assert.That(next.Shafts[0].IsReversing, Is.True);
            Assert.That(next.RudderAngleRad, Is.EqualTo(.2f));
        }

        [Test]
        public void InvalidSavedInventory_RejectsNaNCapacityAndSharedBudgetViolations()
        {
            var d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            var s = LoadingModel.Initial(d);
            s.massKg[0] = float.NaN;
            Assert.Throws<InvalidDataException>(() => LoadingModel.Validate(d, s));
            s = LoadingModel.Initial(d); s.massKg[2] = 1;
            Assert.Throws<InvalidDataException>(() => LoadingModel.Validate(d, s));
            s = LoadingModel.Initial(d, 0); s.massKg[2] = 1100001;
            Assert.Throws<InvalidDataException>(() => LoadingModel.Validate(d, s));
        }

        [Test]
        public void StateRoundTrip_PreservesAmountsCommandsAndIndependentCopies()
        {
            var d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            var s = LoadingModel.Initial(d, .5f);
            s.commands[2] = 1;
            var copy = JsonUtility.FromJson<LoadingState>(JsonUtility.ToJson(s));
            LoadingModel.Validate(d, copy);
            LoadingModel.Step(d, s, 5, true);
            LoadingModel.Step(d, copy, 5, true);
            Assert.That(copy.massKg, Is.EqualTo(s.massKg));
            var snapshot = s.Copy(); snapshot.massKg[0] = 0;
            Assert.That(s.massKg[0], Is.GreaterThan(0));
        }

        [Test]
        public void SavedCompartmentIdentity_CannotSilentlyMoveMassToAnotherTank()
        {
            var d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            var s = LoadingModel.Initial(d);
            s.compartmentIds[0] = s.compartmentIds[2];
            Assert.Throws<InvalidDataException>(() => LoadingModel.Validate(d, s));
        }

        [Test]
        public void InvalidTransferDuration_CannotCreateOrRemoveMass()
        {
            var d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            var s = LoadingModel.Initial(d, .5f);
            s.commands[0] = 1;
            foreach (float duration in new[] { -1f, float.NaN, float.PositiveInfinity })
                Assert.That(LoadingModel.Step(d, s, duration, true), Is.False);
            Assert.That(LoadingModel.Payload(s), Is.EqualTo(2500000));
        }

        [Test]
        public void Validator_RejectsUnsupportedTransverseOffsetAndOverbookedInitialLoad()
        {
            var d = VesselFixtures.Load(VesselFixtures.VolgoDonFile);
            Assert.That(VesselDataValidator.TryValidate(d, out string error), Is.True, error);
            d.loading.compartments[0].centreLocalM.x = 1;
            Assert.That(VesselDataValidator.TryValidate(d, out _), Is.False);
            d.loading.compartments[0].centreLocalM.x = 0;
            d.loading.compartments[2].initialKg = 100;
            Assert.That(VesselDataValidator.TryValidate(d, out _), Is.False);
        }
    }
}
