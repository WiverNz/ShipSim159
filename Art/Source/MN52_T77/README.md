# MN-52, Project T-77

Operational early-1990s reconstruction of the 1989 Serpukhov petroleum barge.
This is a photo-based reconstruction, not a surveyed replica. The new barge and
push-unit prefab leave the existing Serpukhov model and solo prefab unchanged.

## Use in ShipSim159

In the existing vessel menu choose **Серпухов + МН-52**, then **River
familiarisation** or **Gorodets passage**. Use the existing W/S engine, A/D rudder,
V camera, Space stop and R reset controls. All propulsion comes from Serpukhov.
The navigator view stays in the tug; external views cover the full combination.
The barge's raised cabin obstructs the tug's forward eye-level view in this
centreline arrangement. Use the elevated or external cameras for navigation.
The existing HUD draft and grounding telemetry describe the tug, not the barge.
Save/load preserves both bodies and their connection. Serpukhov alone remains a
separate command in the same menu.

The 50.65 m push unit exceeds the existing Nara/zaton passage's 40 m limit. Its
menu entry is disabled for that passage rather than shrinking either vessel or
changing the passage limits. The standalone barge can be placed in a scene from
its prefab. It has no player controller or independent propulsion.

`PushUnit.Detach()` releases the joint and restores the pair's previous collision
state. There is no new player attach/detach control. `Attach()` requires the
connection points and headings already aligned; it does not teleport a barge.

## Delivered assets

- `MN52_T77.blend`: editable Blender scene, authored via registered Blender MCP.
- `Assets/ShipSimulator/Models/MN52_T77/MN52_T77.fbx`: three batched LODs, two
  renderers each, with glazing separated from opaque geometry.
- The model folder contains 11 shared URP Lit materials and 18 original 512px
  base-color, tangent-normal and metallic/smoothness maps. Other small material
  classes use constant PBR values. Maps are also packed in the Blender source.
- `Assets/ShipSimulator/Prefabs/Vessels/MN52_T77.prefab`: separate passive vessel.
- `Assets/ShipSimulator/Prefabs/Vessels/Serpukhov_MN52.prefab`: variant of the
  existing Serpukhov prefab, plus coupling configuration and wider camera views.
- `Assets/ShipSimulator/Data/Vessels/MN52_T77.json`: SI masses, inferred sections,
  resistance and windage data. No engines, propellers, rudders or thrusters.
- `model-report.json`, `geometry-validation.json`, `Previews/`: counts, checks
  and original render captures. See `VALIDATION.md` for executed Unity tests.
- `ChangedFiles.txt`: created and modified files, relative to the project root.

Hull length is 28.8 m, moulded beam 6.0 m and side height 1.5 m. Guards/fenders
reach approximately 6.29 m; anchors and fittings extend the visual length to
approximately 28.92 m. Mast top is approximately 8.6 m above baseline, inferred
from photographs. The model is metric. Blender uses X starboard, Y forward and
Z up, with baseline origin. Unity uses Z forward, Y up and a root on the loaded
1.2 m waterline. Named landmarks verify the export conversion.

The current triangle counts are recorded in `model-report.json`. The editable
source keeps 386 mesh objects in functional collections; runtime LODs batch
static details. Nine hidden collision references are separate from render meshes.

## References and uncertainty

The exact vessel photographs take precedence over other T-77 examples:

- [FleetPhoto vessel record](https://fleetphoto.ru/vessel/3081/): 6 October 1989,
  Aral yard number 694, completed at Khlebnikovsky, registry 147515, historic
  Serpukhov registration. The board number М-04-946 is documented in later
  records; its early-1990s placement was not verified and is not painted on.
- [Project description](https://fleetphoto.ru/projects/335/): single-deck steel
  petroleum barge with spoon ends and transoms, 28.8 x 6 m, 1.5 m depth,
  153/52.5 t full/empty displacement and 1.2/0.5 m draft. The table's 59 t
  deadweight conflicts with both displacement difference and nominal 100 t
  capacity; it is not used as the payload budget.
- [89189](https://fleetphoto.ru/photo/89189/?vid=3081): principal forward-quarter
  reference for hull ends, anchors, bulwarks, cabin, lower enclosure and hose gear.
- [32577](https://fleetphoto.ru/photo/32577/?vid=3081): aft double doors, steps,
  exposed diagonals/columns and raised cabin proportions.
- [4585](https://fleetphoto.ru/photo/4585/?vid=3081): opposite side, piping and
  equipment arrangement. The accompanying tug is RT-291, not Serpukhov. This
  photo does not establish the requested historical pairing or a centreline
  pushing connection. The playable pairing is an engineering/gameplay inference.
- [35342](https://fleetphoto.ru/photo/35342/?vid=3081): forward transom and
  relationship to neighbouring vessels; the large open hold is another barge.
- [508862](https://fleetphoto.ru/photo/508862/?vid=3081): 2025 surviving structure,
  not evidence for early-1990s corrosion, every later appliance or paint hue.
- [RussianShips exact vessel](https://russianships.net/id232858): confirms
  registry, yard and operator identity; page metadata examined.
- [Korabel technical listing](https://www.korabel.ru/fleet/info/29718.html):
  28.2 m, 6.3 m, 140 t and 1.06 m for its entry. These conflicting values are
  recorded, not averaged into the chosen geometric target.
- [Su Zholdary operator listing](https://su-zholdary.kz/filialy/semeyskiy-filial/potrebitelyam/harakteristiki-sudov/):
  1990 T-77 N-3 listed as 28.8 x 6.31 m, 1.2 m draft, 100 t cargo, no propulsion.
- [Irbis documentation lead](https://irbispluscom.ru/skhemy-chertezhi-proyektov-sudov):
  lists T-77 documentation/electrical diagrams, but no public lines plan was
  obtained. No company was contacted and no paid documentation was acquired.
- Repair procurement metadata examined for
  [NT-101](https://poisktenderov.ru/item/0319100001018000087/),
  [NT-15](https://poisktenderov.ru/item/0387100002225000152/) and
  [NT-102](https://synapsenet.ru/zakupki/fz44/0322100015825000021%231--habarovskij-kraj-vipolnenie-rabot-po-zamene-naruzhnoj).
  No verified hull-offset drawing was recovered from these leads.

All five supplied FleetPhoto images were downloaded for private visual research
and examined. Some browser fetches failed, but direct image downloads succeeded.
They remain outside distributable Assets and are not textures or packed references.
All delivered maps and geometry are original. Cyrillic names are converted meshes,
so Unity does not require a font installation.

The photographs show a raised cabin on open framing, with a roof coaming/box.
They do not support inventing a modern glazed upper wheelhouse. The small windows,
double aft doors, supports, lower cabinet and large hose arrangements follow the
exact vessel. Hidden pipe routes, valve internals, unseen side differences,
underwater offsets and precise equipment dimensions are conservative estimates.
The 1991-1996 paint condition is reconstructed: muted grey, off-white, dark deck,
restrained grime and small rust streaks. No supplied photo verifies every feature
or color in that period. The accommodation interior is not furnished.

## Physics and connection

`PassiveVessel` reuses `VesselParameters`, `HydrostaticsModel`, `ManoeuvringModel`,
current fields, weather and scenario bathymetry. It is not a second player ship
controller. The shared model accepts explicit non-self-propelled data with zero
actuators. Existing powered-vessel validation is retained.

The loaded Rigidbody mass is 153,000 kg, consistent with the project's SI
architecture. Lightship data is 52,500 kg. The inferred section table is fitted
to those masses at 1.2/0.5 m draft; it is not a recovered shipyard lines plan.
There are 19 longitudinal stations and 16 transverse strips. Loaded centre of
mass is 0.85 m above keel; gyration radii and hydrodynamic derivatives are
estimated. The delivered prefab uses the loaded condition; there is no new
interactive cargo transfer UI or tank free-surface model.

Nine independent box colliders approximate the middle hull, three transition
segments at each end, elevated cabin and pump enclosure. Fine deck hardware is
not a dynamic collision mesh. The barge retains physical world/bank contacts;
unlike the player's grounding controller it uses the scene's bed colliders,
without the player's grounding damage/scoring UI.

The push unit spawns MN-52 as a separate scene-root object with its own mass,
buoyancy and resistance. A configurable joint locks all six relative degrees of
freedom at matching anchors. It uses higher local solver iterations and a small
projection tolerance, not transform-following or a merged Rigidbody. Pairwise
collision suppression prevents interface chatter; world collision layers remain
unchanged. Detaching restores the earlier pair settings. The contact anchors are
estimated rigging locations, not documented MN-52 coupling hardware.

The shared water shader places the coupled bow wave at MN-52's bow and the wash
at Serpukhov's stern. Wave amplitude remains estimated. Existing hydrostatics use
the water level rather than the visual shader's instantaneous wave surface, so
no new physical wave-sampling framework or cargo slosh is claimed.

## Reproduction

Use Blender MCP to execute `01_model.py`, `04_refine.py`, then
`05_hydrostatic_hull.py`, `07_fit_shell_fittings.py`, `02_finish_export.py`,
`03_review.py` and `06_validate.py`. Generate the JSON with `create_data.py`
before the hydrostatic hull step. Source scripts use the local Windows project
path; change ROOT for another checkout. The original Serpukhov scene/file is
preserved. Saving MN-52 uses a scene-only Blender library write.

In Unity use **Ship Simulator > Integrate MN-52 and Serpukhov push unit**.
The integrator rebuilds only the new assets and their catalogue entry. The
catalogue builder retains the unit. `VesselShakedownCheck.RunPushUnits` exercises
Serpukhov solo and the combined unit in the actual river scene.

This remains an engineering/gameplay prototype. It is not validated maritime
training software or a measured reconstruction of MN-52's handling.
