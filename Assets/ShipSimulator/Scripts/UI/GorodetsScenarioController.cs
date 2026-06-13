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
        public string Instruction => BuildInstruction();
        public float LocalSpeedLimitMps { get; private set; } = 3.333f;

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

        private string BuildInstruction()
        {
            return Phase switch
            {
                GorodetsMissionPhase.DepartApproach =>
                    "Leave the lock approach and prepare for lateral set",
                GorodetsMissionPhase.AcquireGorodetsLeadingLine =>
                    "Acquire the Gorodets leading line",
                GorodetsMissionPhase.PassGorodetsShoal =>
                    "Hold the leading line and remain inside the marked fairway",
                GorodetsMissionPhase.PassUpperKochergino =>
                    "Anticipate the reversing lateral current",
                GorodetsMissionPhase.PassLowerKochergino =>
                    "Rocky bottom: maintain clearance and small rudder angles",
                GorodetsMissionPhase.ReachFinish =>
                    "Clear the lower shoal and cross the finish gate",
                GorodetsMissionPhase.Completed =>
                    $"Mission complete. Score {Score:F0}/100",
                GorodetsMissionPhase.Failed =>
                    $"Mission failed. Score {Score:F0}/100",
                _ => "Prepare vessel"
            };
        }
    }
}
