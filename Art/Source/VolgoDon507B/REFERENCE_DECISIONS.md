# Part 9: reference decisions and frozen baseline

Date: 2026-10-23. Status: complete as an evidence and baseline stage.
No geometry, material, prefab, runtime code or vessel parameter was changed.
Part 10 is not started. Missing historical information below remains unresolved,
not silently replaced by measured-looking estimates.

## Target decision

Retain an unshortened, representative classic Project 507B with two covered holds,
aft accommodation and two steerable propeller nozzles. Do not assign a vessel name,
construction year, service date or registry identity to the inherited model.
Two hold groups and four moving cover sections are not four separate holds.

The model is not the early open-bunker Volgo-Don 25, and it is not the modernized
four-hold Volgo-Don-max conversion. Volgo-Don 185 photographs are comparison views,
not proof that this mesh depicts that ship or its original construction condition.
Keep existing livery as authored; it is not an authenticated historical paint scheme.

## Sources and permitted use

All URLs were revisited on 2026-10-23. Photo image views were inspected temporarily;
no third-party photographs or PDF pages were copied into the repository or textures.
Archive page numbers below are one-based PDF pages.

| ID | Source | Date / scope | Use and confidence |
|---|---|---|---|
| A | [doc257.pdf](https://russrivership.ru/public/files/doc257.pdf) | Nine-page combined 1565/507B compilation; PDF metadata dated 2009-07-25, not original drawing date | Primary archival scan; read columns and variant notes before adopting a value |
| B | [FleetPhoto 507B project](https://fleetphoto.ru/projects/1697/) | Series history, undated current database | Secondary cross-check; not a ship-specific loading booklet |
| C | [Volgo-Don 185](https://fleetphoto.ru/vessel/4152/) | Database identity; photographs dated below | Identified visual comparison, not an offset source |
| D | [Volgo-Don 25](https://fleetphoto.ru/vessel/12387/) | Early open-bunker configuration | Exclusion reference for hold layout |
| E | [MEB modernization](https://mebspb.com/dry/507B.html) | Akhmat Kadyrov conversion, 2007 delivery listed | Primary designer source for a different configuration; exclude its hold/deadweight changes |
| F | [AL Shipping family sheet](https://alshipping.ru/type-volgo-don/) | Explicitly groups 507, 507A, 507B, 1565, 1565M and 1566 | Operator cross-check only; mixed family data cannot resolve the selected ship |
| G | [Volgaflot commerce](https://www.volgaflot.com/commerce/) | Current fleet marketing | Context only; no individual historical calibration case adopted |

Archive SHA-256: `841d1aeb590193e92ab2a848d6aaa516ec7c1cc498fa2e3dfee6f7db21979e4b`.
The file has 1,215,793 bytes. Re-download and compare this hash before retracing.
Publication/redistribution rights are not established by public availability.
Keep source links, author credits and original measurements separate from game art.

### Archive applicability map

| Page | Figure/table | Decision |
|---|---|---|
| 1 | General arrangement and project history | Arrangement proportions only; the long cargo outline is schematic and does not independently resolve internal hold division |
| 2 | Body sections captioned project 507 | Family shape prior only; not authenticated 507B offsets |
| 3 | Particulars, loading and manoeuvres | Paired entries are 1565 first, 507B second; unpaired entries require case review |
| 4 | Stability, holds, covers, structure | Keep stability cases separate from the selected prototype; use cover sequence and explicit variant dimensions |
| 5 | Engines, screws and electrical plant | G60 machinery baseline; exclude the specifically noted G70 installation on Volgo-Don 101 |
| 6 | Fuel and ballast tables | Candidate compartment evidence, not a complete verified 507B tank geometry model |
| 7 | Steering, waterjet and anchors | Equipment reference; positions, force curves and chain holding are not supplied |
| 8 | Deck equipment and weight breakdown | Variant-dependent weight table conflicts with p3; do not combine columns into one loading condition |
| 9 | Vessel list | Does not identify the FBX and is not a list of verified two-hold reference ships |

### Project 507 lines-plan decision

The embedded page-2 raster is only **600 x 316 pixels**. Rendering a larger PNG
does not add precision. Its station and waterline labels are not a numerical table
of offsets. The scan has visible skew/unequal line slopes. No authenticated mapping
from its project-507 sections to this 507B hull was established.

Part 10 may trace normalized sectional shape, storing original pixel coordinates,
line-width uncertainty and an explicit transformation. It must not call the result
exact 507B offsets. Do not assume equally spaced numbered stations, assign metre
heights to numbered waterlines, or map frame numbers to station numbers without
recording the inference. Physical station positions and scale need independent
constraints. Do not use a selected displacement to distort the scan until it fits.
If no better drawing is found, deliver a documented approximate sectional hull.

## Source and measurement table

S = sourced transcription; D = derived; E = estimate or project selection.
Confidence concerns applicability, not just legibility. Printed precision is not
a certified tolerance. Values in this table are reference decisions, not edits to JSON.

| Quantity | Value and units | Source / locator | Kind; applicability and intended use |
|---|---|---|---|
| Overall length / beam | 138.3 / 16.7 m | A p3 dimensions; B | S, high for family; retain external envelope |
| Calculation length / moulded beam / depth | 135 / 16.5 / 5.5 m | A p3 dimensions | S, high; do not equate 135 m with outer mesh length |
| Selected full mass / draft | 6750 t / 3.53 m | Current JSON; B | E selection; preserve compatibility until Part 11 decision |
| Archive full mass / mean draft, 507B | 6678 t / 3.50 m | A p3 second paired values | S, medium; separate reference condition |
| Archive lightship, covered configuration | 1466 t | A p3 second paired value | S, medium; not the prototype's derived 1750 t |
| Archive ballast condition | 3828 t / 2.11 m mean draft | A p3 second paired values | S, medium; 2200 t ballast is an operating load, not tank capacity |
| Prototype lightship / deadweight | 1750 / 5000 t | Current JSON; 6750 minus 5000 | D/E, low historical confidence |
| Prototype inventory at full load | 4800 t cargo + 200 t stores | Current loading schema | E; 5000 t deadweight is not 5000 t cargo plus stores |
| Covered holds | 2; combined 6370 m3 | A p3 type and p4 second paired capacity | S, medium; not the 6270 m3 family figure |
| Clear hatch openings | 44.4 x 13.1 m each | A p4 hatch dimensions | S, medium; distinguish hatch opening from second-bottom hold length |
| Full-load speed / ballast speed | 20 / 21.2 km/h | A p3 deep still-water speeds | S; case conditions incomplete, comparison only |
| Main engine rating | G60, 375 rpm, 900 metric hp each | A p5 engine block | S; about 662 kW derived per engine; shaft ratio unconfirmed |
| Screws | 2, diameter 1.8 m, 4 blades | A p5 propulsion | S; accepted geometry reference |
| Pitch port / starboard; area ratio | 1.52 / 1.47 m; 0.62 | A p5 propulsion | S; no measured open-water curves or verified rotation convention |
| Steering nozzles | 2; diameter 1.84 m, length 1.66 m | A p7 steering | S; diameter interpretation/profile remain approximate |
| Steering travel / hard-over time | +/-35 degrees; at most 30 s across full range | A p7 steering | S; 2.5 deg/s prototype rate is E and gives 28 s |
| Bow waterjet | 72 kW; pipe diameter 710 mm | A p7 thruster | S; no thrust curve or exact placement established |
| Hull coefficients at draft 3.5 m | waterplane .935; midship .996; block .861 | A p3 bottom-right block | S, medium/uncertain variant; do not overwrite current .90/.98/.851 blindly |
| Archive full-ahead stop | 510 m / 3.5 min (210 s) | A p3 inertia table | S/D; protocol limitations below |
| Archive circulation | 320 m same-side nozzles; 160 m opposite-side | A p3 circulation block | S; not equivalent to a single common-helm tactical-diameter trial |
| Bow / stern anchor mass | 2 x 1750 / 1250 kg | A p7 anchors | S; equipment reference, not holding capacity |
| Bow / stern chain | 43 mm x 150 m each / 37 mm x 75 m | A p8 chain block | S; proposed Part 14 limits after variant review |
| Bow / stern hoisting speed | 10 / 18 m/min at stated 80 / 40 m depth | A p8 windlass/capstan blocks | S; not a universal payout or unseating speed |
| Prototype propeller axis | 1.0 m above keel in loading model | Current JSON | E; differs from measured visual axis below |
| Visual propeller axis | 1.35 m above keel | Source landmarks: -2.18 minus -3.53 | D from authored geometry; reconcile in Parts 10-12 |

The p8 weight table separately gives 1293 t empty and 6755 t full in its second
column. It cannot be substituted for the p3 covered-vessel condition without
resolving configuration and weight categories. No averaging of these values is
authorized. The scan's tank table gives volumes and frame intervals, not sufficient
cross-sections for free-surface moments. Exact tank subdivision and centres remain
unavailable for the selected model.

## Photo comparison and generation exclusions

| Photo link | Identity, date, author | Observed comparison with current model | Limits / decision |
|---|---|---|---|
| [7323](https://fleetphoto.ru/photo/7323/) | Volgo-Don 185, August 2003, Ponch | Raised bow, low cargo run, aft multi-level block and wide front wheelhouse glazing agree qualitatively | Oblique bow view; cannot measure hull length or certify hold count; mast, window proportions and bow curvature need closer review |
| [16084](https://fleetphoto.ru/photo/16084/) | Volgo-Don 185, 2006-08-13, tdima | Covered cargo deck, side operating framework and aft accommodation support the selected appearance | Perspective and loading unknown; model colour/detail arrangement is not an exact match; do not import every 2006 fitting as original |
| [381616](https://fleetphoto.ru/photo/381616/) | Volgo-Don 25, site dated 1970s, PROKOF | Similar family stern/accommodation does not imply the same cargo configuration | Early open-bunker exclusion reference; not a template for replacing the two covers |

Compared with `Previews/part8-front.png`, `part8-side.png` and `part8-top.png`.
The model's two cargo groups are clear in top view. Front-view paint/shading differs
from the photographs; lighting and projection differ, so pixel colour and apparent
width are not dimensional evidence. No calibrated photo camera was recovered.
Do not trace underwater geometry from loaded photographs that conceal it.

MEB's modernization explicitly has four holds, 9360 m3 capacity, 138.74 m overall
length and different draft/deadweight conditions. Exclude these features from this
classic model. AL Shipping's mixed-project sheet and modern operator fleet pages
cannot resolve an individual early configuration. A modern photo's presence on a
507B page alone does not establish whether its coamings or equipment are original.

## Coordinate and datum contract

- Source world coordinates: X starboard, Y forward, Z up, metres. Local mesh axes
  vary; use `matrix_world` when measuring. Source origin is the selected loaded
  waterline at the model's longitudinal centre, not an authenticated midship frame.
- Unity vessel coordinates: X starboard, Y up, Z forward. Datum mapping for measured
  positions is `(x, y, z)_Blender -> (x, z, y)_Unity`; do not apply this as a second
  rotation to the already aligned FBX. Use the existing landmark-based integrator.
- Keel is source Z=-3.53 m; selected waterline Z=0. Heights above baseline/keel are
  therefore `z + 3.53`. For a provisional 3.50 m condition, waterline is Z=-0.03,
  not a rescaled hull. This does not by itself set the correct displacement.
- BowReference is Y=+69.15 m, starboard reference X=+8.35 m. The 135 m calculation
  length is not evidence that actual AP/FP are exactly +/-67.5 m in this model.
- The p1 structural frame labels increase toward the stern. The page-2 stations
  are a different numbering system. Establish their mapping before digitization.
- Current ship forces use forward-positive longitudinal positions and common helm.
  Source LCB/LCG signs must be checked against their own convention before conversion.

## Frozen geometry and trial baseline

`part9-geometry-baseline.json` was measured through registered Blender MCP from
the clean saved source at frame 1. It records per-mesh bounds, topology, hashes
and mechanism/datums. All 49 mesh geometry hashes match Part 8: 53,130 triangles.
Overall extents are 16.7000 x 138.3000 x 16.1651 m in source X/Y/Z.

The hull mesh alone has 544 triangles, 177 boundary edges and 180 non-manifold
edges. This does not mean every boundary is a visible hole in the assembled ship;
it means this individual visual mesh is not a closed displacement solid. Do not
report its signed mesh volume as hydrostatic displacement. Retain it for comparison
while creating a separate closed hydrostatic surface in Part 10.

`part9-baseline.json` freezes 91 file hashes, current vessel parameters, historical
Part 7 load cases and a freshly run all-vessel numerical trial report. All 45 legacy
resource hashes still match the Part 1 baseline. Historical loading cases are
explicitly distinguished from the new planar trial run; they were not rerun here.

Fresh trial: `ShipSimulator.Editor.VirtualSeaTrials.Run`, exit 0,
`SEA_TRIALS|PASS`, `Logs/VolgoDon507B/part9-trials.log`, no compiler errors.
The copied report retains its generated date and all six vessel/benchmark entries.
Current deep-water full-load values, rounded as in that report: 10.7 kn full speed,
advance 3.77 Lpp, tactical diameter 4.60 Lpp, steady diameter 3.70 Lpp,
stop track 5.32 Lpp and stop time 286 s. With Lpp=135 m the rounded stop is 718.2 m;
the more precise historical loading result is about 719 m. Do not interpret rounding
differences as changed physics.

Trial code approaches for 2400 s at dt=0.2 s, resets the track/time, then uses
dt=0.05 s for manoeuvres. Crash stop commands full astern immediately; the shaft
model handles reversal internally. The archive describes ahead, stop, astern, but
does not state the stop dwell, actual initial speed, depth or loading for that table.
It specifies deep still water for the separate speed row, not unambiguously for
all manoeuvres. Turning angle/speed and tactical versus steady definition are absent.
Thus 510 m / 210 s and 320 / 160 m are comparison evidence, not direct pass/fail
targets. Part 12 must define matched procedures before any calibration.

## Tolerances fixed before fitting

These are engineering acceptance rules, not historical measurement accuracy.

| Comparison | Rule established now | Reason / uncertainty |
|---|---|---|
| Existing source to export/integration | 0.02 m envelope/datum discrepancy maximum | Numerical pipeline check, not source accuracy; preserve selected dimensions |
| Page-1 silhouette tracing | Start with +/-2 native scan pixels plus half visible stroke width; convert via documented scale | Reassess local skew with independent landmarks; no global precision claim |
| Page-2 sections | +/-2 native pixels plus half stroke width after documented rectification | 600 px raster; roughly 250 px per half-beam implies about 0.066 m for two pixels if scaled to 8.25 m; scale itself remains hypothetical |
| Ambiguous/merged lines | Exclude the sample or record an interval, never a fabricated centre point | No usable error bound where line identity is unclear |
| Photo proportions | Qualitative only until camera/scale and loading are constrained | Perspective and refits dominate pixel precision |
| Closed-hull numerical integration, Part 11 | Mesh/table refinement changes volume by <0.5%, buoyancy centre by <0.02 m over the declared range | Proposed convergence gate, not agreement with true vessel displacement |
| Hydrodynamic time integration, Part 12 | Halving dt changes stop/diameter metrics by <1% before fitting | Separates solver error from uncertain calibration |
| Full-scale stopping/turning agreement | No numerical acceptance band yet | Missing comparable trial conditions; report sensitivity ranges first |

Part 10 must publish selected scales, station interpretation and sample uncertainty
before editing the hull. If new higher-resolution evidence changes these bounds,
record the reason before refitting, rather than widening tolerances after failure.

## Completion and handoff

Delivered: this decision package and measurement table, dated photo links with
usage restrictions, read-only capture scripts and frozen geometry/physics evidence.
All nine archive pages and the three listed photographs were visually inspected.
No new rendering, modelling or runtime test suite was needed for documentation;
the numerical trial runner was executed to establish the current baseline.

Part 10 can proceed with approximate family-guided sections and explicit uncertainty.
Still unavailable: authenticated 507B offsets for a named vessel, a loading booklet,
complete tank geometry, calibrated manoeuvring trials, main-drive ratio and propeller
curves. These limits do not justify replacing the existing ship or claiming exactness.
