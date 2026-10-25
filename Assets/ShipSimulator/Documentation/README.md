# ShipSim159 Documentation

ShipSim159 is a Unity 6 URP river navigation prototype with five selectable vessels.
It is not a validated maritime training product.

## Documents

- [UsabilityImprovementPlan.md](UsabilityImprovementPlan.md): source audit and nine
  executable parts for interface, languages, camera, map, collisions, recovery and cargo.

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

Three passage scenes support five vessels subject to passage restrictions. Runtime
systems include nine camera views, HUD/radar, save/load, weather and sound. The
Volgo-Don also has loading, hold visuals, animated machinery, anchors and mooring.

Latest recorded baseline, 2026-10-25: EditMode `269/269`, PlayMode `161/161`.
These precede the planned usability changes; see ProjectStatus and project context
for evidence and limitations.

