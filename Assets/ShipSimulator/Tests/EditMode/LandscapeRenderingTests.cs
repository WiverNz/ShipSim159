using NUnit.Framework;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class LandscapeRenderingTests
    {
        [TestCase("RiverTrainingScene")]
        [TestCase("GorodetsTrainingScene")]
        public void Landscape_HasContinuousBanksVegetationLodsAndReflectiveWater(string name)
        {
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity");
            Transform landscape = GameObject.Find("Environment").transform.Find("Natural Landscape");
            Assert.That(landscape, Is.Not.Null);
            var left = landscape.Find("Left natural bank").GetComponent<MeshFilter>();
            var right = landscape.Find("Right natural bank").GetComponent<MeshFilter>();
            foreach (MeshFilter bank in new[] { left, right })
            {
                Assert.That(bank.sharedMesh.vertexCount, Is.GreaterThan(3000));
                Assert.That(bank.sharedMesh.bounds.size.z, Is.GreaterThanOrEqualTo(2400));
                foreach (Vector3 normal in bank.sharedMesh.normals)
                    Assert.That(normal.y, Is.GreaterThan(0), "Bank triangles must face upwards.");
            }
            LODGroup[] plants = landscape.GetComponentsInChildren<LODGroup>();
            Assert.That(plants.Length, Is.GreaterThan(700));
            int trees = 0;
            int bushes = 0;
            foreach (LODGroup plant in plants)
            {
                if (plant.name.StartsWith("Tree")) trees++;
                if (plant.name.StartsWith("Bush")) bushes++;
                LOD[] lods = plant.GetLODs();
                Assert.That(lods.Length, Is.EqualTo(3));
                int near = lods[0].renderers[lods[0].renderers.Length - 1].GetComponent<MeshFilter>().sharedMesh.vertexCount;
                int far = lods[2].renderers[lods[2].renderers.Length - 1].GetComponent<MeshFilter>().sharedMesh.vertexCount;
                Assert.That(far, Is.LessThan(near / 3));
            }
            Assert.That(trees, Is.GreaterThan(400));
            Assert.That(bushes, Is.GreaterThan(200));
            MeshCollider leftCollider = left.GetComponent<MeshCollider>();
            MeshCollider rightCollider = right.GetComponent<MeshCollider>();
            bool temporary = leftCollider == null;
            if (temporary)
            {
                leftCollider = left.gameObject.AddComponent<MeshCollider>();
                rightCollider = right.gameObject.AddComponent<MeshCollider>();
                leftCollider.sharedMesh = left.sharedMesh;
                rightCollider.sharedMesh = right.sharedMesh;
            }
            try
            {
                foreach (LODGroup plant in plants)
                {
                    Vector3 origin = plant.transform.position + Vector3.up * 100;
                    var ray = new Ray(origin, Vector3.down);
                    bool hit = leftCollider.Raycast(ray, out RaycastHit ground, 200) ||
                        rightCollider.Raycast(ray, out ground, 200);
                    Assert.That(hit, Is.True, plant.name + " must sit on a bank");
                    Assert.That(plant.transform.position.y, Is.EqualTo(ground.point.y).Within(0.08f),
                        plant.name + " must not float above the terrain");
                }
            }
            finally
            {
                if (temporary)
                {
                    Object.DestroyImmediate(leftCollider);
                    Object.DestroyImmediate(rightCollider);
                }
            }
            GameObject water = GameObject.Find("RiverWater");
            Assert.That(water.layer, Is.EqualTo(4));
            Assert.That(water.GetComponent<RiverPlanarReflection>(), Is.Not.Null);
            Assert.That(water.GetComponent<MeshFilter>().sharedMesh.bounds.size.z, Is.GreaterThanOrEqualTo(2400));
        }

        [TestCase("ShipSimulator/RiverWater")]
        [TestCase("ShipSimulator/RiverGround")]
        [TestCase("ShipSimulator/RiverFoliage")]
        public void LandscapeShader_CompilesWithoutErrors(string name)
        {
            Shader shader = Shader.Find(name);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
        }
    }
}
