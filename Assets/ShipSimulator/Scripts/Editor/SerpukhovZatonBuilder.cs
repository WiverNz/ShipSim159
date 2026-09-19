using System.Collections.Generic;
using ShipSimulator.CameraSystem;
using ShipSimulator.Physics;
using ShipSimulator.UI;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // The Nara from its mouth on the Oka up into the Serpukhov lay-up basin. The shoreline comes from
    // the scenario geometry file; the depths, currents and shoals below are scenario design, not survey,
    // and are recorded as estimates in SerpukhovZatonScenario_Sources.md.
    public static class SerpukhovZatonBuilder
    {
        private const string Root = "Assets/ShipSimulator";
        private const string ScenePath = Root + "/Scenes/SerpukhovZatonScene.unity";
        private const string GeometryPath = Root + "/Data/Scenarios/SerpukhovZaton.json";
        // The basin admits only the fast craft, and the scene is built with the smaller of them.
        private const string VesselPrefabPath = Root + "/Prefabs/Vessels/Luch14352.prefab";

        private const float CoreStepM = 6f;
        private static readonly Vector2 CoreX = new Vector2(-260f, 620f);
        private static readonly Vector2 CoreZ = new Vector2(-350f, 1600f);
        private const float SkirtReachM = 1400f;

        [MenuItem("Ship Simulator/Build Serpukhov Zaton Scenario")]
        public static void Build()
        {
            ScenarioGeometry geometry = ScenarioGeometry.Load(GeometryPath);
            Material water = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/RiverWater.mat");
            if (water == null)
                throw new System.InvalidOperationException(
                    "RiverWater material must exist before building the Serpukhov zaton.");
            RiverLandscapeBuilder.EnsureFolder();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var routeObject = new GameObject("Serpukhov Fairway Route");
            FairwayRoute route = routeObject.AddComponent<FairwayRoute>();
            route.Configure(RouteSamples(geometry));

            float[] xs = Axis(CoreX.x, CoreX.y);
            float[] zs = Axis(CoreZ.x, CoreZ.y);
            Transform environment = new GameObject("Environment").transform;
            BuildWater(environment, xs, zs, water);
            BuildGround(environment, geometry, xs, zs);
            BuildStructures(environment, geometry);
            BuildVegetation(environment, geometry);
            LeadingMarkPair[] leadingLines = BuildNavigation(route, geometry);

            var physicsRoot = new GameObject("Scenario Physics");
            CurrentFieldProvider currentField = physicsRoot.AddComponent<CurrentFieldProvider>();
            currentField.Configure(new Vector3(0f, 0f, geometry.ambientCurrentZMps), CurrentRegions(geometry));
            ScenarioBathymetry bathymetry = physicsRoot.AddComponent<ScenarioBathymetry>();
            // Outside a 20 m channel the Nara is shoal, so straying out of the marked water grounds you.
            bathymetry.Configure(route, Hazards(geometry), 0.35f);

            new GameObject("Weather System").AddComponent<WeatherController>();

            ShipPhysicsController ship = CreateVessel(geometry);
            GroundingController grounding = ship.gameObject.AddComponent<GroundingController>();
            grounding.Configure(ship, bathymetry);

            var missionObject = new GameObject("Serpukhov Mission");
            GorodetsScenarioController mission = missionObject.AddComponent<GorodetsScenarioController>();
            mission.Configure(ship, route, bathymetry, grounding, leadingLines);
            mission.ConfigurePhases(
                new[] { 340f, 520f, 1050f, 1780f, 2010f, 2330f },
                new[]
                {
                    "Briefing", "Oka Approach", "Mouth Entry", "Nara Reach",
                    "Upper Bend", "Basin Entrance", "Berthing", "Completed", "Failed"
                },
                new[]
                {
                    "Prepare vessel",
                    "Stem the Oka current and hold the 30 m channel up to the Nara mouth",
                    "Turn to starboard into the Nara. The bar lies off the mouth",
                    "20 m of marked water and unlit buoys: hold the axis",
                    "Follow the bends and allow for bank effect in the narrow reach",
                    "Slow down. The basin entrance is about 55 m wide",
                    "Dead slow past the laid-up craft. Stop short of the head of the basin",
                    "Passage complete.",
                    "Passage failed."
                });

            CreateLighting();
            CreateCamera(ship);
            CreateTelemetry(ship);
            ShipSimulatorVisualUpgrade.ConfigureLighting(scene);
            ShipSimulatorVisualUpgrade.ConfigurePostProcessing(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Serpukhov zaton scenario built: " + ScenePath +
                " (" + route.LengthM.ToString("0") + " m fairway)");
        }

        private static FairwayRouteSample[] RouteSamples(ScenarioGeometry geometry)
        {
            var samples = new FairwayRouteSample[geometry.route.Length];
            for (int i = 0; i < samples.Length; i++)
            {
                ScenarioGeometry.RouteSample source = geometry.route[i];
                samples[i] = new FairwayRouteSample
                {
                    position = new Vector3(source.x, 0f, source.z),
                    leftWidthM = source.leftWidthM,
                    rightWidthM = source.rightWidthM,
                    centerDepthM = source.centerDepthM,
                    leftEdgeDepthM = source.leftEdgeDepthM,
                    rightEdgeDepthM = source.rightEdgeDepthM,
                    speedLimitMps = source.speedLimitMps
                };
            }
            return samples;
        }

        private static CurrentRegionData[] CurrentRegions(ScenarioGeometry geometry)
        {
            var regions = new CurrentRegionData[geometry.currentRegions.Length];
            for (int i = 0; i < regions.Length; i++)
            {
                ScenarioGeometry.CurrentRegion source = geometry.currentRegions[i];
                regions[i] = new CurrentRegionData
                {
                    center = new Vector3(source.x, 0f, source.z),
                    size = new Vector3(source.widthM, 20f, source.lengthM),
                    velocityMps = new Vector3(source.velocityXMps, 0f, source.velocityZMps),
                    blendDistanceM = source.blendM,
                    compositionMode = source.overrideAmbient
                        ? CurrentCompositionMode.Override
                        : CurrentCompositionMode.Additive,
                    priority = source.priority
                };
            }
            return regions;
        }

        private static BathymetryHazard[] Hazards(ScenarioGeometry geometry)
        {
            var hazards = new BathymetryHazard[geometry.hazards.Length];
            for (int i = 0; i < hazards.Length; i++)
            {
                ScenarioGeometry.Hazard source = geometry.hazards[i];
                hazards[i] = new BathymetryHazard
                {
                    center = new Vector3(source.x, 0f, source.z),
                    sizeM = new Vector2(source.widthM, source.lengthM),
                    depthReductionM = source.depthReductionM,
                    bottomType = source.bottom == "Rock" ? RiverBottomType.Rock
                        : source.bottom == "Sand" ? RiverBottomType.Sand : RiverBottomType.Silt
                };
            }
            return hazards;
        }

        // A uniform grid over the whole visible area would be either coarse at the waterline or huge,
        // so the rows stay 6 m apart over the water and stretch away towards the horizon.
        private static float[] Axis(float coreMin, float coreMax)
        {
            var values = new List<float>();
            float step = CoreStepM;
            for (float v = coreMin - step; v > coreMin - SkirtReachM; )
            {
                values.Add(v);
                step *= 1.45f;
                v -= step;
            }
            values.Reverse();
            for (float v = coreMin; v <= coreMax + 0.01f; v += CoreStepM) values.Add(v);
            step = CoreStepM;
            for (float v = coreMax; v < coreMax + SkirtReachM; )
            {
                step *= 1.45f;
                v += step;
                values.Add(v);
            }
            return values.ToArray();
        }

        private static void BuildWater(Transform parent, float[] xs, float[] zs, Material material)
        {
            var mesh = new RiverLandscapeBuilder.MeshData();
            foreach (float z in zs)
            foreach (float x in xs)
                mesh.Vertex(new Vector3(x, 0f, z), new Vector2(x, z), Color.white);
            Grid(mesh, xs.Length, zs.Length);
            GameObject surface = RiverLandscapeBuilder.MeshObject("RiverWater", parent,
                RiverLandscapeBuilder.SaveMesh("SerpukhovWater", mesh.Build("SerpukhovWater")), material);
            surface.layer = 4;
            surface.AddComponent<RiverPlanarReflection>();
        }

        private static void BuildGround(Transform parent, ScenarioGeometry geometry, float[] xs, float[] zs)
        {
            var mesh = new RiverLandscapeBuilder.MeshData();
            foreach (float z in zs)
            foreach (float x in xs)
            {
                float shore = geometry.SignedShoreDistance(x, z);
                mesh.Vertex(new Vector3(x, GroundHeight(shore, x, z), z), new Vector2(x, z),
                    new Color(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 20f, shore)), 0f, 0f, 1f));
            }
            Grid(mesh, xs.Length, zs.Length);
            RiverLandscapeBuilder.MeshObject("Basin ground", parent,
                RiverLandscapeBuilder.SaveMesh("SerpukhovGround", mesh.Build("SerpukhovGround")),
                RiverLandscapeBuilder.MaterialAsset("AlluvialGround", "ShipSimulator/RiverGround", Color.white));
        }

        // Visual bed and bank only. The depth the vessel actually feels comes from ScenarioBathymetry.
        private static float GroundHeight(float shoreDistanceM, float x, float z)
        {
            if (shoreDistanceM <= 0f)
                return -(0.2f + 3.3f * Mathf.Clamp01(-shoreDistanceM / 40f));
            float beach = 1.1f * Mathf.Clamp01(shoreDistanceM / 9f);
            float terrace = 3.4f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(9f, 70f, shoreDistanceM));
            float hills = Mathf.PerlinNoise((x + 311f) * 0.0072f, (z + 907f) * 0.0065f) * 11f *
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((shoreDistanceM - 30f) / 110f));
            float grain = (Mathf.PerlinNoise(x * 0.12f, z * 0.1f) - 0.5f) * 0.4f;
            return beach + terrace + hills + grain;
        }

        // Columns run along x inside each row and rows along z, so this winding faces up.
        private static void Grid(RiverLandscapeBuilder.MeshData mesh, int columns, int rows)
        {
            for (int row = 0; row < rows - 1; row++)
            for (int c = 0; c < columns - 1; c++)
            {
                int a = row * columns + c;
                mesh.Quad(a, a + columns, a + columns + 1, a + 1);
            }
        }

        private static void BuildStructures(Transform parent, ScenarioGeometry geometry)
        {
            Transform root = new GameObject("Port structures").transform;
            root.SetParent(parent, false);
            Material concrete = LitMaterial("SerpukhovConcrete", new Color(0.52f, 0.51f, 0.48f));
            Material steel = LitMaterial("SerpukhovLaidUpSteel", new Color(0.30f, 0.33f, 0.34f));
            Material masonry = LitMaterial("SerpukhovMasonry", new Color(0.82f, 0.79f, 0.72f));
            Material roof = LitMaterial("SerpukhovRoof", new Color(0.24f, 0.26f, 0.29f));

            foreach (ScenarioGeometry.Landmark landmark in geometry.landmarks)
            {
                Vector3 afloat = landmark.Center;
                // Shore buildings stand on the bank; quays and laid-up craft belong to the waterline.
                Vector3 grounded = landmark.Center + Vector3.up *
                    GroundHeight(geometry.SignedShoreDistance(landmark.x, landmark.z), landmark.x, landmark.z);
                switch (landmark.kind)
                {
                    case "quay":
                        Box(root, landmark.name, afloat + Vector3.up * landmark.heightM * 0.5f,
                            new Vector3(landmark.widthM, landmark.heightM, landmark.lengthM),
                            landmark.Rotation, concrete);
                        for (int i = 0; i < 4; i++)
                            Box(root, landmark.name + " bollard " + (i + 1),
                                afloat + landmark.Rotation * new Vector3(
                                    0f, landmark.heightM + 0.4f, (i - 1.5f) * landmark.lengthM * 0.28f),
                                new Vector3(0.5f, 0.8f, 0.5f), landmark.Rotation, steel);
                        break;
                    case "mooring":
                        Box(root, landmark.name, afloat + Vector3.up * (landmark.heightM * 0.5f - 1.1f),
                            new Vector3(landmark.widthM, landmark.heightM, landmark.lengthM),
                            landmark.Rotation, steel);
                        Box(root, landmark.name + " house",
                            afloat + landmark.Rotation * new Vector3(
                                0f, landmark.heightM - 0.2f, -landmark.lengthM * 0.34f),
                            new Vector3(landmark.widthM * 0.7f, 2.6f, landmark.lengthM * 0.2f),
                            landmark.Rotation, masonry);
                        break;
                    default:
                        Box(root, landmark.name, grounded + Vector3.up * (landmark.heightM * 0.5f - 1f),
                            new Vector3(landmark.widthM, landmark.heightM, landmark.lengthM),
                            landmark.Rotation, masonry);
                        Box(root, landmark.name + " roof",
                            grounded + Vector3.up * (landmark.heightM + 0.4f),
                            new Vector3(landmark.widthM * 1.08f, 2.8f, landmark.lengthM * 1.08f),
                            landmark.Rotation, roof);
                        break;
                }
            }
        }

        private static void BuildVegetation(Transform parent, ScenarioGeometry geometry)
        {
            Transform root = new GameObject("Bank vegetation").transform;
            root.SetParent(parent, false);
            Material bark = RiverLandscapeBuilder.MaterialAsset("WillowBark", "ShipSimulator/RiverBark",
                new Color(0.22f, 0.18f, 0.13f));
            Material birch = RiverLandscapeBuilder.MaterialAsset("BirchBark", "ShipSimulator/RiverBark",
                new Color(0.64f, 0.62f, 0.53f));
            Material foliage = RiverLandscapeBuilder.MaterialAsset("RiverLeaves", "ShipSimulator/RiverFoliage",
                new Color(0.32f, 0.43f, 0.16f));
            Material silver = RiverLandscapeBuilder.MaterialAsset("WillowLeaves", "ShipSimulator/RiverFoliage",
                new Color(0.40f, 0.48f, 0.24f));
            Material reeds = RiverLandscapeBuilder.MaterialAsset("ReedLeaves", "ShipSimulator/RiverFoliage",
                new Color(0.43f, 0.43f, 0.20f));
            var trees = new GameObject[6];
            for (int i = 0; i < trees.Length; i++)
                trees[i] = Plant("Tree" + i, 507 + i * 71, false, false,
                    i % 3 == 1 ? birch : bark, i % 3 == 2 ? silver : foliage);
            var bushes = new GameObject[3];
            for (int i = 0; i < bushes.Length; i++)
                bushes[i] = Plant("Bush" + i, 159 + i * 19, true, false, bark, foliage);
            GameObject reed = Plant("ReedClump", 902, true, true, bark, reeds);

            var random = new System.Random(1577);
            for (float z = CoreZ.x; z < CoreZ.y; z += 15f)
            for (float x = CoreX.x; x < CoreX.y; x += 15f)
            {
                float px = x + RiverLandscapeBuilder.Range(random, -7f, 7f);
                float pz = z + RiverLandscapeBuilder.Range(random, -7f, 7f);
                float shore = geometry.SignedShoreDistance(px, pz);
                if (shore <= 0.5f) continue;
                var position = new Vector3(px, GroundHeight(shore, px, pz), pz);
                if (shore < 4f)
                {
                    if (random.NextDouble() < 0.55)
                        RiverLandscapeBuilder.Place(reed, root, position,
                            RiverLandscapeBuilder.Range(random, 0.75f, 1.5f),
                            RiverLandscapeBuilder.Range(random, 0f, 360f));
                    continue;
                }
                // Port ground and the quays are kept clear; the willows belong to the open banks.
                if (NearStructure(geometry, px, pz)) continue;
                double density = shore < 26f ? 0.22 : shore < 130f ? 0.16 : 0.07;
                if (random.NextDouble() > density) continue;
                if (random.NextDouble() < 0.28)
                    RiverLandscapeBuilder.Place(bushes[random.Next(bushes.Length)], root, position,
                        RiverLandscapeBuilder.Range(random, 0.7f, 1.6f),
                        RiverLandscapeBuilder.Range(random, 0f, 360f));
                else
                    RiverLandscapeBuilder.Place(trees[random.Next(trees.Length)], root, position,
                        RiverLandscapeBuilder.Range(random, 0.7f, 1.4f),
                        RiverLandscapeBuilder.Range(random, 0f, 360f));
            }
        }

        private static bool NearStructure(ScenarioGeometry geometry, float x, float z)
        {
            foreach (ScenarioGeometry.Landmark landmark in geometry.landmarks)
            {
                if (landmark.kind == "mooring") continue;
                float clearance = Mathf.Max(landmark.lengthM, landmark.widthM) * 0.75f + 14f;
                float dx = x - landmark.x;
                float dz = z - landmark.z;
                if (dx * dx + dz * dz < clearance * clearance) return true;
            }
            return false;
        }

        // Russian inland buoyage is read going downstream: the red right-edge buoys stand on the bank
        // that is on your right when the current is behind you. This route runs up-river, so they sit
        // on its left. The reach is third category, so nothing here is lit at night.
        private static LeadingMarkPair[] BuildNavigation(FairwayRoute route, ScenarioGeometry geometry)
        {
            Transform root = new GameObject("Navigation").transform;
            Material red = NavigationMaterial("NavigationRed", new Color(0.75f, 0.06f, 0.04f));
            Material white = NavigationMaterial("NavigationWhite", new Color(0.9f, 0.9f, 0.82f));
            Material black = NavigationMaterial("MarkerBlack", new Color(0.03f, 0.03f, 0.03f));

            for (float distance = 560f; distance < 1900f; distance += 135f)
            {
                FairwayQuery query = route.QueryDistance(distance);
                PlaceBuoy(root, query.Position - query.Right * query.Sample.leftWidthM, red, white,
                    $"Unlit Right Red Buoy {distance:0000}");
                PlaceBuoy(root, query.Position + query.Right * query.Sample.rightWidthM, white, black,
                    $"Unlit Left White Buoy {distance:0000}");
            }
            // Axial marks: the system is used for the start point and the axis of a fairway, which is
            // exactly what the entry from the Oka and the basin entrance are.
            foreach (float distance in new[] { 430f, 1905f })
            {
                FairwayQuery query = route.QueryDistance(distance);
                PlaceBuoy(root, query.Position, white, red, $"Unlit Axial Buoy {distance:0000}");
            }

            // The entry leading line stands where a real one does: on the bank beyond the leg, in line
            // with it, so holding the marks in transit holds the course out of the Oka into the Nara.
            FairwayQuery entry = route.QueryDistance(560f);
            Vector3 onLine = entry.Position;
            while (geometry.SignedShoreDistance(onLine.x, onLine.z) < 45f && onLine.z < 900f)
                onLine += entry.Tangent * 10f;
            Transform front = CreateLeadingMark("Nara Entry Unlit Front Mark", root,
                Seat(geometry, onLine), 9f, white, black);
            Transform rear = CreateLeadingMark("Nara Entry Unlit Rear Mark", root,
                Seat(geometry, onLine + entry.Tangent * 200f), 15f, white, black);
            var pairObject = new GameObject("Nara Entry Leading Line");
            pairObject.transform.SetParent(root, false);
            LeadingMarkPair pair = pairObject.AddComponent<LeadingMarkPair>();
            pair.Configure(front, rear, 150f, 520f);
            return new[] { pair };
        }

        private static Vector3 Seat(ScenarioGeometry geometry, Vector3 position)
        {
            position.y = GroundHeight(geometry.SignedShoreDistance(position.x, position.z),
                position.x, position.z) - 0.3f;
            return position;
        }

        private static Transform CreateLeadingMark(string name, Transform parent, Vector3 position,
            float height, Material white, Material black)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            Primitive("Post", PrimitiveType.Cylinder, root, Vector3.up * height * 0.5f,
                new Vector3(0.4f, height * 0.5f, 0.4f), black);
            Primitive("Board", PrimitiveType.Cube, root, Vector3.up * height,
                new Vector3(3.6f, 3f, 0.3f), white);
            Primitive("Stripe", PrimitiveType.Cube, root,
                Vector3.up * height + Vector3.back * 0.2f, new Vector3(0.8f, 3f, 0.12f), black);
            return root;
        }

        private static void PlaceBuoy(Transform parent, Vector3 position, Material body, Material top,
            string name)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = position;
            Primitive("Float", PrimitiveType.Cylinder, root, Vector3.up * 0.6f,
                new Vector3(1.1f, 0.8f, 1.1f), body);
            Primitive("Top Mark", PrimitiveType.Sphere, root, Vector3.up * 1.9f,
                Vector3.one * 0.6f, top);
        }

        private static ShipPhysicsController CreateVessel(ScenarioGeometry geometry)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VesselPrefabPath);
            var vessel = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            vessel.name = "TrainingVessel";
            ScenarioGeometry.RouteSample start = geometry.route[0];
            ScenarioGeometry.RouteSample next = geometry.route[1];
            var heading = new Vector3(next.x - start.x, 0f, next.z - start.z);
            vessel.transform.position = new Vector3(start.x, 0.4f, start.z);
            vessel.transform.rotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
            return vessel.GetComponent<ShipPhysicsController>();
        }

        private static void CreateCamera(ShipPhysicsController ship)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 52f;
            camera.nearClipPlane = 0.25f;
            camera.farClipPlane = 2600f;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            UniversalAdditionalCameraData cameraData =
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraObject.AddComponent<AudioListener>();
            ShipFollowCamera follow = cameraObject.AddComponent<ShipFollowCamera>();
            var serialized = new SerializedObject(follow);
            serialized.FindProperty("target").objectReferenceValue = ship;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateTelemetry(ShipPhysicsController ship)
        {
            var canvasObject = new GameObject("TrainingUI");
            canvasObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>().uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObject.AddComponent<GraphicRaycaster>();
            ShipTelemetryUI telemetry = canvasObject.AddComponent<ShipTelemetryUI>();
            var serialized = new SerializedObject(telemetry);
            serialized.FindProperty("ship").objectReferenceValue = ship;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateLighting()
        {
            var lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.05f;
            lightObject.transform.rotation = Quaternion.Euler(41f, 24f, 0f);
            RenderSettings.ambientLight = new Color(0.52f, 0.57f, 0.61f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.58f, 0.65f, 0.67f);
            RenderSettings.fogDensity = 0.0012f;
        }

        private static GameObject Plant(string name, int seed, bool bush, bool reed,
            Material bark, Material leaves)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                Root + "/Settings/NaturalLandscape/" + name + ".prefab");
            return existing != null
                ? existing
                : RiverLandscapeBuilder.PlantPrefab(name, seed, bush, reed, bark, leaves);
        }

        private static Material LitMaterial(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material NavigationMaterial(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void Box(Transform parent, string name, Vector3 center, Vector3 size,
            Quaternion rotation, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.SetPositionAndRotation(center, rotation);
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(box.GetComponent<Collider>());
        }

        private static void Primitive(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            if (material != null) item.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int existing = scenes.FindIndex(scene => scene.path == ScenePath);
            if (existing >= 0) scenes.RemoveAt(existing);
            // Gorodets stays first: it is the startup scene the tests and the editor tooling expect.
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
