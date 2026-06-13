using ShipSimulator.Physics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShipSimulator.CameraSystem
{
    public sealed class ShipFollowCamera : MonoBehaviour
    {
        [SerializeField] private ShipPhysicsController target;
        [SerializeField] private Vector3[] localViews =
        {
            new Vector3(28f, 18f, -58f),
            new Vector3(0f, 13f, -18f),
            new Vector3(0f, 68f, -22f),
            new Vector3(-34f, 12f, -12f),
            new Vector3(34f, 12f, -12f),
            new Vector3(0f, 9f, 58f),
            new Vector3(0f, 10f, -68f),
            new Vector3(-22f, 7f, -28f)
        };
        [SerializeField] private float smoothTime = 0.45f;
        [SerializeField] private float baseFieldOfView = 52f;
        [SerializeField] private float speedFieldOfViewGain = 1.2f;
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 3.5f, 10f);
        [Header("Mouse Orbit")]
        [SerializeField] private float orbitSensitivity = 0.12f;
        [SerializeField] private float minPitch = 6f;
        [SerializeField] private float maxPitch = 78f;
        [SerializeField] private float zoomSensitivity = 0.002f;
        [SerializeField] private float minDistance = 10f;
        [SerializeField] private float maxDistance = 140f;
        [Header("Navigator View")]
        [SerializeField] private Vector3 navigatorPosition = new Vector3(-1.8f, 14.2f, -20f);
        [SerializeField] private Vector3 navigatorLookOffset = new Vector3(-1.8f, 13.8f, 55f);
        [SerializeField] private float navigatorFieldOfView = 60f;
        private int viewIndex;
        private Vector3 velocity;
        private Camera controlledCamera;
        private float orbitYaw;
        private float orbitPitch;
        private float orbitDistance;
        public int ViewIndex => viewIndex;
        public int ViewCount => (localViews != null ? localViews.Length : 0) + 1;
        public string ViewName => GetViewName(viewIndex);
        private bool IsNavigatorView => viewIndex == ViewCount - 1;

        private void Awake()
        {
            controlledCamera = GetComponent<Camera>();
            ResetOrbitToView();
        }

        private void OnDisable()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;
            if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
                NextView();

            if (!IsNavigatorView) UpdateMouseOrbit();

            Vector3 desired = target.transform.TransformPoint(
                IsNavigatorView ? navigatorPosition : GetOrbitOffset());
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            Vector3 lookTarget = target.transform.TransformPoint(
                IsNavigatorView ? navigatorLookOffset : lookOffset);
            Quaternion desiredRotation =
                Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRotation, 1f - Mathf.Exp(-8f * Time.deltaTime));

            if (controlledCamera != null && target.Body != null)
            {
                float targetFov = IsNavigatorView
                    ? navigatorFieldOfView
                    : baseFieldOfView + target.Body.linearVelocity.magnitude * speedFieldOfViewGain;
                controlledCamera.fieldOfView = Mathf.Lerp(
                    controlledCamera.fieldOfView, targetFov, 1f - Mathf.Exp(-2f * Time.deltaTime));
            }
        }

        private void UpdateMouseOrbit()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else if (mouse.rightButton.wasReleasedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                orbitYaw += delta.x * orbitSensitivity;
                orbitPitch = Mathf.Clamp(
                    orbitPitch - delta.y * orbitSensitivity, minPitch, maxPitch);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                orbitDistance = Mathf.Clamp(
                    orbitDistance * Mathf.Exp(-scroll * zoomSensitivity),
                    minDistance,
                    maxDistance);
            }
        }

        private void ResetOrbitToView()
        {
            if (localViews == null || localViews.Length == 0) return;

            Vector3 offset = localViews[Mathf.Clamp(viewIndex, 0, localViews.Length - 1)];
            orbitDistance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);
            orbitYaw = Mathf.Atan2(offset.x, -offset.z) * Mathf.Rad2Deg;
            orbitPitch = Mathf.Clamp(
                Mathf.Asin(offset.y / Mathf.Max(offset.magnitude, 0.001f)) * Mathf.Rad2Deg,
                minPitch,
                maxPitch);
        }

        public void NextView()
        {
            if (ViewCount == 0) return;
            SetView((viewIndex + 1) % ViewCount);
        }

        public void SetView(int index)
        {
            if (ViewCount == 0) return;
            viewIndex = Mathf.Clamp(index, 0, ViewCount - 1);
            if (!IsNavigatorView) ResetOrbitToView();
        }

        public static string GetViewName(int index)
        {
            string[] names =
            {
                "CHASE", "BRIDGE", "TOP", "PORT", "STARBOARD", "BOW", "STERN", "DOCKING",
                "NAVIGATOR"
            };
            return index >= 0 && index < names.Length ? names[index] : $"VIEW {index + 1}";
        }

        private Vector3 GetOrbitOffset()
        {
            float yaw = orbitYaw * Mathf.Deg2Rad;
            float pitch = orbitPitch * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(pitch) * orbitDistance;

            return new Vector3(
                Mathf.Sin(yaw) * horizontal,
                Mathf.Sin(pitch) * orbitDistance,
                -Mathf.Cos(yaw) * horizontal);
        }
    }
}
