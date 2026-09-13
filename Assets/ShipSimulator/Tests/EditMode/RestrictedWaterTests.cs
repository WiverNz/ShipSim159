using NUnit.Framework;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class RestrictedWaterTests
    {
        [Test]
        public void IcorelsSquat_MatchesSerbanAndPanaitescuExample()
        {
            // Cargo ship of Serban and Panaitescu (2016): 118 m, 10 443.97 m3, 8 kn in 10 m of water, C_S = 2.4.
            float squat = RestrictedWaterModel.IcorelsBowSquatM(10443.97f, 118f, 0.667f, 8f * 0.514444f, 10f, 2.4f);
            Assert.That(squat, Is.EqualTo(0.34186f).Within(0.003f));
        }

        [Test]
        public void BlockageFactor_IsOneForWideRiversAndGrowsWhenConfined()
        {
            Assert.That(RestrictedWaterModel.BlockageFactor(0.05f), Is.EqualTo(1f));
            Assert.That(RestrictedWaterModel.BlockageFactor(0.25f), Is.EqualTo(5.74f * Mathf.Pow(0.25f, 0.76f)).Within(1e-4f));
            Assert.That(RestrictedWaterModel.BlockageFactor(0.25f), Is.GreaterThan(RestrictedWaterModel.BlockageFactor(0.12f)));
        }

        [Test]
        public void Squat_FullFormSinksDeeperAtTheBow()
        {
            RestrictedWaterModel.Squat(6750f, 135f, 0.851f, 4f, 4.6f, 0.05f, 0f, out float bow, out float stern);
            Assert.That(bow, Is.GreaterThan(stern));
            Assert.That(stern, Is.GreaterThan(0f));
        }

        [Test]
        public void DepthFactors_AreUnityInDeepWaterAndGrowAsWaterShoals()
        {
            DepthFactors deep = RestrictedWaterModel.DepthFactors(135f, 16.5f, 3.53f, 0.851f, float.PositiveInfinity);
            Assert.That(deep.Yv, Is.EqualTo(1f));
            Assert.That(deep.SwayAddedMass, Is.EqualTo(1f));

            float previousYv = 1f, previousNr = 1f, previousMass = 1f;
            foreach (float depthToDraft in new[] { 6f, 3f, 2f, 1.5f, 1.2f })
            {
                DepthFactors f = RestrictedWaterModel.DepthFactors(135f, 16.5f, 3.53f, 0.851f, 3.53f * depthToDraft);
                Assert.That(f.Yv, Is.GreaterThanOrEqualTo(previousYv));
                Assert.That(f.Nr, Is.GreaterThanOrEqualTo(previousNr));
                Assert.That(f.SwayAddedMass, Is.GreaterThanOrEqualTo(previousMass));
                Assert.That(f.SwayAddedMass, Is.LessThan(10f), "Added mass must stay physically bounded.");
                previousYv = f.Yv;
                previousNr = f.Nr;
                previousMass = f.SwayAddedMass;
            }
        }

        [Test]
        public void LackenbySpeedLoss_ZeroInDeepWaterAndGrowsInShallowWater()
        {
            Assert.That(ResistanceModel.LackenbySpeedLoss(57f, float.PositiveInfinity, 5f), Is.EqualTo(0f));
            Assert.That(ResistanceModel.LackenbySpeedLoss(57f, 30f, 5f), Is.LessThan(0.05f));
            Assert.That(ResistanceModel.LackenbySpeedLoss(57f, 4.6f, 5f), Is.GreaterThan(ResistanceModel.LackenbySpeedLoss(57f, 8.2f, 5f)));
        }

        [Test]
        public void BankOnStarboard_PullsTheShipTowardItAndTurnsTheBowAway()
        {
            VesselParameters p = VesselFixtures.VolgoDon();
            RestrictedWaterModel.BankForces(p, 3f, 4.6f, 260f, 90f, out float y, out float n);

            Assert.That(y, Is.GreaterThan(0f), "Suction toward the starboard bank.");
            Assert.That(n, Is.LessThan(0f), "Bow pushed away, to port.");
            RestrictedWaterModel.BankForces(p, 3f, 4.6f, 175f, 175f, out float symmetricY, out _);
            Assert.That(symmetricY, Is.EqualTo(0f).Within(1e-3f));
        }
    }
}
