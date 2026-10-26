# Serpukhov, original Project R-96

Built at Arkul on 12 October 1973, yard number 12, registry 182169. The playable
asset reconstructs operational 1991-1996 appearance using the actual July 1989
photograph. See `Art/Source/Serpukhov_R96/README.md` for the complete reference
register, uncertainty statements, modelling scripts and visual review images.

## Engineering controls

Primary source: https://www.omtnp.ru/images/flot/p96/p96.pdf, six pages inspected.
Identity and photograph: https://fleetphoto.ru/vessel/3048/ and
https://fleetphoto.ru/photo/408315/.

Overall length 21.8 m, design length 19.9 m, overall/design beams 5.3/5.0 m,
loaded draft 0.62 m, hull side height 1.4 m. The original table gives 45.23 t
loaded and 38.57 t lightship. The builder's current page gives 45.43 t; the
original value is retained, not averaged. The drawing's 6.4 m overall height
excludes removable equipment. The reconstructed mast/aerial tips reach 7.6 m
above baseline, estimated from the photographs.

The original arrangement has its rounded stern at drawing left and pushing
bow at right. The photographs determine Serpukhov-specific external details.
The source is a general arrangement, not a complete lines plan. Loft stations,
fine bilge shape and the obscured waterjet internals remain reconstruction.
The supplementary Katerok drawing is explicitly captioned R-96B and was not
used as the original R-96's geometry.

## Physics interpretation

`Data/Vessels/Serpukhov_R96.json` uses the existing `ShipPhysicsController`,
MMG manoeuvring model, engine actuator and hydrostatic station prisms. No
separate simulation framework or new global controls are installed.

The single 0.846 m, four-blade impeller is represented by an equivalent thrust
and torque polynomial. Existing `SteeringNozzleModel` represents the lateral
reaction of the balanced rudder in the jet. The actual vessel's duct remains
fixed; this is not an azimuth drive. The optional data flag uses an existing
abstraction, not a claim that the real nozzle rotated. Rudder travel is 45
degrees each side, with 70 degrees in 43 seconds from the original sheet.

The signed shaft state is an effective ahead/astern actuator, not a detailed
simulation of pump flow and the twin reversing flaps. It does not reproduce
their transient hydraulics. Gear losses, inertia, astern efficiency, thrust
polynomials, resistance and all manoeuvring derivatives are estimates.
Separate mesh pivots are supplied for future flap/impeller animation; no
new machinery animation or towing/coupling solver is claimed.

Initial pure-model checks: 4.012 m/s (14.44 km/h), tactical diameter 61.01 m,
crash-stop track 47.22 m and time 21.60 s. Compare the sheet's maximum speed
14.9 km/h, circulation indication 1-1.5 hull lengths and full-ahead/full-astern
run-out 20 m in 20 s. Different manoeuvre definitions and estimated coefficients
prevent treating these as matched trials. Further calibration requires better
waterjet and manoeuvring evidence. Prototype physics is not training validated.

## Rendering and integration

The editable Blender source retains separate equipment and collections. Runtime
FBX contains three LODs, with static material batches and independent important
moving parts. Original paint, metal, rubber and wood maps are packed in the
source and supplied separately for URP. FleetPhoto pixels and drawings are not
distributed in the game. White/grey/oxide colors are reconstructed, not verified
1991 paint specifications. The bridge is detailed; lower internal rooms are not.

The root is at the loaded waterline, +Z forward and +Y up. Import checks verify
bow and starboard landmarks. Eight independent box colliders cover the hull,
house and pushing knees, using existing default collision layers and continuous
dynamic Rigidbody collision. These are conservative contact envelopes, not exact
plating surfaces. A single masthead option preserves the other vessels' defaults.

Select `Серпухов, Р-96 (1973)` on the existing vessel selection page, then select
River familiarisation, Gorodets or Serpukhov zaton. The normal passage limits
admit the tug without a special exception. Existing ships remain available.
The navigator camera uses the glazed bridge; existing third-person views use
model-specific positions in `VesselLayout`.
