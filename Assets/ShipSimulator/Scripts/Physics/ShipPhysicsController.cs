using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShipSimulator.Physics
{
    [RequireComponent(typeof(Rigidbody), typeof(VesselDataLoader), typeof(HydrodynamicResistance))]
    public sealed class ShipPhysicsController : MonoBehaviour
    {
        [Header("Environment")]
        [SerializeField] private float waterLevel;
        [SerializeField] private Vector3 ambientCurrentMps = new Vector3(0f, 0f, 0.35f);
        [SerializeField] private Vector3 windVelocityMps;
        [SerializeField] private float windForceCoefficient = 800f;
        [Header("Debug")]
        [SerializeField] private bool drawDebugForces = true;

        private Rigidbody body;
        private VesselData data;
        private PropulsionController propulsion;
        private RudderController rudder;
        private HydrodynamicResistance resistance;
        private BuoyancyPoint[] buoyancyPoints;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float throttleCommand;
        private float rudderCommand;
        private readonly HashSet<RiverCurrentZone> activeCurrentZones = new HashSet<RiverCurrentZone>();

        public Rigidbody Body => body;
        public VesselData Data => data;
        public float ThrottleCommand => throttleCommand;
        public float RudderCommand => rudderCommand;
        public float ActualThrottle => propulsion != null ? propulsion.ActualThrottle : 0f;
        public float RudderAngleDeg => rudder != null ? rudder.AngleDeg : 0f;
        public Vector3 RelativeWaterVelocity => resistance != null ? resistance.RelativeWaterVelocity : Vector3.zero;
        public Vector3 EffectiveCurrentMps => CalculateEffectiveCurrent();
        public float LoadFraction => data != null ? Mathf.Clamp01(data.massProperties.loadFraction) : 0f;
        public float CurrentMassKg => body != null ? body.mass : 0f;
        public float EstimatedDraftM => data != null
            ? Mathf.Lerp(0.9f, data.dimensions.loadedDraftM, LoadFraction)
            : 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            data = GetComponent<VesselDataLoader>().Load();
            resistance = GetComponent<HydrodynamicResistance>();
            propulsion = GetComponentInChildren<PropulsionController>();
            rudder = GetComponentInChildren<RudderController>();
            buoyancyPoints = GetComponentsInChildren<BuoyancyPoint>();
            startPosition = transform.position;
            startRotation = transform.rotation;

            if (data == null)
            {
                enabled = false;
                return;
            }
            if (propulsion == null || rudder == null || buoyancyPoints.Length == 0)
            {
                Debug.LogError(
                    "Ship requires propulsion, rudder and at least one buoyancy point.", this);
                enabled = false;
                return;
            }

            body.mass = Mathf.Lerp(data.massProperties.lightshipMassKg, data.massProperties.loadedMassKg, LoadFraction);
            body.centerOfMass = data.massProperties.centerOfMassLocalM;
            body.inertiaTensor = data.massProperties.inertiaTensorKgM2;
            body.useGravity = true;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.maxAngularVelocity = 1f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.solverIterations = 12;
            body.solverVelocityIterations = 4;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            float throttleInput = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float rudderInput = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            throttleCommand = Mathf.Clamp(throttleCommand + throttleInput * data.controlLimits.throttleCommandRatePerSecond * Time.deltaTime, -1f, 1f);
            rudderCommand = Mathf.MoveTowards(rudderCommand, rudderInput, data.controlLimits.rudderCommandRatePerSecond * Time.deltaTime);
            if (keyboard.spaceKey.wasPressedThisFrame) throttleCommand = 0f;
            if (keyboard.rKey.wasPressedThisFrame) ResetVessel();
        }

        private void FixedUpdate()
        {
            Vector3 current = CalculateEffectiveCurrent();
            Vector3 waterVelocity = body.linearVelocity - current;
            resistance.Apply(body, data, current);
            propulsion.Step(body, data, throttleCommand, Time.fixedDeltaTime);
            rudder.Step(body, data, rudderCommand, waterVelocity, Time.fixedDeltaTime);

            float totalWeight = body.mass * UnityEngine.Physics.gravity.magnitude;
            float pointForce = totalWeight * data.buoyancy.reserveBuoyancyFactor / Mathf.Max(1, buoyancyPoints.Length);
            foreach (BuoyancyPoint point in buoyancyPoints)
                point.Apply(body, waterLevel + data.buoyancy.waterlineLocalY,
                    pointForce * data.calibration.buoyancyMultiplier,
                    data.buoyancy.maxPointDepthM, data.buoyancy.verticalDampingNPerMpsPerPoint);

            Vector3 relativeWind = windVelocityMps - body.linearVelocity;
            body.AddForce(relativeWind * relativeWind.magnitude * windForceCoefficient);
        }

        private void ResetVessel()
        {
            body.position = startPosition;
            body.rotation = startRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            throttleCommand = 0f;
            rudderCommand = 0f;
        }

        public void SetThrottleCommand(float value)
        {
            throttleCommand = Mathf.Clamp(value, -1f, 1f);
        }

        public void SetRudderCommand(float value)
        {
            rudderCommand = Mathf.Clamp(value, -1f, 1f);
        }

        public void CenterRudder()
        {
            rudderCommand = 0f;
        }

        public void ResetToStart()
        {
            ResetVessel();
        }

        public void RegisterCurrentZone(RiverCurrentZone zone)
        {
            if (zone != null) activeCurrentZones.Add(zone);
        }

        public void UnregisterCurrentZone(RiverCurrentZone zone)
        {
            if (zone != null) activeCurrentZones.Remove(zone);
        }

        private Vector3 CalculateEffectiveCurrent()
        {
            activeCurrentZones.RemoveWhere(zone => zone == null || !zone.isActiveAndEnabled);
            if (activeCurrentZones.Count == 0) return ambientCurrentMps;

            Vector3 total = Vector3.zero;
            foreach (RiverCurrentZone zone in activeCurrentZones)
                total += zone.CurrentVelocityMps;
            return total / activeCurrentZones.Count;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugForces || body == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawRay(body.worldCenterOfMass, body.linearVelocity * 5f);
            Gizmos.color = Color.red;
            if (propulsion != null) Gizmos.DrawRay(propulsion.transform.position, propulsion.LastForce / 50000f);
            Gizmos.color = Color.yellow;
            if (rudder != null) Gizmos.DrawRay(rudder.transform.position, rudder.LastForce / 50000f);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(body.worldCenterOfMass, 0.8f);
        }
    }
}
