# Ship Simulator Operator Guide

## Running the Prototype

Open the project with the Unity version in `ProjectSettings/ProjectVersion.txt`, load
`Assets/ShipSimulator/Scenes/RiverTrainingScene.unity`, and enter Play Mode.
The maritime start menu appears when either training scene starts, including in a
standalone player. Choose **New voyage**, pick a vessel, then River familiarisation or
Gorodets passage; or choose **Continue voyage** to restore the saved passage with the
vessel it was sailed in.

## Vessels

| Vessel | Length | Deadweight | Engines | Handling notes |
|---|---:|---:|---|---|
| Volgo-Don 507B | 138.3 m | 5000 t | 2 x 662 kW, bow thruster | Longest and heaviest; the bow thruster helps at low speed |
| Volgo-Balt 2-95A/R | 113.9 m | 3474 t | 2 x 515 kW | Shortest and deepest (3.86 m draft): the tightest turn in deep water, but the least water under the keel and by far the widest turn in the Gorodets reach |
| Volgoneft 1577 | 132.6 m | 4803 t | 2 x 736 kW | Tanker with the most power and the highest simulated speed (10.7 kn); no bow thruster |
| Meteor 342U | 34.6 m | 114 passengers | 2 x 736 kW | Hydrofoil: rises onto its foils above about 30 km/h and runs at 65 km/h; steer early, it covers ground three times faster than a cargo ship |
| Luch 14352 | 23.7 m | 57 passengers | 1 x 382 kW | Air cushion between two skegs: lifts partly out of the water above about 11 km/h, draft drops from 0.66 m to 0.45 m, service speed 40 km/h |

The two fast craft behave differently from the cargo ships. Below takeoff speed they push their
hulls through the water and feel sluggish; through the takeoff range drag peaks, then falls away as
the foils or the cushion take the weight. Once supported they draw much less water, so shoals that
stop a cargo ship can be crossed, and they answer the helm quickly.

All five use the same controls. Generated vessels have exterior camera positions fitted to their
model bounds; the navigator view sits in each vessel's wheelhouse. Luch has one engine: either
Q/Z or E/X operates the same telegraph. The speed readout shows the nominal foil/cushion transition,
and draft/under-keel clearance follows the physical hull and appendages. Radar prediction shortens
its horizon at high speed to keep the straight-ahead path within 300 m.
Particulars are published values; manoeuvring coefficients and generated models are estimates.

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

The save restores the scenario, the vessel type, its position and rotation, linear/angular velocity,
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
| `W` / `Up Arrow` | Increase both engine telegraphs |
| `S` / `Down Arrow` | Decrease both engine telegraphs |
| `Q` / `Z` | Increase / decrease the port engine telegraph |
| `E` / `X` | Increase / decrease the starboard engine telegraph |
| `Space` | Set both telegraphs to Stop |
| `A` / `Left Arrow` | Command port rudder |
| `D` / `Right Arrow` | Command starboard rudder |
| `C` / `Enter` | Rudder midships |
| `J` / `L` | Bow thruster 50 % step to port / starboard |
| `K` | Bow thruster off |
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

The two engines and propellers are separate. A split telegraph (one ahead, one
astern) turns the ship slowly on the spot; the rudders work best in the
propeller slipstream, so a short burst ahead with the helm over steers even at
low speed. Ordering astern while the shaft turns ahead cuts fuel, brakes the
shaft and restarts the engine astern after a delay of several seconds, so crash
stops take time and distance. In shallow water the ship is slower, turns wider
and sinks by the bow (squat); near a bank it is pulled toward the bank and its
bow is pushed away. Wind gusts push and heel the ship, more so when it is
lightly loaded.

The bow thruster swings the bow slowly when the ship is stopped or barely
moving, for berthing and for starting a turn in a narrow reach. It loses most
of its effect once the ship is making a few knots, and in lightship condition
its tunnel is out of the water, so it gives nothing. The rudder readout shows
its side and power. All of these follow an estimated model, not trial data.

## Cameras

Use number keys `1` through `8` for Chase, Bridge, Top, Port, Starboard, Bow,
Stern, and Docking views. `V` cycles views. Hold the right mouse button and
move the mouse to orbit; use the wheel to zoom.

## HUD

The top bar shows speed, course, drift, side slip, estimated depth,
under-keel clearance, current direction, cargo load, and each shaft's RPM and
engine load. A split telegraph is shown as separate port (P) and starboard (S)
orders.
Depth changes from normal to warning or critical colors as clearance reduces.

The minimap displays an approximate fairway, route, vessel heading, buoys,
shallow side zones, and training waypoint. Press `M` to hide it. Press `F1` to
emphasize the control shortcut strip.

The time panel provides `1x`, `2x`, and `4x` buttons. Time scaling affects
vessel physics, current response, mission timing, and visual simulation.

The weather panel controls wind direction and force, rain intensity, and fog.
Wind changes the force applied to the vessel and the procedural water surface.
Rain and fog are visual prototype effects and are not validated visibility
conditions. Fog also obscures the water surface and reflections. Rain produces small splashes
and expanding ripples on exposed water. Ship waves flatten and break near natural banks, with
an estimated, weak reflected component.

At night the vessel displays steady underway lights: red to port, green to starboard, two white
mastheads facing forward, and a white stern light. Visibility depends on viewing direction;
turning across a sector boundary is not a flash cycle. Buoy lights have their own flash cycles.
The prototype does not switch to anchor, towing or special-status signal patterns.

Depth, bathymetry, route geometry, RPM, and engine load are prototype
estimates. They must not be treated as validated navigation or training data.

## Landscape and Water Graphics

Both passages have continuous natural banks with meadow and wet-soil detail, varied
trees, bushes and reed clumps. Vegetation uses three levels of detail, with smaller
meshes at longer distances. The river reflects the vessel and banks, with animated
ripples, shoreline depth shading and sun highlights. Day/night and weather controls
continue to affect the scene.

Shorelines have irregular wet-sand margins and submerged shelves. Water reveals the
bed in the shallows, then becomes opaque with depth; reeds form patches across the
waterline. These visual shapes are inspired by Volga riverbank photographs. The
Gorodets collision boundaries and scenario bathymetry remain separate prototype data.

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
