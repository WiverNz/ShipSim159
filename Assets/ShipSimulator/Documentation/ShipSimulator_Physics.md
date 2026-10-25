# Ship Simulator Physics Prototype

The vessel model follows the literature-based roadmap in `ShipDynamicsRealismApproach.md`
(phases 1 to 6). It is a formula-based prototype: the equations are published ones, but most
Project 507B coefficients are estimates (see `VolgoDon507B_Sources.md`), and nothing here is
validated against controlled 507B trials or for maritime training. Service-speed
calibration alone does not validate manoeuvring behavior.

## Vessels

The model is the same for every vessel; only the JSON specification changes. Five playable vessels
are defined: the cargo ships Volgo-Don 507B, Volgo-Balt 2-95A/R and Volgoneft 1577, and the fast
passenger craft Meteor 342U (hydrofoil) and Luch 14352 (skeg air cushion), plus the KVLCC2 benchmark
used to check the model. For each vessel the published particulars (dimensions, displacement, deadweight,
engine power, service speed) are taken from its sources file. Linear hull derivatives and added masses
come from Clarke et al. and Söding for its own length, beam, draft and block coefficient. The residual
resistance coefficient and the rated propeller speed are then calibrated together so that, at the
published loaded speed, the propellers absorb full delivered power and their thrust balances
resistance. Everything else is estimated.

## Structure

### Optional compartment loading

`loading.enabled` selects per-compartment inventories for 507B. Mass sums
lightship, cargo, ballast and consumables once; deadweight is a shared capacity,
not an extra mass. CG is a weighted centre, and diagonal inertia uses equivalent
uniform boxes plus parallel-axis terms. The reference waterplane/keel remain
unchanged; station buoyancy resolves sinking and trim after mass/CG updates.
Dimensional manoeuvring parameters, Rigidbody properties, bow-thruster immersion
and a mean-draft propeller ventilation approximation update with the inventory.
Shaft governor/reversal objects survive these updates. Other vessel configurations
use their existing fractional loading model. Detailed assumptions and numerical
limits: `Art/Source/VolgoDon507B/PART7_REVIEW.md`.

### Optional steerable nozzles (Volgo-Don 507B)

`steeringNozzle.enabled` selects fixed-shaft, deflected-jet steering instead of
the generic MMG rudder. For effective signed shaft thrust T and actual helm angle
delta, force is `(T cos(k delta), -T sin(k delta))` in forward/starboard axes.
Its moment is `x Fy - y Fx`; `PropellerY` is included in total sway force.
Astern thrust reverses the jet reaction. Each shaft still has its own governor,
torque balance, reversal interlock and throttle input. The nozzle is not an
azimuthing shaft or a new propulsion controller.

Passive nozzle/stabilizer force uses local velocity `(u-r*y, v+r*x)` and normal
`n=(sin(delta), cos(delta))`: `F=-0.5*rho*A*C*|V|*(V dot n)*n`.
Consequently `F dot V <= 0`, including reverse flow; it produces no force at
rest and permits steering while coasting. The generic rudder/slipstream force
is not added on this path. The shared helm actuator retains angle/rate limits.
Absent/disabled nozzle data keeps the original MMG force calculation.

These are tunable lumped approximations, not measured duct coefficients or a
four-quadrant propeller model. The 507B torque curves and assumed shaft rating
were reconciled with archived geometry and service speed in Part 6; the general
calibration description above does not imply full rated-power absorption for
this vessel. See `VolgoDon507B_Sources.md` and the Part 6 report. Existing bow
thruster disc theory approximates the archived waterjet; it does not resolve
pump head, inlet losses or its actual outlet geometry.

The vessel is a Unity `Rigidbody`; no script moves or rotates it directly. The hydrodynamics
live in plain C# classes under `Scripts/Physics/`, so EditMode tests and the virtual sea trials
run them without a scene:

| Class | Responsibility |
|---|---|
| `VesselParameters` | Derived quantities for a loading condition: mass, draft, inertia, added masses, windage |
| `HullDerivativeEstimate` | Clarke et al. (1983) linear derivatives and added masses, Söding surge added mass |
| `ManoeuvringModel` | Three-degree-of-freedom MMG solve (surge, sway, yaw) with the full added-mass matrix |
| `HullForceModel` | MMG hull polynomial, low-speed cross-flow drag blend, current shear |
| `ResistanceModel` | ITTC-1957 friction line, form factor, correlation and residual resistance, Lackenby shallow-water loss |
| `EngineShaft` | Diesel, governor, torque and power limit, shaft inertia, astern reversal |
| `PropellerModel` | Open-water K_T and K_Q, ahead and astern, wake fraction and thrust deduction |
| `RudderModel` | MMG rudder in the propeller slipstream, Fujii lift slope, stall |
| `BowThrusterModel` | Tunnel bow thruster: actuator disc thrust, speed and submergence losses |
| `SupportModel` | Air cushion or hydrofoil lift: weight fraction, drag hump, supported draft |
| `WindLoadModel` | Blendermann (1994) wind load coefficients |
| `RestrictedWaterModel` | Depth factors, ICORELS squat with blockage, estimated bank suction |
| `HydrostaticsModel` | Station prism or tabulated section-column buoyancy, heave and roll damping, squat as a lowered water surface |
| `ManoeuvringSimulator`, `ManoeuvringTrials` | Pure simulation and IMO-style standard manoeuvres |

`ShipPhysicsController` is the hub. Each `FixedUpdate` it:

1. reads surge u, sway v and yaw rate r in the horizontal body frame from the `Rigidbody`
   (it stays the source of truth, including after collisions);
2. samples the effective current, relative wind, depth under the hull and the water flow area on
   each side;
3. steps grounding contact, then `ManoeuvringModel`, which returns body accelerations, and adds
   the bottom friction impulse to them;
4. applies them with `AddForce(..., ForceMode.Acceleration)` at the centre of gravity and
   `AddTorque(up * r_dot, ForceMode.Acceleration)` for yaw;
5. applies station buoyancy, heave and roll damping and the heel moments as ordinary forces.

`ManualStepping` lets tests call `Simulate(dt)` together with `Physics.Simulate`.

## Equations of motion

The MMG standard model (Yasukawa and Yoshimura 2015) is written about midship in water-relative
velocities, which is exact for a uniform, irrotational current:

```
(m + m_x) u_dot - (m + m_y) v r - x_G m r^2 = X
(m + m_y) v_dot + x_G m r_dot + (m + m_x) u r = Y
(I_zG + x_G^2 m + J_z) r_dot + x_G m (v_dot + u r) = N - x_G Y
```

The 3 by 3 system is solved each step, so sway-yaw coupling and large shallow-water added mass
do not need the previous step's forces. The centre of gravity acceleration applied to the body is
`(u_dot - v r - x_G r^2, v_dot + u r + x_G r_dot)`.

Sign convention: Unity z forward is MMG x, Unity x starboard is MMG y, positive r turns the bow to
starboard, positive rudder angle gives positive r.

## Hull

- Linear derivatives Y'_v, Y'_r, N'_v, N'_r and sway and yaw added mass are the Clarke estimates for
  L, B, T and C_B; `VesselParameters` rescales them with draft when the loading changes.
- Nonlinear terms are borrowed from the KVLCC2 MMG set (cross-flow dominated, estimated for 507B).
- Below 0.3 to 1.2 m/s the polynomial blends into a strip model of cross-flow drag with
  C_D = -(Y'_v + Y'_vvv), integrated over stations, so rotation on the spot and berthing drift stay
  finite. Lift and Munk terms scale with u and reverse when going astern.
- Non-uniform current is applied as a per-station sway current difference (shear), sampled from
  `CurrentFieldProvider` when one exists.
- Straight-ahead resistance: `R = 0.5 rho S U^2 ((1 + k) C_F + C_A + C_R)`, ITTC-1957 C_F, Mumford
  wetted surface when none is given. C_R is calibrated so full ahead gives the published 10 kn.

## Propulsion and steering

- Two independent shafts. Each `EngineShaft` solves `2 pi I dn/dt = Q_engine - Q_prop - Q_friction`
  with an implicit governor, a torque limit up to rated speed and a power limit above it. An order
  against the running direction cuts fuel at once, brakes the shaft to the reversal speed, waits the
  restart delay, then drives the other way.
- Propeller thrust and torque come from quadratic open-water K_T(J) and K_Q(J) fits, separate for
  ahead and astern, with advance ratio clamped to [-1.5, 1.3]. This is not four-quadrant data.
  Wake fraction includes the drift and yaw correction `w = w_0 exp(-4 beta_P^2)`.
- Each propeller sits at its own lateral position, so a split telegraph turns the ship.
- Each rudder follows the MMG rudder model: inflow from wake and propeller slipstream (written
  without 1/J so it stays finite at bollard pull), flow straightening, Fujii normal force slope,
  a smooth blend to a post-stall coefficient beyond the stall angle, and the hull interaction terms
  t_R, a_H and x'_H.

## Bow thruster

`BowThrusterModel` is a tunnel thruster at the bow, present when the vessel JSON has a
`bowThruster` section with `fitted: true`. It adds a side force Y at its longitudinal position and
the yaw moment `N = x_T Y` to the manoeuvring solve.

- Bollard thrust from actuator disc momentum theory, `T = FM (2 rho A P^2)^(1/3)`, with an estimated
  figure of merit for tunnel and motor losses. For 507B (160 kW, 1.0 m tunnel, both estimated) this
  gives about 21 kN, or 13.5 kgf per kW, inside the usual 10 to 15 kgf/kW for tunnel thrusters.
- The command ramps to full in a few seconds. Thrust scales linearly with the ramped command.
- Effectiveness falls as the ship gathers way, `f = f_min + (1 - f_min) / (1 + (u / u_ref)^2)`,
  because the passing flow bends the jet back onto the hull. The curve shape and its constants are
  estimates of the trend reported for tunnel thrusters, not measured data.
- Thrust fades to zero as the tunnel axis rises toward the waterline, so a lightship 507B, whose
  draft is below the estimated axis height, gets no thrust.

## Fast craft: cushion and hydrofoil support

Vessels with a `support` section stop floating on their hull as they gather way. One speed-dependent
fraction, zero below the takeoff speed and full at the support speed, says how much of the weight the
air cushion or the foils carry. That fraction:

- supplies nominal lift to four support patches; local immersion provides restoring heave, pitch
  and roll forces and point velocity supplies damping. A dry patch supplies no lift. Hull
  hydrostatics carry the remaining weight;
- weakens everything the immersed hull does, scaling hull forces, squat and bank suction;
- reshapes resistance: a hump through the takeoff range, then a fraction of the displacement drag
  once supported;
- reports the foils or skegs as the deepest point, because they reach below the lifted hull.

Rudders and propellers stay immersed and keep working, which is how both craft steer. Lift-fan power
is taken off the delivered power before the propeller. The curve shape, the takeoff speeds and the
supported drag factors are estimates of published trends, not measured data for these craft; foil
the four-patch lift distribution is an estimate, not foil hydrodynamics or a cushion pressure solver.
Patch span is half the perpendicular length and the overall beam, centred at the support position.
The support centre is aligned with the estimated loaded centre of gravity for neutral trim.
Damping uses the existing heave damping ratio. Force per patch is capped at twice nominal lift.
Appendage extension is inferred from full-support draft and the remaining loaded hull immersion;
clearance and grounding sample the same fixed points, including at rest. This currently gives the
Meteor about 2.27 m draft afloat rather than the published 2.35 m, pending measured geometry.

Shallow water: the Lackenby speed loss is a subcritical regression, so it is faded out above the
critical depth Froude number rather than extrapolated. Without that, a hydrofoil at 65 km/h in 4.6 m
of water was wrongly held below takeoff speed.

## Wind

Blendermann coefficients with the mirrored angle convention of the MSS toolbox give X, Y and N from
frontal and lateral windage. Windage grows with freeboard as the ship lightens. `WeatherController`
pushes its gusting wind (`WindGustModel`) to every vessel each frame, so the ship feels the same
gusts that move the water and trees.

## Restricted water

- Depth factors for added mass and linear derivatives follow Kijima et al. and Taimuri et al.;
  the B/T <= 4 branch is used for 507B deliberately, because the other branch gives unphysical
  factors for a very beamy, shallow-draft hull. Factors are 1 in deep water and grow as h/T falls.
- Shallow-water resistance uses the Lackenby speed loss, capped at 45 %.
- Squat is ICORELS bow squat times a Barrass blockage factor, with the stern value from Barrass'
  bow-to-stern ratio for the block coefficient. It lowers the effective water surface in
  `HydrostaticsModel`, so the hull really sinks and trims and the keel depth feeds grounding.
- Bank effect is an estimated Bernoulli suction model from the difference in flow area between the
  two sides (sampled over a configurable width). It pulls the ship toward the nearer bank and turns
  the bow away. The published Lataire models were not accessible, so its coefficient is estimated.

## Hydrostatics, loading and heel

The hull is a grid of vertical prisms (stations times strips) with a parabolic end taper matching
C_WP, linearly weighted to put the centre of buoyancy at its configured position, and sized to
float the loaded mass at the loaded draft. Draft, trim, heel and GM follow from the geometry.
Heave and roll damping are critical-damping fractions. The turning heel moment uses the lever
`KG - T/2` and the wind heel moment the lever `s_H + T/2`.

`massProperties.loadFraction` interpolates mass between lightship and loaded; draft, windage,
added masses and derivatives follow the resulting condition.

### Section hydrostatics (Volgo-Don 507B)

An enabled `hydrostatics.sectionTable` replaces the prisms. It holds half-breadth offsets per
station (heights above keel, breadth linear between offsets and zero below the first) for a
closed hydrostatic surface. `SectionHydrostatics` builds once per vessel data a table of
immersed area and keel moment for every station and strip, on a uniform height grid, and
`VesselParameters` solves draft and trim by Newton iteration so that the displaced volume
equals mass / rho and buoyancy and gravity lie on one vertical. `HydrostaticsModel` applies
the same columns at runtime, immersed to the local water surface, with heave damping
proportional to each column's waterline breadth. Heel is resolved dynamically; reported GM is
`KB + I_T / V - KG` without a free-surface correction. Immersion above the cap saturates; a
dry or non-finite column adds nothing. A solution outside the table's declared draft and trim
range is flagged (`HydrostaticRange`), and an overload beyond the capped volume is reported as
unsolved without NaN. Propeller immersion and bow thruster submergence use the local draft at
their stations. Vessels without a table keep the prism model unchanged. Derivation, accuracy
and limits: `Art/Source/VolgoDon507B/PART11_REVIEW.md`.

## Grounding

`GroundingController` checks contact points against bathymetry (or the controller's depth
provider). A vessel with a section table (the 507B) uses a grid over its real bottom from the
table plus the propeller tip circles; its bed reaction is an estimated modulus times penetration
over each point's area, capped by a bearing pressure, along the local hull surface normal.
Other vessels keep ten keel-level points with a per-point spring-damper. Soil values by bottom
type (silt, sand, rock) are estimates.

Coulomb friction, limited by mu times each point's reaction, is solved after the manoeuvring
step as an impulse: each point receives at most the force that would stop it, iterated over the
points with the solve's added-mass response (`ManoeuvringModel.ExternalAcceleration`). Below the
limit the hull stays put without creeping; friction never reverses a point or adds energy.
Whether full astern can work a ship off is therefore the holding force against the effective
thrust, (1 - t) T, shown on the HUD. Details and measured cases:
`Art/Source/VolgoDon507B/PART12_REVIEW.md`.

For a vessel with a loading model the propeller immersion follows the actual hub depth each step
(trim, heel, heave, stern squat); the pure simulator keeps the still-water value.

## Hull collision ownership (Part 16)

The 507B uses thirteen convex slices of the corrected hull, installed by
`VolgoDonContactBuilder` through the normal model integrator. Decorative fittings
and propellers are excluded from PhysX hull contact; grounding retains its
section-grid and propeller-tip samples. Quay/independent-obstacle reactions belong
to PhysX; soil normal pressure and friction belong to GroundingController.
Gorodets bank/rock geometry represented by the bathymetry is explicitly marked
`BathymetryContactSurface`; those collider pairs are ignored while bathymetric
grounding is enabled. Disabling either owner restores ordinary collision pairs.
Anchors and moorings apply line forces only, with no duplicate contact joints.

Grounding evaluates contact points and normals from the Rigidbody pose, including
immediately after loading a save. The force law, mass, CG, inertia and saved data
schema are unchanged. Convex approximation and measured contact/grounding limits:
`Art/Source/VolgoDon507B/PART16_REVIEW.md`. No walkable decks or interior collision
system is provided by this contact hull.

## Anchors and mooring lines

`GroundTackle` (507B only; vessel JSON `groundTackle`, hawses and `MooringPoint` leads in the
prefab) is stepped inside `Simulate` after grounding, so pause and simulation speed reach it through
the physics step only. Each chain and line is a quasi-static elastic catenary (`Catenary`): the
horizontal tension is solved from unstretched length, span and height, through slack (hanging down,
the rest on the bed), touchdown with a grounded part, fully suspended, and elastic stretch. A line
never pushes. Damping of 0.1 of critical on the current horizontal stiffness acts on the rate the
span opens at the attachment point's velocity and vanishes as the line slackens.

The horizontal force enters the manoeuvring solve through `AddExternalForce` at the hawse or lead;
the vertical force (line pull, less the anchor and chain weight already in the lightship mass) and
the heel and trim moment of the lead's height above half draft act on the Rigidbody. The step reads
points from the Rigidbody pose, which is current right after a restore when the transform is not.

An anchor lands where the bathymetry says the bottom is; with no depth it keeps hanging. It holds
up to ratio x its submerged weight (estimated per bottom type), set over the first metres of drag
and lost as the chain lifts the shank; chain lying on the bed adds friction. Above that it slides
towards the ship just far enough to carry its limit, so the vessel is never pinned and the work goes
into the soil. The windlass brake renders above 45 % of the chain breaking load; the bitter end
parts above the breaking load. Heaving runs at the windlass rate, slowing to a stall at its overload
pull, and breaks the anchor out when the chain is short. Mooring lines haul up to the winch's stall
pull and part at their breaking load. All holding, rope and soil values are estimates; this is not a
mooring or anchoring safety calculation. Details: `Art/Source/VolgoDon507B/PART14_REVIEW.md`.

## Virtual sea trials

`Ship Simulator > Run Virtual Sea Trials` (or `VirtualSeaTrials.Run` in batch) runs speed steps,
35 degree turning circles, 10/10 and 20/20 zig-zags and a crash stop for the 507B in deep water,
the 8.2 m river channel, the 4.6 m Gorodets reach and lightship, plus the KVLCC2 benchmark. It
writes `Logs/SeaTrials/sea-trials.md` and compares deep-water loaded results with the IMO
MSC.137(76) envelope, which is a sanity bound, not an acceptance test for a river vessel.

## Wake

`ShipWakeController` publishes the vessel's track, drifted with the current, to
`RiverWater.shader`, which draws a Kelvin wake, a bow wave, midship drawdown and propeller wash.
Its amplitudes are estimated visual values; the waves apply no forces.

## Sound coupling

`VesselSoundscape` (every vessel, one implementation, a profile per vessel) reads the simulation once
a frame, after the physics and the machinery have moved, and writes nothing back; its smoothing cannot
change a force or a command (a PlayMode test compares trajectories with and without it bit for bit).
Engine layers follow each `EngineShaft`. The firing rate is engine rpm (shaft rpm times the data's
`engineRpmPerShaftRpm`) x cylinders x 2 / strokes / 60; its layer plays only when the data
establishes those facts and the clip was made for that engine. Combustion level follows
`FuelFraction` (engine torque in the running direction), starting-air braking follows
`IsAirBraking`, and a change of `Direction` is the restart on air, both only for an engine the data
names direct-reversing with air start. The physics reverses every vessel's engine the same way, an
estimated surrogate; for other vessels the real reversing gear is not established, so no reversal
sound is played. Propeller wash follows each propeller's thrust and the computed disc immersion;
the blade rate follows shaft rpm and the data's blade count. Water layers follow
`RelativeWaterVelocity`, scaled by the hull still in the water
(`SupportModel.ImmersionAtFraction` of the computed support fraction); wind layers the wind
relative to the ship. Hatch sound follows the
change of the mechanism state between frames and its phase; chain and line sound follow the rates
`GroundTackle` stepped (`ChainRateMps`, `LineRateMps`), its commands, stall flag, tension and
`ChangedAt` events. A restore or reset (`RestoreCount`) restarts the continuous layers from the
restored state and plays no events for 0.3 s. Pitch is never scaled by simulation speed. Details:
`Art/Source/VolgoDon507B/PART15_REVIEW.md`.

## Limitations

- Coefficients are estimates and not trial-calibrated; see the source table for each one.
- Three-degree-of-freedom manoeuvring; heave, roll and pitch come from hydrostatics and damping,
  not from a seakeeping model. No wave loads.
- Quadratic propeller curves instead of four-quadrant data; no cavitation or ventilation.
- Bank suction model is estimated, not Lataire's formulation.
- Not implemented (phase 7): ship-ship interaction and locks. Anchors and mooring lines exist for
  the 507B only, as a quasi-static prototype without line hydrodynamics or fender springs.
