using UnityEngine;
using UnityEngine.Rendering;

namespace ShipSimulator.Visuals
{
    public static class WaterMeshRefiner
    {
        // Ship waves lift the surface above the flat stored mesh; keep it inside the bounds.
        private const float WaveHeadroomM = 4f;

        // Splits every triangle edge into `divisions` parts, preserving winding. Only positions
        // are kept: RiverWater.shader derives everything else from world position.
        public static Mesh Subdivide(Mesh source, int divisions)
        {
            if (source == null || !source.isReadable || divisions < 1) return null;

            Vector3[] vertices = source.vertices;
            int[] triangles = source.triangles;
            int triangleCount = triangles.Length / 3;
            int verticesPerTriangle = (divisions + 1) * (divisions + 2) / 2;
            var positions = new Vector3[triangleCount * verticesPerTriangle];
            var indices = new int[triangleCount * divisions * divisions * 3];
            int vertex = 0;
            int index = 0;
            float step = 1f / divisions;

            for (int triangle = 0; triangle < triangles.Length; triangle += 3)
            {
                Vector3 a = vertices[triangles[triangle]];
                Vector3 alongB = vertices[triangles[triangle + 1]] - a;
                Vector3 alongC = vertices[triangles[triangle + 2]] - a;
                int rowStart = vertex;
                for (int row = 0; row <= divisions; row++)
                for (int column = 0; column <= divisions - row; column++)
                    positions[vertex++] = a + alongB * (column * step) + alongC * (row * step);

                for (int row = 0; row < divisions; row++)
                {
                    int rowLength = divisions + 1 - row;
                    int nextStart = rowStart + rowLength;
                    for (int column = 0; column < rowLength - 1; column++)
                    {
                        int corner = rowStart + column;
                        int above = nextStart + column;
                        indices[index++] = corner;
                        indices[index++] = corner + 1;
                        indices[index++] = above;
                        if (column >= rowLength - 2) continue;
                        indices[index++] = corner + 1;
                        indices[index++] = above + 1;
                        indices[index++] = above;
                    }
                    rowStart = nextStart;
                }
            }

            var mesh = new Mesh
            {
                name = source.name + " (ship wave detail)",
                indexFormat = IndexFormat.UInt32
            };
            mesh.SetVertices(positions);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            Bounds bounds = source.bounds;
            bounds.Expand(new Vector3(0f, WaveHeadroomM * 2f, 0f));
            mesh.bounds = bounds;
            return mesh;
        }
    }
}
