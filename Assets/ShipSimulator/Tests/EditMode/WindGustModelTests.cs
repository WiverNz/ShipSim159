using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class WindGustModelTests
    {
        private const double Step = 0.02;

        [Test]
        public void Gusts_StayBoundedAndChangeSmoothly()
        {
            float previousDirection = WindGustModel.DirectionOffsetDeg(0);
            float previousSpeed = WindGustModel.SpeedFactor(0);
            float maxDirection = WindGustModel.MaxShiftDeg + WindGustModel.MeanderDeg;
            for (double time = Step; time < 3600; time += Step)
            {
                float direction = WindGustModel.DirectionOffsetDeg(time);
                float speed = WindGustModel.SpeedFactor(time);
                Assert.That(Mathf.Abs(direction), Is.LessThanOrEqualTo(maxDirection));
                Assert.That(speed, Is.InRange(WindGustModel.MinSpeedFactor, WindGustModel.MaxSpeedFactor));
                Assert.That(Mathf.Abs(direction - previousDirection), Is.LessThan(0.2f), $"Direction jumped at {time:F2} s.");
                Assert.That(Mathf.Abs(speed - previousSpeed), Is.LessThan(0.02f), $"Speed jumped at {time:F2} s.");
                previousDirection = direction;
                previousSpeed = speed;
            }
        }

        [Test]
        public void Wind_ShiftsDirectionOccasionally()
        {
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            for (double time = 0; time < 1200; time += 1)
            {
                float direction = WindGustModel.DirectionOffsetDeg(time);
                lowest = Mathf.Min(lowest, direction);
                highest = Mathf.Max(highest, direction);
            }
            Assert.That(highest - lowest, Is.GreaterThan(15f));
        }

        [Test]
        public void VisualWind_IsDeterministicAndNeverDeadCalm()
        {
            Assert.That(WindGustModel.VisualWind(412.5, 90, 6), Is.EqualTo(WindGustModel.VisualWind(412.5, 90, 6)));
            for (double time = 0; time < 600; time += 0.5)
                Assert.That(WindGustModel.VisualWind(time, 0, 0).magnitude, Is.GreaterThan(0.5f));
        }

        [Test]
        public void VisualWind_BlowsRoughlyFromTheConfiguredDirection()
        {
            Vector3 mean = WeatherController.CalculateWindVelocity(250, 8).normalized;
            for (double time = 0; time < 1200; time += 3)
            {
                Vector3 gusting = WindGustModel.VisualWind(time, 250, 8).normalized;
                Assert.That(Vector3.Angle(mean, gusting),
                    Is.LessThanOrEqualTo(WindGustModel.MaxShiftDeg + WindGustModel.MeanderDeg + 0.01f));
            }
        }
    }
}
