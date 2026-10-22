using NUnit.Framework;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class NavigationLensRenderingTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void DistantLens_RemainsAPointInsteadOfCoveringTheShip(bool orthographic)
        {
            var cameraObject = new GameObject("Lens projection test");
            var lens = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(Shader.Find("ShipSimulator/NavigationSectorLight"));
            var target = new RenderTexture(256, 256, 24);
            var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                lens.layer = 31;
                lens.transform.localScale = Vector3.one * .42f;
                lens.GetComponent<Renderer>().sharedMaterial = material;
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_SectorArc", 360);
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = orthographic;
                camera.orthographicSize = 55;
                camera.transform.position = new Vector3(0, 0, -200);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                image.Apply();
                int lit = 0;
                foreach (Color pixel in image.GetPixels()) if (pixel.maxColorComponent > .05f) lit++;
                Assert.That(lit, Is.GreaterThan(0), "Lens must remain visible.");
                Assert.That(lit, Is.LessThan(100), "Navigation lens became a large disc.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lens);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
