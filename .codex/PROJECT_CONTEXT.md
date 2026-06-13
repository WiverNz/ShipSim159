# ShipSim159 Project Context

Last updated: 2026-06-14

## Purpose

ShipSim159 is a Unity 6 URP prototype for simulating a Project 507B Volgo-Don
river cargo vessel. It is an engineering prototype, not a validated maritime
training product. Published vessel particulars and estimated physics parameters
are documented separately.

## Current Architecture

- Unity version: `6000.4.0f1`
- Main scene: `Assets/ShipSimulator/Scenes/RiverTrainingScene.unity`
- Runtime assembly: `ShipSimulator.Runtime`
- Editor assembly: `ShipSimulator.Editor`
- Tests: `ShipSimulator.EditModeTests` and `ShipSimulator.PlayModeTests`
- Vessel data: `Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json`
- Vessel prefab: `Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab`

The vessel uses a Rigidbody with point buoyancy, linear/quadratic resistance,
aggregate propulsion, rudder lift, wind force, and current-relative water
velocity. Keyboard controls are W/S telegraph, A/D rudder, Space Stop, R reset,
C rudder midships, V camera cycle, and number keys 1-9 for direct camera views.

## Work Completed

### Repository Documentation

The repository root now contains a public-facing `README.md` covering the
prototype scope, features, Unity version, startup steps, complete controls,
radar legend, editor tools, test workflow, project structure, simulation model,
known limitations, and links to the detailed project documentation.

### Detailed Vessel Model

The vessel model was imported from the CRYENGINE project
`G:\Projects\VolgoDon159`:

- source FBX: `Assets\props\volgo_don\volgo_don.fbx`
- imported into `Assets/ShipSimulator/Models/VolgoDon507/`
- CRYENGINE color/AO DDS maps were reused as URP base maps
- model axes are converted from CRYENGINE `X forward / Y starboard / Z up`
  to Unity vessel `Z forward / X starboard / Y up`
- scale was corrected to 138.3 m length
- width is approximately 16.4 m
- hull bottom was aligned for the configured 3.53 m loaded draft
- CRYENGINE proxy geometry was hidden
- a simple `CollisionHull` BoxCollider was retained for dynamic physics

`VolgoDonModelIntegrator.cs` can rebuild materials, alignment, prefab
integration, and a preview. Do not replace the dynamic collision hull with a
non-convex MeshCollider.

### Build Configuration

`RiverTrainingScene` is first and enabled in `EditorBuildSettings`.
The template `SampleScene` remains present but disabled. The prototype builder
also preserves this ordering when regenerating the scene.

### River Current

`RiverCurrentZone` registers vessels through trigger enter/stay/exit callbacks.
`ShipPhysicsController` uses:

- `ambientCurrentMps` when outside all zones
- the average velocity when inside overlapping zones

The current affects resistance and rudder-relative water velocity.

### Vessel Data Validation

`VesselDataValidator` validates all required JSON sections before physics starts.
It checks finite positive dimensions, mass ordering, load fraction, inertia,
engine/propeller/rudder configuration, position-array counts, hydrodynamic
coefficients, buoyancy, controls, and calibration multipliers.

Invalid JSON disables `ShipPhysicsController` through a failed load and logs a
specific error. Required propulsion, rudder, and buoyancy components are also
checked during `Awake`.

## Verification Status

Unity compilation completed without C# compiler errors.

Latest verified tests:

- EditMode: 9 passed, 0 failed
- PlayMode: 2 passed, 0 failed

EditMode tests cover the production JSON, invalid draft, mismatched propeller
arrays, startup scene order, required HUD/camera scene configuration, the
curved fairway depth profile, the compound vessel collision hull, and the
runtime ship navigation-light rig, and the buoy beacon flash timing.
PlayMode tests cover current-zone entry/exit and overlapping-zone averaging.

The `mcp-unity` package frequently times out after completing a request. Verify
test results in:

`C:\Users\User\AppData\LocalLow\DefaultCompany\ShipSim159\TestResults.xml`

Timeouts and `TestRunnerService` exceptions observed so far belong to the MCP
package, not the project tests.

## Visual Upgrade

The training scene now uses a procedural URP river shader, a subdivided water
mesh, tuned vessel materials, softer daylight/fog, post-processing, layered
grass banks, rocks, reeds, and lightweight procedural vegetation. The follow
camera has eight views, damped movement/rotation, speed-based FOV, mouse orbit,
wheel zoom, and improved look targeting.

The visual pass is implemented in
`Assets/ShipSimulator/Scripts/Editor/ShipSimulatorVisualUpgrade.cs` and is also
called by `ShipSimulatorPrototypeBuilder` when rebuilding the prototype. Its
Unity menu commands are:

- `Ship Simulator/Apply Visual Upgrade`
- `Ship Simulator/Render Visual Preview`

The water shader is
`Assets/ShipSimulator/Shaders/RiverWater.shader`; generated visual settings and
the water mesh are under `Assets/ShipSimulator/Settings`.

The river surface uses a procedural, texture-free multi-scale flow model rather
than large ocean waves. It combines low-amplitude irregular displacement,
heading-biased current motion, broken cross-ripples, sparse current streaks,
turbid green-brown depth colors, environment reflection, Fresnel response, and
fog. The tuned values are stored in `Materials/RiverWater.mat` and reproduced
by `ShipSimulatorVisualUpgrade.CreateWaterMaterial()`.

The river environment now covers the full orbit-camera view. Both banks use
generated terrain meshes with an irregular shoreline, sloped soil, rolling
grass heights, meadow patches, rocks, reeds, and vegetation extending behind
the vessel. The water and terrain continue far enough in both directions to
end in atmospheric fog rather than at a visible world edge. The original box
banks remain as invisible physics colliders.

## Control HUD

`ShipTelemetryUI` builds a compact simulator-style bridge HUD at runtime. The
center view is kept clear by separate bottom rudder, telegraph, and camera
panels. It includes a heading tape, objective and distance, speed, course,
drift angle and side slip, estimated depth and under-keel clearance, current
direction, cargo load, estimated RPM/engine load, warnings, and active command
highlighting.

The HUD uses larger type and stronger hierarchy for primary values. The
objective is a wider card, the heading tape has a fixed center marker, normal
system status is not duplicated, warnings appear only when active, telegraph
buttons use full command names, and F1 toggles the full shortcut strip. Bottom
controls have larger safe-area margins and the camera panel is compact.

The river radar is vessel-centered and heading-up. The ship remains fixed while
the curved route, waypoint, channel edges, and paired buoys move and rotate
around it. A continuous color bathymetry grid, range frames, crosshairs, and a
rotating sweep combine navigation and depth information in one modern display.
It shows current depth, minimum depth ahead, draft, and a safe/caution/shallow
legend. `M` toggles the radar and `F1` emphasizes the shortcut strip. Depth,
warning thresholds, bank geometry, and radar layout share `FairwayModel`.

Radar line meanings are explicit and visually distinct. A thin cyan dashed line
is the fixed ship heading, a yellow dashed route follows the curved
`FairwayModel` centerline, a gray trail records recent actual vessel positions,
and a short white dashed prediction uses forward speed, lateral drift, and the
actual rudder angle. The legend names all four line types; the radar size remains
420x420 because depth and fairway navigation are primary gameplay information.

UI controls call public command methods on `ShipPhysicsController`; they do not
simulate keyboard input. Engine and rudder response delays remain governed by
the vessel JSON. `W/S` and arrow keys step the telegraph, `A/D` and arrow keys
command the rudder, `C` or Enter centers it, Space selects Stop, and `H` sounds
the horn. Number keys `1` through `9` select Chase, Bridge, Top, Port,
Starboard, Bow, Stern, Docking, and Navigator views. Navigator is a fixed
forward-facing viewpoint from inside the wheelhouse. `V` cycles views. `N`
toggles day and night lighting, sky, ambient light, and fog. At night, each
buoy gains a short flashing beacon with staggered phases; fixed navigation
markers remain continuously lit. The vessel gains port and starboard
sidelights, forward and aft masthead lights, a stern light, and a white bow
light near the foredeck. Each vessel light now has a visible dark lantern
housing, mounting plate, and vertical support tied back to the deck,
wheelhouse, or mast base. The fixtures remain visible during daylight while
only their lenses and light sources are disabled.

`ShipWakeController` creates two propeller-wash, two hull-wake, and two bow-wave
trails at runtime. Wake width and opacity respond to speed and throttle.
Navigation markers are enlarged for visibility.

The training fairway uses 10 paired lateral buoy stations rather than two
straight decorative rows. The scene models Russian inland-river lateral
marking relative to the downstream `+Z` direction: the right edge is marked by
red buoys with red flashing lights, and the left edge by white buoys with dark
top marks and green flashing lights. Buoys are placed on normals to a curved
fairway centerline, with closer spacing through the bends and a slightly
narrower marked channel upstream. The radar mirrors the paired curved layout.
The simulated flash is 0.32 seconds in a 1.5-second period with staggered phases;
it is a prototype characteristic rather than a published local notice to
mariners.
Use `Ship Simulator/Arrange Navigation Buoys` to rebuild only the navigation
layout without rebuilding the full prototype.

Depth is an explicitly estimated curved channel profile because the scene has no
surveyed bathymetry. The minimap is a local fairway indicator, not a route
chart.
Mooring lines, anchors, thrusters, fuel, damage, and autopilot are not exposed
until their simulation systems exist.

Editor automation commands:

- `Ship Simulator/Play Training Scene`
- `Ship Simulator/Stop Play Mode`

After the latest HUD work, Unity compiled without C# errors. The current
EditMode result is 8/8 passed; the latest completed PlayMode result is 2/2
passed.

The latest water shader imported without shader errors and was checked using
front and rear 1600x900 visual previews. The MCP Test Runner timed out during
the water-only visual change, so the test counts above remain the latest
completed functional runs.

After the radar line clarification, EditMode completed again at 8/8. Runtime
Play Mode inspection found and fixed a hot-reload null-array issue; a clean
rerun produced no HUD exceptions. The PlayMode Test Runner request timed out
before replacing the XML, so 2/2 remains the latest completed PlayMode suite.

After correcting Russian river buoy sides and adding flashing beacons, EditMode
completed at 9/9. Runtime inspection confirmed night mode and observed the same
green beacon in both lit and unlit phases. The PlayMode Test Runner timed out
without replacing the XML, so 2/2 remains the latest completed PlayMode suite.

After mounting the vessel navigation lights and adding the bow light, Unity
compiled without C# errors and EditMode completed at 9/9. The navigation-light
test now verifies six sources plus physical support and housing objects. The
latest completed PlayMode suite remains 2/2.

The latest polish pass replaces the escaped objective speed text with
`Speed limit: max 8 km/h`, keeps the camera status on one compact line, and
moves all radar line meanings into an explicit color legend. Night moonlight,
ambient visibility, buoy beacon range/glow, and the existing six-trail vessel
wake were strengthened without changing the HUD or radar footprint. Unity
compiled the runtime and EditMode assemblies successfully. The focused HUD
formatting regression test passed; the suite now contains 10 EditMode tests,
but the MCP service timed out before completing a fresh full-suite run.

## Gorodets Scenario

`Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity` is a separate
2.27 km high-difficulty scenario for the existing Project 507B vessel. It is
enabled in Build Settings after the primary training scene.

The scenario includes a sampled curved route, asymmetric procedural
bathymetry, rocky shoal patches, paired lateral marks, three leading-mark
pairs, six blended current regions, differential bow/stern current yaw,
estimated squat and shallow-water resistance, keel-clearance grounding,
mission phases, warnings, and scoring.

Use `Ship Simulator/Build Gorodets Scenario` to regenerate its scene-owned
content. The geometry, depth, current, squat, damage, and scoring parameters
are estimated training-game values, not current navigation data.

The HUD now provides simulation time controls at 1x, 2x, and 4x. `T` cycles
the speed and `Shift+T` returns to 1x.

The Gorodets scene now reuses the procedural `RiverWater` shader and subdivided
water mesh instead of its former stretched URP Lit cube. `WeatherController`
adds configurable wind direction/force, camera-following rain, and fog. Wind
is passed to vessel physics and also adjusts water ripple strength. The HUD
uses `F2` for wind force, `F3` for direction, `F4` for rain, and `F5` for fog.

Leading marks now receive long-range lights plus large emissive night boards
and alignment stripes. These are created at runtime by `DayNightController`.

Latest verification after this work:

- Unity compilation: successful
- EditMode: 19 passed, 0 failed
- PlayMode: 5 passed, 0 failed
- Gorodets Play Mode smoke check: no runtime exceptions

## Known Limitations

- Hydrodynamic coefficients are estimates and are not training validated.
- Buoyancy uses 15 points rather than sectional or volume hydrostatics.
- Twin-screw propulsion is aggregated at one centerline force point.
- The Gorodets scenario has limited estimated shallow-water resistance, squat,
  grounding drag, and abstract damage points. It does not model cavitation,
  flooding, structural damage, or validated bottom interaction. Wake is visual
  only and does not affect physics.
- Static shore collisions use the generated bank slope meshes. The vessel uses
  three overlapping box colliders for bow, midship, and stern, with continuous
  dynamic collision detection.
- Input reads `Keyboard.current` directly; no rebinding or gamepad workflow.
- `maxLoadedSpeedMps` is data only and is not enforced.
- UI uses legacy `UnityEngine.UI.Text`.
- This workspace was not a Git repository during the initial analysis.

## Recommended Next Work

1. Add tests for loader parse errors and missing required ship components.
2. Replace direct keyboard polling with an Input Actions asset.
3. Split aggregate propulsion into port/starboard forces and controls.
4. Add real bathymetry and collision look-ahead warnings.
5. Calibrate acceleration, stopping, and turning against real trial data.

## Files to Read First

- `AGENTS.md`
- `Assets/ShipSimulator/Documentation/README.md`
- `Assets/ShipSimulator/Documentation/OperatorGuide.md`
- `Assets/ShipSimulator/Documentation/ShipSimulator_Physics.md`
- `Assets/ShipSimulator/Documentation/VolgoDon507B_Sources.md`
- `Assets/ShipSimulator/Scripts/Physics/ShipPhysicsController.cs`
- `Assets/ShipSimulator/Scripts/Physics/VesselDataValidator.cs`
- `Assets/ShipSimulator/Scripts/Editor/VolgoDonModelIntegrator.cs`
