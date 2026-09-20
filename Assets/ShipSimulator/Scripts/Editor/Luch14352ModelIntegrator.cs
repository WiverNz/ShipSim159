using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    public static class Luch14352ModelIntegrator
    {
        public const string ModelPath = "Assets/ShipSimulator/Models/Luch14352/Luch14352.fbx";
        public const string PrefabPath = "Assets/ShipSimulator/Prefabs/Vessels/Luch14352.prefab";
        private const string MaterialFolder = "Assets/ShipSimulator/Models/Luch14352/BlenderMaterials";

        [MenuItem("Ship Simulator/Integrate Luch 14352 Blender Model")]
        public static void Integrate() => UpdatePrefab();

        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject prefab = UpdatePrefab();
                RenderReview(prefab);
                Debug.Log("LUCH_MODEL|PASS: imported, aligned, saved and rendered.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        public static GameObject UpdatePrefab()
        {
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing authored Luch FBX: " + ModelPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform old = contents.transform.Find("DetailedVisual");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, contents.transform);
                visual.name = "DetailedVisual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                // Exported landmarks make axis conversion explicit and detect a reflected import.
                Vector3 origin = Point(visual, "Waterline");
                Vector3 forward = Point(visual, "BowReference") - origin;
                Vector3 up = Point(visual, "UpReference") - origin;
                visual.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
                visual.transform.localPosition = -Point(visual, "Waterline");
                Vector3 bow = Point(visual, "BowReference");
                Vector3 starboard = Point(visual, "StarboardReference");
                Debug.Log($"LUCH_AXES|bow={bow:F4}, starboard={starboard:F4}, up={Point(visual, "UpReference"):F4}");
                if (bow.z < 11f || starboard.x < 2f || Point(visual, "UpReference").y < 0.99f)
                    throw new InvalidOperationException("Luch export axes are reflected or incorrectly scaled.");

                foreach (LODGroup existing in visual.GetComponentsInChildren<LODGroup>())
                    Object.DestroyImmediate(existing);
                var materials = new Dictionary<string, Material>();
                var lods = new LOD[3];
                float[] transitions = { 0.30f, 0.12f, 0.008f };
                for (int i = 0; i < lods.Length; i++)
                {
                    var renderer = Find(visual, "Luch14352_LOD" + i).GetComponent<Renderer>();
                    Material[] slots = renderer.sharedMaterials;
                    for (int m = 0; m < slots.Length; m++)
                    {
                        string key = slots[m].name.Split('.')[0];
                        if (!materials.TryGetValue(key, out Material material))
                        {
                            material = CreateMaterial(key);
                            materials.Add(key, material);
                        }
                        slots[m] = material;
                    }
                    renderer.sharedMaterials = slots;
                    lods[i] = new LOD(transitions[i], new[] { renderer });
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Debug.Log($"LUCH_LOD|{i}: {mesh.triangles.Length / 3} triangles, local bounds {mesh.bounds}");
                }
                LODGroup group = visual.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();

                VesselLayout layout = contents.GetComponent<VesselLayout>();
                Vector3 eye = Point(visual, "CameraNavigator");
                layout.Configure("luch-14352", 23.72f / 138.3f, eye, eye + Vector3.forward * 80f,
                    new Vector3(-1.55f, 2.38f, 8.10f), 2.12f,
                    new Vector3(0f, 3.05f, 8.17f), 2.94f,
                    new Vector3(0f, 4.18f, 7.91f), 2.94f,
                    new Vector3(0f, 1.28f, -11.37f), 0.68f);
                layout.ConfigureCameraViews(new[]
                {
                    new Vector3(7f, 6f, -23f), new Vector3(0f, 6.2f, 0f),
                    new Vector3(0f, 18f, -3f), new Vector3(-10f, 3.4f, -2f),
                    new Vector3(10f, 3.4f, -2f), new Vector3(0f, 3.6f, 19f),
                    new Vector3(0f, 3.8f, -19f), new Vector3(-6.5f, 2.6f, -4f)
                });
                if (visual.GetComponentsInChildren<Collider>().Length != 0)
                    throw new InvalidOperationException("The render model must not introduce physics colliders.");
                GameObject result = PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                AssetDatabase.SaveAssets();
                return result;
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static Transform Find(GameObject visual, string name) =>
            visual.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        private static Vector3 Point(GameObject visual, string name) =>
            visual.transform.parent.InverseTransformPoint(Find(visual, name).position);

        private static Material CreateMaterial(string name)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/ShipSimulator/Models/Luch14352", "BlenderMaterials");
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            (Color color, float metallic, float smoothness) = name switch
            {
                "Paint_Ivory" => (new Color(.90f, .91f, .89f), .12f, .66f),
                "Paint_DeckTeal" => (new Color(.28f, .52f, .55f), .15f, .60f),
                "Paint_HullCharcoal" => (new Color(.20f, .24f, .26f), .22f, .68f),
                "Skeg_Rubber" => (new Color(.14f, .16f, .17f), 0f, .27f),
                "Rail_Aluminium" => (new Color(.72f, .75f, .77f), .75f, .71f),
                "Glass_BlueGrey" => (new Color(.22f, .35f, .39f, .72f), .12f, .87f),
                "Interior_Seats" => (new Color(.39f, .50f, .54f), 0f, .2f),
                "Lights_WarmLens" => (new Color(.93f, .89f, .76f), .05f, .81f),
                _ => (new Color(.20f, .25f, .27f), 0f, .25f)
            };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (name == "Glass_BlueGrey")
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void RenderReview(GameObject prefab)
        {
            Directory.CreateDirectory("Logs/Luch14352/Unity");
            GameObject vessel = Object.Instantiate(prefab);
            var light = new GameObject("Review sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(35, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .59f, .65f);
            var camera = new GameObject("Review camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.18f, .23f, .28f);
            camera.orthographic = true;
            camera.orthographicSize = 8.3f;
            camera.farClipPlane = 200;
            string[] names = { "port", "starboard", "front-quarter", "rear-quarter", "top-front", "skegs" };
            Vector3[] views = { new(-32, 2, 0), new(32, 2, 0), new(22, 12, 30),
                new(-23, 12, -30), new(13, 30, 17), new(-17, -8, -24) };
            var group = vessel.GetComponentInChildren<LODGroup>();
            group.ForceLOD(0);
            for (int i = 0; i < names.Length; i++)
            {
                camera.transform.position = views[i];
                camera.transform.LookAt(new Vector3(0, 1, 0));
                light.transform.rotation = Quaternion.Euler(35, camera.transform.eulerAngles.y - 25, 0);
                LandscapePreview.Render(camera, "Logs/Luch14352/Unity/" + names[i] + ".png");
            }
            for (int lod = 1; lod < 3; lod++)
            {
                group.ForceLOD(lod);
                camera.transform.position = views[2];
                camera.transform.LookAt(new Vector3(0, 1, 0));
                light.transform.rotation = Quaternion.Euler(35, camera.transform.eulerAngles.y - 25, 0);
                LandscapePreview.Render(camera, "Logs/Luch14352/Unity/lod" + lod + ".png");
            }
            Object.DestroyImmediate(vessel);
            Object.DestroyImmediate(light.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }
    }
}
