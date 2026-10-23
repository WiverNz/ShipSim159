# Part 11: section hydrostatics, cargo and ballast

Date: 2026-10-23. Status: complete as a prototype loading-response stage.
The hull geometry, FBX exports, prefab, colliders, mechanisms and controls are
unchanged. Part 12 calibration has not started. Nothing here is a stability
booklet, a certified loading calculation or validated training data.

## Inputs and provenance

| Input | Value | Kind | Source / note |
|---|---|---|---|
| Hydrostatic surface | 4,437 vertices, 8,870 triangles, cap 5.5 m above keel | D | `VD507B_HydrostaticSurface` exported unchanged through registered Blender MCP to `part11-hydrostatic-surface.json` (SHA-256 `defb4c29...e5c`); the blend was not modified or saved |
| Hull reference | project-507 family body plan | D/E | Part 10; maximum trace residual 0.743 m unresolved; estimated AP/FP station mapping; ends and the loft above Z=0 estimated |
| Water density | 1000 kg/m3 | E | fresh river water, unchanged |
| Hold boxes | fore z 10.09..53.35 m, aft z -40.80..3.82 m, width 11.5 m, floor y -2.60 m | D | measured from the authored `HoldFloor_*`, `HoldLining_*`, `HoldEnd_*` meshes in the Blender source |
| Cargo block height | 3.0 m above the hold floor | E | retained Part 7 estimate; implies about 1.6 t/m3 stowage, no cargo type is modelled |
| Ballast groups | same longitudinal extents as the holds, y -2.5 m, 14 x 2 m section | E | Part 7 estimate, now aligned with the hold layout; no tank geometry exists |
| Lightship mass | 1750 t | D/E | unchanged Part 7 derivation |
| Lightship LCG | -21.9 m (was +2.0 m) | E (derived from a convention) | see below |
| Section table | 161 stations, 2,736 offsets, 6 strips per side, 0.05 m column grid | D | `part11_hydrostatics.py --write-vessel`, `hydrostatics.sectionTable` in `VolgoDon507B.json` |

S = sourced, D = derived, E = estimated. Coordinates: Unity x starboard, y up,
z forward, metres; keel at y = -3.53 m; Blender (x, y, z) maps to Unity (x, z, y).
The source landmarks were rechecked: propellers at Blender y = -64.65 m match the
JSON propeller positions, so the hydrostatic surface shares the physics datum.

### Loading-centre decision

The reconstructed hull's LCB at the 6750 t condition is **-0.91 m**. The Part 7
inventory put the full-load LCG at **+2.0 m** because the wall-sided prisms were
configured with LCB = +2.0 m. On real sections that inventory trims the loaded
ship about 0.9 m by the bow. With the holds moved to the authored hold geometry
alone, it would be about 2 m. Neither the archive nor any other source gives a
lightship LCG, and +2.0 m is implausible for a ship with aft machinery and
accommodation.

Resolution, recorded here so it can be revisited: hold and ballast extents follow
the authored geometry (derived), and the lightship LCG is re-estimated with the
design convention that the design full-load condition floats on even keel:
`LCG_light = (6750 t x LCB(3.494 m) - sum(payload moments)) / 1750 t = -21.87 m`,
rounded to -21.9 m. This is an estimate tied to a stated convention, not a
measured weight distribution. The hull geometry was not altered, and no scaling
was applied to force 6750 t at 3.53 m. Other loading conditions are predictions
of this estimate; the lightship's large stern trim follows from it.

`massProperties.centerOfMassLocalM` is updated to the resulting full-load
centre (0, -0.8444, -0.9145) so the declarative value agrees with the inventory.

## Method

1. **Reference (exact).** `part11_hydrostatics.py` clips the closed triangle mesh
   by the plane `z = keel + T + (trim/135) y`. Volume, moments and waterplane
   integrals use divergence-theorem fields that vanish on the plane, integrated
   with a quadratic-exact edge-midpoint rule. It reproduces the Part 10 volumes:
   11,013.6887 vs 11,013.6887 m3 capped, 6,824.7409 vs 6,824.7409 m3 at Z=0.
2. **Offset table.** Candidate stations every 0.25 m plus every mesh ring are
   sliced exactly. Each half-section keeps its true breakpoints, simplified to
   2 mm, with breadth zero below the first offset so flat bottoms keep their
   width. A Douglas-Peucker pass along the length keeps a station wherever linear
   interpolation of the sectional-area (Bonjean) curves would err by more than
   0.05 m2; the middle body is then filled to 2 m spacing because long panels
   lose accuracy under trim.
3. **Runtime columns.** `SectionHydrostatics` builds, once per vessel data, a
   cumulative area and keel-moment table for every station and strip on a
   0.05 m grid, integrating the piecewise-linear overlap exactly (trapezoid for
   area, Simpson for moment). Stations are trapezoidal; strips are vertical
   columns. The Python `Runtime` class mirrors this algorithm for the check.
4. **Equilibrium.** Newton iteration on draft at z = 0 and trim slope, with the
   Jacobian from waterplane area and its first and second moments. Conditions:
   displaced volume = mass / rho, and buoyancy and gravity on one vertical,
   `(LCB - LCG) + (KB - KG) tan(trim) = 0`. Tolerance 1e-5 of volume.
5. **Forces.** `HydrostaticsModel` keeps its interface. For a vessel with a table it
   immerses each column to the local (squat-lowered) water surface, adds its
   buoyancy at the column centroid with heave damping proportional to its
   waterline breadth, sums force and torque in C#, and applies one force and one
   torque. Draft, trim and heel therefore come from the same tables as the
   static solution. Roll damping is unchanged in form, using the tabulated GM.

Small heel is not solved statically: it emerges from the columns under the
actual mass, CG and inertia. GM reported in loads is `KB + I_T / V - KG`.

### Accuracy

| Check | Worst over 0.6-4.0 m drafts (even keel); trimmed cases +/-0.75, +/-1.5 m at 1.0, 2.2, 3.5 m | Gate |
|---|---|---|
| Runtime columns vs exact clipping, volume | 0.003% even keel, 0.004% trimmed | 0.5% |
| Runtime vs exact, LCB | 0.002 m / 0.005 m | 0.02 m |
| Runtime vs exact, KB | 0.00002 m / 0.0003 m | 0.02 m |
| Runtime vs exact, waterplane area / I_T / I_L | 0.25% / 0.36% / 0.64% | reported |
| Runtime vs exact, LCF | 0.08 m | reported |
| Refinement (tolerance /4, candidates 0.125 m, filler 1 m, grid 0.025 m, offsets 1 mm) | 0.002% volume, 0.0015 m LCB, 0.00001 m KB | Part 9: 0.5%, 0.02 m |

The gates are the Part 9 convergence tolerances fixed before fitting. They
measure numerical agreement with the reconstructed surface, not agreement with
the real ship. A brute-force check with 1,490 stations converged to the exact
values within 0.0002% volume and 0.0001 m LCB. The first, coarser attempts failed
the LCB gate (0.046 m at uniform 2 m stations, 0.021 m with 4 m filler) and were
traced to station density, not relaxed. LCF and waterplane values come from
finite height steps and are less precise than volume and centroids.

## Hydrostatic curves

Full table, 0.1 m steps: `part11-hydrostatic-curves.csv`; method, checks and
worst errors: `part11-hydrostatics.json`. Selected even-keel values (exact):

| Draft m | Volume m3 | LCB m | KB m | Awp m2 | LCF m | TPC t/cm | BMt m | Cb (Lpp 135, B 16.5) | Cwp |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1.0 | 1763.0 | +0.209 | 0.517 | 1879 | -0.17 | 18.79 | 22.60 | 0.792 | 0.844 |
| 2.0 | 3693.1 | -0.156 | 1.033 | 1976 | -0.86 | 19.76 | 11.47 | 0.829 | 0.887 |
| 3.0 | 5714.9 | -0.613 | 1.553 | 2079 | -2.44 | 20.79 | 7.80 | 0.855 | 0.933 |
| 3.5 | 6761.6 | -0.908 | 1.816 | 2105 | -2.53 | 21.05 | 6.70 | 0.867 | 0.945 |
| 4.0 | 7818.4 | -1.124 | 2.078 | 2120 | -2.42 | 21.20 | 5.85 | 0.878 | 0.952 |

Comparison only: the archive gives 6678 t at a 3.50 m mean draft and coefficients
Cb 0.861, Cwp 0.935 at 3.5 m. The reconstruction displaces 6762 m3 there, 1.3% more,
with Cb 0.867 and Cwp 0.945. The difference is reported, not fitted away. The
selected 6750 t prototype mass floats at a 3.494 m mean draft, 0.036 m above the
3.53 m reference waterline of the visual model.

## Declared range and out-of-range behaviour

Declared range: mean draft 0.6-4.0 m, trim within +/-1.5 m over 135 m. Inside it,
the accuracy table applies. The surface itself spans 0-5.5 m above the keel.

| Situation | Behaviour |
|---|---|
| Dry column, non-finite immersion | No force; lookup returns zero area, moment and breadth |
| Column immersed above the 5.5 m cap | Area saturates at the capped value; no waterplane, no extra buoyancy |
| Solved but outside declared range (lightship, single-hold cases) | `HydrostaticRange.OutsideDeclaredRange`; values are still used; HUD appends `OUTSIDE TABLES` |
| Mass above capped volume (11,014 t) | `Unsolved`; draft reported at the cap, capped state; the dynamic hull sinks to the cap and saturates without NaN |
| Zero, negative or NaN mass/centre, non-convergence | `Unsolved`, finite zero/last values; no exception |
| Free-floating extreme trim or heel | Columns handle any attitude; deck-edge immersion, downflooding and capsize are not modelled |

The loading budget already prevents payload beyond 5000 t; the overload path
is reachable only through invalid or edited data. Negative buoyancy cannot occur:
column areas are non-negative cumulative integrals and each column force is
clamped at zero after damping, as in the wall-sided model.

## Free surface

Not applied. The archive tank table gives volumes and frame intervals without the
breadths, subdivision and wing/double-bottom geometry needed for free-surface
moments (Part 9). A single 14 m box per ballast group would overstate the effect
several times over (about 2.5 m of GM at the ballast condition), so surrogate tanks
would invent a large, unsupported correction. GM in this stage is the solid-mass
value. Liquid sloshing, fuel and ballast free surfaces, and cargo shift are
unsupported and listed as limitations.

## Load cases

`VolgoDonHydrostaticsReview.Run` writes `part11-loading.json` (all conditions,
inertia, propeller immersion, and the wall-sided values for comparison).
Drafts at the perpendiculars are DraftAt(+/-67.5 m). Speed and stop come from
the unchanged Part 7 deep-water manoeuvring model under each condition.

| Condition | Mass t | Draft m | Trim m | Fwd / aft draft m | GM solid m | Range | Wall-sided draft / GM | Full speed m/s | Stop m / s |
|---|---:|---:|---:|---|---:|---|---|---:|---|
| Full load | 6750 | 3.495 | -0.002 | 3.49 / 3.50 | 5.84 | Supported | 3.530 / 4.67 | 5.49 | 716 / 285 |
| Half deadweight | 4250 | 2.265 | -1.134 | 1.70 / 2.83 | 8.49 | Supported | 2.223 / 7.13 | 5.82 | 525 / 193 |
| Lightship | 1750 | 0.941 | -2.588 | -0.35 / 2.23 | 19.18 | Outside | 0.915 / 18.50 | 6.20 | 283 / 91 |
| Both ballast groups + stores | 4150 | 2.214 | -1.206 | 1.61 / 2.82 | 9.43 | Supported | 2.170 / 8.08 | 5.84 | 516 / 189 |
| Forward ballast + stores | 3050 | 1.671 | -0.171 | 1.59 / 1.76 | 12.03 | Supported | 1.595 / 10.68 | 6.07 | 427 / 147 |
| Forward hold only + stores | 4350 | 2.330 | +2.189 | 3.42 / 1.24 | 8.30 | Outside | 2.275 / 6.99 | 5.34 | 495 / 195 |
| Aft hold only + stores | 4350 | 2.211 | -4.210 | 0.11 / 4.32 | 8.38 | Outside | 2.275 / 6.99 | 5.74 | 521 / 194 |
| Half deadweight, 1000 t forward | 4250 | 2.282 | +0.218 | 2.39 / 2.17 | 8.42 | Supported | 2.223 / 7.13 | 5.83 | 526 / 193 |

A negative forward draft means the forefoot clears the water. The lightship and
single-hold cases are deliberately unbalanced: real operation would add forward
ballast, which the forward-ballast case shows (trim -0.17 m). The full-load
stop (716 m / 285 s) is essentially the Part 9 baseline; the Part 12 calibration
item is unchanged.

## Cost and allocations

Measured by `VolgoDonHydrostaticsReview.Run` on the warmed editor thread,
1000 iterations, written to `part11-loading-cost.json`. Allocation counts are
`GC.Alloc` profiler samples from `UnityEngine.Profiling.Recorder`, verified to
register a known allocation. Editor Mono timings are not player measurements.

| Operation | Before optimisation | After | Wall-sided |
|---|---:|---:|---:|
| Section geometry build (once per vessel data) | 6.1-7.2 ms, 965 allocations | cached | n/a |
| Parameter solve per transfer step | 0.54 ms (cold) | 0.15 ms (warm start) | n/a |
| Full loading rebuild per transfer step | 0.56 ms | 0.16 ms, 13 allocations | 0.007 ms, 16 allocations |
| Hydrostatic force evaluation per physics step | 138 us (vector code) | 108 us, 0 allocations | 17 us, 0 allocations |

Confirmed problems and responses: uncached geometry would cost about 6 ms on
every transfer step, so it is built once per vessel data object (weak-keyed
cache). The cold Newton solve dominated a transfer step, so the controller seeds
it with the previous condition. A test confirms warm and cold solutions agree
within 1e-4 m. The 1,932-column loop was rewritten with scalar
arithmetic and early exits for dry stations. The remaining 108 us per 20 ms step
(about 0.5% of a core at 1x, 2% at 4x simulation speed) was accepted; reducing
strips or stations would trade away the accuracy above. The 13 small allocations per
transfer step come from parameter and model objects shared with every vessel.
No player profile was taken; that remains the separate Part 8 item.

## Tests and checks

All runs on 2026-10-23, Unity 6000.6.0f1, through `Tools/shipsim-check.ps1` or the
direct launches in `AGENTS.local.md`; every run exited 0 with no `error [A-Z]` lines.

- Generator: `python3 -I part11_hydrostatics.py` prints `PART11_HYDROSTATICS|PASS`.
  Rerunning with `--write-vessel` reproduces `VolgoDon507B.json` byte for byte.
- Compile: `Logs/check-Compile-20261023-202236-091.log`, clean.
- EditMode: **243/243**, `TestResults/check-EditMode-20261023-203853-838.xml`
  (228 before; 15 new in `SectionHydrostaticsTests`: exact-mesh agreement, capped volume,
  equilibrium for five conditions, even-keel full load, longitudinal bias, out-of-range
  and lookup safety, warm/cold solve, geometry cache, other vessels unchanged, disabled
  table fallback, validator, transfer mass conservation, zero-duration step).
- PlayMode: **56/56**, `TestResults/check-PlayMode-20261023-204143-895.xml` (47 before).
  New: full, half, ballast and forward-biased conditions settle within 0.03 m of the
  solved keel depth at both perpendiculars and midship, with velocity below 0.01 m/s;
  heel under a known moment matches the tabulated GM (1.5 +/- 0.15 deg); force
  evaluation allocates nothing; a transfer keeps the same shaft objects and RPM within
  0.01 rpm of an identical ship without transfer and conserves mass (+100 kg in 2 s);
  `timeScale = 0` stops transfers and motion, resuming continues them; save/restore
  mid-transfer reproduces draft within 1e-4 m and trim within 1e-3 m and continues.
- First PlayMode run: 55/56. `UnloadingGroundedVessel` compared `Body.position`
  exactly after `ConfigureLoading(0)`. Moving the CoM about 20 m changed the last float
  bits, so the test now requires under 0.1 mm. The test also follows the
  stern-trimmed lightship clearance instead of the old level 0.9 m draft.
- Load cases, probes, other-vessel handling identity, cost:
  `VolgoDonHydrostaticsReview.Run` exit 0, `VOLGODON_HYDROSTATICS|PASS`,
  `Logs/VolgoDon507B/part11-review.log`.
- Sea trials before/after: `SEA_TRIALS|PASS` both, `Logs/VolgoDon507B/part11-trials-{before,after}.log`,
  reports copied to `part11-sea-trials-{before,after}.md`. All non-507B rows and
  KVLCC2 are identical.
- Shakedown: `VESSEL_SHAKEDOWN|PASS: 5 vessels`, `Logs/check-Shakedown-20261023-204313-891.log`;
  the 507B HUD shows `DRAFT 3.49 m TRIM +0.00 m` (`Logs/Shakedown/volgodon-507b-3-port.png`).
- Menu: `MENU_SMOKE|PASS`, `Logs/check-Menu-20261023-204425-884.log`.
- Blender: registered MCP, addon 1.8, protocol 14, Blender 5.2.2. The surface export was
  read-only. Part 10 validation and FBX export were not rerun because no geometry changed.

### Numerical trials, 507B rows (Lpp units unless stated)

| Condition | Full kn | Advance | Tactical D | Steady D | 10/10 deg | Crash stop | Stop s |
|---|---|---|---|---|---|---|---|
| Loaded deep, before / after | 10.7 / 10.7 | 3.77 / 3.66 | 4.60 / 4.29 | 3.70 / 3.42 | 3.0-3.5 / 3.7-4.6 | 5.32 / 5.30 | 286 / 285 |
| Loaded 8.2 m channel | 9.7 / 9.7 | 3.68 / 3.56 | 4.61 / 4.30 | 3.81 / 3.51 | 2.5-2.9 / 3.1-3.7 | 4.44 / 4.44 | 261 / 261 |
| Loaded 4.6 m Gorodets | 7.5 / 7.5 | 5.77 / 5.44 | 9.27 / 8.56 | 8.94 / 8.21 | 0.7 / 0.8 | 2.66 / 2.67 | 203 / 203 |
| Lightship deep | 9.4 / 12.0 | 2.95 / 2.08 | 3.67 / 2.13 | 3.50 / 1.95 | 2.1-2.5 / 4.7 | 1.92 / 2.09 | 107 / 91 |

Loaded turning tightens by about 7% mainly because x_G moved from +2.0 m to
-0.91 m in the unchanged MMG model; draft changes only from 3.53 to 3.495 m. The
lightship gains speed because the stern-trimmed screws are now fully immersed
(local draft 2.2 m) instead of 45% immersed at a level 0.92 m draft, and turns
much tighter with x_G at -21.9 m. These are consequences of the estimated
loading, not calibrated results. Matching published trials belongs to Part 12.

## Limitations

- The hull is a family-guided reconstruction with a 0.743 m unresolved trace
  residual, estimated station mapping, and estimated ends and topsides. Agreement
  is with that surface, not the real 507B. The 1.3% volume excess at 3.5 m
  against the archive is unexplained and unfitted.
- Lightship LCG is a convention-derived estimate; cargo stowage height, ballast
  geometry and capacities, stores centre and all transfer rates remain estimates.
  Changing any of them changes every predicted trim.
- No free-surface correction, cargo shift, sloshing, downflooding, deck-edge
  immersion, large-angle stability, longitudinal strength or bending.
- Columns are vertical in the body frame. Heel is resolved dynamically through
  immersion differences, accurate for small angles only. Transverse loading
  asymmetry is still rejected by validation.
- Hull forces, resistance, wetted surface and added mass still use the mean draft
  through the existing Clarke/Mumford scaling; trim affects them only through mean
  draft. Propeller immersion and bow thruster submergence now use the local draft
  at their stations. Part 12 owns any recalibration.
- The archived 6678 t / 3.50 m condition and the selected 6750 t prototype
  remain separate. The visual model is not repositioned for the 0.036 m higher
  equilibrium waterline.
