# Recommended Next Steps

## Immediate Engineering Work

1. Replace the estimated depth profile with real bathymetry and sampled
   under-keel clearance.
2. Add collision look-ahead, grounding detection, and regression tests.
3. Replace direct keyboard polling with an Input Actions asset and rebinding.
4. Add loader parse-error and missing-component EditMode tests.

## Simulation Development

1. Obtain propeller, rudder, manoeuvring, loading, and trial documentation, then
   calibrate the estimated coefficients against it with `VirtualSeaTrials` and
   agree tolerances with maritime specialists.
2. Replace the wall-sided station prisms with hull lines when a lines plan is available.
3. Replace the estimated bank suction model with a published formulation
   (for example Lataire et al.) and add four-quadrant propeller data.
4. Deepen the fast craft support model: foil lift per foil with banked turns,
   cushion pressure and pitch stability, lift-fan dynamics, and contact on the
   foils and skegs rather than only the hull keel.
5. Replace the generated vessel meshes with modelled or imported hulls, starting
   with the Meteor's streamlined superstructure, and add textures and names.
4. Implement ship-ship interaction, locks, docking mechanics, mooring lines,
   anchors, thrusters, damage, and scenario scoring.
5. Add validated navigation signs, lights, rules, and instructor tools.

All hydrodynamic and exercise behavior must be validated by qualified maritime
specialists before training use.
