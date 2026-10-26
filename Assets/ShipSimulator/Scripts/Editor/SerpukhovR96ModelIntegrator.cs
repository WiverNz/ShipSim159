using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    public static class SerpukhovR96ModelIntegrator
    {
        public const string Id = "serpukhov-r96";
        public const string Folder = "Assets/ShipSimulator/Models/Serpukhov_R96";
        public const string PrefabPath = "Assets/ShipSimulator/Prefabs/Vessels/Serpukhov_R96.prefab";
        public const string DataPath = "Assets/ShipSimulator/Data/Vessels/Serpukhov_R96.json";

        [MenuItem("Ship Simulator/Integrate Serpukhov R-96 Blender Model")]
        public static void Integrate()
        {
            GameObject prefab = UpdatePrefab();
            var catalogue = VesselCatalogue.Load();
            var entries = catalogue.Entries.Where(e => e.id != Id).ToList();
            entries.Add(CatalogueEntry(prefab));
            catalogue.Configure(entries.ToArray());
            EditorUtility.SetDirty(catalogue);
            AssetDatabase.SaveAssets();
            // The shared audio builder references existing recordings; no new audio files.
            ShipAudioBuilder.BuildProfiles();
            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ShipAudioBuilder.InstallSoundscape(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
        }

        public static VesselCatalogue.Entry CatalogueEntry(GameObject prefab) => new VesselCatalogue.Entry
        {
            id = Id, menuName = "СЕРПУХОВ\nР-96", vesselClass = "RIVER TUG-PUSHER\nSINGLE WATERJET", prefab = prefab
        };

        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Integrate();
                Review();
                Debug.Log("SERPUKHOV_MODEL|PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static GameObject UpdatePrefab()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string path = Folder + "/Serpukhov_R96.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Authored Serpukhov FBX is missing.");
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var materials = new Dictionary<string, Material>();
            foreach (Material slot in source.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials))
                if (!materials.ContainsKey(slot.name)) materials.Add(slot.name, MaterialFor(slot.name));
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            var data = JsonUtility.FromJson<VesselData>(json.text);
            if (!VesselDataValidator.TryValidate(data, out string error)) throw new InvalidOperationException(error);
            var root = new GameObject("Serpukhov_R96");
            try
            {
                var body = root.AddComponent<Rigidbody>();
                body.mass = data.massProperties.loadedMassKg;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                root.AddComponent<VesselDataLoader>().Configure(json);
                root.AddComponent<ShipPhysicsController>();
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                visual.name = "DetailedVisual";
                Vector3 origin = Point(root, "Waterline");
                Vector3 forward = Point(root, "BowReference") - origin;
                Vector3 up = Point(root, "UpReference") - origin;
                visual.transform.localRotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
                visual.transform.localPosition = -Point(root, "Waterline");
                if (Vector3.Distance(Point(root, "BowReference"), new Vector3(0, 0, 10.9f)) > .01f ||
                    Vector3.Distance(Point(root, "StarboardReference"), new Vector3(2.65f, 0, 0)) > .01f)
                    throw new InvalidOperationException("Export scale or axes do not match landmarks.");
                var lods = new LOD[3];
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>(true))
                {
                    r.gameObject.SetActive(true);
                    r.enabled = true;
                    r.sharedMaterials = r.sharedMaterials.Select(m => materials[m.name]).ToArray();
                }
                float[] heights = { .28f, .10f, .007f };
                for (int i = 0; i < 3; i++)
                {
                    Renderer[] renderers = visual.GetComponentsInChildren<Renderer>().Where(r => r.name.StartsWith("R96_LOD" + i + "_")).ToArray();
                    if (renderers.Length == 0) throw new InvalidOperationException("Missing LOD " + i);
                    lods[i] = new LOD(heights[i], renderers);
                }
                foreach (LODGroup old in visual.GetComponentsInChildren<LODGroup>()) Object.DestroyImmediate(old);
                var group = visual.AddComponent<LODGroup>();
                group.SetLODs(lods); group.RecalculateBounds();
                var layout = root.AddComponent<VesselLayout>();
                Vector3 eye = Point(root, "CameraNavigator");
                layout.Configure(Id, 21.8f / 138.3f, eye, eye + Vector3.forward * 80,
                    Point(root, "PortLight"), 5.15f, Point(root, "MastheadLight"), 5.16f,
                    Point(root, "MastheadLight"), 5.16f, Point(root, "SternLight"), .83f);
                layout.ConfigureSingleMasthead(true);
                layout.ConfigureNavigationSpill(.18f, .45f);
                layout.ConfigureCameraViews(new[] { new Vector3(10, 8, -25), new Vector3(0, 9, 3),
                    new Vector3(0, 24, 0), new Vector3(-16, 5, 0), new Vector3(16, 5, 0),
                    new Vector3(0, 5, 21), new Vector3(0, 5, -21), new Vector3(-8, 4, -6) });
                Collision(root);
                ShipAudioBuilder.InstallSoundscape(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Vector3 Point(GameObject root, string name) => root.transform.InverseTransformPoint(
            root.GetComponentsInChildren<Transform>(true).First(t => t.name == name).position);

        private static void Collision(GameObject root)
        {
            var group = new GameObject("CollisionHull"); group.transform.SetParent(root.transform, false);
            Box("Midship", new Vector3(0, .08f, -1), new Vector3(4.9f, 1.4f, 14));
            Box("Forecastle", new Vector3(0, .6f, 8.6f), new Vector3(4, 2.44f, 3.8f));
            Box("Stern", new Vector3(0, .35f, -8.5f), new Vector3(3.9f, .7f, 3));
            Box("SternTip", new Vector3(0, .58f, -10), new Vector3(2, .4f, 1.2f));
            foreach (float s in new[] { -1f, 1f })
                Box("PushingKnee" + s, new Vector3(s * 1.97f, 1.34f, 10.82f), new Vector3(.26f, 3.35f, .16f));
            Box("House", new Vector3(0, 1.91f, 1.85f), new Vector3(3.3f, 2.14f, 5.7f));
            Box("Bridge", new Vector3(0, 4.02f, 3.8f), new Vector3(3.14f, 2, 2.5f));
            void Box(string name, Vector3 centre, Vector3 size)
            {
                var o = new GameObject(name); o.transform.SetParent(group.transform, false);
                var c = o.AddComponent<BoxCollider>(); c.center = centre; c.size = size;
            }
        }

        private static Material MaterialFor(string name)
        {
            string directory = Folder + "/Materials";
            Directory.CreateDirectory(directory);
            string path = directory + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            Color color = name switch
            {
                "R96_Glass" => new Color(.55f, .65f, .67f, .20f),
                "R96_Dial" => new Color(.8f, .78f, .65f),
                "R96_Light_Red" => Color.red,
                "R96_Light_Green" => Color.green,
                "R96_Light_White" => new Color(.95f, .88f, .65f),
                "R96_Wear" => new Color(.23f, .105f, .045f),
                _ => new Color(.06f, .07f, .065f)
            };
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .4f);
            string stem = Folder + "/Textures/" + name;
            if (File.Exists(stem + "_BaseColor.png"))
            {
                m.SetTexture("_BaseMap", Texture(stem + "_BaseColor.png", true, false));
                m.SetTexture("_BumpMap", Texture(stem + "_Normal.png", false, true));
                m.SetTexture("_MetallicGlossMap", Texture(stem + "_MetalSmooth.png", false, false));
                m.SetColor("_BaseColor", Color.white); m.SetFloat("_Metallic", 1); m.SetFloat("_Smoothness", 1);
                m.EnableKeyword("_NORMALMAP"); m.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (name == "R96_Glass")
            {
                m.SetFloat("_Surface", 1); m.SetFloat("_ZWrite", 0); m.SetFloat("_Smoothness", .85f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent; m.SetShaderPassEnabled("ShadowCaster", false);
            }
            EditorUtility.SetDirty(m); return m;
        }

        private static Texture2D Texture(string path, bool srgb, bool normal)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var i = (TextureImporter)AssetImporter.GetAtPath(path);
            i.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            i.sRGBTexture = srgb; i.alphaIsTransparency = false; i.wrapMode = TextureWrapMode.Repeat;
            i.mipmapEnabled = true; i.anisoLevel = 4; i.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static void Review()
        {
            string directory = "Art/Source/Serpukhov_R96/Previews"; Directory.CreateDirectory(directory);
            var vessel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var sun = new GameObject("ReviewSun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.intensity = 1.6f; sun.transform.rotation = Quaternion.Euler(35, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .59f, .65f);
            var camera = new GameObject("ReviewCamera").AddComponent<Camera>();
            camera.fieldOfView = 38; camera.nearClipPlane = .05f; camera.farClipPlane = 200;
            camera.backgroundColor = new Color(.16f, .20f, .24f); camera.clearFlags = CameraClearFlags.SolidColor;
            var lod = vessel.GetComponentInChildren<LODGroup>();
            lod.ForceLOD(0);
            Shoot("unity-warmup", new Vector3(-17, 12, 23), new Vector3(0, 2, 0));
            for (int level = 0; level < 3; level++)
            {
                lod.ForceLOD(level);
                Shoot("unity-lod" + level, new Vector3(-17, 12, 23), new Vector3(0, 2, 0));
            }
            lod.ForceLOD(0);
            Shoot("unity-side", new Vector3(30, 4, 0), new Vector3(0, 2, 0));
            var layout = vessel.GetComponent<VesselLayout>();
            Shoot("unity-bridge", layout.NavigatorEye, layout.NavigatorLookAt);
            var data = vessel.GetComponent<VesselDataLoader>().Load();
            var p = VesselParameters.Create(data);
            float speed = ManoeuvringTrials.SteadySpeed(p, 1, float.PositiveInfinity);
            var turn = ManoeuvringTrials.TurningCircle(p, 1, float.PositiveInfinity);
            var stop = ManoeuvringTrials.CrashStop(p, float.PositiveInfinity);
            File.WriteAllText("Art/Source/Serpukhov_R96/trials.txt", $"Estimated prototype, not training validated.\nSpeed {speed:F3} m/s\nTurn completed {turn.Completed}, tactical diameter {turn.TacticalDiameterM:F2} m\nStop completed {stop.Completed}, reach {stop.TrackReachM:F2} m, time {stop.TimeS:F2} s\n");
            Debug.Log($"SERPUKHOV_TRIAL|speed={speed:F3}|turn={turn.TacticalDiameterM:F2}|stop={stop.TrackReachM:F2}");
            Object.DestroyImmediate(vessel); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(sun.gameObject);

            void Shoot(string name, Vector3 position, Vector3 target)
            {
                camera.transform.position = position; camera.transform.LookAt(target);
                var rt = new RenderTexture(1600, 1000, 24); var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); image.Apply();
                File.WriteAllBytes(directory + "/" + name + ".png", image.EncodeToPNG());
                camera.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
            }
        }
    }
}
