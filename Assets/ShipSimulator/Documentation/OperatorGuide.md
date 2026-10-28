# Ship Simulator Operator Guide

## Running the Prototype

Open the project with the Unity version in `ProjectSettings/ProjectVersion.txt`, load
`Assets/ShipSimulator/Scenes/RiverTrainingScene.unity`, and enter Play Mode.
The maritime start menu appears when either training scene starts, including in a
standalone player. Choose **New voyage**, pick a vessel, then River familiarisation or
Gorodets passage; or choose **Continue voyage** to restore the saved passage with the
vessel it was sailed in.

## Choosing HUD panels

Open **Settings > HUD panels** from Escape or the gear icon. Each row cycles through
**Hidden**, **In tab** and **Pinned**. Engines, helm, cargo, deck machinery, anchors/mooring,
camera, simulation speed, weather, passage objective and map have independent choices.
Preferences apply immediately and persist between launches and vessel changes. Equipment
controls appear only when the selected vessel implements that equipment.

The bottom icon bar opens helm, deck, camera, weather, map and help; hover or focus an icon
for its caption. Clicking the active icon collapses its drawer. Pinned controls remain
available across tabs. By default the map and simulation speed are pinned. Wide screens
put pinned controls on the right; narrow screens share a scrolling drawer with the selected
tab. Scroll to reach panels below the visible area. Hiding a panel never cancels its active
commands or removes keyboard controls. Warnings remain visible independently of panel choices.

**Panel size** controls the panel scale from 75% to 115%, with an 85% default. The separate
**Interface & camera > Interface scale** setting still scales the whole interface.
**Restore defaults** on the panels page resets only panel visibility and size.

## Volgo-Don cargo and ballast

The loading panel above the deck-machinery controls provides **SELECT COMPARTMENT**,
**FILL**, **EMPTY** and **STOP ALL**. It shows compartment tonnes/capacity, total
displacement and estimated mean draft. Stop engine commands and reduce ground
speed below 0.3 m/s to transfer. Cargo also requires **HALF OPEN** hatches with
the mechanism stationary. Ballast and stores do not require open cargo hatches.

The default load reserves 200 t for fuel/stores and carries 4800 t of cargo.
All cargo, ballast and stores share the 5000 t deadweight limit. Unload before
adding ballast to the full ship. Rates are gradual and follow simulation time;
commands pause when conditions prevent transfer and resume when conditions allow.
Use **STOP ALL** to cancel them. Save/load preserves individual quantities and
commands; older saves use the default inventory. Tank groups and rates are
estimates, not an operational loading booklet. See
`Art/Source/VolgoDon507B/PART7_REVIEW.md` for limits and test conditions.

Open holds show the cargo actually carried in each hold as a heap of nominal bulk
cargo (an estimated 1.6 t/m3 sand/gravel class, not a documented cargo); an empty
hold shows its dry floor. The river surface no longer shows inside the hull.

## Volgo-Don deck machinery

The DECK MACHINERY panel shows the hatch target, the current phase and the time left.
Hatches run at real-time rates derived from the archive hydraulics and winches: about
6 minutes from closed to half open (outer covers lifted, inner covers rolled under them,
outer covers lowered onto the stack) and about 6 more to move a stack over the other hold.
Simulation speed shortens the wait; pause stops it. A new command reverses an operation
from wherever it is; switching holds always passes through the half-open stack. Cargo
transfer needs the hatches stationary at HALF OPEN.

Anchors and mooring lines have their own panel, below.

## Volgo-Don anchors and mooring lines

The ANCHORS & MOORING panel, available through Deck or independently pinned, lists each anchor with its state and
chain out, the selected mooring station, and every line made fast with its state and its load as a
percentage of the line's breaking load. Keys (also under F1):

| Key | Button | Action |
|---|---|---|
| G | ANCHOR | Select port, starboard or stern anchor |
| B | LET GO | Stowed: let go; the chain runs out freely until the anchor is on the bottom, then the brake is set. On the bottom: veer (pay out on the brake at 0.5 m/s) |
| Y | HOLD | Set the brake (stops veering or heaving) |
| U | HEAVE | Heave in at 10 m/min (bow) or 18 m/min (stern); slows above the windlass's nominal pull and stalls at its overload pull |
| O | STATION | Select a ship mooring station: bow head, bow breast, forward spring, aft spring, stern, starboard (S) or port (P) |
| P | BOLLARD | Select a shore bollard, nearest to the station first |
| I | PASS LINE | Pass a line from the station to the bollard: within 35 m, with less than 0.5 m/s way on |
| , | HAUL | Haul the line in at the winch rate, up to the winch's stall pull |
| / | HOLD | Hold the line at its length |
| . | SLACK | Pay the line out at 0.5 m/s |
| F | CAST OFF | Let the line go |

Anchor states: STOWED, LETTING GO, HANGING, NO BOTTOM DATA (no depth there: the anchor is not
landed on an invented bottom), ON BOTTOM, SLACK (on the bottom, chain carrying next to nothing),
SETTING (dragging over the first metres while it digs in), HOLDING, DRAGGING, VEERING, HEAVING,
HEAVING, AWEIGH, WINDLASS STALLED and CHAIN PARTED. Line states: SLACK, TAUT, OVERLOADED (above half
the breaking load) and PARTED, announced with a notice in the panel.

Practice: let go, let her drift back, then veer to three to five times the depth before relying on
the anchor; slow astern helps it set. Chain only to the bottom lifts the shank and drags. A 1750 kg
anchor holds about 45 kN in sand in this model, so engines or a strong beam wind will drag it.
Heaving brings the ship up to the anchor, or the anchor home if the ship is heavier to move, then
breaks it out. Mooring: stop alongside, pass lines to the nearest bollards, haul them taut, then
hold. Hauling cannot exceed the winch pull, but the ship's own momentum can part a short line.

Demonstration: in the Gorodets passage there is a 150 m demonstration quay on the starboard bank
just ahead of the start, bollards B1 to B7, with a dredged pocket alongside. It is an estimated
practice berth, not a surveyed Gorodets structure, and it lies outside the buoyed channel, so the
mission reports OUTSIDE FAIRWAY there. The straight above it is a good place to practise anchoring.

Saves keep each anchor's state, position on the bottom, chain out and current command, and every
line's bollards, length and command. A line to a bollard that is not in the loaded scenario is not
restored, and the panel says so. Part 13 saves restore the chain paid out; the anchors settle on
the bottom below the hawse. Holding, rope and soil values are estimates for a prototype, not a
mooring safety calculation; see `Art/Source/VolgoDon507B/PART14_REVIEW.md`.

## Vessel sound

Every vessel plays the same shared sound library through its own profile, and only for equipment it
has and facts its data establishes. Everything you hear follows what the simulation computes, never
a lever position on its own.

All five vessels: their diesels (generic engine-room and exhaust texture that follows shaft speed
and fuel, not synchronized with their firing), propeller wash with thrust (not on the Luch, whose
real craft has a waterjet), water along the hull with speed through the water, the bow wave and
lapping, and wind relative to the vessel. On foils or a cushion the hull water fades as the hull
leaves the water. The Volgo-Balt, Volgoneft, Meteor and Luch have no firing rhythm, no starting air
and no blade beat: their engines' cylinders, gearing and propeller blades are not established in
their data, and sound that pretends to know them would be wrong. They have no hatch, anchor or
mooring sounds because those systems are not modelled for them. Their navigator views have no
wheelhouse muffling: their models have no wheelhouse interior.

The 507B, in addition:

- Each main engine separately: the firing rhythm of a six-cylinder four-stroke engine at that
  engine's rpm (direct drive: rpm / 20 firings per second), louder with the fuel
  the governor delivers. Moving a telegraph changes the sound only as the shaft speeds up or slows
  down. Reversing cuts the fuel (the firing stops), starting air hisses while it brakes the shaft,
  and a blast of starting air marks the restart in the new direction. There is no separate engine
  start or stop sound: the model has no such operation.
- Propellers: the blade-passing beat at four times the shaft rate, heard near the stern.
- Hatches: the hydraulic pump while covers are jacked up or lowered, the cover winches and rolling
  wheels while they slide or traverse, start and stop sounds, a clunk when the direction changes
  and a heavy seat at the end positions.
- Anchors: the chain running out when let go, veered or rendering on the brake; the windlass motor
  and links clanking over the gypsy while heaving, at the chain's speed; the brake being released
  and set; the anchor seating in its pocket; the windlass stopping when it stalls.
- Mooring lines: the winch while hauling or slackening, creaking that grows with the load and how
  fast it changes, and a snap when a line or a chain parts.

From the NAVIGATOR view the outside is muffled (an estimated wheelhouse filter, not a model of the
real superstructure); every other view hears the ship from outside.

None of these are recordings of any catalogue vessel or its engines. They are licensed recordings of
comparable equipment and some original synthesis; see `Art/Source/Audio/AUDIO_SOURCES.md`.

## Aground

The HUD shows the bottom's holding force in kN next to the grounding warning.
Full astern works the ship off only while that force stays below the effective
astern push, about 106 kN for the loaded 507B: a drift or very slow touch on a
gentle sand bank. A firmer entry holds her; reduce draft (discharge or deballast
the end that is aground), wait for different water levels, or treat it as needing
assistance. Split engines and nozzles do not add grip, but on a long flat-bottom
contact they will not free her on their own. Soil and friction values are estimates;
see `Art/Source/VolgoDon507B/PART12_REVIEW.md`.

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
model bounds; generated navigator views sit just outside the front glazing, since the models
have no cabin interior. This eye follows the hull without positional lag. Luch has one engine: either
Q/Z or E/X operates the same telegraph. The speed readout shows the nominal foil/cushion transition,
and draft/under-keel clearance follows the physical hull and appendages. Radar prediction shortens
its horizon at high speed to keep the straight-ahead path within 300 m.
Particulars are published values; manoeuvring coefficients and generated models are estimates.

## Menu, Settings and Saved Voyages

Press **Escape** underway to pause the vessel, mission timing and simulation audio.
Choose **Resume voyage**, or press Escape again, to continue at the previous simulation
speed. Escape from a settings or confirmation page returns to the main menu first.
Menu buttons support mouse and keyboard navigation with arrow keys and Enter.

**Settings** provides sound, camera orbit sensitivity, graphics quality, VSync
and fullscreen/windowed display. Settings persist between launches. Display mode is
available in standalone players; the editor controls its own Game view.

**Settings > Sound** holds the master volume, separate levels for engines and propellers,
water, wind and deck machinery, and **Mute**, which silences everything but keeps the mix.
A standalone player also falls silent while another window has focus; the editor leaves that
to its own Mute Audio button. Pause stops all simulation sound and resumes it unchanged. At
2x and 4x simulation speed the machinery runs faster and the sound follows its state, but the
pitch is never raised by the speed-up itself. Loading a voyage brings the continuous sounds back
in from the loaded state over a fraction of a second and does not replay events such as an
anchor being let go.

Use **Save voyage** in the pause menu to record one passage. Replacing a save, loading
over an active voyage, starting a new passage and quitting require confirmation.
There is no autosave: save before leaving if you want to keep your progress.

The save restores the scenario, the vessel type, its position and rotation, linear/angular velocity,
commanded and actual engine/rudder state, camera view and orbit, day/night, weather,
simulation speed, water-level/current multipliers, grounding damage, the 507B's hatches,
anchors and mooring lines, and Gorodets mission phase, score and penalty accumulators. Transient visual effects and radar trails restart.

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

## Volgo-Don deck machinery

The Volgo-Don 507B has a DECK MACHINERY panel above the weather controls. CLOSE
stows its hatch covers; HALF OPEN opens both holds halfway; FORE OPEN and AFT OPEN
fully expose one hold. Transitions lift, slide and stack the covers, including
returning through the half-open arrangement when changing holds. Machinery follows
simulation time, pauses with the voyage and resumes its saved movement after load.

Anchors and mooring lines are described under "Volgo-Don anchors and mooring lines". Propeller
rotation follows actual shaft RPM and nozzle rotation follows actual steering.
Day/night controls continue to operate the ship's five sector navigation lights.

## Volgo-Don contact checks, Part 16

The hull now contacts quays along its corrected bow, stern and sides. Use low
approach speeds; chain/line tension remains active against the quay. A light sand
shoal may allow a gradual astern departure when thrust exceeds bottom holding,
while a heavy grounding may require reduced draft or external assistance. The
sideways recovery check uses a specified external 30 kN pull, not a promise of
sideways self-propulsion. No teleport or automatic release is applied.

Loading restores ground contact from the restored Rigidbody pose. Old saves keep
the existing fallback rules for loading, engines, mechanisms and ground tackle.
The physics and contact values are prototype estimates, not navigation guidance.


## Scalable interface and control groups

Settings > Interface & camera provides interface scale (80-150%), camera rotation
sensitivity and independent zoom sensitivity. Changes apply immediately; Back, Done,
pause-menu resume, focus loss and application exit flush preferences. Restore defaults
on this page resets only these three controls, preserving sound and display choices.
The previous camera-sensitivity preference migrates without losing its value.

The HUD uses labelled icon tabs: Helm, Deck, Camera, Weather, Map, Help and Settings.
Deck is disabled on vessels without loading or deck machinery. Panels scroll vertically
when space is limited. Use the visible scrollbar or mouse wheel; settings also scroll
to keyboard-selected controls. Important engine orders and warnings retain text.
At wide logical resolutions navigation stays in a separate right-hand scroll pane;
at narrower sizes Map opens it in the control drawer. M toggles the side map on wide
screens, or switches between Map and Helm on narrow screens. F1 toggles Help.

Only left-click activates HUD commands. Wheel and right-button gestures that begin over
UI belong to UI; camera orbit started in the world retains its gesture until release.
Editing sliders blocks vessel keyboard commands. Opening the pause menu, changing
camera view or losing focus releases an orbit cursor. A held button after resume must
be released and pressed again before orbit starts.

Default wheel input changes distance by 20% closer / 25% farther per tick, with
fractional wheel motion retained. Default rotation is 0.06 degrees per pixel.
Rotation and zoom converge smoothly using real time, independent of simulation
speed. The exterior camera follows vessel heading with a stable horizon; navigator
follows the model eye. Numbered presets cut immediately, clearing previous motion.
Old origin-based orbit saves migrate to the new look-pivot coordinates, subject to
current vessel limits. See [the Part 3 review](UsabilityPart3Review.md) for tests
and the remaining physical-device/subjective handling assessment.


## Interface language

Open Settings > Language from the start menu or with Escape during a voyage.
Choose English, Русский, Deutsch or Español. The existing menu and HUD update
without reloading the scene, resetting controls or changing the save. The selection
is retained on the next launch. In Russian the path is Настройки > Язык.
Long menu rows wrap and scroll; language changes preserve open control panels.
See [Localization](Localization.md) for catalogue maintenance and display culture.
