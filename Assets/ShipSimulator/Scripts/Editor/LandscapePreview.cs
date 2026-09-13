using System.IO;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Editor
{
    public static class LandscapePreview
    {
        public static void Before() => CaptureBoth("before");
        public static void After() => CaptureBoth("after");

        private static void CaptureBoth(string stage)
        {
            Directory.CreateDirectory("Logs/Landscape");
            foreach (string name in new[] { "RiverTrainingScene", "GorodetsTrainingScene" })
            {
                EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity");
                var ship = Object.FindAnyObjectByType<ShipPhysicsController>();
                var cameraObject = new GameObject("Landscape preview");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.Skybox;
                camera.fieldOfView = 55;
                camera.farClipPlane = 2200;
                camera.allowHDR = true;
                var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.requiresDepthTexture = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                camera.transform.position = ship.transform.position + new Vector3(36, 19, -85);
                camera.transform.LookAt(ship.transform.position + new Vector3(0, 3, 50));
                Render(camera, $"Logs/Landscape/{name}-{stage}.png");
                camera.transform.position = ship.transform.position + new Vector3(24, 7, 80);
                camera.transform.LookAt(ship.transform.position + new Vector3(82, 7, 165));
                Render(camera, $"Logs/Landscape/{name}-{stage}-shore.png");
                Mesh bank = GameObject.Find("Right natural bank").GetComponent<MeshFilter>().sharedMesh;
                Vector3 edge = Vector3.zero;
                float nearest = float.MaxValue;
                foreach (Vector3 vertex in bank.vertices)
                {
                    float score = Mathf.Abs(vertex.y) * 10 + Mathf.Abs(vertex.z - ship.transform.position.z - 150);
                    if (score >= nearest) continue;
                    nearest = score;
                    edge = vertex;
                }
                camera.transform.position = edge + new Vector3(-7, 3.5f, -8);
                camera.transform.LookAt(edge + new Vector3(2, 0, 7));
                Render(camera, $"Logs/Landscape/{name}-{stage}-waterline.png");
                Object.DestroyImmediate(cameraObject);
            }
            Debug.Log("LANDSCAPE_PREVIEW|" + stage + " complete");
        }

        internal static void Render(Camera camera, string path)
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }
}
