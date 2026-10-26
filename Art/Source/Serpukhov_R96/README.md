# Serpukhov, original Project R-96

Target: operational appearance in 1991-1996, using the July 1989 photograph.
Editable reconstruction and Unity integration are supplied. This is not an exact
surveyed replica; the limits below are part of the deliverable.

See [VALIDATION.md](VALIDATION.md) for executed tests and runtime evidence.

## Reference and measurement register

- https://www.omtnp.ru/images/flot/p96/p96.pdf: all six scanned pages examined.
  Page 1 is a general arrangement, not a body-lines or offsets drawing. The
  rounded stern is at drawing left, pushing bow at right. Aft deck is low,
  forecastle raised, wheelhouse forward of the engine casing. Plan and elevation
  have scan skew and must be scaled independently. Original curve interpolation
  between traced sections is an estimate, not recovered shipyard offsets.
- Page 2: 21.8 m overall, 19.9 m design length, 5.3/5.0 m overall/design beam,
  1.4 m hull depth, 3.6 m bridge floor above baseline, 0.62 m loaded draft,
  45.23 tonnes loaded, 38.57 tonnes light. 6.4 m height excludes removable parts.
  Published bollard pull is 2.0/2.1 tonnes-force (calculated/trial).
- Pages 3 and 5: single 0.846 m four-blade waterjet impeller; centreline balanced
  rudder, +/-45 degrees, twin reversing flaps behind the waterjet opening.
  These are not twin propellers or a modern azimuthing jet nozzle.
- https://aoecs.ru/proekty-centra/buksiry-tolkacitolkaci-buksiry/melkosidiashhii-buksir-tolkac-klassa-r-moshhnostiu-110-kvt-proekta-r96
  confirms class dimensions and 110 kW. Its 45.43 t differs from the original
  sheet's 45.23 t; use the original sheet, without averaging.
- https://fleetphoto.ru/vessel/3048/: identity, yard number 12, built 12 October
  1973 at Arkul, registry 182169; withdrawal June 2015, scrapping November 2015.
- https://fleetphoto.ru/photo/408315/: actual July 1989 vessel, primary silhouette
  reference. Three front panes, three side panes, broad visor, name on front
  and sides below glazing, raised bow cheeks, external horizontal stiffeners.
- https://fleetphoto.ru/photo/348223/: 1977 distant photograph. Limited detail.
- https://fleetphoto.ru/photo/28662/: 2009 port-side structure and white paint.
- https://fleetphoto.ru/photo/7933/ and https://fleetphoto.ru/photo/465812/:
  exposed hull, portholes, knees and glazing in 2010. Damage is not reproduced.
- https://fleetphoto.ru/photo/145251/: 2014 wheelhouse console geometry and
  mechanical levers. Missing controls are conservatively reconstructed.
- https://fleetphoto.ru/photo/145253/ and https://fleetphoto.ru/photo/145252/:
  galley and corridor research only; their later paint is not period proof.
- https://katerok.ru/projects/proekt_p96/: supplementary arrangement examined;
  its caption identifies R-96B, so it was not used to replace original R-96 geometry.
- https://ru.djvu.online/file/CKoXO32MVSBHh: accessible book OCR, no verified
  original R-96 offsets extracted. R-96B geometry is not substituted.
- https://boatclub.ru/threads/buksir-proyekta-r-96-modernizatsiya.6838/:
  accessible modernisation discussion, not evidence for Serpukhov's 1990s fittings.

FleetPhoto browser fetches partly failed; direct image downloads succeeded for
all eight supplied photos, which were visually inspected. Private reference
downloads are outside distributable Assets. No photograph is used as a texture.

## Reconstruction limits

Early photographs are monochrome. Off-white house, muted grey hull, dark lower
hull, oxide deck and restrained operational wear are authored color estimates.
Fine hull offsets, obscured fittings, exact panel thicknesses, unseen port/rear
glazing, restored instruments, mechanical internals and hydrodynamic coefficients
are estimates. Dimensions alone do not establish historical fidelity. Prototype
handling must not be described as validated maritime training behaviour.

Blender uses metres, X starboard, Y forward, Z above baseline. Unity root is at
the loaded waterline, with Z forward and Y up; exported landmarks verify conversion.
Editable objects stay separate in named collections. Runtime LODs may batch static
parts; movable waterjet and bridge components retain separate pivots.

## Files and reproduction

- `Serpukhov_R96.blend`: editable source, 487 authored mesh objects in functional
  collections, packed original material maps, eight hidden collision references.
- `../../../Assets/ShipSimulator/Models/Serpukhov_R96/Serpukhov_R96.fbx`:
  LOD0/1/2 at 77,154 / 30,120 / 8,072 triangles. Static meshes are batched;
  LOD0 retains 15 renderers including the important movable parts. LOD1/2 have
  two renderers each. The 17 shared material slots account for additional draws.
- The model folder also holds 30 original 512px PBR maps and URP materials.
- `Assets/ShipSimulator/Prefabs/Vessels/Serpukhov_R96.prefab`: reusable integrated
  prefab, eight box colliders, loaded-waterline root, camera/propulsion landmarks.
- `Assets/ShipSimulator/Data/Vessels/Serpukhov_R96.json`: physics estimates and
  documented dimensional controls. The existing catalogue includes `serpukhov-r96`.
- `Assets/ShipSimulator/Documentation/SerpukhovR96_Sources.md`: engineering data,
  propulsion approximation and handling limitations.
- `Previews/`: eight Blender inspection views, Unity LOD and bridge captures.
- `model-report.json`, `saved-source-validation.json`, `trials.txt`: executed
  geometry and pure-model checks. `ChangedFiles.txt` lists the delivered changes.

All Blender creation, inspection, saving and export used the registered Blender
MCP integration. To recreate in a new Blender session, execute `01_geometry.py`,
then load `context.py` in each subsequent MCP call before `02_details.py`,
`03_finish.py`, `05_review_refinements.py` and `04_export.py`, in that order.
Scripts have phase guards to avoid accidentally duplicating source objects.
`validate_model.py` reads the saved source without rebuilding it. The render rig
is retained in the source; `06_capture.py` uses it for the eight review views.
The local `ROOT` path in the scripts can be changed for another checkout.

In Unity use `Ship Simulator > Integrate Serpukhov R-96 Blender Model` to rebuild
only this vessel's prefab and catalogue entry. `Build Vessel Catalogue` also
retains it. Play a voyage scene, choose `Серпухов, Р-96 (1973)` on the vessel page,
and select the desired passage. The tug fits the existing Serpukhov zaton limits.
The existing navigator view and third-person cameras work through `VesselLayout`.

The source's original image plane was used only for research and removed before
delivery. Original traced reference curves remain in the hidden References
collection. No copyrighted reference images or embedded fonts ship with the asset.

## Scope and remaining limits

The wheelhouse has restored mechanical controls and visible glazing. The lower
house interior, galley and engine room are not detailed. Deck tackle is visual;
there is no new working tug-to-barge coupling or winch gameplay. Moving pieces
have separate meshes/pivots but are not yet animated. A free-walking character
system is not introduced. Fine unseen geometry, paint hues, precise lettering
style and historical port/starboard differences remain uncertain.

The estimated waterjet force representation uses the existing propulsion model.
Its first measured maximum speed is 14.44 km/h, versus the sheet's 14.9 km/h;
turning and stopping distances do not match the sheet's trial observations.
See the sources document before treating those responses as historical behaviour.
