using System;
using UnityEngine;

namespace ShipSimulator.Physics
{
    public static class VesselDataValidator
    {
        public static bool TryValidate(VesselData data, out string error)
        {
            if (data == null) return Fail("Root object is missing.", out error);
            if (data.identity == null) return Fail("identity section is missing.", out error);
            if (string.IsNullOrWhiteSpace(data.identity.displayName))
                return Fail("identity.displayName is required.", out error);
            if (data.dimensions == null) return Fail("dimensions section is missing.", out error);
            if (!Positive(data.dimensions.lengthOverallM) ||
                !Positive(data.dimensions.beamOverallM) ||
                !Positive(data.dimensions.loadedDraftM))
                return Fail("Vessel length, beam and draft must be finite and positive.", out error);
            if (data.dimensions.loadedDraftM >= data.dimensions.depthMouldedM)
                return Fail("Loaded draft must be less than moulded depth.", out error);

            if (data.massProperties == null)
                return Fail("massProperties section is missing.", out error);
            if (!Positive(data.massProperties.lightshipMassKg) ||
                !Positive(data.massProperties.loadedMassKg) ||
                data.massProperties.loadedMassKg < data.massProperties.lightshipMassKg)
                return Fail("Mass values are invalid.", out error);
            if (!InRange(data.massProperties.loadFraction, 0f, 1f))
                return Fail("massProperties.loadFraction must be between 0 and 1.", out error);
            if (!PositiveVector(data.massProperties.inertiaTensorKgM2))
                return Fail("Inertia tensor components must be finite and positive.", out error);

            if (data.engine == null || data.engine.engineCount <= 0 ||
                !Positive(data.engine.powerPerEngineW) ||
                !Positive(data.engine.aheadResponseSeconds) ||
                !Positive(data.engine.asternResponseSeconds))
                return Fail("Engine configuration is invalid.", out error);

            if (data.propeller == null || data.propeller.count <= 0 ||
                !Positive(data.propeller.maxAheadThrustN) ||
                !Positive(data.propeller.maxAsternThrustN))
                return Fail("Propeller configuration is invalid.", out error);
            if (!ArrayMatchesCount(data.propeller.longitudinalPositionsM, data.propeller.count) ||
                !ArrayMatchesCount(data.propeller.lateralPositionsM, data.propeller.count))
                return Fail("Propeller position arrays must match propeller.count.", out error);

            if (data.rudder == null || data.rudder.count <= 0 ||
                !Positive(data.rudder.maxAngleDeg) ||
                !Positive(data.rudder.rateDegPerSecond) ||
                !Positive(data.rudder.areaPerRudderM2))
                return Fail("Rudder configuration is invalid.", out error);

            if (data.hydrodynamics == null ||
                !Positive(data.hydrodynamics.waterDensityKgM3) ||
                !NonNegative(data.hydrodynamics.surgeLinearNPerMps) ||
                !NonNegative(data.hydrodynamics.surgeQuadraticNPerMps2) ||
                !NonNegative(data.hydrodynamics.swayLinearNPerMps) ||
                !NonNegative(data.hydrodynamics.swayQuadraticNPerMps2) ||
                !NonNegative(data.hydrodynamics.yawLinearNmPerRadps) ||
                !NonNegative(data.hydrodynamics.yawQuadraticNmPerRadps2))
                return Fail("Hydrodynamic configuration is invalid.", out error);

            if (data.buoyancy == null || data.buoyancy.pointCount <= 0 ||
                !Positive(data.buoyancy.maxPointDepthM) ||
                !Positive(data.buoyancy.reserveBuoyancyFactor) ||
                !NonNegative(data.buoyancy.verticalDampingNPerMpsPerPoint))
                return Fail("Buoyancy configuration is invalid.", out error);

            if (data.controlLimits == null ||
                !Positive(data.controlLimits.maxLoadedSpeedMps) ||
                !Positive(data.controlLimits.throttleCommandRatePerSecond) ||
                !Positive(data.controlLimits.rudderCommandRatePerSecond))
                return Fail("Control limits are invalid.", out error);

            if (data.calibration == null ||
                !Positive(data.calibration.thrustMultiplier) ||
                !Positive(data.calibration.resistanceMultiplier) ||
                !Positive(data.calibration.rudderMultiplier) ||
                !Positive(data.calibration.buoyancyMultiplier))
                return Fail("Calibration multipliers must be finite and positive.", out error);

            error = null;
            return true;
        }

        private static bool ArrayMatchesCount(float[] values, int count)
        {
            if (values == null || values.Length != count) return false;
            foreach (float value in values)
                if (!Finite(value)) return false;
            return true;
        }

        private static bool PositiveVector(Vector3 value)
        {
            return Positive(value.x) && Positive(value.y) && Positive(value.z);
        }

        private static bool InRange(float value, float minimum, float maximum)
        {
            return Finite(value) && value >= minimum && value <= maximum;
        }

        private static bool Positive(float value)
        {
            return Finite(value) && value > 0f;
        }

        private static bool NonNegative(float value)
        {
            return Finite(value) && value >= 0f;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
