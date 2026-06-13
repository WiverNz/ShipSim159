using ShipSimulator.Physics;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShipSimulator.Visuals
{
    [RequireComponent(typeof(ShipPhysicsController))]
    public sealed class ShipWakeController : MonoBehaviour
    {
        [SerializeField] private float waterLevel = 0.08f;
        [SerializeField] private float wakeTime = 13f;
        [SerializeField] private float emissionSpeedMps = 0.12f;

        private ShipPhysicsController ship;
        private TrailRenderer[] trails;
        private Material wakeMaterial;
        private float[] baseWidths;

        private void Awake()
        {
            ship = GetComponent<ShipPhysicsController>();
            wakeMaterial = CreateWakeMaterial();
            trails = new[]
            {
                CreateTrail("Port Propeller Wash", new Vector3(-4.2f, 0f, -58f)),
                CreateTrail("Starboard Propeller Wash", new Vector3(4.2f, 0f, -58f)),
                CreateTrail("Port Hull Wake", new Vector3(-8.5f, 0f, -38f), 1.35f),
                CreateTrail("Starboard Hull Wake", new Vector3(8.5f, 0f, -38f), 1.35f),
                CreateTrail("Port Bow Wave", new Vector3(-6.8f, 0f, 55f), 0.85f),
                CreateTrail("Starboard Bow Wave", new Vector3(6.8f, 0f, 55f), 0.85f)
            };
            baseWidths = new float[trails.Length];
            for (int i = 0; i < trails.Length; i++)
                baseWidths[i] = trails[i].startWidth;
        }

        private void LateUpdate()
        {
            if (ship == null || ship.Body == null || trails == null) return;

            float speed = ship.Body.linearVelocity.magnitude;
            float throttle = Mathf.Abs(ship.ActualThrottle);
            for (int i = 0; i < trails.Length; i++)
            {
                TrailRenderer trail = trails[i];
                Vector3 position = trail.transform.position;
                position.y = waterLevel;
                trail.transform.position = position;
                bool propellerWash = i < 2;
                trail.emitting = propellerWash
                    ? speed > emissionSpeedMps || throttle > 0.08f
                    : speed > emissionSpeedMps;
                float intensity = propellerWash
                    ? Mathf.Clamp01(Mathf.Max(speed / 4f, throttle))
                    : Mathf.Clamp01(speed / 5f);
                trail.startWidth = baseWidths[i] * Mathf.Lerp(0.8f, 1.6f, intensity);
                trail.startColor = new Color(0.78f, 0.94f, 1f,
                    Mathf.Lerp(0.2f, 0.88f, intensity));
            }
        }

        private TrailRenderer CreateTrail(string trailName, Vector3 localPosition,
            float widthMultiplier = 1f)
        {
            GameObject trailObject = new GameObject(trailName);
            trailObject.transform.SetParent(transform, false);
            trailObject.transform.localPosition = localPosition;
            TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
            trail.material = wakeMaterial;
            trail.time = wakeTime;
            trail.minVertexDistance = 0.45f;
            trail.startWidth = 3.2f * widthMultiplier;
            trail.endWidth = 0.15f;
            trail.startColor = new Color(0.78f, 0.94f, 1f, 0.68f);
            trail.endColor = new Color(0.48f, 0.76f, 0.84f, 0f);
            trail.textureMode = LineTextureMode.Stretch;
            trail.alignment = LineAlignment.View;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            return trail;
        }

        private static Material CreateWakeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader)
            {
                name = "Runtime Wake Material",
                color = Color.white
            };
            return material;
        }

        private void OnDestroy()
        {
            if (wakeMaterial != null) Destroy(wakeMaterial);
        }
    }
}
