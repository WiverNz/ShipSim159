# Volgo-Don 507B: Part 1 Audit

Part 3 continuation: [PART3_REVIEW.md](PART3_REVIEW.md) records the deck-depth
correction, replacement propellers/nozzles, PBR maps and current limitations.
The three-bladed/faceted components listed below refer to the unchanged baseline.

Continuation: Part 2 is implemented. See [README.md](README.md) for the independently
editable source, corrected UVs, 22 removed degenerate triangles, candidate export,
Unity verification and remaining discrepancies. Measurements below retain the
unchanged Part 1 baseline for comparison.

Date: 2026-10-21. Scope: reference review, non-destructive FBX inspection,
baseline captures and static Unity architecture review. No runtime changes.

## Decision

Keep and repair the existing model. Its long hull, aft accommodation, foredeck,
two covered cargo zones and paired cover panels support the classic two-hold
507B target. Four visible cover panels do not mean four holds. This is a
configuration identification, not proof of a particular vessel's identity.
No name, yard number or dated livery has been established for the imported mesh.

The model is a useful foundation, not an accurate finished engineering model.
There is no evidence that wholesale reconstruction is necessary. Repair UV/material
mapping first, then validate normals and make localized geometry corrections.

## Reference review

All nine pages of [doc257.pdf](https://russrivership.ru/public/files/doc257.pdf)
were rendered and visually inspected, rather than relying on extracted text.
See the archive addendum in
`Assets/ShipSimulator/Documentation/VolgoDon507B_Sources.md` for numerical findings.
The temporary PDF checksum is recorded in `baseline-checksums.json`; no archival
scans or third-party photographs have been added to game assets or this source folder.

The [FleetPhoto project record](https://fleetphoto.ru/projects/1697/) distinguishes
early open-bunker ships from later covered two- and four-hold versions and warns of
raised coamings and shortened conversions. This supports retaining a classic,
unshortened two-hold configuration instead of combining generations.

The supplied [vessel 4152](https://fleetphoto.ru/vessel/4152/) is Volgo-Don 185,
built in 1975. [Photo 550661](https://fleetphoto.ru/photo/550661/), Pantikapey,
11 July 2026, was visually inspected for its accommodation geometry: forward
bridge glazing, tiered aft accommodation, side stairs, funnel and lifeboat position.
The model has the same broad arrangement. The photo does not establish the model's
identity, original livery, hatch arrangement or its historical equipment fit.
Do not copy the photo's modern additions without evidence for the chosen target.

[Vessel 12387](https://fleetphoto.ru/vessel/12387/) is Volgo-Don 25, the 1965
lead ship, unsuitable as the sole covered-hold reference. Its photo-page fetch
returned `Cache miss`; no claim is made to have visually inspected those photos.
[AL Shipping](https://alshipping.ru/type-volgo-don/) combines several project
numbers on one page, so it is corroborating family context only.
[Volgaflot](https://www.volgaflot.com/commerce/) describes the commercial fleet;
it does not identify this mesh or resolve the chosen historical fit.

## Measured geometry baseline

Method: Blender 5.2.2 LTS, registered MCP, default FBX import into a new
`VolgoDon507B_Audit` scene. The pre-existing unsaved default scene was preserved.
Addon protocol 13 differs from server expectation 14, but status, scene inspection,
Python execution and rendering succeeded. No alternative connection was used.

Full per-object counts, transforms, world-space bounds, material assignments,
UV channels and image paths are in `audit-baseline.json`.

| Measurement | Result |
|---|---|
| Imported hierarchy | 2 empties, 19 mesh objects |
| All meshes, including proxy | 37,101 vertices; 42,333 triangles |
| Render meshes, excluding proxy | 18 objects; 42,321 triangles |
| Main hull | 544 triangles |
| Each propeller | 84 triangles |
| Each nozzle/rudder assembly | 140 triangles |
| UVs / custom normals | One UV channel and custom normals on every mesh |
| Face smoothing | All imported faces flagged smooth |
| Loose vertices | None |
| Degenerate faces, local area below 1e-10 | 15: hold 9, accommodation 6 |
| Raw boundary / non-manifold edges | 29,796 / 29,859, including proxy |
| Visible Blender bounds X | -8.083045 to +8.083045 |
| Visible Blender bounds Y | -0.666849 to 133.900681 |
| Visible Blender bounds Z | -0.070703 to 15.895500 |

These are imported Blender world coordinates, not final Unity dimensions.
The imported ship points along +Y with +Z up; most mesh origins are at
(0, 66, 3.7), inherited through the root hierarchy. Source mesh-local axes and
Unity's legacy conversion differ from these Blender world axes. Unity's stored
visual scale is 102.69789 on each axis, reflecting its FBX unit conversion.
Do not copy that number onto a new metre-based export.

Visible source extent is approximately 134.568 x 16.166 x 15.966 m in
length/beam/height order. Uniform normalization of that visible length to
138.3 m would give approximately 16.615 m beam. This is a diagnostic ratio,
not a fresh measurement of the live Unity instance. The integrator includes
all renderer bounds, including hidden proxy bounds, when scaling and aligning.
Part 2 must separate hull, appendage and proxy bounds before choosing final scale
and keel datum. A nozzle extends slightly below the hull baseline.

## Topology, shading and materials

Raw boundary counts overstate holes: the imported triangle mesh contains many
split vertices. A diagnostic weld at 0.00001 on disposable BMesh copies leaves:

| Component | Boundary edges | Non-manifold edges |
|---|---:|---:|
| Hull | 52 | 58 |
| Accommodation | 376 | 419 |
| Holds | 122 | 122 |
| One nozzle assembly | 42 | 48 |
| One propeller | 18 | 18 |

The diagnostic never writes back to the source mesh. Open rims, thin rails,
glazing and equipment interfaces can be intentional. Do not blanket-fill or
globally weld the asset. Surface-by-surface repair must preserve UV seams and
deliberate hard edges. This audit does not certify a watertight displacement solid
or exhaustively exclude every internal self-intersection.

The matched clay/textured superstructure renders are decisive: large angular
patches disappear under a uniform material while the geometry and normals remain
unchanged. Therefore the visible defect is substantially in the color texture/UV
path, not proof that all normals are wrong. Precisely distinguishing mismatched
atlas regions, imported UV interpretation and baked texture content is Part 2 work.
Preserve custom normals until controlled comparisons justify changes.

Eight DDS color/AO maps load successfully: six at 2048 square, detail and interior
at 1024 square. The lights HDR is 256 square. Blender imports duplicate image
datablocks, but the referenced disk resources exist. This is an atlas-based asset;
do not replace its UVs wholesale with box projection. There are 11 assigned
material names including proxy and the generic propeller material; Unity also has
an unassigned fallback material. Existing hull URP material lacks normal,
metallic/smoothness and separate AO maps. Glazing is not a clean independent
material group in the imported accommodation mesh.

## Repair priorities

| Priority | Finding | Next action |
|---|---|---|
| P0 | Texture/UV-dependent patchwork dominates accommodation appearance | Diagnose atlas mapping with original textures before a normal rewrite |
| P0 | Different import coordinate/unit conventions | Document one export transform and verify candidate bounds in Unity |
| P1 | 15 degenerate triangles | Repair locally, preserving neighbouring UVs |
| P1 | Propellers visibly have three coarse blades; nozzles are faceted rings | Rework these small components to the verified reference, retain distinct objects |
| P1 | Propeller transforms include 1.4 scale and approximately 10-degree tilt | Establish actual shaft axes and center pivots before animation |
| P1 | Covers share a mesh; anchors and much equipment are merged by material | Separate mechanically meaningful parts in Part 4, not by every triangle island |
| P1 | Nozzle/propeller clearance and openings are schematic | Check swept clearance and watertight structural seams during localized repair |
| P2 | 544-triangle hull has coarse end sections | Compare sections and improve only where silhouette/underwater shape needs it |
| P2 | Equipment, windows and railing proportions remain approximate | Trace dated references and drawing stations, keep uncertain details labelled |
| P2 | No established usable distance LOD chain | Preserve a high-quality baseline; create measured LODs in Part 8 |

Names containing `lod_1` on the propellers do not prove a complete LOD system.
The imported tree has only one `ship_type_507_lod_0` assembly. Visual inspection
shows highly simplified propellers/nozzles and intersecting assembly interfaces;
an exhaustive motion-clearance check requires the pivots/mechanism work later.

## Unity architecture and feature audit

Paths below are relative to `Assets/ShipSimulator/Scripts/`.

| Requested feature | Existing implementation | Status and required work |
|---|---|---|
| Independent engines | `Physics/ShipPhysicsController.SetEngineCommand`, `EngineShaft`, per-engine UI telegraphs | Exists; retain and calibrate |
| Inertia and reversal | `ManoeuvringModel`, added mass, `EngineShaft` governor and restart delay | Exists with estimated parameters |
| Steering | `RudderModel` MMG lift model, one shared `RudderAngleRad` | Incomplete for independently steerable nozzles; propeller thrust remains longitudinal |
| Bow thruster | `BowThrusterModel`, command ramp, speed and immersion reductions | Exists as tunnel-disc approximation; investigate compatibility with archive equipment |
| Loading | `VesselParameters`, `ConfigureLoading`, `HydrostaticsModel` | Partial: one load fraction, proportional draft and mass-scaled inertia; fixed CG/gyration radii |
| Ballast tanks | No tank-state model found | Missing; separate from generic load fraction |
| Hatch/anchor mechanisms | No corresponding controllers or bindings found | Missing |
| Propeller/nozzle animation | Imported objects exist; no bindings by their names found in runtime code | Missing for detailed geometry |
| Navigation lights | `Visuals/NavigationLightRig`, `NavigationLightSectors`, `VesselLayout` | Exists; check fixture placement after visual corrections |
| Water/wake | `ShipWakeController`, `ShipWakeTrack`, water shader | Exists; retain presentation-only relationship |
| Save/load | `Persistence/VoyageSave`, controller restore/capture | Engine/shaft/thruster/shared-rudder state exists; cargo, tanks and hatches absent |
| Audio | HUD horn `AudioSource` | No dedicated engine/propeller/mechanism attachment scheme found |

`ShipPhysicsController` applies dynamics through Rigidbody forces/accelerations.
The prefab's serialized mass of 1 kg is initialization data, not the runtime
ship mass: `ConfigureLoading` assigns mass, CG and inertia from vessel parameters.
Do not treat this field alone as a broken physics setup.

The prefab retains one `DetailedVisual` reference to the original FBX and three
box collision sections: midship 16.2 x 4.6 x 82 m, bow 11.5 x 4.1 x 28 m, stern
13.5 x 4.3 x 22 m. Their centers are preserved in the prefab. The vessel ID remains
`volgodon-507b`. Preserve `VesselDataLoader`, physics controller, layout, root and
catalogue identity when replacing only the visual branch.

Two editor paths write material properties: `VolgoDonModelIntegrator.CreateMaterial`
sets hull smoothness 0.2, while `ShipSimulatorVisualUpgrade.TuneVesselMaterials`
sets 0.46 and metallic 0.12. The latter matches the saved hull material. A new PBR
pass must reconcile these writers or a later menu action can undo it.
`VesselCatalogueBuilder` retains the existing 507B prefab and ensures its layout;
do not regenerate the whole catalogue merely to replace this visual.

## Baseline captures

All images use unchanged source mesh/UV/custom-normal data. Display-only texture
relinking and audit lighting were added to the isolated scene. These are Blender
diagnostic renders, not new Unity runtime verification.

- [Front](Previews/baseline-front.png)
- [Side](Previews/baseline-side.png)
- [Top](Previews/baseline-top.png)
- [Stern](Previews/baseline-stern.png)
- [Perspective](Previews/baseline-perspective.png)
- [Propulsion detail](Previews/baseline-propulsion.png)
- [Clay diagnostic](Previews/baseline-clay-superstructure.png)
- [Matching textured diagnostic](Previews/baseline-textured-superstructure.png)

## Reversible continuation

Original FBX, textures, materials, model metadata, vessel JSON and prefab have
SHA-256 entries in `baseline-checksums.json`. Keep them in place as the fallback.
Part 2 creates `VolgoDon507B.blend` here and exports to the separate
`Assets/ShipSimulator/Models/VolgoDon507B_Improved/VolgoDon507B.fbx` path.
Do not switch the active prefab before the candidate's axes, bounds, resources
and appearance pass inspection. Do not run legacy integration on an export with
different axis conventions without adapting it first.

Part 1 does not create the production `.blend` or candidate FBX. The isolated
audit scene is left in Blender for inspection; the original unsaved scene remains.
No compile/runtime tests were needed for documentation-only changes. Static code
inspection identifies existing capabilities, but does not certify current handling.
