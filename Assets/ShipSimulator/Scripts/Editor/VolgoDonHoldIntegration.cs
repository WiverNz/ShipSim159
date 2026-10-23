using System;
using System.IO;
using System.Linq;
using ShipSimulator.Persistence;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // Installs HoldInteriorVisual on the 507B prefab: hold extents measured from the authored hold meshes with
    // the covers closed, a stencil material that keeps river water out of open holds, and a nominal bulk cargo
    // material. Called by the mechanism integration; Run() applies it to the existing prefab in batch mode.
    public static class VolgoDonHoldIntegration
    {
        private const string MaterialFolder = VolgoDonMechanismIntegration.Folder + "/Materials";
        private const string CargoMaterialPath = MaterialFolder + "/CargoNominalBulk.mat";
        private const string CargoTexturePath = VolgoDonMechanismIntegration.Folder + "/Textures/CargoNominalBulk.png";
        private const string MaskMaterialPath = "Assets/ShipSimulator/Materials/HullWaterMask.mat";
        // Estimated nominal bulk cargo (sand/gravel class); the loading model does not name a cargo.
        public const float NominalBulkDensityKgM3 = 1600f;
        // Kept between the highest heap and the closed covers' underside.
        private const float CoverClearanceM = 0.3f;

        public static void Run()
        {
            try
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(VolgoDonMechanismIntegration.PrefabPath);
                try
                {
                    Install(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, VolgoDonMechanismIntegration.PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("HOLD_INTEGRATION|PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        public static void Install(GameObject root)
        {
            var rig = root.GetComponent<VesselMechanisms>();
            MechanismSave pose = rig != null ? rig.CaptureState() : null;
            rig?.RestoreState(null);
            VesselData data = root.GetComponent<VesselDataLoader>().Load();
            var holds = new[]
            {
                Measure(root, "Fore", Compartment(data, "Forward hold")),
                Measure(root, "Aft", Compartment(data, "Aft hold"))
            };
            rig?.RestoreState(pose);
            var visual = root.GetComponent<HoldInteriorVisual>() ?? root.AddComponent<HoldInteriorVisual>();
            visual.Configure(CargoMaterial(), MaskMaterial(), NominalBulkDensityKgM3, holds);
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (child.name.StartsWith("Cargo_") || child.name.StartsWith("WaterMask_")) Object.DestroyImmediate(child.gameObject);
        }

        private static int Compartment(VesselData data, string id)
        {
            int index = Array.FindIndex(data.loading.compartments, c => c.id == id);
            if (index < 0) throw new InvalidDataException("No loading compartment named " + id);
            return index;
        }

        // Clear inner space: floor top, inside of the side linings, inside of the end bulkheads, under the covers.
        private static HoldInteriorVisual.Hold Measure(GameObject root, string name, int compartment)
        {
            Bounds floor = Local(root, "HoldFloor_" + name);
            Bounds port = Local(root, "HoldLining_" + name + "-1");
            Bounds starboard = Local(root, "HoldLining_" + name + "1");
            Bounds endA = Local(root, "HoldEnd_" + name + "0");
            Bounds endB = Local(root, "HoldEnd_" + name + "1");
            float coverUnderside = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.name.StartsWith("Panel_" + name) || r.name.StartsWith("Hatch_" + name))
                .Select(r => LocalBounds(root, r).min.y).DefaultIfEmpty(port.max.y).Min();
            float aft = Mathf.Min(endA.max.z, endB.max.z);
            float fore = Mathf.Max(endA.min.z, endB.min.z);
            float floorTop = floor.max.y;
            return new HoldInteriorVisual.Hold
            {
                name = name,
                compartment = compartment,
                floorCentre = new Vector3(0.5f * (port.max.x + starboard.min.x), floorTop, 0.5f * (aft + fore)),
                footprint = new Vector2(starboard.min.x - port.max.x, fore - aft),
                maxHeightM = Mathf.Min(coverUnderside, port.max.y) - floorTop - CoverClearanceM,
                maskHeight = Mathf.Min(coverUnderside, port.max.y) - 0.05f
            };
        }

        private static Bounds Local(GameObject root, string name)
        {
            Renderer renderer = root.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == name);
            if (renderer == null) throw new InvalidDataException("Hold mesh not found: " + name);
            return LocalBounds(root, renderer);
        }

        private static Bounds LocalBounds(GameObject root, Renderer renderer)
        {
            Bounds world = renderer.bounds;
            var local = new Bounds(root.transform.InverseTransformPoint(world.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = world.center + Vector3.Scale(world.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                local.Encapsulate(root.transform.InverseTransformPoint(corner));
            }
            return local;
        }

        private static Material MaskMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaskMaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("ShipSimulator/HullWaterMask")) { name = "HullWaterMask" };
            AssetDatabase.CreateAsset(material, MaskMaterialPath);
            return material;
        }

        private static Material CargoMaterial()
        {
            Texture2D texture = CargoTexture();
            var material = AssetDatabase.LoadAssetAtPath<Material>(CargoMaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(MaterialFolder);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CargoNominalBulk" };
                AssetDatabase.CreateAsset(material, CargoMaterialPath);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", new Color(0.74f, 0.66f, 0.52f));
            material.SetFloat("_Smoothness", 0.08f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // Generated grain texture: value noise at three scales, no third-party imagery.
        private static Texture2D CargoTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(CargoTexturePath);
            if (existing != null) return existing;
            const int size = 256;
            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            var random = new System.Random(507);
            float[] coarse = Noise(random, 16), medium = Noise(random, 64), fine = Noise(random, 256);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float value = 0.55f + 0.18f * Sample(coarse, 16, x, y, size) + 0.15f * Sample(medium, 64, x, y, size) +
                        0.12f * Sample(fine, 256, x, y, size);
                    image.SetPixel(x, y, new Color(value, value * 0.97f, value * 0.92f));
                }
            File.WriteAllBytes(CargoTexturePath, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(CargoTexturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CargoTexturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(CargoTexturePath);
        }

        private static float[] Noise(System.Random random, int cells)
        {
            var values = new float[cells * cells];
            for (int i = 0; i < values.Length; i++) values[i] = (float)random.NextDouble() * 2f - 1f;
            return values;
        }

        // Tiling bilinear lookup.
        private static float Sample(float[] values, int cells, int x, int y, int size)
        {
            float u = (float)x / size * cells, v = (float)y / size * cells;
            int x0 = (int)u, y0 = (int)v;
            float tx = Mathf.SmoothStep(0, 1, u - x0), ty = Mathf.SmoothStep(0, 1, v - y0);
            float V(int i, int j) => values[(j % cells) * cells + i % cells];
            return Mathf.Lerp(Mathf.Lerp(V(x0, y0), V(x0 + 1, y0), tx), Mathf.Lerp(V(x0, y0 + 1), V(x0 + 1, y0 + 1), tx), ty);
        }
    }
}
