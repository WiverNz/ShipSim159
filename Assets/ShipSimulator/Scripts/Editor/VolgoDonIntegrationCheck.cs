using System;
using System.IO;
using System.Linq;
using ShipSimulator.Physics;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Editor
{
    public static class VolgoDonIntegrationCheck
    {
        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                string path = VolgoDonMechanismIntegration.PrefabPath;
                Directory.CreateDirectory("Logs/VolgoDon507B");
                if (!File.Exists("Logs/VolgoDon507B/before-part5.prefab"))
                    File.Copy(path, "Logs/VolgoDon507B/before-part5.prefab");
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    string physics = EditorJsonUtility.ToJson(root.GetComponent<ShipPhysicsController>());
                    string body = EditorJsonUtility.ToJson(root.GetComponent<Rigidbody>());
                    Collider[] collisions = root.GetComponentsInChildren<Collider>();
                    string[] colliders = collisions.Select(EditorJsonUtility.ToJson).ToArray();
                    for (int i = 0; i < 2; i++)
                    {
                        if (!VolgoDonModelIntegrator.AddDetailedVisual(root)) throw new Exception("Integration failed.");
                        if (root.transform.Cast<Transform>().Count(t => t.name == "DetailedVisual") != 1 ||
                            root.GetComponents<VesselMechanisms>().Length != 1)
                            throw new Exception("Duplicate visual or mechanism rig.");
                        if (physics != EditorJsonUtility.ToJson(root.GetComponent<ShipPhysicsController>()) ||
                            body != EditorJsonUtility.ToJson(root.GetComponent<Rigidbody>()))
                            throw new Exception("Integration changed the physics root.");
                        for (int c = 0; c < collisions.Length; c++)
                            if (collisions[c] == null || colliders[c] != EditorJsonUtility.ToJson(collisions[c]))
                                throw new Exception("Integration changed collision geometry.");
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                AssetDatabase.SaveAssets();
                VolgoDonModelIntegrator.RenderPreview();
                Debug.Log("VOLGODON_INTEGRATION|PASS: repeated installation, physics and colliders preserved");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
