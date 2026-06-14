# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Orientation

ShipSim159 is a Unity 6 URP prototype of a river navigation simulator built around a Project 507B Volgo-Don cargo vessel. It is an engineering/gameplay prototype, **not** validated for real maritime training — never present prototype hydrodynamic, depth, or navigation values as training-validated data.

Before changing the project, read these (in order of usefulness):
- `.codex/PROJECT_CONTEXT.md` — the living log of completed work, verification status, known limitations, and next priorities. Keep it current when you finish meaningful work.
- `AGENTS.md` — contributor conventions (style, testing, PR expectations).
- `Assets/ShipSimulator/Documentation/` — operator guide, physics model, vessel sources/parameter confidence, roadmap.

## Build / Run / Test

Unity Editor version: `6000.4.0f1` (URP 17.4.0, Input System 1.19.0). Desktop, keyboard + mouse.

- **Run:** open `Assets/ShipSimulator/Scenes/RiverTrainingScene.unity` and enter Play Mode (already first in Build Settings). Second scenario scene: `GorodetsTrainingScene.unity`.
- **Tests (UI):** `Window > General > Test Runner`, run EditMode and PlayMode suites.
- **Tests (batch):**
  ```powershell
  Unity.exe -batchmode -projectPath . -runTests `
    -testPlatform EditMode -testResults TestResults.xml -quit
  ```
  Swap `EditMode` for `PlayMode`. Note: the `mcp-unity` Test Runner frequently times out *after* completing a run — confirm actual results in `C:\Users\User\AppData\LocalLow\DefaultCompany\ShipSim159\TestResults.xml` rather than trusting the timeout.
- **Editor automation** (`Ship Simulator` menu): `Build Prototype`, `Integrate Detailed Vessel Model`, `Apply Visual Upgrade`, `Arrange Navigation Buoys`, `Build Gorodets Scenario`, `Render Visual Preview`, `Play Training Scene`, `Stop Play Mode`.

These editor commands **regenerate scene-owned content** — manual scene edits they overwrite must instead be made in the corresponding builder/integrator script (under `Scripts/Editor/`) to survive a rebuild.

## Assembly boundaries

Four assembly definitions; keep code in its correct one or it will not compile/reference correctly:
- `ShipSimulator.Runtime` — `Assets/ShipSimulator/Scripts/{Physics,Camera,UI,Visuals}`
- `ShipSimulator.Editor` — `Assets/ShipSimulator/Scripts/Editor` (builders, model integrator; editor-only)
- `ShipSimulator.EditModeTests` / `ShipSimulator.PlayModeTests` — `Assets/ShipSimulator/Tests/{EditMode,PlayMode}`

## Architecture (the big picture)

`ShipPhysicsController` (`Scripts/Physics/`) is the hub. On `Awake` it loads JSON via `VesselDataLoader`, then coordinates sibling/child components found by `GetComponent*`:
- `PropulsionController` (engine telegraph → gradual aggregate centerline thrust), `RudderController` (lift from local water-relative velocity), `HydrodynamicResistance` (linear + quadratic), `BuoyancyPoint[]` (15-point buoyancy).
- Environment inputs: `CurrentFieldProvider` / `RiverCurrentZone` (ambient vs. trigger-zone-averaged current), `ScenarioBathymetry` + `FairwayModel`/`FairwayRoute` (depth, channel geometry, squat estimate), `GroundingController` (keel-clearance interaction).

Key invariant: **the vessel is driven entirely by forces/torques on its `Rigidbody` inside `FixedUpdate`.** Never move a simulated vessel by writing `transform` directly.

Vessel behavior is data-driven from `Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json`. `VesselDataValidator` validates every required section before physics runs; invalid data disables `ShipPhysicsController` and logs a specific error. When adding a JSON field, extend both the `VesselData` schema and `VesselDataValidator`.

UI/HUD (`Scripts/UI/`, `ShipTelemetryUI`) is built at runtime and calls **public command methods** on `ShipPhysicsController` — it does not synthesize keyboard input. The river radar shares geometry/thresholds with `FairwayModel`. Visuals (`Scripts/Visuals/`: `DayNightController`, `NavigationBeaconFlasher`, `NavigationLightRig`, `ShipWakeController`, `WeatherController`) are presentation-only — wake and wash do not feed back into physics.

Model provenance: the vessel mesh was imported from a CRYENGINE source and re-axised (CRYENGINE X-fwd/Z-up → Unity Z-fwd/Y-up) by `VolgoDonModelIntegrator`. Do not replace the dynamic collision hull (overlapping bow/midship/stern box colliders) with a non-convex MeshCollider.

## Conventions

- Four-space indent, braces on their own line. `PascalCase` types/methods/public props, `camelCase` locals/private fields, `[SerializeField] private` for Inspector config. One primary type per file matching the filename. Use the `ShipSimulator.*` namespace hierarchy.
- Name test fixtures `*Tests` and tests as behavior statements (e.g. `TriggerZone_OverridesAmbientCurrentAndRestoresItOnExit`). EditMode for data/config validation; PlayMode for Rigidbody/trigger/scene/frame-dependent behavior. Every bug fix gets a focused regression test.
- Do not commit Unity-generated dirs: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or generated solution/`.csproj` files.
