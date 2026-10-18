# Meteor project 342U

Editable source: `Meteor342U.blend`. Runtime export:
`Assets/ShipSimulator/Models/Meteor342U/Meteor342U.fbx`.
The source is a reference-based game reconstruction, not a surveyed replica or
training-validated vessel. The source was revised on 2026-09-21 and repaired geometrically on 2026-10-18.

## Identification and references

`MODEL_PROVENANCE.md` holds the long form: every source and what it supplied, the numerical
calibration of the general arrangement drawing, the measured hull and photograph readings,
the tools used, and a pass by pass account of how the model is built.

- [FleetPhoto vessel card](https://fleetphoto.ru/vessel/1361/) identifies
  `Prepodobnyy Serafim`, formerly `Meteor-191`, as project 342U, built in 1984,
  yard number 062. Its photographs provide the appearance reference.
- [Alexxx1979, Lake Ladoga, 17 July 2016](https://commons.wikimedia.org/wiki/File:Lake_Ladoga._Valaam._Meteor_hydrofoil_P7170383_2200.jpg)
  provides a clear bow quarter view of that vessel underway. The raised forward
  saloon, curved windows, low middle cabin, painted struts and dark underbody are
  visible. The photograph is CC BY-SA 4.0 and is not bundled or used as a texture.
- [Ris. 139, Meteor arrangement](https://www.modelizd.ru/ship/modern/imageinfo/3330/2)
  provides a family side elevation and plan. This is an illustrated model-building
  reference, not a fully dimensioned shipyard lines plan.
- [Ris. 140, sections and foil assemblies](https://www.modelizd.ru/ship/modern/imageinfo/3330/3)
  informed the retained hull and foil reconstruction.
- [SPK Fleet particulars](https://spkfleet.ru/meteor.htm) lists family dimensions:
  34.6 m overall length, 9.5 m overall breadth, 2.35 m draft over foils,
  1.05 m hull draft and 0.42 m freeboard. Refits can differ in fittings and height.

The generic catalogue model uses white and blue paint without the named vessel's
registration, religious artwork or exact refit equipment. It represents the classic
342 family appearance associated with 342U, not Meteor-2020 or Meteor 120R.

## Corrections to the previous uncommitted model

The saved broadside comparison and the live Blender scene exposed an excessively
tall cabin, small high side windows and a missing wheelhouse shell. In the old
window pass, the stern screen reused `glass_faces`, replacing the saloon face list.
Boolean operations on the open wheelhouse half-shell also produced invalid results.

The corrected source has:

- A raised bow saloon deck falling to the lower boarding and side decks.
- A lower cabin roof with curved shoulders and seven wrap-around bow panes per side.
- Seven larger rounded middle saloon windows, a machinery porthole and access hatch,
  five aft saloon windows and a glazed stern screen.
- A complete swept wheelhouse with a roof, opaque sill and fourteen glazing panels.
- Surface-conforming paint bands, relocated life rafts with straps, a dark underbody
  and painted foil supports. The unsupported tall bow railing was removed.
- Explicit glass and window-surround geometry instead of shell Booleans. Window
  surrounds include their rectangular patch corners, avoiding triangular gaps.
- Outward glass winding and double-sided Unity glass for the navigator viewpoint.
- Exported light landmarks so the functional lights follow the altered roof and mast.

Cabin heights, window sizes, frame widths, hull sections, paint boundaries and small
fittings are photographic estimates. Overall dimensions alone do not prove that the
shape is an exact reproduction. The approximate 6.0 m rendered hull breadth differs
from the existing 5.2 m physics estimate; this art change does not recalibrate handling.
Foil sections, propellers and the simple interior remain approximations.

## Rebuild in Blender

The seven numbered passes reproduce the model. Run them in order in Blender:

```python
D = 'G:/Projects/ShipSim159/Art/Source/Meteor342U/'
for f in ('01_blockout.py', '02_superstructure.py', '03_foils.py', '04_glazing.py',
          '05_fittings.py', '06_interior.py', '07_export.py'):
    exec(compile(open(D + f, encoding='utf-8').read(), f, 'exec'))
```

Paths currently target this Windows checkout. Change `BASE` and the pass import paths
when moving the source elsewhere. Pass 1 replaces the named Meteor studio scene and
its model collections. Save any manual source edits before rebuilding.
`common.py` holds the frame and shape controls; `studio.py` holds review cameras.
Pass 7 runs `validate_model.py` before exporting. Its regression checks cover saloon
face coverage, the bridge roof, outward glass normals, window patch corners and
propeller/light landmarks and navigator sightline. Export also rejects degenerate triangles and length drift.

## Axes and integration

Blender uses metres, +X starboard, +Y forward, +Z up, with loaded waterline Z=0.
The hull baseline is Z=-1.05. Unity uses +X starboard, +Y up and +Z forward.
FBX axis conversion is checked against exported landmarks by the integrator.
`Meteor342U_Model` contains editable pieces, many with centreline Mirror modifiers.
`GameReady_LODs` contains evaluated exports and is hidden in the studio; the review
camera, lights and water plane are excluded from the FBX.

`Meteor342UModelIntegrator.UpdatePrefab` replaces only `DetailedVisual` and updates
model-dependent camera/light layout. Rigidbody, vessel data, force models, mass,
centre of gravity and the three existing box colliders are retained. The catalogue
builder delegates to the integrator, preserving this model on a catalogue rebuild.

## Geometry audit

LOD0 bounds are 9.5443 x 34.6000 x 7.5453 m, including foils and mast.
Eleven material slots are shared by the three LOD meshes.

| LOD | Triangles | Vertices | Open edges | Degenerate triangles |
|---|---:|---:|---:|---:|
| 0 | 69,994 | 36,673 | 2,784 | 0 |
| 1 | 24,994 | 13,577 | 1,524 | 0 |
| 2 | 16,971 | 8,800 | 443 | 0 |

`model-report.json` records export bounds and counts. `geometry-validation.json`
records closed hull/foil topology, root embedding and shared cabin seams. The hull
and seven mirrored foil/support components are closed individually; separate fittings,
interior surfaces and overlapping assemblies make the complete export a render model,
not a single watertight displacement solid.

## Geometry repair, 2026-10-18

- Closed the finite-width stem, hull deck and cabin ends, and collapsed the degenerate
  stern section to its centreline endpoint. The weather deck now reaches both ends.
- Replaced independently tessellated window surrounds with a single constrained
  triangulation for shell, frames, glass and the blue sheer band. All section bends
  and longitudinal table knots are included, so their shared edges follow the cabin.
- Fitted the complete roots of all four mirrored strut groups to the local hull surface,
  with measured embedding of 0.063 to 0.123 m. Fixed foil tip plates, shaft/hub gaps,
  rudder supports and the rotation of the five propeller blades.
- Corrected the interior ceiling's height-coordinate conversion and sampled its curve
  densely enough to remain inside the cabin. Seats are checked across their full width.
- Removed coplanar deck surfaces, stepped underbody paint and detached paint strips.
  Added visible raft and handrail brackets and seated bridge fittings.
- Welded shared seams before LOD simplification; source parts remain editable.
  Distant LODs dissolve nearly coplanar faces with material boundaries protected,
  avoiding the glass shards produced by ratio-based collapse. Export checks the
  glass and blue-paint surface areas against their unsimplified inputs.

`validate_model.py` rejects open hull/foil shells, inward normals, detached strut roots,
internal cabin boundary edges, protruding interior geometry and degenerate triangles.
`test_geometry.py` deliberately introduces four defects on temporary mesh copies and
verifies that each guard fails, then restores and validates the intact source.
Run it with the same `exec(compile(...))` pattern as the numbered passes.
Root attachment is an overlapping render assembly, not a structural engineering joint.

## Review and verification

`Previews/repair-final-*.png` are the current geometry review, including bow, stern,
underside and window/foil details. `corrected-quarter.png`, `final-*` and `state-*`
are historical September captures. The historical
`compare-side-with-photo.png` is the before-correction comparison, retained for context.
The `repair-unity-*` previews show the current imported model and distance LODs.
Earlier `unity-front-quarter.png` and `game-*` images remain historical.
Current verification results are in
`Assets/ShipSimulator/Documentation/Meteor342U_Sources.md`.
The pre-correction Blender scene was saved to the ignored
`Logs/Meteor342U/before-correction.blend` before editing.

The pre-repair scene is retained at `Logs/Meteor342U/before-geometry-repair.blend`.

## Reference appearance review, 2026-10-18

The roof machinery cover now spans estimated stations 19.8 to 24.9 m aft
of the stem, with tapered ends, replacing the 9.7 m cover. Two tapered
ventilation trunks with blue bands flank it at estimated station 21.3 m.
These reproduce visible features of Meteor-191 (Prepodobnyy Serafim),
without claiming surveyed dimensions. See MODEL_PROVENANCE.md section 15.
`Previews/reference-quarter.png` and `reference-side.png` supersede the
roof appearance in the repair captures. The geometry repair remains in place.

## Silhouette revision after Meteor-247 comparison, 2026-10-18

The bow roof now starts lower and rises into the saloon; bow mullions rake aft.
Side window heads extend to section height 2.46 instead of 1.95, revealing the
curved shoulder below the roof band. The wheelhouse base is wider and longer,
giving its front and rear a gentler slope without moving the navigator marker.
The aft cabin crown stays at 2.72 m and the visor rises only 0.18 m instead
of 0.70 m. These are estimated visual proportions from the user's reference.
`Previews/silhouette-{side,quarter,afloat}.png` supersede earlier silhouette views.
The afloat view uses the loaded waterline, not a simulated foilborne attitude.
LOD2 dissolution now uses 1 degree to retain the thin paint band. Export removes
zero-area triangles generated at collinear dissolved corners and still checks
material-area drift and nondegenerate output. Source and defect guards pass.
