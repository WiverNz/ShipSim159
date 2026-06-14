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
        private ParticleSystem rainSplashes;
        private Transform splashTransform;
        private Material rainMaterial;
        private Material splashMaterial;
        private Mesh rainDropMesh;
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
            {
                rainTransform.position = targetCamera.transform.position +
                    Vector3.up * 24f;
                if (splashTransform != null)
                {
                    splashTransform.position = new Vector3(
                        targetCamera.transform.position.x,
                        0.08f,
                        targetCamera.transform.position.z);
                }
            }
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 2.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.65f, 1.35f);
            main.maxParticles = 5500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.58f, 0.69f, 0.78f, 0.28f),
                new Color(0.78f, 0.86f, 0.92f, 0.62f));

            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(100f, 5f, 100f);

            ParticleSystem.VelocityOverLifetimeModule velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = -30f;

            ParticleSystem.NoiseModule noise = rain.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.35f;
            noise.frequency = 0.18f;
            noise.scrollSpeed = 0.12f;

            ParticleSystemRenderer renderer =
                rainObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            rainDropMesh = CreateRainDropMesh();
            renderer.mesh = rainDropMesh;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                rainMaterial = new Material(shader) { name = "Runtime Rain" };
                rainMaterial.SetColor("_BaseColor",
                    new Color(0.64f, 0.76f, 0.84f, 0.52f));
                rainMaterial.SetFloat("_Surface", 1f);
                rainMaterial.SetFloat("_ZWrite", 0f);
                rainMaterial.renderQueue = 3000;
                renderer.material = rainMaterial;
            }

            EnsureSplashSystem(shader);
        }

        private void ApplyRain()
        {
            EnsureRainSystem();
            ParticleSystem.EmissionModule emission = rain.emission;
            emission.rateOverTime = Mathf.Lerp(0f, 1850f, rainIntensity);
            ParticleSystem.VelocityOverLifetimeModule velocity =
                rain.velocityOverLifetime;
            Vector3 wind = WindVelocityMps;
            velocity.x = wind.x * 0.55f;
            velocity.y = -30f;
            velocity.z = wind.z * 0.55f;

            ParticleSystem.EmissionModule splashEmission =
                rainSplashes.emission;
            splashEmission.rateOverTime = Mathf.Lerp(
                0f, 420f, rainIntensity);
            if (rainIntensity > 0.01f)
            {
                if (!rain.isPlaying) rain.Play();
                if (!rainSplashes.isPlaying) rainSplashes.Play();
            }
            else
            {
                rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                rainSplashes.Stop(
                    true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void EnsureSplashSystem(Shader shader)
        {
            Transform existing = transform.Find("Rain Surface Splashes");
            GameObject splashObject = existing != null
                ? existing.gameObject
                : new GameObject("Rain Surface Splashes");
            if (existing == null)
                splashObject.transform.SetParent(transform, false);
            splashTransform = splashObject.transform;
            rainSplashes = splashObject.GetComponent<ParticleSystem>();
            if (rainSplashes == null)
                rainSplashes = splashObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = rainSplashes.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.68f, 0.78f, 0.84f, 0.18f),
                new Color(0.82f, 0.9f, 0.94f, 0.5f));
            main.maxParticles = 900;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.3f;

            ParticleSystem.EmissionModule emission = rainSplashes.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = rainSplashes.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(75f, 0.02f, 75f);

            ParticleSystem.ColorOverLifetimeModule color =
                rainSplashes.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.65f, 0.18f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = fade;

            ParticleSystemRenderer renderer =
                splashObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (shader != null)
            {
                splashMaterial = new Material(shader)
                {
                    name = "Runtime Rain Splashes"
                };
                splashMaterial.SetColor("_BaseColor",
                    new Color(0.72f, 0.83f, 0.88f, 0.42f));
                splashMaterial.SetFloat("_Surface", 1f);
                splashMaterial.SetFloat("_ZWrite", 0f);
                splashMaterial.renderQueue = 3000;
                renderer.material = splashMaterial;
            }
        }

        private static Mesh CreateRainDropMesh()
        {
            Mesh mesh = new Mesh { name = "Runtime Rain Drop" };
            mesh.vertices = new[]
            {
                new Vector3(0f, 0.09f, 0f),
                new Vector3(-0.014f, 0f, 0f),
                new Vector3(0f, 0f, 0.014f),
                new Vector3(0.014f, 0f, 0f),
                new Vector3(0f, 0f, -0.014f),
                new Vector3(0f, -0.48f, 0f)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, 0, 4, 3, 0, 1, 4,
                5, 1, 2, 5, 2, 3, 5, 3, 4, 5, 4, 1
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
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
            DestroyGenerated(rainMaterial);
            DestroyGenerated(splashMaterial);
            DestroyGenerated(rainDropMesh);
        }

        private static void DestroyGenerated(Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }
    }
}
