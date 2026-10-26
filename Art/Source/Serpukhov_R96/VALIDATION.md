# Validation, 26 October 2026

The editable Blender source and Unity prefab are delivered. The model is an
evidence-based reconstruction with the historical uncertainties in README.md.

| Executed check | Result | Evidence from repository root |
|---|---|---|
| Saved Blender geometry, UVs, packed maps | PASS | `Art/Source/Serpukhov_R96/saved-source-validation.json` |
| Final FBX import, materials, prefab and calibration trials | PASS, exit 0 | `Logs/Serpukhov_R96/integration-clean.log` |
| Final EditMode suite | 273/273 passed | `TestResults/check-EditMode-20261026-075827-050.xml` |
| Full PlayMode suite before final export cleanup and menu test addition | 165/165 passed | `TestResults/check-PlayMode-20261025-234437-344.xml` |
| Final focused menu suite, including sixth-button spacing | 5/5 passed | `TestResults/serpukhov-menu-final.xml` |
| Final runtime fleet shakedown | All six vessels passed, exit 0 | `Logs/check-Shakedown-20261026-075432-341.log` |

The full PlayMode run includes three R-96 obstacle collision cases and the
four-running-light check. Final EditMode checks cover import orientation,
waterline offset, colliders, LODs, materials, vessel data and propulsion response.
The final import, EditMode, focused menu and shakedown logs contain no compiler
errors. Test scenes can emit no-audio-listener warnings; a warning-free editor
session is not claimed. New Unity assets have their metadata files.

Eight Blender inspection views and Unity LOD/bridge views were inspected.
The final top view includes the complete hull. Runtime chase and navigator
captures are retained in `Previews/runtime-0-chase.png` and
`Previews/runtime-8-navigator.png`. They show camera framing, waterline and
materials during actual simulation. The shakedown is an automated river run,
not an independent historical handling assessment or performance benchmark.

The pure-model maximum speed is 4.012 m/s (14.44 km/h), tactical diameter
61.01 m and crash-stop distance 47.22 m in 21.6 seconds. These do not reproduce
the original sheet's short turning/stopping observations. The finite runtime
shakedown reached 3.23 m/s through water, with 0.2 degree heel and 7.5 m under
keel. Those measurements use different scenarios and durations.

Historical paint colors, fine hull offsets, concealed fittings and restored
controls remain inferred. The existing steering-nozzle force abstraction is
used as a waterjet/rudder surrogate; reversing-flap transients are not simulated.
No training validation, subjective handling trial, tug-to-barge coupling,
moving-equipment animation or free-walking interior test is claimed.

Select `Серпухов, Р-96 (1973)` in the existing voyage vessel menu. Existing ships
remain available. Changes are left uncommitted. See `ChangedFiles.txt` for the
file inventory and README.md for references, rebuilding and asset locations.
