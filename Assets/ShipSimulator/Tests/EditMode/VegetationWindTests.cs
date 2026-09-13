using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class VegetationWindTests
    {
        [TestCase("ShipSimulator/RiverBark")]
        [TestCase("ShipSimulator/RiverFoliage")]
        public void VegetationShader_SwaysInShadowsAndMotionVectors(string name)
        {
            Shader shader = Shader.Find(name);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var material = new Material(shader);
            try
            {
                Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0));
                Assert.That(material.FindPass("MotionVectors"), Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(material);
            }
        }

        [TestCase("WillowBark")]
        [TestCase("BirchBark")]
        public void BarkMaterials_BendWithTheirLeaves(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ShipSimulator/Settings/NaturalLandscape/" + name + ".mat");
            Assert.That(material.shader.name, Is.EqualTo("ShipSimulator/RiverBark"),
                "Branches on a standard shader stay rigid while their leaves sway.");
        }
    }
}
