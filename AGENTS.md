# Repository Guidelines

Codex and automated contributors should read
`.codex/PROJECT_CONTEXT.md` before changing the project. It records completed
work, verification status, known limitations, and continuation priorities.

## Project Structure & Module Organization

This is a Unity 6 URP ship-simulator prototype.

- `Assets/ShipSimulator/Scripts/`: runtime C# code, split into `Physics`, `Camera`, `UI`, and `Visuals`.
- `Assets/ShipSimulator/Scripts/Editor/`: editor-only builders and model integration tools.
- `Assets/ShipSimulator/Tests/EditMode/`: data validation and project configuration tests.
- `Assets/ShipSimulator/Tests/PlayMode/`: runtime physics and trigger interaction tests.
- `Assets/ShipSimulator/Models/VolgoDon507/`: imported vessel FBX, textures, and URP materials.
- `Assets/ShipSimulator/Prefabs/`: vessel, navigation, and environment prefabs.
- `Assets/ShipSimulator/Scenes/RiverTrainingScene.unity`: primary scene and build entry point.
- `Assets/ShipSimulator/Data/Vessels/`: JSON vessel specifications.
- `Assets/ShipSimulator/Documentation/`: operator, physics, source, and roadmap documentation.
- `ProjectSettings/` and `Packages/`: Unity configuration and package dependencies.

Keep runtime, editor, and test code in their existing assembly definition boundaries.

## Build, Test, and Development Commands

Open the repository with Unity `6000.4.0f1`.

- Run locally: open `RiverTrainingScene` and enter Play Mode.
- Rebuild generated assets: use `Ship Simulator > Build Prototype`.
- Reintegrate the detailed vessel: use `Ship Simulator > Integrate Detailed Vessel Model`.
- Run tests: open `Window > General > Test Runner`, then run both EditMode and PlayMode suites.

For automated runs, invoke Unity in batch mode with `-runTests`, `-testPlatform EditMode` or `PlayMode`, and `-testResults <path>`.

Do not commit `Library/`, `Temp/`, `Logs/`, generated solution files, or user-specific settings.

## Coding Style & Naming Conventions

Use four-space indentation and braces on separate lines. Follow standard C# naming:

- `PascalCase` for types, methods, and public properties.
- `camelCase` for local variables and private fields.
- `[SerializeField] private` for Inspector configuration.
- One primary type per file; match the filename to the type.

Use the `ShipSimulator` namespace hierarchy. Keep physics changes in `FixedUpdate`; do not move a simulated vessel by directly changing its transform.

## Testing Guidelines

Tests use Unity Test Framework and NUnit. Name fixtures `*Tests` and tests as behavior statements, for example `TriggerZone_OverridesAmbientCurrentAndRestoresItOnExit`.

Add EditMode tests for validation and editor configuration. Add PlayMode tests for Rigidbody, trigger, scene, or frame-dependent behavior. Every bug fix should include a focused regression test.

## Commit & Pull Request Guidelines

No Git history is available in this workspace, so existing commit conventions cannot be inferred. Use short imperative subjects, such as `Add vessel data validation`.

Pull requests should include:

- a concise behavioral summary;
- affected scenes, prefabs, and data files;
- EditMode and PlayMode test results;
- screenshots for visible model, material, UI, or scene changes;
- notes identifying estimated versus validated vessel parameters.

Never present prototype hydrodynamic values as training-validated data.
