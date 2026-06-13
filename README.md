# ShipSim159

ShipSim159 is a Unity 6 URP prototype of a river navigation simulator centered
on a Project 507B Volgo-Don cargo vessel.

The project explores large-vessel handling in a constrained river fairway,
including delayed engine and rudder response, current-relative motion,
estimated depth, navigation marks, day/night conditions, and bridge-style
instrumentation.

> [!IMPORTANT]
> ShipSim159 is an engineering and gameplay prototype. Hydrodynamic values,
> depth data, navigation-light characteristics, and vessel parameters are not
> validated for professional maritime training or real-world navigation.

## Features

- Detailed 138.3 m Volgo-Don vessel model integrated into Unity URP.
- Rigidbody-based vessel simulation with point buoyancy.
- Linear and quadratic water resistance.
- Engine telegraph with gradual propulsion response.
- Rudder lift based on water-relative velocity.
- Ambient and trigger-based river currents.
- Curved river fairway with estimated bathymetry and under-keel clearance.
- Compound bow, midship, and stern collision hull.
- Heading-up river radar with:
  - depth zones and minimum depth ahead;
  - curved fairway route and marked channel edges;
  - ship heading, recent track, and predicted path;
  - waypoint and paired navigation buoys.
- Nine camera views, including docking and wheelhouse navigator views.
- Day/night switching with illuminated navigation environment.
- Flashing red and green buoy lights.
- Mounted vessel navigation lights.
- Procedural river water, riverbanks, vegetation, fog, wake, bow waves, and
  propeller wash.
- Runtime vessel-data validation and Unity EditMode/PlayMode tests.

## Requirements

- Unity `6000.4.0f1`
- Universal Render Pipeline `17.4.0`
- Unity Input System `1.19.0`

The project currently targets desktop development and uses keyboard and mouse
input.

## Getting Started

1. Clone or download the repository.
2. Open the project folder in Unity Hub.
3. Use Unity Editor `6000.4.0f1`.
4. Open:
   `Assets/ShipSimulator/Scenes/RiverTrainingScene.unity`
5. Enter Play Mode.

The training scene is already configured as the first enabled scene in Build
Settings.

## Controls

| Input | Action |
|---|---|
| `W` / `Up Arrow` | Increase telegraph command |
| `S` / `Down Arrow` | Decrease telegraph command |
| `Space` | Set telegraph to Stop |
| `A` / `Left Arrow` | Command port rudder |
| `D` / `Right Arrow` | Command starboard rudder |
| `C` / `Enter` | Rudder midships |
| `H` | Sound horn |
| `R` | Reset vessel |
| `V` | Cycle camera |
| `1`-`9` | Select a camera directly |
| Right mouse button | Orbit supported cameras |
| Mouse wheel | Adjust camera distance |
| `M` | Toggle river radar |
| `N` | Toggle day/night mode |
| `F1` | Toggle the full control reference |

### Camera Views

| Key | View |
|---|---|
| `1` | Chase |
| `2` | Bridge |
| `3` | Top |
| `4` | Port |
| `5` | Starboard |
| `6` | Bow |
| `7` | Stern |
| `8` | Docking |
| `9` | Navigator / wheelhouse |

## Navigation Display

The radar is vessel-centered and heading-up. The vessel remains fixed while
the surrounding fairway and contacts move relative to it.

| Display | Meaning |
|---|---|
| Cyan dashed line | Current ship heading |
| Yellow route | Curved fairway centerline |
| Gray line | Recent vessel track |
| White dashed curve | Predicted path from speed, drift, and rudder |
| Red bathymetry | Shallow water or bank |
| Amber bathymetry | Caution depth |
| Blue/green bathymetry | Safer water |

Depth and predicted-path information are simulation estimates, not surveyed or
certified navigation data.

## Editor Tools

The `Ship Simulator` Unity menu contains project automation commands:

| Command | Purpose |
|---|---|
| `Build Prototype` | Regenerate the prototype scene and generated assets |
| `Integrate Detailed Vessel Model` | Rebuild vessel materials and prefab integration |
| `Apply Visual Upgrade` | Regenerate procedural environment visuals |
| `Arrange Navigation Buoys` | Rebuild the curved paired-buoy layout |
| `Render Visual Preview` | Render project preview images |
| `Play Training Scene` | Open and run the main scene |
| `Stop Play Mode` | Stop the running scene |

Generated scene content may be replaced by these tools. Keep custom changes in
the appropriate builder or integration script when they must survive a rebuild.

## Testing

Run both suites from:

`Window > General > Test Runner`

- EditMode tests validate vessel data, scene configuration, fairway depth,
  collision setup, navigation lights, buoy flashing, and HUD formatting.
- PlayMode tests validate river-current trigger behavior and overlapping zones.

Unity tests can also be executed in batch mode:

```powershell
Unity.exe -batchmode -projectPath . -runTests `
  -testPlatform EditMode -testResults TestResults.xml -quit
```

Replace `EditMode` with `PlayMode` for the runtime suite.

## Project Structure

```text
Assets/ShipSimulator/
|-- Data/Vessels/          Vessel JSON specifications
|-- Documentation/         Operator, physics, source, and roadmap documents
|-- Models/VolgoDon507/    Imported vessel model and materials
|-- Prefabs/               Vessel, navigation, and environment prefabs
|-- Scenes/                Main river training scene
|-- Scripts/
|   |-- Camera/            Camera views and tracking
|   |-- Editor/            Project builders and model integration
|   |-- Physics/           Vessel, buoyancy, current, and validation logic
|   |-- UI/                HUD and river radar
|   `-- Visuals/           Lighting, navigation beacons, and wake
|-- Shaders/               Procedural river shader
`-- Tests/                 EditMode and PlayMode test assemblies
```

## Simulation Model

The vessel uses a Unity `Rigidbody` and is moved through forces and torques
rather than direct transform changes. The current implementation includes:

- 15-point buoyancy;
- aggregate twin-engine/twin-propeller propulsion;
- rudder force from local water velocity;
- longitudinal and lateral resistance;
- wind and river-current forces;
- configurable response times and calibration multipliers from vessel JSON.

See
[ShipSimulator_Physics.md](Assets/ShipSimulator/Documentation/ShipSimulator_Physics.md)
for implementation details.

## Known Limitations

- Hydrodynamic coefficients are estimated and not trial-calibrated.
- Bathymetry is procedural and not based on surveyed river data.
- Twin propulsion is currently represented by one aggregate centerline force.
- The simulation does not yet model squat, cavitation, grounding damage,
  anchors, mooring lines, fuel, damage, or autopilot.
- Wake and propeller wash are visual effects only.
- Keyboard input is polled directly and is not yet rebindable.
- The HUD currently uses legacy `UnityEngine.UI.Text`.

## Documentation

- [Documentation index](Assets/ShipSimulator/Documentation/README.md)
- [Operator guide](Assets/ShipSimulator/Documentation/OperatorGuide.md)
- [Physics model](Assets/ShipSimulator/Documentation/ShipSimulator_Physics.md)
- [Vessel sources and parameter confidence](Assets/ShipSimulator/Documentation/VolgoDon507B_Sources.md)
- [Engineering roadmap](Assets/ShipSimulator/Documentation/NextSteps.md)
- [Contributor guidelines](AGENTS.md)

## Contributing

Read [AGENTS.md](AGENTS.md) and `.codex/PROJECT_CONTEXT.md` before changing the
project. Keep runtime, editor, and test code inside their existing assembly
boundaries. Every bug fix should include a focused regression test.

Do not commit Unity-generated directories such as `Library`, `Temp`, `Logs`,
or `UserSettings`.
