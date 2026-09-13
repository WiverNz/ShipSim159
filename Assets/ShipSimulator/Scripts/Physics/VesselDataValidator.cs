using UnityEngine;

namespace ShipSimulator.Physics
{
    public static class VesselDataValidator
    {
        public static bool TryValidate(VesselData data, out string error)
        {
            if (data == null) return Fail("Root object is missing.", out error);
            if (data.identity == null || string.IsNullOrWhiteSpace(data.identity.displayName))
                return Fail("identity.displayName is required.", out error);

            VesselDimensions d = data.dimensions;
            if (d == null) return Fail("dimensions section is missing.", out error);
            if (!Positive(d.lengthOverallM) || !Positive(d.lengthBetweenPerpendicularsM) ||
                !Positive(d.beamOverallM) || !Positive(d.beamMouldedM) ||
                !Positive(d.depthMouldedM) || !Positive(d.loadedDraftM))
                return Fail("Vessel length, beam, depth and draft must be finite and positive.", out error);
            if (d.loadedDraftM >= d.depthMouldedM)
                return Fail("Loaded draft must be less than moulded depth.", out error);
            if (d.lengthBetweenPerpendicularsM > d.lengthOverallM || d.beamMouldedM > d.beamOverallM)
                return Fail("Length between perpendiculars and moulded beam cannot exceed overall values.", out error);

            VesselMassProperties m = data.massProperties;
            if (m == null) return Fail("massProperties section is missing.", out error);
            if (!Positive(m.lightshipMassKg) || !Positive(m.loadedMassKg) || m.loadedMassKg < m.lightshipMassKg)
                return Fail("Mass values are invalid.", out error);
            if (!InRange(m.loadFraction, 0f, 1f))
                return Fail("massProperties.loadFraction must be between 0 and 1.", out error);
            if (!FiniteVector(m.centerOfMassLocalM) || !Positive(m.rollGyrationRadiusM) ||
                !Positive(m.pitchGyrationRadiusM) || !Positive(m.yawGyrationRadiusM))
                return Fail("Centre of mass and radii of gyration must be finite, radii positive.", out error);

            VesselHydrostatics h = data.hydrostatics;
            if (h == null || !Positive(h.waterDensityKgM3) || !Finite(h.waterlineLocalY) ||
                !InRange(h.blockCoefficient, 0.3f, 1f) || !InRange(h.midshipCoefficient, 0.5f, 1f) ||
                !InRange(h.waterplaneCoefficient, 0.67f, 1f) || h.waterplaneCoefficient < h.blockCoefficient ||
                !InRange(h.longitudinalCentreOfBuoyancyM, -0.1f * d.lengthBetweenPerpendicularsM, 0.1f * d.lengthBetweenPerpendicularsM) ||
                h.stationCount < 4 || h.stripsAcross < 2 || !NonNegative(h.heaveDampingRatio) ||
                !NonNegative(h.heaveAddedMassFraction) || !NonNegative(h.rollDampingRatio))
                return Fail("Hydrostatics configuration is invalid.", out error);

            VesselResistance r = data.resistance;
            if (r == null || !Positive(r.kinematicViscosityM2PerS) || !NonNegative(r.wettedSurfaceM2) ||
                !(Finite(r.formFactor) && r.formFactor >= 1f) || !Finite(r.correlationAllowance) ||
                !NonNegative(r.residualResistanceCoefficient))
                return Fail("Resistance configuration is invalid.", out error);

            VesselHull hull = data.hull;
            if (hull == null || !NonNegative(hull.surgeAddedMassPrime) || !NonNegative(hull.swayAddedMassPrime) ||
                !NonNegative(hull.yawAddedInertiaPrime) ||
                !AllFinite(hull.xVV, hull.xVR, hull.xRR, hull.xVVVV, hull.yR, hull.yVVV, hull.yVVR, hull.yVRR,
                    hull.yRRR, hull.nV, hull.nVVV, hull.nVVR, hull.nVRR, hull.nRRR) ||
                !(Finite(hull.yV) && hull.yV < 0f) || !(Finite(hull.nR) && hull.nR < 0f) ||
                !Positive(hull.maxYawRatePrime) || !NonNegative(hull.lowSpeedBlendStartMps) ||
                !(Finite(hull.lowSpeedBlendEndMps) && hull.lowSpeedBlendEndMps > hull.lowSpeedBlendStartMps) ||
                hull.crossFlowStations < 4)
                return Fail("Hull manoeuvring coefficients are invalid.", out error);

            VesselEngine e = data.engine;
            if (e == null || e.engineCount <= 0 || !Positive(e.powerPerEngineW) ||
                !InRange(e.gearEfficiency, 0.01f, 1f) || !InRange(e.shaftEfficiency, 0.01f, 1f) ||
                !Positive(e.ratedPropellerRpm) || !Positive(e.shaftInertiaKgM2) ||
                !InRange(e.governorBandFraction, 0.001f, 0.5f) || !Positive(e.rpmRampSecondsFullRange) ||
                !InRange(e.reversalRpmFraction, 0f, 0.99f) || !NonNegative(e.reversalDelaySeconds) ||
                !InRange(e.reversalBrakeTorqueFraction, 0f, 1f))
                return Fail("Engine configuration is invalid.", out error);

            VesselPropeller p = data.propeller;
            if (p == null || p.count <= 0 || p.count != e.engineCount || !Positive(p.diameterM))
                return Fail("Propeller configuration is invalid; each propeller needs one engine.", out error);
            if (!ArrayMatchesCount(p.longitudinalPositionsM, p.count) || !ArrayMatchesCount(p.lateralPositionsM, p.count))
                return Fail("Propeller position arrays must match propeller.count.", out error);
            if (!Coefficients(p.aheadThrustCoefficients) || !Coefficients(p.aheadTorqueCoefficients) ||
                !Coefficients(p.asternThrustCoefficients) || !Coefficients(p.asternTorqueCoefficients))
                return Fail("Propeller thrust and torque polynomials need three finite coefficients with a positive bollard value.", out error);
            if (!InRange(p.wakeFraction, 0f, 0.8f) || !InRange(p.thrustDeduction, 0f, 0.6f) ||
                !NonNegative(p.wakeDriftC1) || !Positive(p.wakeDriftC2))
                return Fail("Propeller wake and thrust deduction values are invalid.", out error);

            VesselRudder rd = data.rudder;
            if (rd == null || rd.count <= 0 || !InRange(rd.maxAngleDeg, 1f, 89f) || !Positive(rd.rateDegPerSecond) ||
                !Positive(rd.areaPerRudderM2) || !Positive(rd.spanM) || !Finite(rd.longitudinalPositionM) ||
                !ArrayMatchesCount(rd.lateralPositionsM, rd.count) || !NonNegative(rd.normalForceSlope) ||
                !InRange(rd.stallAngleDeg, 1f, 89f) || !NonNegative(rd.postStallNormalCoefficient) ||
                !InRange(rd.steeringResistanceDeduction, 0f, 0.99f) || !NonNegative(rd.hullInteractionFactor) ||
                !Finite(rd.hullInteractionPositionPrime) || !Positive(rd.wakeRatio) ||
                !InRange(rd.slipstreamFactor, 0f, 1f) || !NonNegative(rd.flowStraightening) ||
                !Finite(rd.flowStraighteningLeverPrime))
                return Fail("Rudder configuration is invalid.", out error);

            VesselBowThruster bt = data.bowThruster;
            if (bt != null && bt.fitted &&
                (!Positive(bt.powerW) || !Positive(bt.tunnelDiameterM) ||
                 !InRange(bt.longitudinalPositionM, 0f, 0.5f * d.lengthOverallM) ||
                 !InRange(bt.axisHeightAboveKeelM, 0f, d.depthMouldedM) || !InRange(bt.figureOfMerit, 0.1f, 1f) ||
                 !Positive(bt.rampSecondsToFull) || !Positive(bt.speedLossReferenceMps) ||
                 !InRange(bt.minimumSpeedEffectiveness, 0f, 1f)))
                return Fail("Bow thruster configuration is invalid.", out error);

            VesselWindage w = data.windage;
            if (w == null || !Positive(w.airDensityKgM3) || !Positive(w.frontalAreaM2) || !Positive(w.lateralAreaM2) ||
                !Finite(w.lateralCentroidLongitudinalM) || !NonNegative(w.lateralCentroidHeightM) ||
                !Positive(w.transverseDragCoefficient) || !NonNegative(w.longitudinalDragBow) ||
                !NonNegative(w.longitudinalDragStern) || !InRange(w.crossForceParameter, 0f, 0.99f))
                return Fail("Windage configuration is invalid.", out error);

            VesselRestrictedWater rw = data.restrictedWater;
            if (rw == null || !NonNegative(rw.squatCoefficient) || !Positive(rw.bankSamplingWidthBeams) ||
                !NonNegative(rw.bankSuctionCoefficient) || !Finite(rw.bankMomentLeverPrime))
                return Fail("Restricted-water configuration is invalid.", out error);

            if (data.controlLimits == null || !Positive(data.controlLimits.maxLoadedSpeedMps) ||
                !Positive(data.controlLimits.throttleCommandRatePerSecond) ||
                !Positive(data.controlLimits.rudderCommandRatePerSecond))
                return Fail("Control limits are invalid.", out error);

            if (data.calibration == null || !Positive(data.calibration.thrustMultiplier) ||
                !Positive(data.calibration.resistanceMultiplier) || !Positive(data.calibration.rudderMultiplier))
                return Fail("Calibration multipliers must be finite and positive.", out error);

            error = null;
            return true;
        }

        private static bool Coefficients(float[] values)
        {
            return ArrayMatchesCount(values, 3) && values[0] > 0f;
        }

        private static bool ArrayMatchesCount(float[] values, int count)
        {
            if (values == null || values.Length != count) return false;
            foreach (float value in values)
                if (!Finite(value)) return false;
            return true;
        }

        private static bool AllFinite(params float[] values)
        {
            foreach (float value in values)
                if (!Finite(value)) return false;
            return true;
        }

        private static bool FiniteVector(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool InRange(float value, float minimum, float maximum) => Finite(value) && value >= minimum && value <= maximum;
        private static bool Positive(float value) => Finite(value) && value > 0f;
        private static bool NonNegative(float value) => Finite(value) && value >= 0f;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Fail(string message, out string error)
        {
            error = message;
            return false;
        }
    }
}
