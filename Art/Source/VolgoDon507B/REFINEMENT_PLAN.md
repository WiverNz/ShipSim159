# Volgo-Don 507B: remaining fidelity work

Created: 2026-10-23. Parts 9-13 complete; Parts 14-17 not started.

This continues Parts 1-8 of [IMPROVEMENT_CHECKLIST.md](IMPROVEMENT_CHECKLIST.md).
The baseline and known discrepancies are recorded in [PART8_REVIEW.md](PART8_REVIEW.md).
Execute one numbered part when requested, then record its result before proceeding.
For example, "do Part 9" refers to the first part below. Creating this plan does
not authorize starting any implementation part.

## Scope and invariants

Improve the existing classic two-covered-hold vessel, not a replacement ship.
Preserve the editable source, original FBX fallback, materials/UVs, mechanism
hierarchy, normal export/integration workflow, prefab references and existing controls.
Use registered Blender MCP only for Blender work; report connection errors without
shell or socket fallbacks. Do not commit or push automatically.

Continue using the existing force-driven Rigidbody architecture. New systems must
extend its parameter, command, save and reset contracts. Optional features must not
change the other four playable vessels or the KVLCC2 benchmark by default.
Never move the simulated ship through Transform animation.

Current dimensions and loading baseline are 138.3 m length, 16.7 m beam, 6750 t
loaded displacement and 3.53 m draft. These are the project's selected baseline,
not universal values for all 507B variants. Resolve conflicting evidence explicitly.
Do not tune geometry or forces merely to conceal an inconsistent loading condition.

Every numerical input must be tagged as sourced, derived or estimated, with units,
applicable variant and uncertainty. Missing evidence does not justify invented
precision. A completed prototype feature is not validated maritime training.

## Execution order

| Part | Deliverable | Dependencies | Status |
|---|---|---|---|
| 9 | Reference and variant decision package | Existing Parts 1-8 | Complete, 2026-10-23 |
| 10 | Hull sections and corrected editable geometry | 9 | Complete with documented trace discrepancy, 2026-10-23 |
| 11 | Section-based hydrostatics and loading response | 10 | Complete, 2026-10-23; estimated lightship LCG and no free-surface correction |
| 12 | Propulsion, stopping and steering calibration | 9, 11 | Complete, 2026-10-24; audit with no refit, stop and turning published as envelopes; open-hold water and refloating fixes |
| 13 | Evidence-based machinery rates and detail corrections | 9; reconcile with 12 | Complete, 2026-10-24; rates derived or estimated within archive limits, visual anchors only |
| 14 | Anchor holding and mooring prototype | 11, 12, 13 | Not started |
| 15 | Licensed machinery and water audio | 12, 13 | Not started |
| 16 | Improved contact geometry | 10, 11; retest 14 | Not started |
| 17 | Integrated fidelity acceptance and discrepancy update | 9-16 | Not started |

Part 9 may retain a representative classic configuration if no specific ship can
be identified. Document that decision; do not block all work on a speculative name.
Parts 13, 15 and 16 can be scheduled separately once their dependencies are satisfied.
Built-player performance acceptance remains the separate open item from Part 8.

## Part 9: references, variant and measurement baseline

Result: [REFERENCE_DECISIONS.md](REFERENCE_DECISIONS.md), geometry and numerical
baseline JSON files, and read-only capture scripts. See its completion record.

- [x] Revisit the archived documentation and existing source notes. Record page,
  figure, units, date, variant, confidence and intended use for every adopted value.
- [x] Distinguish the page-2 project-507 lines plan from confirmed 507B particulars.
  Establish whether and how its sections can apply to the selected vessel.
- [x] Compare the existing model with identified classic covered-hold photographs;
  separate original fittings from later modifications. Do not use photographs as
  game textures without appropriate rights.
- [x] Reconcile length definitions, displacement, draft, cargo/deadweight and the
  conditions/procedure behind archived stopping and turning values.
- [x] Capture current geometry and numerical trial baselines before any edits.

Deliverables: `REFERENCE_DECISIONS.md`, a source/measurement table and reference
overlays or links with usage notes. Keep the baseline evidence from Parts 1-8.
Acceptance: a documented target configuration, coordinate/datum conventions and
an explicit list of usable, conflicting and unavailable evidence. Set tolerances
for later comparisons from source resolution and uncertainty, before fitting.

## Part 10: hull reconstruction from sections

Result: [PART10_REVIEW.md](PART10_REVIEW.md). Geometry and integration checks pass.
The maximum 0.743 m family-trace residual is explicitly unresolved, not accepted
as falling within the three-pixel tracing tolerance. Waterlines and buttocks are
derived from the reconstructed surface, not independently traced offsets.

- [x] Digitize traceable stations, waterlines and buttocks into a versioned table
  in metres. Preserve original drawing coordinates and conversion parameters.
- [x] Check symmetry, station order, scan distortion, keel/deck/waterline datums
  and consistency with the accepted length and beam.
- [x] Fit fair sections, bow and stern surfaces to the retained hull. Preserve
  working geometry wherever compatible; document any locally replaced mesh.
- [x] Create a closed hydrostatic surface separate from cosmetic openings and
  fittings. Verify winding, positive volume, manifoldness and intersections.
- [x] Regenerate UVs only where necessary, preserve material continuity, update
  LODs and recheck propeller/nozzle clearances through the steering range.

Deliverables: section data, updated `.blend` and candidate FBXs, comparison
overlays, volume/error report and front/side/top/underwater views.
Acceptance: errors satisfy Part 9 tolerances or are explicitly unresolved; no
arbitrary scaling to force displacement agreement. Existing mechanisms and Unity
integration still work. Update fixed geometry-count assertions with reviewed new
baselines, not by removing the checks.

## Part 11: hydrostatics, cargo and ballast

Result: [PART11_REVIEW.md](PART11_REVIEW.md). Column integration agrees with exact
mesh clipping within 0.004% volume and 0.005 m LCB; full load floats at 3.494 m on
even keel after a documented lightship-LCG re-estimate. Free surface is not applied.

- [x] Derive displacement, buoyancy centre and waterplane properties across
  the useful draft range from the approved section data.
- [x] Replace the 507B wall-sided approximation through the existing hydrostatic
  interface, retaining the current implementation for other vessels.
- [x] Resolve draft, trim and small-angle roll response from actual loading;
  keep mass, CG and inertia accounting consistent with the existing inventory.
- [x] Model free-surface corrections only for documented tank groups/geometries.
  Label surrogate tanks and their valid range; do not claim certified stability.
- [x] Define out-of-range handling for dry sections, extreme trim, overload and
  unsupported loading conditions. Avoid extrapolated negative buoyancy or NaNs.
- [x] Profile loading updates and allocations with a supported profiler; remove
  unnecessary repeated construction only after measurement.

Deliverables: hydrostatic tables with derivation, optional vessel parameters,
implementation and load-case tests/report.
Acceptance: volume integration and interpolated results agree within declared
tolerances; full, partial, ballast and longitudinally biased cases settle stably.
Mass conservation, pause, save/load and shaft-state continuity remain correct.
Unsupported stability effects remain visible in the documentation.

## Part 12: propulsion and manoeuvring calibration

Result: [PART12_REVIEW.md](PART12_REVIEW.md). Loaded speed -1.2% and the unfitted
ballast check -0.05% against the archive; no coefficient refitted. The 716 m stop
is explained by unstated archive conditions, not fitted. Separately requested fixes:
river water no longer shows in open holds (stencil mask, nominal cargo heaps) and
refloating uses real-bottom contact points and non-creeping Coulomb friction.

- [x] Verify engine/shaft RPM relationship, reversal delay, propeller handedness,
  thrust locations, nozzle angle limits and applicable thruster configuration.
- [x] Establish identical initial speed, loading, water depth and manoeuvre
  procedure before comparing the simulator with any published trial.
- [x] Calibrate steady resistance/thrust first, acceleration and reversal next,
  then stopping, turning, zigzag and differential-thrust response.
- [x] Use documented parameter bounds and sensitivity sweeps. Keep separate
  calibration cases and validation cases; avoid fitting one stopping number at
  the expense of all other behaviour.
- [x] Repeat deep/shallow-water and loaded/ballast cases, timestep convergence
  checks and all other-vessel regression trials.

Deliverables: before/after trial tables, parameter provenance and calibration
script/configuration. Explain the current roughly 719 m stopping discrepancy.
Acceptance: improvements are quantified against comparable evidence. Where trials
are missing, publish an estimated response envelope rather than a false accuracy
claim. Preserve independent engines, steering, thruster and save compatibility.

## Part 13: machinery timing and remaining fittings

Result: [PART13_REVIEW.md](PART13_REVIEW.md). Real-time hatch, anchor and steering
rates derived from the archive hydraulics, winch and windlass data in the
`operating_rates` block of `mechanisms.json`; windlass re-bound from the mooring
drums to the gypsies; forward outer cover lifts before it shifts; visual chain payout.

- [x] Resolve shaft reduction, hatch travel/lift sequence and operating rates,
  anchor payout limits and steering actuator rates from available evidence.
- [x] Separate real-time operating rates from accelerated preview animations.
- [x] Correct only supported fittings, pivots or clearances. Retain the archived
  hatch lift/translation mechanism rather than substituting hinged covers.
- [x] Verify partially completed operations, interruption/reversal, pause and
  save/load at each mechanism state, including every LOD.

Deliverables: updated mechanism manifest, clips/parameters and review captures.
Acceptance: visual and physical shaft/nozzle state agree; covers and anchors do
not jump or intersect their operating envelope. Unverified rates stay configurable
and labelled estimated, with no claim of exact vessel-specific timing.

## Part 14: anchors and mooring forces

- [ ] Define scope first: payout/retrieval, seabed contact, holding/dragging and
  mooring to explicit attachment points. Identify required bathymetry inputs.
- [ ] Add a bounded tension model through Rigidbody forces at attachment points;
  make chain/rope visualization follow the physical state.
- [ ] Distinguish slack, tension, dragging and overload. Treat seabed holding
  coefficients and line elasticity as estimated unless sourced.
- [ ] Add commands, interlocks, persistence and reset behaviour to existing APIs.
  Avoid duplicate force application through both collision and tension systems.
- [ ] Test slack-to-taut transitions, changing depth, current/wind loads,
  retrieval, excessive payout, save/load and timestep sensitivity.

Deliverables: optional anchoring/mooring subsystem, controls, tests and a small
reproducible demonstration scenario.
Acceptance: stable bounded forces, believable dragging when holding is exceeded,
and no teleporting or locking the vessel's Transform. This is a prototype holding
model, not an operational anchoring or mooring safety calculation.

## Part 15: audio production and integration

- [ ] Inventory existing emitters and define engine, shaft/propeller, water,
  hatch, windlass and line/chain events.
- [ ] Obtain usable recordings or create original assets. Record creator, URL,
  license, attribution and edits for each asset; do not extract unlicensed audio.
- [ ] Drive layers from actual RPM, load, immersion, speed and machinery state;
  use cross-fades, distance attenuation and interior/exterior balance.
- [ ] Pool one-shots and check pause, vessel switching, save/load and reset for
  duplicate loops or stale sources.

Deliverables: audio assets, license manifest, mixer setup and listening checklist.
Acceptance: audible state changes track the actual vessel; no clipping, missing
assets or persistent sounds after stopping/switching. Generic substitute recordings
must not be described as authentic 507B recordings.

## Part 16: collision fidelity

- [ ] Compare the current compound boxes with the corrected hull, especially
  the uncovered extremities, waterline and berth-contact areas.
- [ ] Improve contact using a small documented set of primitive or convex shapes.
  Do not install a non-convex MeshCollider on the dynamic vessel.
- [ ] Keep decorative fittings and propellers out of hull contact unless a
  concrete gameplay requirement justifies their collision cost.
- [ ] Test glancing bank/quay contact, bow/stern contact, grounding and moored
  contact under tension. Check jitter, tunnelling and artificial snagging.

Deliverables: builder-backed collision layout, diagnostic overlays and regression
scenarios. Acceptance: measured contact coverage improves while physics remains
stable. A walking character, climbable decks and interior navigation are outside
this plan; they require their own gameplay scope and separate collision layers.

## Part 17: integrated acceptance

- [ ] Reopen the final source, regenerate all exports and reinstall through the
  standard builders. Verify original fallback hashes and resource licenses.
- [ ] Run applicable EditMode/PlayMode suites, all-vessel shakedown, numerical
  trials, menu/save/load and new anchor/mooring/contact scenarios.
- [ ] Compare reference overlays and capture loaded/ballast, day/night, bridge,
  underwater and operating-mechanism views at all relevant LODs.
- [ ] Confirm no unintended regression in controls, loading, water effects,
  other vessels, materials or the Part 8 resource budgets.
- [ ] Update README, provenance, parameter sources, operator documentation and
  project context. Reconcile this plan with the final discrepancy ledger.

Acceptance: a reproducible source and integrated vessel with measured improvements
and honest unresolved items. Built-player GPU/frame-time/GC acceptance is still a
separate requirement: link its results if available, otherwise leave it unchecked.

## Completion record for each part

Record date, status (complete / partial / blocked), changed paths, produced
artifacts, evidence and estimates, exact tests/results, screenshots and remaining
discrepancies. Explain missing evidence and complete independent work where possible.
Do not mark a part complete solely because its code runs. Leave subsequent parts
unstarted until requested.
