using System.IO;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // Still captures of the Serpukhov basin, so a scene rebuild can be looked at instead of assumed.
    // Edit-mode renders: the wake, the weather and the HUD are not in these frames.
    public static class SerpukhovZatonPreview
    {
        private const string ScenePath = "Assets/ShipSimulator/Scenes/SerpukhovZatonScene.unity";
        private const string OutputRoot = "Logs/Serpukhov";

        [MenuItem("Ship Simulator/Render Serpukhov Zaton Preview")]
        public static void Render()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            FairwayRoute route = Object.FindAnyObjectByType<FairwayRoute>();
            GameObject vessel = GameObject.Find("TrainingVessel");

            var cameraObject = new GameObject("Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.allowHDR = true;
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 2600f;
            UniversalAdditionalCameraData data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            Capture(camera, "01-lay-up-berth", route, 15f, new Vector3(0f, 22f, -130f), 210f);
            Capture(camera, "02-basin", route, 200f, new Vector3(0f, 44f, -260f), 380f);
            Capture(camera, "03-port-and-marina", route, 390f, new Vector3(0f, 36f, -210f), 340f);
            Capture(camera, "04-basin-entrance", route, 500f, new Vector3(0f, 32f, -190f), 320f);
            Capture(camera, "05-nara-reach", route, 1200f, new Vector3(0f, 30f, -200f), 320f);
            Capture(camera, "06-nara-mouth", route, 1940f, new Vector3(0f, 34f, -200f), 330f);
            if (vessel != null)
            {
                camera.transform.position = vessel.transform.position + new Vector3(24f, 11f, -34f);
                camera.transform.LookAt(vessel.transform.position + Vector3.up * 2f);
                Write(camera, "07-vessel-on-station");
            }
            foreach (string shoreName in new[] { "Port administration", "Vladychny monastery massing",
                         "Passenger berth", "Laid-up craft 2" })
            {
                GameObject item = GameObject.Find(shoreName);
                if (item == null) continue;
                camera.transform.position = item.transform.position + new Vector3(58f, 18f, -62f);
                camera.transform.LookAt(item.transform.position);
                Write(camera, "09-" + shoreName.Replace(' ', '-').ToLowerInvariant());
            }
            GameObject mark = GameObject.Find("Nara Entry Unlit Front Mark");
            if (mark != null)
            {
                camera.transform.position = mark.transform.position + new Vector3(46f, 16f, -58f);
                camera.transform.LookAt(mark.transform.position + Vector3.up * 6f);
                Write(camera, "09-entry-leading-marks");
            }
            Transform town = GameObject.Find("Shore buildings") != null
                ? GameObject.Find("Shore buildings").transform : null;
            if (town != null && town.childCount > 0)
            {
                Transform block = town.GetChild(town.childCount / 2);
                camera.transform.position = block.position + new Vector3(62f, 26f, -78f);
                camera.transform.LookAt(block.position);
                Write(camera, "10-town-massing");
            }
            Transform craftRoot = GameObject.Find("Laid-up craft") != null
                ? GameObject.Find("Laid-up craft").transform : null;
            if (craftRoot != null && craftRoot.childCount > 0)
            {
                Transform hull = craftRoot.GetChild(0);
                camera.transform.position = hull.position + new Vector3(52f, 22f, -60f);
                camera.transform.LookAt(hull.position);
                Write(camera, "11-laid-up-craft");
            }
            camera.transform.position = new Vector3(160f, 620f, 560f);
            camera.transform.rotation = Quaternion.Euler(70f, 0f, 0f);
            camera.fieldOfView = 62f;
            Write(camera, "08-plan-view");

            Object.DestroyImmediate(cameraObject);
            Debug.Log("SERPUKHOV_PREVIEW|PASS captures in " + OutputRoot);
        }

        private static void Capture(Camera camera, string name, FairwayRoute route,
            float routeDistanceM, Vector3 localOffset, float height)
        {
            FairwayQuery query = route.QueryDistance(routeDistanceM);
            Quaternion along = Quaternion.LookRotation(query.Tangent, Vector3.up);
            camera.transform.position = query.Position + along * localOffset + Vector3.up * (height * 0.12f);
            camera.transform.LookAt(query.Position + query.Tangent * 90f);
            Write(camera, name);
        }

        private static void Write(Camera camera, string name)
        {
            const int width = 1600;
            const int height = 900;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            Directory.CreateDirectory(OutputRoot);
            File.WriteAllBytes(Path.Combine(OutputRoot, name + ".png"), image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }
}
