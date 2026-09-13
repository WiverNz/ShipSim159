# ShipSim159 Documentation

ShipSim159 is a Unity 6 URP prototype for a Project 507B Volgo-Don river cargo
vessel. It is not a validated maritime training product.

## Documents

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
- [VolgoDon507B_Sources.md](VolgoDon507B_Sources.md): published vessel
  particulars, sources, confidence, and estimated parameters.
- [NextSteps.md](NextSteps.md): prioritized engineering and simulation work.
- [Project context](../../../.codex/PROJECT_CONTEXT.md): implementation history,
  verification status, and continuation notes for automated contributors.

## Current Prototype Status

The main scene contains the detailed Volgo-Don model, procedural river and
terrain, current trigger zones, compact bridge HUD, eight camera views,
simulator controls, fairway minimap, warnings, and shader-drawn ship waves.

Latest verified tests: EditMode `5/5`, PlayMode `2/2`.

