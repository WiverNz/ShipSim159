using NUnit.Framework;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Tests
{
    // Virtual sea trials on the pure simulator. Deep-water results are checked against the IMO MSC.137(76)
    // envelope as a sanity bound; they are not validated trial data for any of these vessels.
    public sealed class SeaTrialsTests
    {
        private const float Deep = float.PositiveInfinity;

        // Published loaded service speeds: 507B 10 kn (MEB), Volgo-Balt 2-95A/R 10 kn (FleetPhoto, korabel.ru),
        // Volgoneft 1577 20 km/h (korabel.ru, FleetPhoto).
        private static readonly object[] PublishedSpeeds =
        {
            new object[] { VesselFixtures.VolgoDonFile, 10f },
            new object[] { VesselFixtures.VolgoBaltFile, 10f },
            new object[] { VesselFixtures.VolgoneftFile, 20f / 1.852f }
        };

        [TestCaseSource(nameof(PublishedSpeeds))]
        public void FullAhead_DeepWater_ReachesPublishedLoadedSpeed(string file, float publishedKnots)
        {
            float speedKnots = ManoeuvringTrials.SteadySpeed(VesselFixtures.Parameters(file), 1f, Deep) / 0.514444f;
            Assert.That(speedKnots, Is.EqualTo(publishedKnots).Within(0.5f));
        }

        [TestCase(VesselFixtures.VolgoDonFile)]
        [TestCase(VesselFixtures.VolgoBaltFile)]
        [TestCase(VesselFixtures.VolgoneftFile)]
        public void TelegraphSteps_GiveIncreasingSpeeds(string file)
        {
            VesselParameters p = VesselFixtures.Parameters(file);
            float slow = ManoeuvringTrials.SteadySpeed(p, 0.32f, Deep);
            float half = ManoeuvringTrials.SteadySpeed(p, 0.65f, Deep);
            float full = ManoeuvringTrials.SteadySpeed(p, 1f, Deep);
            Assert.That(slow, Is.GreaterThan(0.5f));
            Assert.That(half, Is.GreaterThan(slow));
            Assert.That(full, Is.GreaterThan(half));
        }

        [TestCase(VesselFixtures.VolgoDonFile)]
        [TestCase(VesselFixtures.VolgoBaltFile)]
        [TestCase(VesselFixtures.VolgoneftFile)]
        public void TurningCircle_MeetsImoCriteriaAndIsSymmetric(string file)
        {
            VesselParameters p = VesselFixtures.Parameters(file);
            TurningCircleResult starboard = ManoeuvringTrials.TurningCircle(p, 1f, Deep);
            TurningCircleResult port = ManoeuvringTrials.TurningCircle(p, -1f, Deep);

            Assert.That(starboard.AdvanceM, Is.LessThanOrEqualTo(4.5f * p.Lpp));
            Assert.That(starboard.TacticalDiameterM, Is.LessThanOrEqualTo(5f * p.Lpp));
            Assert.That(starboard.TacticalDiameterM, Is.GreaterThan(1.5f * p.Lpp));
            Assert.That(port.TacticalDiameterM, Is.EqualTo(starboard.TacticalDiameterM).Within(0.02f * starboard.TacticalDiameterM));
            Assert.That(starboard.SteadySpeedMps, Is.LessThan(starboard.ApproachSpeedMps), "Speed falls in a hard turn.");
        }

        [TestCase(VesselFixtures.VolgoDonFile)]
        [TestCase(VesselFixtures.VolgoBaltFile)]
        [TestCase(VesselFixtures.VolgoneftFile)]
        public void ZigZag_MeetsImoOvershootCriteria(string file)
        {
            VesselParameters p = VesselFixtures.Parameters(file);
            ZigZagResult ten = ManoeuvringTrials.ZigZag(p, 10f, Deep);
            ZigZagResult twenty = ManoeuvringTrials.ZigZag(p, 20f, Deep);

            Assert.That(ten.FirstOvershootDeg, Is.InRange(0f, ManoeuvringTrials.ImoFirstOvershootLimitDeg(ten.LengthOverSpeedS)));
            Assert.That(ten.SecondOvershootDeg, Is.InRange(0f, ManoeuvringTrials.ImoSecondOvershootLimitDeg(ten.LengthOverSpeedS)));
            Assert.That(twenty.FirstOvershootDeg, Is.InRange(0f, 25f));
            Assert.That(ten.InitialTurningDistanceM, Is.LessThanOrEqualTo(2.5f * p.Lpp));
        }

        [TestCase(VesselFixtures.VolgoDonFile)]
        [TestCase(VesselFixtures.VolgoBaltFile)]
        [TestCase(VesselFixtures.VolgoneftFile)]
        public void CrashStop_TrackReachIsWithinImoLimit(string file)
        {
            VesselParameters p = VesselFixtures.Parameters(file);
            StoppingResult stop = ManoeuvringTrials.CrashStop(p, Deep);

            Assert.That(stop.TrackReachM, Is.InRange(1f * p.Lpp, 15f * p.Lpp));
            Assert.That(stop.TimeS, Is.GreaterThan(30f));
        }

        [TestCase(VesselFixtures.VolgoDonFile)]
        [TestCase(VesselFixtures.VolgoBaltFile)]
        [TestCase(VesselFixtures.VolgoneftFile)]
        public void ShallowWater_SlowsTheVesselWidensTheTurnAndSquatsByTheBow(string file)
        {
            VesselParameters p = VesselFixtures.Parameters(file);
            float deepSpeed = ManoeuvringTrials.SteadySpeed(p, 1f, Deep);
            ManoeuvringSimulator shallow = ManoeuvringTrials.Approach(p, 1f, 4.6f);
            TurningCircleResult deepTurn = ManoeuvringTrials.TurningCircle(p, 1f, Deep);
            TurningCircleResult shallowTurn = ManoeuvringTrials.TurningCircle(p, 1f, 4.6f);

            Assert.That(shallow.SurgeSpeed, Is.LessThan(0.9f * deepSpeed));
            Assert.That(shallowTurn.SteadyTurningDiameterM, Is.GreaterThan(1.2f * deepTurn.SteadyTurningDiameterM));
            Assert.That(shallow.Last.BowSquatM, Is.GreaterThan(shallow.Last.SternSquatM));
            Assert.That(shallow.Last.BowSquatM, Is.GreaterThan(0.05f));
        }

        [Test]
        public void Kvlcc2Benchmark_TurnsAndChecksYawWithinABroadPublishedBand()
        {
            VesselParameters p = VesselFixtures.Kvlcc2();
            float speedKnots = ManoeuvringTrials.SteadySpeed(p, 1f, Deep) / 0.514444f;
            TurningCircleResult turn = ManoeuvringTrials.TurningCircle(p, 1f, Deep);
            ZigZagResult zigZag = ManoeuvringTrials.ZigZag(p, 10f, Deep);

            Assert.That(speedKnots, Is.EqualTo(15.5f).Within(0.8f));
            Assert.That(turn.AdvanceM / p.Lpp, Is.InRange(2.5f, 4f));
            Assert.That(turn.TacticalDiameterM / p.Lpp, Is.InRange(2.7f, 4.2f));
            Assert.That(zigZag.FirstOvershootDeg, Is.InRange(2f, 14f));
        }
    }
}
