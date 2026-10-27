# Usability Part 3: predictable camera controls

Implementation date: 2026-10-27. Scope: Part 3 of the
[usability plan](UsabilityImprovementPlan.md). Vessel dynamics, navigation and
cargo are unchanged; the map improvements remain Part 4.

## Findings

The installed Input System 1.20.0 defaults to `UniformAcrossAllPlatforms`.
The actual Windows player reported `1` per conventional wheel event and `0.125`
for each injected high-resolution fragment. The previous distance gain `0.002`
therefore changed distance by only about 0.2% per detent. Settings alone could
not compensate for the unit mismatch.

Rotation applied 0.12 degrees per pixel directly to its requested pose, while
position and orientation followed with separate, scaled-time smoothers. The
camera orbited the vessel origin but looked at an offset point; hull pitch/roll
also moved that offset. This made mouse motion, tracking and look direction
interact. The lower pitch clamp raised some authored presets, and the old upper
clamp displaced the overhead tug and push-unit presets.

## Implementation

`CameraOrbit` owns yaw, pitch and logarithmic distance targets and rendered
values. `ShipFollowCamera` owns device sampling, UI ownership and presentation.
Wheel normalization happens once: uniform input is already in ticks; native
Windows input, when explicitly selected, is divided by 120. Fractional ticks
are retained. Default logarithmic gain is ln(1.25): one positive tick moves
20% closer, one negative tick 25% farther. Opposite steps cancel away from limits.
Rotation is 0.06 degrees per pixel, half the old gain. Pixel input is never
multiplied by frame duration.

Yaw takes the shortest angular path. Yaw, pitch and log distance use exponential
smoothing with a 0.085 s time constant; vessel tracking uses 0.12 s. Both use
unscaled elapsed time, including FOV interpolation. The camera looks at the same
pivot it orbits. Exterior tracking uses vessel heading and position without the
pitch/roll lever effect; the navigator remains exactly at the model's moving eye.
Authored bridge views retain their forward river look point. Pitch is bounded
0-90 degrees, with an explicit orientation at the overhead pole so LookRotation
cannot become singular. All three serialized scenes use the new tuning, as do
new cameras created by the existing builders.

Selecting a numbered view is an immediate cut to its authored pose. Restore,
vessel swap and reset clear pending motion and tracking history and reset temporal
render history. The camera follows subsequent sailing smoothly. Distance limits
scale with the vessel and include authored presets that extend beyond the usual
maximum, avoiding a jump on the first scroll.

The existing independent rotation and zoom settings, translations, bounds and
persistence are reused. No duplicate preferences or menu controls were added.
RMB begun over UI cannot turn into an orbit by dragging outside. An orbit begun
in the world owns its gesture until release, including crossing UI. Wheel input
over UI stays with UI when no orbit is owned. Menu, focus loss, disable, target
change and view change release the cursor. Acquiring or resuming input discards
stale deltas; a held button cannot silently start a new gesture.

Camera saves now contain `orbitVersion = 1` and the currently rendered orbit,
not a still-pending target. Missing version means the original origin-based
coordinates, converted to the new pivot and bounded by the current vessel's
limits. The voyage schema version and atomic save mechanism are unchanged.
Unknown camera coordinate versions fail validation.

## Verification

EditMode **304/304** passed in `TestResults/check-EditMode-20261027-133102-872.xml`.
PlayMode **187/187** passed in `TestResults/check-PlayMode-20261027-133134-307.xml`.
Tests cover proportional/fractional zoom, independent gains,
30/60/144 FPS integration and elapsed-time smoothing, simulation 1x/2x/4x,
pitch/yaw bounds, horizontal height, UI drag ownership, lifecycle release,
menu resume, vessel restore, old/new saves and all seven vessels' nine presets.
The existing navigator eye, vessel dynamics and save/load tests are retained.
The fleet shakedown now captures all nine views, rather than six.

Fleet shakedown **PASS, seven vessels/commands**, in
`Logs/check-Shakedown-20261027-133702-112.log`. Menu/save-load and existing
mechanism/loading smoke **PASS**, in `Logs/check-Menu-20261027-133806-675.log`.
All launchers exited zero. No production camera code changed after these runs.
Earlier failed tests are retained: the first catalogue check found the two top
presets displaced by an 85-degree clamp; input tests initially sampled UI before
layout was ready or started an orbit inside the HUD at the small batch resolution.
Those fixture issues were corrected, and the final full suites passed.

The 63 current view captures are under `Logs/Shakedown/`; seven overview sheets
are under `Logs/Camera/Views/`. All overview sheets were inspected. Authored close
views and forward bridge views are retained; the push unit's navigator still has
the pre-existing barge-cabin obstruction. This part does not move its model eye
to fabricate a clear view. Some presets intentionally show only part of a long
cargo deck; wheel zoom supplies wider framing.

The development Windows build succeeded with zero errors and 13 existing warnings
in `Logs/camera-after-build.log`; details are in `Logs/Camera/build-report.txt`.
The executable is `build/CameraReview/ShipSim159.exe`. It is a review build, not a
release or a claim of complete Part 9 acceptance.

Native input traces: `Logs/Camera/baseline-native/` and
`Logs/Camera/after-native/`; summary `Logs/Camera/native-comparison.json`.
Both used WindowsPlayer, Unity 6000.6.0f1, uniform scroll mode, Volgo-Don 507B,
and both sensitivities at one. Distances below are measured after settling and
before the next event, rather than assuming targets were reached instantly.

| Measurement | Before | After |
|---|---:|---:|
| Distance ratio after one positive tick | 0.998002 | 0.800039 |
| Distance ratio after eight 0.125-tick fragments | 0.998002 | 0.800050 |
| Yaw from 100 horizontal pixels | 12.000 degrees | 6.000 degrees |
| Pitch from 50 vertical pixels | 6.000 degrees | 3.000 degrees |
| Pitch drift during horizontal drag | 0 | 0 |

Both native traces completed successfully. Different initial radii reflect the
new pivot coordinates, not a change of the authored chase eye position. Remaining
height change during the new horizontal drag was about 0.000083 m, consistent
with the tail of the preceding zoom response; horizontal input introduced no pitch.

## Reproducing the native input trace

Build with `ShipSimulator.Editor.LocalWindowsBuild.RunCameraReview` in the
project's pinned Windows editor. This produces a development player at
`build/CameraReview/ShipSim159.exe`, independently of the usual local build path.
Then run `Tools/camera-input-probe.ps1` in Windows PowerShell. It launches its
own window and records `conditions.txt`, `input.csv`, the player log and completion
markers in a fresh `Logs/Camera/native-<timestamp>` directory. It stops injecting
input if that window loses foreground ownership.

The opt-in `--camera-input-probe` player mode freezes only the diagnostic vessel,
hides canvases, temporarily sets both sensitivities to one and restores those
preferences on exit. It never writes the voyage save. Normal player launches
remain interactive. The script injects conventional wheel events, fractional
wheel fragments and horizontal/vertical RMB drags through the Windows input
backend; it does not substitute InputTestFixture for the player backend.

## Limits and remaining player assessment

The Windows traces use synthetic OS mouse events. They verify real backend units
and application response, but do not establish the feel of a physical mouse,
trackpad acceleration or subjective comfort. No physical trackpad is available
in this run. A human should assess all nine views while sailing, zoom at both
sensitivity limits, horizontal/vertical drags, menu/focus transitions and return
to navigator. Visual captures and numerical tests do not replace that assessment.
No new graphic resources or 3D models are required for this part.
