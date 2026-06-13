# Ship Simulator Operator Guide

## Running the Prototype

Open the project with Unity `6000.4.0f1`, load
`Assets/ShipSimulator/Scenes/RiverTrainingScene.unity`, and enter Play Mode.
The same scene is the first enabled scene in Build Settings.

Use `Ship Simulator > Build Prototype` to regenerate the prototype scene and
prefabs. Use `Ship Simulator > Apply Visual Upgrade` after manually changing
the generated environment.

## Vessel Controls

| Input | Action |
|---|---|
| `W` / `Up Arrow` | Increase engine telegraph command |
| `S` / `Down Arrow` | Decrease engine telegraph command |
| `Space` | Set telegraph to Stop |
| `A` / `Left Arrow` | Command port rudder |
| `D` / `Right Arrow` | Command starboard rudder |
| `C` / `Enter` | Rudder midships |
| `H` | Sound horn |
| `T` | Cycle simulation time through 1x, 2x, and 4x |
| `Shift+T` | Return simulation time to 1x |
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

Depth, bathymetry, route geometry, RPM, and engine load are prototype
estimates. They must not be treated as validated navigation or training data.

## Verification

Run EditMode and PlayMode suites from `Window > General > Test Runner`.
Latest verified result: EditMode `16/16`, PlayMode `4/4`.
