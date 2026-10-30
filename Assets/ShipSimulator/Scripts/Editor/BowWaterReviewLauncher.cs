using ShipSimulator.Diagnostics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShipSimulator.Editor
{
    [InitializeOnLoad]
    public static class BowWaterReviewLauncher
    {
        private const string Key = "ShipSimulator.BowWaterReview";
        static BowWaterReviewLauncher() => EditorApplication.update += Tick;
        public static void Run()
        {
            if (!Application.isBatchMode) throw new System.InvalidOperationException("Use a dedicated batch editor.");
            if (!System.Linq.Enumerable.Contains(System.Environment.GetCommandLineArgs(), "--shipsim-bow-review"))
                throw new System.InvalidOperationException("Pass --shipsim-bow-review to run the shared player fixture.");
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene("Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity");
            VolgoDonBowReview.Validate(Object.FindAnyObjectByType<ShipSimulator.Physics.ShipPhysicsController>().gameObject);
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || BowWaterReview.Result < 0) return;
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(BowWaterReview.Result);
        }
    }
}
