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
        private float splashRemainder;
        private Material rainMaterial;
        private Material splashMaterial;
        private Texture2D rainTexture;
        private Texture2D splashTexture;
        private Camera targetCamera;
        private MaterialPropertyBlock waterProperties;
        private Vector3 visualWindMps;
        private Vector2 windTravelM;
        private static readonly int WindId = Shader.PropertyToID("_RiverWind");
        private static readonly int WindTravelId = Shader.PropertyToID("_RiverWindTravel");
        private static readonly int PreviousWindId = Shader.PropertyToID("_RiverPreviousWind");
        private static readonly int PreviousWindTravelId = Shader.PropertyToID("_RiverPreviousWindTravel");

        public float WindDirectionDeg => windDirectionDeg;
        public float WindSpeedMps => windSpeedMps;
        public float RainIntensity => rainIntensity;
        public float FogIntensity => fogIntensity;
        public Vector3 WindVelocityMps =>
            CalculateWindVelocity(windDirectionDeg, windSpeedMps);
        // Gusting wind seen by water, clouds and trees; the vessel uses WindVelocityMps.
        public Vector3 VisualWindMps => visualWindMps;
        public string StatusText =>
            $"WIND {windDirectionDeg:000} deg  {windSpeedMps:0} m/s   " +
            $"RAIN {rainIntensity * 100f:0}%   FOG {fogIntensity * 100f:0}%";

        private void Awake()
        {
            if (GetComponent<RiverLighting>() == null) gameObject.AddComponent<RiverLighting>();
            EnsureRainSystem();
            ApplyWeather();
        }

        private void Start()
        {
            ApplyWeather();
        }

        private void LateUpdate()
        {
            UpdateVisualWind();
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
                    EmitWaterSplashes();
                }
            }
        }

        private void EmitWaterSplashes()
        {
            if (rainSplashes == null || rainIntensity <= 0 || Time.deltaTime <= 0) return;
            RiverShoreProfile shore = RiverShoreProfile.Active;
            if (shore == null || !shore.IsReady) return;
            splashRemainder += 950f * rainIntensity * Mathf.Min(Time.deltaTime, 0.1f);
            int count = Mathf.FloorToInt(splashRemainder);
            splashRemainder -= count;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = splashTransform.position + new Vector3(
                    Random.Range(-37.5f, 37.5f), 0, Random.Range(-37.5f, 37.5f));
                if (shore.DistanceToBank(position) < 0.25f) continue;
                // Reject hulls, jetties and dry structures above the water surface.
                if (UnityEngine.Physics.Raycast(position + Vector3.up * 30, Vector3.down,
                    out RaycastHit hit, 30, ~(1 << 4), QueryTriggerInteraction.Ignore)) continue;
                rainSplashes.Emit(new ParticleSystem.EmitParams { position = position, applyShapeToPosition = false }, 1);
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
            GetComponent<RiverLighting>()?.Refresh();
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
            GetComponent<RiverLighting>()?.Refresh();
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
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.maxParticles = 5500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.62f, 0.72f, 0.80f, 0.16f),
                new Color(0.80f, 0.88f, 0.94f, 0.38f));

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
            // Stretched billboards with a soft streak texture read as atmospheric
            // rain rather than hard vertical lines, and stay camera-facing.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 1.6f;
            renderer.cameraVelocityScale = 0f;
            renderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null)
            {
                rainTexture = CreateRainStreakTexture();
                rainMaterial = new Material(shader) { name = "Runtime Rain" };
                rainMaterial.SetColor("_BaseColor",
                    new Color(0.70f, 0.80f, 0.88f, 0.38f));
                if (rainMaterial.HasProperty("_BaseMap"))
                    rainMaterial.SetTexture("_BaseMap", rainTexture);
                rainMaterial.SetFloat("_Surface", 1f);
                rainMaterial.SetFloat("_ZWrite", 0f);
                ConfigureParticleBlending(rainMaterial);
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
            splashEmission.rateOverTime = 0f;
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.68f, 0.78f, 0.84f, 0.18f),
                new Color(0.82f, 0.9f, 0.94f, 0.5f));
            main.maxParticles = 1800;
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
                splashTexture = CreateSplashTexture();
                splashMaterial.SetTexture("_BaseMap", splashTexture);
                ConfigureParticleBlending(splashMaterial);
                renderer.material = splashMaterial;
            }
        }

        private static void ConfigureParticleBlending(Material material)
        {
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.renderQueue = 3000;
        }

        private static Texture2D CreateSplashTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Soft rain impact";
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + 0.5f) / size * 2 - 1, (y + 0.5f) / size * 2 - 1).magnitude;
                float alpha = Mathf.Pow(Mathf.Clamp01(1 - radius), 2);
                pixels[y * size + x] = new Color(1, 1, 1, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        // Soft vertical streak: opaque-ish core fading at the ends and across the
        // width, so stretched billboards look like blurred raindrops, not solid bars.
        private static Texture2D CreateRainStreakTexture()
        {
            const int width = 8;
            const int height = 32;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Runtime Rain Streak",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (height - 1f);
                float lengthFade = Mathf.Sin(v * Mathf.PI);          // fade both ends
                for (int x = 0; x < width; x++)
                {
                    float u = x / (width - 1f);
                    float across = 1f - Mathf.Abs(u * 2f - 1f);      // fade across width
                    across = across * across;
                    float a = Mathf.Clamp01(lengthFade * across);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
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
            if (rainMaterial != null) rainMaterial.SetColor("_BaseColor", night
                ? new Color(0.20f, 0.25f, 0.32f, 0.32f) : new Color(0.70f, 0.80f, 0.88f, 0.38f));
            if (splashMaterial != null) splashMaterial.SetColor("_BaseColor", night
                ? new Color(0.18f, 0.23f, 0.30f, 0.4f) : new Color(0.72f, 0.83f, 0.88f, 0.42f));
            // A global rather than a skybox material property, so the sky asset is never edited
            // before DayNightController swaps in its runtime copy.
            Shader.SetGlobalFloat("_RiverCloudWeather",
                Mathf.Clamp01(Mathf.Max(rainIntensity, fogIntensity * 0.8f)));
        }

        // Shaders move ripples and clouds by the accumulated travel rather than wind times time,
        // so a gust or shift changes their speed without making them jump.
        private void UpdateVisualWind()
        {
            // Last frame's values let swaying vegetation write exact motion vectors.
            Shader.SetGlobalVector(PreviousWindId, new Vector4(visualWindMps.x, 0f, visualWindMps.z, visualWindMps.magnitude));
            Shader.SetGlobalVector(PreviousWindTravelId, new Vector4(windTravelM.x, 0f, windTravelM.y, 0f));
            visualWindMps = WindGustModel.VisualWind(Time.timeAsDouble, windDirectionDeg, windSpeedMps);
            windTravelM += new Vector2(visualWindMps.x, visualWindMps.z) * Time.deltaTime;
            Shader.SetGlobalFloat("_RiverRain", rainIntensity);
            Shader.SetGlobalVector(WindId, new Vector4(visualWindMps.x, 0f, visualWindMps.z, visualWindMps.magnitude));
            Shader.SetGlobalVector(WindTravelId, new Vector4(windTravelM.x, 0f, windTravelM.y, 0f));
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
                    Mathf.Lerp(0.20f, 0.40f, wind01) + rainIntensity * 0.12f);
                waterProperties.SetFloat("_WaveHeight",
                    Mathf.Lerp(0.03f, 0.08f, wind01));
                waterProperties.SetFloat("_WaveSpeed",
                    Mathf.Lerp(0.35f, 0.8f, wind01));
                waterProperties.SetFloat("_Turbidity",
                    Mathf.Lerp(0.55f, 0.72f, rainIntensity));
                // Rain impacts roughen the surface and soften reflections.
                waterProperties.SetFloat("_Smoothness",
                    Mathf.Lerp(0.80f, 0.68f, rainIntensity));
                waterProperties.SetFloat("_ReflectionStrength",
                    Mathf.Lerp(0.62f, 0.82f, rainIntensity));
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
            // Shader globals outlive Play Mode in the editor.
            Shader.SetGlobalFloat("_RiverCloudWeather", 0f);
            Shader.SetGlobalVector(WindId, Vector4.zero);
            Shader.SetGlobalVector(WindTravelId, Vector4.zero);
            Shader.SetGlobalVector(PreviousWindId, Vector4.zero);
            Shader.SetGlobalVector(PreviousWindTravelId, Vector4.zero);
            DestroyGenerated(rainMaterial);
            DestroyGenerated(splashMaterial);
            DestroyGenerated(rainTexture);
            DestroyGenerated(splashTexture);
            Shader.SetGlobalFloat("_RiverRain", 0);
        }

        private static void DestroyGenerated(Object generated)
        {
            if (generated == null) return;
            if (Application.isPlaying) Destroy(generated);
            else DestroyImmediate(generated);
        }
    }
}
