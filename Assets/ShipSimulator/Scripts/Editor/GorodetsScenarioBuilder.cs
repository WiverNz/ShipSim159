using System.Collections.Generic;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ShipSimulator.Editor
{
    public static class GorodetsScenarioBuilder
    {
        private const string Root = "Assets/ShipSimulator";
        private const string ScenePath = Root + "/Scenes/GorodetsTrainingScene.unity";
        private const string VesselPrefabPath =
            Root + "/Prefabs/Vessels/VolgoDon507B.prefab";

        [MenuItem("Ship Simulator/Build Gorodets Scenario")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material water = LoadOrCreateMaterial(
                "GorodetsWater", new Color(0.07f, 0.28f, 0.3f));
            Material bank = LoadOrCreateMaterial(
                "GorodetsBank", new Color(0.23f, 0.34f, 0.13f));
            Material rock = LoadOrCreateMaterial(
                "GorodetsRock", new Color(0.22f, 0.2f, 0.17f));
            Material red = LoadOrCreateMaterial(
                "NavigationRed", new Color(0.75f, 0.06f, 0.04f));
            Material white = LoadOrCreateMaterial(
                "NavigationWhite", new Color(0.9f, 0.9f, 0.82f));
            Material black = LoadOrCreateMaterial(
                "MarkerBlack", new Color(0.03f, 0.03f, 0.03f));

            GameObject routeObject = new GameObject("Gorodets Fairway Route");
            FairwayRoute route = routeObject.AddComponent<FairwayRoute>();
            route.Configure(CreateRouteSamples());

            BuildEnvironment(route, water, bank, rock);
            LeadingMarkPair[] leadingLines = BuildNavigation(
                route, red, white, black);

            GameObject physicsRoot = new GameObject("Scenario Physics");
            CurrentFieldProvider currentField =
                physicsRoot.AddComponent<CurrentFieldProvider>();
            currentField.Configure(new Vector3(0f, 0f, 0.22f), CreateCurrentRegions());
            ScenarioBathymetry bathymetry =
                physicsRoot.AddComponent<ScenarioBathymetry>();
            bathymetry.Configure(route, CreateHazards());

            ShipPhysicsController ship = CreateVessel();
            GroundingController grounding =
                ship.gameObject.AddComponent<GroundingController>();
            grounding.Configure(ship, bathymetry);

            GameObject missionObject = new GameObject("Gorodets Mission");
            GorodetsScenarioController mission =
                missionObject.AddComponent<GorodetsScenarioController>();
            mission.Configure(ship, route, bathymetry, grounding, leadingLines);

            CreateLighting();
            CreateCamera(ship);
            CreateTelemetry(ship);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Gorodets scenario built: " + ScenePath);
        }

        private static FairwayRouteSample[] CreateRouteSamples()
        {
            return new[]
            {
                Sample(0f, -180f, 24f, 24f, 5.4f, 3.8f, 3.7f, 2.8f),
                Sample(0f, 80f, 26f, 28f, 5.2f, 3.5f, 3.4f, 2.8f),
                Sample(18f, 260f, 34f, 38f, 4.8f, 3.0f, 2.7f, 3.0f),
                Sample(42f, 470f, 39f, 42f, 4.6f, 2.5f, 2.2f, 3.0f),
                Sample(68f, 700f, 40f, 38f, 4.5f, 2.3f, 2.0f, 3.1f),
                Sample(50f, 930f, 44f, 43f, 4.8f, 2.6f, 2.5f, 3.2f),
                Sample(6f, 1160f, 46f, 42f, 4.7f, 2.4f, 2.2f, 3.2f),
                Sample(-38f, 1390f, 41f, 45f, 4.6f, 2.1f, 2.4f, 3.0f),
                Sample(-55f, 1620f, 36f, 38f, 4.4f, 1.9f, 2.0f, 2.8f),
                Sample(-30f, 1860f, 39f, 42f, 4.7f, 2.2f, 2.4f, 3.0f),
                Sample(5f, 2070f, 52f, 55f, 5.3f, 3.0f, 3.2f, 3.3f)
            };
        }

        private static FairwayRouteSample Sample(float x, float z,
            float leftWidth, float rightWidth, float centerDepth,
            float leftDepth, float rightDepth, float speedLimit)
        {
            return new FairwayRouteSample
            {
                position = new Vector3(x, 0f, z),
                leftWidthM = leftWidth,
                rightWidthM = rightWidth,
                centerDepthM = centerDepth,
                leftEdgeDepthM = leftDepth,
                rightEdgeDepthM = rightDepth,
                speedLimitMps = speedLimit
            };
        }

        private static void BuildEnvironment(FairwayRoute route, Material water,
            Material bank, Material rock)
        {
            Transform root = new GameObject("Environment").transform;
            GameObject surface = Primitive("River Water", PrimitiveType.Cube, root,
                new Vector3(0f, -0.8f, 950f), new Vector3(430f, 1.5f, 2500f), water);
            Object.DestroyImmediate(surface.GetComponent<Collider>());

            for (float distance = 0f; distance <= route.LengthM; distance += 120f)
            {
                FairwayQuery query = route.QueryDistance(distance);
                Vector3 left = query.Position - query.Right * 115f;
                Vector3 right = query.Position + query.Right * 115f;
                Primitive($"Left Bank {distance:0000}", PrimitiveType.Cube, root,
                    left + Vector3.up * 2f, new Vector3(120f, 6f, 135f), bank);
                Primitive($"Right Bank {distance:0000}", PrimitiveType.Cube, root,
                    right + Vector3.up * 2f, new Vector3(120f, 6f, 135f), bank);
            }

            Transform hazards = new GameObject("Visible Rocky Shoals").transform;
            hazards.SetParent(root, false);
            foreach (BathymetryHazard hazard in CreateHazards())
            {
                if (hazard.bottomType != RiverBottomType.Rock) continue;
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * 137.5f * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    offset = Vector3.Scale(offset,
                        new Vector3(hazard.sizeM.x, 0f, hazard.sizeM.y) * 0.32f);
                    Primitive($"Rock {i + 1}", PrimitiveType.Sphere, hazards,
                        hazard.center + offset + Vector3.down * 2.2f,
                        new Vector3(4f, 2f, 5f), rock);
                }
            }
        }

        private static LeadingMarkPair[] BuildNavigation(FairwayRoute route,
            Material red, Material white, Material black)
        {
            Transform root = new GameObject("Navigation").transform;
            for (float distance = 80f; distance < route.LengthM - 80f; distance += 105f)
            {
                FairwayQuery query = route.QueryDistance(distance);
                PlaceBuoy(root, query.Position - query.Right * query.Sample.leftWidthM,
                    white, black, $"Left White Buoy {distance:0000}");
                PlaceBuoy(root, query.Position + query.Right * query.Sample.rightWidthM,
                    red, white, $"Right Red Buoy {distance:0000}");
            }

            return new[]
            {
                CreateLeadingPair(root, route, 300f, 760f, white, black, "Gorodets"),
                CreateLeadingPair(root, route, 830f, 1300f, white, black, "Upper Kochergino"),
                CreateLeadingPair(root, route, 1380f, 1800f, white, black, "Lower Kochergino")
            };
        }

        private static LeadingMarkPair CreateLeadingPair(Transform parent,
            FairwayRoute route, float startDistance, float endDistance,
            Material white, Material black, string name)
        {
            FairwayQuery line = route.QueryDistance(endDistance);
            Vector3 shoreOffset = line.Right * (line.Sample.rightWidthM + 70f);
            Transform front = CreateLeadingMark($"{name} Front Mark", parent,
                line.Position + shoreOffset, 11f, white, black);
            Transform rear = CreateLeadingMark($"{name} Rear Mark", parent,
                line.Position + line.Tangent * 75f + shoreOffset, 18f, white, black);
            GameObject pairObject = new GameObject($"{name} Leading Line");
            pairObject.transform.SetParent(parent, false);
            LeadingMarkPair pair = pairObject.AddComponent<LeadingMarkPair>();
            pair.Configure(front, rear, startDistance, endDistance);
            return pair;
        }

        private static Transform CreateLeadingMark(string name, Transform parent,
            Vector3 position, float height, Material white, Material black)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            Primitive("Post", PrimitiveType.Cylinder, root, Vector3.up * height * 0.5f,
                new Vector3(0.5f, height * 0.5f, 0.5f), black);
            Primitive("Board", PrimitiveType.Cube, root, Vector3.up * height,
                new Vector3(5f, 4f, 0.4f), white);
            Primitive("Stripe", PrimitiveType.Cube, root,
                Vector3.up * height + Vector3.back * 0.25f,
                new Vector3(1f, 4f, 0.15f), black);
            return root;
        }

        private static void PlaceBuoy(Transform parent, Vector3 position,
            Material body, Material top, string name)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            Primitive("Float", PrimitiveType.Cylinder, root, Vector3.up * 0.7f,
                new Vector3(1.3f, 0.9f, 1.3f), body);
            Primitive("Top Mark", PrimitiveType.Sphere, root, Vector3.up * 2.1f,
                Vector3.one * 0.7f, top);
        }

        private static CurrentRegionData[] CreateCurrentRegions()
        {
            return new[]
            {
                Region(12f, 180f, 100f, 250f, 0.45f, 0.45f),
                Region(32f, 390f, 140f, 260f, 0.22f, 0.82f),
                Region(55f, 650f, 150f, 360f, 0.08f, 0.95f),
                Region(34f, 1050f, 170f, 300f, -0.48f, 0.85f),
                Region(-24f, 1320f, 170f, 300f, 0.5f, 0.82f),
                Region(-45f, 1640f, 140f, 330f, -0.22f, 0.78f)
            };
        }

        private static CurrentRegionData Region(float x, float z, float width,
            float length, float lateralMps, float downstreamMps)
        {
            return new CurrentRegionData
            {
                center = new Vector3(x, 0f, z),
                size = new Vector3(width, 20f, length),
                velocityMps = new Vector3(lateralMps, 0f, downstreamMps),
                blendDistanceM = 28f,
                compositionMode = CurrentCompositionMode.Override,
                priority = 1
            };
        }

        private static BathymetryHazard[] CreateHazards()
        {
            return new[]
            {
                Hazard(-6f, 560f, 44f, 150f, 1.4f, RiverBottomType.Rock),
                Hazard(93f, 720f, 55f, 190f, 1.9f, RiverBottomType.Rock),
                Hazard(-83f, 1480f, 65f, 180f, 1.7f, RiverBottomType.Rock),
                Hazard(-8f, 1650f, 46f, 190f, 1.2f, RiverBottomType.Rock),
                Hazard(58f, 1180f, 60f, 220f, 0.9f, RiverBottomType.Sand)
            };
        }

        private static BathymetryHazard Hazard(float x, float z, float width,
            float length, float reduction, RiverBottomType bottom)
        {
            return new BathymetryHazard
            {
                center = new Vector3(x, 0f, z),
                sizeM = new Vector2(width, length),
                depthReductionM = reduction,
                bottomType = bottom
            };
        }

        private static ShipPhysicsController CreateVessel()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VesselPrefabPath);
            GameObject vessel = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            vessel.name = "TrainingVessel";
            vessel.transform.position = new Vector3(0f, 1.7f, -145f);
            return vessel.GetComponent<ShipPhysicsController>();
        }

        private static void CreateCamera(ShipPhysicsController ship)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 2600f;
            cameraObject.AddComponent<AudioListener>();
            ShipFollowCamera follow = cameraObject.AddComponent<ShipFollowCamera>();
            SerializedObject serialized = new SerializedObject(follow);
            serialized.FindProperty("target").objectReferenceValue = ship;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateTelemetry(ShipPhysicsController ship)
        {
            GameObject canvasObject = new GameObject("TrainingUI");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();
            ShipTelemetryUI telemetry = canvasObject.AddComponent<ShipTelemetryUI>();
            SerializedObject serialized = new SerializedObject(telemetry);
            serialized.FindProperty("ship").objectReferenceValue = ship;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateLighting()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            lightObject.transform.rotation = Quaternion.Euler(43f, -28f, 0f);
            RenderSettings.ambientLight = new Color(0.52f, 0.57f, 0.61f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.58f, 0.65f, 0.67f);
            RenderSettings.fogDensity = 0.0012f;
        }

        private static Material LoadOrCreateMaterial(string name, Color color)
        {
            string path = $"{Root}/Materials/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(
                    Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                material.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(material, path);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type,
            Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localScale = scale;
            if (material != null)
                gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return gameObject;
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int existing = scenes.FindIndex(scene => scene.path == ScenePath);
            if (existing >= 0) scenes.RemoveAt(existing);
            int primaryIndex = scenes.FindIndex(scene =>
                scene.path == "Assets/ShipSimulator/Scenes/RiverTrainingScene.unity");
            int insertIndex = primaryIndex >= 0 ? primaryIndex + 1 : 0;
            scenes.Insert(insertIndex, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
