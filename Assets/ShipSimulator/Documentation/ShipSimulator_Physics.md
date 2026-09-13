# Ship Simulator Physics Prototype

The vessel model follows the literature-based roadmap in `ShipDynamicsRealismApproach.md`
(phases 1 to 6). It is a formula-based prototype: the equations are published ones, but most
Project 507B coefficients are estimates (see `VolgoDon507B_Sources.md`), and nothing here is
calibrated against 507B trials or validated for maritime training.

## Structure

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
| `WindLoadModel` | Blendermann (1994) wind load coefficients |
| `RestrictedWaterModel` | Depth factors, ICORELS squat with blockage, estimated bank suction |
| `HydrostaticsModel` | Station prism buoyancy, heave and roll damping, squat as a lowered water surface |
| `ManoeuvringSimulator`, `ManoeuvringTrials` | Pure simulation and IMO-style standard manoeuvres |

`ShipPhysicsController` is the hub. Each `FixedUpdate` it:

1. reads surge u, sway v and yaw rate r in the horizontal body frame from the `Rigidbody`
   (it stays the source of truth, including after collisions);
2. samples the effective current, relative wind, depth under the hull and the water flow area on
   each side;
3. steps grounding contact, then `ManoeuvringModel`, which returns body accelerations;
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

## Grounding

`GroundingController` checks ten keel points against bathymetry (or the controller's depth
provider). Penetration gives a spring-damper normal force by bottom type (silt, sand, rock), and
Coulomb friction limited by that force resists horizontal motion. Friction goes through the
manoeuvring solve, so it acts against the added mass too.

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

## Limitations

- Coefficients are estimates and not trial-calibrated; see the source table for each one.
- Three-degree-of-freedom manoeuvring; heave, roll and pitch come from hydrostatics and damping,
  not from a seakeeping model. No wave loads.
- Quadratic propeller curves instead of four-quadrant data; no cavitation or ventilation.
- Bank suction model is estimated, not Lataire's formulation.
- Not implemented (phase 7): ship-ship interaction, locks, mooring lines and anchors.
