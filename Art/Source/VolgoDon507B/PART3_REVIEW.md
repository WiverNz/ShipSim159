# Part 3: reference geometry and PBR candidate

Date: 2026-10-22. The target remains the classic two-covered-hold 507B, with no
claim to a particular named ship, paint scheme or modernization date.

## Reference decisions

Reopened and visually reviewed the [archive](https://russrivership.ru/public/files/doc257.pdf),
especially pages 1, 2, 5, 7 and 8, and the previously retained reference photograph
[Volgo-Don 185, photo 550661](https://fleetphoto.ru/photo/550661/).
The photograph checks arrangement, not the historical equipment inventory of
this unidentified mesh. No photograph or archival drawing became a game texture.

| Assembly | Evidence and inspection | Result |
|---|---|---|
| Hull ends and underwater body | Page 2 is labelled Project 507, with broad flat bottom, rounded bilge, fine forward sections and rising aft floor. Existing hull has these features, but only 544 triangles and no verified station offsets. | Retained underwater sections. No unsupported subdivision or whole-hull replacement. Exact station reconstruction remains uncertain. |
| Main deck and depth | Ray intersections at X=7.2 m, Y=-25/0/+25 m put the side deck at Z=2.143 m. With the -3.53 m keel this exceeded the 5.5 m depth target. Internal vertices at Z=1.064 m were not the deck datum. | Lowered above-water geometry continuously by up to 0.173 m, keeping the waterline and underwater shape fixed. Side deck now Z=1.970 m, keel-to-side-deck depth 5.500 m. |
| Accommodation and windows | Page 1 and the photograph agree on a forward-facing bridge over tiered aft accommodation, side stairs and two lifeboats. The inherited raked bridge panes are in the right broad arrangement. | Retained frames and tiers, shifted with deck correction. Thirty panes now use transparent tinted glass. Interior is an inherited proxy, not a surveyed bridge. |
| Covers and passageways | Existing two covered zones, four large cover panels, side tracks and clear side passages match the selected variant. Page 1's open arrangement alone cannot specify the later covers. | Retained geometry and cover layout; track mechanics and separation belong to Part 4. Side passage width is approximately 2 m before local fittings. |
| Funnels, masts, rails and lamps | Page 1 and the photograph support the general arrangement. They do not resolve every antenna or lamp fitting for this model's date. | Retained existing inventory and spacing. No modern equipment added. Railings, stairs and equipment follow the deck correction together. |
| Anchors, mooring and lifesaving | Pages 7-8 describe Hall anchors, two boats and davits, windlass/capstan/winches. Existing model includes these categories. | Retained shapes and count. Exact anchor mass, winch model and boat capacity cannot be inferred from the mesh. |
| Propellers | Page 5: two steel four-bladed screws, diameter 1.8 m, left/right pitch 1.52/1.47 m, expanded area ratio 0.62. | Replaced only the coarse three-bladed components with closed twisted blades, hub and shaft. Blade chord integration targets 0.62; radial extent checks approximately 0.9 m. |
| Steering nozzles | Page 7: two steerable nozzles with stabilizers, diameter 1.84 m, length 1.66 m. | Replaced polygonal rings with smooth closed rounded ducts, stock and aft stabilizer. 1.84 m is interpreted as clear throat diameter, giving about 20 mm radial clearance. |

Blade sections, skew, shaft alignment/handedness, stock bearings and nozzle
cross-section are explicitly authored approximations. The archive does not give
their fabrication drawings. The new geometry does not establish hydrodynamic
performance. Nozzle steering travel and collision clearance through that travel
remain Part 4 work, not a claim of an animated rig.

## Materials and UVs

The useful corrected color atlas and original UV backup are retained. Eight
material sets add derived base color, tangent-space normal, roughness and packed
metallic/smoothness images. Base colors retain the original 2048 or 1024 resolution;
data maps are 1024 square. Lights retain their 256-square atlas. Painted steel is
dielectric; the propeller is metallic steel. Glazing uses alpha 0.32, low roughness,
and the transparent URP pass, without transmission/refraction simulation.

`part3_materials.py` rasterizes actual surface positions into the existing atlas:
waterline staining, restrained runoff and wear on side-deck walking routes follow
geometry. Small seam relief and walking-surface relief are authored estimates.
There is no uniform random-noise overlay, and baked AO/color is not converted
into invented normal-map relief. Shared atlas islands retain repeated appearance;
this pass does not claim unique weathering at every fitting. Surfaces without
justified relief retain flat normals and restrained scalar response in their maps.

Unity reads base color as sRGB, normal maps as normal-map assets, and surface data
as linear. Packed metallic/smoothness uses R=metallic, A=1-roughness, G/B unused.
Blender uses separate linear roughness maps and matching tangent normals. Normal
strength is 0.5 in both applications. Original image rights remain the unresolved
legacy provenance, not a newly granted license.

The candidate has 52,187 triangles, 18 render meshes and 12 materials. The eight
normal plus eight packed surface maps add 16 Mi pixels before compression/mips.
This is a source-quality candidate, not the final LOD or performance budget.
Atlas density remains uneven; no broad re-unwrap was justified where it would
destroy useful legacy paint placement. A future close-view quality target may
require additional unique UV space and higher-resolution original artwork.
`part3-uv-density.json` records area-derived P10/median/P90 density for triangles
over 0.01 square metres. Median density is about 146 px/m on hull triangles,
195 px/m on decks, 189 px/m on covers and 33 px/m on accommodation. Tiny/repeated
detail islands skew counts; these are triangle percentiles, not area averages.
The low accommodation density is an inherited close-up limit, not resolved by
upscaling. All 41 PNG files, including the nine fallback atlases and Blender-only
roughness maps, total 29,454,127 bytes on disk; this is not a VRAM measurement.

## Reproduction and validation

Checkpoint: ignored `Logs/VolgoDon507B/before-part3.blend`. On that Part 2 source,
run `part3_geometry.py`, `part3_materials.py`, then `part3_finish.py` through the
registered Blender MCP. Geometry refuses duplicate application. Finish validates,
renders, exports and saves the regular independently opening `.blend`.
After reopening, run `test_part3.py`; it checks actual connected blade components,
clearance, both UV channels, packed images and transparent glass. Temporary
oversized-blade and damaged-UV mutations must fail and are restored afterwards.
Part 2 scripts/reports describe the earlier milestone, not current triangle counts.

`part3-validation.json` records source geometry checks. `part3-regression.json`
records reopened-source and deliberate-defect checks. Unity's
`VolgoDonCandidateReview.Run` checks axes, bounds, normals, UVs, triangle count,
PBR resources/import settings and glass, then builds only the separate visual
candidate prefab. It never replaces the playable vessel or changes physics.

Verified: Unity candidate review PASS and exit 0 in
`Logs/VolgoDon507B/part3-final.log`, with no compiler errors. EditMode: 200 passed,
zero failed in `TestResults/check-EditMode-20261022-074354-836.xml`. Source checks
and both deliberate-defect regressions passed after reopening the saved file.
All 47 original resource checksums still match; new Unity resources have metadata.
No runtime shakedown was run because the candidate is not installed in the active
vessel. No claims of runtime handling or performance validation follow from this.

## Images

| View | Part 2 | Part 3 Blender | Part 3 Unity |
|---|---|---|---|
| Front | [Before](Previews/part2-front.png) | [Blender](Previews/part3-front.png) | [Unity](Previews/part3-unity-front.png) |
| Side | [Before](Previews/part2-side.png) | [Blender](Previews/part3-side.png) | [Unity](Previews/part3-unity-side.png) |
| Top | [Before](Previews/part2-top.png) | [Blender](Previews/part3-top.png) | [Unity](Previews/part3-unity-top.png) |
| Perspective | [Before](Previews/part2-perspective.png) | [Blender](Previews/part3-perspective.png) | [Unity](Previews/part3-unity-perspective.png) |
| Accommodation | [Before](Previews/part2-superstructure.png) | [Blender](Previews/part3-superstructure.png) | [Unity](Previews/part3-unity-superstructure.png) |
| Propulsion | [Legacy](Previews/baseline-propulsion.png) | [Blender](Previews/part3-propulsion.png) | [Unity](Previews/part3-unity-propulsion.png) |

[Waterline and passage detail](Previews/part3-waterline.png). Studio lighting differs
between applications; the images do not establish calibrated paint color.

Next requested stage is Part 4: separate moving assemblies, establish their pivots
and archive-supported cover travel, and check full motion without interference.
The hull remains an approximate render shell, not a watertight displacement solid.
