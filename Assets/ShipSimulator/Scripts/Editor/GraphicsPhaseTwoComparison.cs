using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Editor
{
    public static class GraphicsPhaseTwoComparison
    {
        public static void Run()
        {
            string root = "Logs/GraphicsPhaseTwo/";
            string report = "condition,pose,before_temporal_variance,after_temporal_variance,ratio\n";
            foreach (string condition in new[] { "clear", "dawn", "fog", "rain", "night" })
            foreach (string pose in new[] { "toward-sun", "away-sun" })
            {
                string name = condition + "-" + pose;
                double before = Variance(root + "before/temporal/", name);
                double after = Variance(root + "after/temporal/", name);
                report += condition + "," + pose + "," + Number(before) + "," + Number(after) + "," + Number(after / Math.Max(before, 1e-12)) + "\n";
                if (condition == "clear" && after >= before)
                    throw new InvalidOperationException("Clear-water temporal variance did not improve: " + pose);
                Debug.Log("PHASE_TWO_VARIANCE|" + name + "|ratio=" + Number(after / Math.Max(before, 1e-12)));
            }
            File.WriteAllText(root + "temporal-comparison.csv", report);
        }
        private static string Number(double value) => value.ToString("G8", CultureInfo.InvariantCulture);
        private static double Variance(string directory, string name)
        {
            const int count = 480 * 160;
            var sum = new double[count]; var squares = new double[count];
            var image = new Texture2D(2, 2, TextureFormat.RGB24, false);
            try
            {
                for (int frame = 0; frame < 16; frame++)
                {
                    image.LoadImage(File.ReadAllBytes(directory + name + "-" + frame.ToString("D2") + ".png"));
                    Color32[] pixels = image.GetPixels32();
                    if (pixels.Length != count) throw new InvalidOperationException("Temporal crop dimensions differ.");
                    for (int i = 0; i < count; i++)
                    {
                        double value = (pixels[i].r * 0.2126 + pixels[i].g * 0.7152 + pixels[i].b * 0.0722) / 255;
                        sum[i] += value; squares[i] += value * value;
                    }
                }
                double variance = 0;
                for (int i = 0; i < count; i++) variance += Math.Max(0, squares[i] / 16 - sum[i] * sum[i] / 256);
                return variance / count;
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
