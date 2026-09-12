using NUnit.Framework;
using ShipSimulator.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Tests
{
    public sealed class RiverWaterAndSkyTests
    {
        [TestCase("RiverTrainingScene")]
        [TestCase("GorodetsTrainingScene")]
        public void Scene_UsesCloudSkyRippleNormalMapAndSmaa(string name)
        {
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity");

            Assert.That(RenderSettings.skybox, Is.Not.Null);
            Assert.That(RenderSettings.skybox.shader.name, Is.EqualTo("ShipSimulator/RiverSky"));

            Material water = GameObject.Find("RiverWater").GetComponent<Renderer>().sharedMaterial;
            Texture ripple = water.GetTexture("_RippleNormal");
            Assert.That(AssetDatabase.GetAssetPath(ripple), Is.EqualTo(RiverWaterAndSkyBuilder.RippleNormalPath));
            var importer = (TextureImporter)AssetImporter.GetAtPath(RiverWaterAndSkyBuilder.RippleNormalPath);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.NormalMap));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.mipmapEnabled, Is.True, "Mipmaps keep distant ripples from shimmering.");

            var camera = GameObject.Find("Main Camera").GetComponent<UniversalAdditionalCameraData>();
            Assert.That(camera.antialiasing, Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
        }

        [Test]
        public void RippleNormalMap_WrapsWithoutASeam()
        {
            const int size = 256;
            Color32[] pixels = RiverWaterAndSkyBuilder.CreateRippleNormalPixels(size);
            for (int line = 0; line < size; line++)
            {
                int largestRowStep = 0;
                int largestColumnStep = 0;
                for (int i = 0; i < size - 1; i++)
                {
                    largestRowStep = Mathf.Max(largestRowStep,
                        Difference(pixels[line * size + i], pixels[line * size + i + 1]));
                    largestColumnStep = Mathf.Max(largestColumnStep,
                        Difference(pixels[i * size + line], pixels[(i + 1) * size + line]));
                }
                Assert.That(Difference(pixels[line * size + size - 1], pixels[line * size]),
                    Is.LessThanOrEqualTo(largestRowStep + 1));
                Assert.That(Difference(pixels[(size - 1) * size + line], pixels[line]),
                    Is.LessThanOrEqualTo(largestColumnStep + 1));
            }
        }

        [TestCase("ShipSimulator/RiverSky")]
        public void SkyShader_CompilesWithoutErrors(string name)
        {
            Shader shader = Shader.Find(name);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }

        private static int Difference(Color32 a, Color32 b)
        {
            return Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g));
        }
    }
}
