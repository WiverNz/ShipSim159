# Part 4: movable geometry and mechanical motion

Completed 2026-10-22. This milestone prepares independently controlled geometry
and a review animation. Gameplay integration remains Part 5.

## Artifacts and use

- Editable source: `VolgoDon507B.blend`, 49 local render meshes, 53,130 triangles,
  12 materials, packed images and preserved two-channel UV contract.
- Candidate: `Assets/ShipSimulator/Models/VolgoDon507B_Improved/VolgoDon507B.fbx`.
- `mechanisms.json`: 15 named bindings with hierarchy paths, pivots, axes, units,
  limits, states and confidence. A copy accompanies the Unity candidate.
- `part4-validation.json`: geometric sweep results.
- `part4_rig.py`: one-time conversion of the Part 3 source; refuses a second run.
- `part4_validate.py`: checks the current source, including collision regression.
- `part4_finish.py`: renders, exports the animation and saves the editable source.

Run Blender scripts through the registered MCP connection. To rebuild the rig,
start from the retained `Logs/VolgoDon507B/before-part4.blend` checkpoint; do not
apply historical Part 2/3 mutation scripts to the current rig. The final `.blend`
opens independently without the checkpoint. Scrub frames 1-770 at 24 fps to review.
Timing is accelerated demonstration timing, not measured machinery performance.

## Mechanisms and reference comparison

The classic two-covered-hold arrangement remains the selected configuration.
[Archive page 4](https://russrivership.ru/public/files/doc257.pdf) describes lifting
the extreme covers, moving the middle covers beneath them, lowering onto the
middle covers, then moving the pairs using winches. This permits either hold to
open fully or both to open halfway. The animation follows that sequence.

| Assembly | Implementation | Evidence and limits |
|---|---|---|
| Propellers | Independent port/starboard shaft controls; shaft axis source Y | Part 3 geometry retained; rotation sign and RPM binding remain Part 5 |
| Nozzles | Independent stock controls; source Z rotation, +/-35 degrees | Travel from archive p.7; stock locations estimated |
| Four hatch panels | Original skins, handles, rolling gear and seal strips separated | Lift and longitudinal translation; no hinge substitution |
| Two lift assemblies | Original lifting heads plus extending jack rods | Stroke estimated from model clearances |
| Three anchors | Independent pivots and chain attachment empties | Short unseating preview only; payout and chain geometry deferred |
| Two windlass drive parts | Separate original circular drive cheeks | Rotation axis measured from geometry; gearing/rates uncalibrated |

All controls are children of the identity `Mechanisms` root. Structural geometry
does not move. Source coordinates are +X starboard, +Y bow, +Z up, in metres.
Unity positions map to (X,Z,Y); signed axial rotation vectors map to (-X,-Z,-Y).
Use the manifest and imported local basis when binding controls, not assumptions
about raw FBX Euler channels.

The formerly continuous deck beneath the covers now has two actual apertures.
Simple inner walls and floors make the open holds visible. Their interior depth
and construction are visual approximations, not tank or hydrostatic measurements.
Added longitudinal tracks support the travel demonstration. Existing hull,
accommodation, materials, texture mapping and useful custom normals are retained.

## Review sequence

| Frame | State |
|---|---|
| 1 | Closed and stowed |
| 30 / 60 | Opposite nozzle travel limits; propeller rotation demonstration |
| 100 | Extreme panels lifted 1.05 m |
| 120-180 | Middle panels lift 0.25 m and slide beneath extreme panels |
| 210 | Both holds half open, extreme panels at 0.93 m stacked elevation |
| 300 | Forward hold fully open |
| 390 | Return to both half open |
| 480 | After hold fully open |
| 570-710 | Return, unstack, slide and lower to closed |
| 740 | Anchors briefly unseated, windlass drive rotation |
| 770 | Closed and stowed again |

Lift heights and travel offsets are model-derived estimates. Unequal inherited
cover lengths require a small longitudinal adjustment during stacking. The minimum
checked gap between stacked assemblies is 0.08054 m. This is model clearance,
not an archival machinery tolerance.

## Verification

The source was saved, reopened through Blender MCP and validated again. Checks
passed for 49 local meshes, 53,130 triangles, UV pairing, no degenerate triangles,
15 controls and reference dimensions 138.3 x 16.7 m. Hull keel remains -3.53 m.

Movement checks sample all 541 frames from 120 through 660: complete hatch
assemblies against one another and panel cores against fixed geometry. Propeller
clearance passes 720 configurations: two sides, 15 nozzle angles and 24 shaft
angles. A deliberately lowered stacked panel causes the collision check to fail,
then is restored. All three unseated anchors clear the hull/deck at frame 740.
Closed seal contacts and stowed-anchor hawse contacts are intentional exceptions.
These are discrete geometric checks, not a continuous mechanical simulation.

Unity reviewer `ShipSimulator.Editor.VolgoDonCandidateReview.Run` checks imported
geometry, materials, datums, independent pivots, animation positions/rotations and
stable mesh attachments. It also renders closed and moving states. Animated FBX
export must use `bake_space_transform=False`: experimental mesh-space baking
caused animated children to detach even while control pivots passed their checks.
The mesh-attachment regression check guards that failure.

Final Unity review passed with actual exit 0 and no compiler errors in
`Logs/VolgoDon507B/part4-candidate-final.log`. Both mechanism and candidate PASS
markers are present. Final forward/after open-hold and nozzle captures were
visually inspected after the export correction. All 200 EditMode tests passed in
`TestResults/check-EditMode-20261022-183641-410.xml`. All 47 original baseline
resource hashes still match. The active prefab, catalogue and physics remain
unchanged; no runtime shakedown was run for this uninstalled candidate.

## Images

| State | Blender | Unity |
|---|---|---|
| Closed | [View](Previews/part4-closed.png) | [View](Previews/part4-unity-perspective.png) |
| Lift | [View](Previews/part4-lift.png) | [View](Previews/part4-unity-lift.png) |
| Slide under | [View](Previews/part4-slide-under.png) | [View](Previews/part4-unity-slide-under.png) |
| Both half open | [View](Previews/part4-both-half-open.png) | [View](Previews/part4-unity-both-half-open.png) |
| Forward hold open | [View](Previews/part4-fore-open.png) | [View](Previews/part4-unity-fore-open.png) |
| After hold open | [View](Previews/part4-aft-open.png) | [View](Previews/part4-unity-aft-open.png) |
| Nozzle limit | [View](Previews/part4-nozzles-port.png) | [View](Previews/part4-unity-nozzles-port.png) |
| Anchors unseated | [View](Previews/part4-anchors-unseated.png) | [View](Previews/part4-unity-anchors-unseated.png) |

Additional Unity front, side, top, opposite nozzle limit and closed-return views
are under `Previews/part4-unity-*.png`.

## Explicitly deferred mechanisms and remaining work

The archive lists 16 vertical jacks, four horizontal jacks and two winches. The
legacy model supplies 12 identifiable vertical jack positions. Those positions
are retained; the four missing vertical and four horizontal jack locations,
hydraulic lines, winch cable routing and detailed transmission remain unresolved.
Do not present the simplified lifting/translation rig as a complete hydraulic
reconstruction. Full anchor payout, chain catenary, seabed attachment and braking
are also deferred. The current anchor demonstration only releases the stowed
geometry by approximately 0.35 m.

No new external texture assets were introduced. Historical livery identity,
material resolution, detailed hold structure and Part 3 hull approximations remain
as documented. LOD preservation and draw-call/performance budgets remain Part 8.
Part 5 should connect these controls to existing shaft, steering, command and save
systems, preserving the physics root and original fallback. The candidate is not
installed in the playable prefab by this milestone.
