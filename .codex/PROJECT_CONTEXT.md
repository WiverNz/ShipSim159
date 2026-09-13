# ShipSim159 Project Context

Last updated: 2026-09-13

## Literature-Based Ship Dynamics, 2026-09-13

Phases 1 to 6 of `ShipDynamicsRealismApproach.md` are implemented; phase 7 (ship-ship
interaction, locks, mooring, anchors) is not. `ShipSimulator_Physics.md` describes the model.

- `PropulsionController`, `RudderController`, `HydrodynamicResistance` and `BuoyancyPoint` were
  deleted. The vessel prefab was cleaned of their components, the `PropulsionPoint` object and the
  `BuoyancyPoints` hierarchy; `ShipSimulatorPrototypeBuilder` no longer creates them.
- New plain C# models under `Scripts/Physics/`: `VesselParameters`, `HullDerivativeEstimate`,
  `ManoeuvringModel` (3-DOF MMG with the full added-mass matrix), `HullForceModel` (cross-flow drag
  blend, current shear), `ResistanceModel` (ITTC-1957, Lackenby), `EngineShaft`, `PropellerModel`,
  `RudderModel`, `WindLoadModel` (Blendermann), `RestrictedWaterModel` (depth factors, ICORELS
  squat with blockage, estimated bank suction), `HydrostaticsModel` (station prisms),
  `ManoeuvringSimulator` and `ManoeuvringTrials`.
- `ShipPhysicsController` applies the MMG accelerations with `ForceMode.Acceleration`, station
  buoyancy and heel moments, and steps `GroundingController` (keel springs plus bottom friction
  through the added-mass solve). `ManualStepping` and `SetDepthProvider` support tests.
- The vessel JSON schema was rewritten (`VolgoDon507B.json`), and `KVLCC2_MMG_Benchmark.json`
  holds the published KVLCC2 MMG set for verification. Most 507B values are estimated; see
  `VolgoDon507B_Sources.md`. The old JSON had roll and pitch inertia swapped on Unity's axes.
- Twin engines are independent: HUD `Q`/`Z` port and `E`/`X` starboard telegraphs, `W`/`S` both;
  the HUD shows real shaft RPM and load per engine, and the radar prediction uses the actual rate
  of turn. `VoyageSave` stores `engineCommands` and `shaftRps` (older saves fall back to the shared
  throttle). `WeatherController` now pushes its gusting wind to the vessel.
- Reversing a running engine cuts fuel at once, brakes the shaft and restarts astern after a delay
  (regression test `EngineReversal_BrakesBeforeDrivingAstern`).
- `ShipWakeRuntimeCheck` waited for 4 m/s through the water, which the new model cannot reach in
  the 4.6 m Gorodets reach (about 3.5 m/s at full ahead), so the ship ran on until it grounded and
  the check still passed. It now triggers at 3 m/s and fails if the vessel is not under way or is
  aground at a capture. `VoyageMenuSmokeCheck` clears the per-engine save arrays so it keeps
  testing the shared-throttle fallback.

Virtual sea trials (`Logs/SeaTrials/sea-trials.md`, `SEA_TRIALS|PASS`), all simulated with
estimated coefficients: 507B loaded deep water slow/half/full 3.0/6.3/9.8 kn, advance 3.22 L,
tactical diameter 3.70 L, 10/10 overshoots 3.6/4.2 deg, 20/20 8.2 deg, crash stop 4.48 L in 263 s.
Gorodets 4.6 m: full ahead 6.9 kn, tactical diameter 6.59 L, bow squat 0.29 m. KVLCC2 benchmark:
15.4 kn, advance 3.00 L, tactical diameter 3.09 L, 10/10 overshoots 5.7/10.3 deg.

Verified: compile clean (`Logs/dyn-compile.log`); EditMode 105 passed
(`TestResults/EditMode.xml`); PlayMode 15 passed (`TestResults/PlayMode.xml`).

Known limits: quadratic, not four-quadrant, propeller curves; estimated bank model (the Lataire
papers were not accessible); B/T <= 4 depth factor branch used for all hulls; wall-sided
hydrostatics; Unity collider contacts bypass the added-mass solve. Calibration against 507B trial
data is the next step and needs real documentation.

## Water Weather, Bank Wake and Running Lights, 2026-09-13

- Water fog was missing because shared HLSL contained the fog variant pragmas but was loaded
  with a normal include. Both water passes now use `include_with_pragmas`; surface fog uses
  URP's per-fragment eye depth, including reflections, foam and highlights.
- Rain particles now use explicit alpha blending and soft impact textures. Small water impacts
  are emitted only inside the bank waterlines and below unobstructed air; rain also adds expanding
  normal-map rings and increases surface roughness. Impact rings remain normal-only under TAA.
- `RiverShoreProfile` samples the actual zero-height crossings of the generated natural-bank
  meshes in each scene. Wake crests flatten and dissipate in shallow water, with breaking foam
  and a weak mirrored incident wave near the bank. Current and previous water displacement use
  the same bank response, preserving motion-vector consistency. The exaggerated normal gain
  was reduced from 1.9 to 1.15.
- Ship running lights are steady, with red port and green starboard 112.5-degree sectors,
  two forward-facing white 225-degree mastheads, and a white 135-degree stern light. The aft
  masthead is 4 m higher than the forward one; the extra all-round white bow light was removed.
  Point-light cookies and emissive lenses share these sectors. The lens keeps a small minimum
  pixel size to reduce aliasing and handles flipped render-texture projections. Its shader lives
  in Resources so player builds include it. Buoy flashers remain separate.

Reference pattern: [UNECE CEVNI Article 3.08](https://wiki.unece.org/spaces/TransportSustainableCEVNIv5/pages/25265334/Article%2B3.08%2B-%2BMarking%2Bfor%2Bmotorized%2Bvessels%2Bproceeding%2Balone),
with light sectors as defined in [navigation Rule 21](https://www.navcen.uscg.gov/navigation-rules-amalgamated).
Fixture positions are estimated, not a verified Project 507B electrical plan or certification
against local navigation rules. This is an underway pattern; anchor, towing, special-status and
signal-light modes are not implemented. A stopped engine does not change the running pattern.

The bank effect is an analytic visual approximation, not a shallow-water solver: it does not
model propagated wave energy after a stopped track, full refraction, overtopping or reflected
wave travel around bends. Its near-bank depth slope and reflection strength are estimated;
physical bathymetry and vessel forces are unchanged. Missing natural-bank meshes disable the
shore response and water impact emission. Rain impact rejection uses collision geometry for
hulls and structures; it does not create rain impacts on decks or land.

Verified: 76 EditMode tests and 10 PlayMode tests passed in
`TestResults/water-weather-{editmode,playmode}.xml`. `WaterWeatherCheck.Run` checks dense-fog
attenuation, live rain impacts, and steady night light state over 400 frames, and captures both
weather and four viewing directions in `Logs/WaterWeather/`. The bank-wake capture uses a
synthetic wake fixture, leaving vessel physics frozen. Fog detail contrast dropped from 0.0068
to about 0.0001. It also rejects oversized night lenses, a flipped-projection bug caught during
visual inspection. `GraphicsPhaseOneCheck.Run` also passed (`Logs/water-weather-phase-one.log`):
water motion reached 90.8% of open-water pixels with a static camera, the cloud cookie and sky
ambient passed, and rainy-night sun intensity remained 0.127. Run graphics checks with rendering
enabled, in a dedicated batch editor.

## Wind-Driven Water and Vegetation, 2026-09-13

A stationary scene no longer looks frozen. `WindGustModel` varies the configured wind for
visuals only: speed gusts with occasional bursts, a slow direction meander and a shift of up to
20 degrees roughly every two minutes, eased over 25 s, with a 0.8 m/s light-air floor.
`WeatherController` publishes the gusting wind, its accumulated travel and the previous frame's
values; ripples, clouds and cloud shadows move by travel, so gusts change speed without jumps.
The vessel's wind force still uses the configured mean wind.

- Water: short wind waves (2.3 m and 5.1 m) on six fixed headings weighted toward the wind,
  moving at deep-water phase speed, with drifting wave groups; gust patches (cat's paws) roughen
  the surface. Both are normal-only, so water motion vectors are unchanged.
- Vegetation: `RiverVegetationWind.hlsl` bends whole plants downwind with height, rocks them on
  individual timing and flutters leaves, driven by `RiverGust`, the same gust field as the water.
  Plants mostly stand in lulls and bend when a gust passes. `RiverBark.shader` gives branches the
  same motion (with shadow, depth and motion vector passes); the bark materials were switched from
  URP Lit and their inherited disabled motion pass removed.

Amplitudes are estimated visual values. Verified: EditMode 64 passed (`TestResults/wind-editmode.xml`),
PlayMode 10 passed (`TestResults/wind-playmode.xml`); `GraphicsPhaseOneCheck.Run` passed with a new
still-water stage (mean image change 0.0245 over 80 frames with vessel and camera still) and motion
vectors on 94.7 % of open water and 23 % of the tree line (`Logs/wind-check.log`).

## Graphics Roadmap Phase 1 and Review Fixes, 2026-09-13

Phase 1 of `GraphicsRealismApproach.md` (commit f959559) added `RiverLighting` (sky ambient,
live sky reflection probe, histogram auto exposure through `RiverExposureFeature`), cloud
shadows, temporal AA with foliage and water motion vectors (`RiverTemporalFeature`), LOD
cross-fade on vegetation, and removed the saturation boost. Menu: `Apply Graphics Phase One`.

A review of that commit found six problems, all now fixed:

- **Post-processing never reached the screen.** `ConfigurePostProcessing` added volume components
  without making them sub-assets, so `RiverVisualProfile.asset` reloaded with five null entries:
  no tonemapping, bloom, vignette, white balance or colour adjustments. Components are now saved
  as sub-assets; ACES and zero saturation apply.
- **Night was never detected in Gorodets.** `RiverLighting` looked for the clock on its own
  object, but Gorodets serializes the weather system while the HUD adds the clock to itself.
  Controllers are now found scene-wide and `DayNightController` refreshes the active instance.
- **TAA kept 20 % history** (`m_FrameInfluence` 0.8). The builder now sets `baseBlendFactor` 0.85.
- **Cloud shadows skipped URP Lit objects.** `Resources/RiverSkyLighting.compute` renders the
  same cloud function into a 512 px main light cookie spanning 6 km around the camera, so the
  vessel, buoys and marks darken with the terrain and water.
- **Water motion vectors were never written.** Requesting per-object motion data filtered out
  the stationary water renderer, and URP's `CalcNdcMotionVectorFromCsPositions` returns zero
  without that data. The water pass now draws without it and computes the vector itself.
  `_RiverMotionProbe` is a verification hook.
- **Sky ambient duplicated the sky gradient in C#** and its weighting was about 1.9 times too
  bright. Ambient is now projected from the live sky cubemap through the compute shader, scaled
  so a uniform sky of radiance L evaluates to L. Temporal AA is selected by whether the active
  renderer carries `RiverTemporalFeature`, not by asset name.

`GraphicsPhaseOneCheck.Run` now also verifies sky ambient, the cookie, motion vectors with a
still camera (the water and tree line must move, and a probe proves the water pass draws) and
night lighting in Gorodets.

Verified with Windows Unity 6000.6.0f1:

- EditMode: 56 passed, 0 failed (`TestResults/phase1-fix-editmode.xml`), including profile
  sub-assets, TAA history, cookie projection and ambient normalization.
- PlayMode: 10 passed, 0 failed (`TestResults/phase1-fix-playmode.xml`), including night with
  the clock on a different object.
- `GraphicsPhaseOneCheck.Run` passed (`Logs/phase1-fix-check.log`): with a still camera 90.9 %
  of the open-water region and 23 % of the tree line carry motion; night sun 0.127 in rain.
  Captures in `Logs/GraphicsPhaseOne/` inspected.

Limits: sky ambient and the cloud cookie need compute shaders (otherwise ambient stays at the
scene default and Lit objects get no cloud shadow); scrolling ripple normals have no motion
vectors and may soften under TAA; check timings are editor frame intervals on one RTX 4090, not
isolated GPU cost.

## Softer Natural Shoreline, 2026-09-13

Both scenes now have irregular wet-sand margins, a gently sloping submerged shelf,
and reed patches extending into the shallows. Bank rows are spaced at 3 m; plant
roots interpolate the actual bank triangles. Additional reeds use a separate seeded
random sequence so the woodland layout stays stable.

Reference: the Volga bank at Podgornoye, https://www.tursar.ru/page-joy.php?j=809,
showing exposed sediment, a visible submerged bed and uneven reed patches. No external
image assets were added to the game.

RiverGround lacked the DepthNormals pass needed by the SSAO prepass. This omitted the
bed from camera depth and prevented shallow-water blending. The added pass restores
bed detection. Water now blends over wet sediment with depth-based transmission,
fades shallow colour before the shelf ends, and reduces quiet-bank foam. Ground has
height-dependent wetness and a patchy sand-to-grass transition. Gorodets collision
boxes and scenario bathymetry remain unchanged; the familiarisation scene's existing
mesh colliders follow the regenerated banks. These remain procedural visual estimates.

Verified: 46 EditMode tests passed (`TestResults/shore-editmode.xml`), including
wet-margin area, submerged shelf, depth-pass presence and plant grounding. Final
shaders rendered without errors (`Logs/shore-final.log`). Both shore and close
waterline previews were inspected (`Logs/Landscape/*-after-shore.png` and
`*-after-waterline.png`). Runtime daylight, rain/fog and night checks passed with
planar reflections active (`Logs/shore-runtime.log`). The first test editor failed
during native initialization; the successful EditMode retry used `-nographics`,
with rendered shaders checked separately.

## Research Documents: Graphics and Ship Dynamics, 2026-09-13

Two research documents were added under `Assets/ShipSimulator/Documentation/`; no code
changed. `GraphicsRealismApproach.md` surveys sky, water, terrain, vegetation and
post-processing techniques and proposes a phased URP roadmap. `ShipDynamicsRealismApproach.md`
audits the current physics, collects MMG, Clarke, Blendermann, squat and bank-effect
formulations, and proposes an implementation and validation plan. Its audit found that
configured thrust (620 kN, speed independent) is about three times a power-based estimate and
that no physical speed limit exists; these are findings for later work, not fixed.

## Ship Waves, Cloud Sky and Realistic Water, 2026-09-13

Shader-drawn ship waves replace the old trail-renderer wake. `ShipWakeController` feeds
`ShipWakeTrack`, which records the stern track in the water frame (drifted by the
effective current, cleared when the vessel jumps more than 40 m, as on reset or load) and
publishes bow, stern and 46 history samples to `RiverWater.shader`. The shader draws:

- a deep-water Kelvin wake by stationary phase: transverse and diverging waves inside the
  19.47 degree wedge, build-up at the cusp, spreading loss and age decay;
- a bow pressure crest, midship drawdown and stern rise around a stadium-shaped hull;
- propeller wash as a long pale aerated band with shorter-lived streaky foam, plus hull,
  crest and shoreline foam.

Waves displace the surface and tilt normals, follow the curved track in turns, and keep
spreading after the vessel stops. The track search runs per pixel, because interpolating it
from vertices left jagged seams. For displacement the water mesh is subdivided three times
per edge at runtime only; stored scene meshes are unchanged.

All wake amplitudes are **estimated visual values** (amplitude 0.07 x U^2/g, slope gain
1.9, hull wave shape tuned by eye), not a validated wave height, wash or bank-erosion model.
Nothing feeds back into physics: no wave forces on the vessel or buoys, no shallow-water
or bank reflection of waves, and the Kelvin pattern assumes deep water.

Water: a generated seamless ripple normal map (`Settings/Water/RiverRippleNormal.png`,
integer wave numbers per tile) replaces per-pixel value noise; Schlick Fresnel,
distance-widened sun glint, view-aligned reflection distortion and an olive turbid colour.
`RiverSky.shader` replaces the procedural skybox with a fog-matched horizon, sun glow and
disc, drifting lit clouds that close to overcast under rain or fog, wind drift and night
stars. The ground shader uses rotated fBm and fades aliasing detail. Cameras use SMAA High,
the PC URP asset grades in HDR, and SSAO uses a 0.9 m radius at 0.6 intensity.

`RiverWaterAndSkyBuilder.ApplyBoth` (menu `Apply Realistic Water And Sky`) updates both
scenes; `RiverLandscapeBuilder.Apply` calls it, so scene rebuilds keep the result.
`WeatherController` drives the sky through the `_RiverCloudWeather` and `_RiverWind` shader
globals instead of editing the sky material.

Cost: up to 47 segment tests per water pixel inside the wake bounds, and about 0.6 million
water vertices in Gorodets after subdivision. Not profiled on low-end hardware.

Verified with Windows Unity 6000.6.0f1:

- Compilation: exit 0, no blocking errors.
- EditMode: 43 passed, 0 failed (`TestResults/wake-editmode.xml`).
- PlayMode: 9 passed, 0 failed (`TestResults/wake-playmode.xml`).
- `ShipWakeRuntimeCheck.Run` passed (`Logs/wake-runtime.log`); captures in `Logs/Wake/`
  inspected. The straight run reached 4.0 m/s through the water. Under helm the vessel
  slowed to 0.42 m/s near a bank, so the turning captures show a slow curved wake.
- `LandscapeRuntimeCheck.Run` passed with the cloud sky in daylight, rain/fog and night.

Known limitations: the environment reflection probe was not rebaked for the new sky, so
water reflections are clamped to the planar image at screen edges; a visible seam can
remain on the inner side of very tight turns; vegetation and vessel materials are unchanged.

## Empty Editor Startup Fix, 2026-09-12

The user editor log showed Play entered an `untitled` scene, with no voyage camera
or menu, and `Library/LastSceneManagerSetup.txt` contained an empty scene list.
`TrainingSceneStartup` now opens Gorodets on interactive startup when only a clean
untitled scene is open. From other non-voyage scenes, Play uses `playModeStartScene`
to launch Gorodets while preserving editor work. Either selected training scene
still plays directly. Automatic setup is disabled in batch mode for test/build isolation.
The explicit Play Training Scene command now offers to save modified scenes.

Verified: 33 EditMode tests passed (`TestResults/startup-editmode.xml`), including
unsaved-work preservation and both selected scenarios. The dedicated
`LandscapeRuntimeCheck.RunFromEmptyScene` passed empty-scene startup through the
menu into daylight, rain/fog and night rendering (`Logs/startup-runtime.log`).
Interactive verification also opened Gorodets successfully (`Logs/startup-editor.log`).
Startup waits for Unity to restore its scene setup before selecting the default scene;
the first editor delay callback can run too early.

## Water and Natural Landscape, 2026-09-12

Both scenes now use continuous riverbank meshes, alluvial soil and meadow shading,
branched foliage meshes, bushes, reeds and deeper woodland. RiverTrainingScene has
782 trees and 255 bushes; Gorodets has 1,036 trees and 361 bushes, plus reed clumps.
Plant roots interpolate the actual bank triangles, avoiding floating trees on hills.
Six tree variants, three bush variants and a reed prefab share generated mesh assets
with three LOD levels. Leaves use procedural cutouts, colour variation, translucency
and wind motion. No external vegetation packages or texture downloads were added.

`RiverLandscapeBuilder.ApplyBoth` updates existing scenes, and both original scene
builders call `Apply` so the upgrade survives regeneration. Assets are under
`Settings/NaturalLandscape/`; existing generated asset GUIDs are preserved on rebuild.
The Gorodets box-bank renderers are hidden while their existing colliders remain.
The familiarisation scene uses the new bank meshes for shore collision. Fairway,
bathymetry and vessel physics parameters are unchanged.

Water now uses scene-depth shoreline shading, distance-filtered ripple normals,
shadowed sun highlights and planar reflections of the vessel, banks and vegetation.
`RiverPlanarReflection` renders a 768-pixel HDR reflection with a 1,400 m far limit,
excludes water/UI layers and guards recursive rendering. It adds one scene render per
visible camera, so it has a GPU cost; tree LODs and reflection mipmaps limit detail.
DirectX reflection UVs require a vertical flip. Runtime assembly references now include
URP/Core to submit the reflection render request. The PC shadow distance is 180 m and
daylight sky tint is neutral to remove the previous green horizon cast.

Verification:

- Unity compilation and rendered shaders: no blocking errors.
- EditMode: 30 passed, 0 failed (`TestResults/landscape-editmode.xml`).
- PlayMode: 8 passed, 0 failed (`TestResults/landscape-playmode.xml`).
- `LandscapeRuntimeCheck.Run` passed daylight, rain/fog and night captures with planar
  reflections active (`Logs/landscape-runtime.log`).
- Both scenarios' rendered previews and in-game captures inspected in `Logs/Landscape/`.

Rendering was checked on this Windows machine's desktop URP configuration. This is
an improved procedural environment, not scanned terrain or validated geography.

## Maritime Menu and Saved Voyages, 2026-09-12

Both training scenes now open a runtime maritime start menu with a procedural river
chart, compass and vessel motif, navy panels and brass accents. New voyage selects
River familiarisation or Gorodets; Continue restores the saved passage. Escape opens
the pause menu and returns from submenus before resuming. Menu pause blocks gameplay
input, hides the HUD, pauses simulation audio and restores the prior time scale.

`Scripts/UI/VoyageMenu.cs` bootstraps without scene edits, so builders preserve the
feature. `VoyageSettings` persists volume, camera sensitivity, quality, VSync and
standalone display mode. `Scripts/Persistence/VoyageSave.cs` provides versioned JSON,
validation and atomic replacement with a previous-save backup. The default save is
`Application.persistentDataPath/voyage.json`; saving is manual, with one slot.

Save state includes scene, Rigidbody pose and velocities, commanded and actual engine
and rudder state, camera orbit/view, weather and day/night, simulation speed, current
and water-level multipliers, grounding damage, Gorodets phase, score and penalties.
Particles and radar trails restart. Normalize Unity's Rigidbody quaternion when
capturing: floating-point drift can otherwise fail validation on a valid rotation.

Verified with Windows Unity 6000.6.0f1 from WSL:

- Compilation successful; no blocking compiler errors.
- EditMode: 25 passed, 0 failed (`TestResults/menu-editmode.xml`).
- PlayMode: 8 passed, 0 failed (`TestResults/menu-playmode.xml`), including actual
  injected Escape input, blocked gameplay keys, pause speed and state restoration.
- Dedicated `VoyageMenuSmokeCheck.Run`: passed start, save, switch to Gorodets,
  load the river save, pause and resume at the saved speed (`Logs/menu-smoke.log`).
- Menu screenshots inspected at `Logs/MenuScreenshots/`: start, settings and pause.

The smoke check enables background execution and queues editor player-loop updates;
a batch editor without a focused Game view otherwise stalls. PlayMode input tests use
Unity's isolated `InputTestFixture` to avoid the editor consuming injected keyboard
events. Screenshot tests render the menu through an offscreen camera because a batch
editor does not produce normal `ScreenCapture` output without a Game view.

Standalone fullscreen switching has not been exercised in a built player. Historical
verification notes below describe earlier work; the counts above are the latest runs.

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

Verify test results in:

`C:\Users\User\AppData\LocalLow\DefaultCompany\ShipSim159\TestResults.xml`

The `mcp-unity` package used to run these and frequently timed out after
completing a request; timeouts and `TestRunnerService` exceptions observed then
belonged to that package, not to the project tests.

## Unity 6000.4 to 6000.6 upgrade, 2026-09-05

The editor was upgraded from `6000.4.0f1` to `6000.6.0f1`, which rewrote
`ProjectVersion.txt`, `Packages/manifest.json`, `packages-lock.json`,
`QualitySettings.asset` and `UniversalRenderPipelineGlobalSettings.asset`, and
added `PhysicsCoreProjectSettings2D.asset` and `ProjectAuditorSettings.asset`.
Package bumps of note: URP 17.4.0 to 17.6.0, Input System 1.19.0 to 1.20.0,
Test Framework 1.6.0 to 1.8.0, Timeline 1.8.11 to 6.6.0.

The upgrade left the project unable to enter Play Mode. **No ShipSimulator code
was affected**: all 75 logged `error CS` lines came from the third-party
`com.gamelovers.mcp-unity` package, which used two APIs that Unity 6.6 made
hard-obsolete, `EditorUtility.InstanceIDToObject(int)` and
`Object.GetInstanceID()`, reported as CS0619 at 25 call sites.

Resolved by removing `com.gamelovers.mcp-unity` from `Packages/manifest.json`.
It was a dev-only editor bridge; no code under `Assets/` referenced it. The
package had been declared as an unpinned git URL, so the resolved revision was
whatever it last fetched (`cce8b57de9cb`).

That cleared all 75 `CS` errors but Play Mode was still blocked, by a second
incompatible package that the first pass had missed: `com.unity.ai.assistant`,
pinned to the stale pre-release `2.12.0-pre.2`, failing Unity 6.6's own analyzer
with `error UAC0020` in `DynamicAssemblyBuilder.cs(191,20)`, because
`System.Reflection.Assembly.Load(byte[])` loads into a non-Unity
`AssemblyLoadContext`. It was a direct manifest entry (`depth: 0`) with no
reverse dependencies and nothing under `Assets/` referencing it, so it was
removed the same way.

The lesson worth keeping: **grep for `error [A-Z]`, not `error CS`.** Unity 6.6
analyzers report blocking errors under prefixes like `UAC`, so a `CS`-only
search reported this project as clean while it could not enter Play Mode.

Not yet verified: a clean compile and the EditMode/PlayMode suites have not been
run since either removal. That is the next thing to confirm.

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
