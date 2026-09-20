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
            // Outside a 20 m channel the Nara shoals, so leaving the marked water puts you on the
            // bottom. 0.6 m lets the Luch touch and work herself off astern, while the deeper Meteor
            // grounds properly; less than that simply holds either of them there for good.
            bathymetry.Configure(route, Hazards(geometry), 0.6f);

            new GameObject("Weather System").AddComponent<WeatherController>();

            ShipPhysicsController ship = CreateVessel(geometry);
            GroundingController grounding = ship.gameObject.AddComponent<GroundingController>();
            grounding.Configure(ship, bathymetry);

            var missionObject = new GameObject("Serpukhov Mission");
            GorodetsScenarioController mission = missionObject.AddComponent<GorodetsScenarioController>();
            mission.Configure(ship, route, bathymetry, grounding, leadingLines);
            mission.ConfigurePhases(
                new[] { 70f, 330f, 470f, 620f, 1800f, 2320f },
                new[]
                {
                    "Briefing", "Leaving the Lay-up Berth", "Basin", "Port and Marina",
                    "Basin Entrance", "Nara Reach", "Oka Mouth", "Completed", "Failed"
                },
                new[]
                {
                    "Prepare vessel",
                    "Let go and come clear of the laid-up barges",
                    "Dead slow down the basin: moored craft both sides",
                    "Past the passenger berth and the yacht harbour, mind your wash",
                    "The gate is about 55 m wide: line her up early",
                    "20 m of marked water and unlit buoys: hold the axis and watch the bends",
                    "Turn to port out of the Nara and stem the Oka current",
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
                mesh.Vertex(new Vector3(x, GroundHeight(geometry, shore, x, z), z), new Vector2(x, z),
                    new Color(GrassAmount(geometry, shore, x, z), 0f, 0f, 1f));
            }
            Grid(mesh, xs.Length, zs.Length);
            RiverLandscapeBuilder.MeshObject("Basin ground", parent,
                RiverLandscapeBuilder.SaveMesh("SerpukhovGround", mesh.Build("SerpukhovGround")),
                RiverLandscapeBuilder.MaterialAsset("AlluvialGround", "ShipSimulator/RiverGround", Color.white));
        }

        // Real relief from the elevation grid, but a 30 m elevation source smears the waterline, so the
        // first 70 m of bank is carried by the shoreline instead and blended into the terrain behind it.
        // Visual only: the depth the vessel feels comes from ScenarioBathymetry.
        private static float GroundHeight(ScenarioGeometry geometry, float shoreDistanceM, float x, float z)
        {
            if (shoreDistanceM <= 0f)
                return -(0.2f + 3.3f * Mathf.Clamp01(-shoreDistanceM / 40f));
            float terrain = Mathf.Max(geometry.ElevationAbove(x, z), 0.4f);
            float beach = 0.35f + 2.3f * Mathf.Clamp01(shoreDistanceM / 26f);
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shoreDistanceM / 70f));
            float grain = (Mathf.PerlinNoise(x * 0.09f + 31f, z * 0.08f + 17f) - 0.5f) * 0.55f +
                (Mathf.PerlinNoise(x * 0.021f, z * 0.019f) - 0.5f) * 1.5f * blend;
            return Mathf.Lerp(beach, terrain, blend) + grain;
        }

        private static float GrassAmount(ScenarioGeometry geometry, float shoreDistanceM, float x, float z)
        {
            float bare = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(2f, 18f, shoreDistanceM));
            return geometry.CoverAt(x, z) switch
            {
                "sand" => 0f,
                "built" => Mathf.Min(bare, 0.42f),
                "wood" => Mathf.Max(bare, 0.92f),
                "farmland" => Mathf.Max(bare, 0.88f),
                "allotments" => Mathf.Max(bare, 0.72f),
                "orchard" => Mathf.Max(bare, 0.78f),
                "reeds" => Mathf.Max(bare, 0.65f),
                _ => bare
            };
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

            // Berths sit at the waterline; everything else ashore comes from the building footprints.
            foreach (ScenarioGeometry.Landmark landmark in geometry.landmarks)
            {
                Box(root, landmark.name, landmark.Center + Vector3.up * landmark.heightM * 0.5f,
                    new Vector3(landmark.widthM, landmark.heightM, landmark.lengthM),
                    landmark.Rotation, concrete);
                for (int i = 0; i < 4; i++)
                    Box(root, landmark.name + " bollard " + (i + 1),
                        landmark.Center + landmark.Rotation * new Vector3(
                            0f, landmark.heightM + 0.4f, (i - 1.5f) * landmark.lengthM * 0.28f),
                        new Vector3(0.5f, 0.8f, 0.5f), landmark.Rotation, steel);
            }
            BuildMoorings(root, geometry);
            BuildTown(parent, geometry);
        }

        // Laid-up craft, barges, the floating dock and the port's piers, traced from satellite imagery.
        // Approximate shapes: a hull box with a raised coaming or deckhouse, not a modelled vessel.
        private static void BuildMoorings(Transform parent, ScenarioGeometry geometry)
        {
            if (geometry.moorings == null) return;
            Transform root = new GameObject("Laid-up craft").transform;
            root.SetParent(parent, false);
            Material rust = LitMaterial("SerpukhovBargeHull", new Color(0.33f, 0.20f, 0.14f));
            Material steel = LitMaterial("SerpukhovLaidUpSteel", new Color(0.30f, 0.33f, 0.34f));
            Material paint = LitMaterial("SerpukhovVesselPaint", new Color(0.74f, 0.74f, 0.70f));
            Material deck = LitMaterial("SerpukhovDeck", new Color(0.42f, 0.40f, 0.36f));
            var random = new System.Random(342);

            foreach (ScenarioGeometry.Mooring craft in geometry.moorings)
            {
                float length = craft.lengthM;
                float beam = Mathf.Max(2.5f, craft.widthM);
                Quaternion rotation = craft.Rotation;
                Vector3 centre = craft.Center;
                switch (craft.kind)
                {
                    case "pier":
                        SolidBox(root, craft.name, centre + Vector3.up * 0.9f,
                            new Vector3(beam, 0.5f, length), rotation, deck);
                        int piles = Mathf.Max(3, Mathf.RoundToInt(length / 12f));
                        for (int i = 0; i < piles; i++)
                            SolidBox(root, craft.name + " pile " + (i + 1),
                                centre + rotation * new Vector3(0f, 0.1f, (i / (piles - 1f) - 0.5f) * length * 0.92f),
                                new Vector3(0.45f, 2.4f, 0.45f), rotation, steel);
                        break;
                    case "dock":
                        // A floating dock reads as an open box: two side walls on a submerged pontoon.
                        SolidBox(root, craft.name, centre + Vector3.up * 0.3f,
                            new Vector3(beam, 2.4f, length), rotation, rust);
                        for (int side = -1; side <= 1; side += 2)
                            SolidBox(root, craft.name + (side < 0 ? " port wall" : " starboard wall"),
                                centre + rotation * new Vector3(side * (beam * 0.5f - 1.2f), 3.4f, 0f),
                                new Vector3(2.4f, 5.6f, length * 0.92f), rotation, rust);
                        break;
                    case "craft":
                        SolidBox(root, craft.name, centre + Vector3.up * 0.25f,
                            new Vector3(beam, 1.5f, length), rotation, paint);
                        SolidBox(root, craft.name + " cabin",
                            centre + rotation * new Vector3(0f, 1.6f, -length * 0.1f),
                            new Vector3(beam * 0.62f, 1.6f, length * 0.34f), rotation, paint);
                        break;
                    case "vessel":
                        SolidBox(root, craft.name, centre + Vector3.up * 0.1f,
                            new Vector3(beam, 3.4f, length), rotation, RiverLandscapeBuilder.Range(random, 0f, 1f) < 0.45f ? rust : steel);
                        SolidBox(root, craft.name + " deckhouse",
                            centre + rotation * new Vector3(0f, 3.1f, -length * 0.22f),
                            new Vector3(beam * 0.72f, 3.0f, length * 0.3f), rotation, paint);
                        SolidBox(root, craft.name + " wheelhouse",
                            centre + rotation * new Vector3(0f, 5.6f, -length * 0.18f),
                            new Vector3(beam * 0.45f, 2.2f, length * 0.13f), rotation, paint);
                        break;
                    default:
                        // Barge: a long low hull with cargo coamings and a small house aft.
                        SolidBox(root, craft.name, centre + Vector3.down * 0.2f,
                            new Vector3(beam, 3.2f, length), rotation, rust);
                        SolidBox(root, craft.name + " coaming",
                            centre + rotation * new Vector3(0f, 1.9f, length * 0.06f),
                            new Vector3(beam * 0.82f, 1.4f, length * 0.68f), rotation, deck);
                        SolidBox(root, craft.name + " house",
                            centre + rotation * new Vector3(0f, 2.6f, -length * 0.41f),
                            new Vector3(beam * 0.55f, 2.6f, length * 0.12f), rotation, paint);
                        break;
                }
                root.Find(craft.name).gameObject.AddComponent<RadarObstacle>();
            }
        }

        // The town, the port yard and the monastery on the bank. Massing only: real footprints reduced
        // to their minimum-area box, with heights estimated from the storey count or the building type.
        private static void BuildTown(Transform parent, ScenarioGeometry geometry)
        {
            if (geometry.buildings == null) return;
            Transform root = new GameObject("Shore buildings").transform;
            root.SetParent(parent, false);
            // A row of identical white blocks reads as a test scene, so the walls vary within a muted
            // palette chosen from the position, which keeps a rebuild reproducible.
            Material[] walls =
            {
                LitMaterial("SerpukhovWallRender", new Color(0.44f, 0.42f, 0.38f)),
                LitMaterial("SerpukhovWallBrick", new Color(0.38f, 0.29f, 0.24f)),
                LitMaterial("SerpukhovWallPanel", new Color(0.36f, 0.37f, 0.36f)),
                LitMaterial("SerpukhovWallLime", new Color(0.49f, 0.46f, 0.39f))
            };
            Material[] roofs =
            {
                LitMaterial("SerpukhovRoofSlate", new Color(0.16f, 0.16f, 0.17f)),
                LitMaterial("SerpukhovRoofTin", new Color(0.21f, 0.25f, 0.27f)),
                LitMaterial("SerpukhovRoofRed", new Color(0.30f, 0.15f, 0.11f))
            };
            Material masonry = LitMaterial("SerpukhovMasonry", new Color(0.58f, 0.55f, 0.49f));
            Material metalRoof = LitMaterial("SerpukhovMetalRoof", new Color(0.26f, 0.29f, 0.31f));

            foreach (ScenarioGeometry.Building building in geometry.buildings)
            {
                float ground = GroundHeight(geometry,
                    geometry.SignedShoreDistance(building.x, building.z), building.x, building.z);
                var basePoint = new Vector3(building.x, ground - 0.8f, building.z);
                Quaternion rotation = Quaternion.Euler(0f, building.headingDeg, 0f);
                bool tall = building.heightM > 11f;
                int tint = Mathf.Abs(Mathf.RoundToInt(building.x * 7.3f + building.z * 3.1f));
                Material wall = building.roof == "church" ? masonry : walls[tint % walls.Length];
                Material roof = roofs[(tint / 3) % roofs.Length];
                Box(root, building.name, basePoint + Vector3.up * building.heightM * 0.5f,
                    new Vector3(building.widthM, building.heightM, building.lengthM), rotation, wall);
                if (building.roof == "church")
                {
                    Box(root, building.name + " drum",
                        basePoint + Vector3.up * (building.heightM + 2.6f),
                        new Vector3(building.widthM * 0.42f, 5.2f, building.lengthM * 0.34f), rotation, masonry);
                    Box(root, building.name + " cupola",
                        basePoint + Vector3.up * (building.heightM + 6.4f),
                        new Vector3(building.widthM * 0.3f, 2.6f, building.lengthM * 0.24f), rotation, metalRoof);
                }
                else if (building.roof == "pitched")
                {
                    Box(root, building.name + " roof",
                        basePoint + Vector3.up * (building.heightM + 0.7f),
                        new Vector3(building.widthM * 1.06f, 1.5f, building.lengthM * 1.06f), rotation, roof);
                }
                else
                {
                    Box(root, building.name + " parapet",
                        basePoint + Vector3.up * (building.heightM + 0.25f),
                        new Vector3(building.widthM * 1.02f, 0.6f, building.lengthM * 1.02f), rotation, metalRoof);
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

            // Planting follows the mapped land cover, so the wooded peninsula reads as a wood and the
            // fields and the port yard stay open. That is what makes the way in legible from the water.
            var random = new System.Random(1577);
            for (float z = CoreZ.x; z < CoreZ.y; z += 12f)
            for (float x = CoreX.x; x < CoreX.y; x += 12f)
            {
                float px = x + RiverLandscapeBuilder.Range(random, -5.5f, 5.5f);
                float pz = z + RiverLandscapeBuilder.Range(random, -5.5f, 5.5f);
                float shore = geometry.SignedShoreDistance(px, pz);
                if (shore <= 0.5f) continue;
                var position = new Vector3(px, GroundHeight(geometry, shore, px, pz), pz);
                string cover = geometry.CoverAt(px, pz);
                if (shore < 4.5f && cover != "built" && cover != "sand")
                {
                    if (random.NextDouble() < 0.5)
                        RiverLandscapeBuilder.Place(reed, root, position,
                            RiverLandscapeBuilder.Range(random, 0.75f, 1.5f),
                            RiverLandscapeBuilder.Range(random, 0f, 360f));
                    continue;
                }
                if (NearBuilding(geometry, px, pz)) continue;
                double trees_, bushes_;
                switch (cover)
                {
                    case "wood": trees_ = 0.50; bushes_ = 0.10; break;
                    case "scrub": trees_ = 0.05; bushes_ = 0.30; break;
                    case "orchard": trees_ = 0.26; bushes_ = 0.05; break;
                    case "allotments": trees_ = 0.08; bushes_ = 0.10; break;
                    case "farmland": trees_ = 0.004; bushes_ = 0.004; break;
                    case "meadow": trees_ = 0.02; bushes_ = 0.03; break;
                    case "built": trees_ = 0.03; bushes_ = 0.02; break;
                    case "sand": trees_ = 0.0; bushes_ = 0.0; break;
                    // Unmapped ground: a thin fringe near the bank, open behind it.
                    default: trees_ = shore < 40f ? 0.14 : 0.05; bushes_ = 0.06; break;
                }
                double roll = random.NextDouble();
                if (roll < trees_)
                    RiverLandscapeBuilder.Place(trees[random.Next(trees.Length)], root, position,
                        RiverLandscapeBuilder.Range(random, 0.7f, 1.45f),
                        RiverLandscapeBuilder.Range(random, 0f, 360f));
                else if (roll < trees_ + bushes_)
                    RiverLandscapeBuilder.Place(bushes[random.Next(bushes.Length)], root, position,
                        RiverLandscapeBuilder.Range(random, 0.7f, 1.6f),
                        RiverLandscapeBuilder.Range(random, 0f, 360f));
            }
        }

        private static bool NearBuilding(ScenarioGeometry geometry, float x, float z)
        {
            foreach (ScenarioGeometry.Landmark landmark in geometry.landmarks)
            {
                float clearance = Mathf.Max(landmark.lengthM, landmark.widthM) * 0.6f + 12f;
                if (Sqr(x - landmark.x) + Sqr(z - landmark.z) < clearance * clearance) return true;
            }
            if (geometry.buildings == null) return false;
            foreach (ScenarioGeometry.Building building in geometry.buildings)
            {
                float clearance = Mathf.Max(building.lengthM, building.widthM) * 0.6f + 4f;
                if (Sqr(x - building.x) + Sqr(z - building.z) < clearance * clearance) return true;
            }
            return false;
        }

        private static float Sqr(float value) => value * value;

        // Russian inland buoyage is read going downstream: the red right-edge buoys stand on the bank
        // that is on your right when the current is behind you. This passage runs downstream, so red
        // sits to starboard of the route. The reach is third category: nothing here is lit at night.
        private static LeadingMarkPair[] BuildNavigation(FairwayRoute route, ScenarioGeometry geometry)
        {
            Transform root = new GameObject("Navigation").transform;
            Material red = NavigationMaterial("NavigationRed", new Color(0.75f, 0.06f, 0.04f));
            Material white = NavigationMaterial("NavigationWhite", new Color(0.9f, 0.9f, 0.82f));
            Material black = NavigationMaterial("MarkerBlack", new Color(0.03f, 0.03f, 0.03f));

            for (float distance = 560f; distance < 1910f; distance += 135f)
            {
                FairwayQuery query = route.QueryDistance(distance);
                PlaceBuoy(root, query.Position + query.Right * query.Sample.rightWidthM, red, white,
                    $"Unlit Right Red Buoy {distance:0000}");
                PlaceBuoy(root, query.Position - query.Right * query.Sample.leftWidthM, white, black,
                    $"Unlit Left White Buoy {distance:0000}");
            }
            // Axial marks: the system is used for the start point and the axis of a fairway, which is
            // exactly what the basin entrance and the Nara mouth are.
            foreach (float distance in new[] { 478f, 1985f })
            {
                FairwayQuery query = route.QueryDistance(distance);
                PlaceBuoy(root, query.Position, white, red, $"Unlit Axial Buoy {distance:0000}");
            }

            // The leading line for the mouth stands where a real one does: on the bank beyond the
            // straight, in line with it. Outbound it is a stern transit, inbound it leads you in.
            FairwayQuery straight = route.QueryDistance(1720f);
            Vector3 onLine = straight.Position;
            Vector3 upRiver = -straight.Tangent;
            while (geometry.SignedShoreDistance(onLine.x, onLine.z) < 45f && onLine.z < 900f)
                onLine += upRiver * 10f;
            Transform front = CreateLeadingMark("Nara Mouth Unlit Front Mark", root,
                Seat(geometry, onLine), 9f, white, black);
            Transform rear = CreateLeadingMark("Nara Mouth Unlit Rear Mark", root,
                Seat(geometry, onLine + upRiver * 200f), 15f, white, black);
            var pairObject = new GameObject("Nara Mouth Leading Line");
            pairObject.transform.SetParent(root, false);
            LeadingMarkPair pair = pairObject.AddComponent<LeadingMarkPair>();
            pair.Configure(front, rear, 1650f, 1990f);
            return new[] { pair };
        }

        private static Vector3 Seat(ScenarioGeometry geometry, Vector3 position)
        {
            position.y = GroundHeight(geometry, geometry.SignedShoreDistance(position.x, position.z),
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

        private static void SolidBox(Transform parent, string name, Vector3 center, Vector3 size,
            Quaternion rotation, Material material)
        {
            Box(parent, name, center, size, rotation, material, true);
        }

        private static void Box(Transform parent, string name, Vector3 center, Vector3 size,
            Quaternion rotation, Material material, bool solid = false)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.SetPositionAndRotation(center, rotation);
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(box.GetComponent<Collider>());
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
