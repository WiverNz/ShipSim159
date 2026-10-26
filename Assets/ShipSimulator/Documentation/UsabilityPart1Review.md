# Usability Part 1: interface and settings

Date: 2026-10-27. Scope: Part 1 of [the usability plan](UsabilityImprovementPlan.md).
Parts 2-9 are not implemented by this change.

## Changes

- Shared canvas scale policy and an 80-150% interface preference, applied live to menu
  and HUD. Settings categories, vessel selection, confirmations and control drawers
  scroll instead of shrinking labels to fit. The menu retains its decorative chart
  when there is enough room and uses a single content column otherwise.
- Labelled icon tabs for Helm, Deck, Camera, Weather, Map, Help and Settings, using
  the existing procedural HudTheme. Main telemetry and warnings remain independent
  of the active drawer. Navigation stays beside the drawer at wide logical widths;
  narrower layouts expose it through Map. Existing command methods are reused.
- Additional height for rudder readouts, radar legend and weather controls; larger
  loading/tackle labels and anchor buttons. Selected HUD buttons no longer grow into
  adjacent controls. Tackle status can grow with the number of mooring lines. Loading status grows for
  wrapped limit/range messages, keeping its buttons pinned below the readout.
- Settings > Interface & camera provides independent rotation and zoom multipliers,
  restores their defaults together with UI scale, and preserves other settings.
  Rotation migrates from the existing Sensitivity preference. Invalid/nonfinite
  values are bounded or replaced with defaults. PlayerPrefs remains the persistence
  mechanism; settings completion, resume, focus loss, suspend and quit flush it.
- Left-click-only HUD commands; visible slider/input/dropdown focus blocks gameplay
  keyboard input. Hidden settings controls do not block a resumed passage. Camera
  gestures begin only outside UI, and cursor capture ends on release, pause, view
  change, disable or focus loss. Zoom uses its new multiplier; the default zoom
  response and camera smoothing remain for Part 3.

No new external fonts, icon packs, models or physics parameters were introduced.
The physics controller change is limited to the keyboard-input guard. Scene and
prefab content is unchanged. Menu save DTOs and physical ship command methods remain
unchanged, including tug and push-unit support added since the original plan audit.

## Evidence

Before captures: `Logs/Usability/before/`, 20 images. These showed the radar overlapping
telemetry, weather buttons extending below their panel and small crowded controls.
The follow-up text-bound checks also exposed truncated rudder and radar legend text.

After captures: `Logs/Usability/after/{80,100,150}/`. The matrix covers 1280x720,
1920x1080, 2560x1440, 3440x1440 and 1280x1024. It includes home, vessel selection,
sound, interface settings and all six HUD drawers, plus settings/deck scrolled to
bottom. The capture test checks preferred text height, command-button bounds and
bottom-of-content reachability. UI captures use a neutral background intentionally;
world-composited evidence comes from the fleet shakedown.

Earlier `early-text-overflow.txt` files and failed XML runs are retained as diagnostic
history, not final acceptance results. Three existing runtime tests were adapted to
open the relevant HUD tab instead of assuming every panel was simultaneously visible
or directly parented to the canvas; their telemetry/contact assertions remain intact.
The focus test runs in PlayMode because EditMode does not provide the same EventSystem
lifecycle. Scroll-bound checks measure content rectangles, excluding decorative shadows.

Final verification:

- EditMode: 282/282, `TestResults/check-EditMode-20261026-223113-163.xml`.
- Full PlayMode: 175/175, `TestResults/check-PlayMode-20261027-000936-077.xml`.
- Final UI/focus/menu regression after the capture and adaptive-loading-panel follow-up:
  7/7, `Logs/usability-acceptance.xml`, exit 0, no compiler errors. The matrix also
  empties the holds to exercise combined loading-limit and hydrostatic-range messages.
- Fleet shakedown: PASS, 7 commands, `Logs/check-Shakedown-20261027-004101-773.log`.
  Corrected world-composited images are under `Logs/Shakedown/`.
- Voyage menu smoke: PASS, `Logs/check-Menu-20261027-004200-375.log`, including hatch
  orders, anchor release, ballast transfer, vessel/scenario changes and save/load.
- All listed runs completed successfully; `git diff --check` passed.

The synchronous LandscapePreview capture now refreshes HUD layout after assigning
its render target. Otherwise it captured layout for the editor's smaller Game view
inside a larger image. The menu smoke check opens Deck and supplies explicit left
pointer events, matching the new input contract rather than passing null events.

## Remaining scope

Localization, camera smoothing/tuning, interactive map range/pan, physical buoys,
grounded-bow rendering, safe-position recovery and commodity-specific cargo remain
in Parts 2-9. No claim of those fixes is made here.

The automated checks and reviewed renders do not replace a human handling session.
A fresh standalone release build and integrated player acceptance remain Part 9;
this stage exercises Unity runtime UI and the existing editor-driven voyage checks.
