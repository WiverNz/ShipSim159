using System;
using ShipSimulator.Physics;
using UnityEngine;

namespace ShipSimulator.UI
{
    public enum GorodetsMissionPhase
    {
        Briefing,
        DepartApproach,
        AcquireGorodetsLeadingLine,
        PassGorodetsShoal,
        PassUpperKochergino,
        PassLowerKochergino,
        ReachFinish,
        Completed,
        Failed
    }

    public sealed class GorodetsScenarioController : MonoBehaviour
    {
        [SerializeField] private ShipPhysicsController ship;
        [SerializeField] private FairwayRoute route;
        [SerializeField] private ScenarioBathymetry bathymetry;
        [SerializeField] private GroundingController grounding;
        [SerializeField] private LeadingMarkPair[] leadingLines = Array.Empty<LeadingMarkPair>();
        [SerializeField] private float[] phaseDistancesM =
            { 60f, 300f, 700f, 1150f, 1600f, 1950f };
        // Other scenarios reuse this controller with their own wording; empty falls back to Gorodets.
        [SerializeField] private string[] phaseLabels = Array.Empty<string>();
        [SerializeField] private string[] phaseInstructions = Array.Empty<string>();

        private Vector3 startPosition;
        private Quaternion startRotation;
        private float outsideFairwaySeconds;
        private float overspeedSeconds;
        private float leadingErrorIntegral;
        private float previousRudder;
        private float controlPenalty;

        public GorodetsMissionPhase Phase { get; private set; } =
            GorodetsMissionPhase.Briefing;
        public float Score { get; private set; } = 100f;
        public float RouteDistanceM { get; private set; }
        public float CrossTrackErrorM { get; private set; }
        public float LeadingLineErrorDeg { get; private set; }
        public string PhaseLabel => Localization.Source(Entry(phaseLabels) ?? Phase.ToString());
        public string Instruction => BuildInstruction();
        public float LocalSpeedLimitMps { get; private set; } = 3.333f;
        public ShipPhysicsController Ship => ship;

        public void SetShip(ShipPhysicsController targetShip, GroundingController groundingController)
        {
            ship = targetShip;
            grounding = groundingController;
            if (ship == null) return;
            startPosition = ship.transform.position;
            startRotation = ship.transform.rotation;
        }

        public void Configure(ShipPhysicsController targetShip, FairwayRoute fairway,
            ScenarioBathymetry depthProvider, GroundingController groundingController,
            LeadingMarkPair[] marks)
        {
            ship = targetShip;
            route = fairway;
            bathymetry = depthProvider;
            grounding = groundingController;
            leadingLines = marks ?? Array.Empty<LeadingMarkPair>();
            if (ship != null)
            {
                startPosition = ship.transform.position;
                startRotation = ship.transform.rotation;
            }
        }

        public void ConfigurePhases(float[] distancesM, string[] labels, string[] instructions)
        {
            if (distancesM != null && distancesM.Length >= 6) phaseDistancesM = distancesM;
            phaseLabels = labels ?? Array.Empty<string>();
            phaseInstructions = instructions ?? Array.Empty<string>();
        }

        private void Start()
        {
            if (ship != null)
            {
                startPosition = ship.transform.position;
                startRotation = ship.transform.rotation;
            }
            Phase = GorodetsMissionPhase.DepartApproach;
        }

        private void Update()
        {
            if (VoyageMenu.IsOpen) return;
            if (ship == null || ship.Body == null || route == null ||
                Phase == GorodetsMissionPhase.Completed ||
                Phase == GorodetsMissionPhase.Failed)
                return;

            FairwayQuery query = route.Query(ship.transform.position);
            RouteDistanceM = query.RouteDistanceM;
            CrossTrackErrorM = query.LateralOffsetM;
            LocalSpeedLimitMps = Mathf.Max(0.5f, query.Sample.speedLimitMps);
            AdvancePhase();

            float allowedWidth = CrossTrackErrorM >= 0f
                ? query.Sample.rightWidthM
                : query.Sample.leftWidthM;
            if (Mathf.Abs(CrossTrackErrorM) > allowedWidth)
                outsideFairwaySeconds += Time.deltaTime;
            if (ship.Body.linearVelocity.magnitude > LocalSpeedLimitMps * 1.05f)
                overspeedSeconds += Time.deltaTime;

            LeadingLineErrorDeg = 0f;
            foreach (LeadingMarkPair pair in leadingLines)
            {
                if (pair == null || !pair.IsActiveAt(RouteDistanceM)) continue;
                LeadingLineErrorDeg = pair.AngularErrorDeg(ship.transform.position);
                leadingErrorIntegral += Mathf.Max(0f,
                    Mathf.Abs(LeadingLineErrorDeg) - 0.4f) * Time.deltaTime;
                break;
            }

            float rudderDelta = Mathf.Abs(ship.RudderCommand - previousRudder);
            if (rudderDelta > 0.35f)
                controlPenalty += rudderDelta * 0.25f;
            previousRudder = ship.RudderCommand;

            float groundingPenalty = grounding == null
                ? 0f
                : grounding.DamagePoints * 0.8f +
                  (grounding.State == GroundingState.Touching ? 4f : 0f);
            Score = Mathf.Clamp(100f -
                outsideFairwaySeconds * 0.15f -
                overspeedSeconds * 0.1f -
                leadingErrorIntegral * 0.025f -
                controlPenalty -
                groundingPenalty, 0f, 100f);

            if (grounding != null &&
                (grounding.DamagePoints >= 25f ||
                 grounding.State == GroundingState.HardGrounding &&
                 ship.Body.linearVelocity.magnitude < 0.1f))
                Phase = GorodetsMissionPhase.Failed;
        }

        public ShipSimulator.Persistence.MissionSave CaptureState() =>
            new ShipSimulator.Persistence.MissionSave
            {
                phase = (int)Phase, score = Score, outsideSeconds = outsideFairwaySeconds,
                overspeedSeconds = overspeedSeconds, leadingIntegral = leadingErrorIntegral,
                previousRudder = previousRudder, controlPenalty = controlPenalty
            };

        public void RestoreState(ShipSimulator.Persistence.MissionSave save)
        {
            Phase = (GorodetsMissionPhase)save.phase;
            Score = save.score;
            outsideFairwaySeconds = save.outsideSeconds;
            overspeedSeconds = save.overspeedSeconds;
            leadingErrorIntegral = save.leadingIntegral;
            previousRudder = save.previousRudder;
            controlPenalty = save.controlPenalty;
        }

        public void ResetMission()
        {
            if (ship != null && ship.Body != null)
            {
                ship.Body.position = startPosition;
                ship.Body.rotation = startRotation;
                ship.ResetToStart();
            }
            grounding?.ResetState();
            outsideFairwaySeconds = 0f;
            overspeedSeconds = 0f;
            leadingErrorIntegral = 0f;
            controlPenalty = 0f;
            Score = 100f;
            Phase = GorodetsMissionPhase.DepartApproach;
        }

        private void AdvancePhase()
        {
            if (phaseDistancesM == null || phaseDistancesM.Length < 6) return;
            if (RouteDistanceM >= phaseDistancesM[5])
                Phase = GorodetsMissionPhase.Completed;
            else if (RouteDistanceM >= phaseDistancesM[4])
                Phase = GorodetsMissionPhase.ReachFinish;
            else if (RouteDistanceM >= phaseDistancesM[3])
                Phase = GorodetsMissionPhase.PassLowerKochergino;
            else if (RouteDistanceM >= phaseDistancesM[2])
                Phase = GorodetsMissionPhase.PassUpperKochergino;
            else if (RouteDistanceM >= phaseDistancesM[1])
                Phase = GorodetsMissionPhase.PassGorodetsShoal;
            else if (RouteDistanceM >= phaseDistancesM[0])
                Phase = GorodetsMissionPhase.AcquireGorodetsLeadingLine;
        }

        private string Entry(string[] table)
        {
            int index = (int)Phase;
            return table != null && index < table.Length && !string.IsNullOrEmpty(table[index])
                ? table[index]
                : null;
        }

        private string BuildInstruction()
        {
            string custom = Entry(phaseInstructions);
            if (custom != null)
                return Phase == GorodetsMissionPhase.Completed || Phase == GorodetsMissionPhase.Failed
                    ? Localization.Get("text.0.score.1.f0.100.aabc81fd", Localization.Source(custom), Score)
                    : Localization.Source(custom);
            return Phase switch
            {
                GorodetsMissionPhase.DepartApproach =>
                    Localization.Get("text.leave.the.lock.approach.and.prepare.for.lateral.set.a4635afe"),
                GorodetsMissionPhase.AcquireGorodetsLeadingLine =>
                    Localization.Get("text.acquire.the.gorodets.leading.line.cc5bbf0b"),
                GorodetsMissionPhase.PassGorodetsShoal =>
                    Localization.Get("text.hold.the.leading.line.and.remain.inside.the.marked.fairw.61b855ce"),
                GorodetsMissionPhase.PassUpperKochergino =>
                    Localization.Get("text.anticipate.the.reversing.lateral.current.959e862a"),
                GorodetsMissionPhase.PassLowerKochergino =>
                    Localization.Get("text.rocky.bottom.maintain.clearance.and.small.rudder.angles.1c79e8f8"),
                GorodetsMissionPhase.ReachFinish =>
                    Localization.Get("text.clear.the.lower.shoal.and.cross.the.finish.gate.cf65c295"),
                GorodetsMissionPhase.Completed =>
                    Localization.Get("text.mission.complete.score.0.f0.100.e1531559", Score),
                GorodetsMissionPhase.Failed =>
                    Localization.Get("text.mission.failed.score.0.f0.100.e7c6b77d", Score),
                _ => Localization.Get("text.prepare.vessel.c3dcea26")
            };
        }
    }
}
