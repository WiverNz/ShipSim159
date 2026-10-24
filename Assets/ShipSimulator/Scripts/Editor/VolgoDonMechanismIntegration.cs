using System;
using System.IO;
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
            string[] sides = { "Port", "Starboard", "Stern" };
            Transform[] gypsies = { Find(visual, "Windlass_Port"), Find(visual, "Windlass_Starboard") };
            rig.ConfigureAnchors(sides.Select(s => Find(visual, "Anchor_" + s)).ToArray(),
                sides.Select(s => Find(visual, "ChainAttachment_" + s)).ToArray(), gypsies,
                gypsies.Select(t => t.parent.InverseTransformDirection(root.transform.right)).ToArray(),
                ChainMaterial(), Rates());
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

        [Serializable] private sealed class Value { public float value; }
        [Serializable] private sealed class Phases { public Value outer_lift, inner_rolling_lift, inner_slide_under, outer_lower_onto_stack, stack_traverse; }
        [Serializable] private sealed class HatchRates { public Phases phases_s; }
        [Serializable] private sealed class AnchorRates
        {
            public Value bow_chain_limit_m, stern_chain_limit_m, bow_heave_m_per_min, stern_heave_m_per_min, let_go_mps, chain_per_gypsy_turn_m;
        }
        [Serializable] private sealed class OperatingRates { public HatchRates hatch; public AnchorRates anchor; }
        [Serializable] private sealed class Manifest { public OperatingRates operating_rates; }

        // mechanisms.json is the single source of the runtime rates; its operating_rates block records sources.
        public static MachineryRates Rates()
        {
            OperatingRates r = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/mechanisms.json")).operating_rates;
            if (r?.hatch?.phases_s == null || r.anchor == null) throw new InvalidDataException("mechanisms.json has no operating_rates.");
            return new MachineryRates
            {
                outerLiftSeconds = r.hatch.phases_s.outer_lift.value,
                innerLiftSeconds = r.hatch.phases_s.inner_rolling_lift.value,
                innerSlideSeconds = r.hatch.phases_s.inner_slide_under.value,
                outerLowerSeconds = r.hatch.phases_s.outer_lower_onto_stack.value,
                traverseSeconds = r.hatch.phases_s.stack_traverse.value,
                letGoMetresPerSecond = r.anchor.let_go_mps.value,
                bowHeaveMetresPerSecond = r.anchor.bow_heave_m_per_min.value / 60f,
                sternHeaveMetresPerSecond = r.anchor.stern_heave_m_per_min.value / 60f,
                bowChainLimitM = r.anchor.bow_chain_limit_m.value,
                sternChainLimitM = r.anchor.stern_chain_limit_m.value,
                chainPerGypsyTurnM = r.anchor.chain_per_gypsy_turn_m.value
            };
        }

        // Generated stud-less chain texture, two links per tile, alpha-clipped on a line renderer.
        private static Material ChainMaterial()
        {
            const string texturePath = Folder + "/Textures/AnchorChain.png";
            const string materialPath = Folder + "/materials/AnchorChain.mat";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
            {
                const int width = 256, height = 64;
                var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        // Face-on oval ring in the first half, edge-on link in the second.
                        float u = (x % 128) / 128f * 2 - 1, v = y / (float)(height - 1) * 2 - 1;
                        bool faceOn = x < 128;
                        float ring = faceOn ? Mathf.Abs(Mathf.Sqrt(u * u / 1.0f + v * v / 0.45f) - 0.72f) : Mathf.Abs(v);
                        bool steel = faceOn ? ring < 0.2f : ring < 0.22f && Mathf.Abs(u) < 0.98f;
                        float shade = faceOn ? 0.55f + 0.35f * (1 - ring / 0.2f) : 0.5f + 0.4f * (1 - Mathf.Abs(v) / 0.22f);
                        image.SetPixel(x, y, new Color(shade * .42f, shade * .4f, shade * .38f, steel ? 1 : 0));
                    }
                File.WriteAllBytes(texturePath, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(texturePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.alphaIsTransparency = true;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "AnchorChain" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Metallic", 0.6f);
            material.SetFloat("_Smoothness", 0.35f);
            material.SetFloat("_Cull", 0);
            EditorUtility.SetDirty(material);
            return material;
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
