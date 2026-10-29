# Usability Part 5: buoy and obstacle contacts

Implemented 2026-10-29. Physics values below are estimates for gameplay, not
validated buoy specifications or maritime training data.

## Demonstrated causes

`BuoyVisualRig.Build` changed every placeholder collider into a trigger. A ship
could therefore cross a red buoy without a contact impulse. Making those shapes
solid alone would reintroduce the older static-buoy blockade. The visible buoy also
moved independently of the contact root through a Transform animation.

The contact matrix exposed a second issue: large hulls using sweep CCD could enter
a small stationary mark before contact resolution, especially with a coarse step.
The Meteor bow at 13.545 m/s and 0.04 s reached 0.475 m penetration. Setting only the
buoy to speculative detection did not fix the moving hull's contact. The common
vessel policy now predicts contacts on the moving hull using Continuous Speculative;
small buoy bodies retain Continuous Dynamic. The same policy covers the passive
barge. It also avoids relying on sweep CCD for the 507B's convex mesh hull.
Unity documents the [CCD modes and their supported shapes](https://docs.unity.com/en-us/engine/6000.0/manual/physics-section/physics-overview/collision-section/collision-detection/continuous).
The physical hull geometry, hydrostatics and force-driven vessel motion are unchanged.

## Implementation and estimates

`MooredBuoy` owns the Rigidbody, contacts and forces. `BuoyVisualRig` only builds
presentation and no longer changes colliders or animates a separate floating pose.
The detailed body, lantern and map marker follow the physical root. The two simple
convex contacts approximate the visible steel float and tower; obsolete placeholder
contacts are disabled. Existing light timing and unlit marks are preserved.

| Parameter | Estimated value / implementation |
|---|---|
| Mass | 650 kg |
| Float | Sphere radius 0.85 m, centre y = 0.12 m, approximating the existing 1.74 m wide float |
| Tower | Capsule radius 0.23 m, height 2.44 m, centre y = 2.02 m |
| Centre of mass | y = -0.2 m; inertia from the compound contact shapes |
| Heave | Linearized buoyancy, 18,000 N/m and 5,000 Ns/m damping; gravity-balanced at the scene water level |
| Horizontal water drag | 450 Ns/m plus 200 N/(m/s)^2, relative to the existing local current field |
| Mooring | 0.5 m slack, 800 N/m restoring stiffness, 700 Ns/m opening damping, maximum 4,000 N |
| Righting | 8 rad/s^2 effective righting coefficient, 4/s angular damping |
| Contact material | Friction 0.12, zero restitution, minimum combine |

Mass and force coefficients live in `Resources/BuoyPhysics.json`; the shared wet
contact material is `Resources/BuoyContact.physicMaterial`. Heave and drag use
step-aware damping. Mooring only pulls, with a finite holding load, so it cannot
become a rigid constraint on a ship. This uses the same tension-only principle as
`GroundTackle`, without assigning ship machinery to marks. The ship's full catenary
solve was not reused: this estimated small-mark model has no chain geometry or
chain-length control to solve. It does not simulate chain breakage, anchor relocation,
flooding, sinking or wave loading. Water level and current use the existing scenario
providers, scoped to the buoy's scene. Legacy RiverCurrentZone volumes override
the field using the same averaged-zone convention as ships.

`BuoyContactBuilder.Install` migrates only navigation marks in the three saved scenes
and existing navigation prefab. All four canonical creation paths also install the
component, including unlit Serpukhov marks. The older optional left-buoy prefab is
absent in this checkout; its generator includes the same installation when created.
A runtime fallback handles marks from older scenes. No new art resources are needed.

## Contact ownership audit

The EditMode checks write complete collider inventories under `Logs/BuoyPart5/`.
They include disabled placeholders as well as active contacts.

| Scene | Physical buoys | Collider inventory | Other contact ownership |
|---|---:|---:|---|
| River familiarisation | 20 | 103 | Natural-bank MeshColliders are solid; old straight-bank placeholders are disabled |
| Gorodets | 42 | 270 | Existing bathymetry surfaces retain ship-pair exclusions, keel/bank reaction stays in GroundingController; demonstration quay remains solid PhysX |
| Serpukhov | 22 | 278 | Mooring craft, piers and piles retain solid box contacts; shoaling is represented by scenario bathymetry |

Active mark and vessel contacts use interacting layer 0. No global layer exclusion
or new collision-ignore pair was introduced. The verified 507B hull, its grounding
contact samples, terrain ownership and push-unit pair exclusions are unchanged.

## Save, reset and compatibility

The existing version-1 voyage JSON gains an optional `buoys` array. Each entry has a
stable `Navigation/<marker name>` id, body position/orientation and linear/angular
velocity. Capture and restoration are scoped to the voyage scene and sorted by id.
Loading validates finite values, rotations, counts and unique ids before applying
state. The bounded file-size limit is now 1 MiB to accommodate scene state.

New passages start at the authored mark stations. Loading a new save restores mark
motion. Old saves without buoy state reset marks to their stations at the saved water
level; unknown ids from changed scene content are ignored. The existing R ship reset
leaves the current passage's marks where they are. It is not Part 7 safe-position
recovery. A displaced buoy retains its original station and returns under its tether.

## Verification

- `BuoyContactTests`: 864 contact cases across seven catalogue hull commands plus
  the separate MN-52 barge hull, four approaches (bow, stern, side, glancing),
  0.5 m/s, 75% of configured top speed and configured top speed, 0.01/0.02/0.04 s,
  and simulation scale 1/2/4. These are isolated physics-scene tests with explicit
  stepping; time scale does not enlarge the physics step. Configured top speeds
  span 3 to 18.06 m/s. They verify contact events, bounded overlap and speeds,
  momentum response, displacement and separation when reversing. They do not
  substitute for hydrodynamic manoeuvring trials or a jointed-formation collision trial.
- Maximum overlap in that matrix: 0.15001 m, with at least one physical contact in
  every case. Numerical overlap is bounded, not literally zero. Reports:
  `Logs/BuoyPart5/contacts-<step>-<scale>.csv`.
- Additional tests cover sustained propulsion against a mark, reverse release,
  return/righting after displacement, water-level/current equilibrium, saved motion,
  legacy reset and invalid saves. The existing menu save/load test now also checks
  a displaced buoy through the actual file path.
- Three scene inventories verify unique ids, solid dynamic mark contacts, masses,
  collision modes and active layer pairs. Visual tests verify idempotent construction
  and disabled placeholders instead of asserting that all contacts are triggers.
- Full EditMode: **344/344**, `TestResults/check-EditMode-20261029-174933-681.xml`.
- Focused final integration: **23/23**, `TestResults/buoy-focused-final.xml`,
  including 63 solid-wall approaches (seven catalogue hulls, three directions and
  three steps) at each vessel's top speed, stopped motion and reverse separation.
- Full PlayMode, fleet shakedown and visual beacon check: pending final runs.

The 25 m/s exploratory cargo-hull stress run was outside configured vessel speeds
and motivated the collision-policy investigation; it is not reported as an accepted
matrix result. Acceptance above is limited to the declared vessel speeds and steps.

No standalone player rebuild, commit or push is included. Part 6 remains separate.
