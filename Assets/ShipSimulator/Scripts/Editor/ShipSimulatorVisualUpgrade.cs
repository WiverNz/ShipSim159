using System;
using System.IO;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShipSimulator.Editor
{
    public static class ShipSimulatorVisualUpgrade
    {
        private const string Root = "Assets/ShipSimulator";
        private const string ScenePath = Root + "/Scenes/RiverTrainingScene.unity";
        private const string ProfilePath = Root + "/Settings/RiverVisualProfile.asset";
        private const string WaterMeshPath = Root + "/Settings/RiverWaterMesh.asset";
        private const string LeftBankMeshPath = Root + "/Settings/LeftBankTerrain.asset";
        private const string RightBankMeshPath = Root + "/Settings/RightBankTerrain.asset";
        private const string LeftShoreMeshPath = Root + "/Settings/LeftShoreSlope.asset";
        private const string RightShoreMeshPath = Root + "/Settings/RightShoreSlope.asset";

        [MenuItem("Ship Simulator/Play Training Scene")]
        public static void PlayTrainingScene()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
        }

        [MenuItem("Ship Simulator/Stop Play Mode")]
        public static void StopPlayMode()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }

        [MenuItem("Ship Simulator/Apply Visual Upgrade")]
        public static void ApplyToTrainingScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ship Simulator] Visual upgrade applied.");
        }

        [MenuItem("Ship Simulator/Render Visual Preview")]
        public static void RenderVisualPreview()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject vessel = FindRoot(scene, "TrainingVessel");
            if (vessel == null) return;
            NavigationLightRig previewLights =
                vessel.GetComponent<NavigationLightRig>() ??
                vessel.AddComponent<NavigationLightRig>();
            previewLights.EnsureCreated();

            GameObject cameraObject = new GameObject("Visual Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            camera.fieldOfView = 48f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2500f;
            UniversalAdditionalCameraData cameraData =
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            Vector3 target = vessel.transform.position + new Vector3(0f, 4f, 28f);
            camera.transform.position = vessel.transform.position + new Vector3(82f, 42f, -105f);
            camera.transform.LookAt(target);
            RenderPreviewImage(camera, "G:/tmp/shipsim_visual_preview.png");

            camera.transform.position = vessel.transform.position + new Vector3(-72f, 34f, 115f);
            camera.transform.LookAt(vessel.transform.position + new Vector3(0f, 4f, -20f));
            RenderPreviewImage(camera, "G:/tmp/shipsim_visual_preview_rear.png");
            UnityEngine.Object.DestroyImmediate(cameraObject);
            previewLights.RemoveGeneratedObjects();
            UnityEngine.Object.DestroyImmediate(previewLights);
            Debug.Log("[Ship Simulator] Front and rear visual previews rendered.");
        }

        private static void RenderPreviewImage(Camera camera, string path)
        {
            var renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            image.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
            image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }

        public static void Apply(Scene scene)
        {
            EnsureFolder(Root + "/Settings");
            Transform environment = FindOrCreateRoot(scene, "Environment").transform;
            Transform visualRoot = ReplaceChild(environment, "VisualUpgrade");

            Material water = CreateWaterMaterial();
            Material earth = CreateLitMaterial(
                "RiverEarth", new Color(0.30f, 0.23f, 0.13f), 0f, 0.16f);
            Material grass = CreateLitMaterial(
                "RiverGrass", new Color(0.24f, 0.43f, 0.14f), 0f, 0.2f);
            Material grassLight = CreateLitMaterial(
                "RiverGrassLight", new Color(0.39f, 0.54f, 0.20f), 0f, 0.18f);
            Material foliage = CreateLitMaterial(
                "TreeFoliage", new Color(0.12f, 0.27f, 0.09f), 0f, 0.15f);
            Material foliageLight = CreateLitMaterial(
                "TreeFoliageLight", new Color(0.25f, 0.40f, 0.13f), 0f, 0.15f);
            Material bark = CreateLitMaterial(
                "TreeBark", new Color(0.18f, 0.10f, 0.055f), 0f, 0.12f);
            Material rock = CreateLitMaterial(
                "RiverRock", new Color(0.28f, 0.29f, 0.25f), 0f, 0.22f);
            Material reed = CreateLitMaterial(
                "RiverReed", new Color(0.38f, 0.46f, 0.13f), 0f, 0.1f);

            ConfigureExistingEnvironment(environment, water, earth);
            CreateBankLayers(visualRoot, earth, grass, grassLight);
            CreateVegetation(visualRoot, bark, foliage, foliageLight, rock, reed);
            ConfigureLighting(scene);
            ConfigurePostProcessing(scene);
            ConfigureCamera(scene);
            ConfigureNavigation(scene);
            ConfigureVesselFeedback(scene);
            TuneVesselMaterials();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ConfigureExistingEnvironment(
            Transform environment, Material water, Material earth)
        {
            Transform waterObject = environment.Find("RiverWater");
            if (waterObject != null)
            {
                waterObject.GetComponent<MeshFilter>().sharedMesh = CreateWaterMesh();
                waterObject.GetComponent<Renderer>().sharedMaterial = water;
                waterObject.localPosition = new Vector3(0f, 0f, 100f);
                waterObject.localRotation = Quaternion.identity;
                waterObject.localScale = Vector3.one;
            }

            foreach (string name in new[] { "LeftBankStraight", "RightBankStraight", "BendInnerBank" })
            {
                Transform bank = environment.Find(name);
                if (bank != null)
                {
                    Renderer renderer = bank.GetComponent<Renderer>();
                    if (renderer != null) renderer.enabled = false;
                    Collider collider = bank.GetComponent<Collider>();
                    if (collider != null) collider.enabled = false;
                }
            }
        }

        private static void CreateBankLayers(
            Transform root, Material earth, Material grass, Material grassLight)
        {
            CreateMeshObject("LeftShoreSlope", root,
                CreateBankMesh(LeftShoreMeshPath, -1f, true), earth, true);
            CreateMeshObject("RightShoreSlope", root,
                CreateBankMesh(RightShoreMeshPath, 1f, true), earth, true);
            CreateMeshObject("LeftBankTerrain", root,
                CreateBankMesh(LeftBankMeshPath, -1f, false), grass);
            CreateMeshObject("RightBankTerrain", root,
                CreateBankMesh(RightBankMeshPath, 1f, false), grassLight);

            var random = new System.Random(159);
            for (int i = 0; i < 54; i++)
            {
                float z = -1120f + i * 39f;
                float side = i % 2 == 0 ? -1f : 1f;
                float x = side * RandomRange(random, 94f, 155f);
                CreatePrimitive($"MeadowPatch_{i}", PrimitiveType.Sphere, root,
                    new Vector3(x, RandomRange(random, 3.2f, 6.5f), z),
                    new Vector3(RandomRange(random, 9f, 24f),
                        RandomRange(random, 0.35f, 1.1f),
                        RandomRange(random, 12f, 31f)),
                    Quaternion.Euler(0f, RandomRange(random, 0f, 180f), 0f),
                    i % 3 == 0 ? grassLight : grass, false);
            }
        }

        private static void CreateVegetation(
            Transform root, Material bark, Material foliage, Material foliageLight,
            Material rock, Material reed)
        {
            var random = new System.Random(507);
            for (int i = 0; i < 96; i++)
            {
                float z = -1120f + i * 21f + RandomRange(random, -8f, 8f);
                float leftX = RandomRange(random, -230f, -94f);
                float rightX = RandomRange(random, 94f, 230f);
                CreateTree(root, $"LeftTree_{i}", new Vector3(leftX, TerrainHeight(leftX, z), z),
                    RandomRange(random, 0.8f, 1.55f), bark,
                    i % 3 == 0 ? foliageLight : foliage);
                if (i % 3 != 1)
                    CreateTree(root, $"RightTree_{i}",
                        new Vector3(rightX, TerrainHeight(rightX, z + 11f), z + 11f),
                        RandomRange(random, 0.85f, 1.45f), bark,
                        i % 4 == 0 ? foliageLight : foliage);
            }

            for (int i = 0; i < 62; i++)
            {
                float z = -1090f + i * 32f;
                float side = i % 2 == 0 ? -1f : 1f;
                CreatePrimitive($"Rock_{i}", PrimitiveType.Sphere, root,
                    new Vector3(side * RandomRange(random, 74f, 88f), 0.2f, z),
                    new Vector3(RandomRange(random, 1.2f, 3.4f),
                        RandomRange(random, 0.6f, 1.5f),
                        RandomRange(random, 1.3f, 3.2f)),
                    Quaternion.Euler(RandomRange(random, 0f, 25f),
                        RandomRange(random, 0f, 180f), 0f), rock, false);
            }

            for (int i = 0; i < 140; i++)
            {
                float z = -1110f + i * 14f;
                float side = i % 2 == 0 ? -1f : 1f;
                CreatePrimitive($"Reed_{i}", PrimitiveType.Capsule, root,
                    new Vector3(side * RandomRange(random, 72f, 78f), 0.9f, z),
                    new Vector3(0.18f, RandomRange(random, 0.7f, 1.4f), 0.18f),
                    Quaternion.Euler(RandomRange(random, -6f, 6f), 0f,
                        RandomRange(random, -6f, 6f)), reed, false);
            }
        }

        private static void CreateTree(
            Transform root, string name, Vector3 position, float scale,
            Material bark, Material foliage)
        {
            GameObject tree = new GameObject(name);
            tree.transform.SetParent(root, false);
            tree.transform.localPosition = position;
            tree.transform.localScale = Vector3.one * scale;

            CreatePrimitive("Trunk", PrimitiveType.Cylinder, tree.transform,
                new Vector3(0f, 2.4f, 0f), new Vector3(0.55f, 2.4f, 0.55f),
                Quaternion.identity, bark, false);
            CreatePrimitive("CrownLow", PrimitiveType.Sphere, tree.transform,
                new Vector3(0f, 5.2f, 0f), new Vector3(3.2f, 2.4f, 3.2f),
                Quaternion.identity, foliage, false);
            CreatePrimitive("CrownHigh", PrimitiveType.Sphere, tree.transform,
                new Vector3(0.5f, 7.1f, -0.2f), new Vector3(2.4f, 2.2f, 2.4f),
                Quaternion.identity, foliage, false);
        }

        private static void ConfigureLighting(Scene scene)
        {
            Light sun = FindComponentInScene<Light>(scene, "Directional Light");
            if (sun != null)
            {
                sun.color = new Color(1f, 0.94f, 0.84f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.82f;
                sun.transform.rotation = Quaternion.Euler(34f, -42f, 0f);
            }

            Material sky = AssetDatabase.LoadAssetAtPath<Material>(
                Root + "/Materials/RiverSky.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural")) { name = "RiverSky" };
                AssetDatabase.CreateAsset(sky, Root + "/Materials/RiverSky.mat");
            }
            sky.SetFloat("_SunSize", 0.025f);
            sky.SetFloat("_AtmosphereThickness", 0.72f);
            sky.SetColor("_SkyTint", new Color(0.32f, 0.48f, 0.68f));
            sky.SetColor("_GroundColor", new Color(0.30f, 0.32f, 0.25f));
            sky.SetFloat("_Exposure", 0.92f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.53f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.40f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.57f, 0.67f, 0.73f);
            RenderSettings.fogDensity = 0.0011f;
        }

        private static void ConfigurePostProcessing(Scene scene)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            ClearProfile(profile);
            ColorAdjustments color = profile.Add<ColorAdjustments>();
            color.postExposure.Override(0.2f);
            color.contrast.Override(8f);
            color.saturation.Override(4f);
            color.colorFilter.Override(new Color(1f, 0.99f, 0.96f));

            Tonemapping tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            Bloom bloom = profile.Add<Bloom>();
            bloom.intensity.Override(0.16f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.55f);

            Vignette vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.62f);

            WhiteBalance balance = profile.Add<WhiteBalance>();
            balance.temperature.Override(1f);
            balance.tint.Override(-2f);
            EditorUtility.SetDirty(profile);

            GameObject volumeObject = FindOrCreateRoot(scene, "River Post Processing");
            Volume volume = volumeObject.GetComponent<Volume>();
            if (volume == null) volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
        }

        private static void ConfigureCamera(Scene scene)
        {
            Camera camera = FindComponentInScene<Camera>(scene, "Main Camera");
            if (camera == null) return;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.fieldOfView = 52f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2500f;
            camera.clearFlags = CameraClearFlags.Skybox;

            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            ShipFollowCamera follow = camera.GetComponent<ShipFollowCamera>();
            if (follow != null)
            {
                SerializedObject serializedFollow = new SerializedObject(follow);
                SerializedProperty views = serializedFollow.FindProperty("localViews");
                Vector3[] positions =
                {
                    new Vector3(28f, 18f, -58f),
                    new Vector3(0f, 13f, -18f),
                    new Vector3(0f, 68f, -22f),
                    new Vector3(-34f, 12f, -12f),
                    new Vector3(34f, 12f, -12f),
                    new Vector3(0f, 9f, 58f),
                    new Vector3(0f, 10f, -68f),
                    new Vector3(-22f, 7f, -28f)
                };
                views.arraySize = positions.Length;
                for (int i = 0; i < positions.Length; i++)
                    views.GetArrayElementAtIndex(i).vector3Value = positions[i];
                serializedFollow.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ConfigureNavigation(Scene scene)
        {
            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.gameObject.scene == scene && light.type != LightType.Directional)
                    light.shadows = LightShadows.None;
            }

            GameObject navigation = FindRoot(scene, "Navigation");
            if (navigation == null) return;
            RebuildNavigationBuoys(navigation);
            foreach (Transform marker in navigation.transform)
                marker.localScale = Vector3.one * 1.45f;
            foreach (Renderer renderer in navigation.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        [MenuItem("Ship Simulator/Arrange Navigation Buoys")]
        public static void ArrangeNavigationBuoys()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "[Ship Simulator] Stop Play Mode before arranging navigation buoys.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject navigation = FindRoot(scene, "Navigation");
            if (navigation == null) return;
            RebuildNavigationBuoys(navigation);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Ship Simulator] Navigation buoys arranged along the fairway.");
        }

        [MenuItem("Ship Simulator/Apply Navigation And Collision Upgrade")]
        public static void ApplyNavigationAndCollisionUpgrade()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning(
                    "[Ship Simulator] Stop Play Mode before upgrading navigation and collision.");
                return;
            }

            const string vesselPrefabPath =
                "Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab";
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(vesselPrefabPath);
            try
            {
                VolgoDonModelIntegrator.EnsureCollisionHull(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, vesselPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Apply(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ship Simulator] Navigation, depth and collision upgrade applied.");
        }

        private static void RebuildNavigationBuoys(GameObject navigation)
        {
            for (int i = navigation.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = navigation.transform.GetChild(i);
                if (child.name.Contains("Buoy"))
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            Material red = GetOrCreateNavigationMaterial(
                "NavigationRed", new Color(0.88f, 0.035f, 0.02f));
            Material black = GetOrCreateNavigationMaterial(
                "NavigationBlack", new Color(0.025f, 0.035f, 0.04f));
            Material white = GetOrCreateNavigationMaterial(
                "NavigationWhite", new Color(0.95f, 0.95f, 0.82f));
            GameObject redPrefab = CreateRuntimeBuoyPrefab("RiverBuoy", red, white);
            GameObject leftPrefab = CreateRuntimeBuoyPrefab(
                "RiverBuoyLeft", white, black);
            ShipSimulatorPrototypeBuilder.PlaceFairwayBuoys(
                navigation.transform, redPrefab, leftPrefab);
            UnityEngine.Object.DestroyImmediate(redPrefab);
            UnityEngine.Object.DestroyImmediate(leftPrefab);
        }

        private static GameObject CreateRuntimeBuoyPrefab(
            string name, Material bodyMaterial, Material topMaterial)
        {
            GameObject root = new GameObject(name);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Float";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            body.transform.localScale = new Vector3(1.2f, 0.8f, 1.2f);
            body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            top.name = "TopMark";
            top.transform.SetParent(root.transform, false);
            top.transform.localPosition = new Vector3(0f, 2f, 0f);
            top.transform.localScale = Vector3.one * 0.65f;
            top.GetComponent<Renderer>().sharedMaterial = topMaterial;
            return root;
        }

        private static Material GetOrCreateNavigationMaterial(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = name
                };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureVesselFeedback(Scene scene)
        {
            GameObject vessel = FindRoot(scene, "TrainingVessel");
            if (vessel != null && vessel.GetComponent<ShipWakeController>() == null)
                vessel.AddComponent<ShipWakeController>();
        }

        private static void TuneVesselMaterials()
        {
            string materialRoot = Root + "/Models/VolgoDon507/Materials";
            foreach (string path in AssetDatabase.FindAssets("t:Material", new[] { materialRoot }))
            {
                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(path));
                if (material == null || material.name.Contains("proxy")) continue;
                bool metal = material.name.Contains("hull") ||
                    material.name.Contains("detali") ||
                    material.name.Contains("deck");
                material.SetFloat("_Metallic", metal ? 0.18f : 0.04f);
                material.SetFloat("_Smoothness",
                    material.name.Contains("hull") ? 0.42f :
                    material.name.Contains("rubka") ? 0.32f : 0.26f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                EditorUtility.SetDirty(material);
            }
        }

        private static Material CreateWaterMaterial()
        {
            string path = Root + "/Materials/RiverWater.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("ShipSimulator/RiverWater");
            if (material == null)
            {
                material = new Material(shader) { name = "RiverWater" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_ShallowColor", new Color(0.16f, 0.31f, 0.28f, 0.9f));
            material.SetColor("_DeepColor", new Color(0.035f, 0.12f, 0.13f, 0.96f));
            material.SetColor("_ReflectionTint", new Color(0.48f, 0.58f, 0.62f, 1f));
            material.SetColor("_FoamColor", new Color(0.68f, 0.73f, 0.67f, 1f));
            material.SetFloat("_Smoothness", 0.7f);
            material.SetFloat("_WaveScale", 0.055f);
            material.SetFloat("_WaveHeight", 0.03f);
            material.SetFloat("_WaveSpeed", 0.42f);
            material.SetVector("_FlowDirection", new Vector4(0.08f, -1f, 0f, 0f));
            material.SetFloat("_RippleScale", 0.38f);
            material.SetFloat("_RippleStrength", 0.16f);
            material.SetFloat("_StreakScale", 0.075f);
            material.SetFloat("_Turbidity", 0.62f);
            material.SetFloat("_ReflectionStrength", 0.48f);
            material.SetFloat("_FresnelPower", 4.2f);
            material.SetFloat("_Opacity", 0.94f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh CreateWaterMesh()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(WaterMeshPath);
            if (existing != null && existing.bounds.size.z >= 2399f) return existing;
            if (existing != null) AssetDatabase.DeleteAsset(WaterMeshPath);

            const int columns = 48;
            const int rows = 320;
            const float width = 180f;
            const float length = 2400f;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];

            for (int row = 0; row <= rows; row++)
            {
                float row01 = row / (float)rows;
                for (int column = 0; column <= columns; column++)
                {
                    float column01 = column / (float)columns;
                    int index = row * (columns + 1) + column;
                    vertices[index] = new Vector3(
                        (column01 - 0.5f) * width, 0f, (row01 - 0.5f) * length);
                    uv[index] = new Vector2(column01, row01);
                }
            }

            int triangle = 0;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int lowerLeft = row * (columns + 1) + column;
                int lowerRight = lowerLeft + 1;
                int upperLeft = lowerLeft + columns + 1;
                int upperRight = upperLeft + 1;
                triangles[triangle++] = lowerLeft;
                triangles[triangle++] = upperLeft;
                triangles[triangle++] = lowerRight;
                triangles[triangle++] = lowerRight;
                triangles[triangle++] = upperLeft;
                triangles[triangle++] = upperRight;
            }

            var mesh = new Mesh
            {
                name = "RiverWaterMesh",
                indexFormat = IndexFormat.UInt32,
                vertices = vertices,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, WaterMeshPath);
            return mesh;
        }

        private static Mesh CreateBankMesh(string path, float side, bool shoreOnly)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
                AssetDatabase.DeleteAsset(path);

            const int rows = 132;
            int columns = shoreOnly ? 2 : 8;
            const float startZ = -1200f;
            const float endZ = 1000f;
            var vertices = new Vector3[(rows + 1) * (columns + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[rows * columns * 6];

            for (int row = 0; row <= rows; row++)
            {
                float z01 = row / (float)rows;
                float z = Mathf.Lerp(startZ, endZ, z01);
                float shoreline = FairwayModel.ShoreDistance(z);
                float centerX = FairwayModel.CenterX(z);

                for (int column = 0; column <= columns; column++)
                {
                    float across = column / (float)columns;
                    float distance = shoreOnly
                        ? Mathf.Lerp(shoreline, shoreline + 20f, across)
                        : Mathf.Lerp(shoreline + 18f, 680f, across);
                    float x = centerX + side * distance;
                    float height = shoreOnly
                        ? Mathf.Lerp(0.05f, TerrainHeight(x, z), across)
                        : TerrainHeight(x, z);
                    int index = row * (columns + 1) + column;
                    vertices[index] = new Vector3(x, height, z);
                    uv[index] = new Vector2(across * 8f, z01 * 18f);
                }
            }

            int triangle = 0;
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int lowerLeft = row * (columns + 1) + column;
                int lowerRight = lowerLeft + 1;
                int upperLeft = lowerLeft + columns + 1;
                int upperRight = upperLeft + 1;
                if (side < 0f)
                {
                    triangles[triangle++] = lowerLeft;
                    triangles[triangle++] = lowerRight;
                    triangles[triangle++] = upperLeft;
                    triangles[triangle++] = lowerRight;
                    triangles[triangle++] = upperRight;
                    triangles[triangle++] = upperLeft;
                }
                else
                {
                    triangles[triangle++] = lowerLeft;
                    triangles[triangle++] = upperLeft;
                    triangles[triangle++] = lowerRight;
                    triangles[triangle++] = lowerRight;
                    triangles[triangle++] = upperLeft;
                    triangles[triangle++] = upperRight;
                }
            }

            var mesh = new Mesh
            {
                name = System.IO.Path.GetFileNameWithoutExtension(path),
                indexFormat = IndexFormat.UInt32,
                vertices = vertices,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static float ShoreNoise(float z)
        {
            return Mathf.Sin(z * 0.018f) * 4.5f +
                Mathf.Sin(z * 0.047f + 1.7f) * 2.2f +
                Mathf.PerlinNoise(2.31f, z * 0.008f) * 7f - 3.5f;
        }

        private static float TerrainHeight(float x, float z)
        {
            float shoreDistance = Mathf.Max(
                0f, Mathf.Abs(x - FairwayModel.CenterX(z)) - 76f);
            float rise = Mathf.SmoothStep(2.8f, 11f, Mathf.Clamp01(shoreDistance / 150f));
            float broadHills = Mathf.PerlinNoise(
                (x + 720f) * 0.0038f, (z + 680f) * 0.0038f) * 13f;
            float detail = Mathf.PerlinNoise(
                (x + 190f) * 0.014f, (z - 320f) * 0.014f) * 2.6f;
            return rise + broadHills + detail;
        }

        private static GameObject CreateMeshObject(
            string name, Transform parent, Mesh mesh, Material material,
            bool addCollider = false)
        {
            var instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            instance.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (addCollider)
                instance.AddComponent<MeshCollider>().sharedMesh = mesh;
            return instance;
        }

        private static Material CreateLitMaterial(
            string name, Color color, float metallic, float smoothness)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = name
                };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrimitive(
            string name, PrimitiveType type, Transform parent, Vector3 position,
            Vector3 scale, Quaternion rotation, Material material, bool collider)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = position;
            primitive.transform.localRotation = rotation;
            primitive.transform.localScale = scale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
            {
                Collider existingCollider = primitive.GetComponent<Collider>();
                if (existingCollider != null) UnityEngine.Object.DestroyImmediate(existingCollider);
            }
            return primitive;
        }

        private static void ClearProfile(VolumeProfile profile)
        {
            while (profile.components.Count > 0)
                profile.Remove(profile.components[0].GetType());
        }

        private static Transform ReplaceChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static GameObject FindOrCreateRoot(Scene scene, string name)
        {
            GameObject root = FindRoot(scene, name);
            if (root != null) return root;
            root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            return root;
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static T FindComponentInScene<T>(Scene scene, string objectName)
            where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (T component in root.GetComponentsInChildren<T>(true))
                    if (component.gameObject.name == objectName) return component;
            }
            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static float RandomRange(System.Random random, float minimum, float maximum)
        {
            return minimum + (float)random.NextDouble() * (maximum - minimum);
        }
    }
}
