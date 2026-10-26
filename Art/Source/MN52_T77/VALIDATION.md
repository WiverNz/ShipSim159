# MN-52 validation, 26 October 2026

## Authored geometry and import

Blender MCP created, inspected and saved the editable source. Eight Blender
views were reviewed against the five exact-vessel photographs. No copyrighted
reference image is packed or shipped. `geometry-validation.json` records a
closed hull, positive volume, no degenerate LOD triangles and UV coverage.
LOD0/1/2 contain 41,264 / 23,393 / 13,552 triangles. The lowest LOD retains the
raised cabin, supports, pipes and end silhouette. All use the same 11 materials.

Unity imports `mn52-import-reviewed.log` and `mn52-camera-import.log` exited 0.
URP material previews and runtime views show no missing/pink shaders. Runtime
screenshots verify the separate barge, tug contact and plausible loaded waterline.
The external camera target was corrected after a port capture clipped the bow.

## Executed tests

- EditMode: 277/277 passed in
  `TestResults/check-EditMode-20261026-132724-794.xml` after the final camera fix.
- Full PlayMode: 173/173 passed in
  `TestResults/check-PlayMode-20261026-131701-439.xml`.
  This includes seven push-unit cases and existing vessels, menu and save tests.
- New checks exercise zero actuators, passive resistance, loaded/empty equilibrium,
  axes, materials, LODs, colliders, passage admission, forward/reverse, both turns,
  coasting, frontal/side collisions, detach/current drift and save/reset.
- Controlled calm-water run: solo Serpukhov 4.012 m/s, coupled 2.593 m/s,
  coupled reverse -2.034 m/s. Logged maximum contact error rounds to 0.0001 m.
- Hydrostatic equilibrium: loaded 1.200 m, empty 0.500 m. These follow fitted
  inferred sections and do not independently validate the real hull offsets.
- Final actual river scene: `Logs/mn52-shakedown-final.log` exited 0 and reports PASS for both vessels.
  Solo 3.23 m/s, coupled 2.53 m/s; coupled heel 0.0 degrees, support 0%,
  tug under-keel clearance 7.55 m. Those are the check's sampled prototype values.
  The corrected port view shows the entire combination without clipping.

Raw XML/logs remain in ignored
TestResults/Logs; selected original captures are retained in `Previews/`.
Some isolated PlayMode scenes emit the existing no-audio-listener warning.
No compilation errors or failed assertions remained in the successful runs.

## Limits

This is a photo-based reconstruction, not a measured replica or validated
maritime handling model. No public T-77 lines plan was recovered. Period paint,
underwater offsets, hidden piping and coupling rigging are inferred; see README.
The supplied underway photo depicts RT-291, not evidence of Serpukhov pushing
MN-52 on the centreline. The requested pairing is a gameplay configuration.

No player attach/detach UI, tank slosh, cargo-transfer interface or physical
shader-wave sampling was added. The loaded barge uses existing water-level
hydrostatics and world colliders. Tug HUD/grounding scoring still concerns the
tug. The raised barge cabin blocks much of the navigator's forward view;
external and elevated cameras are provided. Human assessment of handling feel,
extended rough-weather runs and a standalone player build were not performed.
