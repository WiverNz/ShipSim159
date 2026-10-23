# Volgo-Don 507B editable source

## Current milestone: Part 11 section hydrostatics

Part 11 is implemented. Read [PART11_REVIEW.md](PART11_REVIEW.md). The 507B now floats on
an offset table derived from the closed hydrostatic surface (`part11_hydrostatics.py`,
`part11-hydrostatic-surface.json`), with draft, trim and heel resolved from the loading
inventory. The lightship LCG is a convention-derived estimate and free surface is not applied.
The blend and FBX exports are unchanged in this stage.

## Part 10 sectional hull

Part 10 is implemented. Read [PART10_REVIEW.md](PART10_REVIEW.md) for the corrected
lower shell, separate closed hydrostatic surface, source uncertainty, validation
and screenshots. Current LOD triangle counts are 59,543 / 35,972 / 24,362.
The blend contains source, two LOD, archival hull and hydrostatic scenes.
The hydrostatic surface is excluded from FBX exports; Part 11 derives the physics table from it.
The largest trace-to-section residual is 0.743 m and remains explicitly unresolved.

## Part 9 reference baseline

Parts 1-8 are implemented, with target-machine performance acceptance still open.
Remaining fidelity work is staged as Parts 9-17 in
[REFINEMENT_PLAN.md](REFINEMENT_PLAN.md). Parts 9-11 are complete; Parts 12-17 are not started.

Read [REFERENCE_DECISIONS.md](REFERENCE_DECISIONS.md) before hull or physics edits.
It records the retained representative two-hold variant, source applicability,
photo comparisons, coordinate conventions and pre-fit tolerances. The page-2
project-507 raster is a 600 x 316 pixel family reference, not exact 507B offsets.
`part9-geometry-baseline.json` and `part9-baseline.json` freeze the current source
and numerical evidence. The visual hull is not a closed hydrostatic solid.

## Part 7 record

Read [PART7_REVIEW.md](PART7_REVIEW.md) for cargo, ballast and stores accounting,
shared deadweight limits, loading controls, physical draft/trim/inertia changes,
save compatibility, tests and remaining hydrostatic limitations. The current
source/FBX retain the Part 4 primary geometry integrated in Part 5.

## Part 8 record

See [PART8_REVIEW.md](PART8_REVIEW.md) for distance LODs, resource budgets,
reproducibility, performance measurements, final screenshots and remaining limits.
At Part 8 the source contained three scenes, with 53,130 / 34,429 / 24,072 triangles.
Use `part8_export.py` through registered Blender MCP to regenerate all exports,
then `ShipSimulator.Editor.VolgoDonQualityGate.Run` in a dedicated Unity batch
editor to reimport, validate and install them. This supersedes the earlier export
instructions for the final LOD-enabled source. Physics and controls are preserved.
Target-machine built-player GPU performance remains unverified.

## Part 6 handling record

Read [PART6_REVIEW.md](PART6_REVIEW.md) for the optional steering-nozzle force
model, archived propulsion particulars, estimated calibration, reproducible
handling comparison and limitations. Current full speed is about 19.8 km/h;
stopping and turning remain approximate and do not match the archive figures.
Blender geometry and exports are unchanged by this physics stage.

## Part 5 integration record

The Part 4 source and geometry are now integrated into the existing playable
prefab. Read [PART5_REVIEW.md](PART5_REVIEW.md) for HUD controls, physics-driven
shaft/nozzle visuals, persistence, navigation lights, audio points, fallback and
runtime verification. Part 4 remains the current Blender authoring/export guide.
The lower milestone sections are historical, including their statements that
the candidate is not installed in the playable prefab.

## Part 4 record

Read [PART4_REVIEW.md](PART4_REVIEW.md) for the current rig, reference-supported
hatch sequence, manifest, validation, screenshots and limitations. The same source
and candidate now contain 15 mechanism controls, 49 meshes and 53,130 triangles.
Play frames 1-770 for the accelerated review sequence. Use `part4_validate.py`
and `part4_finish.py` through Blender MCP for current checks and export.
Animated export uses `bake_space_transform=False`; landmark alignment retains
the documented Unity axes. The playable prefab remains unchanged.

The sections below are historical milestone records. Old static export options,
mesh counts and statements about unseparated mechanisms no longer describe the
current source. Do not run Part 2/3 mutation scripts on the Part 4 rig.

## Part 3 record

Part 3 updates the same editable `.blend` and separate candidate FBX. Read
[PART3_REVIEW.md](PART3_REVIEW.md) for current geometry, materials, reference
decisions, reproducible scripts, verification and comparison images. Current
count is 52,187 triangles, 18 render meshes and 12 materials. Main deck Z=1.97 m,
hull keel Z=-3.53 m; length and beam remain 138.3 x 16.7 m. Four-bladed propellers,
rounded nozzles, transparent glazing and PBR maps supersede the limitations below.
The original FBX and active playable prefab remain untouched.

The following sections record the Part 2 milestone. Its scripts and JSON reports
are historical; Part 3 used `part3_finish.py` and `test_part3.py` for validation.

## Part 2 record

Part 2 completed 2026-10-22. This is the first corrected visual candidate for the
classic two-covered-hold configuration, not the finished vessel or a physics upgrade.

## Files

- `VolgoDon507B.blend`: independently opening editable source, 18 local meshes,
  nine packed texture images and a review camera/light rig.
- `prepare_source.py`: imports the legacy FBX into an isolated scene, repairs UV
  orientation and degenerate faces, and normalizes dimensions.
- `finish_source.py`: assigns bridge glazing, renders views, exports the candidate
  and writes its material manifest. Re-export from a source copy when preserving
  hand edits; the preparation script deliberately refuses an existing source scene.
- `validate_source.py`: checks the reopened source, dimensions, local geometry,
  packed resources, absence of legacy animation and corrected/preserved UV pairs.
- `part2-report.json` and `source-validation.json`: measured changes and checks.
- `Previews/part2-*.png`: Blender and Unity review captures.

Unity candidate files are under
`Assets/ShipSimulator/Models/VolgoDon507B_Improved/`: `VolgoDon507B.fbx`,
`VolgoDon507B_VisualCandidate.prefab`, PNG textures, URP materials and `materials.json`.
The candidate prefab is visual-only. The playable prefab still uses the original FBX.

## Coordinate and export contract

The Blender source uses metres: +Y toward the bow, +X starboard, +Z up. Origin is
the midpoint of the overall longitudinal bounds on the loaded waterline, Z=0.
Main hull keel is Z=-3.53. Appendages extend slightly below it, approximately
Z=-3.601; do not use the lowest nozzle vertex as the hull keel.
Overall visible bounds are 138.3 m long and 16.7 m wide. This uses modest source
normalization: X scale 1.03302824, longitudinal/vertical scale 1.02773680.
Detailed hull sections and moulded depth still need the Part 3 drawing review.

The named empties `Waterline`, `BowReference`, `StarboardReference`, `UpReference`
and `KeelReference` define the export frame. FBX export uses -Z forward, Y up,
unit scaling and baked space conversion. Unity's candidate reviewer aligns these
landmarks to +Z forward, +X starboard, +Y up with unit root scale. Do not apply the
old CRYENGINE 102.69789 visual scale or its axis rotation to this export.

Individual objects retain their imported local geometry/pivots with normalized
world transforms. They are not yet a verified animation rig. Imported legacy
radar animation is deliberately disabled: its source-space channels otherwise
move the radar away from the normalized ship after a frame/dependency update.

## Organization and editability

Collections group Hull, Decks, HoldsAndCovers, Accommodation, WheelhouseInterior,
Propulsion, Steering, Lifesaving, DeckEquipment, LightsAndRadar, ReferenceDatums
and Studio. `AnchoringAndMooring` is reserved for Part 4 separation: the legacy
anchors and fittings are still editable mesh islands within the hull/detail
objects. Covers likewise remain within the hold meshes. Collection labels do not
claim that every mechanism is already independently rigged.

All mesh datablocks are local and independently editable, including the originally
shared port/starboard radar, propeller and nozzle meshes. The `.blend` contains
actual geometry, not an external FBX link. Textures are packed; relative external
paths also point to the candidate texture folder. A checkpoint of the pre-task
Blender session is in ignored `Logs/VolgoDon507B/before-part2.blend`.

## First-pass changes

The color atlas mismatch was fixed with V'=1-V. Original UVs remain in the second
channel `LegacyUV`; corrected UVs are the first/render channel. A matched render
test showed that this correction restores the accommodation paint, windows,
fittings and hull coating placement without replacing the texture artwork.
Shared meshes are copied before modification so paired components are not flipped
twice. Eight legacy DDS atlases and the lights image were converted to PNG for
portable import. This is format conversion, not a claim of newly authored textures.

Removed 22 zero-area triangles: 16 in the holds and six in accommodation.
The Part 1 BMesh-area test found 15; an additional cross-product test found seven
collinear hold faces missed by that calculation. Final count is 42,299 triangles.
Useful UV seams, openings, hard edges and custom normals were retained. No global
welding, hole filling or normal recalculation was used to hide the atlas problem.

Thirty existing bridge panes (60 triangles) now use a dedicated tinted glazing
material. It is opaque in this first pass, with low roughness, to avoid promising
interior/refraction fidelity that has not been checked. Painted surfaces are
dielectric; decks/covers are rougher and propeller metal has a metallic response.
Detailed normal/roughness maps, wear and transparent glazing belong to Part 3.

## Review and verification

`ShipSimulator.Editor.VolgoDonCandidateReview.Run` is a dedicated Unity batch
entry point. It imports only the candidate, creates its URP materials and visual
prefab, checks datums/bounds, 18 renderers, 42,299 triangles, normals, resources
and both UV channels, then renders five views. It does not replace the active
vessel or modify its physics. Success is `VOLGODON_CANDIDATE|PASS` and exit 0.
Use the Unity version in `ProjectSettings/ProjectVersion.txt`, graphics enabled.

Verified source reopening with nine packed images and no linked meshes. Unity
review passed in `Logs/VolgoDon507B/candidate-final.log`, exit 0, no compiler
errors. `Logs/VolgoDon507B/candidate-review.txt` contains the measured import.
The final source checks are recorded in `source-validation.json`. All 200 EditMode
tests passed in `TestResults/check-EditMode-20261022-072713-430.xml`. All 47
original resource hashes match the baseline manifest; new assets have metadata.

| View | Before | Corrected Blender | Unity candidate |
|---|---|---|---|
| Front | [Before](Previews/baseline-front.png) | [After](Previews/part2-front.png) | [Unity](Previews/part2-unity-front.png) |
| Side | [Before](Previews/baseline-side.png) | [After](Previews/part2-side.png) | [Unity](Previews/part2-unity-side.png) |
| Top | [Before](Previews/baseline-top.png) | [After](Previews/part2-top.png) | [Unity](Previews/part2-unity-top.png) |
| Perspective | [Before](Previews/baseline-perspective.png) | [After](Previews/part2-perspective.png) | [Unity](Previews/part2-unity-perspective.png) |
| Accommodation | [Before](Previews/baseline-textured-superstructure.png) | [After](Previews/part2-superstructure.png) | [Unity](Previews/part2-unity-superstructure.png) |

The reference comparison from `AUDIT.md` still applies: the classic two-hold
layout, aft accommodation and bridge arrangement are retained. Corrected surface
mapping exposes the intended window and deck layout clearly. The legacy livery
is not claimed to match a named ship/year. Lighting and framing differ between
Blender and Unity captures; these are visual checks, not photometric comparisons.

## Remaining work

Part 3: check hull end sections, depth and equipment proportions against drawings;
refine the coarse three-bladed propellers and faceted nozzles to the documented
configuration; develop PBR surface maps and measured wear. Preserve the repaired
atlas mapping. Part 4 separates covers and other mechanisms with verified pivots.
Parts 5-7 handle live integration, nozzle forces and loading/ballast. LODs and
performance validation remain Part 8. No new dynamics accuracy is claimed here.
