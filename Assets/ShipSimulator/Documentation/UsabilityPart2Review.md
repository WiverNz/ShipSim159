# Usability Part 2: four interface languages

Implementation date: 2026-10-27. This is Part 2 of
[the usability plan](UsabilityImprovementPlan.md), not a vessel modelling part.

## Cause and implementation

The project had no localization service or package. English text was embedded in
runtime UI builders, telemetry formatters, scene-authored mission descriptions,
camera names, passage admission messages and equipment notices. Merely adding a
language dropdown would not have updated those sources or already visible text.

The implementation adds one shared runtime service and 440 matching keys in four
UTF-8 catalogues under `Resources/Localization`. English is the default and missing
translation fallback. Missing keys remain visible and produce development diagnostics.
The existing Unity UI and built-in font are retained; no external package, font,
graphic or model is required.

Settings > Language offers English, Русский, Deutsch and Español. The selected
choice is indicated and persisted immediately through VoyageSettings/PlayerPrefs.
Selection changes existing labels and current formatting functions without rebuilding
sliders, reloading a scene, changing the selected vessel or altering the voyage save.
Open tabs, focus, controls, mission state, cargo and camera selection are preserved.

Coverage includes the start/pause/confirmation menus, settings, help, HUD instruments,
map legend, weather, camera views, loading, hatches, anchors, mooring, grounding
warnings, mission instructions, passage limits and save/load failure summaries.
Technical exception details remain in the log. Native vessel names, keyboard keys,
compass letters and conventional unit notation may remain language-independent.

Tackle notices retain a template and arguments, so a notice already on screen changes
language too. Loading keeps its canonical status code. Physics branches and anchor
warning colours do not compare translated text. Display culture is explicit and does
not change serialization, data IDs, force integration or the save schema.

LocalizedLabel updates existing Text components and refreshes hidden labels on enable.
Menu rows wrap and reflow in place. HudPanelFlow measures the text and buttons in the
existing loading/machinery/tackle panels, wraps command groups when required and
updates their scrollable height. It responds to language, text, Canvas scale and font
atlas changes. Telegraph and tab captions have more horizontal space.

See [Localization](Localization.md) for catalogue maintenance and API contracts.

## Verification

The layout matrix covers en/ru/de/es, 1280x720, 1920x1080, 2560x1440,
3440x1440 and 1280x1024, at 80%, 100% and 150% UI scale. It renders menu,
vessel selection, sound, language, display, passages, interface settings and all
HUD groups, with empty and loaded compartments. It checks text bounds, button
containment and access to the bottom of scrollable panels. The 960 captures are
under `Logs/Localization/<language>/<scale>/`; the final failure list is empty.
Representative Russian HUD/menu, German settings and Spanish language screenshots
were visually inspected. These captures isolate the UI on a neutral background;
the fleet check renders the HUD over the actual scene.

The first full PlayMode run passed 175/176 tests and identified a Russian telegraph
caption clipped in its old button width. The full layout audit then identified narrow
loading, hatch and mooring buttons. Those are corrected by measured layout, not smaller
fonts. A further check exposed stale text metrics after a Canvas scale/font atlas
change; invalidation now includes both. Earlier failed logs are retained.

The focused final layout and live-switch tests passed 2/2 in
`Logs/localization-layout-final.xml`. Live tests switch while sailing and paused,
activate the actual language button and verify slider identity/focus, camera view,
vessel pose, cargo, controls, mission state and a pending equipment notice.
Catalogue tests check key/placeholder/rich-text parity, glyph coverage, persistent
preferences, invalid language fallback, missing-key diagnostics, English fallback
and display culture isolation.

Final integration results on the implementation:

- EditMode: **289/289**, `TestResults/check-EditMode-20261027-090608-215.xml`.
- PlayMode: **176/176**, `TestResults/check-PlayMode-20261027-085844-868.xml`.
- Fleet shakedown: **PASS, seven vessels/commands**,
  `Logs/check-Shakedown-20261027-090407-978.log`.
- Menu/save-load smoke: **PASS**, including the existing mechanism and loading
  interlock checks, `Logs/check-Menu-20261027-090511-655.log`.

The final EditMode rerun includes a test-isolation adjustment: the missing-translation
case uses a unique temporary key and restores the table, so it can be rerun in the
same editor. No production code changed after the full PlayMode run.
All final launchers exited zero and reported no C# or analyzer errors.

## Limits and scope

This work does not implement Parts 3-9: camera smoothing, map interactions, buoy
physics, grounded-bow water rendering, recovery and cargo identity remain separate.
No standalone executable was rebuilt; release/player acceptance remains Part 9.
The prototype is not validated for maritime training. Translation and automated
layout checks do not establish training suitability or substitute for player feedback
on nautical terminology.
