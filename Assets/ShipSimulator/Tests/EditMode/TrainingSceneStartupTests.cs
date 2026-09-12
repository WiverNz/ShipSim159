using NUnit.Framework;
using ShipSimulator.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Tests
{
    public class TrainingSceneStartupTests
    {
        [Test]
        public void EmptyScene_PlayLaunchesTrainingWithoutDiscardingUnsavedWork()
        {
            SceneAsset previous = EditorSceneManager.playModeStartScene;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var work = new GameObject("Unsaved work");
            EditorSceneManager.MarkSceneDirty(scene);
            try
            {
                TrainingSceneStartup.ConfigurePlayScene();
                Assert.That(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
                    Is.EqualTo(TrainingSceneStartup.ScenePath));
                Assert.That(scene.isLoaded, Is.True);
                Assert.That(scene.isDirty, Is.True);
                Assert.That(work, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(work);
                EditorSceneManager.playModeStartScene = previous;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [TestCase("RiverTrainingScene")]
        [TestCase("GorodetsTrainingScene")]
        public void SelectedVoyage_PlaysTheSelectedScenario(string name)
        {
            SceneAsset previous = EditorSceneManager.playModeStartScene;
            try
            {
                EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/" + name + ".unity");
                TrainingSceneStartup.ConfigurePlayScene();
                Assert.That(EditorSceneManager.playModeStartScene, Is.Null);
            }
            finally
            {
                EditorSceneManager.playModeStartScene = previous;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }
}
