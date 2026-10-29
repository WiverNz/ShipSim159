using System.Collections.Generic;
using ShipSimulator.UI;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Editor
{
    public static class MapChartBuilder
    {
        public static void Build()
        {
            var geometry = ScenarioGeometry.Load("Assets/ShipSimulator/Data/Scenarios/SerpukhovZaton.json");
            const string folder = "Assets/ShipSimulator/Resources/Charts";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/ShipSimulator/Resources", "Charts");
            const string path = folder + "/SerpukhovZatonScene.asset";
            var chart = AssetDatabase.LoadAssetAtPath<MapChartData>(path);
            if (chart == null) { chart = ScriptableObject.CreateInstance<MapChartData>(); AssetDatabase.CreateAsset(chart, path); }
            chart.northHeading = -geometry.frame.upRiverBearingDeg;
            var lines = new List<Vector3>();
            for (int s = 0; s < geometry.shorelines.Length; s++)
            {
                var points = geometry.shorelines[s].points;
                for (int i = 0; i < points.Length; i++)
                    if (geometry.IsShoreEdge(s, i))
                    { lines.Add(points[(i + points.Length - 1) % points.Length].ToWorld()); lines.Add(points[i].ToWorld()); }
            }
            chart.shoreSegments = lines.ToArray();
            EditorUtility.SetDirty(chart);
            AssetDatabase.SaveAssets();
        }
    }
}
