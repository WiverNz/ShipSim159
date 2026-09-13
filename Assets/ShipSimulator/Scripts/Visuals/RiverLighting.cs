using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Visuals
{
    [DefaultExecutionOrder(100)]
    public sealed class RiverLighting : MonoBehaviour
    {
        // Must match RiverSkyLighting.compute.
        public const int SkyColumns = 32;
        public const int SkyRows = 16;
        // Below the horizon the sky capture holds haze; open ground returns far less light.
        public const float GroundBounce = 0.24f;
        // The sun disc already lights the scene directly, so it is kept out of the ambient term.
        private const float MaxSkyRadiance = 4f;
        private const int SkyMip = 3;
        private const int CookieResolution = 512;
        // Fog hides the scene well inside this; past it the cookie clamps to its edge texels.
        private const float CookieSizeM = 6000f;

        private static readonly int CookieId = Shader.PropertyToID("_Cookie");
        private static readonly int CookieToWorldId = Shader.PropertyToID("_CookieToWorld");
        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        private static readonly int CookieResolutionId = Shader.PropertyToID("_CookieResolution");
        private static readonly int TimeId = Shader.PropertyToID("_Time");
        private static readonly int WindId = Shader.PropertyToID("_RiverWind");
        private static readonly int CloudWeatherId = Shader.PropertyToID("_RiverCloudWeather");
        private static readonly int CloudSettingsId = Shader.PropertyToID("_RiverCloudSettings");
        private static readonly int CloudShadowDisableId = Shader.PropertyToID("_RiverCloudShadowDisable");

        private Light sun;
        private WeatherController weather;
        private DayNightController clock;
        private float nextControllerLookup;
        private ReflectionProbe skyProbe;
        private Volume exposureVolume;
        private VolumeProfile exposureProfile;
        private ColorAdjustments exposure;
        private float nextRefresh;
        private float lastWeather = -1;
        private bool lastNight;
        private Camera skyCamera;
        private RenderTexture skyTexture;
        private ComputeShader lightingShader;
        private ComputeBuffer skySamples;
        private bool skyReadbackPending;
        private RenderTexture cloudCookie;
        private float targetExposure;
        private Texture originalReflection;
        private DefaultReflectionMode originalReflectionMode;
        private SphericalHarmonicsL2 originalAmbient;
        private AmbientMode originalAmbientMode;
        private Texture originalCookie;
        public static RiverLighting Active { get; private set; }
        public float ExposureEV => exposure != null ? exposure.postExposure.value : 0;
        public bool IsNight => lastNight;
        public int MeterSamples { get; private set; }
        public int ProbeUpdates { get; private set; }
        public int AmbientUpdates { get; private set; }

        private void OnEnable()
        {
            Active = this;
            originalReflection = RenderSettings.customReflectionTexture;
            originalReflectionMode = RenderSettings.defaultReflectionMode;
            originalAmbient = RenderSettings.ambientProbe;
            originalAmbientMode = RenderSettings.ambientMode;
            sun = RenderSettings.sun;
            if (sun == null)
                foreach (Light light in FindObjectsByType<Light>())
                    if (light.type == LightType.Directional) { sun = light; break; }
            if (sun != null) originalCookie = sun.cookie;
            var probeObject = new GameObject("Live sky reflection");
            probeObject.transform.SetParent(transform, false);
            skyProbe = probeObject.AddComponent<ReflectionProbe>();
            skyProbe.mode = ReflectionProbeMode.Custom;
            skyProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            skyProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            skyProbe.cullingMask = 0;
            skyProbe.clearFlags = ReflectionProbeClearFlags.Skybox;
            skyProbe.resolution = 64;
            skyProbe.hdr = true;
            skyProbe.size = Vector3.one * 10000;
            skyProbe.importance = 100;
            skyTexture = new RenderTexture(64, 64, 16, RenderTextureFormat.ARGBHalf)
            {
                name = "Live sky cubemap", dimension = TextureDimension.Cube,
                useMipMap = true, autoGenerateMips = false
            };
            skyTexture.Create();
            skyProbe.customBakedTexture = skyTexture;
            var cameraObject = new GameObject("Sky capture camera");
            cameraObject.transform.SetParent(transform, false);
            skyCamera = cameraObject.AddComponent<Camera>();
            skyCamera.enabled = false;
            skyCamera.cameraType = CameraType.Reflection;
            skyCamera.cullingMask = 0;
            skyCamera.clearFlags = CameraClearFlags.Skybox;
            skyCamera.allowHDR = true;
            skyCamera.allowMSAA = false;
            skyCamera.fieldOfView = 90;
            var cameraData = skyCamera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraData.antialiasing = AntialiasingMode.None;
            lightingShader = SystemInfo.supportsComputeShaders
                ? Resources.Load<ComputeShader>("RiverSkyLighting")
                : null;
            if (lightingShader != null)
            {
                skySamples = new ComputeBuffer(SkyColumns * SkyRows, sizeof(float) * 4);
                cloudCookie = new RenderTexture(CookieResolution, CookieResolution, 0, RenderTextureFormat.RHalf)
                {
                    name = "River cloud shadow cookie", enableRandomWrite = true,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
                };
                cloudCookie.Create();
            }
            exposureProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            exposure = exposureProfile.Add<ColorAdjustments>();
            exposure.postExposure.Override(0);
            exposureVolume = gameObject.AddComponent<Volume>();
            exposureVolume.isGlobal = true;
            exposureVolume.priority = 1000;
            exposureVolume.sharedProfile = exposureProfile;
            Refresh();
        }

        private void Start() => Refresh();

        private void LateUpdate()
        {
            Shader.SetGlobalFloat("_RiverPreviousTime", Time.time - Time.deltaTime);
            FindControllers(false);
            bool night = clock != null && clock.IsNight;
            if (Mathf.Abs(CloudCover() - lastWeather) > 0.01f || night != lastNight) Refresh();
            if (Time.unscaledTime >= nextRefresh) CaptureSky();
            UpdateCloudCookie();
            ConfigureCameraQuality();
            float rate = targetExposure < ExposureEV ? 2.5f : 0.65f;
            exposure.postExposure.value = Mathf.Lerp(ExposureEV, targetExposure,
                1 - Mathf.Exp(-rate * Time.unscaledDeltaTime));
        }

        // Temporal AA needs the water motion vectors, which only renderers carrying
        // RiverTemporalFeature produce; others fall back to SMAA.
        public static void ConfigureCameraQuality()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            var data = camera.GetUniversalAdditionalCameraData();
            AntialiasingMode mode = RiverTemporalFeature.IsActiveRenderer
                ? AntialiasingMode.TemporalAntiAliasing
                : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            if (data.antialiasing == mode) return;
            data.antialiasing = mode;
            data.resetHistory = true;
        }

        public void Refresh()
        {
            FindControllers(true);
            lastWeather = CloudCover();
            lastNight = clock != null && clock.IsNight;
            if (sun != null)
            {
                sun.intensity = (lastNight ? 0.28f : 1.25f) * Mathf.Lerp(1, 0.22f, lastWeather);
                sun.shadowStrength = Mathf.Lerp(lastNight ? 0.4f : 0.72f, 0.25f, lastWeather);
            }
            Material sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_CloudCoverage"))
                Shader.SetGlobalVector(CloudSettingsId, new Vector4(
                    sky.GetFloat("_CloudCoverage"), sky.GetFloat("_CloudScale"), sky.GetFloat("_CloudSpeed"), 1));
            nextRefresh = 0;
        }

        // The weather and day-night controllers can live on different objects: Gorodets
        // serializes the weather system, while the HUD adds the clock to itself at runtime.
        private void FindControllers(bool force)
        {
            if (weather != null && clock != null) return;
            if (!force && Time.unscaledTime < nextControllerLookup) return;
            nextControllerLookup = Time.unscaledTime + 1f;
            if (weather == null) weather = FindAnyObjectByType<WeatherController>();
            if (clock == null) clock = FindAnyObjectByType<DayNightController>();
        }

        private float CloudCover()
        {
            return weather == null ? 0 : Mathf.Max(weather.RainIntensity, weather.FogIntensity * 0.8f);
        }

        private void CaptureSky()
        {
            nextRefresh = Time.unscaledTime + 8;
            if (!skyCamera.RenderToCubemap(skyTexture)) return;
            skyTexture.GenerateMips();
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = skyTexture;
            ProbeUpdates++;
            if (lightingShader == null || skyReadbackPending) return;

            int kernel = lightingShader.FindKernel("SkySamples");
            lightingShader.SetTexture(kernel, "_Sky", skyTexture);
            lightingShader.SetBuffer(kernel, "_Samples", skySamples);
            lightingShader.SetFloat("_SkyMip", SkyMip);
            lightingShader.Dispatch(kernel, Mathf.CeilToInt(SkyColumns / 8f), Mathf.CeilToInt(SkyRows / 8f), 1);
            skyReadbackPending = true;
            AsyncGPUReadback.Request(skySamples, request =>
            {
                skyReadbackPending = false;
                if (request.hasError || this == null || !isActiveAndEnabled) return;
                RenderSettings.ambientMode = AmbientMode.Custom;
                RenderSettings.ambientProbe = ProjectSkyToAmbient(request.GetData<Vector4>().ToArray(), SkyColumns, SkyRows);
                AmbientUpdates++;
            });
        }

        // Ambient spherical harmonics from latitude-longitude sky radiance, scaled so a uniform sky of
        // radiance L evaluates to L, as Unity's flat ambient colour does. The scale is measured rather
        // than assumed because AddDirectionalLight applies its own normalization.
        public static SphericalHarmonicsL2 ProjectSkyToAmbient(Vector4[] samples, int columns, int rows)
        {
            SphericalHarmonicsL2 sky = Project(samples, columns, rows, true);
            var uniform = new Vector4[samples.Length];
            for (int i = 0; i < uniform.Length; i++) uniform[i] = Vector4.one;
            var reference = new Color[1];
            Project(uniform, columns, rows, false).Evaluate(new[] { Vector3.up }, reference);
            return sky * (1f / Mathf.Max(reference[0].r, 0.0001f));
        }

        private static SphericalHarmonicsL2 Project(Vector4[] samples, int columns, int rows, bool shadeGround)
        {
            var sh = new SphericalHarmonicsL2();
            float cellArea = Mathf.PI / rows * (2 * Mathf.PI / columns);
            for (int y = 0; y < rows; y++)
            {
                float latitude = ((y + 0.5f) / rows - 0.5f) * Mathf.PI;
                float weight = Mathf.Cos(latitude) * cellArea / Mathf.PI;
                for (int x = 0; x < columns; x++)
                {
                    float longitude = (x + 0.5f) / columns * 2 * Mathf.PI;
                    var direction = new Vector3(
                        Mathf.Cos(latitude) * Mathf.Cos(longitude),
                        Mathf.Sin(latitude),
                        Mathf.Cos(latitude) * Mathf.Sin(longitude));
                    Vector4 sample = samples[y * columns + x];
                    var radiance = new Color(sample.x, sample.y, sample.z);
                    float luminance = 0.2126f * radiance.r + 0.7152f * radiance.g + 0.0722f * radiance.b;
                    if (luminance > MaxSkyRadiance) radiance *= MaxSkyRadiance / luminance;
                    if (shadeGround && latitude < 0) radiance *= GroundBounce;
                    sh.AddDirectionalLight(direction, radiance, weight);
                }
            }
            return sh;
        }

        // Inverse of URP's directional cookie projection (LightCookieManager.SetupMainLight), where
        // uv = (lightSpace.xy - offset) / size + 0.5. Maps cookie uv back to the light-space plane z = 0.
        public static Matrix4x4 CookieToWorld(Matrix4x4 lightToWorld, float size, Vector2 offset)
        {
            Matrix4x4 uvToLight = Matrix4x4.identity;
            uvToLight.m00 = size;
            uvToLight.m11 = size;
            uvToLight.m03 = offset.x - size * 0.5f;
            uvToLight.m13 = offset.y - size * 0.5f;
            return lightToWorld * uvToLight;
        }

        private void UpdateCloudCookie()
        {
            if (cloudCookie == null || sun == null) return;
            Camera view = Camera.main;
            Vector3 center = view != null ? view.transform.position : transform.position;
            Matrix4x4 lightToWorld = sun.transform.localToWorldMatrix;
            Vector2 offset = lightToWorld.inverse.MultiplyPoint3x4(center);
            var lightData = sun.GetUniversalAdditionalLightData();
            lightData.lightCookieSize = Vector2.one * CookieSizeM;
            lightData.lightCookieOffset = offset;

            int kernel = lightingShader.FindKernel("CloudCookie");
            float time = Time.time;
            lightingShader.SetTexture(kernel, CookieId, cloudCookie);
            lightingShader.SetMatrix(CookieToWorldId, CookieToWorld(lightToWorld, CookieSizeM, offset));
            lightingShader.SetVector(SunDirectionId, -sun.transform.forward);
            lightingShader.SetFloat(CookieResolutionId, CookieResolution);
            lightingShader.SetVector(TimeId, new Vector4(time / 20, time, time * 2, time * 3));
            lightingShader.SetVector(WindId, Shader.GetGlobalVector(WindId));
            lightingShader.SetFloat(CloudWeatherId, Shader.GetGlobalFloat(CloudWeatherId));
            lightingShader.SetVector(CloudSettingsId, Shader.GetGlobalVector(CloudSettingsId));
            lightingShader.SetFloat(CloudShadowDisableId, Shader.GetGlobalFloat(CloudShadowDisableId));
            int groups = Mathf.CeilToInt(CookieResolution / 8f);
            lightingShader.Dispatch(kernel, groups, groups, 1);
            sun.cookie = cloudCookie;
        }

        public static float MeterExposure(float[] luminance, bool night)
        {
            if (luminance == null || luminance.Length == 0) return 0;
            var histogram = new int[64];
            foreach (float value in luminance)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) continue;
                int bin = Mathf.Clamp(Mathf.FloorToInt((Mathf.Log(Mathf.Max(value, 0.00001f), 2) + 16) * 2), 0, 63);
                histogram[bin]++;
            }
            int total = 0;
            foreach (int count in histogram) total += count;
            if (total == 0) return 0;
            float low = total * 0.1f, high = total * 0.9f;
            float sum = 0, weight = 0;
            int cumulative = 0;
            for (int bin = 0; bin < histogram.Length; bin++)
            {
                float included = Mathf.Max(0, Mathf.Min(cumulative + histogram[bin], high) - Mathf.Max(cumulative, low));
                sum += included * ((bin + 0.5f) / 2 - 16);
                weight += included;
                cumulative += histogram[bin];
            }
            return Mathf.Clamp(Mathf.Log(night ? 0.035f : 0.18f, 2) - sum / Mathf.Max(weight, 1), -1.5f, night ? 1.2f : 2f);
        }

        public void SubmitMeter(float[] values)
        {
            targetExposure = MeterExposure(values, lastNight);
            MeterSamples++;
        }

        private void OnDisable()
        {
            if (Active == this) Active = null;
            Shader.SetGlobalVector(CloudSettingsId, Vector4.zero);
            RenderSettings.customReflectionTexture = originalReflection;
            RenderSettings.defaultReflectionMode = originalReflectionMode;
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientProbe = originalAmbient;
            if (sun != null) sun.cookie = originalCookie;
            if (skyProbe != null) Destroy(skyProbe.gameObject);
            if (skyCamera != null) Destroy(skyCamera.gameObject);
            if (skyTexture != null) Destroy(skyTexture);
            if (cloudCookie != null) Destroy(cloudCookie);
            skySamples?.Release();
            skySamples = null;
            if (exposureVolume != null) Destroy(exposureVolume);
            if (exposureProfile != null) Destroy(exposureProfile);
        }
    }
}
