using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ShipSimulator.Editor
{
    // Reviews a separate visual candidate without touching the playable vessel.
    public static class VolgoDonCandidateReview
    {
        private const string Folder = "Assets/ShipSimulator/Models/VolgoDon507B_Improved";
        private const string Model = Folder + "/VolgoDon507B.fbx";
        private const string Output = "Art/Source/VolgoDon507B/Previews";

        [Serializable] private sealed class MaterialList { public MaterialSpec[] materials; public int stage; public int triangles; public int meshCount; }
        [Serializable] private sealed class MechanismList { public MechanismSpec[] mechanisms; }
        [Serializable] private sealed class MechanismSpec { public string name; public float[] unity_rest_position; }
        [Serializable] private sealed class MaterialSpec
        {
            public string name;
            public float[] color;
            public float metallic;
            public float smoothness;
            public string texture;
            public string normal;
            public string metallicSmoothness;
            public bool transparent;
        }

        public static void Run()
        {
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Review();
                Debug.Log("VOLGODON_CANDIDATE|PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        public static void Review(bool capture = true)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var specs = JsonUtility.FromJson<MaterialList>(File.ReadAllText(Folder + "/materials.json"));
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            if (importer == null) throw new InvalidOperationException("Missing candidate FBX.");
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = specs.stage >= 4;
            if (specs.stage >= 4)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.optimizeGameObjects = false;
            }
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.isReadable = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.SaveAndReimport();

            if (!AssetDatabase.IsValidFolder(Folder + "/materials"))
                AssetDatabase.CreateFolder(Folder, "materials");
            var materials = new Dictionary<string, Material>();
            foreach (MaterialSpec spec in specs.materials)
            {
                string path = Folder + "/materials/" + spec.name + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.name = spec.name;
                mat.SetFloat("_Metallic", spec.metallic);
                mat.SetFloat("_Smoothness", spec.smoothness);
                Color color = new Color(spec.color[0], spec.color[1], spec.color[2], spec.color[3]);
                if (!string.IsNullOrEmpty(spec.texture))
                {
                    string texturePath = Folder + "/Textures/" + spec.texture;
                    var ti = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                    if (ti == null) throw new InvalidOperationException("Missing texture: " + texturePath);
                    ti.sRGBTexture = true;
                    ti.alphaSource = TextureImporterAlphaSource.None;
                    ti.mipmapEnabled = true;
                    ti.wrapMode = TextureWrapMode.Repeat;
                    ti.SaveAndReimport();
                    Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                    mat.SetTexture("_BaseMap", texture);
                    color = Color.white;
                    if (spec.name == "VD507B_lights")
                    {
                        mat.SetTexture("_EmissionMap", texture);
                        mat.SetColor("_EmissionColor", Color.white * .5f);
                        mat.EnableKeyword("_EMISSION");
                    }
                }
                mat.SetColor("_BaseColor", color);
                if (!string.IsNullOrEmpty(spec.normal))
                {
                    mat.SetTexture("_BumpMap", ImportDataTexture(spec.normal, true));
                    mat.SetFloat("_BumpScale", .5f);
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetTexture("_MetallicGlossMap", ImportDataTexture(spec.metallicSmoothness, false));
                    ImportDataTexture(spec.name + "_Roughness.png", false);
                    mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                    mat.SetFloat("_SmoothnessTextureChannel", 0);
                }
                mat.SetFloat("_Surface", spec.transparent ? 1 : 0);
                mat.SetFloat("_SrcBlend", (float)(spec.transparent ? BlendMode.SrcAlpha : BlendMode.One));
                mat.SetFloat("_DstBlend", (float)(spec.transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
                mat.SetFloat("_ZWrite", spec.transparent ? 0 : 1);
                mat.SetOverrideTag("RenderType", spec.transparent ? "Transparent" : "Opaque");
                mat.renderQueue = spec.transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
                if (spec.transparent) mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                else mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetShaderPassEnabled("ShadowCaster", !spec.transparent);
                EditorUtility.SetDirty(mat);
                materials.Add(spec.name, mat);
            }

            var vessel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            vessel.name = "VolgoDon507B_VisualCandidate";
            try
            {
                Vector3 forward = Point(vessel, "BowReference") - Point(vessel, "Waterline");
                Vector3 up = Point(vessel, "UpReference") - Point(vessel, "Waterline");
                vessel.transform.rotation = Quaternion.Inverse(Quaternion.LookRotation(forward, up));
                vessel.transform.position = -Point(vessel, "Waterline");
                Require(Vector3.Distance(Point(vessel, "BowReference"), new Vector3(0, 0, 69.15f)) < .01f,
                    "Bow axis or scale mismatch.");
                Require(Vector3.Distance(Point(vessel, "StarboardReference"), new Vector3(8.35f, 0, 0)) < .01f,
                    "Starboard axis mismatch.");
                Require(Vector3.Distance(Point(vessel, "KeelReference"), new Vector3(0, -3.53f, 0)) < .01f,
                    "Keel datum mismatch.");
                Renderer[] renderers = vessel.GetComponentsInChildren<Renderer>();
                Require(renderers.Length == (specs.stage >= 4 ? specs.meshCount : 18), "Unexpected mesh count.");
                Bounds bounds = renderers[0].bounds;
                int triangles = 0;
                foreach (Renderer renderer in renderers)
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => materials[m.name.Split('.')[0]]).ToArray();
                    Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Require(mesh.uv.Length == mesh.vertexCount && mesh.uv2.Length == mesh.vertexCount,
                        "Missing corrected or preserved UV channel: " + renderer.name);
                    for (int i = 0; i < mesh.vertexCount; i++)
                    {
                        Require(Mathf.Abs(mesh.uv[i].x - mesh.uv2[i].x) < .0001f &&
                            Mathf.Abs(mesh.uv[i].y + mesh.uv2[i].y - 1) < .0001f,
                            "UV flip regression: " + renderer.name);
                    }
                    triangles += mesh.triangles.Length / 3;
                    bounds.Encapsulate(renderer.bounds);
                    Require(mesh.normals.Length == mesh.vertexCount, "Missing normals.");
                }
                Require(triangles == (specs.stage >= 3 ? specs.triangles : 42299), "Triangle count changed unexpectedly.");
                Require(Mathf.Abs(bounds.size.z - 138.3f) < .03f && Mathf.Abs(bounds.size.x - 16.7f) < .03f,
                    "Candidate bounds mismatch: " + bounds);
                Renderer hull = renderers.Single(r => r.name.Split('.')[0] == "VD507B_hull");
                Require(Mathf.Abs(hull.bounds.min.y + 3.53f) < .01f, "Hull keel/waterline mismatch.");
                PrefabUtility.SaveAsPrefabAsset(vessel, Folder + "/VolgoDon507B_VisualCandidate.prefab");
                AssetDatabase.SaveAssets();
                if (specs.stage >= 3)
                {
                    Require(materials.Values.Count(m => m.IsKeywordEnabled("_NORMALMAP")) == 8,
                        "Missing PBR normal maps.");
                    Require(materials["VD507B_Glass"].renderQueue == (int)RenderQueue.Transparent,
                        "Glazing must use the transparent pass.");
                }
                if (capture) Render(vessel, specs.stage >= 3 ? specs.stage : 2);
                if (specs.stage >= 4) ReviewMechanisms(vessel, capture);
                Directory.CreateDirectory("Logs/VolgoDon507B");
                File.WriteAllText("Logs/VolgoDon507B/candidate-review.txt",
                    $"PASS\nMeshes: {renderers.Length}\nTriangles: {triangles}\nBounds: {bounds}\n" +
                    $"Hull keel: {hull.bounds.min.y:F4}\nUV flip and retained UVs: PASS\n" +
                    "Playable prefab untouched; this is a visual-only candidate.\n");
            }
            finally { Object.DestroyImmediate(vessel); }
        }

        private static Texture ImportDataTexture(string name, bool normal)
        {
            string path = Folder + "/Textures/" + name;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) throw new InvalidOperationException("Missing PBR map: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            Require(!importer.sRGBTexture, "PBR data texture imported as colour.");
            return AssetDatabase.LoadAssetAtPath<Texture>(path);
        }

        private static void ReviewMechanisms(GameObject vessel, bool capture)
        {
            var manifest = JsonUtility.FromJson<MechanismList>(File.ReadAllText(Folder + "/mechanisms.json"));
            var transforms = vessel.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
            foreach (MechanismSpec spec in manifest.mechanisms)
            {
                Require(transforms.ContainsKey(spec.name), "Missing mechanism: " + spec.name);
                Transform pivot = transforms[spec.name];
                Require(pivot.parent.name == "Mechanisms", "Mechanism parent changed: " + spec.name);
                var p = spec.unity_rest_position;
                Require(Vector3.Distance(pivot.position, new Vector3(p[0], p[1], p[2])) < .015f,
                    "Pivot mismatch: " + spec.name + " at " + pivot.position);
            }
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            Require(clip != null && clip.length > 31, "Missing complete mechanism review clip.");
            var rest = transforms.ToDictionary(kv => kv.Key, kv => kv.Value.position);
            var meshTransforms = vessel.GetComponentsInChildren<MeshFilter>().Select(m => m.transform).ToArray();
            var meshPositions = meshTransforms.ToDictionary(t => t, t => t.localPosition);
            var meshRotations = meshTransforms.ToDictionary(t => t, t => t.localRotation);
            Quaternion nozzleRest = transforms["Nozzle_Port"].rotation;
            Quaternion hatchRest = transforms["Hatch_AftOuter"].rotation;
            Vector3 rootPosition = vessel.transform.position;
            Quaternion rootRotation = vessel.transform.rotation;
            var camera = new GameObject("Mechanism review camera").AddComponent<Camera>();
            var sun = new GameObject("Mechanism review light").AddComponent<Light>();
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.23f, .28f, .32f);
                camera.orthographic = true;
                camera.farClipPlane = 1000;
                sun.type = LightType.Directional;
                sun.intensity = 1.1f;
                int[] frames = { 30, 60, 100, 150, 210, 300, 480, 740, 770 };
                string[] names = { "nozzles-port", "nozzles-starboard", "lift", "slide-under",
                    "both-half-open", "fore-open", "aft-open", "anchors-unseated", "closed-return" };
                for (int i = 0; i < frames.Length; i++)
                {
                    int frame = frames[i];
                    clip.SampleAnimation(vessel, (frame - 1) / 24f);
                    foreach (Transform mesh in meshTransforms)
                    {
                        Require(Vector3.Distance(mesh.localPosition, meshPositions[mesh]) < .001f &&
                            Quaternion.Angle(mesh.localRotation, meshRotations[mesh]) < .02f,
                            "Animation changed mesh attachment: " + mesh.name);
                    }
                    Require(Vector3.Distance(vessel.transform.position, rootPosition) < .001f &&
                        Quaternion.Angle(vessel.transform.rotation, rootRotation) < .01f,
                        "Review animation moved the vessel root.");
                    Require(Quaternion.Angle(transforms["Hatch_AftOuter"].rotation, hatchRest) < .01f,
                        "Hatch sequence must translate, not hinge.");
                    if (frame == 30 || frame == 60)
                        Require(Mathf.Abs(Quaternion.Angle(nozzleRest, transforms["Nozzle_Port"].rotation) - 35) < .1f,
                            "Nozzle limit did not survive animation import.");
                    if (frame == 100)
                        Require(Mathf.Abs(transforms["Hatch_AftOuter"].position.y - rest["Hatch_AftOuter"].y - 1.05f) < .015f,
                            "Hatch lift did not survive import.");
                    if (frame == 300)
                        Require(Mathf.Abs(transforms["Hatch_ForeInner"].position.z + 6.1f) < .015f,
                            "Forward hold opening travel mismatch.");
                    if (frame == 480)
                        Require(Mathf.Abs(transforms["Hatch_AftInner"].position.z - 18.95f) < .015f,
                            "After hold opening travel mismatch.");
                    if (frame == 770)
                        foreach (MechanismSpec spec in manifest.mechanisms)
                            Require(Vector3.Distance(transforms[spec.name].position, rest[spec.name]) < .015f,
                                "Mechanism did not return to rest: " + spec.name);
                    bool nozzle = frame <= 60;
                    bool anchor = frame == 740;
                    camera.transform.position = nozzle ? new Vector3(9, -1, -74) :
                        anchor ? new Vector3(12, 6, 80) : new Vector3(85, 95, 25);
                    camera.transform.LookAt(nozzle ? new Vector3(0, -2, -64.65f) :
                        anchor ? new Vector3(0, 1.5f, 65) : new Vector3(0, 0, 5));
                    camera.orthographicSize = nozzle ? 4 : anchor ? 4 : 43;
                    sun.transform.rotation = Quaternion.Euler(40, camera.transform.eulerAngles.y - 25, 0);
                    if (capture) LandscapePreview.Render(camera, Output + "/part4-unity-" + names[i] + ".png");
                }
                Debug.Log($"VOLGODON_MECHANISMS|PASS|bindings={manifest.mechanisms.Length}|clip={clip.name}|duration={clip.length}");
            }
            finally
            {
                clip.SampleAnimation(vessel, 0);
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(sun.gameObject);
            }
        }

        private static void Render(GameObject vessel, int stage)
        {
            Directory.CreateDirectory(Output);
            var camera = new GameObject("Candidate review camera").AddComponent<Camera>();
            var sun = new GameObject("Candidate review sun").AddComponent<Light>();
            try
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.23f, .28f, .32f);
                camera.orthographic = true;
                camera.farClipPlane = 1000;
                sun.type = LightType.Directional;
                sun.intensity = 1.5f;
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.6f, .6f, .6f);
                string[] names = { "side", "front", "top", "perspective", "superstructure", "propulsion" };
                Vector3[] positions = { new(180, 8, 0), new(0, 8, 180), new(0, 180, 0),
                    new(110, 80, 114), new(30, 17, -20), new(9, -1, -74) };
                for (int i = 0; i < names.Length; i++)
                {
                    camera.transform.position = positions[i];
                    camera.transform.LookAt(i == 5 ? new Vector3(0, -2, -64.65f) :
                        i == 4 ? new Vector3(0, 6, -50) : new Vector3(0, 3, 0));
                    camera.orthographicSize = i == 5 ? 4 : i == 1 ? 10 : i == 4 ? 12 : i == 2 ? 78 : 44;
                    sun.transform.rotation = Quaternion.Euler(35, camera.transform.eulerAngles.y - 25, 0);
                    LandscapePreview.Render(camera, Output + $"/part{stage}-unity-" + names[i] + ".png");
                }
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(sun.gameObject);
            }
        }

        private static Vector3 Point(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>().Single(t => t.name == name).position;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
