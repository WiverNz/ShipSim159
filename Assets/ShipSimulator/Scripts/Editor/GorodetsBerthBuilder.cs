using ShipSimulator.Physics;
using ShipSimulator.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShipSimulator.Editor
{
    // A demonstration quay on the right bank of the Gorodets reach, just below the start, for mooring practice.
    // It is not a surveyed Gorodets structure: its position, size, dredged depth and bollard spacing are
    // estimated so a loaded 507B can lie alongside clear of the buoyed channel.
    public static class GorodetsBerthBuilder
    {
        public const string RootName = "Gorodets Demonstration Berth";
        private const string ScenePath = "Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity";
        // Quay face along x = FaceX from StartZ to EndZ, deck QuayTopY above the still water level.
        private const float FaceX = 48f, StartZ = -70f, EndZ = 80f, QuayTopY = 1.6f, QuayWidth = 12f;
        private static readonly float[] BollardZ = { -62f, -38f, -14f, 10f, 34f, 58f, 76f };

        // Adds or rebuilds the berth in the open Gorodets scene without regenerating anything else.
        [MenuItem("Ship Simulator/Install Gorodets Demonstration Berth")]
        public static void InstallInScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            Build(Object.FindAnyObjectByType<ScenarioBathymetry>());
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("BERTH|Gorodets demonstration berth installed");
        }

        // Batch entry point: install, then quit with the result.
        public static void Run()
        {
            try
            {
                InstallInScene();
                EditorApplication.Exit(0);
            }
            catch (System.Exception error)
            {
                Debug.LogError("BERTH|FAIL " + error);
                EditorApplication.Exit(1);
            }
        }

        public static void Build(ScenarioBathymetry bathymetry)
        {
            GameObject previous = GameObject.Find(RootName);
            if (previous != null) Object.DestroyImmediate(previous);
            var root = new GameObject(RootName).transform;
            Material concrete = Material("GorodetsQuay", new Color(0.52f, 0.52f, 0.5f));
            Material iron = Material("BollardIron", new Color(0.08f, 0.08f, 0.08f));
            Material timber = Material("FenderTimber", new Color(0.28f, 0.2f, 0.12f));

            float length = EndZ - StartZ, middle = 0.5f * (StartZ + EndZ);
            GameObject quay = Box("Quay", root, new Vector3(FaceX + 0.5f * QuayWidth, 0.5f * (QuayTopY - 6f), middle),
                new Vector3(QuayWidth, QuayTopY + 6f, length), concrete);
            quay.AddComponent<RadarObstacle>();
            for (float z = StartZ + 4f; z < EndZ - 2f; z += 8f)
            {
                GameObject fender = Box("Fender", root, new Vector3(FaceX - 0.15f, -0.2f, z), new Vector3(0.3f, 2.2f, 0.6f), timber);
                Object.DestroyImmediate(fender.GetComponent<Collider>());
            }
            for (int i = 0; i < BollardZ.Length; i++)
            {
                var point = new GameObject("Bollard B" + (i + 1)).transform;
                point.SetParent(root, false);
                point.position = new Vector3(FaceX + 1f, QuayTopY + 0.45f, BollardZ[i]);
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Post";
                post.transform.SetParent(point, false);
                post.transform.localPosition = new Vector3(0f, -0.2f, 0f);
                post.transform.localScale = new Vector3(0.5f, 0.25f, 0.5f);
                post.GetComponent<Renderer>().sharedMaterial = iron;
                Object.DestroyImmediate(post.GetComponent<Collider>());
                // The line leads over the quay edge in front of the bollard.
                point.gameObject.AddComponent<MooringPoint>().Configure("gorodets-berth-b" + (i + 1), "B" + (i + 1),
                    MooringPointKind.Shore, new Vector3(-1f, -0.35f, 0f), i);
            }
            if (bathymetry != null)
                bathymetry.SetBasins(new[]
                {
                    new BathymetryBasin
                    {
                        center = new Vector3(FaceX - 10f, 0f, middle), sizeM = new Vector2(20f, length + 10f),
                        depthM = 5f, marginM = 10f, bottomType = RiverBottomType.Silt
                    }
                });
            ClearVegetation();
        }

        // Shoreline reeds and bushes placed by the landscape pass would grow through the quay deck.
        public static void ClearVegetation()
        {
            GameObject landscape = GameObject.Find("Natural Landscape");
            if (landscape == null || GameObject.Find(RootName) == null) return;
            var doomed = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in landscape.transform)
            {
                Vector3 p = child.position;
                if (child.GetComponent<MeshFilter>() == null && child.GetComponentInChildren<Renderer>() == null) continue;
                if (child.name.Contains("natural bank")) continue;
                if (p.x > FaceX - 1f && p.x < FaceX + QuayWidth + 1f && p.z > StartZ - 1f && p.z < EndZ + 1f) doomed.Add(child.gameObject);
            }
            foreach (GameObject plant in doomed) Object.DestroyImmediate(plant);
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.position = position;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            box.isStatic = true;
            return box;
        }

        private static Material Material(string name, Color color)
        {
            string path = "Assets/ShipSimulator/Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
