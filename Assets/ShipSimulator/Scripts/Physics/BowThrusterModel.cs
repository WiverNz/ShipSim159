using UnityEngine;

namespace ShipSimulator.Physics
{
    // Tunnel bow thruster. Bollard thrust follows actuator disc momentum theory, T = FM (2 rho A P^2)^(1/3),
    // with an estimated figure of merit for tunnel and motor losses. As the ship gathers way the passing flow
    // bends the jet back onto the hull, and a tunnel close to the surface draws air; both are estimated
    // reductions, not measured curves for this installation.
    public sealed class BowThrusterModel
    {
        private readonly VesselBowThruster data;
        private readonly float submergence;

        // Ramped command, -1 full to port, 1 full to starboard.
        public float Output { get; private set; }
        // Positive pushes the bow to starboard.
        public float ThrustN { get; private set; }
        public float BollardThrustN { get; }
        public float LongitudinalPositionM => data.longitudinalPositionM;

        public BowThrusterModel(VesselParameters parameters)
        {
            data = parameters.Data.bowThruster;
            BollardThrustN = BollardThrust(parameters.Rho, data.powerW, data.tunnelDiameterM, data.figureOfMerit);
            submergence = SubmergenceFactor(data, parameters.Draft);
        }

        public static float BollardThrust(float rho, float powerW, float diameterM, float figureOfMerit)
        {
            float area = 0.25f * Mathf.PI * diameterM * diameterM;
            return figureOfMerit * Mathf.Pow(2f * rho * area * powerW * powerW, 1f / 3f);
        }

        public static float SpeedEffectiveness(VesselBowThruster thruster, float surgeSpeed)
        {
            float ratio = surgeSpeed / thruster.speedLossReferenceMps;
            float minimum = thruster.minimumSpeedEffectiveness;
            return minimum + (1f - minimum) / (1f + ratio * ratio);
        }

        // Full thrust once the water covers the tunnel axis by one diameter; none with the axis out of the water.
        public static float SubmergenceFactor(VesselBowThruster thruster, float draftM)
        {
            return Mathf.Clamp01((draftM - thruster.axisHeightAboveKeelM) / thruster.tunnelDiameterM);
        }

        public void Restore(float output)
        {
            Output = Mathf.Clamp(output, -1f, 1f);
            ThrustN = 0f;
        }

        public void Step(float command, float surgeSpeed, float dt)
        {
            Output = Mathf.MoveTowards(Output, Mathf.Clamp(command, -1f, 1f), dt / data.rampSecondsToFull);
            ThrustN = Output * BollardThrustN * submergence * SpeedEffectiveness(data, surgeSpeed);
        }
    }
}
