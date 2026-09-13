using UnityEngine;

namespace ShipSimulator.Physics
{
    public struct ManoeuvringInput
    {
        // Water-relative velocity at midship in the horizontal body frame: surge forward, sway to starboard.
        public float SurgeSpeed;
        public float SwaySpeed;
        // Positive turns the bow to starboard.
        public float YawRate;
        public float[] EngineCommands;
        public float RudderCommand;
        // Positive pushes the bow to starboard.
        public float BowThrusterCommand;
        // Air velocity relative to the ship: x forward, y starboard.
        public Vector2 RelativeWind;
        // PositiveInfinity for unrestricted water.
        public float DepthM;
        public float PortFlowAreaM2;
        public float StarboardFlowAreaM2;
        public float[] StationSwayCurrent;
        public float ExternalX;
        public float ExternalY;
        public float ExternalN;
    }

    public struct ManoeuvringOutput
    {
        public float SurgeAcceleration;
        public float SwayAcceleration;
        public float YawAcceleration;
        // Inertial acceleration of the centre of gravity, horizontal body axes.
        public float GravityCentreAccelerationX;
        public float GravityCentreAccelerationY;
        public HullForces Hull;
        public float ResistanceX;
        public float PropellerX;
        public float PropellerN;
        public float RudderX;
        public float RudderY;
        public float RudderN;
        public float WindX;
        public float WindY;
        public float WindN;
        public float BankY;
        public float BankN;
        public float ThrusterY;
        public float ThrusterN;
        public float TotalX;
        public float TotalY;
        public float TotalN;
        public float AdvanceSpeed;
        public float Blockage;
        public float BowSquatM;
        public float SternSquatM;
        public DepthFactors Depth;
    }

    // Three-degree-of-freedom MMG manoeuvring model (Yasukawa and Yoshimura 2015), written about midship
    // with the centre of gravity at x_G, in water-relative velocities (Fossen's irrotational-current form).
    public sealed class ManoeuvringModel
    {
        private readonly HullForceModel hull;
        private readonly int[] rudderPropeller;
        private readonly float[] propellerCoefficient;
        private float cachedDepth = float.NaN;
        private DepthFactors cachedFactors = DepthFactors.Deep;

        public VesselParameters Parameters { get; }
        public EngineShaft[] Shafts { get; }
        public float RudderAngleRad { get; private set; }
        public float[] PropellerThrustN { get; }
        public float[] PropellerTorqueNm { get; }
        public float[] RudderNormalForceN { get; }
        public HullForceModel Hull => hull;
        // Null when the vessel has no bow thruster.
        public BowThrusterModel BowThruster { get; }

        public ManoeuvringModel(VesselParameters parameters)
        {
            Parameters = parameters;
            hull = new HullForceModel(parameters);
            if (parameters.Data.bowThruster != null && parameters.Data.bowThruster.fitted)
                BowThruster = new BowThrusterModel(parameters);
            VesselPropeller propeller = parameters.Data.propeller;
            Shafts = new EngineShaft[propeller.count];
            for (int i = 0; i < Shafts.Length; i++) Shafts[i] = new EngineShaft(parameters);
            PropellerThrustN = new float[propeller.count];
            PropellerTorqueNm = new float[propeller.count];
            propellerCoefficient = new float[propeller.count];
            VesselRudder rudder = parameters.Data.rudder;
            RudderNormalForceN = new float[rudder.count];
            rudderPropeller = new int[rudder.count];
            for (int j = 0; j < rudder.count; j++)
            {
                float nearest = float.PositiveInfinity;
                for (int i = 0; i < propeller.count; i++)
                {
                    float distance = Mathf.Abs(propeller.lateralPositionsM[i] - rudder.lateralPositionsM[j]);
                    if (distance >= nearest) continue;
                    nearest = distance;
                    rudderPropeller[j] = i;
                }
            }
        }

        public void RestoreActuators(float rudderAngleRad, float[] shaftRps)
        {
            RudderAngleRad = rudderAngleRad;
            for (int i = 0; i < Shafts.Length; i++)
                Shafts[i].Restore(shaftRps != null && i < shaftRps.Length ? shaftRps[i] : 0f);
        }

        public ManoeuvringOutput Step(in ManoeuvringInput input, float dt)
        {
            VesselParameters p = Parameters;
            VesselData data = p.Data;
            float lpp = p.Lpp;
            float u = input.SurgeSpeed;
            float v = input.SwaySpeed;
            float r = input.YawRate;
            float speed = Mathf.Sqrt(u * u + v * v);
            var output = new ManoeuvringOutput();

            float maxRudder = data.rudder.maxAngleDeg * Mathf.Deg2Rad;
            RudderAngleRad = Mathf.MoveTowards(RudderAngleRad, Mathf.Clamp(input.RudderCommand, -1f, 1f) * maxRudder,
                data.rudder.rateDegPerSecond * Mathf.Deg2Rad * dt);

            if (!Mathf.Approximately(cachedDepth, input.DepthM))
            {
                cachedDepth = input.DepthM;
                cachedFactors = data.restrictedWater.shallowWaterCorrections
                    ? RestrictedWaterModel.DepthFactors(lpp, p.Beam, p.Draft, p.BlockCoefficient, input.DepthM)
                    : DepthFactors.Deep;
            }
            output.Depth = cachedFactors;

            // Wake fraction falls as the stern sweeps sideways (MMG 2015); no hull wake leads a propeller astern.
            VesselPropeller propeller = data.propeller;
            float wake = 0f;
            if (u > 0f)
            {
                float reference = Mathf.Max(speed, Mathf.Max(data.hull.lowSpeedBlendStartMps, 0.05f));
                float drift = Mathf.Atan2(-v, u) - propeller.longitudinalPositionsM[0] / lpp * (r * lpp / reference);
                wake = 1f - (1f - propeller.wakeFraction) *
                    (1f + (1f - Mathf.Exp(-propeller.wakeDriftC1 * Mathf.Abs(drift))) * (propeller.wakeDriftC2 - 1f));
                wake = Mathf.Clamp(wake, 0f, 0.95f);
            }
            float advance = u * (1f - wake);
            output.AdvanceSpeed = advance;

            for (int i = 0; i < Shafts.Length; i++)
            {
                float thrust = PropellerModel.Evaluate(propeller, p.Rho, Shafts[i].Rps, advance,
                    out float torque, out float coefficient) * data.calibration.thrustMultiplier;
                PropellerThrustN[i] = thrust;
                PropellerTorqueNm[i] = torque;
                propellerCoefficient[i] = coefficient;
                float effective = (1f - propeller.thrustDeduction) * thrust;
                output.PropellerX += effective;
                output.PropellerN += -propeller.lateralPositionsM[i] * effective;
            }

            output.Hull = hull.Evaluate(u, v, r, cachedFactors, input.StationSwayCurrent);
            output.ResistanceX = speed > 1e-6f ? -ResistanceModel.ResistanceN(p, speed, input.DepthM) * u / speed : 0f;

            for (int j = 0; j < RudderNormalForceN.Length; j++)
            {
                int shaft = rudderPropeller[j];
                RudderForces rudderForces = RudderModel.Evaluate(p, data.rudder.lateralPositionsM[j], u, v, r,
                    RudderAngleRad, Shafts[shaft].Rps, propellerCoefficient[shaft], advance);
                RudderNormalForceN[j] = rudderForces.NormalForceN;
                output.RudderX += rudderForces.X;
                output.RudderY += rudderForces.Y;
                output.RudderN += rudderForces.N;
            }

            WindLoadModel.Evaluate(p, input.RelativeWind, out output.WindX, out output.WindY, out output.WindN);
            RestrictedWaterModel.BankForces(p, speed, input.DepthM, input.PortFlowAreaM2, input.StarboardFlowAreaM2,
                out output.BankY, out output.BankN);

            if (BowThruster != null)
            {
                BowThruster.Step(input.BowThrusterCommand, u, dt);
                output.ThrusterY = BowThruster.ThrustN;
                output.ThrusterN = BowThruster.LongitudinalPositionM * BowThruster.ThrustN;
            }

            float x = output.Hull.X + output.ResistanceX + output.PropellerX + output.RudderX + output.WindX + input.ExternalX;
            float y = output.Hull.Y + output.RudderY + output.WindY + output.BankY + output.ThrusterY + input.ExternalY;
            float n = output.Hull.N + output.PropellerN + output.RudderN + output.WindN + output.BankN + output.ThrusterN +
                input.ExternalN;
            output.TotalX = x;
            output.TotalY = y;
            output.TotalN = n;

            for (int i = 0; i < Shafts.Length; i++)
                Shafts[i].Step(input.EngineCommands != null && i < input.EngineCommands.Length ? input.EngineCommands[i] : 0f,
                    PropellerTorqueNm[i], dt);

            Solve(u, v, r, x, y, n, cachedFactors, ref output);

            output.Blockage = RestrictedWaterModel.Blockage(p, input.DepthM, input.PortFlowAreaM2, input.StarboardFlowAreaM2);
            RestrictedWaterModel.Squat(p.DisplacementVolume, lpp, p.BlockCoefficient, speed, input.DepthM,
                output.Blockage, data.restrictedWater.squatCoefficient, out output.BowSquatM, out output.SternSquatM);
            return output;
        }

        private void Solve(float u, float v, float r, float x, float y, float n, in DepthFactors depth, ref ManoeuvringOutput output)
        {
            VesselParameters p = Parameters;
            float m = p.Mass;
            float mx = p.SurgeAddedMass;
            float my = p.SwayAddedMass * depth.SwayAddedMass;
            float jz = p.YawAddedInertia * depth.YawAddedInertia;
            float xg = p.CentreOfGravityX;
            float yawInertia = p.YawInertiaAtG + xg * xg * m + jz;

            float surge = x + (m + my) * v * r + xg * m * r * r;
            float sway = y - (m + mx) * u * r;
            float yaw = n - xg * m * u * r;
            float coupling = xg * m;
            float determinant = (m + my) * yawInertia - coupling * coupling;

            output.SurgeAcceleration = surge / (m + mx);
            output.SwayAcceleration = (sway * yawInertia - coupling * yaw) / determinant;
            output.YawAcceleration = ((m + my) * yaw - coupling * sway) / determinant;
            output.GravityCentreAccelerationX = output.SurgeAcceleration - v * r - xg * r * r;
            output.GravityCentreAccelerationY = output.SwayAcceleration + u * r + xg * output.YawAcceleration;
        }
    }
}
