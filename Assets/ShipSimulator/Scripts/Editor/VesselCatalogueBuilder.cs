using System;
using System.IO;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // Builds the generated vessel prefabs, the selectable vessel catalogue and preview renders in Logs/Vessels.
    public static class VesselCatalogueBuilder
    {
        private const string CataloguePath = "Assets/ShipSimulator/Resources/VesselCatalogue.asset";
        private const string VolgoDonPrefabPath = "Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab";
        private const string PreviewFolder = "Logs/Vessels";

        [MenuItem("Ship Simulator/Build Vessel Catalogue")]
        public static void BuildFromMenu()
        {
            Build();
            RenderPreviews();
        }

        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Build();
                RenderPreviews();
                Debug.Log("VESSEL_CATALOGUE|PASS: prefabs, catalogue and previews in " + Path.GetFullPath(PreviewFolder));
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            EnsureVolgoDonLayout();
            GameObject volgoDon = AssetDatabase.LoadAssetAtPath<GameObject>(VolgoDonPrefabPath);
            GameObject volgoBalt = ProceduralVesselBuilder.Build(ProceduralVesselBuilder.VolgoBalt);
            GameObject volgoneft = ProceduralVesselBuilder.Build(ProceduralVesselBuilder.Volgoneft);
            GameObject meteor = ProceduralVesselBuilder.Build(ProceduralVesselBuilder.Meteor);
            GameObject luch = ProceduralVesselBuilder.Build(ProceduralVesselBuilder.Luch);

            var catalogue = AssetDatabase.LoadAssetAtPath<VesselCatalogue>(CataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<VesselCatalogue>();
                AssetDatabase.CreateAsset(catalogue, CataloguePath);
            }
            catalogue.Configure(new[]
            {
                new VesselCatalogue.Entry
                {
                    id = VesselCatalogue.DefaultVesselId, menuName = "VOLGO-DON\n507B",
                    vesselClass = "RIVER CLASS\nCARGO VESSEL", prefab = volgoDon
                },
                Entry(ProceduralVesselBuilder.VolgoBalt, volgoBalt),
                Entry(ProceduralVesselBuilder.Volgoneft, volgoneft),
                Entry(ProceduralVesselBuilder.Meteor, meteor),
                Entry(ProceduralVesselBuilder.Luch, luch)
            });
            EditorUtility.SetDirty(catalogue);
            AssetDatabase.SaveAssets();
        }

        private static VesselCatalogue.Entry Entry(ProceduralVesselBuilder.Design design, GameObject prefab) =>
            new VesselCatalogue.Entry { id = design.Id, menuName = design.MenuName, vesselClass = design.VesselClass, prefab = prefab };

        private static void EnsureVolgoDonLayout()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(VolgoDonPrefabPath);
            try
            {
                if (contents.GetComponent<VesselLayout>() != null) return;
                contents.AddComponent<VesselLayout>();
                PrefabUtility.SaveAsPrefabAsset(contents, VolgoDonPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        public static void RenderPreviews()
        {
            VesselCatalogue catalogue = AssetDatabase.LoadAssetAtPath<VesselCatalogue>(CataloguePath);
            Directory.CreateDirectory(PreviewFolder);
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.transform.localScale = new Vector3(80f, 1f, 80f);
            var waterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            waterMaterial.SetColor("_BaseColor", new Color(0.1f, 0.17f, 0.2f));
            waterMaterial.SetFloat("_Smoothness", 0.85f);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;

            var lightObject = new GameObject("Preview sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(38f, -140f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.46f, 0.52f, 0.58f);

            var cameraObject = new GameObject("Preview camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.64f, 0.74f, 0.82f);
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2000f;

            try
            {
                foreach (VesselCatalogue.Entry entry in catalogue.Entries)
                {
                    GameObject vessel = Object.Instantiate(entry.prefab);
                    vessel.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    float length = entry.LoadData().dimensions.lengthOverallM;
                    // Imported textures load on first use; an unsaved first frame keeps placeholders out of the captures.
                    Shoot(camera, new Vector3(0.62f, 0.26f, 0.72f) * length, Vector3.zero, null);
                    Shoot(camera, new Vector3(0.62f, 0.26f, 0.72f) * length, new Vector3(0f, 2f, 0.05f * length), $"{entry.id}-bow-quarter");
                    Shoot(camera, new Vector3(-0.6f, 0.2f, -0.7f) * length, new Vector3(0f, 3f, -0.1f * length), $"{entry.id}-stern-quarter");
                    Shoot(camera, new Vector3(1.05f * length, 4f, 0f), new Vector3(0f, 3f, 0f), $"{entry.id}-side");
                    Shoot(camera, new Vector3(0.18f * length, 0.9f * length, -0.05f * length), Vector3.zero, $"{entry.id}-top");
                    // Without the water plane, so foils, skegs, propellers and rudders can be checked.
                    water.SetActive(false);
                    Shoot(camera, new Vector3(0.5f, 0.12f, 0.6f) * length, new Vector3(0f, -0.3f, 0f), $"{entry.id}-underwater");
                    water.SetActive(true);
                    Object.DestroyImmediate(vessel);
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(water);
                Object.DestroyImmediate(waterMaterial);
            }
        }

        private static void Shoot(Camera camera, Vector3 position, Vector3 target, string name)
        {
            camera.transform.position = position;
            camera.transform.LookAt(target);
            const int width = 1600;
            const int height = 900;
            var texture = new RenderTexture(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            if (name != null) File.WriteAllBytes(Path.Combine(PreviewFolder, name + ".png"), image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(texture);
        }
    }
}
