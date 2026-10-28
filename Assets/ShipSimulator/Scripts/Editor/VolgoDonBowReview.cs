using System;
using System.IO;
using ShipSimulator.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    public static class VolgoDonBowReview
    {
        public static void Validate(GameObject vessel)
        {
            int levels = 0;
            foreach (var filter in vessel.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name != "VD507B_hull" && filter.transform.parent?.name != "VD507B_hull") continue;
                var mesh = filter.sharedMesh;
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                var normals = mesh.normals;
                Matrix4x4 local = vessel.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                float area = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = local.MultiplyPoint3x4(vertices[triangles[i]]);
                    Vector3 b = local.MultiplyPoint3x4(vertices[triangles[i + 1]]);
                    Vector3 c = local.MultiplyPoint3x4(vertices[triangles[i + 2]]);
                    bool InStep(Vector3 v) => v.z > 55.90f && v.z < 55.94f && v.y > 1.98f && v.y < 2.94f;
                    if (!InStep(a) || !InStep(b) || !InStep(c)) continue;
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    if (Vector3.Dot(cross.normalized, Vector3.back) < .99f)
                        throw new InvalidOperationException("Forecastle wall faces into the ship: " + filter.name);
                    for (int k = 0; k < 3; k++)
                        if (Vector3.Dot(local.inverse.transpose.MultiplyVector(normals[triangles[i + k]]).normalized, Vector3.back) < .9f)
                            throw new InvalidOperationException("Forecastle shading normal faces inward: " + filter.name);
                    area += cross.magnitude * .5f;
                }
                if (area < 13.5f || area > 14f) throw new InvalidOperationException("Forecastle wall coverage changed: " + filter.name + " " + area);
                levels++;
            }
            if (levels != 3) throw new InvalidOperationException("Expected the hull in all three LODs.");
        }

        private static void Capture(Camera camera, string path)
        {
            var target = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(target);
            }
        }

        public static void Run()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-bowStage");
                string stage = index >= 0 ? args[index + 1] : "after";
                EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity");
                var ship = Object.FindAnyObjectByType<ShipPhysicsController>();
                var group = ship.GetComponentInChildren<LODGroup>();
                if (stage != "before") Validate(ship.gameObject);
                var go = new GameObject("Bow review camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
                var camera = go.GetComponent<Camera>();
                camera.fieldOfView = 40;
                camera.nearClipPlane = .1f;
                camera.farClipPlane = 2500;
                camera.allowHDR = true;
                go.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
                Directory.CreateDirectory("Logs/BowGap");
                for (int level = 0; level < 3; level++)
                {
                    group.ForceLOD(level);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        go.transform.position = ship.transform.TransformPoint(new Vector3(side * 18, 13, 34));
                        go.transform.LookAt(ship.transform.TransformPoint(new Vector3(0, 2.5f, 57)));
                        Capture(camera, $"Logs/BowGap/{stage}-lod{level}-{(side < 0 ? "port" : "starboard")}.png");
                    }
                }
                group.ForceLOD(-1);
                Object.DestroyImmediate(go);
                Debug.Log("BOW_REVIEW|PASS " + stage);
                EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
    }
}
