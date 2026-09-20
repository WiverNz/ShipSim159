using System;
using System.Collections.Generic;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    public static class RiverLandscapeBuilder
    {
        private const string Root = "Assets/ShipSimulator/Settings/NaturalLandscape";
        private const float BankRowSpacing = 3f;
        private static readonly float[] BankOffsets = { -16, -10, -6, -3, -1, 0, 1, 2, 4, 7, 11, 16, 28, 48, 80, 130, 210, 340, 520 };

        [MenuItem("Ship Simulator/Upgrade Water And Landscape")]
        public static void ApplyBoth()
        {
            foreach (string name in new[] { "RiverTrainingScene", "GorodetsTrainingScene" })
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity");
                Apply(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("LANDSCAPE|Both river environments upgraded");
        }

        public static void Apply(Scene scene)
        {
            EnsureFolder();
            ShipSimulatorVisualUpgrade.ConfigureLighting(scene);
            var route = Object.FindAnyObjectByType<FairwayRoute>();
            bool gorodets = route != null;
            float start = gorodets ? -600 : -1200;
            float end = gorodets ? 2600 : 1200;
            Transform environment = GameObject.Find("Environment").transform;
            Transform old = environment.Find("Natural Landscape");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            old = environment.Find("VisualUpgrade");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (Transform child in environment)
            {
                if (!child.name.Contains("Bank")) continue;
                foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            }
            var root = new GameObject("Natural Landscape").transform;
            root.SetParent(environment, false);
            Material ground = MaterialAsset("AlluvialGround", "ShipSimulator/RiverGround", Color.white);
            Material bark = MaterialAsset("WillowBark", "ShipSimulator/RiverBark", new Color(0.22f, 0.18f, 0.13f));
            Material birch = MaterialAsset("BirchBark", "ShipSimulator/RiverBark", new Color(0.64f, 0.62f, 0.53f));
            Material foliage = MaterialAsset("RiverLeaves", "ShipSimulator/RiverFoliage", new Color(0.32f, 0.43f, 0.16f));
            Material silver = MaterialAsset("WillowLeaves", "ShipSimulator/RiverFoliage", new Color(0.40f, 0.48f, 0.24f));
            Material reeds = MaterialAsset("ReedLeaves", "ShipSimulator/RiverFoliage", new Color(0.43f, 0.43f, 0.20f));
            for (int side = -1; side <= 1; side += 2)
            {
                Mesh mesh = BankMesh(scene.name + (side < 0 ? "Left" : "Right"), route, side, start, end);
                GameObject bank = MeshObject(side < 0 ? "Left natural bank" : "Right natural bank", root, mesh, ground);
                if (!gorodets)
                {
                    bank.AddComponent<MeshCollider>().sharedMesh = mesh;
                    // Store the cooked collision data in the asset: the player build warns that it
                    // will stop cooking mesh colliders for us.
                    UnityEngine.Physics.BakeMesh(mesh.GetEntityId(), false);
                    EditorUtility.SetDirty(mesh);
                }
            }
            Transform water = environment.Find("RiverWater");
            water.localPosition = Vector3.zero;
            water.localScale = Vector3.one;
            water.localRotation = Quaternion.identity;
            water.gameObject.layer = 4;
            water.GetComponent<MeshFilter>().sharedMesh = WaterMesh(scene.name, route, start, end);
            if (water.GetComponent<RiverPlanarReflection>() == null) water.gameObject.AddComponent<RiverPlanarReflection>();

            GameObject[] trees = new GameObject[6];
            for (int i = 0; i < trees.Length; i++)
                trees[i] = PlantPrefab("Tree" + i, 507 + i * 71, false, false, i % 3 == 1 ? birch : bark, i % 3 == 2 ? silver : foliage);
            GameObject[] bushes = new GameObject[3];
            for (int i = 0; i < bushes.Length; i++)
                bushes[i] = PlantPrefab("Bush" + i, 159 + i * 19, true, false, bark, foliage);
            GameObject reed = PlantPrefab("ReedClump", 902, true, true, bark, reeds);
            var random = new System.Random(gorodets ? 247 : 159);
            int treeCount = 0;
            int bushCount = 0;
            for (float z = start + 25; z < end - 20; z += 14)
            for (int side = -1; side <= 1; side += 2)
            {
                float density = Mathf.PerlinNoise(z * 0.006f + 30, side + 8);
                for (int band = 0; band < 3; band++)
                {
                    if (random.NextDouble() > (band == 0 ? 0.63 : 0.83)) continue;
                    float offset = band == 0 ? Range(random, 15, 52) : band == 1 ? Range(random, 55, 145) : Range(random, 150, 340);
                    float treeZ = z + Range(random, -12, 12);
                    float x = ShoreX(route, treeZ, side) + side * offset;
                    // Keep the existing leading marks clear of nearby vegetation.
                    if (gorodets && side > 0 && NearLeadingMark(new Vector3(x, 0, treeZ))) continue;
                    Place(trees[random.Next(trees.Length)], root, new Vector3(x, SurfaceHeight(route, side, x, treeZ, start, end), treeZ),
                        Range(random, 0.68f, 1.42f) * Mathf.Lerp(0.85f, 1.1f, density), Range(random, 0, 360));
                    treeCount++;
                }
                if (random.NextDouble() < 0.78)
                {
                    float offset = Range(random, 6, 32);
                    float bushZ = z + Range(random, -7, 7);
                    float x = ShoreX(route, bushZ, side) + side * offset;
                    Place(bushes[random.Next(bushes.Length)], root, new Vector3(x, SurfaceHeight(route, side, x, bushZ, start, end), bushZ), Range(random, 0.7f, 1.65f), Range(random, 0, 360));
                    bushCount++;
                }
                if (random.NextDouble() < 0.68)
                {
                    float offset = Range(random, 0.5f, 4);
                    float x = ShoreX(route, z, side) + side * offset;
                    Place(reed, root, new Vector3(x, SurfaceHeight(route, side, x, z, start, end), z), Range(random, 0.75f, 1.5f), Range(random, 0, 360));
                }
            }
            var shoreRandom = new System.Random(gorodets ? 819 : 518);
            for (float z = start + 20; z < end - 20; z += 5)
            for (int side = -1; side <= 1; side += 2)
            {
                float patch = Mathf.PerlinNoise(z * 0.023f + 43, side * 7 + 20);
                if (patch < 0.54f) continue;
                for (int i = 0; i < 3; i++)
                {
                    float reedZ = z + Range(shoreRandom, -3, 3);
                    float x = ShoreX(route, reedZ, side) + side * Range(shoreRandom, -1.4f, 5);
                    float y = SurfaceHeight(route, side, x, reedZ, start, end);
                    if (y < -0.3f) continue;
                    Place(reed, root, new Vector3(x, y, reedZ),
                        Range(shoreRandom, 0.85f, 1.7f), Range(shoreRandom, 0, 360));
                }
            }
            foreach (Camera camera in Object.FindObjectsByType<Camera>())
            {
                camera.depthTextureMode |= DepthTextureMode.Depth;
                camera.farClipPlane = 2400;
            }
            RiverWaterAndSkyBuilder.Apply(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"LANDSCAPE|{scene.name}: {treeCount} trees, {bushCount} bushes, shoreline reeds and continuous banks");
        }

        private static bool NearLeadingMark(Vector3 position)
        {
            GameObject navigation = GameObject.Find("Navigation");
            if (navigation == null) return false;
            foreach (Transform child in navigation.transform)
                if (child.name.Contains("Mark") && Vector2.Distance(new Vector2(position.x, position.z), new Vector2(child.position.x, child.position.z)) < 22) return true;
            return false;
        }

        private static float ShoreX(FairwayRoute route, float z, int side)
        {
            if (route == null) return FairwayModel.CenterX(z) + side * FairwayModel.ShoreDistance(z);
            FairwayQuery query = route.Query(new Vector3(0, 0, z));
            float edge = 55 + Mathf.Sin(z * 0.013f) * 3 + Mathf.Sin(z * 0.035f) * 1.3f;
            return query.Position.x + side * edge;
        }

        private static float Height(float offset, float x, float z)
        {
            float shore = ShoreOffset(offset, x, z);
            if (shore < 0) return shore * (0.10f + Mathf.Abs(shore) * 0.007f);
            float beachWidth = Mathf.Lerp(5, 11, Mathf.PerlinNoise(x * 0.011f + 12, z * 0.019f));
            float beach = Mathf.Min(shore, beachWidth) * 0.065f;
            float terrace = Mathf.SmoothStep(0, 3.8f - beachWidth * 0.065f,
                Mathf.InverseLerp(beachWidth, 28, shore));
            float hills = Mathf.PerlinNoise((x + 731) * 0.008f, (z + 271) * 0.007f) * 12;
            return beach + terrace + hills * Mathf.SmoothStep(0, 1, Mathf.Clamp01(offset / 80)) +
                Mathf.PerlinNoise(x * 0.13f, z * 0.09f) * 0.4f * Mathf.Clamp01((shore - beachWidth) / 8);
        }

        private static float ShoreOffset(float offset, float x, float z)
        {
            float scallop = (Mathf.PerlinNoise(x * 0.035f + 19, z * 0.055f) - 0.5f) * 5f +
                (Mathf.PerlinNoise(x * 0.13f, z * 0.19f + 81) - 0.5f) * 1.4f;
            return offset - scallop * (1 - Mathf.SmoothStep(0, 1, Mathf.Abs(offset) / 28));
        }

        private static float SurfaceHeight(FairwayRoute route, int side, float x, float z, float start, float end)
        {
            int rows = Mathf.CeilToInt((end - start) / BankRowSpacing);
            float step = (end - start) / rows;
            float row = Mathf.Clamp((z - start) / step, 0, rows - 0.0001f);
            float lowZ = start + Mathf.Floor(row) * step;
            float highZ = lowZ + step;
            float along = row - Mathf.Floor(row);
            float offset = side * (x - Mathf.Lerp(ShoreX(route, lowZ, side), ShoreX(route, highZ, side), along));
            int column = 0;
            while (column < BankOffsets.Length - 2 && offset > BankOffsets[column + 1]) column++;
            float low = BankOffsets[column];
            float high = BankOffsets[column + 1];
            float across = Mathf.InverseLerp(low, high, offset);
            float a = Height(low, ShoreX(route, lowZ, side) + side * low, lowZ);
            float b = Height(low, ShoreX(route, highZ, side) + side * low, highZ);
            float c = Height(high, ShoreX(route, highZ, side) + side * high, highZ);
            float d = Height(high, ShoreX(route, lowZ, side) + side * high, lowZ);
            // Match the actual mesh triangles so distant tree roots never float above hills.
            return across <= along ? a * (1 - along) + b * (along - across) + c * across
                : a * (1 - across) + c * along + d * (across - along);
        }

        private static Mesh BankMesh(string name, FairwayRoute route, int side, float start, float end)
        {
            int rows = Mathf.CeilToInt((end - start) / BankRowSpacing);
            int columns = BankOffsets.Length;
            var builder = new MeshData();
            for (int row = 0; row <= rows; row++)
            {
                float z = Mathf.Lerp(start, end, row / (float)rows);
                for (int c = 0; c < columns; c++)
                {
                    float offset = BankOffsets[c];
                    float x = ShoreX(route, z, side) + side * offset;
                    builder.Vertex(new Vector3(x, Height(offset, x, z), z), new Vector2(x, z),
                        new Color(Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5, 17, ShoreOffset(offset, x, z))),
                            Mathf.InverseLerp(-0.1f, 0.65f, Height(offset, x, z)), 0, 1));
                }
            }
            for (int row = 0; row < rows; row++)
            for (int c = 0; c < columns - 1; c++)
            {
                int a = row * columns + c;
                if (side > 0) builder.Quad(a, a + columns, a + columns + 1, a + 1);
                else builder.Quad(a, a + 1, a + columns + 1, a + columns);
            }
            return SaveMesh(name + "Bank", builder.Build(name));
        }

        private static Mesh WaterMesh(string name, FairwayRoute route, float start, float end)
        {
            const int columns = 48;
            int rows = Mathf.CeilToInt((end - start) / 5);
            var builder = new MeshData();
            for (int row = 0; row <= rows; row++)
            {
                float z = Mathf.Lerp(start, end, row / (float)rows);
                float left = ShoreX(route, z, -1) - 5;
                float right = ShoreX(route, z, 1) + 5;
                for (int c = 0; c <= columns; c++)
                {
                    float x = Mathf.Lerp(left, right, c / (float)columns);
                    builder.Vertex(new Vector3(x, 0, z), new Vector2(x, z), Color.white);
                }
            }
            for (int row = 0; row < rows; row++)
            for (int c = 0; c < columns; c++)
            {
                int a = row * (columns + 1) + c;
                builder.Quad(a, a + columns + 1, a + columns + 2, a + 1);
            }
            return SaveMesh(name + "Water", builder.Build(name + "Water"));
        }

        internal static GameObject PlantPrefab(string name, int seed, bool bush, bool reed, Material bark, Material leaves)
        {
            var plant = new GameObject(name);
            var lods = new LOD[3];
            for (int lod = 0; lod < 3; lod++)
            {
                var random = new System.Random(seed);
                var branches = new MeshData();
                var canopy = new MeshData();
                float height = reed ? 1.7f : bush ? 2.4f : 12f;
                float radius = bush ? 1.6f : seed % 3 == 0 ? 3.5f : 2.8f;
                if (reed)
                {
                    for (int stem = 0; stem < 22; stem++)
                    {
                        Vector3 foot = new Vector3(Range(random, -1.1f, 1.1f), 0, Range(random, -1.1f, 1.1f));
                        branches.Branch(foot, foot + new Vector3(0.08f, Range(random, 1.0f, 1.8f), 0), 0.012f, 0.005f, 3);
                    }
                }
                else
                {
                    branches.Branch(Vector3.zero, new Vector3(0.15f, height * 0.86f, 0.1f), bush ? 0.09f : 0.30f, 0.035f, 8);
                    for (int b = 0; b < (lod == 2 ? 4 : 14); b++)
                    {
                        float angle = b * 2.4f + Range(random, -0.3f, 0.3f);
                        float level = Range(random, 0.25f, 0.82f);
                        Vector3 from = Vector3.up * height * level;
                        Vector3 tip = from + new Vector3(Mathf.Cos(angle) * radius * (1.2f - level * 0.4f), height * 0.25f, Mathf.Sin(angle) * radius * (1.2f - level * 0.4f));
                        branches.Branch(from, tip, bush ? 0.038f : 0.11f * (1 - level), 0.012f, 5);
                    }
                }
                int count = reed ? 95 : bush ? 300 : 1400;
                count = Mathf.RoundToInt(count * (lod == 0 ? 1 : lod == 1 ? 0.34f : 0.10f));
                var clusters = new Vector3[11];
                for (int cluster = 0; cluster < clusters.Length; cluster++)
                {
                    float bearing = cluster * 2.4f;
                    float reach = radius * Range(random, 0.3f, 0.85f);
                    clusters[cluster] = new Vector3(Mathf.Cos(bearing) * reach,
                        height * Range(random, bush ? 0.25f : 0.48f, 0.86f), Mathf.Sin(bearing) * reach);
                }
                for (int leaf = 0; leaf < count; leaf++)
                {
                    float angle = Range(random, 0, Mathf.PI * 2);
                    float y = Range(random, -1, 1);
                    float r = Mathf.Sqrt(1 - y * y) * Mathf.Pow(Range(random, 0.1f, 1), 0.35f);
                    Vector3 center = clusters[leaf % clusters.Length] + new Vector3(Mathf.Cos(angle) * r * radius * 0.52f,
                        y * height * (bush ? 0.28f : 0.19f), Mathf.Sin(angle) * r * radius * 0.52f);
                    center.y = Mathf.Max(0.1f, center.y);
                    float size = (bush ? 0.16f : 0.19f) * (lod == 0 ? 1 : lod == 1 ? 1.6f : 2.6f);
                    if (reed)
                    {
                        center = new Vector3(Range(random, -1.1f, 1.1f), Range(random, 0.5f, height), Range(random, -1.1f, 1.1f));
                        size = 0.028f;
                    }
                    Quaternion rotation = Quaternion.Euler(reed ? Range(random, -15, 15) : Range(random, -70, 70), Range(random, 0, 360), Range(random, -35, 35));
                    Color color = Color.Lerp(new Color(0.64f, 0.77f, 0.54f), new Color(1.25f, 1.15f, 0.82f), Range(random, 0, 1));
                    color.a = Mathf.Clamp01(center.y / height);
                    canopy.Leaf(center, rotation, size, reed ? Range(random, 0.6f, 1.4f) : size * (seed % 3 == 2 ? 2.8f : 1.7f), color);
                }
                var renderers = new List<Renderer>();
                renderers.Add(MeshObject("Branches LOD" + lod, plant.transform, SaveMesh(name + "Branches" + lod, branches.Build(name + "Branches")), bark).GetComponent<Renderer>());
                renderers.Add(MeshObject("Leaves LOD" + lod, plant.transform, SaveMesh(name + "Leaves" + lod, canopy.Build(name + "Leaves")), leaves).GetComponent<Renderer>());
                lods[lod] = new LOD(lod == 0 ? 0.065f : lod == 1 ? 0.018f : 0.0035f, renderers.ToArray());
            }
            var group = plant.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.SetLODs(lods);
            group.RecalculateBounds();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(plant, Root + "/" + name + ".prefab");
            Object.DestroyImmediate(plant);
            return prefab;
        }

        internal static void Place(GameObject prefab, Transform parent, Vector3 position, float scale, float yaw)
        {
            var plant = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            plant.transform.localPosition = position;
            plant.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            plant.transform.localScale = new Vector3(scale, scale * 1.04f, scale);
        }

        internal static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var item = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            item.transform.SetParent(parent, false);
            item.GetComponent<MeshFilter>().sharedMesh = mesh;
            item.GetComponent<MeshRenderer>().sharedMaterial = material;
            return item;
        }

        internal static Mesh SaveMesh(string name, Mesh mesh)
        {
            string path = Root + "/" + name + ".asset";
            // The importer warns when the main object name differs from the file name.
            mesh.name = name;
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                mesh = existing;
            }
            return mesh;
        }

        internal static Material MaterialAsset(string name, string shader, Color color)
        {
            string path = Root + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find(shader);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            material.enableInstancing = true;
            // Materials converted from URP Lit keep its disabled motion pass, which would leave
            // swaying wood without motion vectors under TAA.
            material.SetShaderPassEnabled("MotionVectors", true);
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/ShipSimulator/Settings", "NaturalLandscape");
        }

        internal static float Range(System.Random random, float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

        internal sealed class MeshData
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<Color> colors = new List<Color>();
            private readonly List<int> triangles = new List<int>();
            public void Vertex(Vector3 position, Vector2 texture, Color color)
            {
                vertices.Add(position); uv.Add(texture); colors.Add(color);
            }
            public void Quad(int a, int b, int c, int d)
            {
                triangles.AddRange(new[] { a, b, c, a, c, d });
            }
            public void Leaf(Vector3 center, Quaternion rotation, float width, float length, Color color)
            {
                int a = vertices.Count;
                Vertex(center + rotation * new Vector3(-width, -length * 0.5f, 0), Vector2.zero, color);
                Vertex(center + rotation * new Vector3(-width, length * 0.5f, 0), Vector2.up, color);
                Vertex(center + rotation * new Vector3(width, length * 0.5f, 0), Vector2.one, color);
                Vertex(center + rotation * new Vector3(width, -length * 0.5f, 0), Vector2.right, color);
                Quad(a, a + 1, a + 2, a + 3);
            }
            public void Branch(Vector3 start, Vector3 end, float baseRadius, float tipRadius, int sides)
            {
                int a = vertices.Count;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);
                for (int ring = 0; ring < 2; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    float radius = ring == 0 ? baseRadius : tipRadius;
                    Vertex((ring == 0 ? start : end) + rotation * new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius), new Vector2(i / (float)sides, ring), Color.white);
                }
                for (int i = 0; i < sides; i++) Quad(a + i, a + (i + 1) % sides, a + sides + (i + 1) % sides, a + sides + i);
            }
            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
