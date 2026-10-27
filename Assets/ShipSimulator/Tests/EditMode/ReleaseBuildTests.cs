using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShipSimulator.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public sealed class ReleaseBuildTests
    {
        [TestCase(BuildResult.Succeeded, 0, true)]
        [TestCase(BuildResult.Succeeded, 3, false)]
        [TestCase(BuildResult.Failed, 0, false)]
        [TestCase(BuildResult.Cancelled, 0, false)]
        [TestCase(BuildResult.Unknown, 0, false)]
        public void ReleaseRejectsLoggedErrorsEvenWhenUnityReportsSuccess(BuildResult result, int errors, bool accepted)
        {
            if (accepted) Assert.DoesNotThrow(() => LocalWindowsBuild.ValidateResult(result, errors));
            else Assert.Throws<InvalidOperationException>(() => LocalWindowsBuild.ValidateResult(result, errors));
        }

        [Test]
        public void McpDependencyIsPinnedAndItsBatchTestHasMetadata()
        {
            string manifest = File.ReadAllText("Packages/manifest.json");
            Assert.That(Regex.IsMatch(manifest,
                "\"com.gamelovers.mcp-unity\"\\s*:\\s*\"https://github.com/CoderGamester/mcp-unity.git#[a-f0-9]{40}\""), Is.True);
            var package = PackageInfo.FindForAssetPath("Packages/com.gamelovers.mcp-unity/package.json");
            Assert.That(package, Is.Not.Null);
            Assert.That(File.Exists(Path.Combine(package.resolvedPath, "Editor/Tests/McpUnityServerBatchModeTests.cs.meta")), Is.True,
                "An immutable UPM package cannot generate missing metadata during import.");
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
        [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)] [TestCase(12)]
        public void HullSectionsRequestConvexPrebaking(int section)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                $"Assets/ShipSimulator/Models/VolgoDon507B_Improved/Contact/Section{section:00}.asset");
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.HasPreBakeCollisionMesh(true), Is.True);
        }

        [TestCase("Left")] [TestCase("Right")]
        public void RiverBanksRequestTrianglePrebaking(string side)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                $"Assets/ShipSimulator/Settings/NaturalLandscape/RiverTrainingScene{side}Bank.asset");
            Assert.That(mesh, Is.Not.Null);
            Assert.That(mesh.HasPreBakeCollisionMesh(false), Is.True);
        }
    }
}
