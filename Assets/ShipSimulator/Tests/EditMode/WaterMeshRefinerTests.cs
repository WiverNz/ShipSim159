using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class WaterMeshRefinerTests
    {
        [Test]
        public void Subdivide_SplitsTrianglesIntoSmallerCoplanarTrianglesWithTheSameWinding()
        {
            var quad = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 9f),
                    new Vector3(9f, 0f, 9f), new Vector3(9f, 0f, 0f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            Mesh refined = WaterMeshRefiner.Subdivide(quad, 3);
            try
            {
                int[] triangles = refined.triangles;
                Vector3[] vertices = refined.vertices;
                Assert.That(triangles.Length, Is.EqualTo(2 * 9 * 3));

                float area = 0f;
                float longestAllowedEdge = 9f * Mathf.Sqrt(2f) / 3f + 0.001f;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    Assert.That(normal.y, Is.GreaterThan(0f), "Every triangle must keep the upward winding.");
                    Assert.That(Mathf.Max((b - a).magnitude, (c - b).magnitude, (a - c).magnitude),
                        Is.LessThanOrEqualTo(longestAllowedEdge));
                    area += normal.magnitude * 0.5f;
                }
                Assert.That(area, Is.EqualTo(81f).Within(0.01f), "Triangles must cover the quad without overlap.");
                foreach (Vector3 vertex in vertices) Assert.That(vertex.y, Is.EqualTo(0f));
                Assert.That(refined.bounds.Contains(new Vector3(4.5f, 3f, 4.5f)), Is.True,
                    "Bounds must leave headroom for displaced waves.");
            }
            finally
            {
                Object.DestroyImmediate(quad);
                Object.DestroyImmediate(refined);
            }
        }
    }
}
