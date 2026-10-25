using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShipSimulator.Editor
{
    public static class LocalWindowsBuild
    {
        [MenuItem("Ship Simulator/Build Local Windows Player")]
        public static void Run()
        {
            bool timing = PlayerSettings.enableFrameTimingStats;
            try
            {
                string required = File.ReadAllLines("ProjectSettings/ProjectVersion.txt")
                    .First(line => line.StartsWith("m_EditorVersion: ")).Substring("m_EditorVersion: ".Length).Trim();
                if (Application.unityVersion != required) throw new InvalidOperationException("Use Unity " + required);
                string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
                if (scenes.Length == 0 || scenes.Any(scene => !File.Exists(scene))) throw new InvalidOperationException("Invalid enabled scene list");
                Directory.CreateDirectory("build/StandaloneWindows64");
                PlayerSettings.enableFrameTimingStats = true;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, locationPathName = "build/StandaloneWindows64/ShipSim159.exe",
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                Directory.CreateDirectory("Logs/Part17");
                File.WriteAllText("Logs/Part17/build-report.txt", $"Unity {Application.unityVersion}\nResult {report.summary.result}\nErrors {report.summary.totalErrors}\nWarnings {report.summary.totalWarnings}\nBytes {report.summary.totalSize}\nDuration {report.summary.totalTime}\nDevelopment, no script debugging, frame timing enabled\n" + string.Join("\n", scenes));
                if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                    throw new InvalidOperationException("Local Windows build failed: " + report.summary.result);
                Debug.Log("LOCAL_WINDOWS_BUILD|PASS");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
            finally { PlayerSettings.enableFrameTimingStats = timing; }
        }
    }
}
