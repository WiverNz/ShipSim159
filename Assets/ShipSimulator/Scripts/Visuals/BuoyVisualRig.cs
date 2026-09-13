using System.Collections.Generic;
using UnityEngine;

namespace ShipSimulator.Visuals
{
    public sealed class BuoyVisualRig : MonoBehaviour
    {
        private readonly List<Object> owned = new List<Object>();
        private Transform floating;
        private float phase;
        public Transform Floating => floating;

        public void Build(bool red)
        {
            if (floating != null) return;
            foreach (Renderer old in GetComponentsInChildren<Renderer>()) old.enabled = false;
            floating = new GameObject("Detailed buoy").transform;
            floating.SetParent(transform, false);
            phase = Mathf.Repeat(transform.position.x * 0.37f + transform.position.z * 0.13f, 6.28f);
            Material paint = Material(red ? new Color(0.55f, 0.045f, 0.025f) : new Color(0.73f, 0.72f, 0.62f), 0.25f);
            Material iron = Material(new Color(0.07f, 0.085f, 0.075f), 0.4f);
            Material glass = Material(red ? new Color(0.18f, 0.025f, 0.015f) : new Color(0.025f, 0.12f, 0.07f), 0.75f);
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, true, true);
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.19f, y * 0.31f);
                float streak = Mathf.PerlinNoise(x * 0.4f, y * 0.025f);
                float rust = Mathf.SmoothStep(0, 1, Mathf.Clamp01((noise * streak - 0.39f) * 8));
                Color value = Color.Lerp(Color.white * (0.78f + noise * 0.22f), new Color(0.3f, 0.16f, 0.055f), rust);
                value.a = 1; pixels[y * 128 + x] = value;
            }
            texture.SetPixels(pixels); texture.Apply(); owned.Add(texture);
            paint.SetTexture("_BaseMap", texture);
            Lathe("Steel float", new[] { new Vector2(0.35f,-0.65f), new Vector2(0.85f,-0.25f),
                new Vector2(0.87f,0.12f), new Vector2(0.76f,0.6f), new Vector2(0.48f,0.9f), new Vector2(0,0.9f) }, paint);
            Lathe("Wet waterline belt", new[] { new Vector2(0.875f,-0.18f), new Vector2(0.875f,0.1f) }, iron);
            Lathe("Deck rim", new[] { new Vector2(0.48f,0.88f), new Vector2(0.5f,0.96f), new Vector2(0.38f,0.96f) }, iron);
            var pieces = new List<MeshFilter>();
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                Vector3 bottom = new Vector3(Mathf.Cos(a) * 0.36f, 0.94f, Mathf.Sin(a) * 0.36f);
                Vector3 top = new Vector3(Mathf.Cos(a) * 0.2f, 2.75f, Mathf.Sin(a) * 0.2f);
                pieces.Add(Strut(bottom, top, 0.045f));
                float b = (i + 1) * Mathf.PI * 0.5f;
                pieces.Add(Strut(bottom + Vector3.up * 0.25f,
                    new Vector3(Mathf.Cos(b) * 0.23f, 2.35f, Mathf.Sin(b) * 0.23f), 0.025f));
            }
            Combine(pieces, paint);
            Lathe("Lantern base", new[] { new Vector2(0.27f,2.7f), new Vector2(0.27f,2.83f), new Vector2(0.19f,2.86f) }, iron);
            Lathe("Lantern glass", new[] { new Vector2(0.19f,2.86f), new Vector2(0.19f,3.14f) }, glass);
            Lathe("Lantern cap", new[] { new Vector2(0.23f,3.14f), new Vector2(0.25f,3.19f), new Vector2(0,3.24f) }, iron);
            for (int i = 0; i < 4; i++)
                Lathe("Lens rib", new[] { new Vector2(0.195f,2.9f+i*0.055f), new Vector2(0.195f,2.91f+i*0.055f) }, glass);
            foreach (Material material in new[] { paint, iron, glass })
            {
                var group = new List<MeshFilter>();
                foreach (MeshFilter filter in floating.GetComponentsInChildren<MeshFilter>())
                    if (filter.gameObject.activeSelf && filter.GetComponent<Renderer>().sharedMaterial == material) group.Add(filter);
                Combine(group, material, material == glass ? "Lantern glass" : material == paint ? "Painted steel" : "Hardware");
            }
        }

        private Material Material(Color color, float smoothness)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0.35f);
            owned.Add(material); return material;
        }

        private void Lathe(string name, Vector2[] profile, Material material)
        {
            const int segments = 32;
            var vertices = new Vector3[profile.Length * (segments + 1)];
            var uv = new Vector2[vertices.Length];
            var indices = new List<int>();
            for (int row = 0; row < profile.Length; row++)
            for (int side = 0; side <= segments; side++)
            {
                int index = row * (segments + 1) + side;
                float angle = side * 2 * Mathf.PI / segments;
                vertices[index] = new Vector3(Mathf.Cos(angle) * profile[row].x, profile[row].y, Mathf.Sin(angle) * profile[row].x);
                uv[index] = new Vector2((float)side / segments, profile[row].y * 0.4f);
                if (row == 0 || side == segments) continue;
                int below = index - segments - 1;
                indices.AddRange(new[] { below, index, index + 1, below, index + 1, below + 1 });
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = vertices; mesh.uv = uv; mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            owned.Add(mesh);
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(floating, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private MeshFilter Strut(Vector3 a, Vector3 b, float radius)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Collider collider = part.GetComponent<Collider>(); collider.enabled = false; Release(collider);
            part.transform.SetParent(floating, false);
            part.transform.localPosition = (a + b) * 0.5f;
            part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            part.transform.localScale = new Vector3(radius * 2, (b - a).magnitude * 0.5f, radius * 2);
            return part.GetComponent<MeshFilter>();
        }

        private void Combine(List<MeshFilter> pieces, Material material, string name = "Braced tower")
        {
            var entries = new CombineInstance[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
            {
                entries[i] = new CombineInstance { mesh = pieces[i].sharedMesh,
                    transform = floating.worldToLocalMatrix * pieces[i].transform.localToWorldMatrix };
                pieces[i].gameObject.SetActive(false); Release(pieces[i].gameObject);
            }
            var mesh = new Mesh { name = "Buoy frame" }; mesh.CombineMeshes(entries); owned.Add(mesh);
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(floating, false);
            part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void Update()
        {
            if (floating == null) return;
            float time = Time.time;
            floating.localPosition = Vector3.up * (Mathf.Sin(time * 1.35f + phase) * 0.035f + Mathf.Sin(time * 0.73f + phase) * 0.02f);
            floating.localRotation = Quaternion.Euler(Mathf.Sin(time * 0.83f + phase) * 1.5f, 0, Mathf.Sin(time * 1.03f + phase) * 1.8f);
        }
        private static void Release(Object item) { if (Application.isPlaying) Destroy(item); else DestroyImmediate(item); }
        private void OnDestroy() { foreach (Object item in owned) if (item != null) Release(item); }
    }
}
