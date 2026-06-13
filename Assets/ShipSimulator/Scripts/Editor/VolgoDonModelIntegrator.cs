using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    public static class VolgoDonModelIntegrator
    {
        private const string ModelRoot = "Assets/ShipSimulator/Models/VolgoDon507";
        private const string ModelPath = ModelRoot + "/volgo_don.fbx";
        private const string MaterialRoot = ModelRoot + "/Materials";
        private const string VesselPrefabPath =
            "Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab";
        private const float TargetLengthM = 138.3f;
        private const float LoadedDraftM = 3.53f;

        private static readonly string[] PlaceholderObjects =
        {
            "Hull", "Bow", "Deck", "CargoHold", "AftSuperstructure",
            "Wheelhouse", "BowDirectionMarker", "PortPropellerDebugMarker",
            "StarboardPropellerDebugMarker"
        };

        [MenuItem("Ship Simulator/Integrate Detailed Vessel Model")]
        public static void IntegratePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VesselPrefabPath) == null)
            {
                Debug.LogError("Vessel prefab is missing: " + VesselPrefabPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(VesselPrefabPath);
            try
            {
                root.name = "VolgoDon507B";
                RemovePlaceholderVisuals(root);
                AddDetailedVisual(root);
                EnsureCollisionHull(root);
                HideControlMarkers(root);
                PrefabUtility.SaveAsPrefabAsset(root, VesselPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            UpdateScenePrefabInstances();
            AssetDatabase.SaveAssets();
            RenderPreview();
            Debug.Log("[VolgoDon Model] Detailed model integrated into " + VesselPrefabPath);
        }

        [MenuItem("Ship Simulator/Render Detailed Vessel Preview")]
        public static void RenderPreview()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(VesselPrefabPath);
            if (source == null) return;

            GameObject vessel = Object.Instantiate(source);
            vessel.hideFlags = HideFlags.HideAndDontSave;
            vessel.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (MonoBehaviour behaviour in vessel.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;

            Renderer[] renderers = vessel.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = CalculateBounds(renderers);

            GameObject cameraObject = new GameObject("Preview Camera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.2f);
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.transform.position = bounds.center + new Vector3(120f, 65f, -170f);
            camera.transform.LookAt(bounds.center);

            GameObject lightObject = new GameObject("Preview Light")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            var renderTexture = new RenderTexture(1200, 700, 24);
            var image = new Texture2D(1200, 700, TextureFormat.RGB24, false);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0, 0, 1200, 700), 0, 0);
            image.Apply();

            string previewPath = "G:/tmp/volgodon507_preview.png";
            Directory.CreateDirectory(Path.GetDirectoryName(previewPath));
            File.WriteAllBytes(previewPath, image.EncodeToPNG());

            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(vessel);
            Debug.Log("[VolgoDon Model] Preview rendered to " + previewPath);
        }

        [MenuItem("Ship Simulator/Inspect Detailed Vessel Model")]
        public static void Inspect()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null)
            {
                Debug.LogError("Detailed vessel model is missing: " + ModelPath);
                return;
            }

            GameObject instance = Object.Instantiate(source);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var report = new StringBuilder();
            report.AppendLine("[VolgoDon Model] Import inspection");
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = CalculateBounds(renderers);
            report.AppendLine($"Bounds center={bounds.center}, size={bounds.size}");
            report.AppendLine($"Renderers={renderers.Length}");

            foreach (Renderer renderer in renderers)
            {
                report.Append(renderer.transform.GetHierarchyPath(instance.transform));
                report.Append(" | ");
                report.Append(renderer.GetType().Name);
                report.Append(" | materials: ");
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (i > 0) report.Append(", ");
                    report.Append(materials[i] != null ? materials[i].name : "<null>");
                }
                report.AppendLine();
            }

            Debug.Log(report.ToString());
            Object.DestroyImmediate(instance);
        }

        public static bool AddDetailedVisual(GameObject vesselRoot)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) return false;

            Transform previous = vesselRoot.transform.Find("DetailedVisual");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);

            ConfigureModelImporter();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(source, vesselRoot.transform);
            visual.name = "DetailedVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            Bounds initialBounds = CalculateBounds(renderers);
            // The CRYENGINE source uses X forward, Y starboard and Z up.
            // Convert it to Unity axes, then turn the imported bow toward local +Z,
            // which is also the forward direction used by propulsion and steering.
            visual.transform.localRotation =
                Quaternion.Euler(0f, 180f, 0f) *
                Quaternion.LookRotation(Vector3.up, Vector3.right);

            float importedLength = Mathf.Max(initialBounds.size.x, initialBounds.size.z);
            float scale = TargetLengthM / Mathf.Max(0.01f, importedLength);
            visual.transform.localScale = Vector3.one * scale;

            AssignMaterials(renderers, materials);
            RemoveImportedComponents(visual);

            Bounds alignedBounds = CalculateBounds(renderers);
            Vector3 offset = new Vector3(
                -alignedBounds.center.x,
                -LoadedDraftM - alignedBounds.min.y,
                -alignedBounds.center.z);
            visual.transform.position += offset;

            Bounds finalBounds = CalculateBounds(renderers);
            Debug.Log(
                $"[VolgoDon Model] Visual aligned. Source={initialBounds.size}, " +
                $"final={finalBounds.size}, center={finalBounds.center}, scale={scale:F4}");
            return true;
        }

        public static void EnsureCollisionHull(GameObject vesselRoot)
        {
            Transform existing = vesselRoot.transform.Find("CollisionHull");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            GameObject collisionHull = new GameObject("CollisionHull");
            collisionHull.transform.SetParent(vesselRoot.transform, false);
            AddHullSection(collisionHull.transform, "MidshipCollision",
                new Vector3(0f, -1.25f, -5f), new Vector3(16.2f, 4.6f, 82f));
            AddHullSection(collisionHull.transform, "BowCollision",
                new Vector3(0f, -1.05f, 48f), new Vector3(11.5f, 4.1f, 28f));
            AddHullSection(collisionHull.transform, "SternCollision",
                new Vector3(0f, -1.15f, -54f), new Vector3(13.5f, 4.3f, 22f));
        }

        private static void AddHullSection(
            Transform parent, string name, Vector3 center, Vector3 size)
        {
            GameObject section = new GameObject(name);
            section.transform.SetParent(parent, false);
            BoxCollider collider = section.AddComponent<BoxCollider>();
            collider.center = center;
            collider.size = size;
        }

        public static void HideControlMarkers(GameObject vesselRoot)
        {
            Transform rudder = vesselRoot.transform.Find("RudderDebugMarker");
            if (rudder == null) return;

            Renderer renderer = rudder.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = false;
            Collider collider = rudder.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        private static void ConfigureModelImporter()
        {
            if (AssetImporter.GetAtPath(ModelPath) is not ModelImporter importer) return;

            bool changed = importer.importAnimation || importer.importCameras || importer.importLights;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            if (changed) importer.SaveAndReimport();
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MaterialRoot))
                AssetDatabase.CreateFolder(ModelRoot, "Materials");

            var materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            AddTexturedMaterial(materials, "ship_type_507_interior", "ship_type_507_interior_ao.dds");
            AddTexturedMaterial(materials, "ship_type_507_lights", "ship_type_507_lights.hdr", true);
            AddTexturedMaterial(materials, "ship_type_507_rubka_detali", "ship_type_507_rubka_detali_ao.dds");
            AddTexturedMaterial(materials, "ship_type_507_hull", "ship_type_507_hull_ao.dds");
            AddTexturedMaterial(materials, "ship_type_507_boats", "ship_type_507_boats_ao.dds");
            AddTexturedMaterial(materials, "ship_type_507_hold", "ship_type_507_hold_ao.dds");
            AddTexturedMaterial(materials, "ship_type_507_rubka", "ship_type_507_rubka_ao.dds");
            AddTexturedMaterial(materials, "ship_type_507_deck", "ship_type_507_deck_ao.dds");
            AddTexturedMaterial(materials, "ship_type_507_detali", "ship_type_507_detali_ao.dds");
            materials["ship_gms_axalp_main"] =
                CreateMaterial("ship_gms_axalp_main", null, new Color(0.55f, 0.58f, 0.6f), false);
            materials["<unassigned>"] =
                CreateMaterial("unassigned", null, new Color(0.2f, 0.22f, 0.23f), false);
            materials["proxy_mat"] = CreateProxyMaterial();
            return materials;
        }

        private static void AddTexturedMaterial(
            IDictionary<string, Material> materials, string name, string textureFile, bool emissive = false)
        {
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(ModelRoot + "/" + textureFile);
            materials[name] = CreateMaterial(name, texture, Color.white, emissive);
        }

        private static Material CreateMaterial(
            string name, Texture texture, Color color, bool emissive)
        {
            string path = MaterialRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Smoothness", name.Contains("hull") ? 0.2f : 0.35f);
            if (emissive && texture != null)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.white * 1.5f);
                material.SetTexture("_EmissionMap", texture);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateProxyMaterial()
        {
            Material material = CreateMaterial("proxy_mat", null, new Color(0f, 0f, 0f, 0f), false);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0f));
            material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignMaterials(
            IEnumerable<Renderer> renderers, IReadOnlyDictionary<string, Material> materials)
        {
            foreach (Renderer renderer in renderers)
            {
                Material[] assigned = renderer.sharedMaterials;
                bool proxyOnly = assigned.Length > 0;
                for (int i = 0; i < assigned.Length; i++)
                {
                    string materialName = assigned[i] != null ? assigned[i].name : "<unassigned>";
                    proxyOnly &= materialName.Equals("proxy_mat", StringComparison.OrdinalIgnoreCase);
                    if (materials.TryGetValue(materialName, out Material replacement))
                        assigned[i] = replacement;
                }
                renderer.sharedMaterials = assigned;
                if (proxyOnly || renderer.name.Contains("proxy", StringComparison.OrdinalIgnoreCase))
                    renderer.enabled = false;
            }
        }

        private static void RemoveImportedComponents(GameObject visual)
        {
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            foreach (Rigidbody body in visual.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(body);
            foreach (Animator animator in visual.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(animator);
            foreach (Animation animation in visual.GetComponentsInChildren<Animation>(true))
                Object.DestroyImmediate(animation);
        }

        private static void RemovePlaceholderVisuals(GameObject root)
        {
            foreach (string objectName in PlaceholderObjects)
            {
                Transform child = root.transform.Find(objectName);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
        }

        private static void UpdateScenePrefabInstances()
        {
            string scenePath = "Assets/ShipSimulator/Scenes/RiverTrainingScene.unity";
            UnityEngine.SceneManagement.Scene scene =
                UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded) return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) ==
                    AssetDatabase.LoadAssetAtPath<GameObject>(VesselPrefabPath))
                {
                    PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                }
            }
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static string GetHierarchyPath(this Transform transform, Transform root)
        {
            string path = transform.name;
            while (transform.parent != null && transform.parent != root)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
    }
}
