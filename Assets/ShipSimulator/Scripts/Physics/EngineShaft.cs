using UnityEngine;

namespace ShipSimulator.Physics
{
    // One diesel, gearbox and propeller shaft: 2 pi I dn/dt = Q_engine - Q_propeller - Q_friction.
    // The governor is a stiff proportional controller solved implicitly, so its gain does not limit the
    // time step. A diesel cannot drive against its running direction: reversing cuts fuel, brakes the
    // shaft with starting air until it is slow enough, then waits the restart delay.
    public sealed class EngineShaft
    {
        private const float FrictionTorqueFraction = 0.02f;

        private readonly float ratedRps;
        private readonly float deliveredPower;
        private readonly float maxTorque;
        private readonly float inertia;
        private readonly float gain;
        private readonly float rampRate;
        private readonly float reversalRps;
        private readonly float reversalDelay;
        private readonly float brakeTorque;
        private float setpointRps;
        private int direction;
        private float reversalTimer;

        public float Rps { get; private set; }
        public float EngineTorqueNm { get; private set; }
        public bool IsReversing { get; private set; }
        public float RatedRps => ratedRps;
        public float LoadFraction => deliveredPower > 0f
            ? Mathf.Abs(EngineTorqueNm * 2f * Mathf.PI * Rps) / deliveredPower
            : 0f;

        public EngineShaft(VesselParameters parameters)
        {
            VesselEngine engine = parameters.Data.engine;
            ratedRps = parameters.RatedRps;
            deliveredPower = parameters.DeliveredPowerPerShaftW;
            maxTorque = deliveredPower / (2f * Mathf.PI * ratedRps);
            inertia = engine.shaftInertiaKgM2;
            gain = maxTorque / (engine.governorBandFraction * ratedRps);
            rampRate = ratedRps / engine.rpmRampSecondsFullRange;
            reversalRps = engine.reversalRpmFraction * ratedRps;
            reversalDelay = engine.reversalDelaySeconds;
            brakeTorque = engine.reversalBrakeTorqueFraction * maxTorque;
        }

        public void Restore(float rps)
        {
            Rps = rps;
            setpointRps = rps;
            direction = rps > 1e-3f ? 1 : rps < -1e-3f ? -1 : 0;
            reversalTimer = 0f;
            EngineTorqueNm = 0f;
            IsReversing = false;
        }

        public void Step(float command, float propellerTorqueNm, float dt)
        {
            float target = Mathf.Clamp(command, -1f, 1f) * ratedRps;
            int commanded = Mathf.Abs(target) < 1e-3f ? 0 : target > 0f ? 1 : -1;

            // An order against the running direction cuts fuel at once rather than ramping down through ahead.
            IsReversing = commanded != 0 && direction != 0 && commanded != direction;
            if (IsReversing)
            {
                setpointRps = 0f;
                if (Mathf.Abs(Rps) <= reversalRps)
                {
                    reversalTimer += dt;
                    if (reversalTimer >= reversalDelay)
                    {
                        direction = commanded;
                        reversalTimer = 0f;
                        IsReversing = false;
                    }
                }
                else
                {
                    reversalTimer = 0f;
                }
            }
            else
            {
                reversalTimer = 0f;
                if (direction == 0 && commanded != 0) direction = commanded;
            }
            if (!IsReversing) setpointRps = Mathf.MoveTowards(setpointRps, target, rampRate * dt);
            int wanted = Mathf.Abs(setpointRps) < 1e-3f ? 0 : setpointRps > 0f ? 1 : -1;

            float scale = dt / (2f * Mathf.PI * inertia);
            float friction = Mathf.Abs(Rps) > 1e-3f ? FrictionTorqueFraction * maxTorque * Mathf.Sign(Rps) : 0f;
            float next;
            if (wanted != 0 && !IsReversing)
            {
                next = (Rps + scale * (gain * setpointRps - propellerTorqueNm - friction)) / (1f + scale * gain);
                float torque = gain * (setpointRps - next);
                float limit = Mathf.Abs(next) <= ratedRps ? maxTorque : deliveredPower / (2f * Mathf.PI * Mathf.Abs(next));
                float limited = torque * direction < 0f ? 0f : Mathf.Clamp(torque, -limit, limit);
                if (!Mathf.Approximately(limited, torque))
                    next = Rps + scale * (limited - propellerTorqueNm - friction);
                EngineTorqueNm = limited;
            }
            else
            {
                float brake = IsReversing && Mathf.Abs(Rps) > reversalRps ? -Mathf.Sign(Rps) * brakeTorque : 0f;
                EngineTorqueNm = brake;
                next = Rps + scale * (brake - propellerTorqueNm - friction);
            }

            // Bearing friction alone may stop the shaft but never turn it backwards.
            if (Rps * next < 0f && Mathf.Abs(propellerTorqueNm) <= Mathf.Abs(friction) && EngineTorqueNm * next <= 0f)
                next = 0f;
            Rps = next;
        }
    }
}
