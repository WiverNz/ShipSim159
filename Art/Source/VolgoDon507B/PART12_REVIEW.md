# Part 12: propulsion, stopping and steering calibration

Date: 2026-10-24. Status: complete as an audit and calibration stage, with two
separately requested fixes (open-hold water, refloating from a shoal) reported in
their own sections below. No hydrodynamic coefficient was refitted: the comparable
archive evidence does not justify it (see "Calibration decision"). Nothing here is
validated training data. Parts 13 to 17 are not started.

S = sourced, D = derived, E = estimated. Archive = `doc257.pdf`, page numbers as in
[REFERENCE_DECISIONS.md](REFERENCE_DECISIONS.md).

## 1. Machinery and actuator audit

| Item | Model / data | Kind | Finding |
|---|---|---|---|
| Engine to shaft | 375 rpm rated, direct drive (ratio 1) | S rating, E ratio | Archive p5 gives the engine rating only; no gearbox evidence. Ratio kept 1:1, still unconfirmed. Full-ahead shaft 341.7 rpm at 91% load, deep loaded |
| Reversal | fuel cut, air brake 0.5 Qmax to 25% rpm, 8 s restart delay, 15 s full-range setpoint ramp | E | Measured crash-stop reversal: shaft negative after 15.1 s, 81 m. No windmilling stall: the brake brings the shaft below 25% and it stays there |
| Handedness | port shaft visual axis -forward, starboard +forward | E | Outward-turning pair (port anticlockwise, starboard clockwise seen from astern). The force model has no paddle-wheel term, so handedness affects visuals only. Archive pitch difference 1.52/1.47 m is not modelled |
| Thrust points | x = +/-3.6 m, z = -64.65 m | D | Matches the authored shafts (Part 6) |
| Thrust height | `loading.propellerAxisHeightAboveKeelM` 1.0 -> **1.35 m** | D | Now equals the authored visual axis (Part 9 discrepancy closed). The 3-DOF solve applies thrust in the horizontal plane; its pitching lever (about 1.3 m below G, under 0.2 MN m at full astern) is omitted, a trim effect of millimetres |
| Propeller immersion | static per loading -> **dynamic** in the Rigidbody path | D | `ShipPhysicsController` now passes the immersion of the actual hub depth (trim, heel, heave, stern squat) to `ManoeuvringModel`. Only vessels with a loading model use it; the pure simulator and other vessels keep the still-water value |
| Nozzle limits / rate | +/-35 deg, 2.5 deg/s, 28 s hard over to hard over | S limit, E rate | Within the archive "at most 30 s" (p7) |
| Bow thruster | 72 kW bow waterjet, 0.71 m pipe, 9.9 kN estimated bollard | S equipment, E thrust | Applicable: archive p7 lists it for the 507B. Placement and thrust curve are estimated. 13.0 deg/min yaw from rest, loaded; zero in lightship because the pipe at 1.1 m above keel is out of the water at the 0.94 m mean draft forward (bow draft about -0.35 m) |

## 2. Matched conditions and calibration decision

Evidence and its conditions (archive p3), before any comparison:

| Archive value | Stated conditions | Missing | Use here |
|---|---|---|---|
| 20.0 km/h loaded, 21.2 km/h ballast | deep still water | exact load for the speed row | Calibration (loaded), independent validation (ballast) |
| Stop 510 m / 3.5 min from full ahead | inertia table | initial speed, loading, depth, stop dwell, stop criterion | Comparison only |
| Circulation 320 m (nozzles to one side), 160 m (other configuration) | none | speed, loading, depth, steady vs tactical, meaning of the second case | Comparison only |

Matched simulator conditions: deep water; full load 6750 t (prototype) or the
archive ballast displacement 3828 t built as lightship 1750 t + 200 t stores +
1878 t ballast split across both groups; 2400 s approach at full ahead; crash stop
by full astern on both engines at t = 0 until surge speed is zero through the water.

Results (pure simulator, `VolgoDonPropulsionReview.Run`, files
`part12-trials-before.json` from `part12-baseline-vessel.json` and
`part12-trials-after.json` from the current JSON):

| Condition | Mass t / draft m | Speed km/h | 0-90% s / m | Crash stop m / s | Reversal s / m | Tactical / steady D m | 10/10 deg | Split + nozzles yaw deg/min / D m |
|---|---|---|---|---|---|---|---|---|
| Full load, deep, before | 6750 / 3.495 | 19.76 | 481 / 1484 | 716 / 285 | 15.1 / 81 | 579 / 462 | 3.7 / 4.6 | 14.9 / 137 |
| Full load, deep, after | same | 19.76 | 481 / 1484 | 716 / 285 | 15.1 / 81 | 579 / 462 | 3.7 / 4.6 | 14.9 / 137 |
| Archive ballast 3828 t, deep | 3828 / 2.051 | 21.19 | 293 / 957 | 488 / 176 | 15.3 / 87 | 475 / 408 | 3.6 / 3.7 | 19.6 / 160 |
| Full load, 8.2 m | 6750 / 3.495 | 18.02 | 437 / 1224 | 599 / 261 | 14.8 / 73 | 580 / 474 | 3.1 / 3.7 | 12.8 / 166 |
| Full load, 4.6 m | 6750 / 3.495 | 13.84 | 334 / 713 | 361 / 203 | 14.2 / 53 | 1156 / 1108 | 0.8 / 0.8 | 7.5 / 300 |
| Ballast 3828 t, 4.6 m | 3828 / 2.051 | 17.29 | 230 / 600 | 335 / 147 | 14.7 / 68 | 422 / 382 | 3.9 / 4.6 | 13.9 / 249 |
| Lightship, deep, before / after | 1750 / 0.941 | 22.31 / 22.37 | 145 / 145 | 283 / 91, 285 / 91 | 15.4 / 15.5 | 288 / 288, 263 / 262 | 4.7 / 4.7, 4.8 / 4.9 | 33.2 / 130 |

Only the lightship changes: with the corrected 1.35 m axis its stern-trimmed screws
are 99% rather than 100% immersed. Every row of the all-vessel report outside the
507B lightship is identical to Part 11 (`part12-sea-trials-after.md` against
`part11-sea-trials-after.md`), including KVLCC2.

Archive comparison: loaded speed -1.2% (19.76 against 20, inside the two-digit
resolution of the archive value); ballast speed -0.05% (21.19 against 21.2) as an
independent check that was not fitted; ballast mean draft 2.05 m against the
archive 2.11 m.

**Calibration decision.** Steady resistance and thrust stay as they are: refitting
the residual coefficient to 20.0 km/h would be false precision and would move the
independent ballast check away from agreement. Acceleration, reversal, stopping and
turning are not refitted because the archive does not give comparable conditions.
All parameters stay estimated and configurable in the vessel JSON.

### The 716 m stopping discrepancy

Decomposition at full load, deep: reversal 15.1 s / 81 m, then 270 s / 635 m of
full astern. Effective astern push is (1 - t) of 132 kN against a resistance that
falls with V squared. Sensitivity (one parameter at a time, deep, full load):

| Parameter (bounds) | Stop m | Turning steady D m | Comment |
|---|---|---|---|
| Reversal delay 4 / 8 / 12 s | 705 / 716 / 726 | unchanged | reversal phase is small |
| Brake torque 0.3 / 0.5 / 1.0 Qmax | 723 / 716 / 709 | unchanged | |
| Reversal rpm 15 / 25 / 40% | 720 / 716 / 711 | unchanged | |
| Setpoint ramp 8 / 15 / 25 s | 711 / 716 / 730 | unchanged | |
| Astern KT x0.8 / 1 / 1.2 | 821 / 716 / 639 | unchanged | astern thrust 106 / 132 / 158 kN |
| Astern KQ x0.8 / 1 / 1.2 | 628 / 716 / 796 | unchanged | power-limited rpm |
| Thrust deduction 0.1 / 0.2 / 0.3 | 716 / 716 / 715 | 462 / 462 / 461 | also moves speed 20.9 / 19.8 / 18.6 km/h |
| Surge added mass 0.007 / 0.011 / 0.02 / 0.04 | 704 / 716 / 742 / 801 | 460 / 462 / 465 / 472 | |
| Residual resistance 0.5e-4 / 1.0e-4 / 1.5e-4 | 728 / 716 / 704 | ~ | speed 19.94 / 19.76 / 19.60 |

Reaching 510 m from 5.49 m/s would need roughly 50% more astern thrust, above the
model's ahead bollard pull (166 kN), which is not a plausible ducted-propeller
astern characteristic. The same model gives 428 m / 228 s from half ahead
(3.83 m/s), 132 m / 131 s from slow ahead, and 488 m / 176 s in the ballast
condition. The archive figure is therefore consistent with a lower approach speed or
a lighter condition, which the archive does not state. The discrepancy is explained
as unresolved test conditions, not as a fitted parameter. Published envelope for the
loaded deep-water crash stop: 630-820 m over the astern-coefficient bounds above.

### Turning and differential thrust

Steady diameter 462 m (3.4 Lpp), tactical 579 m, against the undefined archive 320 m.
Within the bounds tried (passive nozzle slope 2-5 per rad, deflection 0.7-1.0, hull
N_r x0.8-1.25) the steady diameter spans 421-564 m; reaching 320 m would need
coefficients outside these physically argued ranges (the ring-wing slope estimate
for a duct of length/diameter 0.9 is about 3 per rad). Differential thrust (port
ahead, starboard astern, nozzles hard over, from rest, 300 s) turns at 14.9 deg/min
on a 137 m circle loaded and 160 m in ballast, the same order as the archive 160 m
second case, whose configuration is not stated. Zig-zag overshoots stay small and
far inside the IMO limits; IMO criteria are a sanity bound only for this river ship.

Step size: dt 0.025, 0.05 and 0.1 s agree to 0.3% on every metric above (Part 9 gate 1%).

## 3. Separate fix: river water inside open holds

Cause, established before the change (`Previews/part12-before-hold-*.png`): the
hold interior exists and renders correctly (floor at y = -2.60 m, side linings and end
bulkheads to y = +2.60 m, closed covers from +2.61 m). The single river water plane
at y = 0 is transparent, drawn after opaque geometry with a depth test, and passes
through the hull; in a loaded ship the hold floor lies 2.6 m below it, so the water
surface, with its reflections, filled the open hold. Normals, materials and the hold
meshes were not at fault.

Fix, compatible with URP Forward+ (`PC_Renderer`, rendering mode 2):

- `Shaders/HullWaterMask.shader`: an invisible quad per hatch opening, 5 cm under the
  closed covers, drawn in queue Transparent-10 with the normal depth test. It writes
  stencil bit 8 (a URP user bit) only where it is the nearest surface, so closed
  covers and other hulls in front of it hide it.
- `RiverWater.shader`: the ForwardLit and motion-vector passes skip pixels with that bit.
- The quads are children of the vessel, so they follow draft, heel and trim. Water
  outside the hull and around other vessels is untouched
  (`part12-after-hold-outside-waterline.png`). The hold floor was not raised.

Cargo: `HoldInteriorVisual` (runtime, presentation only) draws one heap per hold
from `ShipPhysicsController.CompartmentMassKg`, so loading, unloading and save
restore update it, and it adds no mass, collider or force. The loading model names
no cargo, so the heap is a **nominal bulk cargo of sand or gravel class, estimated
bulk density 1600 kg/m3**, not a historical cargo. Each heap is a frustum with a
0.6 m shoulder, limited to 0.3 m under the covers' underside; a mass that needs
more room is drawn full and reported by `Overfilled`. `VolgoDonHoldIntegration`
measures the extents from the authored meshes with the covers closed and is called by
the mechanism integration, so a rebuild keeps it.

| Hold | Clear floor m | Clear height to covers m | Volume m3 | Full heap 2400 t m | Physics block m |
|---|---|---|---|---|---|
| Fore | 11.38 x 43.20 | 5.20 | 2557 | 3.27 | 3.0 |
| Aft | 11.38 x 44.56 | 5.20 | 2637 | 3.17 | 3.0 |

Recorded inconsistencies, not resolved: the measured clear volume 5194 m3 is 18% under
the archive 6370 m3 (archive p3/p4, likely including coaming and cover space the model
does not reproduce); the loading model keeps each cargo centre at the 3.0 m block
centroid whatever the fill, while the heap at half load stands 1.6 m high, so partial
cargo is drawn about 0.7 m lower than its physics centre (a conservative GM error).

Checked views (`Previews/part12-after-hold-*.png`, Gorodets water, vessel at its
solved draft and trim): fore open empty / half / full from above and at deck level,
aft open empty / full, both half open full / empty, closed, LOD1 full, LOD2 empty,
waterline outside the hull. Hatch animations are unchanged; the integration restores
the mechanism pose after measuring.

## 4. Separate fix: refloating from a shoal

Reproduction before the change (`part12-grounding-before.jsonl`, 507B loaded, sand,
bathymetry only, no PhysX colliders involved):

| Case | Contact speed m/s | Ground reaction kN | Holding kN | Astern thrust kN | Result |
|---|---|---|---|---|---|
| 1:50 shoal, slow ahead | 1.84 | 1776 | 799 | 132 | stuck for 600 s at dt 0.01, 0.02, 0.04 |
| 1:50 shoal, coasting | 0.65 | 664 | 299 | 132 | "freed" after 275 s, only by creep |
| Stern, 1:50 | 1.20 | 1314 | 591 | 162 ahead | stuck |

Causes found:

1. **Contact geometry.** All ten contact points sat at keel level. On the
   reconstructed hull the bow pair (z = 62.1 m, x = +/-2.48 m) was about 1.2-1.5 m
   and the stern pair (z = -62.1 m, x = +/-4.95 m) about 2.2 m below the real bottom,
   so the ship touched with phantom points, early and at the extremities.
2. **Friction model.** Below 5 cm/s the regularised friction became a large viscous
   damper: any thrust below the limit produced a slow creep instead of static
   friction, and the only "release" from a light touch was that creep.
3. **Normal direction.** Always vertical: a raked or flared bottom could not push
   the hull back out.
4. Not causes: normal-force cap, damping, propellers, PhysX colliders (the
   cases use bathymetry only) or grounding state; there is no state latch, zeroing
   of velocity or teleport in the old or new code.

Changes in `GroundingController`, applying only to a vessel with a section table
(the 507B) except where noted:

- Contact points on a 4.5 m x 1.67 m grid over the real bottom from the section
  table (`SectionHydrostatics.BottomHeightAt`) below the selected waterline, plus the
  two propeller tip circles under the cut-up stern (lowest point 0.45 m above keel).
- Normal pressure = modulus x penetration over each point's plan area, capped by an
  estimated bearing pressure, with damping only against further penetration. The
  reaction acts along the local hull surface normal; its vertical share lifts the
  hull, its horizontal share enters the manoeuvring solve.
- All vessels: Coulomb friction is solved after the manoeuvring step as an impulse
  (12 projected Gauss-Seidel sweeps over the contact points, using the solve's own
  added-mass response). Each point gets at most the force that would stop it, within
  mu times its reaction. No creep below the limit, no overshoot, no energy gain.
  Vessels without section tables keep their legacy points and stiffness.

| Soil (section contact) | Modulus N/m3 | Bearing Pa | mu | Source |
|---|---|---|---|---|
| Silt / mud | 0.5e6 | 30e3 | 0.25 | E; mu middle of 0.2-0.3 |
| Sand | 1.5e6 | 150e3 | 0.35 | E; mu middle of 0.3-0.4 |
| Rock | 20e6 (step-limited) | 5e6 | 1.0 | E; mu inside 0.8-1.5 |

Friction ranges: U.S. Navy Salvor's Handbook, static coefficients, quoted via a
secondary copy (https://1library.net/article/refloating-operations-refloating-strandings.zl22w9lq);
the current Navy Salvage Manual Vol. 1 (S0300-A6-MAN-010) was not checked line by
line. Moduli are ship-footing values (plate values reduced for a footing tens of
metres wide); bearing pressures are loose saturated sand and soft silt orders of
magnitude. All are estimates and remain `SerializeField` values.

Result after the change (`part12-grounding-after.jsonl`, `GroundingRecoveryTests`).
Effective astern push is 0.8 x 132 = 106 kN for the loaded 507B.

| Case (sand, 507B unless stated) | Contact m/s | Reaction kN | Holding kN | Recovery | Freed after s |
|---|---|---|---|---|---|
| Light bow touch, 1:100 | 0.23 | 213 | 74 | full astern | 66 |
| Partial entry, 1:100, dt 0.01 / 0.02 / 0.04 | 0.31 | 279 / 279 / 266 | 98 / 98 / 93 | full astern, gradual | 93 / 93 / 87 |
| Stern touch, 1:100 | 0.23 | 257 | 90 | full ahead (162 kN) | 58 |
| Forward hold only (bow trim 2.2 m), 1:100 | 0.27 | 195 | 68 | full astern (88 kN: the screws are partly out of the water) | 111 |
| Firmer entry, 1:100 | 0.67 | 609 | 213 | full astern, then split engines and nozzles | not freed |
| Hard grounding, full ahead onto 1:25 | 4.52 | 7320 | 2543 | full astern, then split engines | not freed |
| Same, forward hold discharged over 10 min | | | | full astern | yes, with no jump |
| Volgo-Balt 2-95A/R light bow touch, 1:100 (legacy points) | 0.29 | 146 | 66 | full astern | 64 |
| Volgoneft 1577 light bow touch, 1:100 (legacy points) | 0.29 | 198 | 89 | full astern | 73 |

Every case: maximum rise above the floating position under 0.6 m (0.40 m at the
hard grounding), vertical speed RMS on the bottom under 0.03 m/s, friction energy
gain under 1e-3 of the friction work (largest 63 J against megajoules). The Luch and
Meteor grounding tests from 2026-09-20 still pass.

What it means for play: whether astern works her off is decided by the bottom's
holding force against the effective thrust, shown on the HUD as kN HOLDING. For the
loaded 507B on sand that is roughly a ground reaction under 300 kN (about 30 t, a
few centimetres of lost draft): a drift or very slow touch. A firmer entry needs
the draft reduced, a change in water level or outside help. Working the engines does
not create grip or energy, but on a long flat-bottom contact it does not by itself
break the ship free either. Speeds are about the bank slope and soil assumed here.

## Files

Runtime: `Scripts/Physics/GroundingController.cs`, `ManoeuvringModel.cs`
(`ExternalAcceleration`, dynamic propeller immersion input), `SectionHydrostatics.cs`
(`BottomHeightAt`), `ShipPhysicsController.cs`; `Scripts/Visuals/HoldInteriorVisual.cs`;
`Shaders/HullWaterMask.shader`, `Shaders/RiverWater.shader` (stencil test only).
Editor: `VolgoDonHoldIntegration.cs`, `VolgoDonHoldReview.cs`, `VolgoDonPropulsionReview.cs`,
`VolgoDonMechanismIntegration.cs` (calls the hold integration). Assets:
`Materials/HullWaterMask.mat`, `Models/VolgoDon507B_Improved/materials/CargoNominalBulk.mat`,
`Models/VolgoDon507B_Improved/Textures/CargoNominalBulk.png` (generated noise, no
third-party imagery), `Prefabs/Vessels/VolgoDon507B.prefab` (new component only).
Data: `Data/Vessels/VolgoDon507B.json` (`propellerAxisHeightAboveKeelM` 1.35).
Tests: `Tests/PlayMode/GroundingRecoveryTests.cs`, `HoldInteriorVisualTests.cs`,
`Tests/EditMode/HoldInteriorAndContactTests.cs`, updated
`ShipDynamicsIntegrationTests.UnloadingGroundedVessel_RestoresClearanceThroughBuoyancy`
(it expected the clearance at the removed keel-level stern point).

## Verification, 2026-10-24

All launches with Unity 6000.6.0f1; every one exited 0 with no `error [A-Z]` lines.

- EditMode **245/245**, `TestResults/check-EditMode-20261024-005812-882.xml`.
- PlayMode **68/68**, `TestResults/check-PlayMode-20261024-005354-537.xml`. The first
  full run (66 tests) failed one, the clearance test noted above.
- Sea trials `SEA_TRIALS|PASS`, `Logs/Part12/seatrials-after.log`, copied to
  `part12-sea-trials-after.md`.
- Propulsion review `PROPULSION_REVIEW|PASS` before and after, `Logs/Part12/trials-*.log`.
- Shakedown `VESSEL_SHAKEDOWN|PASS: 5 vessels`, `Logs/check-Shakedown-20261024-005626-601.log`.
- Menu `MENU_SMOKE|PASS` (start, save, change scenario and vessel, load, pause and
  resume), `Logs/check-Menu-20261024-005724-646.log`; it also restores a mid-travel
  hatch state (forward open, opening 0.625) and released anchors after load. Mechanism
  save/restore mid-transition is also covered by EditMode `VesselMechanismTests`.
- Hold captures `HOLD_REVIEW|PASS`, `Logs/Part12/hold-before3.log`, `hold-after.log`.
  The first "before" set was overwritten by a rerun after the fix; it was regenerated
  with the component inactive, which with no mask renders the unchanged water.
- Blender: not used. No geometry, UV or FBX changed, so no Blender step was needed.

## Remaining limits

- No comparable full-scale trial: stopping and turning are envelopes, not calibrated.
  Engine ratio, reversal timings, astern curves and nozzle coefficients stay estimated.
- Thrust pitching moment and propeller handedness forces are not modelled.
- Grounding: no bottom-slope normal, suction, scour by propeller wash, ploughing
  memory or damage model; bathymetry is a smooth surface. PhysX scene colliders
  (Gorodets rocks and bank boxes) still use default PhysX friction under the hull
  colliders; that contact was not part of these cases. Part 16 owns collider geometry.
- Hold capacity and partial-cargo centre inconsistencies as listed in section 3;
  the heap texture is a generated placeholder.
- The water mask relies on URP stencil bit 8 being unused, true for the Forward+
  renderer in use; a deferred renderer or a feature that clears stencil would need a
  review. A camera below the deck inside the hold is not a supported view.
