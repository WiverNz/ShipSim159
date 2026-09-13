using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Visuals
{
    [DefaultExecutionOrder(100)]
    public sealed class RiverLighting : MonoBehaviour
    {
        private Light sun;
        private ReflectionProbe skyProbe;
        private Volume exposureVolume;
        private VolumeProfile exposureProfile;
        private ColorAdjustments exposure;
        private float nextRefresh;
        private float lastWeather = -1;
        private bool lastNight;
        private Camera skyCamera;
        private RenderTexture skyTexture;
        private string lastPipeline;
        private float targetExposure;
        private Texture originalReflection;
        private DefaultReflectionMode originalReflectionMode;
        private SphericalHarmonicsL2 originalAmbient;
        private AmbientMode originalAmbientMode;
        public static RiverLighting Active { get; private set; }
        public float ExposureEV => exposure != null ? exposure.postExposure.value : 0;
        public int MeterSamples { get; private set; }
        public int ProbeUpdates { get; private set; }

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
                useMipMap = true, autoGenerateMips = true
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
            float previousTime = Time.time - Time.deltaTime;
            Shader.SetGlobalFloat("_RiverPreviousTime", previousTime);
            var weather = GetComponent<WeatherController>();
            var clock = GetComponent<DayNightController>();
            float cover = weather == null ? 0 : Mathf.Max(weather.RainIntensity, weather.FogIntensity * 0.8f);
            bool night = clock != null && clock.IsNight;
            if (Mathf.Abs(cover - lastWeather) > 0.01f || night != lastNight) Refresh();
            if (Time.unscaledTime >= nextRefresh)
            {
                if (skyCamera.RenderToCubemap(skyTexture))
                {
                    RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                    RenderSettings.customReflectionTexture = skyTexture;
                    ProbeUpdates++;
                }
                nextRefresh = Time.unscaledTime + 8;
            }
            string pipeline = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "";
            if (pipeline != lastPipeline)
            {
                lastPipeline = pipeline;
                ConfigureCameraQuality();
            }
            float rate = targetExposure < ExposureEV ? 2.5f : 0.65f;
            exposure.postExposure.value = Mathf.Lerp(ExposureEV, targetExposure,
                1 - Mathf.Exp(-rate * Time.unscaledDeltaTime));
        }

        public static void ConfigureCameraQuality()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            var data = camera.GetUniversalAdditionalCameraData();
            bool desktop = GraphicsSettings.currentRenderPipeline != null && GraphicsSettings.currentRenderPipeline.name == "PC_RPAsset";
            data.antialiasing = desktop ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.resetHistory = true;
        }

        public void Refresh()
        {
            var weather = GetComponent<WeatherController>();
            var clock = GetComponent<DayNightController>();
            lastWeather = weather == null ? 0 : Mathf.Max(weather.RainIntensity, weather.FogIntensity * 0.8f);
            lastNight = clock != null && clock.IsNight;
            if (sun != null)
            {
                sun.intensity = (lastNight ? 0.28f : 1.25f) * Mathf.Lerp(1, 0.22f, lastWeather);
                sun.shadowStrength = Mathf.Lerp(lastNight ? 0.4f : 0.72f, 0.25f, lastWeather);
            }
            Material sky = RenderSettings.skybox;
            if (sky != null && sky.shader != null && sky.shader.name == "ShipSimulator/RiverSky")
            {
                Shader.SetGlobalVector("_RiverCloudSettings", new Vector4(sky.GetFloat("_CloudCoverage"), sky.GetFloat("_CloudScale"), sky.GetFloat("_CloudSpeed"), 1));
                Color tint = sky.GetColor("_SkyTint") * 2;
                Color zenith = sky.GetColor("_ZenithColor") * tint;
                Color horizon = Color.Lerp(sky.GetColor("_HorizonColor") * tint, RenderSettings.fogColor, 0.55f);
                Color cloud = sky.GetColor("_CloudShadeColor") * tint;
                float skyExposure = sky.GetFloat("_Exposure");
                float coverage = Mathf.Lerp(sky.GetFloat("_CloudCoverage"), 1, lastWeather);
                var sh = new SphericalHarmonicsL2();
                const int samples = 128;
                for (int i = 0; i < samples; i++)
                {
                    float y = 1 - 2 * (i + 0.5f) / samples;
                    float radius = Mathf.Sqrt(1 - y * y);
                    float angle = i * 2.39996323f;
                    Vector3 direction = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                    Color radiance = Color.Lerp(horizon, zenith, 1 - Mathf.Exp(-Mathf.Max(y, 0) * 3.2f));
                    radiance = Color.Lerp(radiance, cloud, coverage * 0.55f);
                    if (y < 0) radiance *= 0.24f;
                    sh.AddDirectionalLight(direction, radiance * skyExposure, 4f / samples);
                }
                RenderSettings.ambientMode = AmbientMode.Custom;
                RenderSettings.ambientProbe = sh;
            }
            nextRefresh = 0;
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
            Shader.SetGlobalVector("_RiverCloudSettings", Vector4.zero);
            RenderSettings.customReflectionTexture = originalReflection;
            RenderSettings.defaultReflectionMode = originalReflectionMode;
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientProbe = originalAmbient;
            if (skyProbe != null) Destroy(skyProbe.gameObject);
            if (skyCamera != null) Destroy(skyCamera.gameObject);
            if (skyTexture != null) Destroy(skyTexture);
            lastPipeline = null;
            if (exposureVolume != null) Destroy(exposureVolume);
            if (exposureProfile != null) Destroy(exposureProfile);
        }
    }
}
