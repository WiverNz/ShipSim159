using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.Visuals
{
    public sealed class WeatherController : MonoBehaviour
    {
        private static readonly float[] WindSpeedsMps = { 0f, 4f, 8f, 12f, 16f };
        private static readonly float[] RainLevels = { 0f, 0.35f, 0.7f, 1f };
        private static readonly float[] FogLevels = { 0f, 0.3f, 0.65f, 1f };

        [SerializeField, Range(0f, 359f)] private float windDirectionDeg = 270f;
        [SerializeField] private float windSpeedMps = 4f;
        [SerializeField, Range(0f, 1f)] private float rainIntensity;
        [SerializeField, Range(0f, 1f)] private float fogIntensity;

        private ParticleSystem rain;
        private Transform rainTransform;
        private Material rainMaterial;
        private Camera targetCamera;
        private MaterialPropertyBlock waterProperties;

        public float WindDirectionDeg => windDirectionDeg;
        public float WindSpeedMps => windSpeedMps;
        public float RainIntensity => rainIntensity;
        public float FogIntensity => fogIntensity;
        public Vector3 WindVelocityMps =>
            CalculateWindVelocity(windDirectionDeg, windSpeedMps);
        public string StatusText =>
            $"WIND {windDirectionDeg:000} deg  {windSpeedMps:0} m/s   " +
            $"RAIN {rainIntensity * 100f:0}%   FOG {fogIntensity * 100f:0}%";

        private void Awake()
        {
            EnsureRainSystem();
            ApplyWeather();
        }

        private void Start()
        {
            ApplyWeather();
        }

        private void LateUpdate()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null && rainTransform != null)
                rainTransform.position = targetCamera.transform.position +
                    Vector3.up * 22f;
        }

        public void Configure(float directionDeg, float speedMps,
            float rain01, float fog01)
        {
            windDirectionDeg = NormalizeDegrees(directionDeg);
            windSpeedMps = Mathf.Max(0f, speedMps);
            rainIntensity = Mathf.Clamp01(rain01);
            fogIntensity = Mathf.Clamp01(fog01);
            ApplyWeather();
        }

        public void RotateWind(float degrees)
        {
            windDirectionDeg = NormalizeDegrees(windDirectionDeg + degrees);
            ApplyWeather();
        }

        public void CycleWindSpeed()
        {
            windSpeedMps = NextValue(WindSpeedsMps, windSpeedMps);
            ApplyWeather();
        }

        public void CycleRain()
        {
            rainIntensity = NextValue(RainLevels, rainIntensity);
            ApplyWeather();
        }

        public void CycleFog()
        {
            fogIntensity = NextValue(FogLevels, fogIntensity);
            ApplyWeather();
        }

        public void RefreshVisuals()
        {
            ApplyFog();
            ApplyWater();
        }

        public static Vector3 CalculateWindVelocity(float directionDeg, float speedMps)
        {
            float radians = directionDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians)) *
                Mathf.Max(0f, speedMps);
        }

        private void ApplyWeather()
        {
            Vector3 velocity = WindVelocityMps;
            foreach (ShipPhysicsController ship in
                     FindObjectsByType<ShipPhysicsController>(FindObjectsInactive.Include))
                ship.SetWindVelocity(velocity);
            ApplyRain();
            ApplyFog();
            ApplyWater();
        }

        private void EnsureRainSystem()
        {
            if (rain != null) return;
            Transform existing = transform.Find("Rain Volume");
            GameObject rainObject = existing != null
                ? existing.gameObject
                : new GameObject("Rain Volume");
            if (existing == null)
                rainObject.transform.SetParent(transform, false);
            rainTransform = rainObject.transform;
            rain = rainObject.GetComponent<ParticleSystem>();
            if (rain == null) rain = rainObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = rain.main;
            main.loop = true;
            main.startLifetime = 2.4f;
            main.startSpeed = 28f;
            main.startSize = 0.055f;
            main.maxParticles = 6500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.2f;
            main.startColor = new Color(0.64f, 0.75f, 0.85f, 0.58f);

            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(95f, 3f, 95f);
            shape.rotation = new Vector3(90f, 0f, 0f);

            ParticleSystem.VelocityOverLifetimeModule velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = -28f;

            ParticleSystemRenderer renderer =
                rainObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 18f;
            renderer.velocityScale = 0.08f;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                rainMaterial = new Material(shader) { name = "Runtime Rain" };
                rainMaterial.SetColor("_BaseColor",
                    new Color(0.62f, 0.74f, 0.84f, 0.5f));
                renderer.material = rainMaterial;
            }
        }

        private void ApplyRain()
        {
            EnsureRainSystem();
            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = Mathf.Lerp(0f, 2600f, rainIntensity);
            ParticleSystem.VelocityOverLifetimeModule velocity =
                rain.velocityOverLifetime;
            Vector3 wind = WindVelocityMps;
            velocity.x = wind.x * 0.45f;
            velocity.y = -28f;
            velocity.z = wind.z * 0.45f;
            if (rainIntensity > 0.01f)
            {
                if (!rain.isPlaying) rain.Play();
            }
            else
            {
                rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void ApplyFog()
        {
            DayNightController dayNight = FindAnyObjectByType<DayNightController>();
            bool night = dayNight != null && dayNight.IsNight;
            float clearDensity = night ? 0.00145f : 0.0011f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = Mathf.Lerp(clearDensity, 0.012f, fogIntensity);
            Color clearColor = night
                ? new Color(0.04f, 0.075f, 0.12f)
                : new Color(0.57f, 0.67f, 0.73f);
            Color denseColor = night
                ? new Color(0.075f, 0.09f, 0.11f)
                : new Color(0.56f, 0.59f, 0.60f);
            RenderSettings.fogColor = Color.Lerp(clearColor, denseColor, fogIntensity);
        }

        private void ApplyWater()
        {
            if (waterProperties == null)
                waterProperties = new MaterialPropertyBlock();
            float wind01 = Mathf.Clamp01(windSpeedMps / 16f);
            foreach (Renderer renderer in
                     FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null ||
                    material.shader.name != "ShipSimulator/RiverWater")
                    continue;
                renderer.GetPropertyBlock(waterProperties);
                waterProperties.SetFloat("_RippleStrength",
                    Mathf.Lerp(0.14f, 0.34f, wind01));
                waterProperties.SetFloat("_WaveHeight",
                    Mathf.Lerp(0.025f, 0.075f, wind01));
                waterProperties.SetFloat("_WaveSpeed",
                    Mathf.Lerp(0.35f, 0.8f, wind01));
                waterProperties.SetFloat("_Turbidity",
                    Mathf.Lerp(0.62f, 0.76f, rainIntensity));
                renderer.SetPropertyBlock(waterProperties);
            }
        }

        private static float NextValue(float[] values, float current)
        {
            for (int i = 0; i < values.Length; i++)
                if (values[i] > current + 0.01f)
                    return values[i];
            return values[0];
        }

        private static float NormalizeDegrees(float value)
        {
            return (value % 360f + 360f) % 360f;
        }

        private void OnDestroy()
        {
            if (rainMaterial == null) return;
            if (Application.isPlaying) Destroy(rainMaterial);
            else DestroyImmediate(rainMaterial);
        }
    }
}
