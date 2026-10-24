# Part 14: anchor holding and mooring forces

Date: 2026-10-24. Status: complete as a prototype holding and mooring model. Hull, hydrostatics,
propulsion, steering, grounding, cargo visual and hatch machinery (Parts 10-13) are unchanged.
This is a gameplay and engineering prototype: soil holding, line elasticity, damping and failure
loads are estimates, and nothing here is an operational anchoring or mooring safety calculation.
Part 15 (audio) is not started.

S = sourced, D = derived, E = estimated. Archive = `doc257.pdf` (SHA-256 `841d1aeb...e4b`,
rechecked 2026-10-24), one-based pages, read from the scanned pages.

## 1. Scope

Anchoring, on the existing 507B ground tackle (two bow anchors, one stern anchor):

- let go (free run to the bottom, then the brake), veer on the brake, hold, heave in at the
  windlass or capstan rate;
- anchor reaching the bottom from scenario bathymetry; no bottom data means no landing;
- chain as a quasi-static elastic catenary with the part on the bed, slack to taut to stretched;
- holding up to a soil-dependent limit, setting over the first metres of drag, holding lost as the
  chain lifts the shank, dragging towards the ship above the limit, breakout when hove short;
- brake rendering above its holding load, chain parting at the bitter end above the breaking load;
- heaving the ship up to the anchor (or the anchor home), breaking it out and stowing it.

Mooring, between explicit `MooringPoint` components:

- ten ship points at the model's twin-post bollards with leads at the bulwark rail;
- seven shore bollards on an estimated demonstration quay in the Gorodets scene;
- passing a line within reach and nearly stopped, hauling at the winch rate up to its stall pull,
  holding, slackening, casting off;
- several lines together with anchors, current, wind and quay contact;
- overload warning at half the breaking load, parting at the breaking load.

Not in scope: individual chain links as bodies, chain or line hydrodynamic drag beyond a damping
ratio, fenders as springs, ship-to-ship mooring, tugs, a separate emergency steering of nozzles,
and any new anchors, windlasses or bollards.

## 2. Evidence and applicability

| Page | Item | Value | Applicability |
|---|---|---|---|
| p7 | Anchors | Hall; bow 2 x 1750 kg, stern 1250 kg | S; used as given |
| p8 | Chains | Bow 43 mm x 150 m each, stern 37 mm x 75 m | S; grade and stud type not given |
| p8 | Windlass | BE16-4: 5 tf on the warping drum, 10 m/min raising one anchor from 80 m; MAP611-4/8/16, 36/17/10 kW; remote let-go, brake setting and chain length from the wheelhouse. First 507B ships: BER-5-2 | S rate; gypsy pull not given |
| p8 | Stern capstan on 507B ships | ShER-1.5/3/2-1: 3 tf on the drum, 18 m/min from 40 m; MAP511-4/16, 20/5 kW | S |
| p8 | Mooring winches | LEGShA2-1, electrohydraulic, automated, 2 units, 2.5 tf, AM71-4 14 kW | S pull; speed not given |
| model | Bollards | Twin-post bollards in `VD507B_detali`: forecastle (2 pairs per side), abreast the hold ends, at the stern | D; positions read from the mesh (`Previews/part14-source-*.png`) |

The archive gives no mooring rope size, no chain grade, no windlass gypsy pull and no soil data.
No outside 507B source was used; generic design values are labelled E below.

## 3. Model choice

The chain and each line are quasi-static elastic catenaries (`Physics/Catenary.cs`). For a given
unstretched length, span and height the solver finds the horizontal tension by bisection on a
span function that grows with tension: from the slack state (hanging straight down, the rest on
the bed), through the touchdown catenary with a grounded part, to a fully suspended line and
then elastic stretch (strain from the horizontal tension over EA). This is enough here because
the ship moves slowly against the speed of waves along a chain, the shape and the end forces
come out together, and the transition from slack to taut is continuous. Chain links as
Rigidbodies or joints were not needed: they would add stiff constraints in series with the
manoeuvring solve and double-apply forces.

`GroundTackle` (on the vessel, stepped inside `ShipPhysicsController.Simulate` after grounding)
applies each line's force at its hawse or lead:

- the horizontal force goes through `AddExternalForce`, so the manoeuvring solve's added mass
  and its yaw moment about midship apply;
- the vertical force (line pull minus the lightship's own weight of anchor and chain now outboard)
  acts at the point on the Rigidbody;
- the heel and trim moment of the lead's height above half draft, where the solve places all
  lateral force, is added as a torque.

Damping is 0.1 of critical on the line's current horizontal stiffness and the ship mass, acting
on the rate the span opens at the attachment point's actual velocity. It scales to zero with a
slack line and the total never goes below zero, so a line only pulls. No joint, collider or
kinematic constraint holds the ship.

Holding: the load reaching the anchor is the horizontal tension less the friction of the chain on
the bed. Capacity is ratio x anchor submerged weight, rising from 0.3 to 1 of that over the first
4 m of drag (setting), and falling to zero as the chain lifts the shank to 10 degrees. Above
capacity the anchor slides along the bed towards the ship by exactly the distance that brings the
load back to capacity (rigid-plastic), so the ship is never pinned to a world point and the work is
lost to the soil. A chain shorter than the height, or lifting the crown harder than 1.5 times the
anchor's submerged weight, breaks it out.

The physics step reads attachment points from the Rigidbody pose, not the transform: right after a
save restore the transform still holds the old position until the next simulation (see section 6).

## 4. Parameters

All physics values are in `Data/Vessels/VolgoDon507B.json` `groundTackle`, validated by
`VesselDataValidator`; hawse and lead positions are in the prefab, from `mechanisms.json`
`mooring_stations` and the chain attachment pivots. The Part 13 anchor rates moved there from
`mechanisms.json` `operating_rates.anchor`, which keeps only the visual gypsy ratio.

| Parameter | Before (Part 13) | After | Units | Status, source (range) |
|---|---|---|---|---|
| Anchor mass, bow / stern | visual only | 1750 / 1250 | kg | S p7 |
| Chain length, bow / stern | 150 / 75 | 150 / 75 | m | S p8 |
| Chain mass per metre, bow / stern | none | 36.8 / 27.2 | kg/m | E: stud-less 0.0199 d^2 (stud-link 40.5 / 30.0) |
| Chain submerged weight, bow | none | 315 | N/m | D: steel 7850 kg/m^3 in fresh water |
| Chain axial stiffness EA, bow / stern | none | 1.58e8 / 1.17e8 | N | E: stud-less 0.854e8 d^2 N, d in m (0.8-1.2 x) |
| Chain breaking load, bow / stern | none | 1027 / 770 | kN | E: grade 1, 0.0137 d^2 (44 - 0.08 d) kN; grade unknown (grade 2 is 1.43 x) |
| Brake holding | none | 0.45 x breaking load = 462 / 347 | kN | E: common windlass brake design value |
| Gypsy nominal pull, bow / stern | none | 69.3 / 51.3 | kN | E: 37.5 d^2 N for grade 1; archive gives only the drum pull |
| Stall pull | none | 104 / 77 | kN | E: 1.5 x nominal |
| Heave rate, bow / stern | 10 / 18 | 10 / 18 | m/min | S p8 |
| Let-go rate | 1.0 | 1.0 | m/s | E (0.5-2) |
| Veer on the brake | none | 0.5 | m/s | E (0.2-1) |
| Brake render rate limit | none | 2.0 | m/s | E |
| Holding ratio silt / sand / rock | none | 2.0 / 3.0 / 1.0 | x submerged anchor weight | E (1.5-3 / 2-4 / 0-2); bow anchor in sand 45 kN |
| Chain on bed friction | none | 0.7 | - | E (0.5-1.0) |
| Unset fraction, setting distance | none | 0.3, 4 | -, m | E |
| Shank uplift for zero holding | none | 10 | deg | E (5-15) |
| Breakout pull | none | 1.5 | x submerged anchor weight | E (1-3) |
| Line damping | none | 0.1 | of critical | E (0.05-0.3) |
| Mooring winch pull | none | 24.5 | kN | S p8 (2.5 tf); used for bow and midship stations |
| Stern capstan pull | none | 29.4 | kN | S p8 (3 tf) |
| Winch haul rate | none | 0.25 | m/s | E (10-20 m/min) |
| Capstan haul rate | none | 0.3 | m/s | S p8 (18 m/min) |
| Winch stall | none | 1.5 x rated pull | - | E |
| Line breaking load | none | 200 | kN | E (150-300); rope not given |
| Line EA | none | 1.3 | MN | E: about 15 % stretch at break (0.8-2.5) |
| Line mass | none | 1.0 | kg/m | E (0.7-1.5) |
| Line length min / max | none | 2 / 120 | m | E |
| Line slack rate | none | 0.5 | m/s | E |
| Overload warning | none | 0.5 x breaking load | - | E |
| Heaving-line reach, speed to pass | none | 35, 0.5 | m, m/s | E |
| Lines per shore bollard | none | 2 | - | E |
| Demonstration quay | none | face x = 48 m, z -70 to 80 m, deck 1.6 m above water, 7 bollards 24 m apart | m | E: not a surveyed structure |
| Berth pocket | none | 5.0 m, silt, 10 m blend | m | E |

Ship mooring points (Blender coordinates, starboard side; port mirrored), D from the mesh:

| Station | Bollard (x, y, z) | Lead (x, y, z) |
|---|---|---|
| BOW HEAD | 3.35, 63.55, 4.35 | 4.70, 63.90, 4.75 |
| BOW BREAST | 5.82, 57.99, 3.55 | 6.85, 57.99, 4.45 |
| FWD SPRING | 7.88, 31.83, 2.45 | 8.25, 31.83, 3.00 |
| AFT SPRING | 7.88, -36.22, 2.45 | 8.25, -36.22, 3.00 |
| STERN | 2.68, -66.85, 2.45 | 2.68, -68.40, 2.95 |

## 5. Baseline before the change

`GroundTackleTrajectoryTests` was written first and run on the Part 13 code
(`part14-trajectory-before.json`): with the anchors let go (Part 13 payout) the drift track in a
0.5 m/s current and 12 m/s beam wind was identical to the stowed one, -90.46 m across after 300 s:
the anchors carried no force. Rerun after the change (`part14-trajectory-after.json`):

| Track | Before | After |
|---|---|---|
| Drift, anchors stowed | x -90.46, z 1.69 | identical, difference 0.0 |
| Drift, all three anchors let go to the bottom (no veer) | x -90.46, z 1.69 | x -86.06, z 4.36: short scope drags, holding changes the track |
| Half ahead, half starboard helm | x 231.21, z 264.47 | identical, difference 0.0 |

`StowedTackle_LeavesTheMotionUnchanged` also compares the prefab with and without the component
over 200 s of manoeuvring in current and wind: bit-identical.

## 6. Defects found and fixed during the part

1. **Stale transform after a restore.** The first save-and-load test mid-drag broke the anchor out
   on the first step after loading: `Rigidbody.position` was restored but the transform keeps the
   old position until the next simulation, so the hawse looked 0.63 m further away and the chain
   seemed overstretched. The tackle now reads its points from the Rigidbody pose; pass-line reach
   too. Regression: `SaveLoadMidDrag_ContinuesWithoutAJump_AndMissingBollardIsReported`.
2. **Solver at the edge of slack.** With the tension vanishing at the exact slack span the shape
   parameter underflowed and the sampled end height came out 14.2 m instead of 10 m; a vertical
   slack line without a bed reported a negative grounded length. Both now return the slack shape.
   Regressions in `GroundTackleDataTests`.
3. **Part 13 visual payout** was a separate frame-time animation. `VesselMechanisms` now only
   poses the anchors and gypsies from `GroundTackle`; there is one source of chain length, state
   and rate, stepped with physics.

## 7. Tests and checks

- EditMode 258/258: `TestResults/check-EditMode-20261024-070344-301.xml` (new: solver, data,
  save validation, prefab hawses and bollards, demonstration berth in the scene).
- PlayMode 90/90: `TestResults/check-PlayMode-20261024-071513-553.xml` (new: 19 ground tackle
  cases and the trajectory record), repeated after the last HUD change. Case measurements:
  `part14-cases.jsonl`.
- `GroundTackleRuntimeCheck.Run` in the Gorodets scene: `GROUND_TACKLE_RUNTIME|PASS`,
  `part14-runtime.json`.
- Menu save/load (`Logs/check-Menu-20261024-072112-512.log`): `MENU_SMOKE|PASS`, `VOLGODON_RUNTIME|PASS` (port anchor let go to the bottom,
  saved, restored across scenes), `VOLGODON_LOADING_UI|PASS`.
- Shakedown: `VESSEL_SHAKEDOWN|PASS: 5 vessels`.
- Sea trials: `SEA_TRIALS|PASS`, rows identical to Part 13.
- Quality gate: `VOLGODON_QUALITY|PASS` (prefab reinstalled; geometry unchanged).

| Case (507B loaded, flat bed) | Result |
|---|---|
| Let go in 5, 12, 25 m | Lands, brake set, chain 6.3 / 13.3 / 26.3 m: hawse to shackle plus the 0.35 m unseat |
| No bottom data | Hangs at the 75 m limit, status NO BOTTOM DATA, no depth invented |
| Short scope (to the bottom), 12 m, 14 m/s wind astern | Shank lifted: capacity 0.14 kN, anchor dragged 52 m, tension at most 4.2 kN |
| 90 m extra scope, same wind | Slack first (under 2 kN), then holding at 57 kN peak; anchor moved 9.8 m while setting |
| 0.5 m/s current and 10 m/s wind, 40 m chain in 8 m | HOLDING, 12.2 kN against 35.9 kN capacity, 0.006 m/s after 1500 s, largest change per 0.02 s 1.0 kN |
| Full astern, 44 m chain, dt 0.01 / 0.02 / 0.04 s | DRAGGING at 41.1 kN; ship moved 205.6 / 205.7 / 205.7 m, anchor 162.4 m: step-independent |
| Heave from 40 m in 8 m, 8 m/s wind | Broke out and stowed in 236 s (windlass limit 236 s), peak 32.6 kN, under the stall pull |
| Two bow anchors, 16 m/s wind | Load shared 9.7 / 9.7 kN, holding |
| Free surge from 0.6 m/s, brakes holding | Running work of the chain never above 0 J; it absorbed 1.20 of 1.22 MJ |
| Four lines, 0.42 m/s current and 10 m/s wind off the berth | Held within 2.5 m, peak 98.7 kN (49 % of breaking) |
| Haul two lines 8 m in | Peak 35.9 kN, under the 36.8 kN stall pull; slack at 0.5 m/s; cast off under load |
| One stern line, full ahead | Parted at 199.9 kN, LINE PARTED event and notice |
| Four lines hauled against a quay collider | RMS speed below 0.001 m/s over 300 s, 0.003 m motion, no line parted, peak 53.8 kN |
| Save and load mid-drag with a let-go in progress | Same chain, command and line length; tension within tolerance after 1 s; a missing shore bollard is reported and that line dropped; a Part 13 save hangs the anchor and settles it on the bottom |
| Step cost, three anchors on the bottom and four lines | 20.2 microseconds per physics step, 0 bytes allocated |

Gorodets scene (`part14-runtime.json`): five lines alongside held the ship within 6 mm over
240 s, loads 36-59 kN after hauling (at most 30 % of breaking); at anchor with 30 m of chain in
5.3 m head to the stream and a 10 m/s wind, HOLDING at 11 kN; full astern dragged the anchor
32 m at 32.4 kN peak and carried her into 4.2 m of water near the channel edge, where she touched
the bottom (grounding holding force 87 kN) with the chain still on: the existing grounding contact
and the chain acted together without instability; heaving then ran. Line and chain drawing: 7.8 microseconds per frame,
0 bytes allocated.

## 8. Images

`Previews/`:

- berth: `part14-berth-overview.png`, `part14-berth-bow-lines.png`, `part14-berth-springs.png`,
  `part14-berth-stern-line.png`, `part14-hud-alongside.png`;
- anchor: `part14-anchor-at-anchor.png`, `part14-anchor-at-anchor-underwater.png` (catenary with
  the grounded part at the anchor; the scene has no rendered river bed), `part14-hud-at-anchor.png`,
  `part14-hud-dragging.png`, `part14-anchor-heaving.png`, `part14-anchor-heaving-underwater.png`,
  `part14-hud-heaving.png`;
- source bollards: `part14-source-bow-bollards.png`, `part14-source-fwd-spring-bollards.png`,
  `part14-source-stern-bollards.png` (bpy renders through the registered Blender MCP);
- integration: `part14-unity-*.png`, `part14-lod2-*.png`, `part14-transition-*.png` from the
  quality gate.

## 9. Limits and remaining work

- Soil holding, chain grade, rope size and strength, damping, setting and uplift are estimates.
  The model is not a holding-power or mooring-load calculation.
- Quasi-static lines: no snatch waves along the chain, no hydrodynamic drag on chain or rope, no
  rope bending over a fairlead. A short stiff line catching a moving ship produces large but
  continuous loads, which can part it.
- The stud type, so the chain weight, is unknown; stud-link chain would be about 10 % heavier.
- The windlass type of the representative ship (BE16-4 or the early BER-5-2) is unknown; BE16-4
  rates are used.
- Midship stations are assumed to be led to a mooring winch; the archive lists two winches and
  the stern capstan without saying which lines they work.
- The demonstration quay is estimated and placed for practice; it is outside the buoyed channel,
  so the Gorodets mission reports OUTSIDE FAIRWAY there. No fender springs: the quay is a collider.
- The river bed is not rendered in Gorodets; the underwater views show the chain against the sky.
- Hull collision shapes are the existing compound boxes (Part 16). Ropes may pass through the
  bulwark between bollard and lead in close views.
- Anchors lie upright on the bed; their orientation is not modelled.
- Other vessels have no ground tackle data and are unchanged.
