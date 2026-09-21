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
    // Replaces the generated Meteor visual with the authored Blender model. Only the render mesh and
    // the model-dependent camera and light layout change; the Rigidbody, controller, data loader and
    // the three-box collision hull are kept exactly as the physics calibration left them.
    public static class Meteor342UModelIntegrator
    {
        public const string ModelPath = "Assets/ShipSimulator/Models/Meteor342U/Meteor342U.fbx";
        public const string PrefabPath = "Assets/ShipSimulator/Prefabs/Vessels/Meteor342U.prefab";
        private const string ModelFolder = "Assets/ShipSimulator/Models/Meteor342U";
        private const string MaterialFolder = ModelFolder + "/BlenderMaterials";

        [MenuItem("Ship Simulator/Integrate Meteor 342U Blender Model")]
        public static void Integrate() => UpdatePrefab();

        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                GameObject prefab = UpdatePrefab();
                RenderReview(prefab);
                Debug.Log("METEOR_MODEL|PASS: imported, aligned, saved and rendered.");
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
            if (importer == null) throw new InvalidOperationException("Missing authored Meteor FBX: " + ModelPath);
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
                Vector3 upPoint = Point(visual, "UpReference");
                Debug.Log($"METEOR_AXES|bow={bow:F4}, starboard={starboard:F4}, up={upPoint:F4}");
                if (bow.z < 16f || starboard.x < 2.5f || upPoint.y < 0.99f)
                    throw new InvalidOperationException("Meteor export axes are reflected or incorrectly scaled.");

                // The propellers and rudders the physics uses must sit where the render mesh puts them.
                Vector3 propStarboard = Point(visual, "PropPoint_S");
                if (Mathf.Abs(propStarboard.z + 13.5f) > 0.25f || Mathf.Abs(propStarboard.x - 1.6f) > 0.15f)
                    throw new InvalidOperationException(
                        "Meteor propeller landmark disagrees with the vessel JSON: " + propStarboard);

                foreach (LODGroup existing in visual.GetComponentsInChildren<LODGroup>())
                    Object.DestroyImmediate(existing);
                var materials = new Dictionary<string, Material>();
                var lods = new LOD[3];
                float[] transitions = { 0.30f, 0.12f, 0.008f };
                for (int i = 0; i < lods.Length; i++)
                {
                    var renderer = Find(visual, "Meteor342U_LOD" + i).GetComponent<Renderer>();
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
                    Debug.Log($"METEOR_LOD|{i}: {mesh.triangles.Length / 3} triangles, local bounds {mesh.bounds}");
                }
                LODGroup group = visual.AddComponent<LODGroup>();
                group.SetLODs(lods);
                group.RecalculateBounds();

                VesselLayout layout = contents.GetComponent<VesselLayout>();
                Vector3 eye = Point(visual, "CameraNavigator");
                layout.Configure("meteor-342u", 34.6f / 138.3f, eye, eye + Vector3.forward * 80f,
                    Point(visual, "Light_Port"), 0.44f,
                    // One masthead light would suit this length; the rig carries a pair, so the
                    // forward one sits on a staff on the bow saloon roof and the after one on the mast.
                    Point(visual, "Light_MastheadFwd"), Point(visual, "Light_MastheadFwd").y - .42f,
                    Point(visual, "Light_MastheadAft"), Point(visual, "Light_MastheadAft").y - .74f,
                    Point(visual, "Light_Stern"), Point(visual, "Light_Stern").y - .36f);
                layout.ConfigureCameraViews(new[]
                {
                    new Vector3(11.69f, 5.71f, -32.87f), new Vector3(0f, 6.69f, -3.46f),
                    new Vector3(0f, 20.76f, -4.15f), new Vector3(-13.42f, 3.12f, -2.77f),
                    new Vector3(13.42f, 3.12f, -2.77f), new Vector3(0f, 3.90f, 24.91f),
                    new Vector3(0f, 4.16f, -26.30f), new Vector3(-8.92f, 2.34f, -6.23f)
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
                AssetDatabase.CreateFolder(ModelFolder, "BlenderMaterials");
            string path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            // Painted duralumin above water, unpainted alloy for the foil system, tinted saloon glass.
            (Color color, float metallic, float smoothness) = name switch
            {
                "Meteor_HullPaint" => (new Color(.855f, .867f, .862f), .15f, .64f),
                "Meteor_SecondaryPaint" => (new Color(.055f, .243f, .494f), .12f, .66f),
                "Meteor_Glass" => (new Color(.130f, .200f, .235f, .60f), .05f, .92f),
                "Meteor_Rubber" => (new Color(.055f, .058f, .062f), 0f, .20f),
                "Meteor_Steel" => (new Color(.640f, .660f, .680f), .85f, .70f),
                "Meteor_FoilSteel" => (new Color(.520f, .545f, .560f), .55f, .62f),
                "Meteor_Underbody" => (new Color(.105f, .125f, .120f), .20f, .34f),
                "Meteor_InteriorDark" => (new Color(.040f, .045f, .052f), 0f, .15f),
                "Meteor_Lights" => (new Color(.900f, .870f, .720f), .05f, .78f),
                "Meteor_Deck" => (new Color(.300f, .318f, .330f), .10f, .38f),
                "Meteor_InteriorSeats" => (new Color(.150f, .175f, .205f), 0f, .22f),
                _ => (new Color(.60f, .62f, .63f), .1f, .4f)
            };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (name == "Meteor_Glass")
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.SetFloat("_Cull", (float)CullMode.Off);
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
            Directory.CreateDirectory("Logs/Meteor342U/Unity");
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
            camera.orthographicSize = 11.5f;
            camera.farClipPlane = 300;
            string[] names = { "port", "starboard", "front-quarter", "rear-quarter", "top-front", "foils" };
            Vector3[] views = { new(-46, 3, 0), new(46, 3, 0), new(30, 15, 42),
                new(-32, 15, -42), new(18, 42, 24), new(-24, -10, 30) };
            var group = vessel.GetComponentInChildren<LODGroup>();
            group.ForceLOD(0);
            for (int i = 0; i < names.Length; i++)
            {
                camera.transform.position = views[i];
                camera.transform.LookAt(new Vector3(0, 1, 0));
                light.transform.rotation = Quaternion.Euler(35, camera.transform.eulerAngles.y - 25, 0);
                LandscapePreview.Render(camera, "Logs/Meteor342U/Unity/" + names[i] + ".png");
            }
            for (int lod = 1; lod < 3; lod++)
            {
                group.ForceLOD(lod);
                camera.transform.position = views[2];
                camera.transform.LookAt(new Vector3(0, 1, 0));
                light.transform.rotation = Quaternion.Euler(35, camera.transform.eulerAngles.y - 25, 0);
                LandscapePreview.Render(camera, "Logs/Meteor342U/Unity/lod" + lod + ".png");
            }
            Object.DestroyImmediate(vessel);
            Object.DestroyImmediate(light.gameObject);
            Object.DestroyImmediate(camera.gameObject);
        }
    }
}
