using System;
using System.IO;
using System.Linq;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    public static class MN52ModelIntegrator
    {
        public const string Id = "serpukhov-mn52";
        public const string Folder = "Assets/ShipSimulator/Models/MN52_T77";
        public const string BargePath = "Assets/ShipSimulator/Prefabs/Vessels/MN52_T77.prefab";
        public const string UnitPath = "Assets/ShipSimulator/Prefabs/Vessels/Serpukhov_MN52.prefab";
        public const string DataPath = "Assets/ShipSimulator/Data/Vessels/MN52_T77.json";
        public const string Previews = "Art/Source/MN52_T77/Previews";

        [MenuItem("Ship Simulator/Integrate MN-52 and Serpukhov push unit")]
        public static void Integrate()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string path = Folder + "/MN52_T77.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("MN52 authored FBX missing.");
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.importLights = importer.importCameras = false;
            importer.addCollider = false; importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import; importer.SaveAndReimport();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var materials = source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Select(m => m.name).Distinct().ToDictionary(n => n, MaterialFor);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            var data = JsonUtility.FromJson<VesselData>(json.text);
            if (!VesselDataValidator.TryValidate(data, out string error)) throw new InvalidOperationException(error);
            var root = new GameObject("MN52_T77");
            GameObject barge;
            try
            {
                root.AddComponent<Rigidbody>().mass = 153000;
                root.AddComponent<VesselDataLoader>().Configure(json);
                root.AddComponent<PassiveVessel>();
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                visual.name = "DetailedVisual";
                Vector3 origin = Point(root, "Waterline");
                visual.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(
                    Point(root, "BowReference") - origin, Point(root, "UpReference") - origin));
                visual.transform.localPosition = -Point(root, "Waterline");
                if (Vector3.Distance(Point(root, "BowReference"), Vector3.forward * 14.4f) > .01f ||
                    Vector3.Distance(Point(root, "StarboardReference"), Vector3.right * 3.15f) > .01f)
                    throw new InvalidOperationException("MN52 axis/scale conversion failed.");
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    r.gameObject.SetActive(true); r.enabled = true;
                    r.sharedMaterials = r.sharedMaterials.Select(m => materials[m.name]).ToArray();
                }
                var lods = new LOD[3]; float[] sizes = { .25f, .09f, .006f };
                for (int i = 0; i < 3; i++) lods[i] = new LOD(sizes[i], visual.GetComponentsInChildren<Renderer>()
                    .Where(r => r.name.StartsWith("MN52_LOD" + i)).ToArray());
                foreach (LODGroup old in visual.GetComponentsInChildren<LODGroup>()) Object.DestroyImmediate(old);
                var lod = visual.AddComponent<LODGroup>(); lod.SetLODs(lods); lod.RecalculateBounds();
                Collisions(root);
                barge = PrefabUtility.SaveAsPrefabAsset(root, BargePath);
            }
            finally { Object.DestroyImmediate(root); }
            GameObject unit = BuildUnit(barge);
            var catalogue = VesselCatalogue.Load();
            catalogue.Configure(catalogue.Entries.Where(e => e.id != Id).Append(CatalogueEntry(unit)).ToArray());
            EditorUtility.SetDirty(catalogue); AssetDatabase.SaveAssets();
        }

        public static VesselCatalogue.Entry CatalogueEntry(GameObject unit) => new VesselCatalogue.Entry
        {
            id = Id, prefab = unit, menuName = "СЕРПУХОВ\nМН-52", displayName = "Серпухов + МН-52",
            vesselClass = "PUSH UNIT\nPETROLEUM BARGE", formationSizeM = new Vector3(6.31f, 1.2f, 50.65f)
        };

        private static GameObject BuildUnit(GameObject barge)
        {
            var tugPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SerpukhovR96ModelIntegrator.PrefabPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(tugPrefab);
            root.name = "Serpukhov_MN52";
            try
            {
                root.AddComponent<PushUnit>().Configure(barge);
                var anchor = new GameObject("BargePushConnection"); anchor.transform.SetParent(root.transform, false);
                anchor.transform.localPosition = new Vector3(0, .65f, 10.9f);
                var layout = root.GetComponent<VesselLayout>();
                layout.Configure(Id, 50.65f / 138.3f, layout.NavigatorEye, layout.NavigatorLookAt,
                    layout.PortSideLight, layout.SideLightSupportBaseY, layout.ForwardMastheadLight,
                    layout.ForwardMastheadSupportBaseY, layout.AftMastheadLight, layout.AftMastheadSupportBaseY,
                    layout.SternLight, layout.SternLightSupportBaseY);
                layout.ConfigureExternalLookAt(new Vector3(0, 2, 14));
                layout.ConfigureCameraViews(new[] { new Vector3(18, 20, -34), new Vector3(0, 14, 5),
                    new Vector3(0, 58, 14), new Vector3(-43, 17, 14), new Vector3(43, 17, 14),
                    new Vector3(0, 14, 60), new Vector3(0, 12, -32), new Vector3(-14, 8, 11) });
                return PrefabUtility.SaveAsPrefabAsset(root, UnitPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Vector3 Point(GameObject root, string name) => root.transform.InverseTransformPoint(
            root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "MN52_" + name).position);

        private static void Collisions(GameObject root)
        {
            var parent = new GameObject("CollisionHull"); parent.transform.SetParent(root.transform, false);
            Box("Middle", new Vector3(0, -.45f, 0), new Vector3(5.85f, 1.5f, 20));
            foreach (float sign in new[] { -1f, 1f })
            {
                Box("SpoonInner", new Vector3(0, -.30f, sign * 11.25f), new Vector3(5.72f, 1.2f, 2.5f));
                Box("SpoonOuter", new Vector3(0, 0, sign * 13.0f), new Vector3(5.35f, .6f, 1.1f));
                Box("Transom", new Vector3(0, .25f, sign * 13.96f), new Vector3(4.88f, .5f, .88f));
            }
            Box("RaisedCabin", new Vector3(0, 3.4f, -8.5f), new Vector3(5.3f, 2.3f, 6));
            Box("PumpCabinet", new Vector3(0, 1.74f, 5.25f), new Vector3(2.4f, 2.5f, 2.25f));
            void Box(string name, Vector3 centre, Vector3 size)
            {
                var o = new GameObject(name); o.transform.SetParent(parent.transform, false);
                var c = o.AddComponent<BoxCollider>(); c.center = centre; c.size = size;
            }
        }

        private static Material MaterialFor(string name)
        {
            Directory.CreateDirectory(Folder + "/Materials"); string path = Folder + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", name switch
            {
                "MN52_Glass" => new Color(.36f, .49f, .50f, .25f),
                "MN52_Orange" => new Color(.66f, .13f, .025f),
                "MN52_Wear" => new Color(.22f, .115f, .06f),
                "MN52_Metal" => new Color(.13f, .16f, .17f),
                _ => new Color(.022f, .03f, .026f)
            });
            mat.SetFloat("_Smoothness", .3f);
            foreach (var suffix in new[] { "BaseColor", "Normal", "MetalSmooth" })
            {
                string texturePath = Folder + "/Textures/" + name + "_" + suffix + ".png";
                if (!File.Exists(texturePath)) continue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.textureType = suffix == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = suffix == "BaseColor"; importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true; importer.SaveAndReimport();
                mat.SetTexture(suffix == "BaseColor" ? "_BaseMap" : suffix == "Normal" ? "_BumpMap" : "_MetallicGlossMap",
                    AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                if (suffix == "BaseColor") mat.SetColor("_BaseColor", Color.white);
                if (suffix == "Normal") mat.EnableKeyword("_NORMALMAP");
                if (suffix == "MetalSmooth") { mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.SetFloat("_Smoothness", 1); mat.SetFloat("_Metallic", 1); }
            }
            if (name == "MN52_Glass")
            {
                mat.SetFloat("_Surface", 1); mat.SetFloat("_ZWrite", 0);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = 3000; mat.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(mat); return mat;
        }

        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Integrate(); Review();
                Debug.Log("MN52_IMPORT|PASS"); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void Review()
        {
            Directory.CreateDirectory(Previews);
            var barge = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BargePath));
            var sun = new GameObject("ReviewSun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.intensity = 1.8f; sun.transform.rotation = Quaternion.Euler(35, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .59f, .65f);
            var camera = new GameObject("ReviewCamera").AddComponent<Camera>();
            camera.fieldOfView = 40; camera.nearClipPlane = .05f; camera.farClipPlane = 200;
            camera.backgroundColor = new Color(.16f, .2f, .24f); camera.clearFlags = CameraClearFlags.SolidColor;
            var lod = barge.GetComponentInChildren<LODGroup>();
            for (int i = -1; i < 3; i++)
            {
                lod.ForceLOD(Mathf.Max(i, 0));
                camera.transform.position = new Vector3(26, 17, 34);
                camera.transform.LookAt(new Vector3(0, 2, 0));
                Capture(camera, Previews + (i < 0 ? "/unity-warmup.png" : "/unity-lod" + i + ".png"));
            }
            Object.DestroyImmediate(barge); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(sun.gameObject);
        }

        public static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1600, 1000, 24); target.Create();
            var old = RenderTexture.active; camera.targetTexture = target; camera.Render();
            RenderTexture.active = target; var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG()); camera.targetTexture = null; RenderTexture.active = old;
            Object.DestroyImmediate(image); target.Release(); Object.DestroyImmediate(target);
        }
    }
}
