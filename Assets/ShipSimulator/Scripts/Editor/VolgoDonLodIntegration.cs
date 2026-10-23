using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShipSimulator.Editor
{
    public static class VolgoDonLodIntegration
    {
        public static void Install(GameObject visual)
        {
            MeshRenderer[] original = visual.GetComponentsInChildren<MeshRenderer>();
            var levels = new List<LOD> { new LOD(.55f, original) };
            for (int level = 1; level <= 2; level++)
            {
                string path = VolgoDonMechanismIntegration.Folder + $"/VolgoDon507B_LOD{level}.fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Missing derived LOD: " + path);
                if (!importer.bakeAxisConversion || importer.importAnimation || !importer.isReadable)
                {
                    importer.bakeAxisConversion = true;
                    importer.importAnimation = false;
                    importer.importCameras = importer.importLights = importer.addCollider = false;
                    importer.isReadable = true;
                    importer.meshCompression = ModelImporterMeshCompression.Off;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.SaveAndReimport();
                }
                var meshes = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MeshFilter>();
                var renderers = new List<Renderer>();
                string prefix = $"LOD{level}__";
                foreach (MeshFilter source in meshes)
                {
                    string name = source.name.Substring(prefix.Length);
                    MeshRenderer attachment = original.Single(r => r.name == name);
                    Mesh reference = attachment.GetComponent<MeshFilter>().sharedMesh;
                    if (Vector3.Distance(reference.bounds.center, source.sharedMesh.bounds.center) > .1f ||
                        Vector3.Distance(reference.bounds.size, source.sharedMesh.bounds.size) > .25f)
                        throw new InvalidOperationException("LOD local geometry frame changed: " + source.name);
                    var child = new GameObject($"DistanceLOD{level}", typeof(MeshFilter), typeof(MeshRenderer));
                    child.transform.SetParent(attachment.transform, false);
                    child.GetComponent<MeshFilter>().sharedMesh = source.sharedMesh;
                    MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = attachment.sharedMaterials;
                    renderer.shadowCastingMode = attachment.shadowCastingMode;
                    renderer.receiveShadows = attachment.receiveShadows;
                    renderers.Add(renderer);
                }
                levels.Add(new LOD(level == 1 ? .20f : .003f, renderers.ToArray()));
            }
            LODGroup group = visual.AddComponent<LODGroup>();
            // Distance-band dither works without the time-based GPU resident drawer limitation.
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = false;
            for (int i = 0; i < levels.Count; i++)
            {
                LOD level = levels[i]; level.fadeTransitionWidth = .10f; levels[i] = level;
            }
            group.SetLODs(levels.ToArray());
            group.RecalculateBounds();
        }
    }
}
