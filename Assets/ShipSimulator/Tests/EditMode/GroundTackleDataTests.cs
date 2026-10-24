using System.IO;
using NUnit.Framework;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class GroundTackleDataTests
    {
        // 43 mm stud-less chain, submerged weight per metre, and its axial stiffness.
        private const double ChainWeight = 36.8 * 9.81 * (1 - 1000.0 / 7850);
        private const double ChainStiffness = 1.58e8;

        [Test]
        public void SlackChain_HangsStraightDownAndPassesNoHorizontalForce()
        {
            CatenaryShape shape = Catenary.Solve(40, 20, 10, ChainWeight, ChainStiffness, true);
            Assert.That(shape.Slack, Is.True);
            Assert.That(shape.HorizontalN, Is.Zero);
            Assert.That(shape.HighVerticalN, Is.EqualTo(ChainWeight * 10).Within(1e-6));
            Assert.That(shape.GroundedM, Is.EqualTo(30).Within(1e-6));
        }

        [Test]
        public void Tension_GrowsSmoothlyWithSpan_FromSlackThroughTouchdownToStretch()
        {
            double previous = 0;
            for (double span = 25; span <= 41; span += 0.05)
            {
                CatenaryShape shape = Catenary.Solve(40, span, 10, ChainWeight, ChainStiffness, true);
                Assert.That(shape.HorizontalN, Is.GreaterThanOrEqualTo(previous), $"span {span}");
                Assert.That(double.IsFinite(shape.HorizontalN) && double.IsFinite(shape.HighVerticalN), Is.True);
                Assert.That(shape.HeightAt(0), Is.EqualTo(0).Within(1e-3), "Starts at the low end.");
                Assert.That(shape.HeightAt(span), Is.EqualTo(10).Within(0.02), "Ends at the high end.");
                previous = shape.HorizontalN;
            }
            // Past the chord the chain stretches elastically: about EA times the strain.
            CatenaryShape stretched = Catenary.Solve(40, 40.5, 10, ChainWeight, ChainStiffness, true);
            double strain = (System.Math.Sqrt(40.5 * 40.5 + 100) - 40) / 40;
            Assert.That(stretched.HighTensionN, Is.EqualTo(ChainStiffness * strain).Within(0.05 * ChainStiffness * strain));
        }

        [Test]
        public void TouchdownAndLiftOff_AreContinuous()
        {
            // Without the bed the suspended solution applies; at lift-off both agree.
            double lastGrounded = double.NaN;
            for (double span = 37; span < 39.6; span += 0.001)
            {
                CatenaryShape shape = Catenary.Solve(40, span, 10, ChainWeight, ChainStiffness, true);
                if (shape.GroundedM > 0) { lastGrounded = span; continue; }
                CatenaryShape before = Catenary.Solve(40, lastGrounded, 10, ChainWeight, ChainStiffness, true);
                Assert.That(shape.HorizontalN, Is.EqualTo(before.HorizontalN).Within(0.01 * before.HorizontalN + 1));
                Assert.That(shape.LowVerticalN, Is.GreaterThanOrEqualTo(0).And.LessThan(0.05 * shape.HorizontalN + 50));
                return;
            }
            Assert.Fail("The chain never lifted off the bed.");
        }

        [Test]
        public void Lines_NeverPush_AndStayFiniteAtExtremes()
        {
            double[] lengths = { 0.001, 2, 30, 120 }, spans = { 0, 0.5, 29.9, 30.1, 200 }, heights = { 0, 1.5, 40 };
            foreach (double length in lengths)
                foreach (double span in spans)
                    foreach (double height in heights)
                        foreach (bool bed in new[] { true, false })
                        {
                            CatenaryShape shape = Catenary.Solve(length, span, height, 9.81, 1.3e6, bed);
                            Assert.That(shape.HorizontalN, Is.GreaterThanOrEqualTo(0));
                            Assert.That(double.IsFinite(shape.HorizontalN) && double.IsFinite(shape.HighVerticalN) &&
                                double.IsFinite(shape.LowVerticalN), Is.True, $"{length} {span} {height} {bed}");
                            Assert.That(shape.GroundedM, Is.GreaterThanOrEqualTo(0));
                        }
        }

        [Test]
        public void VesselData_GroundTackleIsValidated_AndOptional()
        {
            string folder = Path.Combine(Application.dataPath, "ShipSimulator/Data/Vessels");
            var data = JsonUtility.FromJson<VesselData>(File.ReadAllText(Path.Combine(folder, "VolgoDon507B.json")));
            Assert.That(VesselDataValidator.TryValidate(data, out string error), Is.True, error);
            Assert.That(data.groundTackle.fitted, Is.True);
            Assert.That(data.groundTackle.anchors.Length, Is.EqualTo(3));
            Assert.That(data.groundTackle.anchors[0].chainLengthM, Is.EqualTo(150), "Archive p8: 43 mm x 150 m.");
            Assert.That(data.groundTackle.anchors[2].massKg, Is.EqualTo(1250), "Archive p7: stern anchor 1250 kg.");
            data.groundTackle.lineBreakingLoadN = -1;
            Assert.That(VesselDataValidator.TryValidate(data, out error), Is.False);
            Assert.That(error, Does.Contain("Ground tackle"));
            foreach (string file in new[] { "VolgoBalt295AR.json", "Volgoneft1577.json", "Meteor342U.json", "Luch14352.json" })
            {
                var other = JsonUtility.FromJson<VesselData>(File.ReadAllText(Path.Combine(folder, file)));
                Assert.That(VesselDataValidator.TryValidate(other, out error), Is.True, file + ": " + error);
                Assert.That(other.groundTackle == null || !other.groundTackle.fitted, Is.True, file + " keeps no ground tackle.");
            }
        }

        [Test]
        public void Saves_WithoutGroundTackleStillLoad_AndBadTackleIsRejected()
        {
            var old = JsonUtility.FromJson<VoyageSave>("{\"version\":1,\"mechanisms\":{\"chainOutM\":[5,0,0]}}");
            Assert.That(old.groundTackle.present, Is.False);
            Assert.DoesNotThrow(() => old.groundTackle.Validate());
            Assert.That(old.mechanisms.LegacyChainM()[0], Is.EqualTo(5));
            Assert.Throws<InvalidDataException>(() => new GroundTackleSave
            {
                present = true, lines = new[] { new MooringLineSave { shipPointId = "a", shorePointId = "b", lengthM = float.NaN } }
            }.Validate());
            Assert.Throws<InvalidDataException>(() => new GroundTackleSave
            {
                present = true, anchors = new[] { new AnchorSave { id = "port", phase = 9 } }
            }.Validate());
            var good = new GroundTackleSave
            {
                present = true, anchors = new[] { new AnchorSave { id = "port", phase = 2, chainOutM = 40, embedment = 1 } },
                lines = new[] { new MooringLineSave { shipPointId = "stern-port", shorePointId = "gorodets-berth-b1", lengthM = 20, command = 1 } }
            };
            Assert.DoesNotThrow(() => JsonUtility.FromJson<GroundTackleSave>(JsonUtility.ToJson(good)).Validate());
        }
    }
}
