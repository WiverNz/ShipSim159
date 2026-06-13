# Gorodets Reach Scenario: Technical Plan

## Status and Scope

This document defines a technical implementation plan for a high-difficulty
training mission inspired by the reach from the Gorodets lock approach through
the Gorodets and Kochergino shoals toward Balakhna.

The controlled vessel remains the project's existing Project 507B Volgo-Don
river dry cargo vessel. The scenario must use its current dimensions, loaded
draft, mass, propulsion response, rudder response, cameras, and controls.

The scene is an engineering training abstraction, not a chart, pilot book, or
navigation aid. Distances, depths, currents, marks, discharge timing, and
traffic behavior are estimated gameplay parameters until validated against
surveyed bathymetry, current measurements, current navigation publications,
and specialist review.

## Mission Definition

Working title: `Gorodets Shoal: Exit Under Hydropower Discharge`

Primary objective:

1. Leave the confined lower lock approach.
2. Stabilize the vessel before its bow enters the main current.
3. Acquire and hold the Gorodets leading line.
4. Pass the marked shoal without grounding or leaving the fairway.
5. Correct for a reversing lateral set through the upper Kochergino section.
6. Clear the lower rocky section and reach the finish gate.

Nominal mission length should be 1.8-2.4 km in world scale. This is long enough
for the 138.3 m vessel to expose delayed control response while remaining
practical for repeated training runs. It is not a one-to-one representation of
the real reach.

Target run time is 12-18 minutes. The first implementation should be daylight,
good visibility, and a deterministic discharge schedule. Fog and traffic are
later variants.

The HUD provides 1x, 2x, and 4x simulation-time controls. `T` cycles the
available scales and `Shift+T` returns to 1x.

## Scene Strategy

Create a separate scene:

`Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity`

Do not replace `RiverTrainingScene`. It remains the compact systems test and
general handling scene. Add the new scene after it in Build Settings.

Create a dedicated editor builder:

`Assets/ShipSimulator/Scripts/Editor/GorodetsScenarioBuilder.cs`

The builder should generate scenario-owned geometry, navigation marks, current
volumes, gates, hazards, and mission configuration without rebuilding the
vessel prefab or shared materials.

Recommended hierarchy:

```text
GorodetsTrainingScene
  Environment
    Water
    LockApproach
    Banks
    Shoals
    RockyHazards
  Navigation
    LateralMarks
    GorodetsLeadingLine
    UpperKocherginoLeadingLine
    LowerKocherginoLeadingLine
  CurrentField
    ApproachFlow
    LockExitSet
    MainStreamEntry
    GorodetsFlow
    UpperKocherginoWhiteSet
    UpperKocherginoRedSet
    LowerKocherginoFlow
  Mission
    GorodetsScenarioController
    StartGate
    HoldPoint
    SectionGates
    FinishGate
    GroundingHazards
  Traffic
  TrainingVessel
  TrainingUI
  Main Camera
```

## Route Layout

Use local route distance rather than real river kilometer numbers for physics
and scoring. Store display names separately.

| Section | Route distance | Width | Design purpose |
|---|---:|---:|---|
| Lower lock approach | 0-300 m | 45-55 m | Confined start and limited correction room |
| Lock exit transition | 300-500 m | 60-80 m | Lateral set begins before the full vessel clears |
| Gorodets leading line | 500-950 m | 70-85 m | Precise alignment and shallow margins |
| Upper Kochergino | 950-1450 m | 75-95 m | Lateral set changes from one edge to the other |
| Lower Kochergino | 1450-1900 m | 65-80 m | Rocky bottom and reduced recovery margin |
| Balakhna direction finish | 1900-2200 m | 90-110 m | Recovery and mission completion |

The centerline must be represented by sampled route data, not another set of
hard-coded sine functions. Introduce a scenario route asset containing control
points, marked widths, target headings, speed limits, and nominal depths.

Suggested types:

- `FairwayRouteData`: ScriptableObject with ordered route samples.
- `FairwaySample`: distance, world position, tangent, left/right marked width,
  center depth, edge depths, bottom type, and local speed limit.
- `FairwayRoute`: runtime interpolation and nearest-point queries.

Refactor HUD and buoy placement to consume an `IFairwayProvider`. Keep the
current static `FairwayModel` behind an adapter for `RiverTrainingScene`.

## Bathymetry and Bottom

The current `FairwayModel.DepthAt` is adequate for the existing prototype but
cannot represent isolated shoals, asymmetric slopes, or rocky hazards.

Implement a sampled bathymetry provider:

`ScenarioBathymetry.DepthAt(Vector3 worldPosition)`

Depth should combine:

1. Interpolated cross-section depth from the nearest route sample.
2. Authored shoal patches represented by polygon or spline masks.
3. A scenario-wide water-level offset.
4. Optional local irregularity noise with a fixed seed and small amplitude.

Initial estimated depth envelope for the loaded 3.53 m draft:

- center of maintained fairway: 4.3-5.2 m;
- caution margin: under-keel clearance below 1.0 m;
- critical margin: under-keel clearance below 0.4 m;
- outside marked edge: transition toward 1.5-2.5 m;
- rocky hazard patches: depth sufficient to create contact risk near the edge.

Do not create invisible vertical walls at the fairway edge. The consequence of
leaving the channel should first be falling clearance, then bottom contact.
Bank colliders remain only at the physical shoreline.

Represent bottom type as `Silt`, `Sand`, or `Rock`. It drives contact severity
and feedback, not different hydrodynamic coefficients in the first version.

## Current Field

Replace overlapping-zone averaging for this scene. Averaging prevents a base
downstream flow and a local lateral set from composing correctly.

Introduce:

- `CurrentFieldProvider`: returns current velocity at a world position and
  simulation time.
- `CurrentRegion`: oriented box or spline region with velocity, blend distance,
  priority, and composition mode.
- composition modes: `Additive`, `Override`, and `MaxMagnitude`.
- `HydropowerDischargeController`: controls a normalized discharge value and
  propagates it to current and water-level systems.

`ShipPhysicsController` should query the provider at the vessel center of mass.
A later refinement may sample bow, center, and stern to produce current shear
and yaw moment. The first scenario version should include three-point sampling
because a 138.3 m vessel entering the stream bow-first is the central handling
challenge.

For each sample point:

1. Query local current.
2. Apply resistance using the local relative water velocity.
3. Convert the difference between bow/stern current into a bounded yaw moment.

Keep the existing single-current calculation as the fallback when no field
provider exists.

Initial estimated current presets:

| Region | Longitudinal component | Lateral component | Behavior |
|---|---:|---:|---|
| Lock approach | 0.15-0.35 m/s | 0-0.10 m/s | Mostly sheltered |
| Lock exit | 0.45-0.80 m/s | 0.35-0.65 m/s | Set toward the left bank |
| Main-stream entry | 0.9-1.4 m/s | 0.15-0.35 m/s | Strong bow/stern differential |
| Gorodets shoal | 0.8-1.3 m/s | up to 0.25 m/s | Narrow usable corridor |
| Upper Kochergino A | 0.8-1.2 m/s | toward white marks, 0.3-0.6 m/s | First lateral set |
| Upper Kochergino B | 0.8-1.2 m/s | toward red marks, 0.3-0.6 m/s | Reversing set |
| Lower Kochergino | 0.7-1.1 m/s | 0.15-0.35 m/s | Low recovery margin |

The optional peak-discharge variant may reach 1.9 m/s locally, but it should
not be the default until vessel control remains playable at the nominal preset.

## Water Level and Shallow-Water Effects

Add a shared `WaterLevelProvider`. Bathymetry returns bottom elevation or depth
at a reference level; HUD clearance uses the active water level.

The baseline mission uses a fixed level. A later timed variant may change level
slowly during the run. Do not animate water level by 1.5 m over a few gameplay
minutes; that would compress a daily operational cycle into an implausible
physical event.

Add a limited, explicitly estimated shallow-water model:

- increase surge resistance as depth-to-draft ratio falls below 1.6;
- reduce rudder effectiveness as under-keel clearance approaches zero;
- add bounded squat based on speed and depth-to-draft ratio;
- expose effective draft and squat separately in telemetry.

Keep bank suction/cushion out of the first implementation unless it can be
tested independently. Lateral current and asymmetric bathymetry already create
the required handling problem.

## Leading Marks

Create `LeadingMarkPair` with front and rear marks. Each pair stores a target
route segment and alignment tolerance.

The marks must work visually from bridge and navigator cameras:

- front board lower and nearer;
- rear board higher and farther;
- contrasting day boards;
- emissive or lit night variant reserved for a later pass;
- sufficient scale to remain readable at the intended acquisition distance.

`LeadingLineEvaluator` calculates signed cross-track error and angular error.
It feeds scoring and optional debrief data, but normal play should not display a
large artificial alignment bar. The primary cues remain visual alignment,
buoys, depth, drift, and current.

HUD warnings may appear only after meaningful deviation:

- `LEADING LINE DEVIATION` after sustained angular or cross-track error;
- `DEPTH FALLING` when forward samples predict decreasing clearance;
- no immediate failure solely for imperfect alignment.

## Grounding and Rocky Contact

Implement grounding from sampled keel clearance, not only collider contact.
Use at least bow, center, and stern keel sample points.

`GroundingController` states:

- `Clear`: all samples above caution clearance;
- `Shallow`: minimum clearance below warning threshold;
- `Touching`: one or more samples at or below zero;
- `HardGrounding`: contact above a speed/energy threshold or sustained contact;
- `Recovered`: vessel has cleared after a soft touch.

Soft mud or sand contact applies progressive drag and may allow recovery.
Rock contact applies a stronger impulse/drag penalty and records hull or
propulsion damage. The first version can use abstract damage points without a
full flooding model.

Mission failure conditions:

- hard grounding on rock;
- propulsion damage above the configured threshold;
- vessel stranded for a configured duration;
- collision with a bank, navigation structure, or another vessel above the
  impact threshold.

A brief soft touch should reduce score but should not always end the mission.

## Mission Controller and Scoring

Create `GorodetsScenarioController` as a state machine:

```text
Briefing
WaitingForClearance
DepartApproach
AcquireGorodetsLeadingLine
PassGorodetsShoal
PassUpperKochergino
PassLowerKochergino
ReachFinish
Completed / Failed
```

Section gates must be crossed in order and in the correct direction. Progress
must use route distance, not raw world `z`.

Score out of 100:

| Category | Points | Measurement |
|---|---:|---|
| Safe depth and no contact | 35 | Minimum clearance, touch count, contact severity |
| Fairway discipline | 20 | Time and distance outside marked edges |
| Leading-line accuracy | 15 | Integrated cross-track and angular error |
| Speed compliance | 10 | Duration and magnitude over local limit |
| Control smoothness | 10 | Excessive rudder reversals and yaw rate |
| Traffic/rule compliance | 10 | Clearance, hold point, prohibited passing |

Record a compact debrief timeline with route distance, speed, clearance,
cross-track error, current, rudder, and event markers.

## Traffic and Visibility Variants

Traffic is phase two, after the solo route is stable.

Add a scripted meeting vessel or tow that occupies the Gorodets section.
The player receives an instruction to hold before the entry gate. The traffic
vessel may follow a kinematic route only if collision behavior is clearly
bounded and it does not claim to be a second full ship simulation.

Traffic outcomes:

- correct: hold outside the restricted section and proceed when clear;
- warning: enter before clearance but maintain separation;
- failure: dangerous meeting, collision, or passing inside the prohibited gate.

Fog is also phase two. Visibility should affect rendered range and mark
acquisition. A sub-1 km condition should produce a hold/cancel instruction in
the scenario logic rather than asking the player to navigate blind.

## HUD Changes

Move mission-specific text and limits out of `ShipTelemetryUI`.

Add:

- current mission phase and next instruction;
- local speed limit from route data;
- forward minimum clearance from bathymetry samples;
- effective draft including estimated squat;
- grounding/contact warning;
- hold/clearance state;
- end-of-run debrief panel.

Keep the existing radar heading-up. Replace its static `FairwayModel` queries
with the active fairway and bathymetry providers. Leading-line geometry may be
shown on the radar only as a route segment, not as an exact real-world chart.

## Data and Configuration

Scenario tuning must live outside MonoBehaviour source constants.

Recommended assets:

```text
Assets/ShipSimulator/Data/Scenarios/Gorodets/
  GorodetsScenario.asset
  GorodetsRoute.asset
  GorodetsBathymetry.asset
  NominalDischarge.asset
  PeakDischarge.asset
  GorodetsScoring.asset
```

Every estimated field should be identified in the Inspector tooltip or data
schema. Add source notes and validation status to the scenario asset.

## Implementation Phases

### Phase 1: Route and Static Solo Mission

- create the separate scene and route provider;
- build banks, approach channel, leading marks, and paired buoys;
- implement sampled asymmetric bathymetry;
- connect HUD/radar to provider interfaces;
- add ordered mission gates and basic scoring;
- use fixed current regions and fixed water level.

Exit criterion: the existing vessel can complete the route, deviations reduce
clearance predictably, and the scene is repeatable.

### Phase 2: Current Transition and Grounding

- implement additive/blended current field;
- add bow/center/stern current sampling and bounded yaw effect;
- add keel clearance samples, soft contact, rock contact, and damage points;
- add shallow-water resistance, rudder reduction, and estimated squat;
- tune nominal discharge for controllability.

Exit criterion: entering the stream bow-first creates a measurable but
recoverable yaw response, and leaving the marked route produces physical rather
than purely textual consequences.

### Phase 3: Operational Scenario

- add discharge presets and briefing selection;
- add hold point, meeting traffic, and clearance logic;
- add fog cancellation/hold variant;
- add debrief timeline and final score;
- add night-ready leading marks only after daylight validation.

Exit criterion: mission rules, hazards, and feedback form a complete
start-to-debrief exercise.

### Phase 4: Validation and Tuning

- compare geometry with permitted reference material;
- replace estimated depth/current values where authoritative data is available;
- run manoeuvring trials at several load and discharge presets;
- obtain navigation-specialist review;
- retain explicit prototype disclaimers until validated.

## Tests

EditMode:

- route interpolation returns continuous position, tangent, width, and depth;
- bathymetry is deepest in the maintained corridor and includes authored
  asymmetric hazards;
- current regions blend and compose according to mode;
- water-level offset changes clearance without changing bottom geometry;
- scenario gates reject out-of-order and reverse crossings;
- scoring applies configured thresholds;
- builder adds the new scene after `RiverTrainingScene` in Build Settings;
- all scenario assets identify estimated and validated fields.

PlayMode:

- vessel receives different bow and stern currents during stream entry;
- leaving a current region restores the base field without stale registration;
- soft bottom contact adds drag and can be recovered from;
- rock contact records greater severity than soft-bottom contact;
- forward clearance warning precedes center-keel contact;
- crossing section gates advances mission state in order;
- entering the restricted section before clearance records a violation;
- reset restores vessel, mission, damage, current schedule, and score.

Regression:

- existing `RiverCurrentZoneTests` continue to pass;
- `RiverTrainingScene` retains its current fairway, HUD, and controls;
- no scenario system directly changes the vessel transform during simulation.

## Main Technical Risks

- The current hydrodynamic coefficients are estimated, so tuning the discharge
  against them may produce scenario-specific behavior rather than validated
  vessel behavior.
- A 138.3 m vessel needs substantially more route length than the current
  900 m scene to demonstrate anticipation and delayed response.
- Three-point current sampling can double-count forces if added directly to the
  existing aggregate resistance. Implement it as a controlled yaw correction
  or refactor resistance into sectional samples, not both.
- Grounding based only on center depth will miss bow and stern contact in bends.
- Strong warning overlays can turn navigation into instrument-following.
  Consequences and visual marks should remain the primary feedback.
- Rebuilding shared assets from the prototype builder can overwrite manual
  scenario work. The Gorodets builder must own only its scene-specific assets.

## Definition of Done

The technical implementation is complete when:

1. The Project 507B vessel can run the full mission without special-case
   transform movement or modified vessel dimensions.
2. Leading marks, lateral marks, depth, and current all agree with one shared
   route coordinate system.
3. Current changes across the vessel length produce the intended stream-entry
   handling challenge.
4. Fairway departure leads to falling clearance and possible bottom contact.
5. Rocky contact is distinguishable from soft grounding.
6. Speed, route discipline, leading-line accuracy, grounding, and traffic rules
   are scored and included in a debrief.
7. EditMode and PlayMode regression suites pass.
8. All unvalidated values remain explicitly labeled as estimates.
