using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ShipSimulator.Editor
{
    public static class RiverWaterAndSkyBuilder
    {
        private const string WaterFolder = "Assets/ShipSimulator/Settings/Water";
        public const string RippleNormalPath = WaterFolder + "/RiverRippleNormal.png";
        private const int RippleNormalSize = 512;

        [MenuItem("Ship Simulator/Apply Realistic Water And Sky")]
        public static void ApplyBoth()
        {
            foreach (string name in new[] { "RiverTrainingScene", "GorodetsTrainingScene" })
            {
                Scene scene = EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity");
                ShipSimulatorVisualUpgrade.ConfigurePostProcessing(scene);
                Apply(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("WATER_SKY|Both river scenes upgraded");
        }

        public static void Apply(Scene scene)
        {
            ShipSimulatorVisualUpgrade.ConfigureLighting(scene);
            GraphicsPhaseOneBuilder.ConfigureRenderer();
            ConfigureWater(scene);
            ConfigureCameras(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ConfigureWater(Scene scene)
        {
            Renderer water = FindWater(scene);
            if (water == null) return;
            Material material = water.sharedMaterial;
            material.SetTexture("_RippleNormal", EnsureRippleNormalMap());
            material.SetFloat("_RippleTileM", 11f);
            material.SetFloat("_RippleStrength", 0.2f);
            material.SetFloat("_ReflectionDistortion", 0.18f);
            material.SetColor("_ShallowColor", new Color(0.19f, 0.24f, 0.15f, 1f));
            material.SetColor("_DeepColor", new Color(0.06f, 0.095f, 0.07f, 1f));
            material.SetColor("_FoamColor", new Color(0.86f, 0.89f, 0.86f, 1f));
            material.SetColor("_AeratedColor", new Color(0.36f, 0.47f, 0.4f, 1f));
            material.SetFloat("_Smoothness", 0.86f);
            material.SetFloat("_ReflectionStrength", 0.85f);
            material.SetFloat("_WaveHeight", 0.035f);
            material.SetFloat("_Opacity", 1f);
            EditorUtility.SetDirty(material);
        }

        private static Renderer FindWater(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Environment") continue;
                Transform water = root.transform.Find("RiverWater");
                if (water != null) return water.GetComponent<Renderer>();
            }
            return null;
        }

        private static void ConfigureCameras(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (UniversalAdditionalCameraData camera in
                     root.GetComponentsInChildren<UniversalAdditionalCameraData>(true))
            {
                camera.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                camera.taaSettings.quality = TemporalAAQuality.High;
                camera.taaSettings.baseBlendFactor = 0.2f;
                camera.GetComponent<Camera>().allowMSAA = false;
                camera.antialiasingQuality = AntialiasingQuality.High;
                EditorUtility.SetDirty(camera);
            }
        }

        public static Texture2D EnsureRippleNormalMap()
        {
            if (!AssetDatabase.IsValidFolder(WaterFolder))
                AssetDatabase.CreateFolder("Assets/ShipSimulator/Settings", "Water");

            var texture = new Texture2D(RippleNormalSize, RippleNormalSize, TextureFormat.RGB24, false);
            texture.SetPixels32(CreateRippleNormalPixels(RippleNormalSize));
            byte[] png = texture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(texture);
            // Rewrite only on change, so rebuilding keeps the asset and its GUID untouched.
            if (!File.Exists(RippleNormalPath) || !File.ReadAllBytes(RippleNormalPath).SequenceEqual(png))
            {
                File.WriteAllBytes(RippleNormalPath, png);
                AssetDatabase.ImportAsset(RippleNormalPath, ImportAssetOptions.ForceUpdate);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(RippleNormalPath);
            if (importer.textureType != TextureImporterType.NormalMap ||
                importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled ||
                importer.anisoLevel != 8 || importer.filterMode != FilterMode.Trilinear ||
                importer.maxTextureSize != RippleNormalSize)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.anisoLevel = 8;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = RippleNormalSize;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(RippleNormalPath);
        }

        // Sum of sine ripples with whole-number wave counts per tile, so the map repeats
        // without a seam. The spectrum leans along one axis like wind-driven ripples.
        public static Color32[] CreateRippleNormalPixels(int size)
        {
            const int waveCount = 72;
            var random = new System.Random(5071);
            var waveX = new int[waveCount];
            var waveY = new int[waveCount];
            var amplitude = new double[waveCount];
            var phase = new double[waveCount];
            for (int i = 0; i < waveCount; i++)
            {
                double magnitude = 3 + 37 * Math.Pow(i / (double)(waveCount - 1), 1.5);
                double angle = random.NextDouble() < 0.25
                    ? random.NextDouble() * Math.PI * 2
                    : (random.NextDouble() - 0.5) * 1.8;
                waveX[i] = (int)Math.Round(magnitude * Math.Cos(angle));
                waveY[i] = (int)Math.Round(magnitude * Math.Sin(angle));
                if (waveX[i] == 0 && waveY[i] == 0) waveX[i] = 3;
                amplitude[i] = Math.Pow(waveX[i] * waveX[i] + waveY[i] * waveY[i], -0.95);
                phase[i] = random.NextDouble() * Math.PI * 2;
            }

            var slopeX = new double[size * size];
            var slopeY = new double[size * size];
            double sumSquares = 0;
            for (int row = 0; row < size; row++)
            for (int column = 0; column < size; column++)
            {
                double x = 0;
                double y = 0;
                for (int i = 0; i < waveCount; i++)
                {
                    double wave = Math.Cos(2 * Math.PI * (waveX[i] * column + waveY[i] * row) / size + phase[i]) *
                        amplitude[i] * 2 * Math.PI;
                    x += wave * waveX[i];
                    y += wave * waveY[i];
                }
                slopeX[row * size + column] = x;
                slopeY[row * size + column] = y;
                sumSquares += x * x + y * y;
            }

            double scale = 0.42 / Math.Sqrt(sumSquares / (size * size));
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                Vector3 normal = new Vector3((float)(-slopeX[i] * scale), (float)(-slopeY[i] * scale), 1f).normalized;
                pixels[i] = new Color32(Encode(normal.x), Encode(normal.y), Encode(normal.z), 255);
            }
            return pixels;
        }

        private static byte Encode(float component)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(component * 0.5f + 0.5f) * 255f);
        }
    }
}
