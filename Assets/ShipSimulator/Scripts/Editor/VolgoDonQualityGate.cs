using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    public static class VolgoDonQualityGate
    {
        public const string Output = "Art/Source/VolgoDon507B/Previews";
        private static int stage = 8;
        private static string Prefix => "part" + stage;
        [Serializable] private sealed class Manifest { public int stage; }
        [Serializable] public sealed class Level { public int level, triangles, renderers, materialPasses; }
        [Serializable] private sealed class Timing { public int level, drawCalls, setPassCalls; public double medianMs, p95Ms; }
        [Serializable] private sealed class Performance
        {
            public string gpu, cpu, method = "1920x1080 isolated editor Camera.Render plus synchronous 1-pixel readback; 20 warmups, 100 samples. Includes submission and synchronization, not runtime frame time.";
            public Timing[] timings;
        }
        [Serializable] private sealed class LoadingCost
        {
            public int iterations;
            public double millisecondsPerRebuild;
            public long bytesPerRebuild;
            public string scope = "Parameter, manoeuvring, hydrostatics and station-array reconstruction only; excludes Rigidbody assignments. Alternating cargo inventory, warmed editor thread.";
        }
        [Serializable] private sealed class Collision { public string name, type; public Vector3 centre, size; }
        [Serializable] private sealed class Report
        {
            public Level[] levels;
            public long textureBytes;
            public int textureCount;
            public Collision[] collisions;
            public Vector3 boundsCentre, boundsSize;
        }

        public static void Run()
        {
            try
            {
                int manifestStage = JsonUtility.FromJson<Manifest>(File.ReadAllText(VolgoDonMechanismIntegration.Folder + "/materials.json")).stage;
                stage = manifestStage >= 13 ? 13 : manifestStage >= 10 ? 10 : 8;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                VolgoDonCandidateReview.Review(false);
                GameObject contents = PrefabUtility.LoadPrefabContents(VolgoDonMechanismIntegration.PrefabPath);
                try
                {
                    string physics = EditorJsonUtility.ToJson(contents.GetComponent<ShipPhysicsController>());
                    string body = EditorJsonUtility.ToJson(contents.GetComponent<Rigidbody>());
                    Collider[] colliders = contents.GetComponentsInChildren<Collider>();
                    string[] colliderState = colliders.Select(EditorJsonUtility.ToJson).ToArray();
                    for (int i = 0; i < 2; i++)
                    {
                        VolgoDonMechanismIntegration.Install(contents);
                        Require(physics == EditorJsonUtility.ToJson(contents.GetComponent<ShipPhysicsController>()), "Physics root changed");
                        Require(body == EditorJsonUtility.ToJson(contents.GetComponent<Rigidbody>()), "Rigidbody changed");
                        for (int j = 0; j < colliders.Length; j++)
                            Require(colliderState[j] == EditorJsonUtility.ToJson(colliders[j]), "Collider changed");
                        Require(contents.GetComponentsInChildren<LODGroup>().Length == 1, "Duplicate LOD groups");
                    }
                    PrefabUtility.SaveAsPrefabAsset(contents, VolgoDonMechanismIntegration.PrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                AssetDatabase.SaveAssets();
                ProfileLoading();
                var vessel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VolgoDonMechanismIntegration.PrefabPath));
                try { Audit(vessel); Capture(vessel); }
                finally { Object.DestroyImmediate(vessel); }
                Debug.Log("VOLGODON_QUALITY|PASS: reproducible integration, LOD budgets, materials, moving attachments and colliders");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static Level[] Measure(LODGroup group)
        {
            return group.GetLODs().Select((lod, index) => new Level
            {
                level = index, renderers = lod.renderers.Length,
                triangles = lod.renderers.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3),
                materialPasses = lod.renderers.Sum(r => r.sharedMaterials.Length)
            }).ToArray();
        }

        private static void ProfileLoading()
        {
            VesselData data = JsonUtility.FromJson<VesselData>(File.ReadAllText("Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json"));
            LoadingState state = LoadingModel.Initial(data);
            ManoeuvringModel model = null;
            for (int i = 0; i < 20; i++) model = new ManoeuvringModel(VesselParameters.Create(data, state), model);
            long startBytes = GC.GetAllocatedBytesForCurrentThread();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                state.massKg[0] += i % 2 == 0 ? -1 : 1;
                VesselParameters parameters = VesselParameters.Create(data, state);
                model = new ManoeuvringModel(parameters, model);
                GC.KeepAlive(new HydrostaticsModel(parameters));
                GC.KeepAlive(new float[model.Hull.StationCount]);
            }
            timer.Stop();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - startBytes;
            File.WriteAllText($"Art/Source/VolgoDon507B/{Prefix}-loading-cost.json", JsonUtility.ToJson(new LoadingCost
                { iterations = 1000, millisecondsPerRebuild = timer.Elapsed.TotalMilliseconds / 1000,
                    bytesPerRebuild = allocated == 0 ? -1 : allocated / 1000 }, true));
        }

        private static void Audit(GameObject vessel)
        {
            LODGroup group = vessel.GetComponentInChildren<LODGroup>();
            Level[] levels = Measure(group);
            Require(levels.Length == 3 && levels[0].triangles == (stage >= 10 ? 59543 : 53130), "LOD0 changed");
            // Part 13 split the windlass gypsies out of VD507B_detali; as small meshes they are not decimated,
            // which adds 72 LOD1 triangles. The reviewed LOD1 budget is 36,100.
            Require(levels[1].triangles <= (stage >= 13 ? 36100 : 36000) && levels[2].triangles <= 25000, "Triangle budget exceeded");
            Require(levels.All(l => l.materialPasses <= 64), "Vessel forward-pass budget exceeded");
            var textures = new HashSet<Texture>();
            foreach (Renderer renderer in group.GetLODs().SelectMany(l => l.renderers))
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                Require(mesh.normals.Length == mesh.vertexCount && mesh.uv.Length == mesh.vertexCount, "Missing mesh attributes");
                Require(renderer.sharedMaterials.All(m => m != null && m.shader != null && m.shader.isSupported), "Missing material/shader");
                foreach (Material material in renderer.sharedMaterials)
                    foreach (string property in material.GetTexturePropertyNames())
                        if (material.GetTexture(property) != null) textures.Add(material.GetTexture(property));
            }
            foreach (Transform transform in vessel.GetComponentsInChildren<Transform>())
                if (transform.name.StartsWith("DistanceLOD"))
                    Require(transform.localPosition == Vector3.zero && transform.localRotation == Quaternion.identity &&
                        transform.localScale == Vector3.one && transform.parent.GetComponent<MeshFilter>() != null,
                        "LOD detached from its animated original");
            long bytes = textures.Sum(Profiler.GetRuntimeMemorySizeLong);
            Require(bytes <= 128L * 1024 * 1024, "Shared texture budget exceeded");
            var collisions = new List<Collision>();
            foreach (Collider collider in vessel.GetComponentsInChildren<Collider>())
            {
                Require(collider is BoxCollider, "Expected existing compound box hull");
                var box = (BoxCollider)collider;
                collisions.Add(new Collision { name = box.name, type = "BoxCollider", centre = box.transform.TransformPoint(box.center),
                    size = Vector3.Scale(box.size, box.transform.lossyScale) });
            }
            Require(collisions.Count > 0, "No collision hull");
            Bounds bounds = group.GetLODs()[0].renderers[0].bounds;
            foreach (Renderer r in group.GetLODs()[0].renderers) bounds.Encapsulate(r.bounds);
            var report = new Report { levels = levels, textureBytes = bytes, textureCount = textures.Count,
                collisions = collisions.ToArray(), boundsCentre = bounds.center, boundsSize = bounds.size };
            File.WriteAllText($"Art/Source/VolgoDon507B/{Prefix}-unity.json", JsonUtility.ToJson(report, true));
        }

        private static void Capture(GameObject vessel)
        {
            Directory.CreateDirectory(Output);
            var camera = new GameObject("Part 8 review camera").AddComponent<Camera>();
            var sun = new GameObject("Part 8 review sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.5f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.20f, .25f, .30f);
            camera.orthographic = true; camera.farClipPlane = 5000;
            var group = vessel.GetComponentInChildren<LODGroup>();
            var rig = vessel.GetComponent<VesselMechanisms>();
            string[] names = { "front", "side", "top", "perspective", "underwater", "bridge", "hatches", "lightship", "night" };
            Vector3[] positions = { new(0, 6, 180), new(180, 8, 0), new(0, 180, 0), new(110, 80, 114),
                new(9, -1, -74), new(-1.8f, 9.1f, -43.3f), new(85, 95, 25), new(180, 8, 0), new(110, 80, 114) };
            var lights = vessel.GetComponent<NavigationLightRig>() ?? vessel.AddComponent<NavigationLightRig>();
            lights.EnsureCreated();
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Static waterline reference";
            water.transform.localScale = Vector3.one * 100;
            Object.DestroyImmediate(water.GetComponent<Collider>());
            var waterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            waterMaterial.color = new Color(.09f, .22f, .28f);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            for (int i = 0; i < names.Length; i++)
            {
                water.SetActive(i == 1 || i == 7);
                group.ForceLOD(0);
                camera.orthographic = i != 5;
                camera.transform.position = positions[i];
                camera.transform.LookAt(i == 4 ? new Vector3(0, -2, -64.65f) : i == 5 ? new Vector3(-1.8f, 9.1f, 55) : new Vector3(0, 3, 0));
                camera.orthographicSize = i == 0 ? 11 : i == 2 ? 78 : i == 4 ? 4 : 44;
                sun.transform.rotation = Quaternion.Euler(35, camera.transform.eulerAngles.y - 25, 0);
                if (i == 6) { rig.SetHatches(HatchPosition.HalfOpen); rig.Advance(1000, 0, 0, 0); }
                else { rig.SetHatches(HatchPosition.Closed); rig.Advance(1000, 0, 0, 0); }
                // Static review of the calculated waterline; runtime flotation is tested separately.
                vessel.transform.position = i == 7 ? Vector3.up * (3.53f - .915185f) : Vector3.zero;
                lights.SetNight(i == 8);
                sun.intensity = i == 8 ? .05f : 1.5f;
                RenderSettings.ambientLight = i == 8 ? new Color(.04f, .05f, .07f) : new Color(.6f, .6f, .6f);
                LandscapePreview.Render(camera, Output + "/" + Prefix + "-unity-" + names[i] + ".png");
            }
            water.SetActive(false);
            lights.SetNight(false); sun.intensity = 1.5f;
            RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
            camera.orthographic = true; camera.orthographicSize = 70;
            camera.transform.position = new Vector3(160, 60, 120); camera.transform.LookAt(new Vector3(0, 3, 0));
            for (int level = 0; level < 3; level++)
            {
                group.ForceLOD(level);
                LandscapePreview.Render(camera, Output + $"/{Prefix}-unity-lod{level}.png");
            }
            Benchmark(camera, group);
            group.ForceLOD(-1);
            foreach (float height in new[] { .56f, .53f, .21f, .19f })
            {
                Vector3 scale = group.transform.lossyScale;
                float worldSize = group.size * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                camera.orthographicSize = worldSize * QualitySettings.lodBias / (2 * height);
                LandscapePreview.Render(camera, Output + "/" + Prefix + "-transition-" + height.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".png");
            }
            group.ForceLOD(2); camera.orthographicSize = 44;
            rig.SetHatches(HatchPosition.HalfOpen); rig.Advance(1000, 0, 0, 0);
            LandscapePreview.Render(camera, Output + "/" + Prefix + "-lod2-hatches.png");
            lights.SetNight(true); sun.intensity = .05f;
            RenderSettings.ambientLight = new Color(.04f, .05f, .07f);
            LandscapePreview.Render(camera, Output + "/" + Prefix + "-lod2-night.png");
            group.ForceLOD(-1);
            Object.DestroyImmediate(water); Object.DestroyImmediate(waterMaterial);
            Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(sun.gameObject);
        }

        private static void Benchmark(Camera camera, LODGroup group)
        {
            var target = new RenderTexture(1920, 1080, 24);
            var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            var results = new List<Timing>();
            try
            {
                camera.targetTexture = target;
                for (int level = 0; level < 3; level++)
                {
                    group.ForceLOD(level);
                    var samples = new List<double>();
                    for (int i = 0; i < 120; i++)
                    {
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        camera.Render();
                        RenderTexture.active = target;
                        pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                        timer.Stop();
                        if (i >= 20) samples.Add(timer.Elapsed.TotalMilliseconds);
                    }
                    samples.Sort();
                    results.Add(new Timing { level = level, medianMs = samples[50], p95Ms = samples[94],
                        drawCalls = UnityStats.drawCalls == 0 ? -1 : UnityStats.drawCalls,
                        setPassCalls = UnityStats.setPassCalls == 0 ? -1 : UnityStats.setPassCalls });
                }
                File.WriteAllText($"Art/Source/VolgoDon507B/{Prefix}-render-cost.json", JsonUtility.ToJson(new Performance
                    { gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType, timings = results.ToArray() }, true));
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixel);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
