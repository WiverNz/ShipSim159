using System;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Editor
{
    // Surveyed geography for one scenario, held in JSON so a shoreline can be re-extracted from its
    // source without editing scene-building code. All coordinates are local metres in the frame the
    // data file describes.
    [Serializable]
    public sealed class ScenarioGeometry
    {
        [Serializable]
        public struct Point
        {
            public float x;
            public float z;

            public Vector3 ToWorld() => new Vector3(x, 0f, z);
        }

        [Serializable]
        public sealed class IdentityBlock
        {
            public string id;
            public string displayName;
            public string sceneName;
        }

        [Serializable]
        public sealed class FrameBlock
        {
            public double originLatitudeDeg;
            public double originLongitudeDeg;
            public float upRiverBearingDeg;
            public string note;
        }

        [Serializable]
        public sealed class ProvenanceBlock
        {
            public string shorelines;
            public string channelWidths;
            public string depths;
            public string speedLimits;
            public string currents;
            public string hazards;
            public string landmarks;
        }

        [Serializable]
        public sealed class RouteSample
        {
            public float x;
            public float z;
            public float leftWidthM;
            public float rightWidthM;
            public float centerDepthM;
            public float leftEdgeDepthM;
            public float rightEdgeDepthM;
            public float speedLimitMps;
        }

        [Serializable]
        public sealed class Shoreline
        {
            public string name;
            public Point[] points;
        }

        [Serializable]
        public sealed class Landmark
        {
            public string name;
            // quay, mooring, building or landmark.
            public string kind;
            public float x;
            public float z;
            public float headingDeg;
            public float lengthM;
            public float widthM;
            public float heightM;
            // Signed distance to the waterline when the generator placed it; negative means afloat.
            public float shoreDistanceM;

            public Vector3 Center => new Vector3(x, 0f, z);
            public Quaternion Rotation => Quaternion.Euler(0f, headingDeg, 0f);
        }

        [Serializable]
        public sealed class CurrentRegion
        {
            public string name;
            public float x;
            public float z;
            public float widthM;
            public float lengthM;
            public float velocityXMps;
            public float velocityZMps;
            public float blendM;
            public int priority;
            public bool overrideAmbient;
        }

        [Serializable]
        public sealed class Hazard
        {
            public string name;
            // Silt, Sand or Rock.
            public string bottom;
            public float x;
            public float z;
            public float widthM;
            public float lengthM;
            public float depthReductionM;
        }

        public IdentityBlock identity;
        public FrameBlock frame;
        public ProvenanceBlock provenance;
        public float ambientCurrentZMps;
        public RouteSample[] route;
        public Shoreline[] shorelines;
        public Landmark[] landmarks;
        public CurrentRegion[] currentRegions;
        public Hazard[] hazards;

        public static ScenarioGeometry Load(string assetPath)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (asset == null)
                throw new InvalidOperationException("Scenario geometry is missing: " + assetPath);
            ScenarioGeometry geometry = JsonUtility.FromJson<ScenarioGeometry>(asset.text);
            if (geometry == null || geometry.route == null || geometry.route.Length < 2 ||
                geometry.shorelines == null || geometry.shorelines.Length == 0)
                throw new InvalidOperationException("Scenario geometry is incomplete: " + assetPath);
            return geometry;
        }

        public Shoreline Water(string shorelineName)
        {
            foreach (Shoreline shoreline in shorelines)
                if (shoreline != null && shoreline.name == shorelineName) return shoreline;
            throw new InvalidOperationException("No shoreline named " + shorelineName);
        }

        public Bounds WaterBounds()
        {
            var bounds = new Bounds(shorelines[0].points[0].ToWorld(), Vector3.zero);
            foreach (Shoreline shoreline in shorelines)
            foreach (Point point in shoreline.points)
                bounds.Encapsulate(point.ToWorld());
            return bounds;
        }

        // Signed horizontal distance to the water edge: negative inside the water, positive on land.
        public float SignedShoreDistance(float x, float z)
        {
            bool[][] boundary = BoundaryEdges();
            float nearest = float.PositiveInfinity;
            bool inside = false;
            for (int s = 0; s < shorelines.Length; s++)
            {
                Point[] points = shorelines[s].points;
                bool crossings = false;
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    if (points[i].z > z != points[j].z > z &&
                        x < (points[j].x - points[i].x) * (z - points[i].z) /
                        (points[j].z - points[i].z) + points[i].x)
                        crossings = !crossings;
                    if (boundary[s][i])
                        nearest = Mathf.Min(nearest, SegmentDistance(x, z, points[j], points[i]));
                }
                inside |= crossings;
            }
            return inside ? -nearest : nearest;
        }

        // Water bodies that meet, such as the basin entrance and the Nara mouth, share edges that are
        // not banks. Measuring to them would raise a sill across every junction, so an edge counts only
        // when the water does not continue on its far side.
        private bool[][] BoundaryEdges()
        {
            if (boundaryEdges != null) return boundaryEdges;
            boundaryEdges = new bool[shorelines.Length][];
            for (int s = 0; s < shorelines.Length; s++)
            {
                Point[] points = shorelines[s].points;
                boundaryEdges[s] = new bool[points.Length];
                for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                {
                    float mx = (points[i].x + points[j].x) * 0.5f;
                    float mz = (points[i].z + points[j].z) * 0.5f;
                    float dx = points[i].x - points[j].x;
                    float dz = points[i].z - points[j].z;
                    float length = Mathf.Sqrt(dx * dx + dz * dz);
                    if (length < 1e-4f) continue;
                    float nx = -dz / length * 0.75f;
                    float nz = dx / length * 0.75f;
                    bool plusInside = Contains(points, mx + nx, mz + nz);
                    float ox = plusInside ? mx - nx : mx + nx;
                    float oz = plusInside ? mz - nz : mz + nz;
                    boundaryEdges[s][i] = !InsideOther(s, ox, oz);
                }
            }
            return boundaryEdges;
        }

        private bool[][] boundaryEdges;

        private bool InsideOther(int skip, float x, float z)
        {
            for (int s = 0; s < shorelines.Length; s++)
                if (s != skip && Contains(shorelines[s].points, x, z)) return true;
            return false;
        }

        private static bool Contains(Point[] points, float x, float z)
        {
            bool crossings = false;
            for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
                if (points[i].z > z != points[j].z > z &&
                    x < (points[j].x - points[i].x) * (z - points[i].z) /
                    (points[j].z - points[i].z) + points[i].x)
                    crossings = !crossings;
            return crossings;
        }

        private static float SegmentDistance(float x, float z, Point a, Point b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            float lengthSqr = dx * dx + dz * dz;
            float t = lengthSqr > 1e-6f
                ? Mathf.Clamp01(((x - a.x) * dx + (z - a.z) * dz) / lengthSqr)
                : 0f;
            float ox = x - (a.x + dx * t);
            float oz = z - (a.z + dz * t);
            return Mathf.Sqrt(ox * ox + oz * oz);
        }
    }
}
