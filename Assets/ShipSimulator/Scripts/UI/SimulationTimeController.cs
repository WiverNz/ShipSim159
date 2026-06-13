using UnityEngine;

namespace ShipSimulator.UI
{
    public sealed class SimulationTimeController : MonoBehaviour
    {
        private static readonly float[] Scales = { 1f, 2f, 4f };
        private int scaleIndex;

        public float CurrentScale => Scales[scaleIndex];
        public string DisplayText => $"{CurrentScale:0}x";

        private void Awake()
        {
            SetScale(1f);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        public void Cycle()
        {
            scaleIndex = (scaleIndex + 1) % Scales.Length;
            Apply();
        }

        public void SetScale(float scale)
        {
            int nearestIndex = 0;
            float nearestDifference = float.PositiveInfinity;
            for (int i = 0; i < Scales.Length; i++)
            {
                float difference = Mathf.Abs(Scales[i] - scale);
                if (difference >= nearestDifference) continue;
                nearestDifference = difference;
                nearestIndex = i;
            }
            scaleIndex = nearestIndex;
            Apply();
        }

        private void Apply()
        {
            Time.timeScale = Scales[scaleIndex];
        }
    }
}
