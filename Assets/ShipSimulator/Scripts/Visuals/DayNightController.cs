using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace ShipSimulator.Visuals
{
    public sealed class DayNightController : MonoBehaviour
    {
        private Light sun;
        private Material runtimeSky;
        private bool night;
        private readonly List<Light> navigationLights = new List<Light>();
        private readonly List<Renderer> navigationLenses = new List<Renderer>();
        private readonly List<Material> navigationMaterials = new List<Material>();
        private readonly List<NavigationBeaconFlasher> navigationFlashers =
            new List<NavigationBeaconFlasher>();

        public bool IsNight => night;
        public string TimeLabel => night ? "NIGHT" : "DAY";

        private void Awake()
        {
            foreach (Light light in FindObjectsByType<Light>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }

            if (RenderSettings.skybox != null)
            {
                runtimeSky = new Material(RenderSettings.skybox)
                {
                    name = "Runtime Day Night Sky"
                };
                RenderSettings.skybox = runtimeSky;
            }
            CreateNavigationAids();
            Apply(false);
        }

        public void Toggle()
        {
            Apply(!night);
        }

        public void Apply(bool useNight)
        {
            night = useNight;
            if (sun != null)
            {
                sun.transform.rotation = night
                    ? Quaternion.Euler(18f, 145f, 0f)
                    : Quaternion.Euler(34f, -42f, 0f);
                sun.color = night
                    ? new Color(0.52f, 0.63f, 0.9f)
                    : new Color(1f, 0.94f, 0.84f);
                sun.intensity = night ? 0.2f : 1.35f;
                sun.shadowStrength = night ? 0.45f : 0.82f;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = night
                ? new Color(0.045f, 0.07f, 0.13f)
                : new Color(0.42f, 0.53f, 0.66f);
            RenderSettings.ambientEquatorColor = night
                ? new Color(0.055f, 0.075f, 0.11f)
                : new Color(0.36f, 0.40f, 0.36f);
            RenderSettings.ambientGroundColor = night
                ? new Color(0.022f, 0.03f, 0.045f)
                : new Color(0.18f, 0.16f, 0.12f);
            RenderSettings.fogColor = night
                ? new Color(0.04f, 0.075f, 0.12f)
                : new Color(0.57f, 0.67f, 0.73f);
            RenderSettings.fogDensity = night ? 0.00145f : 0.0011f;

            if (runtimeSky != null)
            {
                runtimeSky.SetColor("_SkyTint", night
                    ? new Color(0.03f, 0.06f, 0.13f)
                    : new Color(0.32f, 0.48f, 0.68f));
                runtimeSky.SetColor("_GroundColor", night
                    ? new Color(0.02f, 0.03f, 0.05f)
                    : new Color(0.30f, 0.32f, 0.25f));
                runtimeSky.SetFloat("_Exposure", night ? 0.28f : 0.92f);
            }
            for (int i = 0; i < navigationLights.Count; i++)
                navigationLights[i].enabled = night;
            for (int i = 0; i < navigationLenses.Count; i++)
                navigationLenses[i].enabled = night;
            for (int i = 0; i < navigationFlashers.Count; i++)
                navigationFlashers[i].SetNight(night);
            foreach (NavigationLightRig rig in FindObjectsByType<NavigationLightRig>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                rig.SetNight(night);
            DynamicGI.UpdateEnvironment();
        }

        private void CreateNavigationAids()
        {
            GameObject navigation = GameObject.Find("Navigation");
            if (navigation == null) return;

            int buoyIndex = 0;
            foreach (Transform marker in navigation.transform)
            {
                bool isBuoy = marker.name.Contains("Buoy");
                Color color = marker.name.Contains("Right Red")
                    ? new Color(1f, 0.03f, 0.01f)
                    : marker.name.Contains("Left White")
                        ? new Color(0.02f, 1f, 0.14f)
                        : new Color(1f, 0.78f, 0.22f);
                float height = isBuoy ? 3.1f : 9f;
                GameObject beacon = new GameObject("Night Beacon");
                beacon.transform.SetParent(marker, false);
                beacon.transform.localPosition = new Vector3(0f, height, 0f);
                Light light = beacon.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.intensity = isBuoy ? 7.2f : 6f;
                light.range = isBuoy ? 58f : 55f;
                light.shadows = LightShadows.None;

                GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lens.name = "Beacon Lens";
                lens.transform.SetParent(beacon.transform, false);
                lens.transform.localScale = Vector3.one * (isBuoy ? 0.42f : 0.32f);
                Collider collider = lens.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                Material material = new Material(
                    Shader.Find("Universal Render Pipeline/Unlit"))
                {
                    color = color * (isBuoy ? 3.2f : 2.4f)
                };
                Renderer renderer = lens.GetComponent<Renderer>();
                renderer.material = material;
                navigationMaterials.Add(material);

                if (isBuoy)
                {
                    NavigationBeaconFlasher flasher =
                        beacon.AddComponent<NavigationBeaconFlasher>();
                    float phase = (buoyIndex % 5) * 0.21f +
                        (marker.name.Contains("Left White") ? 0.55f : 0f);
                    flasher.Configure(light, renderer, 1.5f, 0.32f, phase);
                    navigationFlashers.Add(flasher);
                    buoyIndex++;
                }
                else
                {
                    navigationLights.Add(light);
                    navigationLenses.Add(renderer);
                }
            }
        }

        private void OnDestroy()
        {
            if (runtimeSky != null) Destroy(runtimeSky);
            for (int i = 0; i < navigationMaterials.Count; i++)
                if (navigationMaterials[i] != null) Destroy(navigationMaterials[i]);
        }
    }
}
