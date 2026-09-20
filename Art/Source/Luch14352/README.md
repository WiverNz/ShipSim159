# Luch 14352, updated 1994 appearance

The primary deliverable is `Luch14352.blend`, modelled and inspected in a live Blender
5.2.2 session through Blender MCP. The numbered Python files record the modelling passes;
Unity does not execute them. They currently retain the author's Windows checkout path.
`resume.py` restores helpers between independent MCP execution calls.

## Assets and coordinates

- Editable source: `Art/Source/Luch14352/Luch14352.blend`.
- Runtime export: `Assets/ShipSimulator/Models/Luch14352/Luch14352.fbx`.
- Prefab: `Assets/ShipSimulator/Prefabs/Vessels/Luch14352.prefab`, `DetailedVisual` child.
- Blender: metres, X starboard, Y forward, Z up. Loaded waterline is Z=0, baseline Z=-0.66.
- Unity: metres, X starboard, Y up, Z forward. Waterline is Y=0. No negative scale.
- FBX: selected evaluated LOD meshes and named attachment landmarks, -Z forward / Y up,
  unit scale applied, baked space transform. Unity bakes axis conversion, keeps scale 1,
  imports normals, and creates no colliders.
- `Luch14352_Model` contains editable parts, including hull/deck Mirror modifiers.
  `GameReady_LODs` contains evaluated, triangulated exports, hidden in the source studio.
  Reference images and studio lights/camera are separate, excluded from FBX.

The source links viewport-only reference images in ignored `Logs/Luch14352/References`.
They are not packed, shipped as textures, or required by the game. The original source URLs
below permit restoring missing local references. Enable reference objects individually for
orthographic comparison; the photographs have perspective, not survey accuracy.

## Reference decisions

1. [Vympel design bureau](https://www.vympel.ru/projects/skeg-ships-and-hovercrafts/passazhirskoe-sudno-dlya-malykh-rek-tipa-luch/):
   published dimensions, construction materials, boarding ramp and accommodation layout.
2. [USC catalogue](https://www.nevainter.com/upload/materials/United%20Shipbuilding%20Corporation%20_POTENTIAL_AREAS_OF_COOPERATION_IN_CIVIL_SHIPBUILDING.pdf):
   the downloaded edition has the Luch sheet on PDF page 19, printed page 16, rather than
   PDF page 18. Used for particulars and single waterjet arrangement. Its modern concept
   illustration was not used for the 1994 superstructure.
3. [Updated 14352, approximately 1994, Vympel History](https://fleetphoto.ru/photo/478451/):
   primary silhouette reference. The page identifies yard number as `201?`, not certain 201.
   Long low saloon, forward raised wheelhouse, one isolated forward side window plus eight
   saloon windows, rounded window corners, long aft engine casing with horizontal ribs,
   white superstructure and dark lower hull.
4. [Luch-202, yard 202, 1995](https://fleetphoto.ru/vessel/17000/): both displayed primary
   photographs were examined: [2012](https://fleetphoto.ru/photo/42996/) and
   [2022](https://fleetphoto.ru/photo/397752/). The 2012 photograph also includes Luch-10
   (14351) in the foreground; the foreground boat was not treated as the 14352 silhouette.
   The 2022 photograph is a distant front view. The site's secondary-photo toggle did not
   expose an additional image through the available requests.
5. [SPK Fleet](https://www.spkfleet.ru/luch.htm): family arrangement, aft machinery and
   waterjet, boarding and underbody context. The mixed 14351/14352 page is supporting
   evidence, not a definitive 14352 construction drawing.

The width conflict is unresolved in the publications: USC lists 4.53 m overall and
3.85 m maximum beam, while Vympel calls 3.85 m overall. The model interprets 3.85 m as
the main hull width and 4.53 m as the extreme side deck/obnose envelope, a 0.34 m extension
on each side. It does not turn the air cushion craft into a 4.53 m monohull. This is a
plausible reconstruction, not a verified explanation of the inconsistent labels.

## Accuracy and approximations

Published controls: approximately 23.72 m overall length, 1.25 m hull depth, 0.45 m
skeg height, 3.60 m baseline-to-wheelhouse top, forward wheelhouse, midship saloon,
aft machinery, hydraulic bow boarding and one waterjet. The photo determines the
recognisable updated 14352 silhouette rather than the older 14351 cabin.

Photographic estimates: station shapes, individual window sizes and spacing, wheelhouse
rake, roof camber, mast, paint colours, rail dimensions, hatch and vent locations. The
waterjet nozzle/reverse bucket/intake, cushion end seals, skeg cross-sections and hydraulic
cylinders are simplified reconstructions without a verified 14352 detail drawing.
The boarding ramp is static and stowed. Forty simple seat proxies prevent an empty saloon;
they do not claim to reproduce the published 57-seat plan. No detailed machinery interior.
Functional running lights still come from the existing Unity light rig.

## Geometry audit

Dimensions include mast and fittings, reported as length x width x height:

| LOD | Triangles | Dimensions, m | Open/non-manifold edges | Degenerate triangles |
|---|---:|---|---:|---:|
| 0 | 59,946 | 23.7201 x 4.5300 x 5.1279 | 0 / 0 | 0 |
| 1 | 26,974 | 23.7152 x 4.5291 x 5.1283 | 0 / 0 | 0 |
| 2 | 7,930 | 23.7060 x 4.5300 x 5.1336 | 0 / 0 | 0 |

See `model-report.json` for raw Blender measurements. Small differences follow bevels and
LOD simplification. LOD2 removes fine fittings and seat detail and omits bevels; aggressive
collapse was rejected because it opened thin geometry. Each export has one mesh renderer
with ten material slots. Separate constructed parts can intersect at intended joints;
the vessel is not a single watertight solid suitable for hydrostatic volume calculations.

## Unity integration

`Luch14352ModelIntegrator.UpdatePrefab` replaces only the visual child and model-dependent
camera/light layout. The original Rigidbody, controller, loader, collision hierarchy and
three BoxColliders are retained. The physics JSON, mass, centre of gravity, buoyancy and
handling calibration are unchanged. Those old simulation parameters include estimates
that differ from newly checked published dimensions; this art replacement does not
silently recalibrate them. Source baseline uses the existing 0.66 m draft, 1 cm different
from the published 0.67 m figure.

`ProceduralVesselBuilder.Build(Luch)` routes to this integrator, so catalogue rebuilding
cannot restore the old mesh. Other vessels still use the existing generator. Old generated
Luch assets are retained as unused historical assets. No procedural and Blender renderers
are overlaid. The catalogue prefab GUID is unchanged; choose Luch in the voyage menu in
Gorodets, River Training or Serpukhov. Scene files need no direct replacement.

CameraNavigator, Waterline, BowReference, SternReference, StarboardReference, UpReference
and WaterJetPoint are exported landmarks. The existing wake uses vessel dimensions and
physics position, not imported mesh children, and remains unchanged. URP Lit materials
are created in `BlenderMaterials`, including transparent glazing. Physics never uses
the render mesh as a collider.

## Review output

`Logs/Luch14352` contains blockout and final Blender renders: port, starboard, front quarter,
rear quarter, top/front and underside/skegs. `Logs/Luch14352/Unity` contains matching imported
model views and LOD1/LOD2 comparisons. Runtime shakedown captures are under `Logs/Shakedown`.
These output directories are ignored by Git. Six final Blender renders are also retained
in this source folder's `Previews` directory. Test and runtime verification results are
recorded in `Assets/ShipSimulator/Documentation/Luch14352_Sources.md`.
