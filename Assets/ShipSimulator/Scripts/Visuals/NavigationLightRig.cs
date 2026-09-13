using System.Collections.Generic;
using UnityEngine;

namespace ShipSimulator.Visuals
{
    public sealed class NavigationLightRig : MonoBehaviour
    {
        private readonly List<Light> lights = new List<Light>();
        private readonly List<Renderer> lenses = new List<Renderer>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<GameObject> generatedObjects = new List<GameObject>();
        private Material fixtureMaterial;
        private readonly List<Cubemap> cookies = new List<Cubemap>();

        private void Awake()
        {
            EnsureCreated();
        }

        public void EnsureCreated()
        {
            if (lights.Count > 0) return;
            fixtureMaterial = new Material(
                Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.055f, 0.07f, 0.075f)
            };
            materials.Add(fixtureMaterial);

            CreateLight("Port Navigation Light", new Vector3(-6.6f, 11.8f, -44f),
                10.7f, new Color(1f, 0.03f, 0.02f), 5.5f, 45f, -56.25f, 112.5f);
            CreateLight("Starboard Navigation Light", new Vector3(6.6f, 11.8f, -44f),
                10.7f, new Color(0.02f, 1f, 0.18f), 5.5f, 45f, 56.25f, 112.5f);
            CreateLight("Forward Masthead Light", new Vector3(0f, 14f, 42f),
                6.8f, Color.white, 6f, 70f, 0, 225);
            CreateLight("Aft Masthead Light", new Vector3(0f, 18f, -46f),
                12.4f, Color.white, 6f, 70f, 0, 225);
            CreateLight("Stern Light", new Vector3(0f, 8.5f, -67f),
                6.6f, Color.white, 4.5f, 45f, 180, 135);
            SetNight(false);
        }

        public void SetNight(bool enabled)
        {
            for (int i = 0; i < lights.Count; i++)
                lights[i].enabled = enabled;
            for (int i = 0; i < lenses.Count; i++)
                lenses[i].enabled = enabled;
        }

        public void RemoveGeneratedObjects()
        {
            for (int i = generatedObjects.Count - 1; i >= 0; i--)
                if (generatedObjects[i] != null)
                    DestroyRuntimeObject(generatedObjects[i]);
            generatedObjects.Clear();
            lights.Clear();
            lenses.Clear();
            ReleaseMaterials();
        }

        private void CreateLight(
            string lightName, Vector3 localPosition, float supportBaseY,
            Color color, float intensity, float range, float sectorCenter, float sectorArc)
        {
            CreateFixture(lightName, localPosition, supportBaseY);

            GameObject lightObject = new GameObject(lightName);
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = localPosition;
            generatedObjects.Add(lightObject);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.enabled = false;
            Cubemap cookie = NavigationLightSectors.CreateCookie(sectorCenter, sectorArc);
            cookies.Add(cookie);
            light.cookie = cookie;
            lights.Add(light);

            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lens.name = "Lens";
            Collider collider = lens.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                DestroyRuntimeObject(collider);
            }
            lens.transform.SetParent(lightObject.transform, false);
            lens.transform.localPosition = Vector3.up * 0.28f;
            lens.transform.localScale = Vector3.one * 0.42f;
            Material material = new Material(Resources.Load<Shader>("NavigationSectorLight"))
            {
                color = color * 2.2f
            };
            material.SetFloat("_SectorCenter", sectorCenter);
            material.SetFloat("_SectorArc", sectorArc);
            lens.GetComponent<Renderer>().material = material;
            lens.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lens.GetComponent<Renderer>().enabled = false;
            lenses.Add(lens.GetComponent<Renderer>());
            materials.Add(material);
        }

        private void CreateFixture(
            string lightName, Vector3 localPosition, float supportBaseY)
        {
            float supportHeight = Mathf.Max(0.25f, localPosition.y - supportBaseY);
            GameObject fixture = new GameObject(lightName + " Fixture");
            fixture.transform.SetParent(transform, false);
            generatedObjects.Add(fixture);

            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Support";
            pole.transform.SetParent(fixture.transform, false);
            pole.transform.localPosition = new Vector3(
                localPosition.x, supportBaseY + supportHeight * 0.5f,
                localPosition.z);
            pole.transform.localScale = new Vector3(0.11f, supportHeight * 0.5f, 0.11f);
            ConfigureFixturePart(pole);

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Mounting Bracket";
            platform.transform.SetParent(fixture.transform, false);
            platform.transform.localPosition = localPosition + Vector3.down * 0.18f;
            platform.transform.localScale = new Vector3(0.72f, 0.12f, 0.58f);
            ConfigureFixturePart(platform);

            GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            housing.name = "Lantern Housing";
            housing.transform.SetParent(fixture.transform, false);
            housing.transform.localPosition = localPosition;
            housing.transform.localScale = new Vector3(0.34f, 0.24f, 0.34f);
            ConfigureFixturePart(housing);
        }

        private void ConfigureFixturePart(GameObject part)
        {
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) DestroyRuntimeObject(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = fixtureMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private void OnDestroy() => ReleaseMaterials();

        private void ReleaseMaterials()
        {
            foreach (Cubemap cookie in cookies) if (cookie != null) DestroyRuntimeObject(cookie);
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null) DestroyRuntimeObject(materials[i]);
            materials.Clear();
            cookies.Clear();
            fixtureMaterial = null;
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }
    }
}
