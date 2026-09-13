using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShipSimulator.Editor
{
    // Accumulates generated geometry into one mesh with a submesh per material. Faces are wound from an
    // outward hint, so callers never reason about Unity's clockwise front faces.
    internal sealed class ProceduralMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int>[] triangles;

        public ProceduralMesh(int submeshCount)
        {
            triangles = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++) triangles[i] = new List<int>();
        }

        public int VertexCount => vertices.Count;

        public Vector3 Position(int index) => vertices[index];

        public int Vertex(Vector3 position)
        {
            vertices.Add(position);
            uvs.Add(new Vector2(0.25f * (position.z + position.x), 0.25f * position.y));
            return vertices.Count - 1;
        }

        public void Triangle(int submesh, int a, int b, int c, Vector3 outward)
        {
            Vector3 normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
            if (Vector3.Dot(normal, outward) < 0f) (b, c) = (c, b);
            triangles[submesh].Add(a);
            triangles[submesh].Add(b);
            triangles[submesh].Add(c);
        }

        // Shared-vertex quad, smoothed with its neighbours.
        public void Quad(int submesh, int a, int b, int c, int d, Vector3 outward)
        {
            Triangle(submesh, a, b, c, outward);
            Triangle(submesh, a, c, d, outward);
        }

        // Quad with its own vertices, so it keeps a hard edge.
        public void FlatQuad(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            Quad(submesh, Vertex(a), Vertex(b), Vertex(c), Vertex(d), outward);
        }

        public void Box(int submesh, Vector3 center, Vector3 size) => Box(submesh, center, size, Quaternion.identity);

        public void Box(int submesh, Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 half = size * 0.5f;
            Vector3 Corner(float x, float y, float z) => center + rotation * new Vector3(x * half.x, y * half.y, z * half.z);
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;
            Vector3 forward = rotation * Vector3.forward;
            FlatQuad(submesh, Corner(1, -1, -1), Corner(1, 1, -1), Corner(1, 1, 1), Corner(1, -1, 1), right);
            FlatQuad(submesh, Corner(-1, -1, -1), Corner(-1, 1, -1), Corner(-1, 1, 1), Corner(-1, -1, 1), -right);
            FlatQuad(submesh, Corner(-1, 1, -1), Corner(1, 1, -1), Corner(1, 1, 1), Corner(-1, 1, 1), up);
            FlatQuad(submesh, Corner(-1, -1, -1), Corner(1, -1, -1), Corner(1, -1, 1), Corner(-1, -1, 1), -up);
            FlatQuad(submesh, Corner(-1, -1, 1), Corner(1, -1, 1), Corner(1, 1, 1), Corner(-1, 1, 1), forward);
            FlatQuad(submesh, Corner(-1, -1, -1), Corner(1, -1, -1), Corner(1, 1, -1), Corner(-1, 1, -1), -forward);
        }

        // Box whose long axis runs between two points, for rails and struts.
        public void Beam(int submesh, Vector3 from, Vector3 to, float width, float height)
        {
            Vector3 axis = to - from;
            if (axis.sqrMagnitude < 1e-6f) return;
            Quaternion rotation = Quaternion.LookRotation(axis, Mathf.Abs(axis.normalized.y) > 0.95f ? Vector3.forward : Vector3.up);
            Box(submesh, (from + to) * 0.5f, new Vector3(width, height, axis.magnitude), rotation);
        }

        public void Cylinder(int submesh, Vector3 from, Vector3 to, float radius, int segments = 10, bool caps = true)
        {
            Vector3 axis = to - from;
            if (axis.sqrMagnitude < 1e-6f) return;
            Vector3 direction = axis.normalized;
            Vector3 side = Vector3.Cross(direction, Mathf.Abs(direction.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 other = Vector3.Cross(direction, side);
            var bottom = new int[segments];
            var top = new int[segments];
            var offsets = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 2f * Mathf.PI / segments;
                offsets[i] = (side * Mathf.Cos(angle) + other * Mathf.Sin(angle)) * radius;
                bottom[i] = Vertex(from + offsets[i]);
                top[i] = Vertex(to + offsets[i]);
            }
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                Quad(submesh, bottom[i], bottom[j], top[j], top[i], offsets[i] + offsets[j]);
            }
            if (!caps) return;
            int startCenter = Vertex(from);
            int endCenter = Vertex(to);
            var startRing = new int[segments];
            var endRing = new int[segments];
            for (int i = 0; i < segments; i++)
            {
                startRing[i] = Vertex(from + offsets[i]);
                endRing[i] = Vertex(to + offsets[i]);
            }
            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                Triangle(submesh, startCenter, startRing[i], startRing[j], -direction);
                Triangle(submesh, endCenter, endRing[i], endRing[j], direction);
            }
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i, false);
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
