# Part 13: machinery timing and remaining fittings

Date: 2026-10-24. Status: complete as a prototype machinery stage. Hull, hydrostatics,
propulsion and steering calibration (Parts 10-12), cargo visual and grounding are
unchanged. Part 14 (anchor holding and mooring forces) is not started: the anchors
in this part are visual and carry no force. Nothing here is validated vessel timing.

S = sourced, D = derived, E = estimated. Archive = `doc257.pdf` (SHA-256
`841d1aeb...e4b`, rechecked 2026-10-24), one-based pages, read from the scanned pages.

## 1. Evidence read for this part

| Page | Item | Value | Applicability |
|---|---|---|---|
| p4 | Hatch scheme | Middle covers move under the raised end covers, which are lowered onto them; the paired covers are moved by winch: one hold fully open or both 50% | 507B covered-hold arrangement; the archived lift-and-roll mechanism, no hinges |
| p4 | Hatch control | Remote, from a post on the port bridge wing | Matches the HUD machinery panel role |
| p4 | Hydraulic jacks | 16 vertical, 4 horizontal; 7 t each at 100 kgf/cm2 | S; piston area 70 cm2 derived |
| p4 | Pump | EMN-1.5/95-1: 1.5 m3/h, 100 kgf/cm2, 8 kW motor; hand pump RNG-50 in reserve | S |
| p4 | Cover winch | LE-61, 2 units, 4 tf pull, MAP211-6 7 kW | S; line speed derived |
| p5 | Main engine | G60 (6ChRN 36/45), reversible, 900 hp, 375 rpm, compressed-air start up to 30 kgf/cm2, pneumatic remote control | S; excludes the G70 of Volgo-Don 101 |
| p5 | Gearbox | TsOM-30 with V-belt drive the shaft generator from the starboard shaft line | No reduction gear is listed for the propeller |
| p5 | Screws | 1.8 m, 4 blades, pitch port 1.52 / starboard 1.47 m, rotating nozzles | Rotation sense not given |
| p7 | Steering gear | RER7.5-15, twin electric sector; simultaneous and separate nozzle movement; 35 to 35 deg in not more than 30 s on the main drive; 15 tf m; 2 x PNZ-145 6.2 kW (Ward-Leonard); emergency 2 x PNZ-17.5 1.2 kW on batteries | S |
| p7 | Anchors | Hall; bow 2 x 1750 kg, stern 1250 kg | S |
| p8 | Chains | Bow 43 mm x 150 m each, stern 37 mm x 75 m | S |
| p8 | Windlass | BE16-4: 5 tf on the warping drum, 10 m/min raising one anchor from 80 m; remote let-go, brake setting and chain length indication from the wheelhouse; 4 x 0.7 kW remote release motors. First 507B ships: BER-5-2 | S; which windlass the representative ship carries is unknown |
| p8 | Stern capstan on project 507B ships | ShER-1.5/3/2-1: 3 tf, 18 m/min from 40 m | S |
| p8 | Mooring winches | LEGShA2-1, 2 units, 2.5 tf | S; not animated |

No outside source was needed. Modernised configurations (MEB conversion, Part 9) were not used.

## 2. Defects found before the change

Recorded with the Part 12 source and code before any edit:

1. **Real-time rates.** `VesselMechanisms` used 40 s for the whole opening sequence
   (outer lift 3.3 s, inner lift 6.7 s, slide 20 s, lowering 10 s, linear over clip frames)
   and 45 s for the traverse; `mechanisms.json` called them an accelerated review sequence.
2. **Phase carry-over.** A step that finished the opening did not spend its remainder on the
   traverse (one 400 s step from closed stopped at the half-open stack), so the result depended
   on the step size at every phase boundary.
3. **Windlass binding.** `Windlass_Port/Starboard` animated the two mooring-winch drums at
   Y = 57.5 m (X = -1.81 / +3.21 m, asymmetric). The anchor windlass, a single symmetric
   unit with two gypsies on a central gearbox at Y = 60.6 m, was static geometry inside
   `VD507B_detali`. Captures: `Logs/Part13/before-bow-*.png` (Blender, not committed).
4. **Forward outer cover kinematics.** Part 4 keyed it to rise 1.05 m and shift 1.27 m aft
   at the same time. Steel-on-steel collision with the forward inner cover in frames 93 and
   700-703 (`Logs/Part13/hatch-steel-before.json`); Part 4 validated only frames 120-660.
5. **Anchors.** Only a 5 s unseat (0.35 m); no chain payout, limits, heave rate or visible chain.

Checked and found correct: shaft visual rotation 6 deg/s per rpm from the signed actual shaft
speed (reversal, neutral and stop follow the shaft model); nozzles follow the actual actuator
angle, rate-limited by the rudder model, not the command; steering limits.

## 3. Changes

Blender, through registered MCP (Blender 5.2.2, addon 1.8, protocol 14; `look` could not read
its temporary file, so captures were rendered with bpy into `Logs/Part13`):

- `part13_windlass.py`: moves the gypsy hub and outboard cheek of each side (80 faces) out of
  `VD507B_detali` into `WindlassMesh_<side>` under the existing `Windlass_<side>` pivots,
  re-centred on the gypsy axis (X = +/-1.0918, Y = 60.6007, Z = 4.5013 m, diameter 1.32 m), and
  returns the drums to the static deck as `MooringDrumMesh_<side>`. The inboard cheeks stay
  fixed (the port one carries the band-brake handwheel). Pure partition: 14,257 faces with
  identical positions, both UV layers and materials (`Logs/Part13/partition-check.json`).
- `part13_hatch.py`: the forward outer cover now rises straight on its jacks (frames 90-100),
  shifts 1.27 m while raised (100-120, with the inner covers' rolling lift), and on closing
  shifts back before lowering (660-680). Only two Y keys changed.
- `part8_export.py` regenerated the three FBXs and LOD scenes; the report is `part13-source.json`
  (the Part 10 report was restored unchanged). Reopening the saved blend reproduced every mesh
  signature at all LODs: 51 / 59,543, 51 / 36,044, 44 / 24,474 meshes / triangles.

`part13_validate.py` (read-only, `part13-validation.json`): all 15 pivots at their manifest
positions; frames 90-710 (621 frames, including the lift and lowering Part 4 skipped): no
steel-on-steel cover collisions, smallest gap between nearby covers 0.135 m, stacked gaps
0.081 / 0.092 m; the forward outer cover's rubber joint seal rubs the inner cover's end plate
for the first and last 0.3 m of its vertical lift (13 frames), a compressed-seal contact, kept
separate from steel collisions. Gypsies on axis, 36 angles through a full turn, contacts only
on their shaft and the inboard cheek face. Unseated anchors clear of the hull.

Unity:

- `VesselMechanisms`: real-time phase durations, a step spent phase by phase, `HatchPhase`,
  `HatchSecondsRemaining`; anchors pay chain out at the let-go speed until the anchor rests on
  the bottom below its hawse or the chain is all out, heave at the windlass or capstan rate,
  hold wherever stopped; anchors hang plumb on their chain; gypsies turn 360 deg per 1.72 m of
  chain; a line-renderer chain from hawse to shackle (generated texture, two 4 d links per
  tile). `PreviewSpeedMultiplier` is for editor captures only and is not saved.
- `MechanismSave.chainOutM` (three lengths). Saves without it restore from `anchorRelease`;
  `anchorRelease` is still written for older readers. Validation rejects bad lengths.
- `VolgoDonMechanismIntegration` reads the rates from `mechanisms.json` `operating_rates`, the
  single parameter source, and binds anchors, chain ends and gypsies.
- HUD: phase and minutes left, chain out per anchor; the button reads LET GO / HEAVE UP ANCHORS.
- Cargo transfer waits for the hatches only (`HatchesMoving`), not for the anchors.
- Quality gate: stage 13 prefix; reviewed LOD1 budget 36,100 triangles (the separated gypsies
  are small meshes the exporter does not decimate, +72 triangles).

## 4. Parameters before and after

| Parameter | Before | After | Kind, source, range |
|---|---|---|---|
| Outer cover lift (1.05 m) | 3.3 s | 141 s | D: 8 jacks x 70 cm2 x 1.05 m / 1.5 m3/h; 141-282 s (4-8 jacks per cover) |
| Inner rolling lift (0.25 m) + forward outer shift | 6.7 s | 34 s | D, same flow; 34-68 s |
| Inner slide under (22.6 m) | 20 s | 174 s | D: winch 0.13 m/s = 7 kW x 0.75 / 39.2 kN; 125-226 s |
| Outer lowered onto stack (0.12 m) | 10 s | 16 s | E, lift flow; 8-32 s |
| Closed to half open | 40 s | 365 s | sum |
| Stack traverse (48.3 m) | 45 s | 371 s | D, winch speed; 268-483 s |
| Half open to one hold open / hold to hold / open to closed | 45 / 90 / 85 s | 371 / 742 / 736 s | runtime, `part13-machinery.json` |
| Anchor unseat | 5 s, no payout | 0.35 m at the let-go speed | E |
| Let-go speed | none | 1.0 m/s | E, gravity under the band brake; 0.5-2 m/s |
| Bow / stern chain | none | 150 / 75 m | S p8 |
| Bow heave | none | 10 m/min | S p8 BE16-4 rating (80 m depth) |
| Stern heave | none | 18 m/min | S p8 ShER-1.5/3/2-1 (40 m depth) |
| Chain per gypsy turn | uncalibrated | 1.72 m | E: five whelps x two links x 4 d; 1.4-2.0 m |
| Steering rate | 2.5 deg/s (28 s) | unchanged | E inside S <= 30 s; 2.33-4.8 deg/s from 2 x 6.2 kW at 15 tf m |
| Shaft to engine ratio | 1, unconfirmed | 1 | D: reversible engine at the screw rpm, the only gearbox drives the shaft generator |
| Shaft visual rate | 6 deg/s per rpm | unchanged | D |
| Handedness | outward turning, estimated | unchanged | E; p5 gives pitches only |

Propulsion, steering and grounding parameters in `VolgoDon507B.json` are unchanged, so no
numerical trial changes are expected; the all-vessel trial report was rerun to confirm.

## 5. Tests and checks, 2026-10-24

All runs: Unity 6000.6.0f1, exit 0, no `error [A-Z]` lines.

- Blender: `part13_validate.py` PASS before saving and again after reopening the saved blend.
- Unity quality gate: `VOLGODON_QUALITY|PASS`, `Logs/Part13/quality.log` (first run failed:
  `Configure` sampled anchors of the replaced visual; fixed by clearing the bindings).
- Machinery review: `MACHINERY_REVIEW|PASS`, `Logs/Part13/machinery.log`; cover-to-cargo
  clearance over the whole cycle (2,214 samples at 1 s, full cargo): at least 1.93 m by
  bounding boxes; chain on the bottom 6.6 m bow / 6.2 m stern in 5.3 m of water.
- EditMode **251/251**, `TestResults/check-EditMode-20261024-013717-860.xml` (first run 249/251:
  two new tests left less than the end-position tolerance before the end of a phase).
  New: phase durations in order, one long step equals 21,000 short ones, interrupted opening
  reverses along its path in the time it ran, chain limits and heave rates, gypsy quarter turn,
  save mid-payout, old save without chain, preview multiplier not saved.
- PlayMode **70/70**, `TestResults/check-PlayMode-20261024-014241-663.xml`. New
  `MechanismTimingTests`: pause stops machinery, double simulation speed applies once,
  10 and 50 frames per second give the same rate, vessel reset stows everything.
- Menu: `MENU_SMOKE|PASS`, `VOLGODON_RUNTIME|PASS`, `VOLGODON_LOADING_UI|PASS`,
  `Logs/check-Menu-20261024-014438-652.log`; it now restores a mid-operation opening and chain
  length captured before the save.
- Shakedown: `VESSEL_SHAKEDOWN|PASS: 5 vessels`, `Logs/check-Shakedown-20261024-014838-147.log`.
- Sea trials: `SEA_TRIALS|PASS`, `Logs/Part13/seatrials.log`; every row identical to
  `part12-sea-trials-after.md`, as expected with no propulsion or steering change.

## Images

`Previews/part13-machinery-*.png` (Gorodets water, full cargo): closed, outer-lifting,
inner-lifted, inner-sliding (LOD0, LOD1, LOD2), half-open, traversing, fore-open, aft-open,
anchor-letting-go, anchors-on-bottom (LOD0, LOD2), stern-anchor, anchors-heaving,
anchors-stowed, windlass, nozzles at both limits. Quality-gate captures `Previews/part13-unity-*`.

## Remaining limits

- Jack count per cover, winch speed at light load, lowering rate, let-go speed and gypsy
  geometry are estimates; actual operating times of a given ship may differ by the ranges above.
- The model has 12 jack pedestals against 16 + 4 in the archive; missing units are not invented.
  Inner covers rise without visible jacks; the forward outer cover's 1.27 m shift is assigned to
  the horizontal jacks by assumption. Hoses, wire ropes and winch drums are not animated.
- Separate nozzle steering (p7) and the emergency steering drive are not modelled.
- One HUD command lets go or heaves all three anchors together; bow and stern are not separated.
  The chain is a straight line from hawse to shackle; chain lying on the bottom is not drawn,
  there is no catenary, and the anchor follows the ship. No holding force: Part 14.
- Whether the representative ship carries BE16-4 or the early BER-5-2 windlass is unknown;
  the BE16-4 rate is used.
