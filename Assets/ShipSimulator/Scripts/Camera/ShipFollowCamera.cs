using ShipSimulator.Physics;
using ShipSimulator.Visuals;
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
        [SerializeField] private float trackingTime = 0.12f;
        [SerializeField] private float orbitResponseTime = 0.085f;
        [SerializeField] private float baseFieldOfView = 52f;
        [SerializeField] private float speedFieldOfViewGain = 1.2f;
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 3.5f, 10f);
        [Header("Mouse Orbit")]
        [SerializeField] private float orbitSensitivity = 0.06f;
        [SerializeField] private float minPitch = 0f;
        [SerializeField] private float maxPitch = 90f;
        [SerializeField] private float zoomPerTick = 0.22314355f;
        [SerializeField] private float minDistance = 10f;
        [SerializeField] private float maxDistance = 140f;
        [Header("Navigator View")]
        [SerializeField] private Vector3 navigatorPosition = new Vector3(-1.8f, 14.2f, -20f);
        [SerializeField] private Vector3 navigatorLookOffset = new Vector3(-1.8f, 13.8f, 55f);
        [SerializeField] private float navigatorFieldOfView = 60f;
        private readonly CameraOrbit orbit = new CameraOrbit();
        private int viewIndex, restoreCount;
        private Camera controlledCamera;
        private VesselLayout layout;
        private bool orbitGesture, skipPointerFrame = true, focused = true, suspended;
        private Vector3 trackedPivot;
        private float trackedHeading;
        public int ViewIndex => viewIndex;
        public int ViewCount => (localViews != null ? localViews.Length : 0) + 1;
        public string ViewName => GetViewName(viewIndex);
        public bool OwnsOrbitGesture => orbitGesture;
        public ShipPhysicsController Target => target;
        private bool IsNavigatorView => viewIndex == ViewCount - 1;
        private float Scale => layout != null ? Mathf.Max(.01f, layout.CameraScale) : 1f;
        private Vector3[] ModelViews => layout != null && layout.CameraViews != null && localViews != null &&
            layout.CameraViews.Length == localViews.Length ? layout.CameraViews : null;
        private Vector3 NavigatorPosition => layout != null ? layout.NavigatorEye : navigatorPosition;
        private Vector3 NavigatorLook => layout != null ? layout.NavigatorLookAt : navigatorLookOffset;
        // The authored bridge preset looks along the river; other orbit presets frame the vessel.
        private Vector3 Pivot => viewIndex == 1 && ModelViews != null ? NavigatorLook :
            layout != null ? layout.ExternalLookAtOr(lookOffset * Scale) : lookOffset * Scale;
        private Quaternion HeadingFrame => Quaternion.Euler(0, target.transform.eulerAngles.y, 0);
        private float FarDistance => Mathf.Max(maxDistance * Scale, (ViewOffset() - Pivot).magnitude);

        private void Awake() => SetTarget(target);
        private void OnEnable() { skipPointerFrame = true; if (target != null) SnapPose(); }
        private void OnDisable() => Suspend();
        private void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value) Suspend();
            else skipPointerFrame = true;
        }

        public void SetTarget(ShipPhysicsController ship)
        {
            ReleaseOrbit();
            target = ship;
            controlledCamera = GetComponent<Camera>();
            layout = ship != null ? ship.GetComponent<VesselLayout>() : null;
            restoreCount = ship != null ? ship.RestoreCount : 0;
            viewIndex = Mathf.Clamp(viewIndex, 0, ViewCount - 1);
            ResetOrbitToView();
            SnapPose();
        }

        private void ReleaseOrbit()
        {
            if (orbitGesture)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            orbitGesture = false;
            skipPointerFrame = true;
        }

        private void Suspend()
        {
            ReleaseOrbit();
            orbit.CancelMotion();
            suspended = true;
        }

        private void LateUpdate()
        {
            if (!focused || ShipSimulator.UI.VoyageMenu.IsOpen)
            {
                if (!suspended) Suspend();
                return;
            }
            if (target == null) { ReleaseOrbit(); return; }
            if (suspended) { suspended = false; skipPointerFrame = true; }
            if (restoreCount != target.RestoreCount)
            {
                restoreCount = target.RestoreCount;
                ReleaseOrbit();
                orbit.CancelMotion();
                SnapPose();
            }
            if (!ShipSimulator.UI.GameplayInput.KeyboardBlocked && Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
                NextView();
            if (!IsNavigatorView) UpdateMouseOrbit();
            Advance(Time.unscaledDeltaTime);
        }

        // Presentation step is separate from device sampling and never moves the simulated vessel.
        public void Advance(float realDeltaTime)
        {
            if (target == null) return;
            orbit.Step(realDeltaTime, orbitResponseTime);
            float weight = CameraOrbit.Weight(realDeltaTime, trackingTime);
            trackedPivot = Vector3.Lerp(trackedPivot, target.transform.position + HeadingFrame * Pivot, weight);
            trackedHeading += Mathf.DeltaAngle(trackedHeading, target.transform.eulerAngles.y) * weight;
            ApplyPose();
            if (controlledCamera != null)
                controlledCamera.fieldOfView = Mathf.Lerp(controlledCamera.fieldOfView, DesiredFieldOfView,
                    CameraOrbit.Weight(realDeltaTime, .625f));
        }

        private float DesiredFieldOfView => IsNavigatorView ? navigatorFieldOfView : Mathf.Clamp(
            baseFieldOfView + (target != null && target.Body != null ? target.Body.linearVelocity.magnitude : 0) * speedFieldOfViewGain, 20, 100);

        private void ApplyPose()
        {
            Vector3 look;
            if (IsNavigatorView)
            {
                transform.position = target.transform.TransformPoint(NavigatorPosition);
                look = target.transform.TransformPoint(NavigatorLook);
            }
            else
            {
                transform.position = trackedPivot + Quaternion.Euler(0, trackedHeading, 0) * orbit.Offset;
                // Analytic orientation remains defined at the authored overhead (90 degree) view.
                transform.rotation = Quaternion.Euler(orbit.Pitch, trackedHeading - orbit.Yaw, 0);
                return;
            }
            if ((look - transform.position).sqrMagnitude > .000001f)
                transform.rotation = Quaternion.LookRotation(look - transform.position, Vector3.up);
        }

        private void SnapPose()
        {
            if (target == null) return;
            trackedPivot = target.transform.position + HeadingFrame * Pivot;
            trackedHeading = target.transform.eulerAngles.y;
            ApplyPose();
            if (controlledCamera != null) controlledCamera.fieldOfView = DesiredFieldOfView;
            var temporal = GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (temporal != null) temporal.resetHistory = true;
        }

        private void UpdateMouseOrbit()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) { ReleaseOrbit(); return; }
            if (skipPointerFrame) { skipPointerFrame = false; return; }
            if (orbitGesture && !mouse.rightButton.isPressed)
            {
                ReleaseOrbit();
                skipPointerFrame = false;
            }
            bool overUi = !orbitGesture && ShipSimulator.UI.GameplayInput.PointerOverUi();
            if (mouse.rightButton.wasPressedThisFrame && !overUi)
            {
                orbitGesture = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                // Discard motion accumulated before the gesture/lock began.
                return;
            }
            if (!orbitGesture && (overUi || mouse.rightButton.isPressed)) return;
            Vector2 pixels = orbitGesture ? mouse.delta.ReadValue() : Vector2.zero;
            float ticks = CameraOrbit.WheelTicks(mouse.scroll.y.ReadValue(), InputSystem.settings.scrollDeltaBehavior, Application.platform);
            orbit.AddInput(pixels, ticks, orbitSensitivity * ShipSimulator.UI.VoyageSettings.CameraSensitivity,
                zoomPerTick * ShipSimulator.UI.VoyageSettings.ZoomSensitivity);
        }

        private Vector3 ViewOffset()
        {
            if (localViews == null || localViews.Length == 0) return new Vector3(0, 18, -58) * Scale;
            int index = Mathf.Clamp(viewIndex, 0, localViews.Length - 1);
            return ModelViews != null ? ModelViews[index] : localViews[index] * Scale;
        }

        private void ResetOrbitToView()
        {
            orbit.Configure(minDistance * Scale, FarDistance, minPitch, maxPitch);
            orbit.Reset(ViewOffset() - Pivot);
        }

        public ShipSimulator.Persistence.CameraSave CaptureState() => new ShipSimulator.Persistence.CameraSave
        {
            orbitVersion = 1, view = viewIndex, yaw = orbit.Yaw, pitch = orbit.Pitch, distance = orbit.Distance
        };

        public void RestoreState(ShipSimulator.Persistence.CameraSave save)
        {
            if (save == null) return;
            SetView(save.view);
            if (save.orbitVersion == 0)
                orbit.Reset(CameraOrbit.ToOffset(save.yaw, save.pitch, save.distance) - Pivot);
            else orbit.Reset(save.yaw, save.pitch, save.distance);
            restoreCount = target != null ? target.RestoreCount : 0;
            SnapPose();
        }

        public void NextView() => SetView((viewIndex + 1) % ViewCount);

        public void SetView(int index)
        {
            ReleaseOrbit();
            viewIndex = Mathf.Clamp(index, 0, ViewCount - 1);
            ResetOrbitToView();
            SnapPose();
        }

        public static string GetViewName(int index)
        {
            string[] names =
            {
                "CHASE", "BRIDGE", "TOP", "PORT", "STARBOARD", "BOW", "STERN", "DOCKING",
                "NAVIGATOR"
            };
            return index >= 0 && index < names.Length ? ShipSimulator.UI.Localization.Source(names[index]) : $"VIEW {index + 1}";
        }

    }
}
