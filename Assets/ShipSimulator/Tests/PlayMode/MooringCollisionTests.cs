using System.Collections;
using System.IO;
using NUnit.Framework;
using ShipSimulator.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ShipSimulator.Tests
{
    public sealed class MooringCollisionTests
    {
        private Scene voyage;
        private Scene collisionScene;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (collisionScene.IsValid()) yield return SceneManager.UnloadSceneAsync(collisionScene);
            if (voyage.IsValid()) yield return SceneManager.UnloadSceneAsync(voyage);
        }

        [UnityTest]
        public IEnumerator SerpukhovMoorings_StopFastBodiesAndAppearOnTheRadar()
        {
            yield return SceneManager.LoadSceneAsync("SerpukhovZatonScene", LoadSceneMode.Additive);
            voyage = SceneManager.GetSceneByName("SerpukhovZatonScene");
            yield return null;
            ShipTelemetryUI hud = null;
            Transform moorings = null;
            foreach (GameObject root in voyage.GetRootGameObjects())
            {
                if (hud == null) hud = root.GetComponentInChildren<ShipTelemetryUI>();
                foreach (Transform child in root.GetComponentsInChildren<Transform>())
                    if (child.name == "Laid-up craft") moorings = child;
            }
            Assert.That(hud, Is.Not.Null);
            Assert.That(moorings, Is.Not.Null);
            RadarObstacle[] obstacles = moorings.GetComponentsInChildren<RadarObstacle>();
            Assert.That(obstacles.Length, Is.GreaterThan(20));
            Transform world = hud.transform.Find("MiniMap/Viewport/MovingWorld");
            Assert.That(world, Is.Not.Null);
            collisionScene = SceneManager.CreateScene("Mooring collision test",
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            PhysicsScene physics = collisionScene.GetPhysicsScene();

            foreach (RadarObstacle obstacle in obstacles)
            {
                RectTransform echo = world.Find("Obstacle " + obstacle.name) as RectTransform;
                Assert.That(echo, Is.Not.Null, obstacle.name);
                obstacle.Project(hud.Ship.transform.position, hud.Ship.transform.eulerAngles.y,
                    0.82f, -82f, out Vector2 position, out Vector2 size, out float angle);
                Assert.That(Vector2.Distance(echo.anchoredPosition, position), Is.LessThan(1f));
                Assert.That(Vector2.Distance(echo.sizeDelta, size), Is.LessThan(0.01f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(echo.localEulerAngles.z, angle)), Is.LessThan(0.1f));

                BoxCollider source = obstacle.GetComponent<BoxCollider>();
                var target = new GameObject("Fixed mooring");
                SceneManager.MoveGameObjectToScene(target, collisionScene);
                target.transform.SetPositionAndRotation(Vector3.zero, obstacle.transform.rotation);
                BoxCollider collider = target.AddComponent<BoxCollider>();
                collider.size = Vector3.Scale(source.size, obstacle.transform.lossyScale);
                foreach (Vector3 axis in new[] { Vector3.right, Vector3.forward })
                {
                    float extent = Vector3.Dot(collider.size, axis) * 0.5f;
                    Vector3 direction = target.transform.rotation * axis;
                    var probe = new GameObject("Approaching vessel");
                    SceneManager.MoveGameObjectToScene(probe, collisionScene);
                    probe.transform.position = -direction * (extent + 8f);
                    probe.AddComponent<BoxCollider>().size = Vector3.one * 2f;
                    Rigidbody body = probe.AddComponent<Rigidbody>();
                    body.useGravity = false;
                    body.mass = 30000f;
                    body.constraints = RigidbodyConstraints.FreezeRotation;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    body.linearVelocity = direction * 25f;
                    for (int step = 0; step < 40; step++) physics.Simulate(0.02f);
                    Assert.That(Vector3.Dot(body.position, direction), Is.LessThan(-extent),
                        obstacle.name + " must block a 25 m/s approach.");
                    Assert.That(Vector3.Dot(body.linearVelocity, direction), Is.LessThan(0.5f));
                    Object.Destroy(probe);
                    yield return null;
                }
                Object.Destroy(target);
                yield return null;
            }
            yield return Capture(hud.GetComponent<Canvas>(), "radar-moorings.png");
        }

        private IEnumerator Capture(Canvas canvas, string name)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Serpukhov"));
            Directory.CreateDirectory(directory);
            var cameraObject = new GameObject("Menu capture camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10000);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var target = new RenderTexture(1440, 900, 24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10;
            yield return null;
            yield return null;
            foreach (Text text in canvas.GetComponentsInChildren<Text>())
                text.font.RequestCharactersInTexture(text.text,
                    Mathf.RoundToInt(text.fontSize * text.pixelsPerUnit), text.fontStyle);
            foreach (Text text in canvas.GetComponentsInChildren<Text>()) text.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(directory, name), image.EncodeToPNG());
            RenderTexture.active = previous;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            camera.targetTexture = null;
            Object.Destroy(image);
            Object.Destroy(target);
            Object.Destroy(cameraObject);
            Assert.That(File.Exists(Path.Combine(directory, name)), Is.True);
        }
    }
}
