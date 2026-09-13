using UnityEngine;

namespace ShipSimulator.Physics
{
    // Pure three-degree-of-freedom integration of the manoeuvring model, without Unity physics. Used for
    // virtual sea trials and as the reference the Rigidbody integration is tested against.
    // World frame follows Unity: Position.x east, Position.y north (Unity z); heading clockwise from north.
    public sealed class ManoeuvringSimulator
    {
        public ManoeuvringModel Model { get; }
        public float TimeS { get; private set; }
        public Vector2 Position { get; private set; }
        public float HeadingRad { get; private set; }
        public float SurgeSpeed { get; private set; }
        public float SwaySpeed { get; private set; }
        public float YawRate { get; private set; }
        public float PathLengthM { get; private set; }
        public ManoeuvringOutput Last { get; private set; }

        public Vector2 CurrentMps { get; set; }
        public Vector2 WindMps { get; set; }
        public float DepthM { get; set; } = float.PositiveInfinity;
        public float PortFlowAreaM2 { get; set; } = float.PositiveInfinity;
        public float StarboardFlowAreaM2 { get; set; } = float.PositiveInfinity;
        public float[] EngineCommands { get; }
        public float RudderCommand { get; set; }
        public float BowThrusterCommand { get; set; }

        public ManoeuvringSimulator(VesselParameters parameters)
        {
            Model = new ManoeuvringModel(parameters);
            EngineCommands = new float[parameters.Data.propeller.count];
        }

        public Vector2 Forward => new Vector2(Mathf.Sin(HeadingRad), Mathf.Cos(HeadingRad));
        public Vector2 Right => new Vector2(Mathf.Cos(HeadingRad), -Mathf.Sin(HeadingRad));
        public Vector2 GroundVelocity => Forward * SurgeSpeed + Right * SwaySpeed + CurrentMps;

        public void SetEngineCommands(float command)
        {
            for (int i = 0; i < EngineCommands.Length; i++) EngineCommands[i] = command;
        }

        public void Restore(float surgeSpeed, float swaySpeed, float yawRate, float rudderAngleRad, float[] shaftRps)
        {
            SurgeSpeed = surgeSpeed;
            SwaySpeed = swaySpeed;
            YawRate = yawRate;
            Model.RestoreActuators(rudderAngleRad, shaftRps);
        }

        public void ResetTrack()
        {
            Position = Vector2.zero;
            HeadingRad = 0f;
            PathLengthM = 0f;
            TimeS = 0f;
        }

        public void Step(float dt)
        {
            Vector2 ground = GroundVelocity;
            Vector2 air = WindMps - ground;
            var input = new ManoeuvringInput
            {
                SurgeSpeed = SurgeSpeed,
                SwaySpeed = SwaySpeed,
                YawRate = YawRate,
                EngineCommands = EngineCommands,
                RudderCommand = RudderCommand,
                BowThrusterCommand = BowThrusterCommand,
                RelativeWind = new Vector2(Vector2.Dot(air, Forward), Vector2.Dot(air, Right)),
                DepthM = DepthM,
                PortFlowAreaM2 = PortFlowAreaM2,
                StarboardFlowAreaM2 = StarboardFlowAreaM2
            };
            ManoeuvringOutput output = Model.Step(input, dt);
            Last = output;
            SurgeSpeed += output.SurgeAcceleration * dt;
            SwaySpeed += output.SwayAcceleration * dt;
            YawRate += output.YawAcceleration * dt;
            HeadingRad += YawRate * dt;
            Vector2 velocity = GroundVelocity;
            Position += velocity * dt;
            PathLengthM += velocity.magnitude * dt;
            TimeS += dt;
        }
    }
}
