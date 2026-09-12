using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class ShipWakeTrackTests
    {
        private const float HullLength = 100f;
        private readonly Vector4[] points = new Vector4[ShipWakeTrack.Capacity];
        private readonly Vector4[] info = new Vector4[ShipWakeTrack.Capacity];

        [Test]
        public void Record_AddsHistorySamplesAtFixedSpacingThroughWater()
        {
            var track = new ShipWakeTrack();
            for (int metre = 0; metre <= 55; metre++) Sail(track, metre, 5f, Vector2.zero, 0.2f);
            Assert.That(track.HistoryCount, Is.EqualTo(6));
        }

        [Test]
        public void Record_DriftsHistoryWithTheCurrent()
        {
            var track = new ShipWakeTrack();
            for (int step = 0; step < 20; step++) Sail(track, 0f, 0f, new Vector2(0f, 1f), 0.1f);

            int count = track.Write(HullLength, points, info, out _);

            Assert.That(count, Is.EqualTo(3));
            Assert.That(points[2].y, Is.EqualTo(1.9f).Within(0.001f));
            Assert.That(points[2].w, Is.EqualTo(1.9f).Within(0.001f));
        }

        [Test]
        public void Record_ClearsHistoryWhenTheVesselJumps()
        {
            var track = new ShipWakeTrack();
            for (int metre = 0; metre <= 40; metre++) Sail(track, metre, 5f, Vector2.zero, 0.2f);
            Assume.That(track.HistoryCount, Is.EqualTo(5));

            Sail(track, 900f, 5f, Vector2.zero, 0.2f);

            Assert.That(track.HistoryCount, Is.EqualTo(1));
        }

        [Test]
        public void Write_PacksBowSternAndHistoryWithGrowingDistanceAndAge()
        {
            var track = new ShipWakeTrack();
            for (int metre = 0; metre <= 1000; metre++) Sail(track, metre, 5f, Vector2.zero, 0.2f);

            int count = track.Write(HullLength, points, info, out Vector4 bounds);

            Assert.That(count, Is.EqualTo(ShipWakeTrack.Capacity));
            Assert.That(points[0], Is.EqualTo(new Vector4(0f, 1100f, 0f, 0f)));
            Assert.That(points[1], Is.EqualTo(new Vector4(0f, 1000f, HullLength, 0f)));
            Assert.That(info[1].y, Is.EqualTo(0.5f), "The stern carries the propeller wash.");
            for (int i = 2; i < count; i++)
            {
                Assert.That(points[i].z, Is.GreaterThanOrEqualTo(points[i - 1].z));
                Assert.That(points[i].w, Is.GreaterThanOrEqualTo(points[i - 1].w));
            }
            Assert.That(points[count - 1].y, Is.EqualTo(550f), "Only the newest samples are kept.");
            Assert.That(points[count - 1].z, Is.EqualTo(550f).Within(0.5f));
            Assert.That(bounds, Is.EqualTo(new Vector4(0f, 550f, 0f, 1100f)));
        }

        [Test]
        public void Write_LetsAStoppedVesselsWakeKeepSpreading()
        {
            var track = new ShipWakeTrack();
            for (int metre = 0; metre <= 30; metre++) Sail(track, metre, 5f, Vector2.zero, 0.2f);
            for (int step = 0; step < 12; step++) Sail(track, 30f, 0f, Vector2.zero, 0.25f);

            int count = track.Write(HullLength, points, info, out _);

            Assert.That(count, Is.EqualTo(6));
            // The sample left at z = 0 is 130 m behind the bow, but 9 s at 5 m/s puts its waves 145 m out.
            Assert.That(points[count - 1].z, Is.EqualTo(145f).Within(0.05f));
        }

        private static void Sail(ShipWakeTrack track, float sternZ, float speed, Vector2 current, float deltaTime)
        {
            track.Record(new Vector2(0f, sternZ + HullLength), new Vector2(0f, sternZ),
                speed, 0.5f, current, deltaTime);
        }
    }
}
