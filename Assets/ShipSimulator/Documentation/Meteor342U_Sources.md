# Meteor Project 342U: Sources and Data Status

This file separates published particulars from prototype estimates for the hydrofoil in
`Meteor342U.json`. The Meteor family (projects 342, 342E, 342U) shares one hull and foil system;
published figures below describe that family. Nothing here is validated for maritime training.

Note on terms: the Meteor is a hydrofoil, a vessel on submerged foils (судно на подводных крыльях),
not an air-cushion craft.

## Published particulars

| Parameter | Value | Unit | Source | Confidence | Notes |
|---|---:|---|---|---|---|
| Hull length | 34.6 | m | [Wikipedia (ru)](https://ru.wikipedia.org/wiki/Метеор_(теплоход)), [katera-lodki](https://www.katera-lodki.ru/sudno-pr342e/) | high | |
| Beam over the foils | 9.5 | m | same | high | Used as overall beam. |
| Draft afloat | 2.35 | m | same | high | Measured to the foils, not to the hull. |
| Draft foilborne | about 1.2 | m | same | high | Depth of the foils when running. |
| Height afloat / foilborne | 5.63 / 6.78 | m | Wikipedia (ru) | medium | |
| Displacement, light / full | 36.4 / 53.4 | t | Wikipedia (ru), katera-lodki | high | |
| Passengers | 114 to 123 | persons | Wikipedia (ru), katera-lodki | high | Three saloons of 26, 44 and 44 seats. |
| Crew | 3 to 5 | persons | Wikipedia (ru), katera-lodki | medium | |
| Engines | 2 x M-400, 1000 hp each at 1700 rpm | count/type | katera-lodki | high | Later ships used M-401, M-417 or MAN units. |
| Propellers | 2, five blades, 0.71 m | count/type | katera-lodki | medium | |
| Service speed | 65 | km/h | katera-lodki, Wikipedia (ru) | high | Maximum 70 to 77 km/h. |
| Range | 600 | km | Wikipedia (ru) | high | |
| Built | over 400 units, 1961 to 1999 | | Wikipedia (ru) | high | Krasnoye Sormovo and Zelenodolsk. |

## Estimated parameters

| Parameter | Value | Basis |
|---|---:|---|
| Hull draft, foils excluded | 1.15 m | estimated: published 2.35 m includes the foils |
| Moulded beam of the hull | 5.2 m | estimated; only the 9.5 m foil span is published |
| Moulded depth | 2.4 m | estimated from the published height afloat |
| Length between perpendiculars | 33.0 m | estimated from the hull length |
| Block coefficient | 0.271 | derived from full displacement, estimated hull draft and beam |
| Centre of gravity, radii of gyration | (0, 0.45, -0.5) m; 1.87 / 8.25 / 8.25 m | estimated |
| Hull derivatives and added masses | see JSON | Clarke et al. (1983) and Söding, fitted to merchant hulls, so only indicative here |
| Residual resistance coefficient | 0.002959 | calibrated to 65 km/h at full power with the supported-drag factor |
| Rated propeller speed | 1924.5 rpm | calibrated; the engines are rated 1700 rpm, so this is a model value that suits the generic propeller curves, not the real shaft speed |
| Takeoff / full support speed | 8.0 / 13.0 m/s | estimated; no published takeoff speed was found |
| Supported weight fraction | 0.93 | estimated: the hull leaves the water when foilborne |
| Supported drag factor / hump | 0.35 / 1.6 | estimated from published hydrofoil resistance trends |
| Rudders | 2 x 0.6 m², span 0.9 m | estimated |
| Windage areas | 22 / 120 m² | estimated from the cabin and hull profile |

The estimated support centre is aligned longitudinally with the loaded centre of gravity for
neutral trim. Four immersion-sensitive patches span half the perpendicular length and the
overall beam; local vertical velocity damps heave, pitch and roll. This approximates ride-height
stability, not measured foil or cushion behaviour. Clearance and bottom contact use the same
appendage geometry inferred from full-support draft. See `ShipSimulator_Physics.md` for limits.

## Model

The visual model is authored in Blender, not generated. `Meteor342UModelIntegrator` imports its
three FBX LODs and updates only the visual child and the model-dependent camera and light layout;
the Rigidbody, controller, data loader and the three-box collision hull are retained.
`ProceduralVesselBuilder.Build(Meteor)` routes to that integrator, so a catalogue rebuild cannot
restore the old generated mesh. The old generated `Meteor342UMesh.asset` is kept as an unused
historical asset.

Identification: FleetPhoto lists one vessel under project 342U, yard number 062 `Метеор-191`, built
31.07.1984 as project 342E, modernised in the early 1990s and reclassified 342U, renamed
`Преподобный Серафим` in 2006. The hull, foil system and arrangement are reconstructed from the
project 342 family general arrangement and foil drawings; the finished appearance is checked against
photographs of that ship. No external feature could be attributed to the modernisation, so nothing
was invented to distinguish 342U from 342E. The model carries the generic white and blue Meteor
livery without a name or board number.

| Property | Value |
|---|---|
| Editable source | `Art/Source/Meteor342U/Meteor342U.blend` |
| Runtime export | `Assets/ShipSimulator/Models/Meteor342U/Meteor342U.fbx` |
| LOD triangles | 76,926 / 34,615 / 9,620 |
| Material slots | 11, URP Lit, created in `Models/Meteor342U/BlenderMaterials` |
| Model dimensions | 9.54 x 34.60 x 7.54 m over mast and foils |

The model uses the published 34.6 m overall length and 9.5 m foil span as scale controls.
The retained appendages reach 2.35 m below the loaded waterline. Cabin heights, sections,
windows, fittings and paint boundaries are estimated from photographs and an illustrated
family arrangement, not a dimensioned shipyard plan. The rendered hull breadth is near
6.0 m, against the existing 5.2 m physics estimate. Physics and handling were not recalibrated.
See `Art/Source/Meteor342U/README.md` for reference links and reconstruction limits.

## Visual correction, 2026-09-21

Review of the uncommitted Blender source found a missing wheelhouse shell and a face-list
reuse bug that replaced the saloon glazing with stern-screen indices. The initial cabin
was also too tall, with undersized side windows and an unsupported tall bow railing.

The revision rebuilds the glazing as explicit surface panels and the bridge as a complete
raked enclosure. It lowers and rounds the cabin, raises the bow saloon deck, enlarges the
side windows, corrects the paint bands, moves the life rafts and colours the foil supports
and underbody. Light positions now come from exported landmarks. The generic livery and
small fitting dimensions remain estimates, not an exact named-vessel refit reconstruction.

`validate_model.py` adds regression checks for missing glazing, bridge roof, normal winding,
window-surround corner gaps and attachment landmarks. All three LODs have zero degenerate
triangles. The old navigator eye fell behind a new bridge pillar; it was moved to a clear
windscreen panel, with source and Unity regression checks verifying the forward view.

Verification of the revised assets:

- Source regression checks: `METEOR_SOURCE|PASS`.
- Final import: exit 0, `METEOR_MODEL|PASS`, no compiler errors in
  `Logs/Meteor342U/correction-import.log`. Axes and propeller landmark checks passed.
- EditMode: 199/199 passed, `TestResults/check-EditMode-20260921-150412-179.xml`.
- PlayMode: 40/40 passed, `TestResults/check-PlayMode-20260921-150610-294.xml`.
- Fast craft shakedown: both vessels passed,
  `Logs/check-FastCrafts-20260921-150510-285.log`. Meteor reached 12.74 m/s,
  99% configured support and 1.04 m hull rise in the short test reach.
- Six serialized Rigidbody, collider and runtime component blocks match HEAD exactly;
  `Meteor342U.json` is unchanged. No changes were committed.
- Blender side, top, bottom, bow, stern, quarter and waterline captures inspected;
  Unity LOD previews and runtime port/navigator captures inspected.
  Retained images are in `Art/Source/Meteor342U/Previews`.

The September 20 results below describe the previous visual revision.

## Verification, 2026-09-20

- Batch compile: exit 0, no `error [A-Z]` in the log.
- EditMode: 199 of 199 passed. PlayMode: 40 of 40 passed.
- `Meteor342UModelIntegrator.Run`: `METEOR_MODEL|PASS`; exported axes and the propeller landmark
  both agree with the vessel JSON.
- `VesselShakedownCheck.RunFastCrafts`: `VESSEL_SHAKEDOWN|PASS`, Meteor 24.8 kn, 99 per cent foil
  support, 1.04 m hull rise, 0.1 degree heel, no collision.
- `VoyageMenuSmokeCheck.Run`: `MENU_SMOKE|PASS`, so start, save, scenario and vessel change, load,
  pause and resume still work with the replaced prefab.
- Displacement and foilborne attitudes checked against a water plane in Blender, using the hull rise
  measured in the shakedown. Captures in `Art/Source/Meteor342U/Previews`.

These are automated checks. They do not establish subjective handling feel, and nothing here is
validated for maritime training.
