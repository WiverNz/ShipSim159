using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShipSimulator.Editor
{
    // Shader.Find references are invisible to build dependency collection.
    public sealed class RuntimeShaderBuildAssets : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Ensure();

        public static void Ensure()
        {
            const string folder = "Assets/ShipSimulator/Resources/RuntimeShaders";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/ShipSimulator/Resources", "RuntimeShaders");
            Keep(folder + "/NavigationUnlit.mat", "Universal Render Pipeline/Unlit", false);
            Keep(folder + "/RainUnlit.mat", "Universal Render Pipeline/Particles/Unlit", true);
            AssetDatabase.SaveAssets();
        }

        private static void Keep(string path, string shaderName, bool transparent)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Missing runtime shader " + shaderName);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            if (transparent)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.renderQueue = 3000;
            }
            EditorUtility.SetDirty(material);
        }
    }
}
