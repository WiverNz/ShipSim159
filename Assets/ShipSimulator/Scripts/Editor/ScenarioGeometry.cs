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
            public string elevation;
            public string landcover;
            public string buildings;
            public string moorings;
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
            // Only "quay" is used now; buildings ashore come from the buildings block instead.
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

        [Serializable]
        public sealed class ElevationBlock
        {
            public float originX;
            public float originZ;
            public float stepM;
            public int columns;
            public int rows;
            public float demWaterLevelM;
            public string note;
            // Row-major, z outer and x inner, in metres above the river surface.
            public float[] heightsM;
        }

        [Serializable]
        public sealed class LandCover
        {
            // wood, scrub, meadow, farmland, orchard, allotments, built, sand or reeds.
            public string cover;
            public string name;
            public Point[] points;
        }

        [Serializable]
        public sealed class Building
        {
            public string name;
            // pitched, flat or church.
            public string roof;
            public float x;
            public float z;
            public float headingDeg;
            public float lengthM;
            public float widthM;
            public float heightM;
        }

        [Serializable]
        public sealed class Mooring
        {
            public string name;
            // barge, vessel, craft, dock or pier.
            public string kind;
            public float x;
            public float z;
            public float headingDeg;
            public float lengthM;
            public float widthM;

            public Vector3 Center => new Vector3(x, 0f, z);
            public Quaternion Rotation => Quaternion.Euler(0f, headingDeg, 0f);
        }

        public IdentityBlock identity;
        public FrameBlock frame;
        public ProvenanceBlock provenance;
        public ElevationBlock elevation;
        public LandCover[] landcover;
        public Building[] buildings;
        public Mooring[] moorings;
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

        // Terrain height above the river surface, bilinear between the sampled elevation posts.
        public float ElevationAbove(float x, float z)
        {
            if (elevation == null || elevation.heightsM == null ||
                elevation.heightsM.Length != elevation.columns * elevation.rows)
                return 0f;
            float fx = Mathf.Clamp((x - elevation.originX) / elevation.stepM, 0f, elevation.columns - 1.001f);
            float fz = Mathf.Clamp((z - elevation.originZ) / elevation.stepM, 0f, elevation.rows - 1.001f);
            int cx = (int)fx;
            int cz = (int)fz;
            float tx = fx - cx;
            float tz = fz - cz;
            float a = Post(cx, cz);
            float b = Post(cx + 1, cz);
            float c = Post(cx, cz + 1);
            float d = Post(cx + 1, cz + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        private float Post(int column, int row)
        {
            column = Mathf.Clamp(column, 0, elevation.columns - 1);
            row = Mathf.Clamp(row, 0, elevation.rows - 1);
            return elevation.heightsM[row * elevation.columns + column];
        }

        // The land cover class at a point, or null where nothing is mapped.
        public string CoverAt(float x, float z)
        {
            if (landcover == null) return null;
            Bounds[] boxes = CoverBounds();
            // Built ground wins over the wood it is cut out of, so the last match through the list
            // would be arbitrary; keep the most specific class instead.
            string found = null;
            for (int i = 0; i < landcover.Length; i++)
            {
                if (!boxes[i].Contains(new Vector3(x, 0f, z))) continue;
                if (!Contains(landcover[i].points, x, z)) continue;
                string cover = landcover[i].cover;
                if (found == null || Rank(cover) > Rank(found)) found = cover;
            }
            return found;
        }

        private static int Rank(string cover) => cover switch
        {
            "built" => 5,
            "sand" => 4,
            "allotments" => 3,
            "orchard" => 3,
            "wood" => 2,
            "scrub" => 2,
            "reeds" => 2,
            _ => 1
        };

        private Bounds[] coverBounds;

        private Bounds[] CoverBounds()
        {
            if (coverBounds != null) return coverBounds;
            coverBounds = new Bounds[landcover.Length];
            for (int i = 0; i < landcover.Length; i++)
            {
                Point[] points = landcover[i].points;
                var bounds = new Bounds(points[0].ToWorld(), new Vector3(0f, 10f, 0f));
                foreach (Point point in points) bounds.Encapsulate(point.ToWorld());
                coverBounds[i] = bounds;
            }
            return coverBounds;
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
