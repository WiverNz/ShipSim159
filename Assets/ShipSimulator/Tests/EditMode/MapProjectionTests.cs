using System.Collections.Generic;
using NUnit.Framework;
using ShipSimulator.UI;
using ShipSimulator.Editor;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class MapProjectionTests
    {
        [TestCase(0)] [TestCase(90)] [TestCase(183)] [TestCase(-43.663f)]
        public void ProjectionAndInverseShareHeadingScaleAndPan(float heading)
        {
            var center = new Vector3(300, 0, -200);
            var p = new MapProjection(center, heading, .3f, new Vector2(0, -40));
            var point = new Vector3(-123, 0, 456);
            Assert.That(Vector3.Distance(p.Unproject(p.Project(point)), point), Is.LessThan(.001f));
            Vector3 ahead = center + Quaternion.Euler(0, heading, 0) * Vector3.forward * 100;
            Assert.That(Vector2.Distance(p.Project(ahead), new Vector2(0, -10)), Is.LessThan(.001f));
            Assert.That(p.IconAngle(heading), Is.EqualTo(0).Within(.001f));
        }
        [TestCase(100)] [TestCase(300)] [TestCase(4000)]
        public void PredictionFitsCurrentRangeForForwardAndAsternSpeeds(float range)
        {
            float step = MapProjection.PredictionStep(18, 10, range * .55f);
            Assert.That(step * 18 * 10, Is.LessThanOrEqualTo(range * .55f + .001f));
            Assert.That(step, Is.LessThanOrEqualTo(2.5f));
        }
        [Test]
        public void ZatonChartUsesAuthoredNorthAndExposedShoreEdges()
        {
            var data = AssetDatabase.LoadAssetAtPath<MapChartData>("Assets/ShipSimulator/Resources/Charts/SerpukhovZatonScene.asset");
            Assert.That(data, Is.Not.Null);
            var source = ScenarioGeometry.Load("Assets/ShipSimulator/Data/Scenarios/SerpukhovZaton.json");
            Assert.That(data.northHeading, Is.EqualTo(-source.frame.upRiverBearingDeg));
            int edge = 0;
            for (int s = 0; s < source.shorelines.Length; s++)
            {
                var points = source.shorelines[s].points;
                for (int i = 0; i < points.Length; i++)
                    if (source.IsShoreEdge(s, i))
                    {
                        Assert.That(data.shoreSegments[edge++], Is.EqualTo(points[(i + points.Length - 1) % points.Length].ToWorld()));
                        Assert.That(data.shoreSegments[edge++], Is.EqualTo(points[i].ToWorld()));
                    }
            }
            Assert.That(edge, Is.EqualTo(data.shoreSegments.Length));
            Assert.That(edge, Is.GreaterThan(20));
        }
        [Test]
        public void MapPreferencesClampInvalidValuesAndPersistOnlyDisplayChoices()
        {
            const string prefix = "ShipSim159.Settings.";
            string[] flags = { "MapNorthUp", "MapExpanded", "MapLegend" };
            var old = new Dictionary<string, int>();
            foreach (string key in flags) if (PlayerPrefs.HasKey(prefix + key)) old[key] = PlayerPrefs.GetInt(prefix + key);
            bool hadRange = PlayerPrefs.HasKey(prefix + "MapRange");
            float range = PlayerPrefs.GetFloat(prefix + "MapRange");
            try
            {
                VoyageSettings.SetMapRange(float.NaN); Assert.That(VoyageSettings.MapRange, Is.EqualTo(300));
                VoyageSettings.SetMapRange(-10); Assert.That(VoyageSettings.MapRange, Is.EqualTo(100));
                VoyageSettings.SetMapRange(99999); Assert.That(VoyageSettings.MapRange, Is.EqualTo(4000));
                VoyageSettings.SetMapRange(650); Assert.That(PlayerPrefs.GetFloat(prefix + "MapRange"), Is.EqualTo(650));
                VoyageSettings.SetMapNorthUp(true); VoyageSettings.SetMapExpanded(true); VoyageSettings.SetMapLegend(true);
                foreach (string key in flags) Assert.That(PlayerPrefs.GetInt(prefix + key), Is.EqualTo(1));
            }
            finally
            {
                foreach (string key in flags)
                    if (old.TryGetValue(key, out int value)) PlayerPrefs.SetInt(prefix + key, value); else PlayerPrefs.DeleteKey(prefix + key);
                if (hadRange) PlayerPrefs.SetFloat(prefix + "MapRange", range); else PlayerPrefs.DeleteKey(prefix + "MapRange");
                VoyageSettings.Save();
            }
        }
    }
}
