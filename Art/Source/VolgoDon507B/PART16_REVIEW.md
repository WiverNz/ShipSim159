# Part 16: contact geometry

Date: 2026-10-24. Status: completed with documented approximation limits; Part 17 integration passed.

The previous three boxes span z -65 to +62 m. Their bottom faces are -3.55,
-3.10 and -3.30 m, irrespective of the raised bow and stern underbody. Against
4253 rendered hull vertices below y=1.97 m their maximum uncovered distance is
5.969 m and mean 0.955 m. This measures missing coverage, not excess volume.

`VolgoDonContactBuilder` clips the imported corrected `VD507B_hull` at thirteen
longitudinal intervals (-70, -66, -64, -62, -60, -54, -44, -25, 25, 44, 54, 62, 66, 70 m).
Each clipped shell is cooked as a convex MeshCollider. Smaller end intervals
follow the curvature; the parallel middle body needs fewer pieces. No decorative
meshes, screws, nozzles or deck-walking colliders are added. The contact cap is
1.97 m above the design waterline, the hydrostatic cap, not the bulwark rail.
Mass, CG, inertia and the existing Rigidbody are not assigned by this builder.

The new maximum uncovered distance is 0.066 m, mean 0.0022 m. PhysX convex cooking
and convex envelopes are approximations; they can bridge concavities. The numbers
in `part16-coverage.json` do not establish historical hull accuracy. Higher
superstructure/bulwark collisions remain outside this contact model.

The normal integrator and collision upgrade call the same builder. Thirteen generated
mesh assets retain their GUIDs; repeating integration retains section objects.
The legacy visual retains its original three-box fallback.

## Reaction ownership

PhysX owns quay and independent obstacle impacts. GroundingController owns soil
normal pressure and bottom friction. Gorodets bank boxes and visible rocky shoals
are marked `BathymetryContactSurface`, so their pairs with a bathymetry-driven
vessel are ignored. They represent the same bottom already sampled by grounding.
The quay is not marked. River natural banks without that marker retain PhysX
contact. The scenario builder installs the markers, as does the focused migration
in `VolgoDonContactBuilder.Run`; no full scenario regeneration is needed.

GroundTackle still applies only chain/line tension and its lead-height moment.
It has no contact collider, joint or fixed-position constraint. It does not
supply another hull-ground or hull-quay reaction.

## Reproduction

Run `ShipSimulator.Editor.VolgoDonContactBuilder.Run` in a dedicated Unity editor.
PlayMode `VolgoDonContactTests` exercises side, bow, stern and five-degree contact
at 0.5 m/s (side approach 0.05 m/s plus 0.5 m/s tangent) across 0.01/0.02/0.04 s steps and full, partial, lightship and ballast
loading. Metrics: penetration, speed, vertical rise and travel; side cases must
retain tangential movement. Results: `Logs/Part16/contact.jsonl`.

`GroundingRecoveryTests` uses the integrated 507B prefab for light/heavy shoal
contact, reverse recovery and discharge recovery. `GroundTackleTests` checks
four taut lines against the actual hull and anchor loading; these existing cases
are retained rather than copied. Results are recorded below and repeated in Part 17.

## Additional diagnostics and corrections

649 transverse rays at y=-3,-2,-1,0,+1 m compare the actual hull with cooked
convex solids. Old maximum excess breadth: 6.591 m; missing breadth: 6.224 m.
New maximum excess: 0.481 m; missing: 0.033 m. This is a sampled diagnostic,
not a global Hausdorff bound. The stern underside remains the largest convex
approximation; the initial ten-piece candidate had 4.17 m excess there and was
rejected. Data: `part16-cross-sections.csv`; waterline diagram:
[contact overlay](Previews/part16-contact-overlay.svg).

Grounding now reads contact positions/normals from Rigidbody pose, including
immediately after restore. This avoids the same stale-Transform hazard already
fixed for anchors in Part 14. No saved data schema changed.

The first full run was 124/144, not accepted. One old anchor comparison left its
first vessel alive when starting the second experiment; their expanded hulls
collided. The first vessel is now destroyed between independent experiments.
The new fixture initially omitted the scene-owned GroundingController. Its side
approach was accidentally 45 degrees instead of glancing, and its COM displacement
bound treated yawing into alignment as tunnelling. Corrected setup uses a 1:10
normal/tangential velocity ratio and checks every hull's support bound against the
quay face; penetration/speed/rise limits are unchanged. No force coefficient or
friction was tuned to make those assertions pass.

Sideways light-shoal recovery is checked with a specified 30 kN external lateral
pull on a 1:100 sandbank. This proves permitted physical movement, not an ability
to move sideways under the vessel's own machinery on every shoal.

## Verified runtime results

Unity 6000.6.0f1, full PlayMode **158/158**, exit 0:
`TestResults/check-PlayMode-20261024-174300-337.xml` (217.1 s).
All 48 contact combinations passed. Maximum penetration 0.0200 m (contact skin),
maximum upward excursion 0.00150 m, maximum speed 0.5025 m/s. No tested hull
section crossed the quay plane. Tangential movement is retained. Raw final rows:
`part16-contact-cases.json`; they supersede the rejected first run.

The 30 kN lateral pull freed the light bank contact after 5.50 m lateral travel,
with final clearance 0.050 m and initial peak reaction 202 kN. Astern light-shoal
recovery, heavy grounding holding until discharge, all three timestep cases,
anchor/mooring forces, old saves and the immediate-restore regression passed in
the same suite. This does not guarantee recovery from every grounding.

EditMode 267/267 also passed: `TestResults/check-EditMode-20261024-174711-959.xml`.
Part 17 repeated both complete suites successfully after the final export/integration.


Final combined-contact addition (2026-10-25): `GroundingUnderAnchorLoad_RemainsBounded`
requires simultaneous nonzero hull soil reaction and chain tension for a full-load
507B at 0.01/0.02/0.04 s. All three cases pass for 180 s: peak ground reaction
0.978-0.989 MN, chain tension 1.389 kN (including suspended chain weight),
maximum speed below 0.000002 m/s, no rise. Wind is 12 m/s; friction holds this
particular shallow contact. This checks simultaneous reactions, not an ability to
pull the ship off using its anchor. Raw rows: `part16-anchor-contact.csv`.
Final complete PlayMode suite: 161/161, 233.5 s,
`TestResults/check-PlayMode-20261025-111903-137.xml`.
