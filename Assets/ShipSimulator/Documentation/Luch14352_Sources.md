# Luch Project 14352: Sources and Data Status

This file separates published particulars from prototype estimates for the skeg air-cushion craft in
`Luch14352.json`. Projects 1435, 14351 and 14352 belong to one family; over 80 were built between 1983
and 1999, mostly for small rivers with landing at unimproved banks. Nothing here is validated for
maritime training.

A skeg air-cushion craft carries most of its weight on a cushion of air trapped between two side
hulls (skegs) that stay in the water. The real 14352 has one waterjet with a reversing/steering
device. The existing simulation's propeller/rudder coefficients remain an estimated force surrogate.

## Published particulars

| Parameter | Value | Unit | Source | Confidence | Notes |
|---|---:|---|---|---|---|
| Length overall | 23.72 | m | [FleetPhoto type Luch](https://fleetphoto.ru/projects/84/), [Vympel design bureau](https://www.vympel.ru/projects/skeg-ships-and-hovercrafts/passazhirskoe-sudno-dlya-malykh-rek-tipa-luch/) | high | Project 14351 is 23.22 m. |
| Beam | 4.53 / 3.85 | m | USC / Vympel | disputed | See the width interpretation below. |
| Height to wheelhouse top from baseline | 3.60 | m | USC, Vympel | high | Family sources also quote 4.46 m with a different height definition. |
| Draft, displacement mode | 0.67 | m | USC, Vympel | high | Simulation retains 0.66 m. |
| Skeg height | 0.45 | m | USC, Vympel | high | Not the same as the 0.60 m aft draft on cushion. |
| Hull depth | 1.25 | m | USC, Vympel | high | Depth, not freeboard. |
| Length between perpendiculars | 21.0 | m | USC, Vympel | high | Simulation retains its earlier 22.5 m estimate. |
| Propulsion | one waterjet | | USC, SPK Fleet | high | Aft machinery compartment. |
| Displacement | 21.2 | t | FleetPhoto (project 14351) | medium | Used as the basis for the 14352 estimate. |
| Passengers | 57 (+15 standing on short routes) | persons | Vympel, FleetPhoto | high | Project 14351 carries 51 (+15). |
| Crew | 2 | persons | FleetPhoto | high | |
| Engine | 1 x 12ChN15/18 (3KD12N-520) | count/type | FleetPhoto, Vympel | high | |
| Power | 1 x 382 | kW | FleetPhoto, Vympel | high | Drives the propeller and the lift fan. |
| Speed | 40 to 44 | km/h | Vympel (40), FleetPhoto (44) | high | 40 km/h used for project 14352. |
| Endurance / range | 8 h / 300 km | | FleetPhoto | medium | |
| Sea state | hovering to 0.6 m wave, reduced speed to 1.2 m | | Vympel | medium | |
| Builders | Urickiy yard (Astrakhan), Moscow shipyard | | FleetPhoto | high | |

## Estimated parameters

| Parameter | Value | Basis |
|---|---:|---|
| Loaded displacement | 23.0 t | estimated from the 21.2 t of project 14351 with the larger passenger capacity |
| Lightship mass | 17.5 t | estimated: displacement minus passengers, luggage and fuel |
| Length between perpendiculars | 22.5 m | estimated |
| Moulded beam, moulded depth | 4.4 / 1.9 m | estimated from beam, freeboard and draft |
| Block coefficient | 0.352 | derived from the estimated displacement and draft |
| Midship / waterplane coefficient | 0.45 / 0.75 | estimated; the real section is two narrow skegs under a wet deck |
| Centre of gravity, radii of gyration | (0, 0.44, -0.3) m; 1.6 / 5.6 / 5.6 m | estimated |
| Hull derivatives and added masses | see JSON | Clarke et al. (1983) and Söding, fitted to merchant hulls, so only indicative here |
| Residual resistance coefficient | 0.003546 | calibrated to 40 km/h with the lift-fan power share and supported-drag factor |
| Rated propeller speed | 1460.3 rpm | calibrated |
| Propeller force surrogate | 1 x 0.65 m | retained simulation estimate, not the physical waterjet geometry |
| Rudder force surrogate | 2 x 0.45 m², span 0.6 m | retained simulation estimate, not evidence for two external rudders |
| Takeoff / full support speed | 3.0 / 7.0 m/s | estimated |
| Supported weight fraction | 0.35 | earlier calibration to a 0.45 m supported draft; published aft draft is 0.60 m |
| Lift power fraction | 0.35 | estimated share of engine power driving the lift fan |
| Supported drag factor / hump | 0.45 / 1.5 | estimated from published cushion-craft resistance trends |
| Windage areas | 14 / 75 m² | estimated from the cabin profile |

The estimated support centre is aligned longitudinally with the loaded centre of gravity for
neutral trim. Four immersion-sensitive patches span half the perpendicular length and the
overall beam; local vertical velocity damps heave, pitch and roll. This approximates ride-height
stability, not measured foil or cushion behaviour. Clearance and bottom contact use the same
appendage geometry inferred from full-support draft. See `ShipSimulator_Physics.md` for limits.

## Blender model, 2026-09-20

The model now depicts the updated 14352 appearance in the
[Vympel archive photograph, approximately 1994](https://fleetphoto.ru/photo/478451/).
The linked page identifies the yard number as `201?`. The two displayed primary
[Luch-202 photographs](https://fleetphoto.ru/vessel/17000/) and the mixed-family
[SPK Fleet arrangement](https://www.spkfleet.ru/luch.htm) were cross-checked. The foreground
Luch-10 in the 2012 photograph is 14351 and was not used as the target cabin.

The [USC catalogue](https://www.nevainter.com/upload/materials/United%20Shipbuilding%20Corporation%20_POTENTIAL_AREAS_OF_COOPERATION_IN_CIVIL_SHIPBUILDING.pdf)
gives 4.53 m breadth overall and 3.85 m maximum beam, while Vympel calls 3.85 m overall.
The art interprets 3.85 m as the main hull and 4.53 m as the side deck/obnose envelope.
That interpretation is plausible but unconfirmed. USC's modern concept illustration does
not represent the 1994 superstructure and was used only for published particulars.

Source: `Art/Source/Luch14352/Luch14352.blend`. Export:
`Assets/ShipSimulator/Models/Luch14352/Luch14352.fbx`. Full modelling decisions, reference
limits, dimensions and topology audit are in `Art/Source/Luch14352/README.md` and
`model-report.json`. LOD budgets are 59,946 / 26,974 / 7,930 triangles. Overall LOD0
dimensions are 23.7201 x 4.5300 x 5.1279 m, including mast and fittings; wheelhouse top
is 3.60 m above the model baseline. All three exports have zero boundary/non-manifold
edges and zero degenerate triangles in Blender's audit.

The low hull, forward wheelhouse, isolated forward side window and eight saloon windows,
roof and ribbed aft machinery casing follow the updated-project photograph. Exact stations,
glazing dimensions, colours and small fittings are photographic estimates. Skeg sections,
cushion seals, waterjet components and ramp hydraulics are simplified reconstructions.

`Luch14352ModelIntegrator` updates the existing catalogue prefab and material assets.
The procedural builder delegates Luch rebuilds to it. Rigidbody, physics scripts, three
BoxColliders and the JSON are unchanged; camera and light positions follow the new model.
The physical parameter discrepancies above are recorded, not silently changed during an art task.
The source uses the simulation's 0.66 m draft to preserve its loaded-waterline origin.

Verified on 2026-09-20 with Unity 6000.6.0f1:

- FBX import and integration: `LUCH_MODEL|PASS`; +Z bow, +X starboard, +Y up,
  waterline at zero, unit scale and no reflected transforms. Imported triangle counts
  match Blender. Six imported views plus LOD1/LOD2 renders inspected.
- EditMode: 199/199 passed, including imported LOD budgets, axes, collider isolation,
  model bounds, camera clearance and unobstructed navigator view through real glazing.
- PlayMode: 40/40 passed.
- Fast-craft shakedown: `VESSEL_SHAKEDOWN|PASS`, both Meteor and Luch. Luch reached
  6.79 m/s (24.5 km/h) in the short test reach, 99% of configured cushion support,
  0.21 m hull rise and 0.1 degree heel. This is not a maximum-speed trial.
  Wake crest was 0.15 m and bow crest 0.49 m, both estimated visual values.
- Inspected gameplay port, chase and navigator captures: the craft advances bow-first,
  the wake trails aft, the lower hull meets the water and the navigator sees through
  the forward glazing. Six camera captures are in `Logs/Shakedown/luch-14352-*.png`.
- Six original serialized physics blocks (Rigidbody, three BoxColliders, loader and
  controller) are byte-identical to the previous prefab; vessel JSON is unchanged.

Logs: `Logs/luch-import-final.log`, `Logs/check-EditMode-20260920-160659-487.log`,
`Logs/check-PlayMode-20260920-160843-456.log`,
`Logs/check-FastCrafts-20260920-161004-378.log`. Test XML files have matching names in
`TestResults`. No compile, shader, or unexpected runtime exception errors were found
in these checks. Gameplay handling remains the existing prototype approximation.
