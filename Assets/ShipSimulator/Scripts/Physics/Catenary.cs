using System;
using UnityEngine;

namespace ShipSimulator.Physics
{
    // Shape and end forces of a uniform line hanging in the vertical plane between a low end and a high end.
    // Lengths are along the stretched line, forces in newtons.
    public struct CatenaryShape
    {
        public double HorizontalN;
        // Vertical tension at the high end; the line pulls that end down by this much.
        public double HighVerticalN;
        // Vertical tension at the low end, positive when the line rises from it and so pulls it up.
        public double LowVerticalN;
        // Length lying on the bed from the low end; zero for a line without a bed.
        public double GroundedM;
        public double StretchedM;
        public double SpanM;
        public double HeightM;
        public bool OnBed;
        // Shape parameters: a = H / w, and the line's angle parameter at the low end of the suspended part.
        public double A;
        public double U1;

        public bool Slack => HorizontalN <= 0;
        public double HighTensionN => Math.Sqrt(HorizontalN * HorizontalN + HighVerticalN * HighVerticalN);

        // Height above the low end at a horizontal distance from it, on the stretched shape.
        public double HeightAt(double x)
        {
            if (Slack) return x >= SpanM ? HeightM : 0;
            if (OnBed && GroundedM > 0)
            {
                // The grounded part lies straight along the bed towards the high end.
                double touchdown = SpanM - A * Acosh(1 + HeightM / A);
                return x <= touchdown ? 0 : A * (Math.Cosh(Clamp((x - touchdown) / A)) - 1);
            }
            return A * (Math.Cosh(Clamp(x / A + U1)) - Math.Cosh(Clamp(U1)));
        }

        private static double Clamp(double value) => Math.Max(-50, Math.Min(50, value));
        internal static double Acosh(double value) => Math.Log(value + Math.Sqrt(value * value - 1));
    }

    // Quasi-static elastic catenary. The shape follows the end positions at once, adequate while the ends move
    // slowly against waves running along the line. Stretch uses the horizontal tension as the mean tension,
    // which slightly underestimates it on steep lines. A line on a bed may lie partly on it from the low end;
    // a line without one hangs freely between its ends.
    public static class Catenary
    {
        private const int Iterations = 60;

        public static CatenaryShape Solve(double length, double span, double height, double weightPerMetreN,
            double axialStiffnessN, bool lowEndOnBed)
        {
            length = Math.Max(length, 1e-3);
            span = Math.Max(span, 0);
            height = Math.Max(height, 0);
            double w = Math.Max(weightPerMetreN, 1e-3);
            double ea = Math.Max(axialStiffnessN, 1);
            var shape = new CatenaryShape { SpanM = span, HeightM = height, OnBed = lowEndOnBed, StretchedM = length };

            // Without tension the line hangs straight down from the high end, the rest lying on the bed.
            double slackSpan = lowEndOnBed ? length - height : 0;
            if (span <= slackSpan) return Slack(shape, length, height, w);

            double low = 0, high = Math.Max(w * length, 1);
            for (int i = 0; i < 200 && SpanAt(high, length, height, w, ea, lowEndOnBed) < span; i++) high *= 2;
            for (int i = 0; i < Iterations; i++)
            {
                double middle = 0.5 * (low + high);
                if (SpanAt(middle, length, height, w, ea, lowEndOnBed) < span) low = middle;
                else high = middle;
            }
            double h = high;
            // At the edge of slack the shape parameter vanishes and cosh loses all precision; that is slack.
            if (h < 1e-9 * w * length) return Slack(shape, length, height, w);
            double stretched = length * (1 + h / ea);
            double a = h / w;
            shape.HorizontalN = h;
            shape.StretchedM = stretched;
            shape.A = a;
            double suspended = Math.Sqrt(height * height + 2 * height * a);
            if (lowEndOnBed && suspended <= stretched)
            {
                shape.GroundedM = stretched - suspended;
                shape.HighVerticalN = w * suspended;
                shape.U1 = 0;
                return shape;
            }
            double m = Atanh(Math.Min(height / stretched, 1 - 1e-12));
            double d = span / (2 * a);
            shape.U1 = m - d;
            shape.HighVerticalN = h * Math.Sinh(Math.Min(m + d, 50));
            shape.LowVerticalN = h * Math.Sinh(Math.Max(Math.Min(m - d, 50), -50));
            return shape;
        }

        private static CatenaryShape Slack(CatenaryShape shape, double length, double height, double w)
        {
            shape.HorizontalN = 0;
            shape.HighVerticalN = w * Math.Min(height, length);
            shape.GroundedM = shape.OnBed ? Math.Max(0, length - height) : 0;
            return shape;
        }

        // Horizontal span a line takes up at horizontal tension h; it grows with h, which the solve relies on.
        private static double SpanAt(double h, double length, double height, double w, double ea, bool bed)
        {
            double stretched = length * (1 + h / ea);
            double a = h / w;
            if (a <= 0) return bed ? length - height : 0;
            if (bed)
            {
                double suspended = Math.Sqrt(height * height + 2 * height * a);
                if (suspended <= stretched) return stretched - suspended + a * CatenaryShape.Acosh(1 + height / a);
            }
            if (stretched <= height) return 0;
            double chord = Math.Sqrt(stretched * stretched - height * height);
            return 2 * a * Asinh(chord / (2 * a));
        }

        // Horizontal stiffness of the line at its present span, for damping that scales with how taut it is.
        public static double HorizontalStiffness(in CatenaryShape shape, double length, double weightPerMetreN,
            double axialStiffnessN)
        {
            if (shape.Slack) return 0;
            const double step = 0.01;
            CatenaryShape further = Solve(length, shape.SpanM + step, shape.HeightM, weightPerMetreN, axialStiffnessN, shape.OnBed);
            return Math.Max(0, (further.HorizontalN - shape.HorizontalN) / step);
        }

        private static double Asinh(double value) => Math.Log(value + Math.Sqrt(value * value + 1));
        private static double Atanh(double value) => 0.5 * Math.Log((1 + value) / (1 - value));

        public static Vector3 Point(in CatenaryShape shape, Vector3 lowEnd, Vector3 highEnd, float fraction)
        {
            Vector3 across = highEnd - lowEnd;
            across.y = 0;
            double x = fraction * shape.SpanM;
            float y = (float)shape.HeightAt(x);
            // The shape was solved at the physics step; spread any change in end height since then linearly.
            float correction = fraction * ((highEnd.y - lowEnd.y) - (float)shape.HeightM);
            return lowEnd + across * fraction + Vector3.up * (y + correction);
        }
    }
}
