using UnityEngine;

namespace ShipSimulator.Visuals
{
    public sealed class NavigationBeaconFlasher : MonoBehaviour
    {
        [SerializeField] private float periodSeconds = 1.5f;
        [SerializeField] private float flashDurationSeconds = 0.32f;
        [SerializeField] private float phaseSeconds;

        private Light beaconLight;
        private Renderer beaconLens;
        private bool nightEnabled;

        public bool IsLit { get; private set; }
        public float PeriodSeconds => periodSeconds;
        public float FlashDurationSeconds => flashDurationSeconds;

        private void Awake()
        {
            beaconLight = GetComponent<Light>();
            beaconLens = GetComponentInChildren<Renderer>(true);
            ApplyState(false);
        }

        private void Update()
        {
            bool shouldBeLit = nightEnabled &&
                Mathf.Repeat(Time.time + phaseSeconds, periodSeconds) <
                flashDurationSeconds;
            ApplyState(shouldBeLit);
        }

        public void Configure(
            Light light, Renderer lens, float period, float duration, float phase)
        {
            beaconLight = light;
            beaconLens = lens;
            periodSeconds = Mathf.Max(0.2f, period);
            flashDurationSeconds = Mathf.Clamp(duration, 0.05f, periodSeconds);
            phaseSeconds = Mathf.Repeat(phase, periodSeconds);
            ApplyState(false);
        }

        public void SetNight(bool enabled)
        {
            nightEnabled = enabled;
            if (!enabled) ApplyState(false);
        }

        public bool EvaluateLit(float time)
        {
            return nightEnabled &&
                Mathf.Repeat(time + phaseSeconds, periodSeconds) <
                flashDurationSeconds;
        }

        private void ApplyState(bool lit)
        {
            IsLit = lit;
            if (beaconLight != null) beaconLight.enabled = lit;
            if (beaconLens != null) beaconLens.enabled = lit;
        }
    }
}
