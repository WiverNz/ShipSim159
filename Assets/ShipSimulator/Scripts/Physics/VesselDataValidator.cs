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
                // Fast craft hulls are far finer than merchant hulls, so the lower bounds stay loose.
                !InRange(h.blockCoefficient, 0.2f, 1f) || !InRange(h.midshipCoefficient, 0.35f, 1f) ||
                !InRange(h.waterplaneCoefficient, 0.5f, 1f) || h.waterplaneCoefficient < h.blockCoefficient ||
                !InRange(h.longitudinalCentreOfBuoyancyM, -0.1f * d.lengthBetweenPerpendicularsM, 0.1f * d.lengthBetweenPerpendicularsM) ||
                h.stationCount < 4 || h.stripsAcross < 2 || !NonNegative(h.heaveDampingRatio) ||
                !NonNegative(h.heaveAddedMassFraction) || !NonNegative(h.rollDampingRatio))
                return Fail("Hydrostatics configuration is invalid.", out error);
            if (SectionHydrostatics.Supported(data) && !SectionTable(h.sectionTable, d))
                return Fail("Hydrostatic section table is invalid.", out error);

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

            if (data.nonSelfPropelled)
            {
                if (data.engine == null || data.engine.engineCount != 0 || data.engine.powerPerEngineW != 0 ||
                    data.propeller == null || data.propeller.count != 0 || data.rudder == null || data.rudder.count != 0 ||
                    data.bowThruster != null && data.bowThruster.fitted || data.steeringNozzle != null && data.steeringNozzle.enabled ||
                    data.loading != null && data.loading.enabled)
                    return Fail("A passive vessel must have no propulsion, rudders or powered loading model.", out error);
                if (!AllFinite(data.engine.ratedPropellerRpm, data.engine.gearEfficiency, data.engine.shaftEfficiency,
                    data.rudder.maxAngleDeg, data.rudder.rateDegPerSecond))
                    return Fail("Passive vessel actuator placeholders must be finite.", out error);
            }
            else
            {
                VesselEngine e = data.engine;
                if (e == null || e.engineCount <= 0 || !Positive(e.powerPerEngineW) ||
                    !InRange(e.gearEfficiency, 0.01f, 1f) || !InRange(e.shaftEfficiency, 0.01f, 1f) ||
                    !Positive(e.ratedPropellerRpm) || !Positive(e.shaftInertiaKgM2) ||
                    !InRange(e.governorBandFraction, 0.001f, 0.5f) || !Positive(e.rpmRampSecondsFullRange) ||
                    !InRange(e.reversalRpmFraction, 0f, 0.99f) || !NonNegative(e.reversalDelaySeconds) ||
                    !InRange(e.reversalBrakeTorqueFraction, 0f, 1f))
                    return Fail("Engine configuration is invalid.", out error);
                if (e.cylinders < 0 || e.cylinders > 24 || (e.strokesPerCycle != 0 && e.strokesPerCycle != 2 && e.strokesPerCycle != 4) ||
                    !(e.engineRpmPerShaftRpm == 0f || (Finite(e.engineRpmPerShaftRpm) && e.engineRpmPerShaftRpm > 0f && e.engineRpmPerShaftRpm < 20f)))
                    return Fail("Engine equipment facts are invalid: cylinders 0-24, strokesPerCycle 0, 2 or 4, engineRpmPerShaftRpm 0 or positive.", out error);

                VesselPropeller p = data.propeller;
                if (p == null || p.count <= 0 || p.count != e.engineCount || !Positive(p.diameterM))
                    return Fail("Propeller configuration is invalid; each propeller needs one engine.", out error);
                if (p.bladeCount < 0 || p.bladeCount > 12)
                    return Fail("Propeller bladeCount must be 0 (not established) to 12.", out error);
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

                VesselSteeringNozzle nozzle = data.steeringNozzle;
                if (nozzle != null && nozzle.enabled)
                {
                    if (!InRange(nozzle.thrustDeflectionFactor, 0f, 1f) || !Positive(nozzle.passiveAreaM2) ||
                        !Positive(nozzle.passiveNormalSlope) || rd.count != p.count)
                        return Fail("Steering nozzles require valid coefficients and one nozzle per shaft.", out error);
                    for (int i = 0; i < p.count; i++)
                        if (Mathf.Abs(rd.lateralPositionsM[i] - p.lateralPositionsM[i]) > .01f ||
                            Mathf.Abs(rd.longitudinalPositionM - p.longitudinalPositionsM[i]) > .01f)
                            return Fail("Steering nozzle and shaft positions must coincide.", out error);
                }

            }

            VesselBowThruster bt = data.bowThruster;
            if (bt != null && bt.fitted &&
                (!Positive(bt.powerW) || !Positive(bt.tunnelDiameterM) ||
                 !InRange(bt.longitudinalPositionM, 0f, 0.5f * d.lengthOverallM) ||
                 !InRange(bt.axisHeightAboveKeelM, 0f, d.depthMouldedM) || !InRange(bt.figureOfMerit, 0.1f, 1f) ||
                 !Positive(bt.rampSecondsToFull) || !Positive(bt.speedLossReferenceMps) ||
                 !InRange(bt.minimumSpeedEffectiveness, 0f, 1f)))
                return Fail("Bow thruster configuration is invalid.", out error);

            VesselSupport support = data.support;
            if (support != null && support.supportedWeightFraction > 0f &&
                (!NonNegative(support.takeoffSpeedMps) ||
                 !(Finite(support.fullSupportSpeedMps) && support.fullSupportSpeedMps > support.takeoffSpeedMps) ||
                 !InRange(support.supportedWeightFraction, 0f, 0.98f) ||
                 !InRange(support.supportedDraftM, 0f, d.depthMouldedM) ||
                 !InRange(support.supportedResistanceFactor, 0.05f, 1f) ||
                 !InRange(support.humpResistanceFactor, 1f, 4f) ||
                 !InRange(support.supportLongitudinalPositionM, -d.lengthOverallM, d.lengthOverallM) ||
                 !InRange(support.liftPowerFraction, 0f, 0.6f)))
                return Fail("Cushion or hydrofoil support configuration is invalid.", out error);

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

            if (LoadingModel.Supported(data))
            {
                VesselLoading loading = data.loading;
                if (!FiniteVector(loading.lightshipCentreLocalM) || loading.lightshipCentreLocalM.x != 0 ||
                    !InRange(loading.propellerAxisHeightAboveKeelM, 0, d.depthMouldedM) ||
                    !Positive(loading.maximumTransferSpeedMps) || loading.compartments == null ||
                    loading.compartments.Length == 0 || loading.compartments.Length > 32 || !Positive(m.deadweightKg))
                    return Fail("Loading configuration is invalid.", out error);
                float initial = 0;
                var ids = new System.Collections.Generic.HashSet<string>();
                foreach (LoadCompartment c in loading.compartments)
                {
                    if (c == null || string.IsNullOrWhiteSpace(c.id) || !ids.Add(c.id) ||
                        (c.kind != "cargo" && c.kind != "ballast" && c.kind != "consumables") ||
                        !Positive(c.capacityKg) || !InRange(c.initialKg, 0, c.capacityKg) || !Positive(c.transferKgPerSecond) ||
                        !FiniteVector(c.centreLocalM) || c.centreLocalM.x != 0 ||
                        !FiniteVector(c.sizeM) || !Positive(c.sizeM.x) || !Positive(c.sizeM.y) || !Positive(c.sizeM.z) ||
                        c.sizeM.x > d.beamOverallM || Mathf.Abs(c.centreLocalM.z) + c.sizeM.z / 2 > d.lengthOverallM / 2 ||
                        c.centreLocalM.y - c.sizeM.y / 2 < data.hydrostatics.waterlineLocalY - d.loadedDraftM ||
                        c.centreLocalM.y + c.sizeM.y / 2 > data.hydrostatics.waterlineLocalY - d.loadedDraftM + d.depthMouldedM)
                        return Fail("Loading compartment is invalid or outside the supported symmetric layout.", out error);
                    initial += c.initialKg;
                }
                if (Mathf.Abs(initial - (m.loadedMassKg - m.lightshipMassKg)) > 1 || initial > m.deadweightKg + 1)
                    return Fail("Initial cargo, ballast and stores must equal the loaded payload within deadweight.", out error);
            }

            if (data.groundTackle != null && data.groundTackle.fitted && !GroundTackle(data.groundTackle))
                return Fail("Ground tackle configuration is invalid.", out error);

            error = null;
            return true;
        }

        private static bool Drive(string drive) => string.IsNullOrEmpty(drive) || drive == "electric" || drive == "electrohydraulic";

        private static bool GroundTackle(VesselGroundTackle g)
        {
            if (g.anchors == null || g.anchors.Length == 0 || g.anchors.Length > 4 || g.stations == null ||
                g.stations.Length > 32 || !Positive(g.letGoMps) || !Positive(g.veerMps) || !Positive(g.renderMaxMps) ||
                !InRange(g.brakeHoldingFraction, 0.05f, 1f) || !Positive(g.siltHoldingRatio) ||
                !Positive(g.sandHoldingRatio) || !NonNegative(g.rockHoldingRatio) || !InRange(g.chainBedFriction, 0f, 2f) ||
                !InRange(g.initialSetFraction, 0f, 1f) || !Positive(g.setDistanceM) || !InRange(g.maxUpliftDeg, 1f, 90f) ||
                !InRange(g.breakoutWeightFactor, 1f, 20f) || !InRange(g.dampingRatio, 0f, 1f) ||
                !Positive(g.lineBreakingLoadN) || !Positive(g.lineAxialStiffnessN) || !Positive(g.lineMassPerMetreKg) ||
                !Positive(g.lineMinLengthM) || !(Finite(g.lineMaxLengthM) && g.lineMaxLengthM > g.lineMinLengthM) ||
                !Positive(g.slackMps) || !InRange(g.stallPullFactor, 1f, 5f) || !InRange(g.overloadFraction, 0.05f, 1f) ||
                !Positive(g.passReachM) || g.passReachM > g.lineMaxLengthM || !Positive(g.passMaxSpeedMps) ||
                g.linesPerBollard < 1 || g.linesPerBollard > 8 || !Drive(g.windlassDrive) || !Drive(g.mooringWinchDrive))
                return false;
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (VesselAnchor a in g.anchors)
                if (a == null || string.IsNullOrWhiteSpace(a.id) || !ids.Add(a.id) || string.IsNullOrWhiteSpace(a.label) ||
                    !Positive(a.massKg) || !Positive(a.chainDiameterMm) || !InRange(a.chainLengthM, 1f, 1000f) ||
                    !Positive(a.chainMassPerMetreKg) || !Positive(a.chainAxialStiffnessN) || !Positive(a.chainBreakingLoadN) ||
                    !Positive(a.heaveMps) || !Positive(a.nominalPullN) || !(Finite(a.stallPullN) && a.stallPullN > a.nominalPullN))
                    return false;
            ids.Clear();
            foreach (VesselMooringStation s in g.stations)
                if (s == null || string.IsNullOrWhiteSpace(s.id) || !ids.Add(s.id) || !Positive(s.winchPullN) || !Positive(s.haulMps))
                    return false;
            return true;
        }

        // Stations ascend along the length, offsets ascend in height within the capped surface, and every
        // breadth lies within the strips that carry it.
        private static bool SectionTable(VesselSectionTable table, VesselDimensions d)
        {
            if (table.halfStripsAcross < 1 || table.halfStripsAcross > 16 ||
                !InRange(table.stripHalfWidthM, 0.5f * d.beamMouldedM, 0.6f * d.beamOverallM) ||
                !InRange(table.capHeightAboveKeelM, d.loadedDraftM, 2f * d.depthMouldedM) ||
                !InRange(table.tableHeightStepM, 0.005f, 0.5f) || table.tableHeightStepM > table.capHeightAboveKeelM / 4f ||
                !InRange(table.minimumMeanDraftM, 0.01f, table.capHeightAboveKeelM) ||
                !InRange(table.maximumMeanDraftM, table.minimumMeanDraftM, table.capHeightAboveKeelM) ||
                !Positive(table.maximumTrimM) || table.stations == null || table.stations.Length < 3 ||
                table.stations.Length > 512)
                return false;
            float previous = float.NegativeInfinity;
            foreach (VesselSectionStation station in table.stations)
            {
                if (station == null || !Finite(station.localZ) || station.localZ <= previous ||
                    Mathf.Abs(station.localZ) > d.lengthOverallM ||
                    station.heightsAboveKeelM == null || station.halfBreadthsM == null ||
                    station.heightsAboveKeelM.Length == 0 || station.heightsAboveKeelM.Length > 128 ||
                    station.heightsAboveKeelM.Length != station.halfBreadthsM.Length)
                    return false;
                previous = station.localZ;
                float height = 0f;
                for (int i = 0; i < station.heightsAboveKeelM.Length; i++)
                {
                    float z = station.heightsAboveKeelM[i];
                    if (!InRange(z, height, table.capHeightAboveKeelM) ||
                        !InRange(station.halfBreadthsM[i], 0f, table.stripHalfWidthM))
                        return false;
                    height = z;
                }
            }
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
