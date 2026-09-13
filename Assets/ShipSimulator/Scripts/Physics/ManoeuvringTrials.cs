using UnityEngine;

namespace ShipSimulator.Physics
{
    public struct TurningCircleResult
    {
        public float ApproachSpeedMps;
        public float AdvanceM;
        public float TransferM;
        public float TacticalDiameterM;
        public float SteadyTurningDiameterM;
        public float SteadySpeedMps;
    }

    public struct ZigZagResult
    {
        public float FirstOvershootDeg;
        public float SecondOvershootDeg;
        public float InitialTurningDistanceM;
        public float LengthOverSpeedS;
    }

    public struct StoppingResult
    {
        public float TrackReachM;
        public float HeadReachM;
        public float LateralDeviationM;
        public float TimeS;
    }

    // Standard manoeuvres from IMO MSC.137(76) and ITTC trial practice, run on the pure simulator.
    public static class ManoeuvringTrials
    {
        public const float StepS = 0.05f;
        private const float ApproachSeconds = 2400f;

        public static ManoeuvringSimulator Approach(VesselParameters parameters, float engineCommand, float depthM)
        {
            var simulator = new ManoeuvringSimulator(parameters) { DepthM = depthM };
            simulator.SetEngineCommands(engineCommand);
            for (float t = 0f; t < ApproachSeconds; t += 0.2f) simulator.Step(0.2f);
            simulator.ResetTrack();
            return simulator;
        }

        public static float SteadySpeed(VesselParameters parameters, float engineCommand, float depthM)
        {
            return Approach(parameters, engineCommand, depthM).SurgeSpeed;
        }

        public static TurningCircleResult TurningCircle(VesselParameters parameters, float rudderCommand, float depthM)
        {
            ManoeuvringSimulator ship = Approach(parameters, 1f, depthM);
            var result = new TurningCircleResult
            {
                ApproachSpeedMps = ship.SurgeSpeed,
                AdvanceM = float.NaN,
                TransferM = float.NaN,
                TacticalDiameterM = float.NaN
            };
            ship.RudderCommand = rudderCommand;
            bool quarter = false;
            bool half = false;
            while (ship.TimeS < 3000f && Mathf.Abs(ship.HeadingRad) < 4f * Mathf.PI)
            {
                ship.Step(StepS);
                float turned = Mathf.Abs(ship.HeadingRad);
                if (!quarter && turned >= 0.5f * Mathf.PI)
                {
                    quarter = true;
                    result.AdvanceM = ship.Position.y;
                    result.TransferM = Mathf.Abs(ship.Position.x);
                }
                if (!half && turned >= Mathf.PI)
                {
                    half = true;
                    result.TacticalDiameterM = Mathf.Abs(ship.Position.x);
                }
            }
            float yaw = Mathf.Max(Mathf.Abs(ship.YawRate), 1e-6f);
            float speed = Mathf.Sqrt(ship.SurgeSpeed * ship.SurgeSpeed + ship.SwaySpeed * ship.SwaySpeed);
            result.SteadySpeedMps = speed;
            result.SteadyTurningDiameterM = 2f * speed / yaw;
            return result;
        }

        public static ZigZagResult ZigZag(VesselParameters parameters, float angleDeg, float depthM)
        {
            ManoeuvringSimulator ship = Approach(parameters, 1f, depthM);
            var result = new ZigZagResult { LengthOverSpeedS = parameters.Lpp / Mathf.Max(ship.SurgeSpeed, 0.1f) };
            float command = angleDeg / parameters.Data.rudder.maxAngleDeg;
            ship.RudderCommand = command;
            int phase = 0;
            float extreme = 0f;
            while (ship.TimeS < 2000f && phase < 3)
            {
                ship.Step(StepS);
                float heading = ship.HeadingRad * Mathf.Rad2Deg;
                if (phase == 0 && heading >= angleDeg)
                {
                    result.InitialTurningDistanceM = ship.PathLengthM;
                    ship.RudderCommand = -command;
                    phase = 1;
                    extreme = heading;
                }
                else if (phase == 1)
                {
                    extreme = Mathf.Max(extreme, heading);
                    if (heading <= -angleDeg)
                    {
                        result.FirstOvershootDeg = extreme - angleDeg;
                        ship.RudderCommand = command;
                        phase = 2;
                        extreme = heading;
                    }
                }
                else if (phase == 2)
                {
                    extreme = Mathf.Min(extreme, heading);
                    if (heading >= angleDeg)
                    {
                        result.SecondOvershootDeg = -extreme - angleDeg;
                        phase = 3;
                    }
                }
            }
            return result;
        }

        public static StoppingResult CrashStop(VesselParameters parameters, float depthM)
        {
            ManoeuvringSimulator ship = Approach(parameters, 1f, depthM);
            ship.SetEngineCommands(-1f);
            while (ship.TimeS < 3000f && ship.SurgeSpeed > 0f) ship.Step(StepS);
            return new StoppingResult
            {
                TrackReachM = ship.PathLengthM,
                HeadReachM = ship.Position.y,
                LateralDeviationM = Mathf.Abs(ship.Position.x),
                TimeS = ship.TimeS
            };
        }

        // IMO MSC.137(76) first overshoot limits for 10/10 zig-zag, in degrees.
        // Full bow thruster to starboard from rest, engines stopped; returns the yaw rate at the end.
        public static float BowThrusterTurnRateDegPerMin(VesselParameters parameters, float depthM, float seconds = 180f)
        {
            var ship = new ManoeuvringSimulator(parameters) { DepthM = depthM, BowThrusterCommand = 1f };
            for (float t = 0f; t < seconds; t += StepS) ship.Step(StepS);
            return ship.YawRate * Mathf.Rad2Deg * 60f;
        }

        public static float ImoFirstOvershootLimitDeg(float lengthOverSpeedS)
        {
            if (lengthOverSpeedS < 10f) return 10f;
            return lengthOverSpeedS < 30f ? 5f + 0.5f * lengthOverSpeedS : 20f;
        }

        public static float ImoSecondOvershootLimitDeg(float lengthOverSpeedS)
        {
            if (lengthOverSpeedS < 10f) return 25f;
            return lengthOverSpeedS < 30f ? 17.5f + 0.75f * lengthOverSpeedS : 40f;
        }
    }
}
