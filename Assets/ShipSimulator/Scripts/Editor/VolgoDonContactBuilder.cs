using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using ShipSimulator.Physics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    public static class VolgoDonContactBuilder
    {
        private const string Folder = VolgoDonMechanismIntegration.Folder + "/Contact";
        private static readonly float[] Cuts = { -70f, -66f, -64f, -62f, -60f, -54f, -44f, -25f, 25f, 44f, 54f, 62f, 66f, 70f };

        public static void Install(GameObject root)
        {
            var hull = root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "VD507B_hull");
            if (hull == null) return;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(VolgoDonMechanismIntegration.Folder, "Contact");
            Transform parent = root.transform.Find("CollisionHull");
            if (parent == null)
            {
                parent = new GameObject("CollisionHull").transform;
                parent.SetParent(root.transform, false);
            }
            foreach (Transform child in parent.Cast<Transform>().ToArray())
                if (!child.name.StartsWith("Section")) Object.DestroyImmediate(child.gameObject);
            Vector3[] source = hull.sharedMesh.vertices.Select(v => root.transform.InverseTransformPoint(hull.transform.TransformPoint(v))).ToArray();
            int[] indices = hull.sharedMesh.triangles;
            for (int section = 0; section + 1 < Cuts.Length; section++)
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                for (int t = 0; t < indices.Length; t += 3)
                {
                    var polygon = new List<Vector3> { source[indices[t]], source[indices[t + 1]], source[indices[t + 2]] };
                    polygon = Clip(polygon, 2, Cuts[section], true);
                    polygon = Clip(polygon, 2, Cuts[section + 1], false);
                    polygon = Clip(polygon, 1, 1.97f, false);
                    int start = vertices.Count;
                    vertices.AddRange(polygon);
                    for (int j = 1; j + 1 < polygon.Count; j++) triangles.AddRange(new[] { start, start + j, start + j + 1 });
                }
                string name = "Section" + section.ToString("00");
                string path = Folder + "/" + name + ".asset";
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
                mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                Transform child = parent.Find(name);
                if (child == null) { child = new GameObject(name).transform; child.SetParent(parent, false); }
                var collider = child.GetComponent<MeshCollider>();
                if (collider == null) collider = child.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = null;
                collider.convex = true;
                collider.sharedMesh = mesh;
                collider.contactOffset = 0.02f;
            }
        }

        private static List<Vector3> Clip(List<Vector3> input, int axis, float limit, bool above)
        {
            var result = new List<Vector3>();
            if (input.Count == 0) return result;
            Vector3 previous = input[input.Count - 1];
            bool insidePrevious = above ? previous[axis] >= limit : previous[axis] <= limit;
            foreach (Vector3 current in input)
            {
                bool inside = above ? current[axis] >= limit : current[axis] <= limit;
                if (inside != insidePrevious) result.Add(Vector3.Lerp(previous, current, (limit - previous[axis]) / (current[axis] - previous[axis])));
                if (inside) result.Add(current);
                previous = current; insidePrevious = inside;
            }
            return result;
        }

        [Serializable] private sealed class Coverage
        {
            public int samples, colliders;
            public float oldMaxGapM, newMaxGapM, oldMeanGapM, newMeanGapM;
            public int raySamples;
            public float maxExcessBreadthM, maxMissingBreadthM;
            public string scope = "Hull mesh vertices at or below y=1.97 m, distance to closed collider solids; convex envelopes can bridge concavities.";
        }
        private static void Measure()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(VolgoDonMechanismIntegration.PrefabPath));
            try
            {
                UnityEngine.Physics.SyncTransforms();
                var hull = root.GetComponentsInChildren<MeshFilter>().First(f => f.name == "VD507B_hull");
                Collider[] parts = root.GetComponentsInChildren<Collider>();
                var old = new[] { new Bounds(new Vector3(0,-1.25f,-5),new Vector3(16.2f,4.6f,82)),
                    new Bounds(new Vector3(0,-1.05f,48),new Vector3(11.5f,4.1f,28)),
                    new Bounds(new Vector3(0,-1.15f,-54),new Vector3(13.5f,4.3f,22)) };
                var result = new Coverage { colliders = parts.Length };
                foreach (Vector3 vertex in hull.sharedMesh.vertices)
                {
                    Vector3 world = hull.transform.TransformPoint(vertex);
                    Vector3 local = root.transform.InverseTransformPoint(world);
                    if (local.y > 1.9701f) continue;
                    float before = old.Min(b => Vector3.Distance(local, b.ClosestPoint(local)));
                    float after = parts.Min(c => Vector3.Distance(world, c.ClosestPoint(world)));
                    result.samples++; result.oldMeanGapM += before; result.newMeanGapM += after;
                    result.oldMaxGapM = Mathf.Max(result.oldMaxGapM, before); result.newMaxGapM = Mathf.Max(result.newMaxGapM, after);
                }
                result.oldMeanGapM /= result.samples; result.newMeanGapM /= result.samples;
                var probe = new GameObject("Static hull measurement");
                try
                {
                    probe.transform.SetPositionAndRotation(hull.transform.position, hull.transform.rotation);
                    probe.transform.localScale = hull.transform.lossyScale;
                    var exact = probe.AddComponent<MeshCollider>(); exact.sharedMesh = hull.sharedMesh;
                    UnityEngine.Physics.SyncTransforms();
                    var csv = new System.Text.StringBuilder("z,y,visual_x,contact_x,excess_m\n");
                    for (float y = -3f; y <= 1f; y += 1f)
                    for (float z = -68f; z <= 68f; z += 1f)
                    {
                        var ray = new Ray(new Vector3(20,y,z), Vector3.left);
                        if (!exact.Raycast(ray, out RaycastHit visualHit, 20)) continue;
                        float contactX = float.NegativeInfinity;
                        foreach (Collider part in parts)
                            if (part.Raycast(ray, out RaycastHit hit, 20)) contactX = Mathf.Max(contactX, hit.point.x);
                        if (float.IsNegativeInfinity(contactX)) continue;
                        float delta = contactX - visualHit.point.x;
                        result.raySamples++;
                        result.maxExcessBreadthM = Mathf.Max(result.maxExcessBreadthM, delta);
                        result.maxMissingBreadthM = Mathf.Max(result.maxMissingBreadthM, -delta);
                        csv.AppendLine(FormattableString.Invariant($"{z},{y},{visualHit.point.x},{contactX},{delta}"));
                    }
                    File.WriteAllText("Art/Source/VolgoDon507B/part16-cross-sections.csv", csv.ToString());
                }
                finally { Object.DestroyImmediate(probe); }
                File.WriteAllText("Art/Source/VolgoDon507B/part16-coverage.json", JsonUtility.ToJson(result, true));
            }
            finally { Object.DestroyImmediate(root); }
        }

        public static void Run()
        {
            try
            {
                var root = PrefabUtility.LoadPrefabContents(VolgoDonMechanismIntegration.PrefabPath);
                try { Install(root); PrefabUtility.SaveAsPrefabAsset(root, VolgoDonMechanismIntegration.PrefabPath); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                var scene = EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity");
                foreach (Transform item in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if ((item.name.StartsWith("Left Bank ") || item.name.StartsWith("Right Bank ") || item.name == "Visible Rocky Shoals") &&
                        item.GetComponent<BathymetryContactSurface>() == null) item.gameObject.AddComponent<BathymetryContactSurface>();
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                Measure();
                Debug.Log("CONTACT_BUILD|PASS"); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
