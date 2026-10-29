using ShipSimulator.Physics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Editor
{
    public static class BuoyContactBuilder
    {
        [MenuItem("Ship Simulator/Install Buoy Physics")]
        public static void Install()
        {
            if (Application.isPlaying || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (Resources.Load<PhysicsMaterial>("BuoyContact") == null)
            {
                var material = new PhysicsMaterial("Wet buoy steel") { dynamicFriction = 0.12f, staticFriction = 0.12f,
                    bounciness = 0, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
                AssetDatabase.CreateAsset(material, "Assets/ShipSimulator/Resources/BuoyContact.physicMaterial");
            }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string name in new[] { "RiverBuoy", "RiverBuoyLeft" })
                {
                    string path = "Assets/ShipSimulator/Prefabs/Navigation/" + name + ".prefab";
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) continue;
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try { Add(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                foreach (string name in new[] { "RiverTrainingScene", "GorodetsTrainingScene", "SerpukhovZatonScene" })
                {
                    var scene = EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity");
                    foreach (GameObject root in scene.GetRootGameObjects())
                        if (root.name == "Navigation")
                            foreach (Transform marker in root.transform)
                                if (marker.name.Contains("Buoy")) Add(marker.gameObject);
                    EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("BUOY_CONTACT_INSTALL|PASS");
            }
            finally
            {
                if (setup.Length > 0 && !Application.isBatchMode) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void Add(GameObject root)
        {
            var buoy = root.GetComponent<MooredBuoy>();
            if (buoy == null) buoy = root.AddComponent<MooredBuoy>();
            buoy.Initialize();
            EditorUtility.SetDirty(root);
        }
    }
}
