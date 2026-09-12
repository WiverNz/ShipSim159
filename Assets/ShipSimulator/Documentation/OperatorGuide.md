# Ship Simulator Operator Guide

## Running the Prototype

Open the project with the Unity version in `ProjectSettings/ProjectVersion.txt`, load
`Assets/ShipSimulator/Scenes/RiverTrainingScene.unity`, and enter Play Mode.
The maritime start menu appears when either training scene starts, including in a
standalone player. Choose **New voyage** for River familiarisation or Gorodets passage,
or **Continue voyage** to restore the saved passage.

## Menu, Settings and Saved Voyages

Press **Escape** underway to pause the vessel, mission timing and simulation audio.
Choose **Resume voyage**, or press Escape again, to continue at the previous simulation
speed. Escape from a settings or confirmation page returns to the main menu first.
Menu buttons support mouse and keyboard navigation with arrow keys and Enter.

**Settings** provides master volume, camera orbit sensitivity, graphics quality, VSync
and fullscreen/windowed display. Settings persist between launches. Display mode is
available in standalone players; the editor controls its own Game view.

Use **Save voyage** in the pause menu to record one passage. Replacing a save, loading
over an active voyage, starting a new passage and quitting require confirmation.
There is no autosave: save before leaving if you want to keep your progress.

The save restores the scenario, vessel position and rotation, linear/angular velocity,
commanded and actual engine/rudder state, camera view and orbit, day/night, weather,
simulation speed, water-level/current multipliers, grounding damage and Gorodets mission
phase, score and penalty accumulators. Transient visual effects and radar trails restart.

Saves use `voyage.json` in Unity's `Application.persistentDataPath`. On this Windows
machine the default location is `%USERPROFILE%\AppData\LocalLow\DefaultCompany\ShipSim159`.
A successful overwrite retains the previous save as `voyage.json.bak`. Invalid or
unsupported saves are rejected without applying their data. The menu reports read/write
failures; settings are stored separately in Unity PlayerPrefs.

Use `Ship Simulator > Build Prototype` to regenerate the prototype scene and
prefabs. Use `Ship Simulator > Apply Visual Upgrade` after manually changing
the generated environment.

## Vessel Controls

| Input | Action |
|---|---|
| `Escape` | Open pause menu; return from a submenu; resume voyage |
| `W` / `Up Arrow` | Increase engine telegraph command |
| `S` / `Down Arrow` | Decrease engine telegraph command |
| `Space` | Set telegraph to Stop |
| `A` / `Left Arrow` | Command port rudder |
| `D` / `Right Arrow` | Command starboard rudder |
| `C` / `Enter` | Rudder midships |
| `H` | Sound horn |
| `T` | Cycle simulation time through 1x, 2x, and 4x |
| `Shift+T` | Return simulation time to 1x |
| `F2` | Cycle wind force |
| `F3` | Rotate wind direction by 45 degrees |
| `F4` | Cycle rain intensity |
| `F5` | Cycle fog intensity |
| `R` | Reset vessel |

Engine thrust and rudder angle change gradually. The selected command is not
the same as the current physical response.

## Cameras

Use number keys `1` through `8` for Chase, Bridge, Top, Port, Starboard, Bow,
Stern, and Docking views. `V` cycles views. Hold the right mouse button and
move the mouse to orbit; use the wheel to zoom.

## HUD

The top bar shows speed, course, drift, side slip, estimated depth,
under-keel clearance, current direction, cargo load, RPM, and engine load.
Depth changes from normal to warning or critical colors as clearance reduces.

The minimap displays an approximate fairway, route, vessel heading, buoys,
shallow side zones, and training waypoint. Press `M` to hide it. Press `F1` to
emphasize the control shortcut strip.

The time panel provides `1x`, `2x`, and `4x` buttons. Time scaling affects
vessel physics, current response, mission timing, and visual simulation.

The weather panel controls wind direction and force, rain intensity, and fog.
Wind changes the force applied to the vessel and the procedural water surface.
Rain and fog are visual prototype effects and are not validated visibility
conditions.

Depth, bathymetry, route geometry, RPM, and engine load are prototype
estimates. They must not be treated as validated navigation or training data.

## Landscape and Water Graphics

Both passages have continuous natural banks with meadow and wet-soil detail, varied
trees, bushes and reed clumps. Vegetation uses three levels of detail, with smaller
meshes at longer distances. The river reflects the vessel and banks, with animated
ripples, shoreline depth shading and sun highlights. Day/night and weather controls
continue to affect the scene.

Use `Ship Simulator > Upgrade Water And Landscape` to regenerate these visuals in
both scenes. The normal scene builders also include the upgrade. Generated assets
are shared under `Assets/ShipSimulator/Settings/NaturalLandscape/`; edit the builder
to make lasting changes to placement or plant shapes.

Planar water reflections require an extra scene render. The default reflection is
768 pixels with reduced distant detail. This is procedural scenery, not surveyed
landscape or validated navigation geography.

## Verification

Run EditMode and PlayMode suites from `Window > General > Test Runner`.
Latest verified result (2026-09-12): EditMode `30/30`, PlayMode `8/8`.
The dedicated menu smoke check also passed save/load across both scenarios.
The landscape smoke check passed daylight, rain/fog and night rendering with reflections.
