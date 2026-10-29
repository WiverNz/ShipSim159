using ShipSimulator.Visuals;
using UnityEngine;

namespace ShipSimulator.Physics
{
    // Shares vessel hydrostatics and MMG resistance without becoming a player/control target.
    [RequireComponent(typeof(Rigidbody), typeof(VesselDataLoader))]
    public sealed class PassiveVessel : MonoBehaviour
    {
        private CurrentFieldProvider currentField;
        private ScenarioBathymetry bathymetry;
        private WeatherController weather;
        private float[] stationCurrent;
        public Rigidbody Body { get; private set; }
        public VesselParameters Parameters { get; private set; }
        public HydrostaticsModel Hydrostatics { get; private set; }
        public ManoeuvringModel Model { get; private set; }
        public ManoeuvringOutput Diagnostics { get; private set; }
        public bool ManualStepping { get; set; }
        public float WaterLevel { get; set; }
        public Vector3 AmbientCurrent { get; set; }
        public ShipPhysicsController EnvironmentSource { get; set; }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            VesselData data = GetComponent<VesselDataLoader>().Load();
            if (data == null || !data.nonSelfPropelled)
            {
                Debug.LogError("Passive vessel needs valid non-self-propelled data.", this);
                enabled = false;
                return;
            }
            Parameters = VesselParameters.Create(data);
            Hydrostatics = new HydrostaticsModel(Parameters);
            Model = new ManoeuvringModel(Parameters);
            stationCurrent = new float[Model.Hull.StationCount];
            Body.mass = Parameters.Mass;
            Body.centerOfMass = Parameters.CentreOfMassLocalM;
            Body.inertiaTensor = new Vector3(Parameters.PitchInertia, Parameters.YawInertiaAtG, Parameters.RollInertia);
            Body.inertiaTensorRotation = Quaternion.identity;
            Body.useGravity = true;
            Body.linearDamping = Body.angularDamping = 0;
            Body.maxAngularVelocity = 1;
            Body.solverIterations = 24;
            Body.solverVelocityIterations = 12;
            Body.collisionDetectionMode = HullCollisionDetection.Mode;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            currentField = FindAnyObjectByType<CurrentFieldProvider>();
            bathymetry = FindAnyObjectByType<ScenarioBathymetry>();
            weather = FindAnyObjectByType<WeatherController>();
        }

        private void FixedUpdate()
        {
            if (!ManualStepping) Simulate(Time.fixedDeltaTime);
        }

        public void Simulate(float dt)
        {
            if (Model == null) return;
            if (EnvironmentSource != null) WaterLevel = EnvironmentSource.WaterLevel;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 current = currentField != null ? currentField.Sample(Body.position) :
                EnvironmentSource != null ? EnvironmentSource.EffectiveCurrentMps : AmbientCurrent;
            Vector3 velocity = Body.GetPointVelocity(transform.position);
            Vector3 relative = velocity - current;
            Vector3 air = (weather != null ? weather.VisualWindMps : Vector3.zero) - velocity;
            float depth = bathymetry != null ? bathymetry.Sample(Body.position).DepthM :
                EnvironmentSource != null ? EnvironmentSource.SampleDepth(Body.position) : 20;
            if (currentField != null)
                for (int i = 0; i < stationCurrent.Length; i++)
                    stationCurrent[i] = Vector3.Dot(currentField.Sample(Body.position + forward * Model.Hull.StationPosition(i)) - current, right);
            Diagnostics = Model.Step(new ManoeuvringInput
            {
                SurgeSpeed = Vector3.Dot(relative, forward), SwaySpeed = Vector3.Dot(relative, right),
                YawRate = Body.angularVelocity.y, DepthM = depth,
                PortFlowAreaM2 = float.PositiveInfinity, StarboardFlowAreaM2 = float.PositiveInfinity,
                RelativeWind = new Vector2(Vector3.Dot(air, forward), Vector3.Dot(air, right)),
                StationSwayCurrent = stationCurrent
            }, dt);
            Body.AddForce(forward * Diagnostics.GravityCentreAccelerationX + right * Diagnostics.GravityCentreAccelerationY,
                ForceMode.Acceleration);
            Body.AddTorque(Vector3.up * Diagnostics.YawAcceleration, ForceMode.Acceleration);
            Hydrostatics.Apply(Body, transform, WaterLevel, Diagnostics.BowSquatM, Diagnostics.SternSquatM);
        }
    }
}
