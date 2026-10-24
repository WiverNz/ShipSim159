# Volgo-Don Project 507B: Sources and Data Status

## Part 15 audio, 2026-10-24

See `Art/Source/VolgoDon507B/PART15_REVIEW.md` and `Art/Source/VolgoDon507B/Audio/AUDIO_SOURCES.md`.
Equipment the sound represents, from the archive: 2 x 6ChRN 36/45 direct-reversing engines at
375 rpm, four-bladed propellers, EMN-1.5/95-1 hydraulic pump and two LE-61 cover winches, windlass
BE16-4, stern capstan ShER, electrohydraulic mooring winches LEGShA2-1. No recording of any of them
was found: every recording used is an analogue (other ferries and river vessels, a vehicle-ramp
hydraulic pack, a lighthouse motor, a ship's anchor chain), all CC0, and the slow-engine firing
rhythm, blade rate and parting snaps are original synthesis. Levels, distances, emitter positions
of the exhaust uptake, hull water and stern capstan, and the wheelhouse filter are estimated.

Equipment facts in the vessel JSON that the sound uses (physics ignores them):

| Field | Value | Status |
|---|---|---|
| `engine.cylinders`, `engine.strokesPerCycle` | 6, 4 | D: GOST designation 6ChRN (6 cylinders, Ch four-stroke) |
| `engine.directReversingAirStart` | true | D: R in 6ChRN (reversible) and archive p5 "reversible"; air start is the usual means, not stated |
| `engine.engineRpmPerShaftRpm` | 1.0 | D: archive p5, the only gearbox drives the shaft generator; not stated directly |
| `propeller.bladeCount` | 4 | S: four-bladed propellers (source item 5 above) |
| `groundTackle.windlassDrive` | electric | S: archive p8, BE16-4 and ShER with electric motors |
| `groundTackle.mooringWinchDrive` | electrohydraulic | S: archive p8, LEGShA2-1 electrohydraulic |

## Part 14 ground tackle, 2026-10-24

See `Art/Source/VolgoDon507B/PART14_REVIEW.md` for the full table; the values live in the
`groundTackle` block of `Data/Vessels/VolgoDon507B.json`. Sourced from archive p7-p8: Hall anchors
2 x 1750 kg bow and 1250 kg stern; chains 43 mm x 150 m and 37 mm x 75 m; windlass BE16-4 at
10 m/min and stern capstan ShER-1.5/3/2-1 at 18 m/min; two LEGShA2-1 mooring winches of 2.5 tf and
the capstan's 3 tf. Estimated: chain mass and stiffness (stud-less), grade 1 breaking load, brake
holding at 45 % of it, gypsy pulls from the chain size, Hall anchor holding ratios 2 / 3 / 1 x
submerged weight on silt / sand / rock, chain friction, setting, uplift, damping, every mooring rope
property, haul rates other than the capstan's, and the Gorodets demonstration quay and its dredged
pocket. Bollard and lead positions are derived from the model mesh. None of these is a measured
holding power or line load of a real ship.

## Part 13 machinery rates, 2026-10-24

See `Art/Source/VolgoDon507B/PART13_REVIEW.md`; the values live in the `operating_rates`
block of `Models/VolgoDon507B_Improved/mechanisms.json`. Archive p4: 16 vertical and 4
horizontal 7 t jacks at 100 kgf/cm2 (70 cm2 derived), pump 1.5 m3/h, two LE-61 winches 4 tf /
7 kW (0.13 m/s derived); hatch phases 141 / 34 / 174 / 16 s and traverse 371 s, derived or
estimated with the ranges recorded there. p5: G60 reversible at 375 rpm, the only gearbox
drives the shaft generator, so the 1:1 shaft ratio is derived. p7: steering 35 to 35 deg in
at most 30 s (model 28 s, estimated rate). p8: chains 43 mm x 150 m bow, 37 mm x 75 m stern;
windlass BE16-4 10 m/min, stern capstan 18 m/min (moved to the vessel JSON in Part 14). Let-go
speed 1 m/s and 1.72 m of chain per gypsy turn are estimates. None of these is a measured operating time of a specific ship.

## Part 12 propulsion, contact and hold data, 2026-10-24

See `Art/Source/VolgoDon507B/PART12_REVIEW.md`. No hydrodynamic coefficient was
refitted. Changed value: `loading.propellerAxisHeightAboveKeelM` 1.0 -> 1.35 m,
derived from the authored shaft axis (source landmarks -2.18 minus -3.53 m).
Archive comparison, matched to deep water: loaded speed 19.76 against 20.0 km/h
(calibration case), archive ballast 3828 t 21.19 against 21.2 km/h (independent,
not fitted), ballast mean draft 2.05 against 2.11 m. Crash stop 716 m / 285 s against
510 m / 210 s is not fitted: the archive omits approach speed, loading and depth;
the model gives 428 m from half ahead and 488 m in the ballast condition. Turning
462 m steady diameter against an undefined 320 m. Engine ratio, reversal timings,
astern curves and nozzle coefficients remain estimated.

Grounding soil values (estimated): section-contact modulus 0.5 / 1.5 / 20 MN/m3,
bearing 30 kPa / 150 kPa / 5 MPa and friction 0.25 / 0.35 / 1.0 for silt, sand and
rock. Friction follows the middle of the U.S. Navy Salvor's Handbook static ranges
(mud 0.2-0.3, sand 0.3-0.4, rock 0.8-1.5), read from a secondary copy.

Hold visual: clear hold space measured from the authored meshes, 2557 m3 forward and
2637 m3 aft to the closed covers (5194 m3, 18% under the archive 6370 m3, unresolved).
Nominal bulk cargo density 1600 kg/m3 (estimate; no historical cargo is modelled).

## Part 11 section hydrostatics, 2026-10-23

`hydrostatics.sectionTable` is derived (D) from the Part 10 closed hydrostatic surface,
itself a family-guided reconstruction with an unresolved 0.743 m trace residual and estimated
station mapping; it is not a 507B offset table. Agreement with exact mesh clipping is within
0.004% volume and 0.005 m LCB over 0.6-4.0 m drafts and +/-1.5 m trim (declared range).
The 6750 t prototype floats at 3.494 m; the archive's 6678 t at 3.50 m compares with 6762 m3
there (1.3% more, not fitted). Hold and ballast-group extents now follow the authored hold
meshes (D). The lightship LCG moved from +2.0 m to -21.9 m (E), re-estimated so the default
full load floats on even keel; no source gives it. No free-surface correction is applied:
tank subdivision is unavailable. The Part 7 table below lists the superseded centres. See
[Part 11 review](../../../../Art/Source/VolgoDon507B/PART11_REVIEW.md).

## Part 7 loading approximation, 2026-10-23

The enabled `loading` configuration now splits lightship, two cargo groups,
two ballast groups and consumables. Default masses are 1750 + 4800 + 0 + 200 t,
with cargo/ballast/stores sharing a 5000 t budget. The 200 t stores reservation,
centres, equivalent box dimensions and rates are estimates. The 2200 t ballast
capacity is inspired by the archive's ballasted operating condition, not a verified
tank-capacity sum. No individual-vessel loading booklet has been established.

Current mass, CG and diagonal inertia follow the inventory. The prior gyration
radii now approximate lightship; loaded inertias are derived by mass summation.
Mean draft still uses wall-sided calibrated prisms, with exaggerated stability
at very low draft and no free-surface correction. The archive's 6678 t / 3.5 m
507B condition remains distinct from the retained 6750 t / 3.53 m prototype.
See [Part 7 review](../../../../Art/Source/VolgoDon507B/PART7_REVIEW.md) for
coordinates, provenance, controls, save compatibility, trial results and limits.

## Part 6 handling configuration, 2026-10-22

The active JSON now uses the archived 1.8 m screws, steerable nozzles and
72 kW / 710 mm bow waterjet particulars. This section supersedes the historical
parameter table below. Full provenance, trial conditions, before/after results
and unresolved discrepancies are in
[PART6_REVIEW.md](../../../../Art/Source/VolgoDon507B/PART6_REVIEW.md).

The 375 rpm engine rating is published, but the main-shaft reduction ratio is
not established. Using 375 rpm as the shaft setpoint assumes direct drive;
it is not a measured propeller rating. Estimated open-water torque coefficients
are 0.8 times the former curves, with thrust curves unchanged. Residual resistance
C_R is calibrated to 0.0001005. Deep-water equilibrium is 341.9 rpm,
91.2% delivered-power load and 19.80 km/h. Existing bollard-pull and open-water
efficiency bounds pass without being relaxed.
Nozzle coefficients (unit jet deflection, 3.0544 m2 passive area, slope 3.0)
are estimates; the area is diameter times length from page 7. Axial/lateral
positions -64.65 / +/-3.6 m follow the authored model, not surveyed offsets.

The waterjet retains the existing actuator-disc force surrogate with 0.62 figure
of merit, 5 s ramp, 1 m/s speed-loss scale and zero high-speed floor. These are
not measured pump or jet curves. Position +60 m and axis 1.1 m above keel are
estimates. The original immersion ramp is conservative and will be reviewed
with loading in Part 7. The engine delays, inertia, hull derivatives and
6750 t / 3.53 m prototype loading condition are retained. Archive page 3 assigns
6678 t / 3.5 m to 507B; the current condition is not asserted to reproduce that trial.

Only ganged nozzle control is implemented; the archive also describes separate
control. Independent engine commands and existing actual-RPM mechanism visuals
remain active. The generic MMG rudder forces are bypassed for this vessel.

## Part 3 visual candidate, 2026-10-22

The separate improved candidate now uses four-bladed 1.8 m steel propellers,
left/right pitch 1.52/1.47 m and an expanded blade-area ratio target of 0.62
(archive page 5). Rounded ducts use the page 7 length 1.66 m and interpret the
1.84 m diameter as clear throat diameter. Sections, shaft alignment, handedness
and stabilizers remain authored approximations. A measured visual side-deck
height correction gives 5.5 m keel-to-side-deck depth at the retained 3.53 m draft.
Neither the playable model nor physics JSON was changed. The archive loading
conflicts below remain unresolved. Reference comparison, PBR provenance and
screenshots: `Art/Source/VolgoDon507B/PART3_REVIEW.md`.

## Archive audit addendum, 2026-10-21

The [nine-page archive](https://russrivership.ru/public/files/doc257.pdf) was
visually inspected. It combines projects 1565 and 507B: page 3 assigns the first
slash-separated value to 1565, the second to 507B. Do not silently transfer the
first value to this vessel. Existing JSON remains unchanged pending calibration.

| Page | Verified observation / implication |
|---|---|
| 1 | General arrangement; use for assembly placement, not precise offsets |
| 2 | Lines plan explicitly labelled 507, only family-level hull guidance |
| 3 | Paired loaded displacement 6750/6678 t and mean draft 3.53/3.5 m; conflicts with the current baseline |
| 3 | Loaded speed 20 km/h; ballasted 21.2 km/h; stopping and turning figures lack sufficient trial conditions for direct validation |
| 4 | Covers lift, overlap and translate by winch; no hinged opening |
| 5 | Two 1.8 m, four-bladed steel propellers; left/right pitch 1.52/1.47 m; engine rating 375 rpm is not automatically shaft RPM |
| 6 | Fuel and ballast compartment tables available for later loading work |
| 7 | Two steerable nozzles, 1.84 m diameter, 1.66 m long; combined/separate control; waterjet-type thruster, 710 mm tube, 72 kW |
| 8 | Equipment and weight tables need variant-aware interpretation |
| 9 | Vessel list, not identification of the FBX |

Selected modelling configuration: classic two-covered-hold 507B, supported by the
mesh layout and [project history](https://fleetphoto.ru/projects/1697/). Exact
vessel and date remain unconfirmed. The generic tunnel-disc thruster and MMG rudder
models are approximations, not implementations of the archived machinery.

The historical tables below predate this archive review. Statements that no public
propeller geometry or bow-thruster data was found are superseded by this addendum;
their numerical JSON values remain estimates. See the detailed
[Part 1 audit](../../../../Art/Source/VolgoDon507B/AUDIT.md) for mesh measurements,
architecture, photographs, discrepancies and the reversible upgrade sequence.

This file distinguishes published vessel particulars from prototype calibration
assumptions. Values vary between individual vessels and later conversions. The
prototype represents an unshortened, river-service Project 507B baseline.

## Published particulars

| Parameter | Value | Unit | Source | Confidence | Notes |
|---|---:|---|---|---|---|
| Overall length | 138.3 | m | [FleetPhoto Project 507B](https://fleetphoto.ru/projects/1697), [Korabel project record](https://www.korabel.ru/fleet/info/7075.html) | high | MEB gives 138.74 m for a modernized variant. |
| Length between perpendiculars / design length | 135.0 | m | [MEB project page](https://mebspb.com/dry/507B.html), Korabel | high | Used by the hydrodynamic reference paper. |
| Overall beam | 16.7 | m | FleetPhoto, MEB, Korabel | high | |
| Moulded beam | 16.5 | m | MEB, Korabel | high | |
| Moulded depth | 5.5 | m | FleetPhoto, MEB, Korabel | high | |
| River loaded draught | 3.5-3.6 | m | MEB, FleetPhoto, Korabel | high | Prototype uses 3.53 m baseline. Loading and conversions change this. |
| Loaded displacement | 6750 | t | FleetPhoto, Korabel | medium | Public database value; vessel-specific loading varies. |
| Deadweight | 5000-5290 | t | FleetPhoto, MEB | high | MEB gives 5290 t in river condition; original/project records commonly state 5000 t. |
| Approximate lightship mass | 1750 | t | derived from 6750 t displacement - 5000 t deadweight | low | Derived, not a published lightship value. Not used as the default loaded simulation mass. |
| Cargo hold capacity | 6270 | m3 | FleetPhoto; VSUWT diploma project | medium | Hold arrangement changed across the series. MEB lists 9360 m3 for modernized vessels. |
| Main engines | 2 x 6ChRN 36/45 | count/type | FleetPhoto; VSUWT diploma project | high | Russian notation: 6ЧРН 36/45. |
| Total main-engine power | 1324 | kW | FleetPhoto, MEB | high | 2 x 662 kW, approximately 1800 metric hp total. |
| Propulsion arrangement | twin screw | count/type | [VSUWT diploma project](https://vsuwt-perm.ru/wp-content/uploads/vypusk/2018/isaev_a.s..pdf), p. 5 | high | Propeller geometry was not found publicly. |
| Service/maximum published speed | 10 +/- 0.5 | kn | MEB | high | FleetPhoto lists 21 km/h (11.34 kn), likely light condition. Prototype limit uses 10 kn loaded. |
| Hull type | single-deck dry cargo motor vessel; double sides and double bottom; aft machinery and accommodation | description | VSUWT diploma project, p. 5 | high | Two/four-hold arrangements exist. |
| River/limited open-water operation | River Register class "M"; Lake Onega/Ladoga, wind <= Beaufort 5, wave <= 2 m for the cited vessel | description | VSUWT diploma project, p. 5 | medium | Class and limits can differ after conversion. |
| Block coefficient | 0.851 | dimensionless | [Science-Education hydrodynamics paper](https://s.science-education.ru/pdf/2013/5/242.pdf), table 2 | medium | Paper uses L=135 m, B=16.5 m, T=3.2 m. |
| Endurance | 15 | days | MEB | medium | Modernized sea-river specification. |

## Unresolved real-world particulars

The following were not found in reliable public sources and must not be treated
as measured Project 507B facts:

- propeller diameter, pitch, blade count, RPM, open-water curves and efficiency
- shaft losses and bollard pull
- rudder count, geometry, area, balance and maximum mechanical rate
- centre of gravity, radii of gyration and inertia tensor
- longitudinal/lateral/yaw resistance derivatives
- turning diameter, advance, transfer, stopping distance and crash-stop time
- windage areas and coefficients
- loaded and ballast mass distributions for a specific vessel

Values for these fields in the JSON are marked `estimated: true` and exist only
to make the prototype executable. They require calibration against trials,
drawings, manoeuvring booklets, or class documentation before training use.

## Historical estimated manoeuvring parameters, before Part 6

All values below are estimates for the manoeuvring model described in
`ShipSimulator_Physics.md`. None is measured on a Project 507B vessel.

| Parameter | Value | Basis |
|---|---:|---|
| Centre of gravity, local (x, y, z) | (0, -1.2, 2.0) m | estimated, loaded cargo |
| Radii of gyration roll / pitch / yaw | 5.9 / 33.75 / 33.75 m | typical fractions 0.36 B and 0.25 L |
| Midship / waterplane coefficient | 0.98 / 0.90 | typical for a full river hull |
| Longitudinal centre of buoyancy | 2.0 m forward of midship | estimated, matches the centre of gravity |
| Heave / roll damping ratio | 0.30 / 0.08 | typical critical-damping fractions |
| Form factor, correlation allowance | 1.30, 0.0004 | Holtrop-range estimates |
| Residual resistance coefficient C_R | 0.000516 | calibrated to 10 kn at full power in deep water |
| Surge added mass m'_x | 0.011104 | Söding formula |
| Sway / yaw added mass m'_y, J'_z | 0.12817 / 0.009087 | Clarke et al. (1983) at the loaded draft |
| Y'_v, Y'_r, N'_v, N'_r | -0.21285, 0.04970, -0.04623, -0.02989 | Clarke et al. (1983) |
| Nonlinear hull derivatives | KVLCC2 set | Yasukawa and Yoshimura (2015), assumed to transfer |
| Gear / shaft efficiency | 0.92 / 0.98 | typical reduction gear and shaft line |
| Rated propeller speed | 257.4 rpm | calibrated with C_R to the published speed |
| Shaft inertia, governor band, ramp | 2500 kg m², 1 %, 15 s | estimated |
| Reversal speed / delay / brake | 25 % rated, 8 s, 50 % torque | estimated for an air-start reversible diesel |
| Propeller diameter, positions | 2.1 m at x = ±4 m, 55 m aft of midship | estimated from draft and stern shape |
| K_T, K_Q ahead | [0.33, -0.25, -0.12], [0.042, -0.022, -0.012] | quadratic fits in the Wageningen B-series range |
| K_T, K_Q astern | [0.23, -0.20, -0.10], [0.036, -0.018, -0.010] | estimated |
| Wake fraction / thrust deduction | 0.20 / 0.20 | typical twin-screw full hull |
| Rudders | 2 x 5.5 m², span 2.6 m, behind each propeller | estimated; total about 2.3 % of L T, typical for river vessels |
| Rudder interaction t_R, a_H, x'_H, ε, κ, γ_R, l'_R | 0.39, 0.31, -0.46, 1.05, 0.5, 0.5, -0.8 | KVLCC2 values, estimated |
| Rudder stall / post-stall normal coefficient | 32 deg / 1.1 | estimated |
| Bow thruster | fitted, 160 kW, 1.0 m tunnel, 60 m forward of midship, axis 1.1 m above keel | fitting reported by the project owner; power, size and position estimated, not found in the public sources checked |
| Bow thruster figure of merit, ramp | 0.62, 5 s to full | estimated; gives about 21 kN bollard thrust (13.5 kgf/kW) |
| Bow thruster speed loss u_ref, f_min | 1.0 m/s, 0.3 | estimated trend for tunnel thrusters under way |
| Windage frontal / lateral area (loaded) | 190 / 650 m² | estimated from superstructure and freeboard |
| Wind centroid (x, height) | -15 m, 3.0 m | estimated, aft superstructure |
| Blendermann coefficients C_Dt, C_Dl bow, C_Dl stern, δ | 0.85, 0.65, 0.55, 0.40 | general cargo ship range from Blendermann (1994) |
| Bank suction coefficient, moment lever | 0.5, -0.25 L | estimated; no published model for this hull was accessible |

The KVLCC2 benchmark file (`KVLCC2_MMG_Benchmark.json`) holds the published MMG coefficient set
used to check the implementation, not Project 507B data. Its resistance C_R, propeller K_Q fit and
engine power are chosen so the benchmark reaches its 15.5 kn design speed.

# Part 4 mechanism candidate, 2026-10-22

Archive doc257.pdf p.4 supports lifting extreme covers, sliding middle covers
underneath, lowering and paired movement. Page 7 supports +/-35 degree nozzle
travel. The candidate implements that sequence; actual strokes, pivot positions,
tracks and visible hold interiors are estimated from the model. Twelve legacy
jack positions are retained; the remaining locations needed for the documented
16 vertical and four horizontal jacks are not invented. Detailed hydraulics and
full anchor chain payout remain deferred. Anchor movement only previews unseating.
See Art/Source/VolgoDon507B/PART4_REVIEW.md and mechanisms.json. This is still a
separate visual candidate; playable integration and physics remain unchanged.

# Part 5 playable visual integration, 2026-10-22

The Part 4 visual is now installed in the existing playable prefab. Shaft RPM and
actual rudder angle drive the visual controls; this does not revise the generic
rudder force model. Cargo/ballast and nozzle thrust calibration remain later parts.
Navigation fixture positions and local spill are model-derived visual estimates.
Bridge glass bounds measured through Blender MCP are source X +/-4.578 m,
Y -48.946 to -42.531 m, Z 8.620 to 9.579 m; the Unity navigator eye is
(-1.8, 9.1, -43.3) m. Opposite shaft visual rotation matches the authored mirrored
pitch, not independently verified historical handedness. Machinery rates are
estimated 40 s opening, 45 s paired travel and 5 s unseating. Full anchor payout
and holding forces remain absent. See Art/Source/VolgoDon507B/PART5_REVIEW.md.

## Part 8 quality and performance addendum, 2026-10-23

Distance LODs and final verification are documented in
`Art/Source/VolgoDon507B/PART8_REVIEW.md`. Primary geometry and all physical
parameters remain unchanged. The source now regenerates three FBXs and the normal
integrator installs their LOD group without changing the physics root. Host render
timings are engineering measurements, not vessel specifications or proof of the
target GPU budget. The final discrepancy ledger retains unknown vessel identity,
estimated hydrostatics, steering calibration and machinery rates.

## Part 9 reference baseline, 2026-10-23

The current source decisions and pre-fit tolerances are in
[REFERENCE_DECISIONS.md](../../../../Art/Source/VolgoDon507B/REFERENCE_DECISIONS.md).
No physics parameters changed. The selected classic two-hold model remains
representative, with no authenticated individual-vessel identity.

Reinspection separates the archive's p3 507B condition (6678 t / 3.50 m,
1466 t empty) from the retained 6750 t / 3.53 m prototype and conflicting p8
weight breakdown. The p3 coefficients at 3.5 m (.935/.996/.861) are additional
reference evidence with unresolved variant applicability, not replacements for
current .90/.98/.851. Page 2 is a 600 x 316 pixel raster labelled project 507.
No exact 507B offsets can be claimed from it. The model hull has open boundaries
and needs a separate closed hydrostatic surface before volume-based calculations.

Archive circulation values distinguish same-side and opposite-side nozzle operation;
current common-helm tactical diameter is not a matching test. The archive stop
sequence includes Stop, while current trials command astern immediately and let
the shaft reversal model respond. Missing dwell/loading/depth prevent direct
calibration to a single stopping figure. Visual propeller height above keel is
1.35 m versus the loading model's estimated 1.0 m; reconcile explicitly later.

## Part 10 geometry update, 2026-10-23

The lower visual hull now uses manually sampled sections from the project-507
family drawing, constrained by the retained model waterline. See
[`PART10_REVIEW.md`](../../../Art/Source/VolgoDon507B/PART10_REVIEW.md) for the
conversion assumptions, source-pixel table, unresolved 0.743 m trace residual,
derived volume and checks. The independent closed surface is not yet consumed
by runtime hydrostatics. No vessel JSON, displacement, thrust location or force
coefficient changed in this stage. This remains an estimated prototype geometry.

