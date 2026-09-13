using ShipSimulator.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Editor
{
    public static class GraphicsPhaseOneBuilder
    {
        [MenuItem("Ship Simulator/Apply Graphics Phase One")]
        public static void ApplyBoth()
        {
            ConfigureRenderer();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/ShipSimulator/Settings/NaturalLandscape" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                var group = prefab.GetComponent<LODGroup>();
                if (group != null)
                {
                    group.fadeMode = LODFadeMode.CrossFade;
                    group.animateCrossFading = true;
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                PrefabUtility.UnloadPrefabContents(prefab);
            }
            foreach (string name in new[] { "RiverTrainingScene", "GorodetsTrainingScene" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity");
                ShipSimulatorVisualUpgrade.ConfigurePostProcessing(scene);
                RiverWaterAndSkyBuilder.Apply(scene);
                EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("GRAPHICS_PHASE_ONE|Scenes and renderer configured");
        }

        public static void ConfigureRenderer()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (renderer == null) return;
            RiverExposureFeature exposure = null;
            RiverTemporalFeature temporal = null;
            foreach (var feature in renderer.rendererFeatures)
            {
                if (feature is RiverExposureFeature e) exposure = e;
                if (feature is RiverTemporalFeature t) temporal = t;
            }
            if (exposure == null)
            {
                exposure = ScriptableObject.CreateInstance<RiverExposureFeature>();
                exposure.name = "River histogram exposure";
                AssetDatabase.AddObjectToAsset(exposure, renderer);
                renderer.rendererFeatures.Add(exposure);
            }
            exposure.meterShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/ShipSimulator/Shaders/RiverExposure.compute");
            EditorUtility.SetDirty(exposure);
            if (temporal == null)
            {
                temporal = ScriptableObject.CreateInstance<RiverTemporalFeature>();
                temporal.name = "River animated water motion";
                AssetDatabase.AddObjectToAsset(temporal, renderer);
                renderer.rendererFeatures.Add(temporal);
            }
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            int previousQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.names[i] != "PC") continue;
                QualitySettings.SetQualityLevel(i);
                QualitySettings.realtimeReflectionProbes = true;
            }
            QualitySettings.SetQualityLevel(previousQuality);
            renderer.SetDirty();
            EditorUtility.SetDirty(renderer);
        }
    }
}
