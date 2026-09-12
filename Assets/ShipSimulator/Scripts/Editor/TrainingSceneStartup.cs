using ShipSimulator.Persistence;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShipSimulator.Editor
{
    [InitializeOnLoad]
    public static class TrainingSceneStartup
    {
        public const string ScenePath = "Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity";
        private static double readyAt;

        static TrainingSceneStartup()
        {
            // Test runners and scene builders own their scene setup.
            if (Application.isBatchMode) return;
            readyAt = EditorApplication.timeSinceStartup + 2;
            EditorApplication.update += Initialize;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.newSceneCreated += OnNewScene;
        }

        private static void Initialize()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= Initialize;
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                readyAt = EditorApplication.timeSinceStartup + 2;
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            // The first delayCall can precede Unity restoring its last scene setup.
            if (!scene.IsValid() || !scene.isLoaded || EditorApplication.timeSinceStartup < readyAt) return;
            EditorApplication.update -= Initialize;
            if (SceneManager.sceneCount == 1 && string.IsNullOrEmpty(scene.path) && !scene.isDirty)
                EditorSceneManager.OpenScene(ScenePath);
            ConfigurePlayScene();
        }

        private static void OnActiveSceneChanged(Scene previous, Scene current) => ConfigurePlayScene();
        private static void OnNewScene(Scene scene, NewSceneSetup setup, NewSceneMode mode) => ConfigurePlayScene();

        public static void ConfigurePlayScene()
        {
            // Play a selected voyage directly; otherwise launch the game without replacing editor work.
            EditorSceneManager.playModeStartScene = VoyageSave.IsVoyageScene(SceneManager.GetActiveScene().name)
                ? null
                : AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        }
    }
}
