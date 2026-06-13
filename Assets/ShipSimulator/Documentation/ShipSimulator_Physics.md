# Ship Simulator Physics Prototype

## Model

The vessel is a Unity `Rigidbody`; no script directly advances or rotates it
during normal simulation. `FixedUpdate` applies:

- distributed point buoyancy and vertical damping
- two-engine aggregate thrust with ahead/astern response lag
- rudder lift from local water velocity using `0.5 * rho * V^2 * A * Cl`
- separate linear/quadratic surge and sway resistance
- separate linear/quadratic yaw resistance
- river-current-relative water velocity
- optional quadratic wind force

Mass, dimensions, propulsion, rudder, resistance, buoyancy, limits and
calibration multipliers are loaded from `VolgoDon507B.json`.

`RiverCurrentZone` trigger volumes override the ambient current. When zones
overlap, their velocities are averaged. The effective current is used for
resistance, rudder-relative water velocity, and HUD telemetry.

`massProperties.loadFraction` is the configured loading level from 0 (lightship)
to 1 (published loaded displacement). Runtime mass is interpolated between the
documented lightship estimate and loaded displacement. Draft shown in the UI is
only an estimated interpolation until hydrostatic curves are available.
Channel depth and under-keel clearance are generated from a simple lateral
channel profile, not scene bathymetry.

## Controls and feedback

`ShipPhysicsController` exposes command methods used by the HUD. Telegraph and
rudder inputs set targets; response rates remain controlled by vessel data.
Normal motion is always produced through Rigidbody forces and torque.

`ShipWakeController` creates visual hull and propeller trails. These trails do
not apply hydrodynamic forces and are not a wake interaction model.

## Reality status

Published dimensions, loaded displacement, engine count/type/power, twin-screw
arrangement, speed and block coefficient are documented facts. The public
sources found do not provide propeller curves, rudder geometry, inertia,
resistance derivatives, or manoeuvring trials. Those JSON fields are explicitly
estimated and the UI states that the prototype is not training validated.

## Tuning

1. Confirm loaded condition and mass distribution for one specific vessel.
2. Enter measured propeller/rudder geometry and shaft RPM.
3. Match acceleration and stopping trials with thrust and surge resistance.
4. Match turning trials with rudder lift, sway resistance and yaw resistance.
5. Match roll/pitch/heave observations with inertia, point layout and damping.
6. Validate at several draughts, currents and under-keel clearances.

## Simplifications and limitations

- Procedural visual water surface; no physical wave spectrum or hull-panel
  integration.
- Buoyancy points approximate displaced volume and do not flood.
- The 1.5 reserve-buoyancy factor provides restoring force above equilibrium;
  it is a stability calibration value, not a published Project 507B parameter.
- No shallow-water squat, bank effect, propeller-rudder interaction, physical
  wake fraction, thrust deduction, cavitation, wind shadow, trim tanks or cargo
  shift.
- Aggregate thrust is applied at the stern center in this first prototype.
- Coefficients are calibration placeholders, not approved training data.
