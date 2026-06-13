using System.IO;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShipSimulator.Editor
{
    public static class ShipSimulatorPrototypeBuilder
    {
        private const string Root = "Assets/ShipSimulator";
        private const string VesselPrefabPath = Root + "/Prefabs/Vessels/VolgoDon507B.prefab";
        private const string BuoyPrefabPath = Root + "/Prefabs/Navigation/RiverBuoy.prefab";
        private const string LeftBuoyPrefabPath = Root + "/Prefabs/Navigation/RiverBuoyLeft.prefab";
        private const string MarkerPrefabPath = Root + "/Prefabs/Navigation/NavigationMarker.prefab";
        private const string BankPrefabPath = Root + "/Prefabs/Environment/RiverBankSegment.prefab";
        private const string ScenePath = Root + "/Scenes/RiverTrainingScene.unity";

        [MenuItem("Ship Simulator/Build Prototype")]
        public static void Build()
        {
            EnsureFolders();
            Material hull = CreateMaterial("Hull", new Color(0.12f, 0.16f, 0.19f));
            Material deck = CreateMaterial("Deck", new Color(0.45f, 0.18f, 0.08f));
            Material superstructure = CreateMaterial("Superstructure", new Color(0.82f, 0.84f, 0.79f));
            Material water = CreateMaterial("RiverWater", new Color(0.06f, 0.32f, 0.37f, 0.82f), true);
            Material bank = CreateMaterial("RiverBank", new Color(0.25f, 0.38f, 0.12f));
            Material red = CreateMaterial("NavigationRed", new Color(0.75f, 0.06f, 0.04f));
            Material white = CreateMaterial("NavigationWhite", new Color(0.9f, 0.9f, 0.82f));
            Material black = CreateMaterial("MarkerBlack", new Color(0.03f, 0.03f, 0.03f));

            CreateBankPrefab(bank);
            CreateBuoyPrefab(red, white);
            CreateBuoyPrefab(white, black, LeftBuoyPrefabPath, "RiverBuoyLeft");
            CreateMarkerPrefab(white, black);
            GameObject vesselPrefab = CreateVesselPrefab(hull, deck, superstructure, red);
            CreateTrainingScene(vesselPrefab, water, bank);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Ship simulator prototype built: " + ScenePath);
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                Root + "/Materials", Root + "/Prefabs", Root + "/Prefabs/Vessels",
                Root + "/Prefabs/Environment", Root + "/Prefabs/Navigation", Root + "/Scenes"
            };
            foreach (string folder in folders) Directory.CreateDirectory(folder);
        }

        private static Material CreateMaterial(string name, Color color, bool transparent = false)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", name == "RiverWater" ? 0.8f : 0.25f);
            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void CreateBankPrefab(Material bankMaterial)
        {
            GameObject root = new GameObject("RiverBankSegment");
            Primitive("Bank", PrimitiveType.Cube, root.transform, Vector3.zero, new Vector3(40f, 5f, 100f), bankMaterial);
            PrefabUtility.SaveAsPrefabAsset(root, BankPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static void CreateBuoyPrefab(Material color, Material white,
            string path = BuoyPrefabPath, string name = "RiverBuoy")
        {
            GameObject root = new GameObject(name);
            Primitive("Float", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.6f, 0f),
                new Vector3(1.2f, 0.8f, 1.2f), color);
            Primitive("TopMark", PrimitiveType.Sphere, root.transform, new Vector3(0f, 2f, 0f), Vector3.one * 0.65f, white);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void CreateMarkerPrefab(Material white, Material black)
        {
            GameObject root = new GameObject("NavigationMarker");
            Primitive("Post", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 3f, 0f), new Vector3(0.35f, 3f, 0.35f), black);
            Primitive("Board", PrimitiveType.Cube, root.transform, new Vector3(0f, 6.5f, 0f), new Vector3(3f, 2f, 0.3f), white);
            PrefabUtility.SaveAsPrefabAsset(root, MarkerPrefabPath);
            Object.DestroyImmediate(root);
        }

        private static GameObject CreateVesselPrefab(Material hull, Material deck, Material superstructure, Material marker)
        {
            GameObject root = new GameObject("VolgoDon507B");
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            root.AddComponent<VesselDataLoader>();
            root.AddComponent<HydrodynamicResistance>();
            root.AddComponent<ShipPhysicsController>();

            if (!VolgoDonModelIntegrator.AddDetailedVisual(root))
            {
                Primitive("Hull", PrimitiveType.Cube, root.transform, new Vector3(0f, -1.2f, 0f), new Vector3(16.7f, 4.2f, 128f), hull);
                Primitive("Bow", PrimitiveType.Cube, root.transform, new Vector3(0f, -0.8f, 66f), new Vector3(10f, 3.5f, 10f), hull).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                Primitive("Deck", PrimitiveType.Cube, root.transform, new Vector3(0f, 1.15f, -3f), new Vector3(15.8f, 0.35f, 128f), deck);
                Primitive("CargoHold", PrimitiveType.Cube, root.transform, new Vector3(0f, 2f, 8f), new Vector3(12.5f, 1.4f, 88f), hull);
                Primitive("AftSuperstructure", PrimitiveType.Cube, root.transform, new Vector3(0f, 5f, -48f), new Vector3(13f, 7f, 20f), superstructure);
                Primitive("Wheelhouse", PrimitiveType.Cube, root.transform, new Vector3(0f, 10f, -44f), new Vector3(10f, 3f, 8f), superstructure);
                Primitive("BowDirectionMarker", PrimitiveType.Cube, root.transform, new Vector3(0f, 3f, 63f), new Vector3(2f, 1f, 5f), marker);
            }
            VolgoDonModelIntegrator.EnsureCollisionHull(root);

            Transform propulsionPoint = new GameObject("PropulsionPoint").transform;
            propulsionPoint.SetParent(root.transform, false);
            propulsionPoint.localPosition = new Vector3(0f, -1.5f, -57f);
            propulsionPoint.gameObject.AddComponent<PropulsionController>();
            GameObject portPropeller = Primitive("PortPropellerDebugMarker", PrimitiveType.Cylinder, root.transform,
                new Vector3(-4f, -1.5f, -57f), new Vector3(1.8f, 0.2f, 1.8f), marker);
            portPropeller.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Object.DestroyImmediate(portPropeller.GetComponent<Collider>());
            GameObject starboardPropeller = Primitive("StarboardPropellerDebugMarker", PrimitiveType.Cylinder, root.transform,
                new Vector3(4f, -1.5f, -57f), new Vector3(1.8f, 0.2f, 1.8f), marker);
            starboardPropeller.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Object.DestroyImmediate(starboardPropeller.GetComponent<Collider>());

            Transform rudderPoint = Primitive("RudderDebugMarker", PrimitiveType.Cube, root.transform,
                new Vector3(0f, -1.2f, -59f), new Vector3(3f, 2f, 0.35f), marker).transform;
            rudderPoint.gameObject.AddComponent<RudderController>();
            VolgoDonModelIntegrator.HideControlMarkers(root);

            float[] zPositions = { -48f, -24f, 0f, 24f, 48f };
            float[] xPositions = { -5.5f, 0f, 5.5f };
            Transform buoyancyRoot = new GameObject("BuoyancyPoints").transform;
            buoyancyRoot.SetParent(root.transform, false);
            foreach (float z in zPositions)
            foreach (float x in xPositions)
            {
                GameObject point = new GameObject($"Buoyancy_{x}_{z}");
                point.transform.SetParent(buoyancyRoot, false);
                point.transform.localPosition = new Vector3(x, -1.8f, z);
                point.AddComponent<BuoyancyPoint>();
            }

            TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Data/Vessels/VolgoDon507B.json");
            SerializedObject loader = new SerializedObject(root.GetComponent<VesselDataLoader>());
            loader.FindProperty("vesselJson").objectReferenceValue = json;
            loader.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, VesselPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateTrainingScene(GameObject vesselPrefab, Material water, Material bank)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject environment = new GameObject("Environment");
            GameObject waterSurface = Primitive("RiverWater", PrimitiveType.Cube, environment.transform,
                new Vector3(0f, -0.75f, 220f), new Vector3(180f, 1.5f, 900f), water);
            Object.DestroyImmediate(waterSurface.GetComponent<Collider>());
            Primitive("LeftBankStraight", PrimitiveType.Cube, environment.transform, new Vector3(-120f, 2f, 180f), new Vector3(80f, 6f, 900f), bank);
            Primitive("RightBankStraight", PrimitiveType.Cube, environment.transform, new Vector3(120f, 2f, 180f), new Vector3(80f, 6f, 900f), bank);
            Primitive("BendInnerBank", PrimitiveType.Cube, environment.transform, new Vector3(40f, 2f, 610f), new Vector3(160f, 6f, 220f), bank).transform.localRotation = Quaternion.Euler(0f, -25f, 0f);

            GameObject current = new GameObject("RiverCurrent");
            current.transform.position = new Vector3(0f, 0f, 200f);
            BoxCollider currentCollider = current.AddComponent<BoxCollider>();
            currentCollider.isTrigger = true;
            currentCollider.size = new Vector3(180f, 20f, 900f);
            current.AddComponent<RiverCurrentZone>();

            GameObject buoyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuoyPrefabPath);
            GameObject leftBuoyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LeftBuoyPrefabPath);
            GameObject markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MarkerPrefabPath);
            GameObject navigation = new GameObject("Navigation");
            PlaceFairwayBuoys(navigation.transform, buoyPrefab, leftBuoyPrefab);
            PlacePrefab(markerPrefab, navigation.transform, new Vector3(-76f, 0f, 180f), Quaternion.Euler(0f, 90f, 0f));
            PlacePrefab(markerPrefab, navigation.transform, new Vector3(76f, 0f, 520f), Quaternion.Euler(0f, -90f, 0f));

            GameObject vessel = (GameObject)PrefabUtility.InstantiatePrefab(vesselPrefab);
            vessel.name = "TrainingVessel";
            vessel.transform.position = new Vector3(0f, 1.7f, -120f);

            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientLight = new Color(0.55f, 0.6f, 0.65f);

            GameObject cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.farClipPlane = 2000f;
            cameraGo.AddComponent<AudioListener>();
            ShipFollowCamera follow = cameraGo.AddComponent<ShipFollowCamera>();
            cameraGo.transform.position = new Vector3(0f, 30f, -185f);
            SerializedObject followSo = new SerializedObject(follow);
            followSo.FindProperty("target").objectReferenceValue = vessel.GetComponent<ShipPhysicsController>();
            followSo.ApplyModifiedPropertiesWithoutUndo();

            CreateTelemetry(vessel.GetComponent<ShipPhysicsController>());
            ShipSimulatorVisualUpgrade.Apply(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
        }

        internal static void PlaceFairwayBuoys(Transform parent, GameObject redBuoyPrefab,
            GameObject leftBuoyPrefab)
        {
            float[] stations = { -55f, 45f, 145f, 240f, 330f, 415f, 495f, 570f, 640f, 705f };
            for (int i = 0; i < stations.Length; i++)
            {
                float z = stations[i];
                float centerX = FairwayCenterX(z);
                float nextCenterX = FairwayCenterX(z + 4f);
                Vector2 tangent = new Vector2(nextCenterX - centerX, 4f).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                float halfWidth = Mathf.Lerp(53f, 45f,
                    Mathf.Clamp01((z - 380f) / 300f));
                Vector3 leftPosition = new Vector3(
                    centerX + normal.x * halfWidth, 0f, z + normal.y * halfWidth);
                Vector3 rightPosition = new Vector3(
                    centerX - normal.x * halfWidth, 0f, z - normal.y * halfWidth);

                GameObject left = PlacePrefab(
                    leftBuoyPrefab, parent, leftPosition, Quaternion.identity);
                GameObject right = PlacePrefab(
                    redBuoyPrefab, parent, rightPosition, Quaternion.identity);
                left.name = $"Left White Buoy {i + 1:00}";
                right.name = $"Right Red Buoy {i + 1:00}";
            }
        }

        internal static float FairwayCenterX(float z)
        {
            return FairwayModel.CenterX(z);
        }

        private static void CreateTelemetry(ShipPhysicsController ship)
        {
            GameObject canvasGo = new GameObject("TrainingUI");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject panel = new GameObject("TelemetryPanel");
            panel.transform.SetParent(canvasGo.transform, false);
            Image image = panel.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.65f);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(15f, -15f);
            panelRect.sizeDelta = new Vector2(700f, 215f);

            GameObject textGo = new GameObject("Readout");
            textGo.transform.SetParent(panel.transform, false);
            Text text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);

            ShipTelemetryUI telemetry = canvasGo.AddComponent<ShipTelemetryUI>();
            SerializedObject telemetrySo = new SerializedObject(telemetry);
            telemetrySo.FindProperty("ship").objectReferenceValue = ship;
            telemetrySo.FindProperty("readout").objectReferenceValue = text;
            telemetrySo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject PlacePrefab(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation)
        {
            GameObject instance = PrefabUtility.IsPartOfPrefabAsset(prefab)
                ? (GameObject)PrefabUtility.InstantiatePrefab(prefab)
                : Object.Instantiate(prefab);
            instance.transform.SetParent(parent);
            instance.transform.position = position;
            instance.transform.rotation = rotation;
            return instance;
        }

        private static void AddSceneToBuildSettings()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            var updated = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            foreach (EditorBuildSettingsScene item in existing)
                if (item.path != ScenePath) updated.Add(item);
            EditorBuildSettings.scenes = updated.ToArray();
        }
    }
}
