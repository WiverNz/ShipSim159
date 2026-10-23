# Part 10: sectional hull reconstruction

Completed 2026-10-23 with an explicitly unresolved family-reference discrepancy.
The existing two-covered-hold ship remains the target. This stage replaces its
sparse lower hull shell, retains the working upper model and integrates updated
exports. It does not change buoyancy, propulsion, mass, controls or colliders.

## Deliverables

- Editable source: `Art/Source/VolgoDon507B/VolgoDon507B.blend`.
- Exports: `Assets/ShipSimulator/Models/VolgoDon507B_Improved/VolgoDon507B.fbx`,
  `VolgoDon507B_LOD1.fbx` and `VolgoDon507B_LOD2.fbx`.
- Raw source pixels and conversion assumptions: `part10-trace.json`.
- Converted samples: `part10-traced-sections.csv`.
- Interpolated geometry: `part10-derived-sections.json`,
  `part10-derived-waterlines.csv`, `part10-derived-buttocks.csv`.
- Geometry, residuals and repeatability: `part10-validation.json`,
  `part10-trace-residuals.json`, `part10-reproducibility.json`.
- Unity import and resources: `part10-unity.json`, `part10-render-cost.json`,
  `part10-loading-cost.json`. Editor measurements are not player acceptance.

## Evidence and fitting limits

Use [REFERENCE_DECISIONS.md](REFERENCE_DECISIONS.md) and
[PART10_METHOD.md](PART10_METHOD.md) together. The source is page 2 of
[the archive PDF](https://russrivership.ru/public/files/doc257.pdf), explicitly
labelled project 507, with a 600 x 316 pixel embedded body plan. It is a family
shape reference, not authenticated 507B offsets. No source photograph or scan
was copied into game textures.

Thirteen labelled section samples retain native raster coordinates. The centre,
baseline and waterline skew are recorded; half-beam is assigned 8.25 m and the
labelled waterline 3.53 m above keel. Equal station spacing over 135 m is estimated,
including AP/FP placement. Unreadable and merged portions are not exact evidence.
Waterline and buttock tables are derived, not independent source measurements.

The fit preserves the original waterline and uses monotone section envelopes
with longitudinal interpolation. There are 147 lower-shell section rings.
The largest nearest-point distance from a traced point to its fitted section
is **0.7428 m**. This exceeds the fixed three-native-pixel tracing tolerance.
The discrepancy remains unresolved because source applicability, station mapping
and the retained waterline constrain different geometry. Do not relabel the fit
as exact or widen the tolerance after fitting. The residual JSON also retains
same-height breadth differences for diagnosis; these are ambiguous on flat bottoms
and are not the primary fit-error metric. The station-0 end sample lies beyond
the retained waterline section range and is not counted as a measured fit there.

## Geometry and materials

The visual hull changes from 544 to 6,957 triangles. Geometry above the selected
waterline is retained, with boundary edge splits for the new shell. The other
48 source meshes match the Part 9 geometry hashes. Lower-shell UVs are transferred
barycentrically from the original triangles; the second channel retains the
legacy inverted-V relationship. Existing materials and packed textures are retained.
No new PBR texture set or invented fittings are introduced in this stage.

`VolgoDon507B_Part9_Hull` archives the original hull in the blend; the JSON snapshot
is `part10-original-hull.json`. `VolgoDon507B_Hydrostatic` contains the separate
`VD507B_HydrostaticSurface`. Neither archival nor hydrostatic geometry is exported.
The original CRYENGINE FBX and its materials remain unchanged.

The hydrostatic solid is a symmetric sectional derivative, closed at Z=1.97 m,
5.5 m above keel. Its dry end sections and upper loft are estimated from existing
hull intersections. It is not the cosmetic hull with every opening sealed.
It has 4,437 vertices and 8,870 triangles, one connected component, no boundary
or non-manifold edges, positive winding/volume, no zero-area triangles and no
non-adjacent triangle intersections detected by the mesh check.

Full capped volume is **11,013.69 m3**; volume below Z=0 is **6,824.74 m3**.
These are derived geometric values, not a verified displacement certificate.
No scaling was used to force agreement with 6,750 t or the separate archive case.
The cap and estimated end sections constrain future loading/trim calculations.
Part 11 must assess the useful draft range and volume integration accuracy before
connecting this surface to physics. Existing prototype hydrostatics remain active.

## Integration and checks

| Level | Triangles | Renderers |
|---|---:|---:|
| LOD0 | 59,543 | 49 |
| LOD1 | 35,972 | 49 |
| LOD2 | 24,362 | 42 |

The reviewed LOD0 budget is now 60,000 triangles. LOD1/2 stay below the existing
36,000/25,000 budgets. Geometry-count assertions remain enabled. Imported bounds
are 138.29999 m long and 16.70003 m wide, within the 0.02 m integration tolerance.
The importer preserves the physics root, three compound box colliders and bindings.
Textures remain 25 imported resources, 82,087,012 bytes by Unity's accounting.

- Blender validation: PASS; no open/non-manifold edges below the waterline.
- Other 48 meshes unchanged; 81 protected legacy, physics and parameter files
  match Part 9 hashes, including all 45 legacy model files.
- Mechanism regression: 541 hatch frames and 720 shaft/nozzle configurations;
  stack clearance 0.080539 m, anchor checks pass, deliberate collision is detected.
- New hull clearance: 720 blade/duct configurations, steering from -35 to +35
  degrees and propeller phases every 15 degrees. No blade/duct contact detected.
  Shaft and stock penetration into the hull is intentional and excluded.
- Reopened saved blend and regenerated all exports: identical mesh signatures.
  All 33 packed file images decoded. Initial unloaded image buffers required
  pixel access after reopening; they were not missing resources.
- Unity quality gate: PASS, `Logs/VolgoDon507B/part10-quality.log`.
- EditMode: 228/228, `TestResults/check-EditMode-20261023-170904-354.xml`.
- PlayMode: 47/47, `TestResults/check-PlayMode-20261023-171008-662.xml`.
- All-vessel shakedown: PASS, 5/5, exit 0;
  `Logs/check-Shakedown-20261023-171424-636.log`. This short runtime run verifies
  integration, not steady full speed or reference manoeuvring accuracy.

## Review images

[Section comparison](Previews/part10-section-comparison.svg) shows blue source
samples and orange fitted sections without reproducing the copyrighted raster.

| Front | Side | Top | Perspective |
|---|---|---|---|
| [Image](Previews/part10-unity-front.png) | [Image](Previews/part10-unity-side.png) | [Image](Previews/part10-unity-top.png) | [Image](Previews/part10-unity-perspective.png) |

[Underbody](Previews/part10-underbody.png), [stern clearance](Previews/part10-stern.png)
and [Unity underwater](Previews/part10-unity-underwater.png) expose the new shell.
Additional `part10-unity-*` and `part10-lod2-*` images cover bridge, hatches,
lightship, night, LODs and transition distances. Historical Part 8 captures remain.

## Reproduction and next work

Use registered Blender MCP, with the saved source open at frame 1. Run
`part10_hull.py` only to reconstruct the shell from its archived original; then
`part10_validate.py`. Run `part8_export.py` to regenerate the three FBXs and LOD
scenes. It detects the Part 10 scene marker and writes `part10-source.json`,
preserving historical Part 8 reports. The Part 4 mechanism validator can be run
with its report filename redirected to `part10-mechanisms.json`.
Run `ShipSimulator.Editor.VolgoDonQualityGate.Run` in a dedicated Unity editor
to import, install, validate and capture the candidate.

Part 11 remains unstarted: derive hydrostatic properties and loading response
through the existing interface. The trace discrepancy, unverified AP/FP mapping,
visual propeller height versus physics thrust height, and uncertain machinery
timing remain documented limitations. Built-player performance is still open.
