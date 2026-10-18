# Meteor 342U model: provenance and construction record

Full record of where the information came from, what was used to build the model, and how the
model was built. `README.md` is the short operational note; this file is the long form.

Nothing here is validated for maritime training. Every figure is marked either published,
measured off a drawing, or estimated.

Scope: the visual model only. The physics in `Assets/ShipSimulator/Data/Vessels/Meteor342U.json`
was not changed by this work, and its own provenance is in
`Assets/ShipSimulator/Documentation/Meteor342U_Sources.md`.

## 1. Identifying which ship project 342U actually is

The project number alone is ambiguous: 342, 342E and 342U share a hull, and most published
material describes the family rather than the variant. The variant was pinned down like this.

1. The SPK Fleet fleet list for the Meteor type was downloaded and decoded from cp1251, and
   entry 191 read in full. It records: `Метеор-191`, yard number 062, 31.07.84, project 342E,
   M-400 engines, 123 passengers, and then, verbatim, that the ship was modernised in the early
   1990s and given project 342U after the modernisation, and sold in spring 2006 to the Valaam
   monastery and renamed `Преподобный Серафим`.
2. The FleetPhoto vessel card for that ship returns the page title
   `Преподобный Серафим – Тип Метеор, проект 342У`, so the site classifies the same hull as 342U.
3. A FleetPhoto photo page for the same vessel lists: home port Saint Petersburg, board number
   `СЗ-12-15`, register number 213615, class formula `О2,0/1,2 СПК А`, MMSI 273338470, built
   31.07.1984.
4. A photograph of the builder's plate on board reads
   `«МЕТЕОР» - ТЕПЛОХОД НА ПОДВОДНЫХ КРЫЛЬЯХ, ПАССАЖИРОВМЕСТИМОСТЬ 123, ПОСТРОЕН В 1984 г.,
   СТРОИТЕЛЬНЫЙ № 191`, which matches the fleet list entry.
5. The RussianShips page for the same vessel is captioned `Pr. 342U - Meteor C3-12-15`.

So there is exactly one identifiable 342U, and it is `Метеор-191` / `Преподобный Серафим`.
The consequence for the model: hull form, foil system and general arrangement are reconstructed
from project 342 family material, and the finished appearance is checked against photographs of
that one ship.

One conflict was found and is left unresolved: the SPK Fleet list gives the build place as
Gorky, Oktyabrsky settlement, while the FleetPhoto photo page gives the Zelenodolsk yard named
after A. M. Gorky. The model does not depend on which is right.

## 2. Sources used

| # | Source | What it supplied | How it was read |
|---|---|---|---|
| 1 | [SPK Fleet, Meteor](https://spkfleet.ru/meteor.htm) | Published particulars table, construction notes, fleet list, drawing numbers | Downloaded, decoded cp1251, parsed to text |
| 2 | [FleetPhoto, project 342U](https://fleetphoto.ru/projects/1778/) | Confirms 342U is one vessel | Page read |
| 3 | [FleetPhoto, vessel 1361](https://fleetphoto.ru/vessel/1361/) | 26 photographs across different years, registry data, builder's plate | Photo ids listed from the DOM, full-size images fetched |
| 4 | [Ris. 139, general arrangement](https://www.modelizd.ru/ship/modern/imageinfo/3330/2) | Side elevation and plan of the family | Image downloaded, measured numerically |
| 5 | [Ris. 140, templates and foil assemblies](https://www.modelizd.ru/ship/modern/imageinfo/3330/3) | Station templates, bow and stern elevations, both foil assemblies in plan and perspective | Image downloaded, read visually at several zoom levels |
| 6 | [Wikimedia Commons, Valaam, foilborne](https://commons.wikimedia.org/wiki/File:Lake_Ladoga._Valaam._Meteor_hydrofoil_P7170383_2200.jpg) | Best single view of the bow foil struts and the bow saloon glazing | Original 3300x2200 resolved through the Commons API |
| 7 | [RussianShips id289864](https://russianships.net/id289864) | Bow-on view: transverse shape, wrap-around glazing, foredeck rail | Page read, image fetched |

Sources deliberately not used: the commercial 342E drawing sets, the commercial `.blend` model,
the TurboSquid and Free3D high-polygon models and the 3D Warehouse model. None was bought, and no
geometry was taken from any of them. They were not needed once Ris. 139 could be calibrated.

Reference images were downloaded to a session scratch directory for viewing only. They are not
packed into the blend file, not used as textures, and not required by the game. The Wikimedia
photograph is CC BY-SA 4.0 by Alexxx1979 and is credited here and in `README.md`.

FleetPhoto serves full-size images through `/image.php?pid=<id>` rather than a static path; the
`_s.jpg` thumbnails linked on the vessel card are 250 px wide and too small to measure from.

## 3. Published particulars taken from source 1

Read directly from the SPK Fleet particulars table for the Meteor type.

| Parameter | Value |
|---|---|
| Length overall | 34.6 m |
| Breadth overall | 9.5 m |
| Height above the baseline | 6.25 m |
| Height above the water when foilborne | 6.73 m |
| Freeboard | 0.42 m |
| Draft overall, afloat | 2.35 m |
| Draft overall, foilborne | about 1.2 m |
| Displacement, full load | 53.4 t |
| Hull draft at full displacement, mean | 1.05 m |
| Displacement, light | 36.4 t |
| Hull draft, light, mean | 0.92 m |
| Service speed | 65 km/h |
| Maximum speed | 70 km/h |
| Passengers | 124 light seats, 116 reclining seats |
| Range | 600 km |
| Hull and superstructure material | D16AT sheet and D16 section duralumin, some AMg and AMts, some steel parts |
| Hull construction | riveted, mixed framing |
| Main engines | M400, two, 1100 hp maximum, 1800 rpm |
| Propulsion | two propellers, 0.71 m diameter, variable pitch, disc ratio 1.1, five blades |

The same page describes the type as a twin-screw passenger motor vessel of streamlined swept
form on submerged foils, with passenger saloons forward, amidships and aft, the machinery space
and a wheelhouse recessed into a half-superstructure. That sentence is what fixes the
compartment order used in the model.

The page also names the real shipyard drawings: general arrangement `342Э-020-048` sheets 1 and
2, foil assemblies `342Э-021-035`, and the theoretical hull lines `342-021-1Б`. None of those
drawings was available, which is why the hull had to be measured off the illustrated Ris. 139.

The 9.5 m figure is the overall breadth across the foil system. It is not the hull breadth, and
the model does not treat it as one.

## 4. Calibrating the general arrangement drawing

Ris. 139 is an illustrated model-building drawing, 1213 x 684 pixels, with a side elevation over
a plan view and a station scale beneath the elevation. It carries no dimension lines, so a pixel
to metre scale had to be established before anything could be measured.

Method, done numerically on the image rather than by eye:

1. The station tick marks under the elevation were located by scanning a horizontal band for
   columns of dark pixels. Ticks were found at x = 112, 594, 687, 784, 878, 926, 1025 and 1073,
   which resolves to a station spacing of about 96.1 px with half stations near the bow.
2. The plan view outline was traced column by column, recording the distance from the plan
   centreline to the outermost ink. This gives a half-breadth profile along the ship. The widest
   point, 300 px across, is the bow foil assembly. Setting that to the published 9.5 m overall
   breadth gives **31.58 px/m**.
3. Two independent checks were then run at that scale and agreed to within about half a per cent:
   - The full ink length of the plan view, 1087 px, is 34.4 m against a published 34.6 m.
   - At midship the red underwater body in the elevation spans 33 px vertically, which is 1.045 m
     against a published hull draft of 1.05 m. This also locates the loaded waterline in the
     image, at y = 213.5.

With the scale and the waterline fixed, the following were read off the drawing.

Plan view, half-breadth in metres against metres aft of the stem:

| Aft of stem | 1 | 2 | 3 | 4 | 5 | 6 | 8 | 10 | 11 | 12 | 13 to 25 | 28 | 30 | 32 | 33 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Half-breadth | 1.23 | 1.87 | 2.25 | 2.53 | 2.75 | 3.48 | 4.50 | 4.75 | 4.78 | 4.02 | 3.36 | 3.93 | 4.21 | 3.26 | 1.68 |

The values above 3.36 m are the foil assemblies, not the hull: the bow assembly occupies about
6 to 12 m aft of the stem and reaches 4.78 m half-breadth, the stern assembly about 28.4 to
31.4 m and reaches 4.24 m. The constant 3.36 m from 13 to 25 m aft is the hull's parallel body.

Sampling the ink across the plan at one midship station gave nested lines at half-breadths of
3.31, 2.99, 2.87, 2.61, 2.39 and 2.14 m. Read as deck edge, hull side, window band, roof edge and
roof camber, this gives:

- hull moulded breadth about **6.0 m**
- extreme breadth over the projecting side decks about **6.7 m**

Elevation, heights above the loaded waterline, read from ink runs at several stations:

| Feature | Height |
|---|---|
| Keel at midship | -1.05 m |
| Main deck at side | +0.42 m, matching the published freeboard |
| Roof crown | about +3.25 m |
| Engine casing top | about +3.66 m |
| Wheelhouse top | about +4.3 m |
| Highest fitting | about +5.2 m |

The last line closes the loop: +5.2 m above the waterline is 6.25 m above the baseline at
-1.05 m, which is exactly the published overall height. That consistency is the main reason the
drawing was trusted for hull shape.

## 5. What the second drawing supplied

Ris. 140 carries station templates, a combined bow and stern elevation split on the centreline,
and both foil assemblies drawn in plan and in perspective. It was read visually at several zoom
levels rather than measured, because the templates are model-builder cutting patterns rather
than a lines plan.

From it:

- The bow section is fine and strongly flared, with a hollow sweep from the deck edge down to a
  narrow keel. The stern section is much boxier.
- The bow foil assembly is a straight main span whose outer arms sweep strongly aft and outboard
  to the extreme breadth, carried on struts rising to the hull.
- The stern foil assembly is a straight span between two outer struts that curve up and outboard
  to the hull sides, with two inner struts on the shaft line and the outer tips turned down into
  shoes.
- The propeller shafts run down and aft at a shallow angle from a bossing in the hull bottom,
  with the rudders at the extreme after end of the assembly.

The whole-ship perspective on the same sheet confirmed the compartment order and the two raised
items on the roof: the wheelhouse forward and a long low casing further aft.

## 6. What the photographs supplied

Twenty-six full-size photographs of `Метеор-191` / `Преподобный Серафим` were examined, plus the
Wikimedia foilborne view and the RussianShips bow-on view.

Window rhythm was measured by overlaying a one-metre grid, derived from the known 34.6 m overall
length, on a near-broadside photograph of the vessel alongside. That gave, in metres aft of the
stem: bow saloon glazing from about 1.5 to 6.7, boarding door 6.9 to 8.0, middle saloon windows
from about 8.5 to 17.6, a round port at about 18.3, a blank machinery side with an access hatch
to about 23.5, aft saloon windows from 23.6 to 27.6, life raft stowage further aft, and the
raked glazed screen and open aft deck under the roof visor from about 31 to the stern.

Also taken from the photographs: the recessed wheelhouse with its raked windscreen, open-array
radar, radome, searchlights, whip antennas and after ladder; the white and blue livery as three
bands, one under the roof edge, a thin line at the deck edge and a boot top; mushroom ventilators
along the roof edges; four life raft canisters a side; the boarding fender; and the blue guard
rails. The bow-on view supplied the wrap-around front glazing and the foredeck rail.

## 7. Tools used

- Blender 5.2.2 LTS, driven live through the Blender MCP addon's code execution endpoint. All
  modelling, rendering and export ran inside that session.
- Python with Pillow, outside Blender, for the numerical drawing measurement in section 4 and for
  the contact sheets and grid overlays used to read the photographs.
- `curl` for downloads, the Wikimedia Commons API to resolve the original image URL, and the
  Chrome browser extension to read the FleetPhoto DOM for photo ids.
- Unity 6000.6.0f1 in batch mode for the compile check, both test suites, the model integrator and
  the runtime checks.

## 8. How the model is built

### Frame and conventions

Metres. `+X` starboard, `+Y` forward, `+Z` up. The loaded waterline is `Z = 0`, the baseline is
`Z = -1.05`, and the origin is midship, so the stem is at `Y = +17.3` and the stern at `Y = -17.3`.
Throughout the source, `a` means metres aft of the stem, and `y_of(a)` converts it. Unity's frame
is `+X` starboard, `+Y` up, `+Z` forward, and the conversion is checked at import rather than
assumed.

### Shape data lives in tables, not in code

`common.py` holds the shared frame, helpers and every shape control, so each pass can be re-run on
its own and no pass depends on state left by another. The hull is defined by seven tables keyed on
`a`, all read off the calibrated drawing:

| Table | Meaning |
|---|---|
| `B_HULL` | Hull half-breadth at the deck edge |
| `Z_DECK` | Height of the deck edge, the sheer line |
| `Z_KNUCK` | Height of the longitudinal knuckle |
| `K_FRAC` | Knuckle half-breadth as a fraction of `B_HULL`, which is what produces the flare |
| `Z_KEEL` | Keel height, so the forefoot and the upswept stern |
| `FLARE` | Topside fullness exponent, above 1 for a hollow flared bow |
| `BOTTOM` | Bottom fullness exponent, which sets deadrise |
| `LEDGE` | Width of the projecting side deck outboard of the hull side |

`hull_section(a)` evaluates those into a half-section: eight points from the keel out to the
knuckle along a power curve set by `BOTTOM`, then five points from the knuckle up to the deck edge
along a power curve set by `FLARE`. Lofting that across 43 stations gives the hull. Because the
shape is a function of the tables, correcting the drawing reading is a table edit, not a remodel.

The superstructure uses the same idea: `B_SUP` for its plan, `ROOF_HEIGHT` for the absolute roof
line above the waterline, and `SUP_SECTION` for the cross-section as half-breadth fractions
against height. `H_SCALE` is derived from `ROOF_HEIGHT` and `Z_DECK` rather than authored, so the
roof line stays where the photographs put it even when the deck height changes.

### The seven passes

Run in order inside Blender; each one execs `common.py` first.

1. **`01_blockout.py`** creates the studio scene, the metric unit settings, the collections, the
   root empty and the material set, then lofts the hull shell from the station tables and builds
   the projecting side deck and its fascia. Every piece gets a centreline Mirror modifier.
2. **`02_superstructure.py`** lofts the superstructure from `B_SUP`, `ROOF_HEIGHT` and
   `SUP_SECTION`, lays the weather deck between the hull side and the cabin, builds the engine
   casing on the roof, the wheelhouse, and the cantilevered aft roof visor with its side fin. The
   visor continues the roof rings aft of the cabin and tapers to a point, which is what gives the
   stern its silhouette.
3. **`03_foils.py`** builds both foil systems from aerofoil rings generated by a NACA-style
   thickness formula, so foils and struts have real sections rather than flat plates. The bow
   assembly is a straight main foil, two broad swept struts, two outer struts and swept-back outer
   arms with tip plates reaching the 9.5 m breadth. The stern assembly is a straight foil on two
   S-curved outer brackets and two inner struts, with downturned outer shoes. It then builds the
   shaft bossings, the inclined shafts, two five-bladed 0.71 m propellers and the rudders.
   Propeller and rudder positions are taken from the vessel JSON, not invented, so the render mesh
   agrees with the physics.
4. **`04_glazing.py`** builds the saloon glazing and the wheelhouse as explicit surfaces: panel
   rings that share the cabin surface with their own window reveals and surrounds. An earlier
   version cut the openings with Boolean difference against a solidified shell; that was replaced
   because Booleans on an open half-shell produced invalid results and the stern screen could
   overwrite the saloon face list. Window pitch is distributed by plan arc length rather than by
   station, so the wrap-around bow panes keep the same pitch as the panes along the side.
5. **`05_fittings.py`** adds the livery as thin raised strips that follow the surface, rather than
   as a texture, so the paint cannot z-fight and needs no atlas. Then the mushroom ventilators,
   the roof intakes, the mast group with radar, radome, searchlights and antennas, the ladder, the
   guard rails, the handrail, the life raft canisters and cradles, the boarding fender, bollards,
   the anchor housing, the navigation light fixtures, and the exported landmark empties.
6. **`06_interior.py`** builds the interior proxy: a sole, a dark deckhead, bulkheads closing each
   saloon and simple seat rows. It exists because the bow glazing is very large and an empty white
   shell reads badly from outside. It spends very little of the polygon budget and does not claim
   to reproduce the published seating plan.
7. **`07_export.py`** runs `validate_model.py`, then evaluates every modifier, joins the result
   into one mesh per level, removes doubles, dissolves degenerate edges, recalculates normals,
   triangulates, and adds a planar metre UV channel for future texturing. LOD1 is decimated; LOD2
   also drops fittings that stop being readable at distance and disables bevels. It writes the FBX
   and `model-report.json`, and saves the blend file.

### Validation before export

`validate_model.py` is a regression guard for faults that were actually hit during development
rather than a generic checker. It asserts that the saloon glazing still has its full face count,
that the bridge shell and its glazing exist, that glass normals wind outward, that window patches
keep their corners so no triangular gaps appear, and that the propeller and light landmarks and
the navigator sightline are present. Export additionally rejects degenerate triangles and length
drift.

### Export conventions

The FBX is written with `-Z` forward and `Y` up, unit scale applied and the space transform baked,
carrying the three LOD meshes and the named landmark empties. The landmarks are the contract with
Unity: `Waterline`, `BowReference`, `SternReference`, `StarboardReference`, `UpReference`,
`CameraNavigator`, `PropPoint_S`, `PropPoint_P`, `FoilPoint_Bow`, `FoilPoint_Stern` and the four
navigation light points.

## 9. Unity integration

`Meteor342UModelIntegrator` imports the FBX, then derives the import rotation from the exported
landmarks instead of trusting the axis conversion. It refuses the import if the axes come back
reflected or wrongly scaled, and it also refuses if the exported propeller landmark disagrees with
the propeller position in the vessel JSON, so the render mesh cannot silently drift away from the
physics. It builds the LOD group, creates URP Lit materials, and writes only `DetailedVisual` and
the model-dependent camera and light layout back into the prefab.

Everything else in the prefab is retained: the Rigidbody, `ShipPhysicsController`,
`VesselDataLoader`, the `CollisionHull` hierarchy and its three box colliders, and therefore the
mass, centre of gravity, buoyancy and handling calibration. The render mesh is never used as a
collider. `ProceduralVesselBuilder.Build(Meteor)` delegates to the integrator, so a catalogue
rebuild cannot restore the old generated mesh.

## 10. Provenance of each part of the model

**Reconstructed from the drawings.** Hull lines and section shape, overall proportions, the
longitudinal positions of both foil assemblies, the layout of both foil assemblies, the compartment
order, and the cantilevered aft roof visor.

**Matched to photographs of 342U.** Window rhythm and sizes, the boarding door, the machinery
porthole and hatch, the aft glazed screen, the wheelhouse and its mast group, the white and blue
livery and where its bands sit, the roof ventilators and casing, the life rafts, the fender and the
guard rails.

**Approximate reconstruction, not established by either source.** Section shape between the
drawing's template stations; aerofoil sections of foils and struts, which are plausible symmetric
sections because no foil section drawing was available; propeller blade shape and pitch
distribution, where only diameter, blade count and disc ratio are published and those are matched;
mast, antenna, searchlight, rail, vent and fender dimensions; and the interior proxy.

**Not distinguishable between 342U and 342E.** No external feature could be attributed to the
early 1990s modernisation. The photographs show a standard Meteor hull, superstructure and foil
system. The only clearly modern fittings are the open-array radar, the radome and the life raft
canisters, and other Meteors carry those as refits too. The model therefore reproduces what the
342U photographs show without asserting which parts the modernisation changed.

**Livery.** Generic white and blue, with no name, board number or religious device, because the
catalogue entry is the generic `Meteor Project 342U` and not the named vessel.

## 11. Agreement with published figures

| Control | Published | Model |
|---|---:|---:|
| Length overall | 34.6 m | 34.60 m |
| Breadth over the foil system | 9.5 m | 9.54 m |
| Height above the baseline | 6.25 m | 6.24 m |
| Draft afloat, over the foils | 2.35 m | 2.35 m |
| Hull draft at full displacement | 1.05 m | 1.05 m |
| Freeboard | 0.42 m | 0.42 m |
| Propeller diameter and blades | 0.71 m, 5 | 0.71 m, 5 |

The 9.54 m is the extreme over the tip stabiliser plates; the foil span itself is 9.50 m.

Matching overall dimensions does not prove the shape is an exact reproduction. Cabin heights,
window sizes, frame widths, section shape between stations, paint boundaries and small fittings
remain photographic estimates.

## 12. Open items

- The hull moulded breadth measured off the plan is about 6.0 m, against the estimated 5.2 m in
  the vessel JSON. The finding is recorded but deliberately not applied, because changing it would
  recalibrate hydrostatics and handling, which is outside an art replacement.
- The real shipyard drawings `342Э-020-048`, `342Э-021-035` and `342-021-1Б` are named by source 1
  but were not available. If they turn up, the hull tables in `common.py` are the only thing that
  needs revisiting.
- The build place conflict in section 1 is unresolved.
- The export is a set of render surfaces with open shell, glazing and fitting edges. It is not a
  watertight solid and must not be used to calculate displacement; the game takes hydrostatics
  from the vessel JSON.

## 13. History

- **2026-09-20.** Research, drawing calibration, the seven-pass source, first export, the Unity
  integrator, and verification: batch compile clean, EditMode and PlayMode suites green, model
  integration, fast craft shakedown and voyage menu smoke check all passing.
- **2026-09-21.** The saved broadside comparison and the live Blender scene exposed real defects
  in that first model: an excessively tall cabin, small high side windows, a missing wheelhouse
  shell, a stern screen that overwrote the saloon glass face list, and invalid Boolean results on
  the open wheelhouse half-shell. The corrected source replaced the Boolean window cutting with
  explicit glazing and surround geometry, added `validate_model.py` as a regression guard, raised
  the bow saloon deck, lowered the cabin roof, enlarged the saloon windows, rebuilt the wheelhouse
  and relocated several fittings. `README.md` lists those corrections in detail, and the current
  geometry counts there supersede the first export's.

## 14. Geometry repair, 2026-10-18

User inspection exposed open stem/cabin ends, foil supports placed at centreline keel
height instead of the local hull surface, and mismatched glazing tessellation. This
revision changes the construction method, not the reference interpretation or physics.

Pass 1 closes the hull and splits the underbody paint boundary. Pass 2 extends the
weather deck to both ends with vertical edge thickness. Pass 3 seats the strut root
rings inside the actual hull and corrects foil tips, shafts, rudder connections and
propeller blade rotation. Pass 4 now uses one constrained Delaunay triangulation of
cabin parameters for shell, glass, frames and sheer paint; they share exact seams.
Front and aft closures follow the cabin section. Pass 5 adds missing fitting brackets.
Pass 6 fixes the scaled-height error that let the interior ceiling protrude through the
window band. Pass 7 welds shared export seams and uses material-delimited planar dissolution for
the distance LODs. Ratio-based collapse was rejected after Unity previews showed
stretched glass polygons; exported glass/paint areas are now checked as well.

`geometry-validation.json` records closed-part checks, measured root embedding and
zero interior cabin boundary edges. `test_geometry.py` proves four defect guards by
mutating temporary mesh copies and restoring the source. `Previews/repair-final-*`
shows this revision. The README and model report supersede earlier geometry counts.
The separate render assemblies are not a single watertight displacement model; no
structural joint design or newly measured vessel dimensions are claimed.

## 15. Reference appearance review, 2026-10-18

Sources inspected:

- [Classic Meteor general arrangement, Ris. 139](https://www.modelizd.ru/ship/modern/imageinfo/3330/2),
  a reproduced model-building illustration, used for relative machinery-cover
  length and the pointed aft canopy plan, not measured 342U dimensions.
- [Templates and foil arrangement, Ris. 140](https://www.modelizd.ru/ship/modern/imageinfo/3330/3),
  used as family-level visual context, not a reason to alter simulation forces.
- [Meteor-191 / Prepodobnyy Serafim, 17 July 2016](https://commons.wikimedia.org/wiki/File:Lake_Ladoga._Valaam._Meteor_hydrofoil_P7170383_2200.jpg),
  photograph by Alexxx1979, CC BY-SA 4.0. Shows paired tapered roof ventilation
  trunks with blue bands and a short rounded central machinery cover.
  The photograph remains a research reference in ignored Logs; it is not packed
  into the model, used as a texture, or redistributed in committed previews.
- [SPK Fleet Meteor archive](https://www.spkfleet.ru/meteor.htm), links to shipyard
  general arrangement 342E-020-048 and foil arrangement 342E-021-035.
  Their Mail cloud pages returned a service error during this review, so their
  drawing contents were not used.

Pass 2 shortens the cover from 9.7 to 5.1 m and tapers both width and height
at its ends. Pass 5 adds paired trunks at a=21.3 m, x=+/-1.62 m, height
0.76 m above the roof crown, with buried bases. These are estimated art
proportions inferred from perspective photographs, not shipyard measurements.
The drawing supports the existing pointed aft canopy plan. Cabin windows,
hull sections and foil geometry remain approximate and were not rescaled from
one perspective photograph. Exact 342U reconstruction remains unverified.

The pre-edit source is saved as
`Logs/Meteor342U/ReferenceReview/before-reference-adjustment.blend`.
Current authored renders are `Previews/reference-quarter.png` and
`Previews/reference-side.png`. No external geometry or photo texture was imported.

## 16. Meteor-247 silhouette comparison, 2026-10-18

The user supplied [MeteorHydrofoil.jpg](https://commons.wikimedia.org/wiki/File:MeteorHydrofoil.jpg),
a photograph of Meteor-247 at Peterhof uploaded by Leonard G., licensed CC SA 1.0.
It is retained only in ignored research Logs, not used as a model texture.
The comparison exposed the oversized white strip above the side windows,
upright bow profile and excessive aft canopy rise. The README records the
resulting estimated controls. We retain Meteor-191 fittings and livery rather
than claiming this is a replica of Meteor-247. Perspective, loading condition,
and vessel-specific equipment differ. Exact survey accuracy remains unverified.
Backup: `Logs/Meteor342U/ReferenceReview/before-silhouette.blend`.
