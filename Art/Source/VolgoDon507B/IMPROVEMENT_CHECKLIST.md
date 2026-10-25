# Volgo-Don Project 507B Improvement Checklist

Status: Parts 1-8 implemented. Refinement Parts 9-16 implemented and Part 17 completed with documented limits on 2026-10-25.
See [REFINEMENT_PLAN.md](REFINEMENT_PLAN.md), [PART17_REVIEW.md](PART17_REVIEW.md) and
[PLAYER_PERFORMANCE.md](PLAYER_PERFORMANCE.md). Target-hardware acceptance remains open.
Created: 2026-10-21.

## How to execute this document

The owner will request individual numbered parts, for example "do part 1".
Execute only the requested part and its necessary checks. Do not automatically
continue into the next part. Read this file, `CLAUDE.md`, relevant source documents
and the latest project context before starting. Inspect the current working tree
and preserve unrelated work.

Each part must produce a usable result, not another plan. Update its checkboxes,
record changed paths, verification evidence, unresolved issues and the next part.
Mark a part complete only when its acceptance criteria are met. If a prerequisite
is missing, report it explicitly rather than silently skipping it.

Parts 1 and 2 form the first visual milestone: audit, editable Blender source,
first substantial appearance pass, candidate export and comparison images.
Later parts refine the vessel and connect its mechanisms to the simulator.

## Objective and constraints

Improve the existing ship into a visually recognizable, editable Project 507B
with credible materials, mechanisms and simulator behavior. This remains an
engineering and gameplay prototype, not validated maritime training software.

- Preserve the existing mesh wherever practical. Replacement requires recorded
  evidence that a specific component cannot reasonably be repaired.
- Preserve the original FBX, texture assets, Unity GUIDs and working integration.
  Export candidates to a new path and verify them before switching the prefab.
- Do not overwrite unsaved Blender work or unrelated open projects.
- Use the registered Blender MCP server. Start with `get_addon_status` and
  `get_scene_info`. If unavailable, report the exact error. The task allows
  Blender batch mode, but do not silently use it as a connection fallback;
  retain the established MCP-only workflow unless the owner explicitly selects
  a batch workflow. Never use PowerShell or raw sockets as a Blender bridge.
- Preserve meshes, UVs and material assignments during import and organization.
- Retain Rigidbody force/torque motion, existing controls, catalogue identity,
  prefab references, save compatibility and water integration.
- Do not replace the dynamic collision hull with a non-convex MeshCollider.
- Keep vessel parameters configurable in physical units. Label estimates and
  do not invent measured hydrodynamic coefficients.
- No automatic commit, push, destructive cleanup or whole-game rewrite.
- Photographs are references, not permission to copy pixels into game textures.
  Record provenance and licenses for any incorporated third-party resources.

## Current repository baseline

| Asset or system | Location / known state |
|---|---|
| Original model | `Assets/ShipSimulator/Models/VolgoDon507/volgo_don.fbx` |
| Existing textures and materials | `Assets/ShipSimulator/Models/VolgoDon507/` |
| Runtime prefab | `Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab` |
| Vessel parameters | `Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json` |
| Parameter sources | `Assets/ShipSimulator/Documentation/VolgoDon507B_Sources.md` |
| Model integration | `Assets/ShipSimulator/Scripts/Editor/VolgoDonModelIntegrator.cs` |
| Physics documentation | `Assets/ShipSimulator/Documentation/ShipSimulator_Physics.md` |
| Previous visual captures | `Logs/Vessels/volgodon-507b-*.png` and `Logs/Shakedown/` |
| Editable source | No existing Volgo-Don `.blend` identified in the previous audit |

The model originated in the earlier CRYENGINE project. It already includes hull,
aft accommodation, bridge, rails, lifeboats, covered cargo spaces and equipment.
The integrator converts source X-forward/Z-up to Unity Z-forward/Y-up, scales
length to 138.3 m and aligns the keel for 3.53 m loaded draft. Previous context
reports approximately 16.4 m visual beam; measure it rather than stretching blindly.
Saved previews show angular superstructure shading, but its cause has not been
isolated. Existing color/AO textures are reused as URP base maps; the inspected
hull material has no normal or metallic/smoothness texture.

## Sources and variant decision

Primary reference: [archival technical document](https://russrivership.ru/public/files/doc257.pdf).
The URL was accessible on 2026-10-21 and the PDF contains nine pages. Its extracted
text is insufficient for engineering transcription. Part 1 must visually read
the scans; no dimensions or mechanisms below are considered newly verified by
this planning step.

User-supplied reading guide, using PDF page numbers starting at 1:

| Pages | Inspection purpose |
|---|---|
| 1 | General arrangement: profile, decks, holds and superstructure |
| 2 | Hull lines, reportedly labelled Project 507; establish applicability to 507B |
| 3-4 | Dimensions, displacement, stability and cargo openings |
| 5 | Engines, propellers and generators |
| 6-7 | Fuel, ballast, bilge, steering and bow thruster systems |
| 8 | Anchoring, mooring and lifesaving equipment |
| 9 | Vessel list and clues for matching the documented configuration |

Additional references to inspect and record with access dates:

- [FleetPhoto Project 507B](https://fleetphoto.ru/projects/1697/)
- [FleetPhoto vessel 4152](https://fleetphoto.ru/vessel/4152/)
- [FleetPhoto vessel 12387](https://fleetphoto.ru/vessel/12387/)
- [Volgaflot commercial fleet](https://www.volgaflot.com/commerce/)
- [AL Shipping Volgo-Don type](https://alshipping.ru/type-volgo-don/)

Working hypothesis: classic covered-hold 507B with two principal cargo holds.
This is not yet a confirmed identification of the FBX. Count holds separately
from hatch-cover panels. Compare the source with dated vessel photographs and
record a specific reference vessel or a documented common configuration.
Do not mix early open-bunker vessels, four-hold arrangements or later conversions.
If evidence contradicts the hypothesis, document the supported variant before
altering geometry. Do not force an incompatible model into this configuration.

## Parameter and uncertainty ledger

These are requested reference targets, not universal particulars of every 507B.
Part 1 must add exact source pages, configuration and confidence to the vessel
sources document. Preserve distinctions between overall and moulded dimensions,
loaded and light conditions, and published maximum and service speeds.

| Item | Requested reference target | Current state / verification needed |
|---|---|---|
| Overall length / beam | 138.3 / 16.7 m | JSON agrees; measure visual beam |
| Moulded depth | 5.5 m | Verify baseline and deck datum |
| Loaded draft / displacement | About 3.53 m / 6750 t | JSON uses these values |
| Deadweight | About 5000 t | Distinguish deadweight from cargo-only mass |
| Engines | Two 6ChRN 36/45 diesels, 662 kW each | JSON agrees; verify operating data |
| Propellers | Two, about 1.8 m, four blades | JSON diameter is 2.1 m, explicitly estimated |
| Steering | Two steerable nozzles | Current model uses generic rudder parameters; requires force-model review |
| Full speed | About 20-21 km/h | JSON limit is 5.14444 m/s, 10 kn; establish loading condition |
| Endurance | 15 days | Confirm applicability; no gameplay requirement implied |
| Bow thruster | Reported in archive | JSON fitted, 160 kW and 1 m tunnel are estimates |
| Cargo arrangement | Two principal covered holds | Must be established from geometry and sources |
| Cover mechanism | Hydraulic lifting and winch movement reported | Read actual sequence, tracks, travel and stowage |
| Hydrostatics and stability | Archive may provide particulars | Do not infer full curves or loading limits from isolated values |

Unresolved until supported: shaft RPM/pitch/handedness, nozzle angles and response,
thrust curves, cargo and ballast distribution, tank capacities, CG/inertia changes,
resistance derivatives, stopping and turning performance, exact hatch kinematics,
equipment fit and livery date. Record estimates with their rationale and tuning
range; do not silently promote them to verified facts.

## Part 1: Reference, geometry and Unity audit

Dependencies: none. Status: complete, 2026-10-21.

- [x] Inspect all nine archive pages; record legible values with page references
  and note unreadable or contradictory details. Record provenance and a checksum
  if a local reference copy is retained; do not assume redistribution rights.
- [x] Identify the FBX variant against arrangement drawings and dated photos.
- [x] Inventory mesh/object counts, triangles, UV channels, materials, textures,
  hierarchy, bounds, transforms, pivots, normals, open boundaries and overlaps.
  Distinguish intentional openings and thin surfaces from defects.
- [x] Capture the unchanged model from front, side, top, stern and perspective.
- [x] Record original asset checksums and a reversible candidate-export strategy.
- [x] Inspect prefab root, Rigidbody, collision hull, `VesselLayout`, controls,
  model integrator, builders, lights, water/wake and persistence contracts.
- [x] Audit existing `ShipPhysicsController`, `ManoeuvringModel`, `EngineShaft`,
  `PropellerModel`, `RudderModel`, `BowThrusterModel` and `HydrostaticsModel`.
  Classify each requested feature as existing, incomplete or missing.
- [x] Write `AUDIT.md` and `MODEL_PROVENANCE.md` beside this file; update
  `VolgoDon507B_Sources.md` with verified evidence and unresolved conflicts.

Acceptance: evidence-backed variant decision, measured baseline, issue list with
priorities, architecture map and before images. Existing assets remain unchanged.
Do not claim that merely rendering the model validates topology or real accuracy.

## Part 2: Editable Blender source and first appearance pass

Dependencies: Part 1. Status: complete, 2026-10-22.

- [x] Import the existing FBX into an isolated Blender scene and preserve editable
  meshes, UVs, textures and assignments. Save `VolgoDon507B.blend` in this folder.
- [x] Organize hull, decks, holds, covers, accommodation, wheelhouse, propulsion,
  steering, anchoring, mooring and lifesaving assemblies into named collections.
- [x] Define source axes, metre units, origin, keel, design waterline and export
  conversion in `README.md`. Avoid applying the legacy axis conversion twice.
- [x] Repair demonstrated normal/smoothing issues while preserving deliberate
  hard edges. Investigate baked shading before changing topology.
- [x] Correct the highest-priority drawing-backed silhouette or proportion errors.
  Keep a component-level before/after record rather than replacing the whole ship.
- [x] Perform an initial material pass with distinct painted steel, deck coating,
  superstructure, glazing and metal responses compatible with the current URP.
- [x] Pack resources or use valid relative paths; reopen the `.blend` independently
  to verify editable geometry and no missing textures.
- [x] Export a separate candidate to
  `Assets/ShipSimulator/Models/VolgoDon507B_Improved/VolgoDon507B.fbx`.
  Retain the original FBX and active prefab until candidate validation.
- [x] Import and inspect the candidate in Unity without switching the live prefab;
  verify scale, orientation, waterline, material slots and shading.
- [x] Save front, side, top and perspective comparisons under `Previews/` and list
  remaining discrepancies in `AUDIT.md`.

Acceptance: real independently opening `.blend`, new candidate FBX, substantial
documented fixes, reference comparisons and screenshots. Report exact paths,
remaining uncertainties and the next functional stage. No claims of a finished
vessel or completed physics upgrade at this milestone.

## Part 3: Detailed geometry and final PBR materials

Dependencies: Part 2. Status: complete, 2026-10-22. See PART3_REVIEW.md for
retained approximations, atlas density and the source-quality material scope.

- [x] Compare bow/stern sections and underwater hull with applicable lines plans.
- [x] Refine aft accommodation height, bridge windows, hatch layout, funnels,
  masts, antennas, light positions, railings and cargo-deck passage clearances.
- [x] Check anchors, mooring gear, lifeboats and deck equipment against the variant.
- [x] Correct propeller and steerable-nozzle geometry using verified dimensions;
  mark unavailable sections, pitch and internal construction as approximations.
- [x] Preserve useful UVs; repair distortion and establish appropriate texel density.
- [x] Produce original or properly licensed base color, normal and surface-response
  maps. Convert roughness to Unity smoothness correctly and document packing.
- [x] Add restrained wear tied to seams, drainage, foot traffic and waterline,
  rather than uniform random noise. Keep painted steel mostly dielectric.
- [x] Check texture color spaces, normal convention, glazing and lighting in both
  Blender and Unity. Record texture budgets and resource provenance.

Acceptance: updated candidate and reference comparisons; no missing maps, visible
normal artifacts or unsupported modern equipment. Record approximated details.

## Part 4: Movable geometry and mechanical motion

Dependencies: Part 3. Status: complete, 2026-10-22. See PART4_REVIEW.md.

- [x] Separate port/starboard propellers and nozzles with documented shaft and
  steering axes, pivots, names and travel limits.
- [x] Separate hatch panels and required lifting/translation mechanisms. Establish
  the archive-supported lift, travel and stow sequence; do not substitute hinges.
- [x] Separate anchors and visible operating gear where a gameplay function exists.
- [x] Create a mechanism manifest containing object paths, axes, units, limits,
  states and animation/control bindings. Keep structural parent transforms stable.
- [x] Demonstrate each mechanism through its full range and check intersections.
  Preserve movement-capable objects through export and future LOD generation.

Acceptance: candidate reimport retains pivots and independent mechanisms; motion
previews demonstrate plausible travel. Unknown mechanisms remain explicitly
deferred, not presented as source-accurate animations.

Completion evidence: PART4_REVIEW.md, mechanisms.json, part4-validation.json and
Blender/Unity movement captures. Updated source and candidate have 15 controls,
49 meshes and 53,130 triangles. Source reopened and validated; 541 hatch frames
and 720 propeller/nozzle configurations checked. Unity final review passed, exit 0,
in Logs/VolgoDon507B/part4-candidate-final.log. All 200 EditMode tests passed in
TestResults/check-EditMode-20261022-183641-410.xml. Full anchor payout and detailed
hydraulics remain explicitly deferred. Next is Part 5, only when requested.

## Part 5: Unity candidate integration and visual controls

Dependencies: Part 4. Status: complete, 2026-10-22. See PART5_REVIEW.md.

- [x] Extend the existing integrator/builder workflow to support the new export
  while preserving the old asset as a usable fallback.
- [x] Replace only the validated visual branch; preserve root components, GUIDs,
  catalogue references, collision behavior and physics parameter values.
- [x] Verify repeated integration produces one visual branch and stable bindings.
- [x] Animate propellers from actual shaft state and nozzles from steering state,
  not from arbitrary visual timers. Correct port/starboard direction conventions.
- [x] Add hatch and justified anchor controls through the existing command/UI
  architecture. Animate mechanisms, never the simulated vessel root.
- [x] Reuse navigation-light systems; verify positions, sectors and operating modes.
- [x] Add named audio attachment points for engines, water, propellers and machinery.
  Sound assets are optional until appropriately sourced.
- [x] Extend save/load for new persistent mechanism state with backward-compatible
  defaults; retain vessel switching, HUD and camera behavior.

Acceptance: compile and relevant tests pass; candidate works in the existing scene;
mechanisms, controls, lights and save/load are demonstrated without duplicated rigs.

Completion evidence: PART5_REVIEW.md, installed existing prefab, filtered hatch/
anchor clips, runtime controls, optional validated save state and scene captures.
Repeated integration preserves Rigidbody, physics and original collider identities.
EditMode 209/209 and PlayMode 40/40 passed; five-vessel shakedown passed. Menu smoke
checks machinery HUD commands and cross-scene/vessel save restoration. Original
FBX, textures, vessel JSON and catalogue references remain unchanged. Only the
playable prefab changes among the 47 Part 1 baseline resources. Part 6 is next,
only when requested; force-model and calibration changes are not included here.

## Part 6: Propulsion, steerable nozzles and handling

Dependencies: Parts 1 and 5. Status: complete, 2026-10-22. See PART6_REVIEW.md.

- [x] Preserve or complete independent engine commands using the existing shaft
  models, including acceleration, reversal delays and differential thrust.
- [x] Review steerable-nozzle force representation. Model thrust deflection and
  speed-dependent steering without double-counting generic rudder forces.
- [x] Reconcile diameter, RPM, power and speed with the selected configuration;
  changing diameter alone invalidates the previous thrust calibration.
- [x] Retain physically meaningful inertia and long acceleration/stopping response.
- [x] Verify bow-thruster applicability and document estimated power, geometry,
  immersion and forward-speed effectiveness where sources are incomplete.
- [x] Run repeatable ahead/astern, differential thrust, low/high-speed turns,
  zigzag and stopping trials with specified load, depth and environmental conditions.
- [x] Compare with trustworthy trial data if available; otherwise report internal
  consistency and approximate calibration, not proven full-scale accuracy.

Acceptance: parameter provenance, force-model regression tests, before/after trial
results and no regressions for other vessels or the KVLCC2 benchmark.

Evidence: 217/217 EditMode, 40/40 PlayMode, five-vessel runtime shakedown PASS.
Other four playable vessels and KVLCC2 match every stored pre-change trial metric.
Full speed is 19.8 km/h; stopping and turning remain approximate and differ
substantially from archive figures. These discrepancies, the assumed shaft ratio
and the retained loading-condition conflict are explicit in PART6_REVIEW.md.

## Part 7: Cargo, ballast and loading-dependent behavior

Dependencies: Part 6. Status: complete, 2026-10-23. See PART7_REVIEW.md.

- [x] Separate lightship, cargo, consumables and ballast in the mass accounting.
  Do not count deadweight as cargo in addition to ballast and fuel.
- [x] Derive a configurable loading approximation from supported hydrostatics and
  compartment geometry. Label missing tank, CG and stability information.
- [x] Update total mass, CG, inertia, displacement and equilibrium draft together;
  retain grounding, under-keel clearance and propeller/thruster immersion behavior.
- [x] Add loading/ballast controls and persistence through existing project contracts.
  Keep physically plausible rates and mass conservation; avoid cosmetic vertical
  Transform offsets pretending to change buoyancy.
- [x] Check empty, partial and loaded cases, asymmetric loads if supported, and
  save/load continuity. Do not claim certified stability or safe loading limits.

Acceptance: repeatable loading tests show consistent mass, draft and handling;
unknown hydrostatic behavior is documented and all-vessel regressions pass.

Evidence: 228/228 EditMode, 47/47 PlayMode, loading review, menu/save smoke and
five-vessel shakedown PASS. Longitudinal asymmetry produces physical trim;
transverse asymmetry/free-surface stability are unsupported. Archive loading
conflict and equivalent compartment estimates remain explicit in PART7_REVIEW.md.

## Part 8: LOD, performance and final quality gate

Dependencies: Parts 3-7. Status: implemented and host-verified, 2026-10-23.
Target-machine built-player GPU acceptance remains open. See `PART8_REVIEW.md`.

- [x] Establish measured triangle, draw-call, texture-memory and frame-time budgets.
  Generate LODs preserving silhouette, material boundaries and moving components.
- [x] Check LOD transitions, distant railings, glass, lights and shadow behavior.
- [x] Review collision approximations against the updated hull without replacing
  the dynamic compound hull with a non-convex mesh.
- [x] Reopen source, regenerate export and reintegrate to prove reproducibility.
- [x] Verify original fallback checksums and candidate resource completeness.
- [x] Run compile checks, relevant EditMode/PlayMode tests, full vessel shakedown,
  manoeuvring trials and save/load/menu checks appropriate to the changed systems.
- [x] Capture front, side, top, perspective, underwater, bridge and mechanism views,
  including loaded/light conditions and day/night navigation lights.
- [x] Finish source README, provenance, parameter sources and discrepancy ledger.
  Update project context with exact verification evidence and remaining limitations.

Acceptance: reproducible editable source and verified Unity integration, no missing
textures or inverted visible surfaces, functioning mechanisms, acceptable measured
performance, preserved controls and an honest final list of remaining differences.

- [ ] External acceptance: measure a 1080p built player on target hardware.
  RTX 4090 built-player measurements are recorded in PLAYER_PERFORMANCE.md, not a target GPU certification.

## Completion record template

For each requested part append:

```text
Part and date:
Status: complete / partial / blocked
Changed files and produced artifacts:
Source evidence and variant decisions:
Geometry/material/system changes:
Checks run and exact results:
Screenshots or reports:
Remaining discrepancies and estimates:
Next part and prerequisites:
```

Planning verification: repository instructions, existing source documentation,
integrator and vessel JSON reviewed; primary PDF availability/page count checked.
The original planning step produced no implementation artifacts. See the completion
record below for subsequent work.


## Part 1 completion record, 2026-10-21

Status: complete.

Artifacts: `AUDIT.md`, `MODEL_PROVENANCE.md`, `audit-baseline.json`,
`baseline-checksums.json`, eight baseline/diagnostic images in `Previews/`, and
the archive addendum in `VolgoDon507B_Sources.md`.

Evidence: all nine archive pages visually inspected; existing model imported into
an isolated Blender scene through registered MCP; counts, transforms, UVs and
resources inventoried. Classic two-covered-hold configuration retained; exact
vessel identity remains unknown. Matched clay/textured views isolate substantial
texture/UV-dependent patchwork. Static Unity architecture review completed.

Original assets remain unchanged. No runtime code, physics parameters, production
Blender source or candidate FBX was changed or produced. Runtime tests were not
run for this documentation-only part. See AUDIT.md for inspection limits.

Next: Part 2, create the independently editable source, diagnose atlas mapping,
repair demonstrated defects and inspect a separate candidate export.


## Volgo-Don 507B Part 2, 2026-10-22

Part 2 complete. Do not start Part 3 until requested. Source and comparison views:
`Art/Source/VolgoDon507B/README.md`. Independently reopened editable source:
`Art/Source/VolgoDon507B/VolgoDon507B.blend`. Candidate export:
`Assets/ShipSimulator/Models/VolgoDon507B_Improved/VolgoDon507B.fbx`.

Corrected inverted atlas V coordinates, preserved original UVs in channel 2,
removed 22 zero-area triangles, retained custom normals and hard edges. Source
has 18 local meshes, nine packed images and 42,299 triangles. Overall length/beam
138.3/16.7 m, hull keel -3.53 m. Dedicated opaque first-pass glazing covers 30
bridge panes; URP materials distinguish paint, decks and propeller metal. Legacy
radar animation disabled to preserve normalized placement. Registered Blender
MCP used throughout. Source validation and Unity candidate review PASS, exit 0:
`Logs/VolgoDon507B/candidate-final.log`. All 200 EditMode tests passed in
`TestResults/check-EditMode-20261022-072713-430.xml`. All 47 original resource
hashes match the baseline. New candidate resources have Unity metadata.

The visual candidate prefab is separate; active prefab, original FBX, physics
and catalogue remain unchanged. No runtime shakedown for this uninstalled visual.
Classic two-covered-hold configuration retained, exact vessel identity unknown.
Hull sections, coarse three-bladed propellers, faceted nozzles, detailed PBR maps
and transparent glazing remain Part 3. Anchoring/mooring islands and covers are
editable within existing meshes; independent mechanisms/pivots remain Part 4.
README and AUDIT document limitations and before/after images. No commit or push.

Cold-path performance limitation: a fresh-process LOD repeat reproduced two >50 ms frames
(maximum 234.93 ms); the next two identical repeats peaked at 31.97/26.46 ms.
First-use preparation is a hypothesis, not a diagnosed cause. Stall elimination
remains open; see PLAYER_PERFORMANCE.md. Final full-run PASS does not erase this finding.
