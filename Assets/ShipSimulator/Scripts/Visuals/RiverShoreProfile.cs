using System.Collections.Generic;
using UnityEngine;

namespace ShipSimulator.Visuals
{
    // Visual waterline sampled from the generated bank meshes, independent of navigable depth.
    public sealed class RiverShoreProfile : MonoBehaviour
    {
        public static RiverShoreProfile Active { get; private set; }
        private Texture2D profile;
        private Color[] samples;
        private float startZ, lengthZ;
        public bool IsReady => profile != null;

        private void OnEnable()
        {
            Active = this;
            Build();
        }

        public void Build()
        {
            var left = ReadBank("Left natural bank");
            var right = ReadBank("Right natural bank");
            if (left.Count < 2 || right.Count < 2) return;
            startZ = Mathf.Max(left[0].y, right[0].y);
            float end = Mathf.Min(left[left.Count - 1].y, right[right.Count - 1].y);
            lengthZ = end - startZ;
            if (lengthZ <= 0) return;
            const int size = 2048;
            samples = new Color[size];
            for (int i = 0; i < size; i++)
            {
                float z = startZ + lengthZ * i / (size - 1);
                samples[i] = new Color(At(left, z), At(right, z), 0, 1);
            }
            if (profile != null)
            {
                if (Application.isPlaying) Destroy(profile); else DestroyImmediate(profile);
            }
            profile = new Texture2D(size, 1, TextureFormat.RGBAFloat, false, true)
            { name = "Actual bank waterlines", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            profile.SetPixels(samples);
            profile.Apply();
            Shader.SetGlobalTexture("_RiverShoreProfile", profile);
            Shader.SetGlobalVector("_RiverShoreRange", new Vector4(startZ, lengthZ, size, 1));
        }

        public float DistanceToBank(Vector3 position)
        {
            if (!IsReady || position.z < startZ || position.z > startZ + lengthZ) return -1;
            float index = (position.z - startZ) / lengthZ * (samples.Length - 1);
            int a = Mathf.FloorToInt(index);
            Color banks = Color.Lerp(samples[a], samples[Mathf.Min(a + 1, samples.Length - 1)], index - a);
            return Mathf.Min(position.x - banks.r, banks.g - position.x);
        }

        private static List<Vector2> ReadBank(string name)
        {
            var rows = new List<Vector2>();
            foreach (MeshFilter filter in FindObjectsByType<MeshFilter>())
            {
                if (filter.name != name || filter.sharedMesh == null) continue;
                Vector3[] vertices = filter.sharedMesh.vertices;
                for (int i = 1; i < vertices.Length; i++)
                {
                    Vector3 a = filter.transform.TransformPoint(vertices[i - 1]);
                    Vector3 b = filter.transform.TransformPoint(vertices[i]);
                    if (Mathf.Abs(a.z - b.z) > 0.01f || a.y == b.y || a.y * b.y > 0) continue;
                    Vector3 crossing = Vector3.Lerp(a, b, -a.y / (b.y - a.y));
                    rows.Add(new Vector2(crossing.x, crossing.z));
                }
            }
            rows.Sort((a, b) => a.y.CompareTo(b.y));
            return rows;
        }

        private static float At(List<Vector2> rows, float z)
        {
            int low = 0, high = rows.Count - 1;
            while (high - low > 1)
            {
                int mid = (low + high) / 2;
                if (rows[mid].y <= z) low = mid; else high = mid;
            }
            return Mathf.Lerp(rows[low].x, rows[high].x, Mathf.InverseLerp(rows[low].y, rows[high].y, z));
        }

        private void OnDisable()
        {
            if (Active != this) return;
            Active = null;
            Shader.SetGlobalVector("_RiverShoreRange", Vector4.zero);
        }

        private void OnDestroy()
        {
            if (profile != null)
            {
                if (Application.isPlaying) Destroy(profile); else DestroyImmediate(profile);
            }
        }
    }
}
