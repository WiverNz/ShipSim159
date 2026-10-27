using UnityEngine;
using UnityEngine.InputSystem;

namespace ShipSimulator.CameraSystem
{
    // Camera-space state only; pixels and wheel ticks are integrated independently of frame time.
    public sealed class CameraOrbit
    {
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Distance => Mathf.Exp(logDistance);
        public float TargetYaw { get; private set; }
        public float TargetPitch { get; private set; }
        public float TargetDistance => Mathf.Exp(targetLogDistance);
        private float logDistance, targetLogDistance;
        private float minimumLog, maximumLog, minimumPitch, maximumPitch;

        public void Configure(float near, float far, float lowPitch, float highPitch)
        {
            minimumLog = Mathf.Log(Mathf.Max(.1f, near));
            maximumLog = Mathf.Log(Mathf.Max(near, far));
            minimumPitch = lowPitch;
            maximumPitch = highPitch;
        }

        public void Reset(float yaw, float pitch, float distance)
        {
            TargetYaw = Yaw = Wrap(Finite(yaw) ? yaw : 0);
            TargetPitch = Pitch = Mathf.Clamp(Finite(pitch) ? pitch : 30, minimumPitch, maximumPitch);
            targetLogDistance = logDistance = Mathf.Clamp(
                Finite(distance) && distance > 0 ? Mathf.Log(distance) : minimumLog, minimumLog, maximumLog);
        }

        public void Reset(Vector3 offset)
        {
            float radius = offset.magnitude;
            Reset(Mathf.Atan2(offset.x, -offset.z) * Mathf.Rad2Deg,
                Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(radius, .001f), -1, 1)) * Mathf.Rad2Deg, radius);
        }

        public void AddInput(Vector2 pixels, float ticks, float degreesPerPixel, float zoomPerTick)
        {
            if (Finite(pixels.x)) TargetYaw = Wrap(TargetYaw + pixels.x * degreesPerPixel);
            if (Finite(pixels.y)) TargetPitch = Mathf.Clamp(TargetPitch - pixels.y * degreesPerPixel, minimumPitch, maximumPitch);
            if (Finite(ticks)) targetLogDistance = Mathf.Clamp(targetLogDistance - ticks * zoomPerTick, minimumLog, maximumLog);
        }

        public void Step(float realDeltaTime, float responseTime)
        {
            float weight = Weight(realDeltaTime, responseTime);
            Yaw = Wrap(Yaw + Mathf.DeltaAngle(Yaw, TargetYaw) * weight);
            Pitch = Mathf.Lerp(Pitch, TargetPitch, weight);
            logDistance = Mathf.Lerp(logDistance, targetLogDistance, weight);
        }

        public void CancelMotion()
        {
            TargetYaw = Yaw;
            TargetPitch = Pitch;
            targetLogDistance = logDistance;
        }

        public Vector3 Offset => ToOffset(Yaw, Pitch, Distance);
        public static Vector3 ToOffset(float yaw, float pitch, float distance)
        {
            float horizontal = Mathf.Cos(pitch * Mathf.Deg2Rad) * distance;
            return new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad) * horizontal,
                Mathf.Sin(pitch * Mathf.Deg2Rad) * distance, -Mathf.Cos(yaw * Mathf.Deg2Rad) * horizontal);
        }

        public static float WheelTicks(float value, InputSettings.ScrollDeltaBehavior behavior, RuntimePlatform platform)
        {
            if (!Finite(value)) return 0;
            bool nativeWindows = behavior == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange &&
                (platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.WindowsEditor);
            return nativeWindows ? value / 120f : value;
        }

        public static float Weight(float dt, float responseTime) =>
            !Finite(dt) || dt <= 0 ? 0 : 1 - Mathf.Exp(-dt / Mathf.Max(.001f, responseTime));
        private static float Wrap(float angle) => Mathf.Repeat(angle + 180, 360) - 180;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
