# Ship, camera and interface usability plan

Status: Parts 1 and 2 complete on 2026-10-27; see the
[Part 1 review](UsabilityPart1Review.md) and [Part 2 review](UsabilityPart2Review.md).
Part 3 is implemented with human handling assessment pending. Parts 4, 5 and 6 are complete;
see [Part 4](UsabilityPart4Review.md), [Part 5](UsabilityPart5Review.md) and [Part 6](UsabilityPart6Review.md). Parts 7-9 remain separate work. The source audit below records the 2026-10-25 baseline.
This plan is independent of the completed Volgo-Don modelling Parts 1-17.
Commands such as `сделай часть 1` refer to the numbered parts below in this workstream.
Implement only the requested part, including its tests and documentation. Record its
result here and in project context. Do not silently start the next part.

## Scope and evidence

The audit read CLAUDE.md, project context, the runtime camera, HUD, settings,
menu, mission, save, loading, buoy visuals, hold visuals and their integration.
It did not reproduce the reported defects in the editor or built player. Code-level
causes below are confirmed where stated; visual symptoms and device-dependent input
still require reproduction. Earlier passing tests are historical evidence, not tests
of these proposed changes.

| Area | Current implementation and finding | Decision |
|---|---|---|
| UI | `ShipTelemetryUI.BuildInterface` uses a 1920x1080 CanvasScaler, fixed panel dimensions and legacy Text. `VoyageMenu.BuildInterface` uses a separate 1440x900 layout with a fixed 480x560 page. No user scale exists. These are layout constraints, not proof of every reported clipping case. | Share scale policy and responsive layout; verify rendered text bounds. |
| Icons | `HudTheme` already caches procedural `HudIcon` sprites; `HudButton` already supplies interaction states. Its click handler currently accepts any pointer button. | Extend these components; keep explicit captions for important commands and restrict activation to intended input. |
| Language | No localization service or localization package was found in runtime sources or manifest. Menu, HUD, passage restrictions, camera names and messages contain English literals. | Add one shared localization service with stable keys, four catalogues and live notification. |
| Zoom | `ShipFollowCamera.UpdateMouseOrbit` uses raw scroll Y in `distance * exp(-scroll * 0.002)`, without a user multiplier. One input unit changes distance by about 0.2%, 120 units by about 21%. Actual device units have not been captured. | Establish input units from the installed Input System and actual player; normalize once, then use proportional target distance and separate zoom sensitivity. |
| Orbit | Raw pixel delta changes yaw/pitch immediately at 0.12 degrees per pixel times existing sensitivity. Position SmoothDamp is 0.45 s, rotation has a second lag, both use scaled time. Orbit is about the vessel origin while look target is offset; hull pitch/roll also affect the transformed position. | Separate input targets, orbit smoothing and vessel tracking; diagnose vertical movement before selecting a fix. |
| Input ownership | Camera reads RMB/wheel globally except when VoyageMenu is open. Cursor unlock runs on release or component disable, but menu early return and navigator switching can skip release processing. | Define UI/map/camera ownership and cursor cleanup on pause, focus loss and view change. |
| Map | HUD radar is heading-up, with fixed `MapPixelsPerMeter = 0.82`, fixed rings and fixed label. Its viewport does not receive raycasts. Predicted path horizon assumes fixed visible range. | Extend the existing radar with range, expanded view, orientation and recenter controls; share projection and fairway data. |
| Buoys | `BuoyVisualRig.Build` explicitly sets existing colliders to `isTrigger = true`. Its comment documents why: old static placeholder colliders stopped loaded ships. Detailed mesh pieces have no solid contact shape; bobbing is visual Transform animation. | A dynamic floating, moored buoy is required; toggling triggers off alone would restore the old defect. Audit all scene builders, including unlit Serpukhov marks. |
| Other collisions | Ship controller already uses ContinuousDynamic. The 507B has 13 convex sections from Part 16. Grounding owns bathymetry contact; Gorodets banks/shoals avoid a duplicate PhysX response, while quays remain PhysX. | Preserve this ownership and audit coverage. Do not add a second rigid bank response or replace the verified hull wholesale. |
| Bow water | `HoldInteriorVisual` creates rectangular stencil quads only over hatch openings. `HullWaterMask` and both river passes share stencil bit 8. | The bow symptom is not reproduced. Distinguish missing water, internal water, hull seam and reflection/depth error before modifying geometry or masking. |
| Recovery | R calls `ResetVessel`, which returns the Rigidbody to startup pose, stops loading and resets controls/mechanisms/tackle. `ResetMission` separately resets mission and grounding. Saves already capture mission, loading and tackle. There is no validated last-safe-position history. | Add a coordinated recovery action using existing state services, with an explicit preservation policy. |
| Cargo | 507B `HoldInteriorVisual` already renders compartment-mass-driven heaps, hides empty heaps and caps overfill. It assumes 1600 kg/m3 nominal bulk. `LoadingState` records mass and compartment ids, but no commodity id. | Extend the existing inventory with cargo identity, volume and appropriate visuals; first reproduce the reported empty appearance in the current prefab/player. |
| Documentation | README incorrectly lists authored Meteor/Luch models as generated and anchors/mooring as absent; documentation index contains obsolete test totals. | Correct current facts now; update both READMEs as each part ships. |

Relevant implementation roots are `Assets/ShipSimulator/Scripts/{UI,Camera,Physics,
Persistence,Visuals,Editor}`. Preserve public ship command methods, assembly boundaries,
force-driven motion, loading budgets, scenario restrictions and generator ownership.
Do not import UI frameworks, replace hydrodynamics or create a parallel save system.

## Part 1: readable interface and persistent settings foundation

Dependencies: none.

- [x] Capture the current HUD, menu, help, loading and tackle panels at representative
  resolutions. Record clipped/overlapping text and inaccessible controls before editing.
- [x] Extend VoyageSettings with validated UI scale, separate rotation/zoom preferences,
  defaults and change notifications. Preserve the existing Sensitivity key through an
  explicit migration; save on settings completion and application lifecycle boundaries.
- [x] Apply a shared UI-scale policy to HUD, menu and popups, initially targeting
  80-150%. Add scrollable settings categories and responsive panel placement so scale
  increases do not simply push controls outside the viewport. Provide Restore defaults.
- [x] Keep readable minimum font sizes, text wrapping and flexible heights; use
  scroll/reflow for long content. Do not solve clipping by shrinking every label.
- [x] Extend HudTheme icons for settings, help, map, camera, zoom and recovery. Use
  icon plus short caption or localized tooltip; retain explicit text for engine orders,
  grounding warnings and destructive actions. Show active/disabled/hover states.
- [x] Fix pointer button acceptance and prevent gameplay commands while editing controls.
  Establish the input ownership contract used by Parts 3-4.
- [x] Add settings persistence/migration and layout regression checks. Save screenshots
  at 1280x720, 1920x1080, 2560x1440, 3440x1440 and 1280x1024 at scale extremes.

Acceptance: all essential actions remain reachable, labels do not clip, scale changes
apply immediately and survive relaunch, existing ship commands still work. Do not mark
camera feel fixed by merely exposing settings; its implementation belongs to Part 3.

## Part 2: Russian, English, German and Spanish

Dependencies: Part 1.

- [x] Add stable translation keys and UTF-8 catalogues for ru/en/de/es, with English
  fallback and visible development diagnostics for missing keys. Use a single service
  and event to update existing views, not a second UI tree.
- [x] Add a clearly visible Language field with native names: Русский, English,
  Deutsch, Español. Persist selection; changing it must not reload the scene or voyage.
- [x] Extract all player-facing literals: HUD, menu, settings, hints, confirmations,
  loading/tackle controls, mission and grounding messages, passage restrictions,
  save/load failures, camera names, weather and map legend. Keep data ids stable.
- [x] Store status codes and format arguments rather than cached English sentences;
  translate dynamic messages and refresh open panels without dropping slider focus/state.
- [x] Verify Cyrillic, German umlauts/ß, Spanish accents/ñ and symbol coverage in the
  shipped font. Keep serialization culture-independent; format display values deliberately.
- [x] Test key/placeholder parity, fallback, persistence and live switching during a
  voyage and pause. Repeat Part 1 layouts in all four languages, including long strings.

Acceptance: complete player-facing coverage in all four languages, no missing glyphs,
no restart, no lost voyage state. Later parts must add all four translations when shipped.

## Part 3: predictable camera controls

Dependencies: Parts 1-2.

- [x] Capture wheel and mouse delta units in the actual Windows player, including
  high-resolution wheel/trackpad where available. Check serialized camera values in
  scenes and builders; changing a C# default alone does not update existing assets.
- [x] Normalize wheel input at one boundary. Use proportional target distance; initial
  tuning target is roughly 15-25% per conventional detent at default sensitivity.
  Expose bounded zoom sensitivity independently of rotation sensitivity.
- [x] Reduce default rotation gain after recording the baseline; smooth target yaw,
  pitch and distance with frame-independent unscaled timing. Do not multiply pixel
  deltas by deltaTime. Keep input gain stable at 30/60/144 FPS and simulation 1x/2x/4x.
- [x] Use a consistent look pivot and pitch limits. Reproduce vertical jumps during
  orbit entry, view switches, zoom and vessel pitch/roll; separate deliberate hull motion
  from input or coordinate errors. Preserve the navigator eye's model-relative behavior.
- [x] Clear stale smoothing velocities/targets on restore, swap and view change;
  synchronize saved camera state and validate limits for every vessel size.
- [x] Consume RMB/wheel only when camera owns the gesture. Release the cursor on menu,
  focus loss, disable and switching into navigator view; do not reuse stale deltas.
- [x] Regression tests for input gain, pitch bounds, cursor lifecycle, save/restore and
  camera/map ownership. Run vessel shakedown and inspect all nine view captures.
- [ ] Manually assess handling in all nine views with a physical mouse or trackpad.

Implementation and automated evidence: [Part 3 review](UsabilityPart3Review.md).
The final checkbox remains open for human handling assessment; synthetic Windows
input and all-view captures are recorded separately.

Acceptance: useful near-to-far zoom in a short wheel gesture, controllable rotation,
no unexplained rise/jump or stuck cursor, independent saved sensitivities. Numerical
tests do not replace manual assessment of camera feel.

## Part 4: useful map and navigation interaction

Dependencies: Parts 1-3.

- [x] Extend ShipTelemetryUI/RadarChannel using existing FairwayModel and bathymetry;
  keep one projection for channel, contacts, vessel, track, waypoint and prediction.
- [x] Add map expand/collapse, bounded range buttons and wheel zoom over the map,
  pan in expanded mode, follow/recenter, heading-up/north-up and a compact legend.
- [x] Recompute scale labels, range rings, projected obstacle sizes and prediction
  horizon from the current viewport/range. Maintain readable icons and vessel heading.
- [x] Route wheel/drag exclusively to the map when appropriate. Starting a camera
  gesture outside UI must have a defined lifecycle when the pointer crosses UI.
- [x] Persist display preferences, not stale world positions from another vessel/scene.
  Recenter appropriately on voyage load, recovery and vessel change.
- [x] Verify north/head-up transforms, range changes, shore/route alignment, waypoint
  direction, resize and return-to-vessel. Preserve estimated-depth warnings.

Acceptance: the player can inspect the route and return to the ship without moving
the camera accidentally; markers and depth remain consistent with simulation geometry.

## Part 5: buoy contact and obstacle collision audit

Dependencies: Parts 1-2 for status messages; independent of map implementation.

- [x] Reproduce red-buoy passage and inventory collider shape, trigger, layer matrix,
  Rigidbody and CCD configuration for buoys, all six ships and the passive barge, banks, quays and moored
  craft in all three scenes. Identify the owner of each physical contact response.
- [x] Separate buoy physics from BuoyVisualRig. Add a solid convex contact shape
  matching the visible float, Rigidbody mass/inertia, buoyancy, drag and a finite
  mooring-restoring force applied in FixedUpdate. Record estimated parameters in data.
- [x] Reuse suitable ground-tackle math where practical, without attaching a ship's
  tackle controller to each buoy. Use stable damping and bounded physical mooring loads;
  the buoy must yield under impact and cannot become an immovable stop for a cargo ship.
- [x] Make the visual follow the simulated body. Retain beacon behavior and update
  map positions from physical buoy positions. Remove conflicting Transform animation.
- [x] Update the canonical prefab/builders, including unlit marks; rebuilding scenes
  must retain collisions. Define reset/save behavior with stable buoy ids, preserving
  displaced buoy state on voyage continuation where necessary.
- [x] Test stationary, glancing, head-on and astern contacts, low/cruising/top vessel
  speeds, 1x/2x/4x and relevant fixed steps. Check penetration, momentum response,
  buoy return, mass-ratio stability, sustained contact and separation after reversing.
- [x] Preserve Part 16 grounding/bank ownership and verified 507B hull. Test quay and
  shore approaches without duplicate forces, energy explosions or artificial trapping.

Acceptance: no solid obstacle tunnelling in the test matrix, no static-buoy blockade,
credible speed/mass response, no new unforced-water manoeuvring regression. Deep
grounding may still immobilize the vessel physically; Part 7 supplies player recovery.

## Part 6: grounded Volgo-Don bow and water rendering

Dependencies: Part 5 for the final contact baseline.

- [x] Reproduce the bow defect with exact pose, load, water level, grounding state,
  camera and screenshot. Compare loaded/light/trimmed hulls and open/closed hatches.
- [x] Inspect hull mesh/LOD seams, stencil footprint, depth, reflection clipping and
  water passes. Identify whether the symptom is exterior water being cut away or
  interior water showing through. Document the demonstrated cause before editing.
- [x] Correct the owning mesh/integrator or water-mask implementation. If hull-wide
  exclusion is needed, derive it from the actual hull volume and use correct depth
  testing, not a larger rectangle that removes adjacent river water.
- [x] Keep collider/hydrostatic geometry unchanged unless separately justified.
  Preserve existing hold masking, reflections, wake and motion-vector rendering.
- [x] Render bow/side/above/waterline views, afloat and grounded, at low/high water,
  light/partial/full load, day/night, multiple LODs and both editor/player paths.

Acceptance: no erroneous water gap outside the bow or water inside the hull in the
reproduction matrix, and no newly hidden river surface. Attach before/after evidence.

Evidence and scope: [Part 6 review](UsabilityPart6Review.md). The existing source
repair is retained; the new editor/player matrix uses controlled grounding and
depth offsets at a fixed rendered water datum.

## Part 7: recovery without losing the passage

Dependencies: Parts 1-2 and 5; recheck bow rendering after Part 6.

Chosen solution: an explicit pause-menu action to move to a validated last safe pose,
with fallback to a validated scenario recovery location. This is gameplay assistance,
not a claim that engines or a simulated tug can refloat any grounding. A tug simulation
is outside this workstream.

- [ ] Add a bounded history of safe poses, sampled only with adequate clearance over
  the full hull/appendages, no obstacle overlap, acceptable heel/trim, no grounding
  and no active tether. Require a stable safe interval, not one clear frame.
- [ ] Revalidate candidates against current loading/draft, water level and obstacle
  state at recovery time. Provide per-scenario recovery locations suitable for every
  permitted vessel; a loaded 507B must not reuse a fast-craft-only spot.
- [ ] Present destination and consequences before activation. R opens the same recovery
  interaction instead of silently invoking a separate reset path; retain internal reset
  APIs required by tests and builders.
- [ ] Preserve current cargo, stores, ballast and mission progress/score. Stop transfer,
  neutralize propulsion/rudder/thruster and velocities, release/retract tackle safely,
  clear active ground contact and explicitly clear immobilizing grounding damage as
  recovery assistance. Do not silently reset the mission or overwrite the manual save.
- [ ] Apply the pose through the existing Rigidbody restore boundary while simulation
  is suspended. Refresh current-zone membership, grounding contacts, camera, wake,
  map track and audio restore markers so old state causes no impulses or artifacts.
- [ ] Extend VoyageSave with optional validated safe-pose data and legacy fallback.
  Keep atomic saves and backup behavior. Missing/stale checkpoints use validated
  scenario fallback; never blindly teleport into a bank or another object.
- [ ] Test repeated hard grounding, berth wedging, active anchors/lines, no history,
  changed load/water, old saves, cancel and recovery-save-load. Verify forward and
  astern movement after recovery. If all fallback locations fail, offer restarting the
  passage with explicit consequences while preserving the manual save.

Acceptance: one steering mistake cannot permanently block play, and recovery does
not erase inventory or unrelated passage progress. Normal sailing remains force-driven.

## Part 8: cargo identity and visible loaded holds

Dependencies: Parts 1-2, 6-7 for final visuals and save compatibility.

- [ ] Reproduce current hold visibility at empty/partial/full load and inspect the
  installed 507B HoldInteriorVisual and hatch opening. Retain its mass-driven behavior.
- [ ] Add stable cargo ids to existing compartments/loading/save state with validation
  and explicit legacy nominal-bulk fallback. Initial commodities: sulphur, steel channel
  sections and grain. Do not silently relabel old nominal cargo as one of these.
- [ ] Allow commodity selection only for empty compatible holds at the existing loading
  interface; clearly explain hatch/speed/transfer restrictions. Preserve actual mass
  through save/load and recovery; never convert loaded cargo by changing a visual dropdown.
- [ ] Define documented estimated bulk densities, stowage/packing volume, allowed hold
  compatibility, materials and visual profiles. Respect both volume and shared deadweight
  limits; avoid silently drawing less cargo than the inventory claims.
- [ ] Reuse HoldInteriorVisual for sulphur/grain heaps with distinct appropriate materials;
  render steel as recognizable low-poly channel bundles, not heaps or arbitrary boxes.
  Match extent and amount to real inventory; zero mass yields an empty visible hold.
- [ ] Keep mass/CG/inertia owned by LoadingModel. Reconcile any new stowage geometry
  assumptions with compartment mass properties; visuals add no duplicate mass/colliders.
- [ ] Share meshes/materials, cache geometry and update only when cargo changes; use
  suitable LOD/instancing for bundles and avoid one object per grain or channel.
- [ ] Restrict visible dry cargo to ships with actual supported open holds. Passenger
  craft and tanker must not receive fabricated cargo displays. Other dry-cargo vessels
  require authored hold definitions and loading support before enabling the feature.
- [ ] Test all commodities empty/partial/full, mixed holds, load/unload transitions,
  hatch motion/clearance, legacy/new saves, over-volume rejection and recovery. Profile
  allocations, draw calls where available, geometry and frame cost in the built player.

Acceptance: visible type/quantity agrees with inventory, empty means empty, loading
physics stays consistent, no clipping into covers or hidden overfill.

## Part 9: integrated acceptance and final documentation

Dependencies: Parts 1-8.

- [ ] Run clean compilation, relevant full EditMode/PlayMode suites, menu/save-load
  smoke, all-vessel shakedown and numerical trials; compare to the recorded baseline.
- [ ] Perform manual Windows player sessions: camera orbit/zoom, ahead/astern and
  independent engines, steering, map navigation, buoy/quay/shore contact, hard grounding,
  recovery and continued passage, cargo transfer and visibility.
- [ ] Exercise all three scenes with allowed vessels, all four languages, screen/scale
  matrix, 1x/2x/4x, pause/focus loss, old/new saves and a fresh settings profile.
- [ ] Check frame-time tails, cold transitions, memory and allocations. Keep the known
  cold-path stall issue separate unless evidence attributes a regression to this work.
  Do not claim RTX 3060 performance from the available RTX 4090 workstation.
- [ ] Update README.md and README.ru.md with matching shipped behavior, controls,
  settings, recovery consequences, cargo, limitations and links. Update OperatorGuide,
  ProjectStatus, documentation index and project context with actual evidence paths.

Acceptance: record passed, failed and unavailable checks honestly. A fresh player build,
screenshots and manual handling assessment are required for final acceptance, not only
compile success. Keep all changes in the working tree; no commit or push.

## Resource needs

- UI icons: extend the established procedural HudTheme set; no external icon pack needed.
- Font: verify current coverage first; if insufficient, add one licensed font covering
  Cyrillic and Latin accents with provenance and deterministic import settings.
- Cargo: author reusable sulphur and grain surface materials and a correctly shaped
  low-poly steel channel/bundle mesh. Record estimated section sizes and packing. These
  can be procedural/authored locally; no unrelated downloaded prop substitutes.
- Bow: no new vessel model is assumed necessary. Fix the demonstrated rendering or
  authored hull issue in its existing source and integrator.
- Buoys: reuse current detailed visuals; add matching physical shapes and parameter data.
- No asset acquisition is required for this planning stage. Any external reference used
  later must be checked for provenance/license; physical estimates remain labelled.

## Execution ledger

| Part | Status | Verification |
|---|---|---|
| 1 | Complete | [Review and evidence](UsabilityPart1Review.md) |
| 2 | Complete | [Review and evidence](UsabilityPart2Review.md) |
| 3 | Implemented; human handling assessment pending | [Review and evidence](UsabilityPart3Review.md) |
| 4 | Complete | [Review and evidence](UsabilityPart4Review.md) |
| 5 | Complete | [Review and evidence](UsabilityPart5Review.md) |
| 6 | Complete | [Review and evidence](UsabilityPart6Review.md) |
| 7 | Not started | Pending |
| 8 | Not started | Pending |
| 9 | Not started | Pending |
