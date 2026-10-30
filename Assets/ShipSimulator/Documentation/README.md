# ShipSim159 Documentation

ShipSim159 is a Unity 6 URP river navigation prototype with six playable vessels
and a seventh catalogue command for the Serpukhov + MN-52 push unit.
It is not a validated maritime training product.

## Documents

- [Usability Part 6 review](UsabilityPart6Review.md): grounded bow, existing source repair, editor/player captures and fresh Windows build.

- [Usability Part 5 review](UsabilityPart5Review.md): physical buoys, collision audit, saved mark motion and verification.

- [Usability Part 4 review](UsabilityPart4Review.md): interactive map, shared projection, scenario geometry and verification.

- [UsabilityPart1Review.md](UsabilityPart1Review.md): scalable interface, settings and verification.
- [UsabilityImprovementPlan.md](UsabilityImprovementPlan.md): source audit and nine
  executable parts for interface, languages, camera, map, collisions, recovery and cargo.
- [Localization](Localization.md): four-language catalogues, live labels and display culture.
- [Usability Part 3 review](UsabilityPart3Review.md): proportional zoom, smoothed orbit, input ownership and save migration.
- [Usability Part 2 review](UsabilityPart2Review.md): language selection and layout verification.

- [Release diagnostics review](ReleaseDiagnosticsReview.md): v0.1.3 CI errors, pinned MCP metadata, collision pre-baking and strict build acceptance.
- [ProjectStatus.md](ProjectStatus.md): what is implemented, what is verified,
  screenshots, known limits and prioritised next work. Start here.
- [GraphicsPhaseTwoPlan.md](GraphicsPhaseTwoPlan.md): step-by-step plan for the
  next graphics work: water optics, height fog and a current flow map.
- [OperatorGuide.md](OperatorGuide.md): startup, controls, cameras, HUD, minimap,
  and test execution.
- [ShipSimulator_Physics.md](ShipSimulator_Physics.md): MMG manoeuvring model,
  propulsion, rudders, wind, restricted water, hydrostatics, grounding, virtual
  sea trials, and simulation limitations.
- [ShipDynamicsRealismApproach.md](ShipDynamicsRealismApproach.md): the research
  and roadmap behind the physics model, with implementation status.
- [VolgoDon507B_Sources.md](VolgoDon507B_Sources.md), [VolgoBalt295AR_Sources.md](VolgoBalt295AR_Sources.md),
  [Volgoneft1577_Sources.md](Volgoneft1577_Sources.md), [Meteor342U_Sources.md](Meteor342U_Sources.md),
  [Luch14352_Sources.md](Luch14352_Sources.md): published particulars,
  sources, confidence, and estimated parameters for each vessel.
- [NextSteps.md](NextSteps.md): prioritized engineering and simulation work.
- [Project context](../../../.codex/PROJECT_CONTEXT.md): implementation history,
  verification status, and continuation notes for automated contributors.

## Current Prototype Status

Three passage scenes support six playable vessels and a seventh push-unit selection,
subject to vessel and formation restrictions. Runtime
systems include nine camera views, HUD/radar, save/load, weather and sound. The
Volgo-Don also has loading, hold visuals, animated machinery, anchors and mooring.

Usability Part 1 verification, 2026-10-27: full EditMode `282/282`, PlayMode `175/175`,
final UI regression `7/7`; see its review and ProjectStatus for evidence and limitations.


Usability Part 2 verification, 2026-10-27: EditMode `289/289`, PlayMode `176/176`,
seven-command shakedown and menu/save-load smoke PASS. The four-language UI matrix
has 960 captures at five resolutions and three scales. See
[UsabilityPart2Review.md](UsabilityPart2Review.md) for exact logs and scope.

Usability Part 3 verification, 2026-10-27: EditMode `304/304`, PlayMode `187/187`,
seven-command/nine-view shakedown and menu/save-load smoke PASS. Development
Windows build and before/after native input traces completed. See its review for
all 63 view captures and the remaining physical-device handling assessment.
