using UnityEngine;

namespace ShipSimulator.Visuals
{
    // Positions tied to one vessel model rather than to its physics: navigation light fixtures, the
    // navigator's eye point and the scale of the orbit cameras. Local frame: origin midship on the loaded
    // waterline, +z forward, +x starboard. The defaults are the Volgo-Don 507B model's.
    public sealed class VesselLayout : MonoBehaviour
    {
        [SerializeField] private string vesselId = "volgodon-507b";
        [SerializeField] private float cameraScale = 1f;
        [SerializeField] private Vector3 navigatorEye = new Vector3(-1.8f, 14.2f, -20f);
        [SerializeField] private Vector3 navigatorLookAt = new Vector3(-1.8f, 13.8f, 55f);
        [Header("Navigation lights (port side light; starboard is mirrored)")]
        [SerializeField] private Vector3 sideLight = new Vector3(-6.6f, 11.8f, -44f);
        [SerializeField] private float sideLightSupportBaseY = 10.7f;
        [SerializeField] private Vector3 forwardMastheadLight = new Vector3(0f, 14f, 42f);
        [SerializeField] private float forwardMastheadSupportBaseY = 6.8f;
        [SerializeField] private Vector3 aftMastheadLight = new Vector3(0f, 18f, -46f);
        [SerializeField] private float aftMastheadSupportBaseY = 12.4f;
        [SerializeField] private Vector3 sternLight = new Vector3(0f, 8.5f, -67f);
        [SerializeField] private float sternLightSupportBaseY = 6.6f;
        [Header("Orbit camera views, in model metres. Empty means the camera scales its own set by length.")]
        [SerializeField] private Vector3[] cameraViews = new Vector3[0];

        public string VesselId => vesselId;
        public float CameraScale => cameraScale;
        public Vector3 NavigatorEye => navigatorEye;
        public Vector3 NavigatorLookAt => navigatorLookAt;
        public Vector3 PortSideLight => sideLight;
        public Vector3 StarboardSideLight => new Vector3(-sideLight.x, sideLight.y, sideLight.z);
        public float SideLightSupportBaseY => sideLightSupportBaseY;
        public Vector3 ForwardMastheadLight => forwardMastheadLight;
        public float ForwardMastheadSupportBaseY => forwardMastheadSupportBaseY;
        public Vector3 AftMastheadLight => aftMastheadLight;
        public float AftMastheadSupportBaseY => aftMastheadSupportBaseY;
        public Vector3 SternLight => sternLight;
        public float SternLightSupportBaseY => sternLightSupportBaseY;
        public Vector3[] CameraViews => cameraViews;

        // A superstructure does not grow with the hull, so scaling the default views by length alone puts
        // the near views inside a short vessel's cabin. Models that know their own silhouette author the
        // views instead.
        public void ConfigureCameraViews(Vector3[] views) => cameraViews = views ?? new Vector3[0];

        public void Configure(string id, float scale, Vector3 eye, Vector3 lookAt,
            Vector3 portSideLight, float sideBaseY, Vector3 forwardMasthead, float forwardBaseY,
            Vector3 aftMasthead, float aftBaseY, Vector3 stern, float sternBaseY)
        {
            vesselId = id;
            cameraScale = scale;
            navigatorEye = eye;
            navigatorLookAt = lookAt;
            sideLight = portSideLight;
            sideLightSupportBaseY = sideBaseY;
            forwardMastheadLight = forwardMasthead;
            forwardMastheadSupportBaseY = forwardBaseY;
            aftMastheadLight = aftMasthead;
            aftMastheadSupportBaseY = aftBaseY;
            sternLight = stern;
            sternLightSupportBaseY = sternBaseY;
        }
    }
}
