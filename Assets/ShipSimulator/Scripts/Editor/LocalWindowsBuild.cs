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
        public static void Run() => Build("build/StandaloneWindows64/ShipSim159.exe", "Logs/Part17");

        public static void RunBowReview() => Build("build/BowReview/ShipSim159.exe", "Logs/BowWater/Build");

        public static void RunHudReview() => Build("build/HudReview/ShipSim159.exe", "Logs/HudReview");

        public static void RunCameraReview() => Build("build/CameraReview/ShipSim159.exe", "Logs/Camera");

        public static void RunRelease() => Build("build/StandaloneWindows64/ShipSim159.exe", "Logs/Release", BuildOptions.None);

        public static void ValidateResult(BuildResult result, int errors)
        {
            if (result != BuildResult.Succeeded || errors != 0)
                throw new InvalidOperationException($"Windows build rejected: {result}, {errors} errors.");
        }

        private static void Build(string executable, string reports, BuildOptions options = BuildOptions.Development)
        {
            bool timing = PlayerSettings.enableFrameTimingStats;
            int exitCode = 0;
            try
            {
                string required = File.ReadAllLines("ProjectSettings/ProjectVersion.txt")
                    .First(line => line.StartsWith("m_EditorVersion: ")).Substring("m_EditorVersion: ".Length).Trim();
                if (Application.unityVersion != required) throw new InvalidOperationException("Use Unity " + required);
                string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
                if (scenes.Length == 0 || scenes.Any(scene => !File.Exists(scene))) throw new InvalidOperationException("Invalid enabled scene list");
                Directory.CreateDirectory(Path.GetDirectoryName(executable));
                PlayerSettings.enableFrameTimingStats = options.HasFlag(BuildOptions.Development) || timing;
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, locationPathName = executable,
                    target = BuildTarget.StandaloneWindows64, options = options
                });
                Directory.CreateDirectory(reports);
                File.WriteAllText(Path.Combine(reports, "build-report.txt"), $"Unity {Application.unityVersion}\nResult {report.summary.result}\nErrors {report.summary.totalErrors}\nWarnings {report.summary.totalWarnings}\nBytes {report.summary.totalSize}\nDuration {report.summary.totalTime}\nOptions {options}, frame timing {PlayerSettings.enableFrameTimingStats}\n" + string.Join("\n", scenes));
                ValidateResult(report.summary.result, report.summary.totalErrors);
                Debug.Log("LOCAL_WINDOWS_BUILD|PASS");
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (!Application.isBatchMode) throw;
                exitCode = 1;
            }
            finally { PlayerSettings.enableFrameTimingStats = timing; }
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }
    }
}
