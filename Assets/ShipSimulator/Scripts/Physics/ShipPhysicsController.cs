using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShipSimulator.Physics
{
    // Drives the vessel Rigidbody from the manoeuvring model. Surge, sway and yaw come from the MMG solve with
    // added mass and are applied as accelerations, so the Rigidbody's single scalar mass does not replace the
    // per-axis added mass. Heave, roll and pitch come from station hydrostatics applied as forces.
    [RequireComponent(typeof(Rigidbody), typeof(VesselDataLoader))]
    public sealed class ShipPhysicsController : MonoBehaviour
    {
        private const int BankSamplesPerSide = 8;

        [Header("Environment")]
        [SerializeField] private float waterLevel;
        [SerializeField] private Vector3 ambientCurrentMps = new Vector3(0f, 0f, 0.35f);
        [SerializeField] private Vector3 windVelocityMps;
        [Header("Debug")]
        [SerializeField] private bool drawDebugForces = true;

        private Rigidbody body;
        private VesselData data;
        private VesselParameters parameters;
        private ManoeuvringModel model;
        private HydrostaticsModel hydrostatics;
        private float[] engineCommands = Array.Empty<float>();
        private float[] stationCurrent;
        private float rudderCommand;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private readonly HashSet<RiverCurrentZone> activeCurrentZones = new HashSet<RiverCurrentZone>();
        private CurrentFieldProvider currentField;
        private ScenarioBathymetry bathymetry;
        private GroundingController grounding;
        private Func<Vector3, float> depthProvider;
        private float externalX;
        private float externalY;
        private float externalN;
        private ManoeuvringOutput diagnostics;
        private Vector3 relativeWaterVelocity;
        private float effectiveDraft;

        public Rigidbody Body => body;
        public VesselData Data => data;
        public VesselParameters Parameters => parameters;
        public ManoeuvringModel Model => model;
        public HydrostaticsModel Hydrostatics => hydrostatics;
        public ManoeuvringOutput Diagnostics => diagnostics;
        // Test and trial harnesses step the vessel themselves with Simulate.
        public bool ManualStepping { get; set; }
        public int EngineCount => engineCommands.Length;
        public float ThrottleCommand => Mean(engineCommands);
        public float RudderCommand => rudderCommand;
        public float ActualThrottle
        {
            get
            {
                if (model == null || model.Shafts.Length == 0) return 0f;
                float sum = 0f;
                foreach (EngineShaft shaft in model.Shafts) sum += shaft.Rps / shaft.RatedRps;
                return Mathf.Clamp(sum / model.Shafts.Length, -1f, 1f);
            }
        }
        public float RudderAngleDeg => model != null ? model.RudderAngleRad * Mathf.Rad2Deg : 0f;
        public Vector3 RelativeWaterVelocity => relativeWaterVelocity;
        public Vector3 EffectiveCurrentMps => CalculateEffectiveCurrent(transform.position);
        public float LoadFraction => parameters != null ? parameters.LoadFraction : 0f;
        public float CurrentMassKg => body != null ? body.mass : 0f;
        // Still-water draft for the loading condition.
        public float EstimatedDraftM => parameters != null ? parameters.Draft : 0f;
        public float EstimatedSquatM => Mathf.Max(diagnostics.BowSquatM, diagnostics.SternSquatM);
        // Deepest keel point below the still water surface, including squat, trim and heel.
        public float EffectiveDraftM => effectiveDraft;
        public GroundingController Grounding => grounding;
        public Vector3 WindVelocityMps => windVelocityMps;
        public float WaterLevel => waterLevel;

        public float EngineCommand(int index) => engineCommands[index];
        public float ShaftRpm(int index) => model != null ? model.Shafts[index].Rps * 60f : 0f;
        public float EngineLoadFraction(int index) => model != null ? model.Shafts[index].LoadFraction : 0f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            data = GetComponent<VesselDataLoader>().Load();
            startPosition = transform.position;
            startRotation = transform.rotation;
            currentField = FindAnyObjectByType<CurrentFieldProvider>();
            bathymetry = FindAnyObjectByType<ScenarioBathymetry>();
            grounding = GetComponent<GroundingController>();
            if (data == null)
            {
                enabled = false;
                return;
            }
            ConfigureLoading(data.massProperties.loadFraction);
        }

        public void ConfigureLoading(float loadFraction)
        {
            parameters = VesselParameters.Create(data, loadFraction);
            model = new ManoeuvringModel(parameters);
            hydrostatics = new HydrostaticsModel(parameters);
            engineCommands = new float[parameters.Data.propeller.count];
            stationCurrent = new float[model.Hull.StationCount];
            effectiveDraft = parameters.Draft;

            body.mass = parameters.Mass;
            body.centerOfMass = data.massProperties.centerOfMassLocalM;
            // Unity body axes: x pitches, y yaws, z rolls.
            body.inertiaTensor = new Vector3(parameters.PitchInertia, parameters.YawInertiaAtG, parameters.RollInertia);
            body.inertiaTensorRotation = Quaternion.identity;
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
            if (ShipSimulator.UI.VoyageMenu.IsOpen || data == null) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            float throttleInput = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float rudderInput = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float step = throttleInput * data.controlLimits.throttleCommandRatePerSecond * Time.deltaTime;
            for (int i = 0; i < engineCommands.Length; i++)
                engineCommands[i] = Mathf.Clamp(engineCommands[i] + step, -1f, 1f);
            rudderCommand = Mathf.MoveTowards(rudderCommand, rudderInput, data.controlLimits.rudderCommandRatePerSecond * Time.deltaTime);
            if (keyboard.spaceKey.wasPressedThisFrame) SetThrottleCommand(0f);
            if (keyboard.rKey.wasPressedThisFrame) ResetVessel();
        }

        private void FixedUpdate()
        {
            if (!ManualStepping) Simulate(Time.fixedDeltaTime);
        }

        public void Simulate(float dt)
        {
            if (model == null) return;
            if (grounding != null && grounding.isActiveAndEnabled) grounding.Step(dt);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 midship = transform.position;

            Vector3 current = CalculateEffectiveCurrent(midship);
            Vector3 shipVelocity = body.GetPointVelocity(midship);
            relativeWaterVelocity = shipVelocity - current;
            relativeWaterVelocity.y = 0f;
            float depth = SampleDepth(midship);
            SampleBankFlowAreas(midship, right, depth, out float portArea, out float starboardArea);
            Vector3 air = windVelocityMps - shipVelocity;

            var input = new ManoeuvringInput
            {
                SurgeSpeed = Vector3.Dot(relativeWaterVelocity, forward),
                SwaySpeed = Vector3.Dot(relativeWaterVelocity, right),
                YawRate = body.angularVelocity.y,
                EngineCommands = engineCommands,
                RudderCommand = rudderCommand,
                RelativeWind = new Vector2(Vector3.Dot(air, forward), Vector3.Dot(air, right)),
                DepthM = depth,
                PortFlowAreaM2 = portArea,
                StarboardFlowAreaM2 = starboardArea,
                StationSwayCurrent = SampleCurrentShear(midship, forward, right, current),
                ExternalX = externalX,
                ExternalY = externalY,
                ExternalN = externalN
            };
            externalX = externalY = externalN = 0f;
            diagnostics = model.Step(input, dt);

            body.AddForce(forward * diagnostics.GravityCentreAccelerationX + right * diagnostics.GravityCentreAccelerationY,
                ForceMode.Acceleration);
            body.AddTorque(Vector3.up * diagnostics.YawAcceleration, ForceMode.Acceleration);

            // Roll moment about G: lateral hull forces act near half draft, wind at its centroid height.
            // Positive torque about forward heels to port in Unity's frame.
            float turningLever = parameters.CentreOfGravityAboveKeel - 0.5f * parameters.Draft;
            float windLever = parameters.WindCentroidHeight + 0.5f * parameters.Draft;
            float heel = parameters.Mass * diagnostics.GravityCentreAccelerationY * turningLever - diagnostics.WindY * windLever;
            body.AddTorque(forward * heel, ForceMode.Force);

            hydrostatics.Apply(body, transform, waterLevel, diagnostics.BowSquatM, diagnostics.SternSquatM);
            effectiveDraft = Mathf.Max(KeelDepth(0.5f * parameters.Lpp), KeelDepth(0f), KeelDepth(-0.5f * parameters.Lpp));
        }

        public float SampleDepth(Vector3 worldPosition)
        {
            if (depthProvider != null) return depthProvider(worldPosition);
            return bathymetry != null ? bathymetry.Sample(worldPosition).DepthM : FairwayModel.DepthAt(worldPosition);
        }

        public void SetDepthProvider(Func<Vector3, float> provider)
        {
            depthProvider = provider;
        }

        // Horizontal force from contact or lines, accumulated into the next manoeuvring solve.
        public void AddExternalForce(Vector3 worldForce, Vector3 worldPoint)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float fx = Vector3.Dot(worldForce, forward);
            float fy = Vector3.Dot(worldForce, right);
            Vector3 offset = worldPoint - transform.position;
            externalX += fx;
            externalY += fy;
            externalN += Vector3.Dot(offset, forward) * fy - Vector3.Dot(offset, right) * fx;
        }

        private float KeelDepth(float localZ)
        {
            return waterLevel - transform.TransformPoint(0f, parameters.KeelLocalY, localZ).y;
        }

        private void SampleBankFlowAreas(Vector3 midship, Vector3 right, float depth, out float port, out float starboard)
        {
            port = starboard = float.PositiveInfinity;
            if (float.IsInfinity(depth)) return;
            float width = data.restrictedWater.bankSamplingWidthBeams * parameters.Beam;
            float step = width / BankSamplesPerSide;
            float portSum = 0f, starboardSum = 0f;
            for (int k = 0; k < BankSamplesPerSide; k++)
            {
                float offset = 0.5f * parameters.Beam + (k + 0.5f) * step;
                float starboardDepth = SampleDepth(midship + right * offset);
                float portDepth = SampleDepth(midship - right * offset);
                if (float.IsInfinity(starboardDepth) || float.IsInfinity(portDepth)) return;
                starboardSum += Mathf.Max(0f, starboardDepth) * step;
                portSum += Mathf.Max(0f, portDepth) * step;
            }
            port = portSum;
            starboard = starboardSum;
        }

        private float[] SampleCurrentShear(Vector3 midship, Vector3 forward, Vector3 right, Vector3 current)
        {
            if (currentField == null) return null;
            for (int i = 0; i < stationCurrent.Length; i++)
            {
                Vector3 local = currentField.Sample(midship + forward * model.Hull.StationPosition(i));
                stationCurrent[i] = Vector3.Dot(local - current, right);
            }
            return stationCurrent;
        }

        private void ResetVessel()
        {
            body.position = startPosition;
            body.rotation = startRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            SetThrottleCommand(0f);
            rudderCommand = 0f;
            externalX = externalY = externalN = 0f;
            model?.RestoreActuators(0f, null);
        }

        public void RestoreVoyage(ShipSimulator.Persistence.VoyageSave save)
        {
            body.position = save.position;
            body.rotation = save.rotation;
            body.linearVelocity = save.velocity;
            body.angularVelocity = save.angularVelocity;
            rudderCommand = save.rudder;
            var rps = new float[engineCommands.Length];
            for (int i = 0; i < engineCommands.Length; i++)
            {
                bool perEngine = save.engineCommands != null && save.engineCommands.Length == engineCommands.Length;
                engineCommands[i] = perEngine ? save.engineCommands[i] : save.throttle;
                bool perShaft = save.shaftRps != null && save.shaftRps.Length == engineCommands.Length;
                rps[i] = perShaft ? save.shaftRps[i] : save.actualThrottle * parameters.RatedRps;
            }
            model.RestoreActuators(save.rudderAngle * Mathf.Deg2Rad, rps);
            activeCurrentZones.Clear();
            UnityEngine.Physics.SyncTransforms();
            foreach (RiverCurrentZone zone in FindObjectsByType<RiverCurrentZone>())
            {
                if (!zone.isActiveAndEnabled) continue;
                BoxCollider trigger = zone.GetComponent<BoxCollider>();
                foreach (Collider hull in GetComponentsInChildren<Collider>())
                {
                    if (!hull.enabled || hull.isTrigger) continue;
                    if (UnityEngine.Physics.ComputePenetration(hull, hull.transform.position, hull.transform.rotation,
                        trigger, trigger.transform.position, trigger.transform.rotation, out _, out _))
                    {
                        activeCurrentZones.Add(zone);
                        break;
                    }
                }
            }
        }

        public float[] CaptureEngineCommands() => (float[])engineCommands.Clone();

        public float[] CaptureShaftRps()
        {
            if (model == null) return Array.Empty<float>();
            var rps = new float[model.Shafts.Length];
            for (int i = 0; i < rps.Length; i++) rps[i] = model.Shafts[i].Rps;
            return rps;
        }

        public void SetThrottleCommand(float value)
        {
            for (int i = 0; i < engineCommands.Length; i++) engineCommands[i] = Mathf.Clamp(value, -1f, 1f);
        }

        public void SetEngineCommand(int index, float value)
        {
            engineCommands[index] = Mathf.Clamp(value, -1f, 1f);
        }

        public void SetRudderCommand(float value)
        {
            rudderCommand = Mathf.Clamp(value, -1f, 1f);
        }

        public void CenterRudder()
        {
            rudderCommand = 0f;
        }

        public void SetWindVelocity(Vector3 velocityMps)
        {
            windVelocityMps = velocityMps;
        }

        // Used where no current zone or field applies.
        public void SetAmbientCurrent(Vector3 velocityMps)
        {
            ambientCurrentMps = velocityMps;
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

        private Vector3 CalculateEffectiveCurrent(Vector3 position)
        {
            if (currentField != null) return currentField.Sample(position);
            activeCurrentZones.RemoveWhere(zone => zone == null || !zone.isActiveAndEnabled);
            if (activeCurrentZones.Count == 0) return ambientCurrentMps;
            Vector3 total = Vector3.zero;
            foreach (RiverCurrentZone zone in activeCurrentZones)
                total += zone.CurrentVelocityMps;
            return total / activeCurrentZones.Count;
        }

        private static float Mean(float[] values)
        {
            if (values.Length == 0) return 0f;
            float sum = 0f;
            foreach (float value in values) sum += value;
            return sum / values.Length;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugForces || body == null || model == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, relativeWaterVelocity * 5f);
            VesselPropeller propeller = data.propeller;
            Gizmos.color = Color.red;
            for (int i = 0; i < propeller.count; i++)
            {
                Vector3 position = transform.TransformPoint(propeller.lateralPositionsM[i], parameters.KeelLocalY + 1f,
                    propeller.longitudinalPositionsM[i]);
                Gizmos.DrawRay(position, transform.forward * model.PropellerThrustN[i] / 50000f);
            }
            Gizmos.color = Color.yellow;
            for (int j = 0; j < data.rudder.count; j++)
            {
                Vector3 position = transform.TransformPoint(data.rudder.lateralPositionsM[j], parameters.KeelLocalY + 1f,
                    data.rudder.longitudinalPositionM);
                Gizmos.DrawRay(position, -transform.right * model.RudderNormalForceN[j] / 50000f);
            }
        }
    }
}
