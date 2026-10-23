using System;
using System.Linq;
using ShipSimulator.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    public static class VolgoDonMechanismIntegration
    {
        public const string Folder = "Assets/ShipSimulator/Models/VolgoDon507B_Improved";
        public const string PrefabPath = "Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab";

        public static bool Install(GameObject root)
        {
            var candidate = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/VolgoDon507B_VisualCandidate.prefab");
            if (candidate == null) return false;
            AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(Folder + "/VolgoDon507B.fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            AnimationClip hatch = FilterClip(source, "Hatches", name => name.StartsWith("Hatch_") ||
                name.StartsWith("JackLift_") || name.StartsWith("JackRods_"));
            AnimationClip anchor = FilterClip(source, "Anchors", name => name.StartsWith("Anchor_") || name.StartsWith("Windlass_"));
            Transform previous = root.transform.Find("DetailedVisual");
            if (previous != null) Object.DestroyImmediate(previous.gameObject);
            GameObject visual = Object.Instantiate(candidate, root.transform, false);
            visual.name = "DetailedVisual";
            foreach (Animator animator in visual.GetComponentsInChildren<Animator>()) Object.DestroyImmediate(animator);
            foreach (Animation animation in visual.GetComponentsInChildren<Animation>()) Object.DestroyImmediate(animation);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            Transform[] shafts = { Find(visual, "Propeller_Port"), Find(visual, "Propeller_Starboard") };
            Transform[] ducts = { Find(visual, "Nozzle_Port"), Find(visual, "Nozzle_Starboard") };
            // Counter-rotation follows the mirrored authored blade pitch; historical handedness is estimated.
            Vector3[] shaftAxes = { shafts[0].parent.InverseTransformDirection(-root.transform.forward),
                shafts[1].parent.InverseTransformDirection(root.transform.forward) };
            Vector3[] steeringAxes = ducts.Select(t => t.parent.InverseTransformDirection(root.transform.up)).ToArray();
            var rig = root.GetComponent<VesselMechanisms>() ?? root.AddComponent<VesselMechanisms>();
            rig.Configure(visual, hatch, anchor, shafts, ducts, shaftAxes, steeringAxes);
            ConfigureLayout(root);
            AddAudioPoints(visual);
            VolgoDonLodIntegration.Install(visual);
            VolgoDonHoldIntegration.Install(root);
            return true;
        }

        private static AnimationClip FilterClip(AnimationClip source, string name, Func<string, bool> include)
        {
            string path = Folder + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip { name = name };
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            clip.legacy = true;
            clip.frameRate = 24;
            int count = 0;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                string leaf = binding.path.Split('/').Last();
                if (binding.type != typeof(Transform) || !include(leaf)) continue;
                AnimationUtility.SetEditorCurve(clip, binding, AnimationUtility.GetEditorCurve(source, binding));
                count++;
            }
            if (count == 0) throw new InvalidOperationException("Missing machinery curves: " + name);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static Transform Find(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>().Single(t => t.name == name);

        private static void ConfigureLayout(GameObject root)
        {
            var layout = root.GetComponent<VesselLayout>() ?? root.AddComponent<VesselLayout>();
            layout.Configure("volgodon-507b", 1, new Vector3(-1.8f, 9.1f, -43.3f),
                new Vector3(-1.8f, 9.1f, 55), new Vector3(-5.5f, 10.7f, -43.6f), 10.4f,
                new Vector3(0, 9.3f, 55), 9, new Vector3(0, 12.4f, -49), 12.1f,
                new Vector3(0, 5.4f, -68.5f), 5.1f);
            layout.ConfigureNavigationSpill(.08f, .12f);
        }

        private static void AddAudioPoints(GameObject visual)
        {
            var group = new GameObject("AudioPoints");
            group.transform.SetParent(visual.transform, false);
            string[] names = { "Engine_Port", "Engine_Starboard", "Water_Bow", "Propeller_Port_Audio",
                "Propeller_Starboard_Audio", "HatchMachinery_Aft", "HatchMachinery_Fore", "Windlass_Audio" };
            Vector3[] points = { new(-3, -1, -54), new(3, -1, -54), new(0, 0, 65),
                new(-3.6f, -2.18f, -64.65f), new(3.6f, -2.18f, -64.65f),
                new(0, 3, -29), new(0, 3, 43), new(0, 4, 57.5f) };
            Transform vessel = visual.transform.parent;
            for (int i = 0; i < names.Length; i++)
            {
                var point = new GameObject(names[i]);
                point.transform.SetParent(group.transform, false);
                point.transform.position = vessel.TransformPoint(points[i]);
            }
        }
    }
}
