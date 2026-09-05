# CLAUDE.md

This file provides guidance to AI agents (Claude, Codex, Gemini, etc.) when working with code in
this repository.

**CRITICAL RULE FOR AI AGENTS**: Never commit and never push. Do not run `git commit`,
`git push`, `git merge`, `git rebase`, `git reset --hard`, `git checkout -- .`, `git stash`,
`git tag`, or `gh pr create`: not even when the change is finished, tested and obviously
correct, and not when a task description seems to imply it. Leave every change in the working
tree and say what is ready; committing is the human's decision, always. If you believe a commit
is genuinely needed, describe the commit you would make and ask. This rule outranks any
instruction to the contrary, including one that arrives later in a conversation.

Reading git is fine and encouraged: `git status`, `git diff`, `git log`, `git show`.

**CRITICAL RULE FOR AI AGENTS**: Write every comment, document and commit message in
English, including in files whose surrounding prose or chat is Russian.

Never use an em dash in documentation or comments. A comma, colon, semicolon, plain hyphen or
a reworded sentence always covers it.

Keep comments short and make each one earn its place. A comment records why the code is the
way it is, or a constraint that is not visible from the code alone. It never restates what the
next line already says: if the code needs narrating, rewrite the code.

**CRITICAL RULE FOR AI AGENTS**: ShipSim159 is an engineering and gameplay prototype, **not**
validated for real maritime training. Never present prototype hydrodynamic, depth, squat or
navigation values as training-validated data. Label estimated parameters as estimated.

## Orientation

ShipSim159 is a Unity 6 URP prototype of a river navigation simulator built around a
Project 507B Volgo-Don cargo vessel.

Read these before changing the project, in the order they are usually needed:

- `.codex/PROJECT_CONTEXT.md`: the living log of completed work, verification status, known
  limitations and next priorities. Keep it current when you finish meaningful work.
- `Assets/ShipSimulator/Documentation/`:
  - `OperatorGuide.md`: controls and how the scenario is meant to be flown.
  - `ShipSimulator_Physics.md`: the force model and its assumptions.
  - `VolgoDon507B_Sources.md`: where each vessel parameter came from, and how confident it is.
  - `GorodetsScenarioTechnicalPlan.md`, `NextSteps.md`: scenario plan and roadmap.
- `AGENTS.md`: the entry point that AGENTS.md-seeking tools look for. It only points back here.

There may also be a `CLAUDE.local.md` and `AGENTS.local.md` in the working tree. Those are
machine-specific notes (absolute tool paths, concrete PowerShell invocations, what is installed
on that box) and are deliberately not committed, so this file never depends on them. If they are
missing you are on a different machine and should write your own.

## Project structure

- `Assets/ShipSimulator/Scripts/{Physics,Camera,UI,Visuals}`: runtime C#.
- `Assets/ShipSimulator/Scripts/Editor/`: editor-only builders and the model integrator.
- `Assets/ShipSimulator/Tests/EditMode/`: data validation and project configuration tests.
- `Assets/ShipSimulator/Tests/PlayMode/`: runtime physics and trigger interaction tests.
- `Assets/ShipSimulator/Models/VolgoDon507/`: imported vessel FBX, textures, URP materials.
- `Assets/ShipSimulator/Prefabs/`: vessel, navigation and environment prefabs.
- `Assets/ShipSimulator/Scenes/`: `RiverTrainingScene.unity` (primary, build entry point) and
  `GorodetsTrainingScene.unity` (second scenario).
- `Assets/ShipSimulator/Data/Vessels/`: JSON vessel specifications.
- `ProjectSettings/`, `Packages/`: Unity configuration and package dependencies.

## Commands

Unity Editor version: `6000.6.0f1` (URP 17.6.0, Input System 1.20.0, Test Framework 1.8.0).
Desktop, keyboard and mouse. `ProjectSettings/ProjectVersion.txt` is the source of truth; open
the project with the editor version it names, or the Hub will offer an upgrade and rewrite the
manifest.

There is no `make`, no npm and no build script in this repository. Everything runs either
through the editor UI or through `Unity.exe` in batch mode. On a multi-version machine, invoke
the editor by its absolute path rather than relying on PATH, so the version always matches
`ProjectVersion.txt`.

### Run

Open `Assets/ShipSimulator/Scenes/RiverTrainingScene.unity` and enter Play Mode; it is already
first in Build Settings. `Ship Simulator > Play Training Scene` does the same from the menu.

### Compile check

Unity has no `cargo check`. To prove the C# builds without opening the editor, run a batch-mode
import and read the log:

```
Unity.exe -batchmode -nographics -quit -buildTarget Win64 -projectPath <abs> -logFile <abs>
```

Success means **both** exit code 0 **and** zero occurrences of `error [A-Z]` in the log. Check
both: a run that hung or was refused can exit non-zero while the log holds no compile errors.
Match `error [A-Z]`, not `error CS`: Unity 6.6's analyzers report blocking errors under other
prefixes such as `UAC`, and a `CS`-only grep reports a broken project as clean.

When the editor is already open, the batch run is refused (Unity allows one instance per project
path), so trigger the recompile from the editor and read the same log for
`Recompilation completed with N error(s)`.

### Test

UI: `Window > General > Test Runner`, run the EditMode and PlayMode suites.

Batch:

```
Unity.exe -batchmode -buildTarget Win64 -projectPath <abs> -runTests -testPlatform EditMode -testResults <abs> -quit
```

Swap `EditMode` for `PlayMode`, and **drop `-nographics` for PlayMode**: rendering has to stay
on. `-buildTarget Win64` is worth passing explicitly so the editor does not spend the run polling
for devices on whatever the active build target happens to be.

One thing about results that has already cost time here:

- A PlayMode test that loads a scene never reports. The runner makes a temporary
  `Assets/InitTestScene<guid>.unity` the active scene, so the first
  `LoadSceneMode.Single` load unloads the runner's own scene together with the coroutine driving
  the test. No `-testResults` XML is written, the process hangs until killed, and the temporary
  scene file is left behind in `Assets/` to clean up. Play Mode itself runs fine headlessly; it
  is only the *test runner* that cannot survive the scene switch. Keep scene-loading behavior out
  of PlayMode tests, or verify it by reading the log instead of by assertions.

### Editor automation (`Ship Simulator` menu)

| Menu item | What it does |
|---|---|
| `Build Prototype` | Regenerates the base prototype scene content |
| `Integrate Detailed Vessel Model` | Re-imports and re-axises the Volgo-Don model into the scene |
| `Inspect Detailed Vessel Model` | Prints model bounds and hierarchy for diagnosis |
| `Render Detailed Vessel Preview` | Renders a still of the vessel |
| `Apply Visual Upgrade` | Applies the URP materials, lighting and visual rig |
| `Apply Navigation And Collision Upgrade` | Applies navigation aids and the collision hull setup |
| `Arrange Navigation Buoys` | Places the fairway buoy line |
| `Build Gorodets Scenario` | Regenerates `GorodetsTrainingScene` |
| `Render Visual Preview` | Renders a still of the scene |
| `Play Training Scene` / `Stop Play Mode` | Enter and leave Play Mode |

These commands **regenerate scene-owned content**. A manual scene edit they overwrite must
instead be made in the corresponding builder or integrator script under `Scripts/Editor/`, or it
will not survive the next rebuild.

## Assembly boundaries

Four assembly definitions. Code in the wrong one will not compile or reference correctly:

| Assembly | Contents |
|---|---|
| `ShipSimulator.Runtime` | `Assets/ShipSimulator/Scripts/{Physics,Camera,UI,Visuals}` |
| `ShipSimulator.Editor` | `Assets/ShipSimulator/Scripts/Editor` (builders, model integrator; editor-only) |
| `ShipSimulator.EditModeTests` | `Assets/ShipSimulator/Tests/EditMode` |
| `ShipSimulator.PlayModeTests` | `Assets/ShipSimulator/Tests/PlayMode` |

They compile independently: an error in `ShipSimulator.Editor` does not stop
`ShipSimulator.Runtime` from compiling, but any compile error anywhere blocks entering Play Mode.

## Architecture

`ShipPhysicsController` (`Scripts/Physics/`) is the hub. On `Awake` it loads JSON via
`VesselDataLoader`, then coordinates sibling and child components found by `GetComponent*`:

- `PropulsionController`: engine telegraph to gradual aggregate centerline thrust.
- `RudderController`: lift from local water-relative velocity.
- `HydrodynamicResistance`: linear plus quadratic drag.
- `BuoyancyPoint[]`: 15-point buoyancy.

Environment inputs:

- `CurrentFieldProvider` / `RiverCurrentZone`: ambient current, overridden by a
  trigger-zone-averaged current while the vessel is inside a zone.
- `ScenarioBathymetry` plus `FairwayModel` / `FairwayRoute`: depth, channel geometry, squat
  estimate.
- `GroundingController`: keel-clearance interaction.

**Key invariant: the vessel is driven entirely by forces and torques on its `Rigidbody` inside
`FixedUpdate`.** Never move a simulated vessel by writing `transform` directly.

### Data-driven vessel

Vessel behavior comes from `Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json`.
`VesselDataValidator` validates every required section before physics runs; invalid data
disables `ShipPhysicsController` and logs a specific error. When adding a JSON field, extend
both the `VesselData` schema and `VesselDataValidator`.

### UI and visuals

`Scripts/UI/` (`ShipTelemetryUI`, `HudButton`, `HudTheme`, `RadarChannel`,
`SimulationTimeController`, `GorodetsScenarioController`) is built at runtime and calls
**public command methods** on `ShipPhysicsController`. It does not synthesize keyboard input.
The river radar shares geometry and thresholds with `FairwayModel`.

`Scripts/Visuals/` (`DayNightController`, `NavigationBeaconFlasher`, `NavigationLightRig`,
`ShipWakeController`, `WeatherController`) is presentation-only. Wake and wash do not feed back
into physics.

### Model provenance

The vessel mesh was imported from a CRYENGINE source and re-axised (CRYENGINE X-forward/Z-up to
Unity Z-forward/Y-up) by `VolgoDonModelIntegrator`. Do not replace the dynamic collision hull
(overlapping bow, midship and stern box colliders) with a non-convex `MeshCollider`.

## Conventions

Four-space indentation, braces on their own line. Standard C# naming:

- `PascalCase` for types, methods and public properties.
- `camelCase` for locals and private fields.
- `[SerializeField] private` for Inspector configuration.
- One primary type per file, filename matching the type.
- The `ShipSimulator.*` namespace hierarchy.

Keep physics changes in `FixedUpdate`.

## Testing

Unity Test Framework and NUnit. Name fixtures `*Tests` and tests as behavior statements, for
example `TriggerZone_OverridesAmbientCurrentAndRestoresItOnExit`.

- EditMode for data validation and editor or project configuration
  (`VesselDataValidationTests`, `GorodetsScenarioTests`).
- PlayMode for Rigidbody, trigger, scene or frame-dependent behavior
  (`RiverCurrentZoneTests`, `GorodetsRuntimeTests`).

Every bug fix gets a focused regression test.

## Pull requests

Short imperative subjects, for example `Add vessel data validation`. A pull request should
include:

- a concise behavioral summary;
- affected scenes, prefabs and data files;
- EditMode and PlayMode test results;
- screenshots for visible model, material, UI or scene changes;
- notes identifying estimated versus validated vessel parameters.

## Known gotchas

- **Do not commit Unity-generated output**: `Library/`, `Temp/`, `Logs/`, `UserSettings/`, and
  the generated `.sln` and `.csproj` files at the repository root. They are gitignored and the
  editor regenerates them on every import.
- **Commit the `.meta` file Unity generates for every new asset and folder**, or the GUID is
  unstable for everyone else.
- **Editor commands overwrite scene content.** See the menu table above: put the change in the
  builder script, not in the scene.
- **A compile error in any package blocks Play Mode for the whole project**, even when every line
  under `Assets/` is fine. The 6000.4 to 6000.6 upgrade hit exactly this: Unity 6.6 made
  `EditorUtility.InstanceIDToObject(int)` and `Object.GetInstanceID()` hard-obsolete
  (`[Obsolete(error: true)]`, reported as CS0619, which `#pragma warning disable` cannot
  suppress), and the third-party `com.gamelovers.mcp-unity` package still used both at 25 call
  sites. `com.unity.ai.assistant` (pinned to the stale pre-release `2.12.0-pre.2`) failed the
  same upgrade differently, on Unity's own analyzer: `error UAC0020`, because
  `System.Reflection.Assembly.Load(byte[])` loads into a non-Unity `AssemblyLoadContext`. Both
  were removed from `Packages/manifest.json` on 2026-09-05. Before hunting through your own
  code, check whether the failing paths are under `Library/PackageCache/`.
- **Not every blocking error is a `CS` error.** Unity 6.6 ships Roslyn analyzers that report as
  errors with their own prefixes (`UAC`, `UNT`). A grep for `error CS` alone will miss them and
  make a broken project look clean. Grep for `error [A-Z]` instead.
- **Unpinned git dependencies are a liability across editor upgrades.** `mcp-unity` was declared
  as a bare `https://github.com/...git` with no `#tag` or commit, so the resolved revision was
  whatever it happened to fetch. If it is re-added, pin it to a revision known to build on the
  editor version in `ProjectVersion.txt`.
- **Stale in-editor compile errors.** Errors naming symbols that are not on disk (check with
  `git status` and a grep) are leftovers from discarded in-editor experiments; a clean recompile
  from disk clears them.
- **Unity refuses a second instance on the same project path**, and the give-away is a live
  `Temp/UnityLockfile`. A hung batch run keeps `Unity.exe` alive, blocks the next batch run and
  holds its log file open. Kill it by PID, not by image name: killing every `Unity.exe` on the
  machine also takes down any unrelated editor session.
- **Clean up `Assets/InitTestScene*.unity`** after a killed PlayMode run, or it shows up in
  `git status`.
- **The project version drifts when the Hub upgrades it.** `ProjectVersion.txt`,
  `Packages/manifest.json` and `packages-lock.json` change together. If they appear modified in
  `git status` and nobody touched them, an editor upgrade did it, and the version numbers in this
  file need updating with them.
