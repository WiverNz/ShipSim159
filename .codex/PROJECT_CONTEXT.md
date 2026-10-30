# ShipSim159 Project Context

## Usability Part 6, 2026-10-30

Closed grounded bow rendering verification. The earlier forecastle triangle-winding
repair remains unchanged in source blend/export/three FBXs. No new hull mask,
shader patch, collider or hydrodynamic changes. Review and screenshots:
`Assets/ShipSimulator/Documentation/UsabilityPart6Review.md`.

Added opt-in `BowWaterReview` shared runtime fixture and dedicated editor launcher;
`LocalWindowsBuild.RunBowReview` writes `build/BowReview/ShipSim159.exe`.
EditorFinal and PlayerFinal both PASS 168 captures, exit 0; 14 conditions across
loading/grounding/trim/hatches/day-night, three LODs and four cameras. Logs under
`Logs/BowWater/`; build succeeded with zero build errors/warnings. Representative
views inspected without recurrence of the step gap. Compile passed and EditMode
344/344 in `check-EditMode-20261029-225708-760.xml`. Full PlayMode
205/205 passed in `check-PlayMode-20261030-052610-933.xml`, exit 0.
Unity-generated serialization/build settings were restored after verification;
only the diagnostic, build entry point, reviews and screenshots remain changed.

Fixture limits: analytical estimated slope, constrained X/Z/yaw, force-stepped
heave/pitch/roll; depth offsets +/-0.4 m retain rendered datum y=0. Pairwise matrix,
not all combinations; PASS is capture/contact/error-log validation, not automated
pixel detection. Still captures disable TAA. Normal gameplay never starts the
fixture without the command-line flag. No new graphics resources required.
Next requested implementation is usability Part 7 recovery, not modelling Part 7.

## Usability Part 5, 2026-10-29

Completed physical buoy/contact work. See
`Assets/ShipSimulator/Documentation/UsabilityPart5Review.md` for the audit,
parameters, behavior, save/reset semantics, tests and declared limits.

- `MooredBuoy` and `Resources/BuoyPhysics.json`: 650 kg dynamic marks, solid
  float/tower contacts, damped heave/righting, field/legacy-zone drag and a bounded
  4 kN horizontal tether. Visuals, lanterns and map markers follow the physical body.
- `BuoyVisualRig` no longer forces triggers or animates an independent Transform.
  Lens colliders are disabled before parenting under a dynamic body.
- `HullCollisionDetection.Mode` selects Continuous Speculative on active/passive
  hulls. Large sweep-CCD hulls missed early contact with small marks at coarse steps.
  Buoys use Continuous Dynamic. Hull geometry, grounding exclusions, hydrodynamic
  equations and push-unit coupling geometry are unchanged.
- Canonical builders and `BuoyContactBuilder.Install` cover all three scenes
  (20/42/22 marks), the navigation prefab and unlit marks.
- Optional version-1 save `buoys` retain pose/velocity under stable navigation ids.
  Legacy saves reset marks at the saved water level. R is still ship-only reset.
  The bounded save-file limit is 1 MiB.
- 864 buoy contacts and 63 wall approaches passed, plus return, environment,
  sustained thrust/reverse, save and lighting regressions. CSVs: `Logs/BuoyPart5/`.

Verification: EditMode 344/344 (`TestResults/check-EditMode-20261029-222108-181.xml`); PlayMode 205/205 (`TestResults/check-PlayMode-20261029-175915-721.xml`). Fleet 7/7 (`Logs/check-Shakedown-20261029-222202-009.log`); beacon visuals PASS (`Logs/check-Buoys-20261029-222409-654.log`).

Core changes were already present in commit 2bb9a3b before final closeout.
This continuation only completed verification and documentation. No agent commit,
push or player build. Part 6 remains separate.

## Usability Part 4, 2026-10-29

The old HUD used a fixed 0.82 pixel/metre projection, fixed range rings and prediction
horizon. It synthesized a short familiarisation route and buoy positions even when a
scene had its own FairwayRoute. Its viewport did not accept pointer events. Several
layers also used fixed clipping extents, which could hide lines crossing the view. Thin lines used
soft sliced sprites, causing visible gaps, and enlarged range rings inherited thick,
blurred strokes. Map lines are now solid; range rings use constant-width UI geometry.

The existing HUD and RadarChannel now share MapProjection for contacts, fairway,
shorelines, mission target, own ship, track, obstacle footprints and prediction.
The range is a bounded 100-4000 m radius, adjusted by buttons or wheel over the chart.
Expand opens the right half of the HUD without covering the permanent engine controls
or left tools. Left-drag pans the expanded map; Follow ship resumes tracking. A compact
legend and heading-up/north-up buttons are available in both sizes. Off-chart ship and
goal markers stay on the chart edge; their clamped position is an indicator, not a
new world position. Rings represent half and full selected range. Labels, footprints
and prediction horizon use current viewport scale, including reverse travel.

FairwayRoute and ScenarioBathymetry supply scenario geometry and estimated depths;
River familiarisation keeps FairwayModel. Channel centre and edge colours use sampled
depth less the vessel's physical draft. MIN AHEAD samples ahead of the ship even while
panning elsewhere. The goal comes from the mission's existing phase-distance table.
Buoy marks follow the actual scene objects, including unlit Serpukhov marks. This does
not change buoy physics. Natural-bank profiles supply the original scenes' shorelines once ShipWakeController
has created the profile (the HUD can start earlier);
Serpukhov uses the same exposed water-polygon edges as its terrain builder.
MapChartBuilder derives its resource from SerpukhovZaton.json and is called by the
scenario builder. Its north orientation uses the authored geographic bearing. The two
synthetic original scenes use world +Z as their north convention.

Map range, orientation, expanded state and legend persist in VoyageSettings. Panned
world positions are transient. Voyage restore/reset clears the track and recentres;
a new HUD for another vessel or scene starts following its ship. Clicking Follow ship
preserves the track. No save schema, ship dynamics, collision response or cargo changed.

The installed InputSystemUIInputModule normalizes scroll by
scrollDeltaPerTick; the map converts that UI value once. The camera retains a world
RMB gesture across UI until release, and map gestures cannot acquire its wheel. A left
map drag retains ownership outside its viewport, ignores other-button releases, and
clears on focus loss, menu opening or disable. Buttons use normal left-click semantics.
All new labels are translated in English, Russian, German and Spanish.


Verification: - Full EditMode: 340/340, check-EditMode-20261029-080700-872.xml.
- Full PlayMode: 192/192, check-PlayMode-20261029-080857-941.xml.
- Additional three-scene integration/captures: 1/1, map-scenarios.xml.
- Focused map/camera/HUD/obstacle checks: 17/17, map-focused.xml.
- Layout matrix: four languages, eight resolutions from 800x600 through 3440x1440,
  three paired UI/panel scales, collapsed and expanded map with legend. Tests check
  text clipping, panel bounds, engine/map/drawer separation and ship-marker visibility.
- Scenario checks compare rendered route segments to FairwayRoute, check goal bearing,
  actual shore presence and engine separation, then capture 800x600 and 1920x1080
  views in River, Gorodets and Serpukhov. Projection/inverse, orientation, range bounds,
  persistence, moving marks, panning, recenter and reset have focused regressions.
- Fleet shakedown: check-Shakedown-20261029-081537-018.log.

Logs and full capture matrix remain under Logs/MapPart4 and Logs/Localization.
No new standalone executable or manual physical-mouse acceptance is claimed. Existing
player builds need rebuilding. Depths and geographic reconstruction remain estimated;
passing these checks does not establish real-navigation accuracy.


Source review: Assets/ShipSimulator/Documentation/UsabilityPart4Review.md.
No commit/push. Part 5 remains unstarted.


## Volgo-Don bow gap beneath the mast, 2026-10-29

User screenshot `/mnt/g/tmp/prosvet.png`. Two forecastle step triangles faced inward;
back-face culling exposed water from aft. Registered Blender MCP repaired their winding
and corner normals in the main source and both LOD scenes. Exported all three FBXs;
saved all five source scenes without the user's unrelated default Blender scene.
Only six triangle orientations changed, no vertex/UV changes; comparison report and
before/after images are in Art/Source/VolgoDon507B/BOW_STEP_REVIEW.md.
part8_export.py invokes idempotent repair_forecastle_step.py before LOD generation.
No prefab, physics, water-mask or material changes.

VolgoDonBowReview validates imported winding/normals/13.744m2 coverage in all three
LODs. Six Unity captures inspected; BOW_REVIEW|PASS in Logs/bow-after.log, no exceptions.
Uses SingleCameraRequest because Camera.Render failed on the first cold-start baseline
frame. The successful baseline starboard capture is retained, not the black first frame.
EditMode331/331: check-EditMode-20261029-001706-114.xml.
PlayMode189/189: check-PlayMode-20261029-001859-746.xml.
Fleet shakedown passed: check-Shakedown-20261029-002529-578.log.
No player rebuild; build/HudReview remains the preceding UI build. No commits or push.


## Adaptive essential HUD and standalone display choices, 2026-10-28

User requested map/own-vessel marker and engines always visible in separate regions,
especially in small windows, then added window-size/fullscreen settings. Supersedes the
shared-scroll behaviour of the earlier compact HUD below. Root causes: HudLayout moved
essentials into one scroll container below 1600 logical pixels; player defaults were
1024x768 with resizableWindow=0; settings only toggled fullscreen, without dimensions.

HudLayout keeps TelegraphBlock and MiniMap directly under the Canvas, at lower left/right,
independent of tabs and legacy hidden preferences. Engine HudPanelFlow wraps astern/stop
and ahead groups; all seven commands and RPM/load remain available. Optional tools share
a left drawer above engines, without covering essentials or the centre. UiLayout.Apply
can cap voyage HUD scale to reserve at least960x720 logical units; menus keep their existing
scale/reflow. Essential settings rows are fixed Always visible; M toggles navigation tools.

VoyageSettings adds saved WindowWidth/Height, presets800x600 through3440x1440, monitor-fit
window bounds, and native FullScreenWindow. Fresh preference defaults fullscreen; legacy
mode and explicit Unity screen CLI arguments respected. Toggle follows actual player mode
(including Alt+Enter changes). Window fallback1280x720, ProjectSettings resizableWindow=1.
Display/window-size pages translated in all4 catalogues. No voyage/physics/save schema change.

EditMode330/330: TestResults/check-EditMode-20261028-200320-399.xml.
Final PlayMode189/189: TestResults/check-PlayMode-20261028-201442-450.xml.
Layout matrix4languages x8resolutions x3paired global/panel scales; no clipping, explicit
essential bounds/no overlap/marker/centre/scroll assertions. Shakedown7 PASS:
Logs/check-Shakedown-20261028-234318-742.log. LocalWindowsBuild.RunHudReview produced
build/HudReview/ShipSim159.exe, Logs/hud-review-build.log and Logs/HudReview/build-report.txt,
0errors/0warnings. Opt-in HudWindowProbe checked actual OS transitions and engine buttons:
Logs/hud-window-player.log HUD_WINDOW|PASS, exit0; Logs/HudWindows/20261028/results.txt.
Actual windows800x600,1024x768,1280x720; requested1920x1080 fitted1888x968 on this monitor;
fullscreen1920x1080; return800x600. Probe restores its three display preference keys and
never saves the voyage. New Tools/README entry documents invocation. No performance claim.

ProjectStatus/OperatorGuide and screenshot inventory updated with actual player captures.
Five build-generated renderer/cloud settings files restored to pre-build snapshots; intended
ProjectSettings window changes retained. No commit/push/release publication. Development
review binary is available for manual checking; existing release executables are unchanged.

## Configurable compact HUD, 2026-10-28

User requested selectable, smaller weather/time/camera/equipment panels. Root cause:
HudLayout exposed mutually exclusive groups, buried time with camera and equipment
with loading, and reserved a 716-unit drawer even for small controls. Extended the
existing layout, settings and four localization catalogues; no duplicate HUD or physics.
Settings > HUD panels cycles ten stable panel IDs through hidden/tab/pinned modes,
saved in PlayerPrefs, with separate bounded 75-115% panel scale (85% default).
Map and simulation speed default pinned. Drawer sizes follow scaled content; wide
screens have a pinned sidebar, narrow screens share a scroll drawer with the selected
tab first. Existing vector icon bar has localized hover/focus captions and toggles
its active drawer closed. Equipment still appears only on supported vessels.
M opens tab-only maps and toggles pinned maps without hiding the pinned objective.
Help wraps to the free width beside pinned controls. Settings reset only panel choices.

Verification: EditMode327/327, TestResults/check-EditMode-20261028-181459-672.xml;
full PlayMode188/188, check-PlayMode-20261028-181546-412.xml before final refinements;
final UI suite12/12, TestResults/hud-final-ui.xml. Matrix4 languages x5 resolutions
x3 paired UI/panel scales; Logs/Localization/layout-failures.txt empty. Added preference,
visibility, pinning, map-shortcut and translation regressions. Final Shakedown and Menu
checks PASS; fresh screenshots copied without editing into docs/screenshots/hud-panels-
20261028-*.png and referenced by ProjectStatus. OperatorGuide updated. No external art
required, no release/player rebuild or commit/push. Existing collision/cargo work unchanged.

## Release CI correction and current ProjectStatus, 2026-10-28

User requested analysis/fixes for Actions run40547214885 and refreshed ProjectStatus.
Read Documentation/ReleaseDiagnosticsReview.md. Original v0.1.3 run on08eea54 was
successful and published, but Unity summary had3errors/46warnings. Repeated errors:
immutable MCP package missing Editor/Tests/McpUnityServerBatchModeTests.cs.meta.
Pin manifest/lock to c69c3c61a2de6492f4d247f94bb3452f3ad82d73, whose sole change from
382a43a is that metadata. Do not edit Library or remove the working integration.
Modernized seven deprecated unsorted object searches. Enabled convex pre-bake on13
507B sections and triangle pre-bake on2 river banks, in assets AND their builders.
Geometry verified unchanged. Release workflow now calls LocalWindowsBuild.RunRelease,
uses manualExit, rejects nonzero reported errors even with Succeeded, and uploads
Logs/Release/build-report.txt. Local development entry points remain available.

EditMode325/325: TestResults/check-EditMode-20261027-193842-846.xml.
PlayMode187/187: TestResults/check-PlayMode-20261027-193914-112.xml.
Shakedown7/nine views PASS: Logs/check-Shakedown-20261027-194435-893.log.
Menu/equipment/loading PASS: Logs/check-Menu-20261027-194543-489.log.
Release fixture tests10/10. On Oct28 RunRelease built non-development Windows player
with0errors/0warnings, Logs/ReleaseDiagnostics/release-build.log and
Logs/Release/build-report.txt. bash release-smoke passed; built-player startup and
voyage probe completed exit0 in Logs/ReleaseDiagnostics/player-startup.
No remote Actions rerun or publication by agent; Linux-specific runner noise remains
separate from verified local corrections. User committed implementation as ab60d21
while task paused; final gallery and verification docs follow in working tree.

ProjectStatus rewritten as current capabilities/fleet/equipment/usability/verification/
limits overview; removed stale five-vessel counts, procedural Meteor/Luch claims and
historical trials labelled current. Ten actual Oct27 captures exported and reviewed
Oct28 in docs/screenshots/status-20261027-*.jpg with source inventory. Incomplete
HUD diagnostic captures excluded; no fabricated imagery. Old images retained for
other documents. Build-only generated render/cloud settings restored to HEAD.
No agent commit/push/tag. Remaining usability Parts4-9 require user request.

## Usability Part 3 implemented, 2026-10-27

Read `Assets/ShipSimulator/Documentation/UsabilityPart3Review.md`. This is the
usability camera part, not the Volgo-Don modelling Part 3. Parts 1-2 complete;
Part 3 implementation and automated verification complete, physical mouse/trackpad
comfort assessment remains open. Do not begin Parts 4-9 without a user request.

Installed Input System 1.20.0 emits uniform wheel ticks, confirmed in the Windows
player: conventional event +1, fractional events +0.125. Old gain .002 changed
radius by only .2% per tick. New CameraOrbit integrates normalized ticks once with
ln(1.25) gain (20% inward/25% outward), .06 deg/pixel rotation instead of .12,
exponential unscaled yaw/pitch/log-distance smoothing (.085s) and follow (.12s).
Exterior orbit and look share one heading-only pivot; navigator remains at model
eye, bridge presets retain forward look. Pitch 0-90 with analytic overhead
orientation preserves all authored presets. Three serialized scenes updated;
builders use component defaults. Existing saved rotation/zoom preferences reused.

Cursor lifecycle handles UI-started versus world-started RMB, wheel ownership,
focus/menu/disable/target/view changes and stale delta rejection. RestoreCount
resets tracking after ship restore. CameraSave orbitVersion1 records rendered orbit;
missing version0 migrates origin-based coordinates, within current bounds. Voyage
version/atomic persistence unchanged; unknown camera coordinate versions rejected.
No vessel physics, map mechanics, collision, recovery or cargo changes.

Final EditMode304/304: TestResults/check-EditMode-20261027-133102-872.xml.
Final PlayMode187/187: TestResults/check-PlayMode-20261027-133134-307.xml.
Shakedown PASS7, all9 views: Logs/check-Shakedown-20261027-133702-112.log.
Menu/save-load/mechanism/loading smoke PASS: Logs/check-Menu-20261027-133806-675.log.
63 screenshots Logs/Shakedown; reviewed 7 overview sheets Logs/Camera/Views.
Known push-unit navigator cabin obstruction preserved, not a new camera regression.

Windows development build PASS zero errors/13 existing warnings:
build/CameraReview/ShipSim159.exe; Logs/camera-after-build.log and
Logs/Camera/build-report.txt. Run normal exe for interactive review; opt-in
Tools/camera-input-probe.ps1 injects OS events only into its own foreground window,
freezes diagnostic vessel and restores temporary sensitivities, without voyage saves.
Before/after traces Logs/Camera/{baseline-native,after-native}; native-comparison.json
shows .998002 -> .800039 distance ratio per positive tick, 12 -> 6 degrees per100px,
6 -> 3 degrees per50 vertical pixels; horizontal pitch drift0. These are synthetic
Windows events, not physical-device or subjective handling proof. No Part9 release
claim. No production code changed after full suites; build-generated render/cloud
settings restored to their clean pre-task contents. Docs/READMEs/plan updated.
No agent commit or push. New assets include .meta files; Logs/build are ignored.

## Usability Part 2 complete, 2026-10-27

Read `Assets/ShipSimulator/Documentation/UsabilityPart2Review.md` and
`Localization.md`. At this checkpoint Parts 1-2 were complete; see Part 3 above for subsequent camera work.
Settings > Language offers English, Русский, Deutsch, Español, persisted in
VoyageSettings. One Localization service loads four UTF-8 Resources catalogues
with 440 stable keys and English fallback/development diagnostics. Source is an
exact adapter for canonical authored text, not a substring translator. Existing
Text bindings refresh live/on enable; no parallel UI, scene reload or save migration.
Tackle notices retain templates/arguments, loading retains its status code.
Display culture is explicit; physics branches, data IDs and serialization stay stable.
HudPanelFlow fits loading, machinery and tackle controls to translated captions,
including Canvas-scale/font-atlas invalidation. Menu reflow preserves focused sliders.

Final EditMode 289/289: check-EditMode-20261027-090608-215.xml.
Full PlayMode 176/176: check-PlayMode-20261027-085844-868.xml.
Live tests preserve pose, cargo, controls, mission, camera, tabs and a pending notice;
they also activate the real language selector. All-language layout matrix:
4 languages x 5 resolutions x 3 scales, 960 captures under Logs/Localization;
layout-failures.txt is empty. Font glyph, key/placeholder/rich-text parity, preference,
fallback and display-culture tests pass. Initial clipping failures are documented.
Shakedown PASS 7 commands: check-Shakedown-20261027-090407-978.log.
Menu/save-load smoke plus mechanism/loading checks PASS:
check-Menu-20261027-090511-655.log. Final launchers exit 0, no compiler errors.

Plan, English/Russian READMEs, operator guide and status/index updated.
No external graphics/fonts/models required. No standalone player rebuilt, Part 9
retains that acceptance. No agent commit/push: the user committed implementation as
c5d6823 while this task was running; completion documentation follows in the working tree.

## Usability Part 1 complete, 2026-10-27

Read `Assets/ShipSimulator/Documentation/UsabilityPart1Review.md`. Part 1 of the
usability plan was completed at this checkpoint; see Part 2 above for subsequent work. Shared 80-150% UI scale,
scrollable responsive menu, labelled icon HUD tabs, independent persisted rotation
and zoom multipliers, legacy sensitivity migration and control-default reset added.
HUD commands accept only left-click. Gameplay keyboard input is blocked by visible
editing controls; hidden sliders do not block resume. UI owns gestures begun over it;
camera orbit releases on pause/focus/view change. Camera smoothing remains Part 3.
Readouts and loading/tackle status fit content; lower controls remain scrollable.
No physics parameters, prefabs, scenes or save DTOs changed.

Verification: full EditMode 282/282 (20261026-223113-163), full PlayMode 175/175
(20261027-000936-077), final UI/focus/menu 7/7 (`Logs/usability-acceptance.xml`).
Matrix: five resolutions x three scales, all HUD groups/menu pages, scroll reach and
empty/full holds; captures under `Logs/Usability/`. Shakedown PASS 7 commands
(20261027-004101-773); menu smoke PASS (20261027-004200-375). Synchronous capture now
refreshes layout after target resize. Existing test/diagnostic lookups now open tabs,
with explicit left-pointer events, preserving their original state assertions.
README/README.ru/OperatorGuide and the plan updated. No external assets, commit or
push. No new standalone executable: integrated player release remains Part 9.

## Usability audit and execution plan, 2026-10-25

User requested source investigation and a numbered plan before implementation.
`Assets/ShipSimulator/Documentation/UsabilityImprovementPlan.md` defines usability
Parts 1-9, independent of completed modelling Parts 1-17: UI/settings, four languages,
camera, map, buoy/contact physics, grounded-bow water, recovery, typed cargo and
integrated acceptance. All parts are unstarted. Execute the requested part only.

Confirmed in code: BuoyVisualRig makes colliders triggers; camera consumes raw wheel
units and lacks UI ownership; radar scale is constant; localization is absent; hold
visuals use mass-driven nominal bulk without commodity ids; R resets to startup pose
without safe-position history. Bow water and reported empty holds need reproduction.
Preserve Part 16 contact/grounding work. Plan selects validated safe-pose recovery
with inventory/mission preservation and scenario fallback; extend existing loading
for sulphur, steel channels and grain.

README corrected; README.ru.md added per the explicit Russian documentation request.
Documentation index updated. No runtime code, scenes or model assets changed; no
Unity tests run for this documentation stage.

## Volgo-Don Parts 16-17 and built player, 2026-10-25

Read Art/Source/VolgoDon507B/PART16_REVIEW.md, PART17_REVIEW.md and PLAYER_PERFORMANCE.md.
Parts 9-15 audited; user-confirmed Part 15 retained. ContactBuilder now regenerates
13 convex sections from the corrected hull: sampled maximum missing coverage
5.969 -> 0.066 m, sampled excess breadth 0.481 m. Physical root/mass/CG/inertia
preserved. Gorodets bathymetry-owned bank/shoal colliders avoid double reaction;
quays remain PhysX. Grounding uses Rigidbody pose immediately after restore.
48 contact/load/timestep combinations, light/heavy ground/recovery and combined
anchor-ground contact pass. Side recovery uses an explicit 30 kN pull, not a promise
of self-refloating from any shoal.

Registered Windows Blender MCP verified: uvx.exe, 127.0.0.1:9876, protocol 15,
execute_blender_code and look both work. Source reopened/exported/reopened; 146 mesh
signatures match Part 13; 45 protected legacy assets and archived Part 9 hull match.
LOD triangles 59543/36044/24474. No new external model/texture/photo/audio assets.

Final tests: EditMode 269/269 (20261025-111830-366), PlayMode 161/161
(20261025-111903-137). Numerical trials match all 15 Part 12 after-report rows;
menu, five-vessel shakedown, machinery/hold/tackle reviews pass. Images in Previews/part17-*.
LocalWindowsBuild produces build/StandaloneWindows64/ShipSim159.exe, Unity6000.6.0f1,
Windows x64 Mono Development, three existing scenes. Package smoke and actual
27-case player run/15 switches pass. Player-only fixes: retained runtime navigation/rain
shader resources; conservative mirrored culling with oblique reflection clipping.
No graphical/physics subsystem disabled to hit budget.

RTX4090/i9-13900K/32GB, 1080p PC, VSync off: final whole-frame p95 3.04-4.92 ms,
no sampled >50 ms frames. Some p99/rare frames exceed 16.67 ms. Earlier rejected run
has 175/259 ms stalls, retained honestly. GPU FrameTimingManager works; draw-call
counter unusable; water GPU/audio DSP not isolated. Target RTX3060 unavailable.
Known fidelity/legacy license/free-surface/lightship-trim/hold-volume/analogue-audio
limits remain. Small soak memory growth is recorded, not a no-leak certification.
No commit/tag/push. Do not rerun completed modelling to fix these external limits.

Cold-path follow-up: independent LOD repeats passed runtime checks but the first
fresh-process path again stalled (max 234.93 ms, two frames >50 ms). Later repeats
max 31.97/26.46 ms. Cause unestablished; cold-path stall elimination remains open.

## Shared sound library, 2026-10-24

Sound is no longer 507B-only. 36 WAVs moved (with `.meta`, GUIDs and bytes unchanged) to
`Assets/ShipSimulator/Audio/Library/<category>/`; one `VesselSoundProfile` per catalogue vessel in
`Audio/Profiles/<vessel id>.asset` (507B's is the moved original); pipeline, sources, registry and
license snapshots in `Art/Source/Audio/` (`build_ship_audio.py`, README with the replacement procedure).
Optional data facts: engine `cylinders`, `strokesPerCycle`, `engineRpmPerShaftRpm`,
`directReversingAirStart`, propeller `bladeCount`, ground tackle `windlassDrive`/`mooringWinchDrive`;
set only for the 507B. `VesselSoundscape` plays a layer when the profile gives a clip and the data or
model allow it (`DisabledLayers` lists reasons): all five vessels hear diesels, wash, hull water
(scaled by the computed support, so it fades foilborne) and wind; firing, starting air, blade rate,
hatches, tackle and wheelhouse filter only the 507B. `ShipAudioBuilder.InstallSoundscape` is called by
every vessel builder. EditMode 267/267, PlayMode 107/107, SHIP_AUDIO_RUNTIME, MENU_SMOKE,
VESSEL_SHAKEDOWN PASS, sea trials identical; 507B behaviour unchanged. See PART15_REVIEW section 10. Follow-up: no propeller wash for the Luch
(waterjet; the clip carries a ferry's propeller tones); diesel layers documented as unsynchronized
texture; raw HTML license snapshots removed (GitHub push protection: embedded site tokens).

## Volgo-Don Part 15, 2026-10-24 (paths as of Part 15; superseded by the shared library above)

Part 15 complete. Read `Art/Source/VolgoDon507B/PART15_REVIEW.md`. `Scripts/Audio/VesselSoundscape`
on the 507B (profile `Audio/VolgoDon507B/VolgoDon507B_SoundProfile.asset`) reads the simulation in
LateUpdate (order 150) and writes nothing back: engine firing pitch = shaft rpm / reference rpm
(synthesized six-cylinder four-stroke rhythm), level from `EngineShaft.FuelFraction`, starting-air
hiss on `IsAirBraking`, air blast on `Direction` change; wash from propeller thrust and immersion;
hull flow, bow wave and lapping from speed through the water; wind relative to the ship; hatch
pump/winch/rolling from the change of opening and parking; chain and line layers from
`GroundTackle.ChainRateMps`/`LineRateMps`, commands, stall, tension and the new `ChangedAt` event.
`RestoreCount` on the controller: a load or reset glides levels, snaps pitch, plays no events for
0.3 s. Pitch never scales with simulation speed; Doppler 0. One-shot pool of 8. Mixer
`Resources/Audio/ShipSimMixer.mixer` (`ShipAudioBuilder`, by reflection; menu Build Ship Audio)
with Engines/Water/Wind/Machinery/Signals volumes and low-pass cutoffs for the wheelhouse filter.
Settings > Sound: master, four buses, Mute; standalone mutes when unfocused. Assets: 36 WAVs in
`Assets/ShipSimulator/Audio/VolgoDon507B/` from 19 CC0 Freesound previews kept in
`Art/Source/VolgoDon507B/Audio/Sources/` and original synthesis; `part15_audio.py` (numpy, scipy,
soundfile) downloads/verifies, builds, analyses; registry `AUDIO_SOURCES.md`. Import rules by name
(`ShipAudioImportRules`): loops Vorbis 0.5, one-shots ADPCM, preload, Force To Mono off.
No 507B recordings exist here: all are analogues. Not listened to by a person.

Verified: EditMode 265/265, PlayMode 100/100, `ShipAudioRuntimeCheck.Run` SHIP_AUDIO_RUNTIME|PASS
(ten real-time recordings in `Logs/Part15/demo`, copies in `Audio/Demo`), MENU_SMOKE|PASS,
VESSEL_SHAKEDOWN|PASS 5 vessels, SEA_TRIALS rows identical to Part 14. Tick 6 us, 0 bytes; clip
memory 3.2 MB; at most 29 voices. Residual: the menu pause cuts sound at once. Part 16 not started.
No commit/push.

## Volgo-Don Part 14, 2026-10-24

Part 14 complete. Read `Art/Source/VolgoDon507B/PART14_REVIEW.md`. `Physics/GroundTackle.cs` on
the 507B, stepped in `ShipPhysicsController.Simulate` after grounding: each chain and mooring line
is a quasi-static elastic catenary (`Physics/Catenary.cs`, bisection on horizontal tension, slack,
touchdown, suspended, elastic stretch; tension only). Horizontal force through `AddExternalForce`,
vertical force and lead-height heel/trim moment on the Rigidbody, damping 0.1 critical on the line
stiffness. Anchors land on scenario bathymetry (none: they hang, NO BOTTOM DATA); hold ratio x
submerged weight (E silt 2 / sand 3 / rock 1), setting over 4 m, uplift loss to 10 deg, rigid-plastic
drag towards the ship, breakout, brake render at 45 % of the E grade 1 breaking load, parting.
Lines: `MooringPoint` (Ship/Shore, stable ids), pass within 35 m at under 0.5 m/s, haul to winch
stall, slack, cast off, overload at 50 %, part at 200 kN (E). Physics values in the vessel JSON
`groundTackle` (validated, optional); hawses and ten ship stations from the prefab, stations from
`mechanisms.json` `mooring_stations` (bollards read from the mesh). `VesselMechanisms` no longer
animates payout; it poses anchors and gypsies from GroundTackle; chains and ropes drawn by
`Visuals/GroundTackleVisual.cs` from the solved shapes. HUD panel ANCHORS & MOORING, keys G B Y U,
O P I , / . F. Saves: `VoyageSave.groundTackle` (present flag); Part 13 `chainOutM` restored as
hanging anchors; missing shore bollards reported. Gorodets: estimated demonstration quay
(`GorodetsBerthBuilder`, menu Install Gorodets Demonstration Berth, also called by the scenario
builder) at x 48, z -70..80 with B1-B7 and a 5 m dredged pocket (`ScenarioBathymetry` basins).

Found and fixed: right after a save restore the transform lags the Rigidbody by one simulation, which
broke anchors out; GroundTackle reads the Rigidbody pose. Catenary returned an imprecise shape at the
exact slack span. Verified: EditMode 258/258, PlayMode 90/90, `GroundTackleRuntimeCheck.Run`
GROUND_TACKLE_RUNTIME|PASS, MENU_SMOKE|PASS, VESSEL_SHAKEDOWN|PASS 5 vessels, SEA_TRIALS rows
identical to Part 13, quality gate PASS. Stowed tackle leaves motion bit-identical. Step 20 us, draw
8 us, 0 bytes allocated. Part 15 was not started at that point. No commit/push.

## Volgo-Don Part 13, 2026-10-24

Part 13 complete. Read `Art/Source/VolgoDon507B/PART13_REVIEW.md`. Archive p4/p5/p7/p8 read
from the scans (hash matches Part 9). Runtime machinery rates are in `mechanisms.json`
`operating_rates` (single source, read by `VolgoDonMechanismIntegration`): hatches 365 s to
half open (141/34/174/16 s phases) and 371 s traverse, derived from the jacks, pump and winch;
anchors let go 1 m/s (E) to the bottom or 150/75 m chain, heave 10/18 m/min. Before: 40/45 s
accelerated preview rates, no chain. Steering 2.5 deg/s and 1:1 shaft ratio kept (now derived).

Fixes: Windlass_* pivots animated the mooring drums; `part13_windlass.py` (registered Blender
MCP) moved the real gypsies under them (pure face partition) and renamed the drums
MooringDrumMesh_*. Forward outer cover rose and shifted together and hit the inner cover;
`part13_hatch.py` lifts it before the shift. `VesselMechanisms` spends each step phase by
phase, exposes HatchPhase/HatchSecondsRemaining, pays out a line-renderer chain, turns the
gypsies from chain; `MechanismSave.chainOutM` with legacy fallback. Cargo transfer waits on
HatchesMoving only. LOD1 budget reviewed to 36,100 (+72 triangles). `part13_validate.py` PASS,
reopened blend reproduces signatures, quality gate PASS, machinery review PASS (covers stay
1.93 m above full cargo), EditMode 251/251, PlayMode 70/70, Menu PASS. Part 14 not started.
No commit/push.

## Volgo-Don Part 12, 2026-10-24

Part 12 complete. Read `Art/Source/VolgoDon507B/PART12_REVIEW.md`. Propulsion audit and
matched archive comparison with sensitivity sweeps (`VolgoDonPropulsionReview.Run`,
`part12-trials-{before,after}.json`): loaded 19.76 vs 20 km/h, unfitted ballast 3828 t
21.19 vs 21.2 km/h; no coefficient refitted. Crash stop 716 m / 285 s stays: reversal is
15 s / 81 m, 510 m would need about +50% astern thrust; archive conditions unstated (half
ahead gives 428 m, ballast 488 m). Steady turn 462 m vs undefined 320 m; dt 0.025-0.1 agree.
`propellerAxisHeightAboveKeelM` 1.0 -> 1.35 m; dynamic propeller immersion for vessels with
loading. Only the 507B lightship sea-trial row changed.

Open-hold water fix: the river plane passed through the hull. `HullWaterMask.shader` quads in
the hatch openings set stencil bit 8, `RiverWater.shader` skips it; `HoldInteriorVisual` draws
nominal 1.6 t/m3 bulk cargo heaps from compartment masses (no mass/colliders), installed by
`VolgoDonHoldIntegration` (called from the mechanism integration). Measured hold space 5194 m3
vs archive 6370 m3, unresolved. Captures `Previews/part12-{before,after}-hold-*.png`.

Grounding fix: 507B contact points were up to 1.5 m (bow) and 2.2 m (stern) below the real
hull, and friction crept below its limit. Now section-grid contacts plus propeller tips,
modulus/bearing pressure along hull normals, and an impulse Coulomb friction solve for all
vessels (no creep, no energy gain). Light touches and a slow partial entry back off under full
astern; firmer/hard groundings hold until draft is reduced. `GroundingRecoveryTests`.
EditMode 245/245, PlayMode 68/68, sea trials PASS, Shakedown 5/5 PASS, Menu PASS. No commit/push.

## Volgo-Don Part 11, 2026-10-23

Part 11 complete. Read `Art/Source/VolgoDon507B/PART11_REVIEW.md`. The 507B now floats
on `hydrostatics.sectionTable` (161 stations, 2736 offsets) derived by
`Art/Source/VolgoDon507B/part11_hydrostatics.py` from the Part 10 closed surface,
exported read-only through Blender MCP to `part11-hydrostatic-surface.json`.
`SectionHydrostatics` (new) builds cached column tables, solves draft/trim by
Newton; `VesselParameters` exposes Draft, TrimSlope/TrimM, DraftAt, Equilibrium,
HydrostaticRange; `HydrostaticsModel` keeps its interface and applies the same
columns. Other vessels and KVLCC2 keep wall-sided prisms (handling bit-identical).
Runtime vs exact mesh clipping: 0.004% volume, 0.005 m LCB, 0.0003 m KB.

Data decision to review: hold/ballast extents now follow authored hold meshes and
lightship LCG moved +2.0 -> -21.9 m (estimate, even keel at design full load). Full
load: 3.494 m, trim 0.00, GM 5.84 m solid. Lightship trims 2.6 m by stern (flagged
outside table range). No free-surface correction (tank geometry unavailable).
HUD shows TRIM and `OUTSIDE TABLES`. Loading rebuild 0.16 ms warm-started, force
evaluation 108 us/step, zero allocations (editor). Tests: EditMode 243/243,
PlayMode 56/56, Shakedown 5/5 PASS, Menu PASS, sea trials PASS with only 507B rows
changed (loaded tactical 4.60 -> 4.29 L, lightship 9.4 -> 12.0 kn). Part 12 not
started; built-player performance still open. No commit/push.

## Volgo-Don Part 10, 2026-10-23

Part 10 complete with explicit unresolved trace discrepancy. Read
`Art/Source/VolgoDon507B/PART10_REVIEW.md`. Reconstructed lower shell with 147 rings,
preserving the original waterline, upper geometry, materials, UV continuity and
48 other meshes. Source blend has an archived original hull and separate closed
hydrostatic scene, excluded from FBX exports. LODs: 59543/35972/24362 triangles.
Unity prefab installed through existing integration; physics/colliders unchanged.

Family507 raster trace retains native pixels and estimated conversion parameters.
Derived section/waterline/buttock tables are not authenticated507B offsets.
Maximum nearest-section residual0.7428m exceeds3px tolerance and remains unresolved.
Hydro surface is connected/manifold, no detected self-intersections; full capped
volume11013.69m3, submerged6824.74m3 atZ0. Cap5.5m above keel; no mass fitting.
Part11 must validate applicability before using this geometry in hydrostatics.

Reopened blend and regenerated exports: identical mesh signatures;33packed images
decode;81protected legacy/physics/vessel files unchanged, including45legacyassets.
720propeller/duct hull configurations and existing mechanism regression pass.
Unity quality gatePASS;228/228EditMode,47/47PlayMode;5/5all-vessel shakedownPASS.
Evidence paths and front/side/top/underwater/LOD screenshots in Part10review.
The Blender look tool failed to retrieve its temporary screenshot; renders were
saved using bpy through registered MCP and inspected locally. No fallback connection.
Part11 and later remain unstarted; player performance remains open. No commit/push.

## Volgo-Don Part 9, 2026-10-23

Part 9 complete, evidence/baseline only. Read
`Art/Source/VolgoDon507B/REFERENCE_DECISIONS.md` before Part 10.
Nine archive pages and three dated FleetPhoto images reviewed. Target remains
representative classic two-covered-hold 507B, no named vessel or modernization.
Page2 project507 body-plan raster is only600x316; use as uncertain family prior,
not authenticated507B offsets. Coordinate mappings and pre-fit tolerances recorded.
Reconciled separate p3/p8 loading cases without changing current6750t/3.53m selection.
Archive320/160m circulation refers to same/opposite nozzle sides; stop protocol
lacks dwell and conditions. p3 .935/.996/.861 coefficients are not silently adopted.

Registered Blender MCP read-only capture:49meshes53130tris match Part8 hashes.
Hull544tris has177boundary/180nonmanifold edges: not a closed hydrostatic solid.
Visual propeller height1.35m above keel differs from physics estimate1.0m.
`part9-geometry-baseline.json` captures meshes/landmarks; `part9-baseline.json`
freezes91file hashes, parameters, historical Part7 evidence and fresh sea trials.
45legacy hashes unchanged. Fresh VirtualSeaTrials exit0, SEA_TRIALS|PASS,
zero compile errors: `Logs/VolgoDon507B/part9-trials.log`.
No runtime/model edits; no full test rerun for this documentation stage.
Part10 not started. Photos/PDF only inspected temporarily, no copyrighted image
assets added. No commit/push.

## Volgo-Don refinement plan, 2026-10-23

User requested a plan for the remaining fidelity work after Part 8.
`Art/Source/VolgoDon507B/REFINEMENT_PLAN.md` defines Parts 9-17: evidence/variant,
hull, hydrostatics, manoeuvring, machinery, anchors/mooring, audio, collision and
integrated acceptance. Each has dependencies, a checklist and acceptance criteria.
Implementation has not started; execute only the next part requested by the user.
Built-player performance remains the separate open Part 8 item. README links the plan.

## Volgo-Don 507B Part 8, 2026-10-23

LOD and final host quality work implemented. Guide: `Art/Source/VolgoDon507B/PART8_REVIEW.md`.
Source .blend has original plus two derived scenes; 53130/34429/24072 triangles.
Run `part8_export.py` through registered Blender MCP, then dedicated batch
`ShipSimulator.Editor.VolgoDonQualityGate.Run` for import, repeated integration,
budgets, collider preservation and screenshots. Keep original FBX fallback unchanged.
33 packed images decode after reopen; semantic geometry matches regeneration;
45 original model resource hashes match. Shared textures 78.28 MiB, 50/50/43 slots.
Runtime LOD meshes are identity children of animated originals. Physics unchanged.

EditMode 228/228: `check-EditMode-20261023-031918-126.xml`.
PlayMode 47/47: `check-PlayMode-20261023-031956-404.xml`.
Full shakedown PASS five vessels: `Logs/VolgoDon507B/part8-shakedown.log`.
Menu/save/load PASS exit0: `Logs/check-Menu-20261023-081632-457.log`.
Sea trials PASS exit0: `Logs/VolgoDon507B/part8-trials.log`.
Final visual/resource gate: `Logs/VolgoDon507B/part8-quality.log`.
Part8 source JSON reports and Previews hold evidence. RTX4090/i9 host timing only:
batch640x480 median1.336ms; GPU timing unavailable. Isolated1080p synchronous
render benchmark is not player FPS. Thread allocation counter unavailable.
Target1080p built-player GPU/GC profiling remains open; do not claim RTX3060 acceptance.
Remaining model/physics discrepancies are listed in PART8_REVIEW.md. No commit/push.

## Volgo-Don 507B Part 7, 2026-10-23

Part 7 complete. Do not start Part 8 until requested. Current guide:
`Art/Source/VolgoDon507B/PART7_REVIEW.md`, with checklist and README updated.
Optional loading.enabled schema and explicit LoadingState.present flag preserve
other-vessel/legacy-save behavior even when JsonUtility instantiates absent objects.
Default inventory:1750t lightship +4800t cargo +200t stores =6750t, draft3.53m.
Two cargo groups, two ballast groups (2200t total) and one stores budget share
5000t deadweight. Capacities, positions, box inertias and rates are estimates.
Archive507B6678t/3.5m conflict remains explicit. No free-surface/certified stability.

CG and diagonal inertia follow actual inventory with parallel-axis terms;
wall-sided hydrostatics resolve sinking/trim through Rigidbody forces. Loading
rebuilds dimensional models and Rigidbody properties without changing pose or
velocity; reuses shaft objects to preserve governor/reversal state and helm.
Mean-draft propeller immersion scales thrust/torque/passive nozzle forces;
bow-thruster immersion refreshes. Physical grounding/clearance remain active.
Transverse asymmetry rejected; longitudinal redistribution supported/tested.

HUD panel above machinery: SELECT COMPARTMENT, FILL, EMPTY, STOP ALL. Stop engines
and ground speed<.3m/s to transfer; cargo needs stationary HALF OPEN hatches.
Commands persist per compartment and pause at interlocks. Rates use simulation
time. Save copies IDs/masses/commands; validates identity/capacity/budget on restore.
Old saves use default inventory. Reset stops transfers, retains inventory.
External shore/water exchange only; no port infrastructure or automatic fuel burn.

Verification: EditMode228/228 TestResults/check-EditMode-20261023-011059-805.xml;
PlayMode47/47 TestResults/check-PlayMode-20261023-011324-017.xml; menu smoke PASS
Logs/check-Menu-20261023-011005-663.log; five-vessel shakedown PASS
Logs/check-Shakedown-20261023-011412-502.log. Loading review PASS exit0
Logs/VolgoDon507B/part7-loading.log, results Art/Source/VolgoDon507B/part7-loading.json.
Other four playable vessels and KVLCC2 exactly match Part6 stored handling metrics.
Full507B straight-line19.80km/h and stop718.5m/286s unchanged; loaded inertia differs.
Physical tests include dry/half/full flotation, forward trim, no teleporting,
loading save continuation and grounded-to-floating clearance recovery.
HUD capture Art/Source/VolgoDon507B/Previews/part7-loading-hud.png inspected.
No geometry/FBX/prefab edits. Runtime model rebuild allocations during transfer
remain a Part8 profiling item. Commits and push not performed.

## Volgo-Don 507B Part 6, 2026-10-22

Part 6 complete. Do not start Part 7 until requested. Current handling guide:
`Art/Source/VolgoDon507B/PART6_REVIEW.md`; checklist and source README updated.
Optional steeringNozzle JSON replaces generic rudder forces for 507B with signed
jet deflection and dissipative passive cross-flow. PropellerY reaches total sway.
Other vessels keep the legacy MMG path. Existing independent engine/reversal
logic, shared helm actuator, Rigidbody, controls, animations and saves retained.

Active 507B physics: archive D=1.8 m, bow waterjet=72 kW / .710 m, nozzle geometry
1.84 x 1.66 m; authored shaft/nozzle x=-64.65, y=+/-3.6 m. Shaft setpoint375rpm
assumes direct drive from the documented engine rating, ratio unconfirmed.
KQ=0.8 of old curves, KT unchanged, CR=.0001005; coefficients remain estimates.
Nozzle deflection=1, passive area=3.0544, slope=3. Bow high-speed floor=0.
6750 t / 3.53 m baseline retained; archive 507B 6678 t / 3.5 m conflict remains.

Deep-water final: 19.80 km/h, shaft341.9rpm /91.2% load, crash stop718.5m/286s,
tactical618.2m, steady-turn499.8m. Speed agrees approximately; stopping agreement
is worse than baseline and archive510m/210s. Do not claim full-scale validation.
Raw before/after and original vessel JSON retained alongside the report.
Editor VolgoDonHandlingReview.After reruns trials and requires unchanged metrics
for other four playable ships and KVLCC2. Before refuses to overwrite baseline.
Trial completion flags prevent timeouts masquerading as completed manoeuvres.

Verification: EditMode217/217 TestResults/check-EditMode-20261022-195651-185.xml;
PlayMode40/40 TestResults/check-PlayMode-20261022-195740-130.xml; handling runner
PASS exit0 Logs/VolgoDon507B/part6-after.log; five-vessel shakedown PASS exit0
Logs/check-Shakedown-20261022-195829-086.log. Initial KQ=.6 candidate failed
efficiency/bollard checks and was discarded; existing bounds remain unchanged.
Source geometry, FBX, materials, prefab and other vessel specifications unchanged.

## Volgo-Don 507B Part 5, 2026-10-22

Part 5 complete. Do not start Part 6 until requested. Current guide:
`Art/Source/VolgoDon507B/PART5_REVIEW.md`. The existing playable prefab now uses
the Part 4 candidate; original FBX/materials and vessel JSON unchanged. Repeated
integration preserves physics/Rigidbody serialization and original colliders.
The existing integrator and catalogue builder install one visual/rig. Menu item
Restore Legacy Volgo-Don Visual restores the original visual; a later normal
integration/build selects improved geometry again. Pre-integration checkpoint:
Logs/VolgoDon507B/before-part5.prefab. Blender source/FBX unchanged in this part.

VesselMechanisms follows each actual signed shaft RPM and actual rudder angle.
Filtered Hatches.anim/Anchors.anim exclude root and propulsion curves. HUD commands
go through ShipPhysicsController. Hatch transitions route through half-open stacks;
anchor control only unseats/stows (no chain payout or holding force). Estimated
40/45/5 second machinery rates are configurable. Optional MechanismSave persists
progress/targets/shaft phase; old saves default closed/stowed. Reset also stows.
Existing navigation-light rig reused with layout-specific local spill reduction;
navigator eye corrected to authored bridge glazing. Eight silent AudioPoints added.

Verification: repeated integration PASS, exit 0 in
Logs/VolgoDon507B/part5-integration-final.log. Final EditMode 209/209:
TestResults/check-EditMode-20261022-194058-156.xml. PlayMode 40/40:
TestResults/check-PlayMode-20261022-185803-807.xml. Five-vessel shakedown PASS,
exit 0 in Logs/VolgoDon507B/part5-shakedown.log (before final spill adjustment).
Menu smoke extended to machinery/HUD/cross-vessel save restoration. Captures:
Art/Source/VolgoDon507B/Previews/part5-runtime-*.png and Logs/Shakedown/.
46/47 baseline resources unchanged; only playable prefab intentionally differs.
All new Unity assets have metadata. No force-model/parameter changes, commits or push.
Navigation lens shader now handles orthographic pixel sizing (previously inflated
distant light discs); rendering regressions cover both camera projections. Forward/
aft mastheads use 9.3/12.4 m elevations. Common hatch opening samples frames 90-210
to skip the review clip's idle lead-in. These final changes are covered by EditMode
and the final scene smoke check, after the earlier PlayMode/shakedown runs.
Final scene smoke PASS, actual exit 0, no compiler errors:
Logs/VolgoDon507B/part5-runtime-final.log. Verified HUD callbacks, both full-open
holds, anchor release, lights and cross-scene/vessel save restoration. Corrected
night capture visually inspected; git diff --check clean.

## Volgo-Don 507B Part 4, 2026-10-22

Part 4 complete. Do not start Part 5 until requested. See
`Art/Source/VolgoDon507B/PART4_REVIEW.md` for artifacts, references and limitations.
Source/candidate paths unchanged; 49 meshes, 53,130 triangles, 15 independent
mechanism controls with `mechanisms.json` in source and candidate directories.
Original hatch/anchor/windlass geometry separated, props/nozzles rigged; actual
hold openings and approximate interior linings added. Archive p.4 lift/slide/stack
sequence demonstrated, nozzle travel +/-35 degrees. Anchor preview only unseats
the stowed geometry; chain payout and missing jack/hydraulic details deferred.

Registered Blender MCP used throughout, source saved and independently reopened.
`part4_validate.py` PASS: 541 hatch frames, 720 propeller/nozzle configurations,
minimum stacked gap 0.08054 m, deliberate collision regression detected. Export
must disable experimental bake_space_transform for animated children. Unity mesh
attachment regression added after screenshots exposed that export failure.
Final review PASS, exit 0, no compiler errors:
`Logs/VolgoDon507B/part4-candidate-final.log`. Final open-hold/nozzle images reviewed.
All 200 EditMode tests passed in
`TestResults/check-EditMode-20261022-183641-410.xml`.
Original playable integration and physics unchanged; no runtime shakedown for
uninstalled candidate. Source checkpoint: Logs/VolgoDon507B/before-part4.blend.
Current scripts: part4_rig.py (one-time Part 3 conversion), part4_validate.py,
part4_finish.py. Do not apply historical mutation scripts to the current rig.

## Volgo-Don 507B Part 3, 2026-10-22

Part 3 complete. Do not start Part 4 until requested. Current source/candidate
paths are unchanged. `Art/Source/VolgoDon507B/PART3_REVIEW.md` records comparisons,
source decisions, PBR provenance, texture-density limits and reproduction steps.
Backup: `Logs/VolgoDon507B/before-part3.blend`. All Blender work used registered MCP.
An initial connection-reset handshake cleared on the same registered server.

Ray-tested side deck corrected from Z=2.143 to 1.97 m, preserving underwater hull
and keel -3.53 m. Source now has 52,187 triangles, 18 meshes, 12 materials. Replaced
only the coarse propellers/nozzles: four blades, 1.8 m diameter, 1.52/1.47 m pitch,
0.62 expanded-area target; smooth ducts 1.84 m clear throat and 1.66 m length.
Measured blade radius 0.89994 m, radial clearance 0.02006 m. New components are
closed/manifold with no degenerates. Sections, handedness, bearings and stock
positions are estimates, not fabrication-accurate. Retained hull station shapes.

Eight atlas-derived PBR sets add geometry-directed wear, normal, roughness and
R-metallic/A-smoothness maps. Paint is dielectric; steel screws metallic. Bridge
glass is transparent. Source images packed and standalone reopen checked. Low
accommodation atlas density and inherited color/AO remain documented limitations.
No photographic pixels or external texture licenses introduced.

Unity final candidate review PASS, actual exit 0, no compiler errors in
`Logs/VolgoDon507B/part3-final.log`. EditMode 200/200 in
`TestResults/check-EditMode-20261022-074354-836.xml`. Source validation and temporary
oversized-blade/broken-UV regression mutations pass. All 47 original hashes match;
new asset metadata complete. Active prefab, vessel JSON, physics and catalogue
unchanged. No runtime shakedown for the uninstalled visual candidate. No commit.
Next: Part 4 mechanism separation, pivots, hatch travel and motion-clearance checks.

## Volgo-Don 507B Part 2, 2026-10-22

Part 2 complete. Do not start Part 3 until requested. Source and comparison views:
`Art/Source/VolgoDon507B/README.md`. Independently reopened editable source:
`Art/Source/VolgoDon507B/VolgoDon507B.blend`. Candidate export:
`Assets/ShipSimulator/Models/VolgoDon507B_Improved/VolgoDon507B.fbx`.

Corrected inverted atlas V coordinates, preserved original UVs in channel 2,
removed 22 zero-area triangles, retained custom normals and hard edges. Source
has 18 local meshes, nine packed images and 42,299 triangles. Overall length/beam
138.3/16.7 m, hull keel -3.53 m. Dedicated opaque first-pass glazing covers 30
bridge panes; URP materials distinguish paint, decks and propeller metal. Legacy
radar animation disabled to preserve normalized placement. Registered Blender
MCP used throughout. Source validation and Unity candidate review PASS, exit 0:
`Logs/VolgoDon507B/candidate-final.log`. All 200 EditMode tests passed in
`TestResults/check-EditMode-20261022-072713-430.xml`. All 47 original resource
hashes match the baseline. New candidate resources have Unity metadata.

The visual candidate prefab is separate; active prefab, original FBX, physics
and catalogue remain unchanged. No runtime shakedown for this uninstalled visual.
Classic two-covered-hold configuration retained, exact vessel identity unknown.
Hull sections, coarse three-bladed propellers, faceted nozzles, detailed PBR maps
and transparent glazing remain Part 3. Anchoring/mooring islands and covers are
editable within existing meshes; independent mechanisms/pivots remain Part 4.
README and AUDIT document limitations and before/after images. No commit or push.

## Volgo-Don 507B Part 1 Audit, 2026-10-21

The staged work is tracked in `Art/Source/VolgoDon507B/IMPROVEMENT_CHECKLIST.md`.
Part 1 is complete; do not start Part 2 until requested. See `AUDIT.md`,
`MODEL_PROVENANCE.md`, geometry JSON, original-asset checksums and eight previews.
The classic two-covered-hold configuration matches the existing model; exact
vessel identity is unknown. The archive mixes 1565/507B values. Its newly checked
propeller/nozzle/thruster data is documented in VolgoDon507B_Sources.md, with no
JSON parameter changes. Model has 42,321 visible triangles and 15 degenerate faces.
Clay/textured comparison indicates major UV/texture-dependent superstructure
patchwork, so do not assume a global normal recalculation will fix it.
Existing physics includes independent engines and approximate loading; nozzle
forces, tank state and hatch mechanisms need later work. Original assets/code
unchanged. Runtime tests not run for this documentation-only audit. Blender MCP
worked despite addon protocol 13/server expectation 14; isolated audit scene
created and the original unsaved scene preserved. No production blend/FBX yet.


Last updated: 2026-09-20

## Release Workflow Failure and Build Warnings, 2026-09-20

The v0.1.1 release run failed while packaging: `zip` could not create the archive because
`game-ci/unity-builder` runs Unity in a container as root, so `build/` came back owned by root.
The workflow now passes the runner's `uid:gid` as `chownFilesTo`, which makes the builder hand
the build path and the project path back to the runner account.

Actions bumped off the deprecated Node 20 runtime: `actions/checkout@v5`,
`game-ci/unity-builder@v5` and `softprops/action-gh-release@v3`, all drop-in for the inputs used
here. A `Library` cache (`actions/cache@v5`) answers the builder's own first-build warning.

Build warnings cleared in the project itself:

- The obsolete `FindFirstObjectByType` and `FindObjectsSortMode` overloads were replaced with
  `FindAnyObjectByType` and the sort-mode-free `FindObjectsByType` overloads in `ShipTelemetryUI`,
  `DayNightController`, `ShipSimulatorVisualUpgrade` and `VesselDataValidationTests`.
- `RiverCloudShadow` now has a single exit point. Its early return for the disabled case made the
  GLSL translator report the result as potentially uninitialized in the `CloudCookie` kernel, which
  only shows up on the Linux build agent.
- Sixty-eight generated mesh assets carried a main object name that differed from their file name,
  which the importer warns about on every build. `RiverLandscapeBuilder.SaveMesh` and
  `ProceduralVesselBuilder.SaveAsset` now name the object after the file, and the committed assets
  were corrected in place, so no geometry changed.
- "Pre-baked collision is disabled on 2 mesh(es)" is left as it is. The two meshes are the
  `RiverTrainingScene` banks, the only `MeshCollider`s in any scene, and in that scene they are the
  only thing that stops the ship at the bank: it has no bathymetry or grounding controller, unlike
  Gorodets, which uses those and no mesh collision at all. There is no supported way to silence it
  for these two. Pre-baked collision is a model importer setting and a generated `.asset` mesh has no
  importer; `Physics.BakeMesh` in the editor was measured to write nothing into the asset, and the
  `MeshUtility.SetPreBakeCollisionMesh` binding is not reachable, even by reflection. Unity 6.6 still
  cooks the data at build time, so the player is unaffected today. When a future editor stops doing
  that, the answer is either to give the river scene the Gorodets treatment (bathymetry plus
  grounding, no bank mesh colliders, 57600 triangles of collision geometry gone) or to cook at load
  with a background `Physics.BakeMesh` call.

Verified: compile clean (exit 0, no `error [A-Z]`, no `warning CS`), EditMode 197/197,
PlayMode 40/40. The GL shader warning and the workflow changes can only be confirmed by the next
tagged release run.

## Serpukhov Mooring Collisions and Radar, 2026-09-20

The scene builder now retains solid box colliders on moored hulls, superstructures,
floating docks and pier parts. These are fixed obstacles with estimated geometry matching
the existing visuals. Each of the 66 moorings has one `RadarObstacle` on its main hull or
deck, rendered as a pale purple footprint using the collider dimensions and relative heading.
The radar mask clips partial footprints at the viewport edge without hiding an entire hull
when its centre goes off screen. The legend identifies craft and piers.

Rebuilt the scene using the existing working-tree scenario JSON without changing that file.
Verified: EditMode 197/197 and PlayMode 40/40, including side and end impacts at 25 m/s
against every generated mooring's collision dimensions, plus head-up radar projection tests.
Inspected the HUD capture at `Logs/Serpukhov/radar-moorings.png`.
The full fleet shakedown also passed for all five playable vessels, with no compiler errors.

## Serpukhov Zaton: Start Inside the Basin and a Yacht Marina, 2026-09-20

The passage now runs outbound and begins where the interesting water is. It starts at the head of
the basin among the laid-up fleet, runs the length of the harbour past the barges, the passenger
berth and the yacht marina, out through the 55 m gate, down the Nara and into the Oka: 2416 m, the
old route reversed with the basin end extended to the far corner. Starting on the Oka put every
traced hull, the town and the marina 2.4 km along a passage nobody had reason to finish, which is
why none of it had been seen.

Reversing the direction is not just reversing the array. Russian inland buoyage is read going
downstream, so the red right-edge marks moved from the route's port side to its starboard; the
leading line for the mouth is now a stern transit and its active chainage moved with it; the axial
marks, the buoy run and all six mission phases were re-chained to the new distances, and the mission
wording is a departure rather than an arrival.

The marina came out of the satellite crop: five pontoon fingers with boats in the slips. Only the
span of bank it occupies is traced. The fingers are then laid out along the outward normal of the
shoreline itself, which keeps them square to the water however the outline is re-extracted and
avoids pretending to know where sixty individual boats lie. Forty five of the sixty six floating
objects in the scene are marina berths and fingers.

Two things the generator taught us. Walking a point out from the bank must follow the negative
gradient of the shore distance; following the positive one walks inland forever, and the first run
hung until it was killed. And the building catchment was keyed only to the fairway, so re-routing the
passage silently dropped a third of the town; it is keyed to the shorelines as well now.

Verified: EditMode 193 passed, PlayMode 39 passed, generator rerun end to end, scene rebuilt from the
menu method and stills in Logs/Serpukhov/ looked at. The lay-up berth view now opens among rusty
barges with the marina beyond, which is what was asked for.

## Grounding Contact Force, Foil Draft and a Shader Warning, 2026-09-20

Three reports from playing the Serpukhov scene, all uncommitted like the rest.

Aground with no way off. The real fault was the contact force, not the friction. The penalty spring
answered a metre of penetration with tens of meganewtons, so the holding force ran to megatonnes and
nothing moved; on a hydrofoil driven well onto a bar it also launched the hull two metres clear of
the water. `GroundingController` now caps the per-point normal force at three times the weight share,
which is what the bottom can actually hold her up with. Measured afterwards, the Luch at 0.60 m
against her 0.66 m draft is held by 12 kN and gets off on 13 kN astern; at 0.50 m it is 28 kN and at
0.35 m 50 kN, and she stays. The scene's depth beyond the marked channel went from 0.35 m to 0.6 m on
those numbers, so the edge of the channel is recoverable and a proper grounding is not.

Correction worth recording: the first diagnosis was that the friction regularisation acted as a huge
damper. It does not. `-mu N * v / max(|v|, 0.05)` is a vector expression whose magnitude is exactly
mu N above 5 cm/s and tapers below it, which is right. That change was reverted after measurement;
the only physics change is the normal-force cap. `HoldingForceN` is new and now shows on the HUD next
to the grounding warning, because the number that tells you whether astern can work you off belongs
on the bridge.

Foil draft. `VoyagePassage` compared `loadedDraftM`, so the Meteor was admitted on her 1.15 m hull
while her foils reach 2.35 m and touched the bottom in the 2.4 m channel. `VoyagePassage.DeepestDraftM`
adds the support appendage, so the passage now admits the Luch alone. This is very likely what the
report of "cannot move at all" actually was: a Meteor aground on her foils at the start line, held by
an uncapped contact force, which also explains never seeing the basin, the barges or the town.

Shader. `RiverReflectionFilter.compute` compared and divided the unsigned dispatch id by an int
`_Size`, warning about a signed/unsigned mismatch on dx12. It casts once to uint now.

Verified: EditMode 193 passed, PlayMode 39 passed, no shader warning in the play-mode log. The
ejection regression is covered by `Grounding_DoesNotThrowTheHullClearOfTheWater`, which was confirmed
to fail without the cap; `Grounding_AfterALightTouch_AsternThrustWorksHerOff` and
`Grounding_WellUpOnTheShoal_HoldsHerAgainstFullAstern` pin the two sides of the measured table.

## Serpukhov Zaton: Real Terrain, Land Cover and Traced Moorings, 2026-09-19

Second pass on the Serpukhov scene, still uncommitted. The point was to make the way in legible from
the water and to put the laid-up fleet where it actually lies.

Moored craft. Fetched Esri World Imagery tiles over the basin at zoom 18, cropped and gridded them,
and traced 21 hulls by eye: bow and stern pixels plus a beam, converted at 0.343 m per pixel. Two
cargo barges of about 70 m on the north bank, laid-up vessels of 30 to 45 m on the north-east shore,
a floating dock of about 62 by 19 m, small craft, two mooring piers and a hulk on the west bank. All
21 landed inside the OpenStreetMap water outline without nudging, which is a useful cross-check that
the outline and the imagery agree. The earlier four invented "laid-up craft" were on the wrong side
of the basin entirely and are gone. The trace table lives in the generator as TRACED_MOORINGS.

Terrain. Elevation now comes from AWS terrarium terrain tiles at zoom 14, sampled onto a 25 m grid
(149 x 193) and levelled to the river surface, which the tiles put at 107.1 m, matching the 107 m
Wikipedia gives for the Nara mouth. A 30 m source smears the waterline, so the first 70 m of bank is
still carried by the shoreline and blended into the terrain behind it; beyond that the real valley
shows, up to about 58 m above the water at the edge of the frame.

Land cover and town. 46 OpenStreetMap landuse/natural polygons and 158 building footprints within
650 m of the fairway, each reduced to its minimum-area box with a height from building:levels or by
type. Planting now follows the cover, so the wooded peninsula reads as a wood, the fields stay open
and the port yard stays clear. Walls take one of four muted colours picked from the position: the
first attempt was a uniform white and read as a test scene.

The geometry file is now 392 KB of derived data, so the generator is committed as
Tools/serpukhov_zaton_geometry.py rather than left in a scratchpad. It caches its downloads under
Tools/.cache (gitignored) and refuses to write a file whose fairway leaves the charted water. Running
it reproduced the hand-built file to within 0.1 m on positions and 0.03 m on elevation, and the
committed data is now its output.

One trap: the generator's own shore test first used the naive minimum over every polygon edge, which
is exactly the sill bug the C# side already fixed, and it rejected a fairway sample that was fine. It
now mirrors ScenarioGeometry.BoundaryEdges.

Verified: batch compile clean, EditMode 192 passed, PlayMode 35 passed, scene rebuilt from the menu
method in batch, generator rerun end to end, and stills in Logs/Serpukhov/ looked at. Not verified:
frame cost, which is now the main open question at about 4600 vegetation instances plus the town and
the moored craft, and handling under manual control.

## Serpukhov Zaton Scenario, 2026-09-19

Added a third voyage scene, `SerpukhovZatonScene`: the Oka approach, the mouth of the Nara and the
Serpukhov lay-up and repair basin, 2.42 km of fairway. The work is in the tree, uncommitted.

Geography is data, not code. `Assets/ShipSimulator/Data/Scenarios/SerpukhovZaton.json` holds the
shorelines (OpenStreetMap water outlines, ODbL, extracted 2026-09-19: relations 2174292 and 2174294
and the Oka bank envelope from way 543276172), the fairway centreline with widths, depths and speed
limits, the landmarks, the current regions and the shoals, in local metres with the frame recorded in
the file. `ScenarioGeometry` (editor) loads it and answers signed shore distance;
`SerpukhovZatonBuilder` builds the scene from it.

What is published and what is invented is written down in
`Assets/ShipSimulator/Documentation/SerpukhovZatonScenario_Sources.md`. Published: the Nara is 2 km of
third-category waterway, guaranteed 1.00 m deep and 20 m wide with a 100 m bend radius, marks working
20 April to 31 October (Rosmorrechflot order ZD-496-r, appendix 2, row 1420); the Oka past Serpukhov
is also third category, 1.00 m and 30 m (row 1381). Estimated: every depth in the scene, the
currents, the shoals, the speed limits and the positions of the marks. No public survey or buoy
scheme of that reach exists.

Third category means unlit, so the scene carries no night beacons. `DayNightController` lights any
child of a root named `Navigation` whose name contains `Buoy`, so the marks here are named `Unlit ...`
and that method now skips them. Edge buoys follow the downstream convention: the route runs up-river,
so the red right-edge marks stand on its left.

Vessel restriction. `VoyagePassage` (runtime, `Scripts/UI/`) is now the single list of passages, with
per-passage limits; `VoyageSave.IsVoyageScene` and the menu's scenario naming both read it. The zaton
allows 40 m length, 10 m beam and 1.6 m draft, so only the Meteor 342U and the Luch 14352 can take it;
the menu greys the passage out for the cargo ships and says why. `VoyageMenu.BindScene` also clamps
the selection, so opening the scene directly cannot put a 507B in the basin. Honest gap: at the
published 1.00 m guarantee the Meteor would have no clearance either, so the scene models an ordinary
navigation-season level instead.

Shared code touched, all small: `GorodetsScenarioController` gained optional phase labels and
instructions (`ConfigurePhases`) so a second scenario can reuse it without showing Gorodets wording,
and the HUD reads `PhaseLabel`; `ScenarioBathymetry.Configure` gained an outside-the-channel depth
(0.35 m here, so leaving 20 m of marked water grounds you); several `RiverLandscapeBuilder` helpers
became `internal` for reuse.

Two traps worth remembering. Distance-to-shore taken as the minimum over every polygon edge raises a
sill wherever two water bodies meet, which put a bar across the basin entrance and the Nara mouth;
`ScenarioGeometry.BoundaryEdges` now drops edges whose far side is also water. And the Nara polygon
ends on a straight line across its mouth, so the generator pushes that end 70 m down-river to overlap
the Oka rather than butt against it.

Verified: batch compile clean, EditMode 189 passed, PlayMode 34 passed, scene built from the menu
method in batch, and stills rendered to `Logs/Serpukhov/` by
`Ship Simulator > Render Serpukhov Zaton Preview` and looked at. Not verified: how it handles under
manual control, and frame cost. Shore structures are placeholder boxes, and the scene is outside
`Upgrade Water And Landscape`, `Apply Realistic Water And Sky` and `Apply Graphics Phase One`.

## Vessel Shakedown and Ride Height, 2026-09-19

Continued the uncommitted vessel presentation work without committing or discarding it.
`SupportModel.Apply` now distributes nominal lift over four immersion-sensitive patches,
with vertical point-velocity damping. Dry patches produce no force; support no longer remains
constant as the craft rises or pitches. Estimated support centres match loaded longitudinal
CG for neutral trim. Physics changes remain Rigidbody forces only.

`EffectiveDraftM` and grounding share fixed foil/skeg contact geometry, including when stopped,
instead of flooring the displayed draft at a speed-dependent constant. The inferred Meteor
appendage gives about 2.27 m afloat (published 2.35 m), and 1.2 m supported at full load;
this is an explicit geometry estimate, not a validated foil/cushion solver.

Preserved the pending displacement/immersion wake scaling, model-fitted exterior camera views,
service-speed readout, support-state indicator, non-solid buoy detection colliders, HUD rebuild
protection for the sky capture rig, and external time-scale readout refresh. Finished single-engine
control help for Luch and bounded straight-ahead radar prediction to its actual viewport.
The shakedown snaps each capture to its selected camera and checks physical hull rise as well as
heel, wake amplitude and clearance. Interactive menu execution returns to the editor, not exit.

Further visual inspection caught a low-roof obstruction in the generated navigator eye and
world-space camera smoothing pulling that eye back through the windscreen at speed. Generated
eyes now sit just outside the front glazing at window height, and navigator position follows
the rendered hull without lag. The bridge overview aims at the model's forward lookout point.
Radar draft and ahead-channel clearance use `EffectiveDraftM`; engine RPM/load text has enough
vertical space for its second line. Prefabs were regenerated; unrelated material churn removed.

Verification: PlayMode 34/34 (`TestResults/Shakedown-PlayMode.xml`), including takeoff, settled
ride height, roll recovery, return to displacement draft, dry-foil force, foil bottom contact,
live radar draft and the moving navigator camera. EditMode 183/183 (`TestResults/Shakedown-EditMode.xml`), including bridge-view direction,
window/roof obstruction, wake scaling and the high-speed radar horizon.
The complete five-vessel run passed (`Logs/shakedown-verified.log`, `VESSEL_SHAKEDOWN|PASS`).
Its first autopilot version followed the local channel tangent into the outside bank; contact
logging identified the real natural-bank mesh. Looking ahead through the bend fixes that test
harness error without changing game collisions. The check uses a fixed 1/60 s capture step,
reports all vessels on failure, and provides `RunFastCrafts` for a focused passenger-craft run.

Captured fast-craft speeds were about 46 km/h for Meteor and 25 km/h for Luch, near completed
takeoff rather than service speed. Estimated wake crests were 0.10/0.15 m; bow waves 0.32/0.49 m,
heel about 0.1 degrees and hull rise 1.04/0.21 m. Images are under `Logs/Shakedown/`.
The 65 km/h radar horizon is covered numerically, not by these underway captures. No manual
half-hour pilot session has been performed; resistance-hump feel remains a human check.

## Graphics Phase 2, Steps 0 and 1, 2026-09-13

Target: RTX 3060 at 1920x1080, selected by the user. Step 0 baseline and Step 1 water optics are
implemented. Height fog, flow-map authoring and the Crest decision remain for later steps.

`GraphicsPhaseTwoCheck` preserves separate `before` and `after` captures in
`Logs/GraphicsPhaseTwo/`. Each has 25 poses/conditions, 7,500 measured frames, and temporal
crops; CPU/GPU/editor intervals are distinguished. The before set was captured before optics
edits. The tests run in a dedicated editor; they do not modify the user's saves.

Water now refracts scene colour with foreground-depth rejection, composes RGB Beer-Lambert
transmission and green-brown scattering using estimated Secchi depth (default 1.2 m), and
reduces visibility slightly in rain. Planar mips use separable Gaussian filtering with a
roughness-dependent width; night sampling stretches light reflections vertically. Normal
variance broadens specular highlights without removing the sun path. The contact alpha fade
remains; submerged colour is composed in the shader rather than alpha blended over the bed.

The existing sky capture was valid, but the water's per-renderer environment lookup returned
black. `RiverLighting` explicitly binds that HDR cubemap to water; the edge fade uses it.
The `RunSkyProbe` capture shows the cloud sky on water with planar rendering disabled.
The material setup lives in `RiverWaterAndSkyBuilder.ConfigureOptics` / `ApplyOpticsBoth`.
No renderer migration or physical current changes were made.

Comparison: clear toward-sun temporal variance -16%, eight of ten crops improved; dawn toward
sun +0.7%, rain toward sun +10.2%. See `temporal-comparison.csv`. CPU median delta across cases
-0.116 ms (largest increase +0.091 ms), RTX 4090/D3D12. GPU timing unavailable, so the RTX 3060
+1 ms acceptance gate is still open. Concurrent vessel-catalogue edits landed between runs;
these are whole-scene comparisons, not isolated shader-cost measurements. Fine normal motion
and exposure still affect the temporal metric. Optical coefficients are visual estimates.

Verification: before, after and sky-probe captures passed on 2026-09-13; `WaterWeatherCheck`
passed with water fog detail contrast 0.0059 clear / 0.0001 fog. `ShipWakeRuntimeCheck` passed
straight and turning captures with 48 wake points. The original suites passed 145 EditMode and
21 PlayMode tests. On 2026-09-19, the current checkout passed 169 EditMode and 23 PlayMode tests
(`TestResults/phase-two-final-{EditMode,PlayMode}.xml`), with zero compile/runtime errors in
those logs. The final clear-water temporal comparison assertion also passed against the
preserved before/after crops (`Logs/phase-two-comparison-final.log`). The historical screenshots
were not regenerated for the newer vessel catalogue; the optics shader is unchanged since
those successful captures. RTX 3060 GPU timing and the rain-view variance remain open limits.

## Passenger Fast Craft: Meteor 342U and Luch 14352, 2026-09-19

Two fast passenger craft are playable, which needed a support model rather than only new data.

- **Meteor 342U** (`Meteor342U.json`): hydrofoil, over 400 of the family built 1961 to 1999.
  Published: 34.6 m hull, 9.5 m over the foils, 2.35 m afloat and about 1.2 m foilborne, 36.4 t light
  and 53.4 t full, 114 to 123 passengers, 2 x M-400 of 1000 hp at 1700 rpm, two five-bladed 0.71 m
  propellers, 65 km/h service speed.
- **Luch 14352** (`Luch14352.json`): skeg air cushion craft, over 80 of the type built 1983 to 1999.
  Published: 23.72 m, 4.53 m beam, 0.66 m displacement draft, 0.45 m skeg depth, 57 (+15) passengers,
  one 382 kW 12ChN15/18, 40 km/h. Displacement 23 t is estimated from project 14351's 21.2 t.
- `SupportModel` plus the optional `support` JSON section: a speed-dependent fraction of the weight
  moves to the cushion or foils. It applies lift at the support position (so draft follows from the
  existing station hydrostatics), scales hull forces, squat and bank suction by the remaining
  immersion, multiplies resistance by a hump-then-drop factor, takes lift-fan power off the delivered
  power, and reports the foils or skegs as the deepest point for clearance.
- Bug found by the trials: `ResistanceModel.LackenbySpeedLoss` kept growing past the critical depth
  Froude number, where the regression does not apply. A hydrofoil at 65 km/h in 4.6 m of water (depth
  Froude 2.7) was held below takeoff at 18.6 kn. The wave term now fades out above critical; the
  craft reaches 34.3 kn there, and the displacement ships are unchanged. Regression test:
  `LackenbySpeedLoss_FadesAboveTheCriticalDepthFroudeNumber`.
- Validator: hull coefficient ranges relaxed for fine fast-craft hulls, plus support validation.
- Models: `ProceduralVesselBuilder` gained V-sections, a passenger cabin with window bands and
  tapered nose, a wheelhouse, hydrofoils with struts and end plates, skegs with the cushion tunnel,
  and a bow boarding ramp. Previews now also render a dry view with the water plane hidden, which is
  the only way to check foils, skegs, propellers and rudders.

Calibrated to published speeds: Meteor 34.4 kn (65 km/h) deep and 34.3 kn in the Gorodets reach, Luch
21.1 kn (39 km/h). Both turn and stop inside 6.2 ship lengths. Numbers for the estimated parameters
are in `Meteor342U_Sources.md` and `Luch14352_Sources.md`.

Verified: vessel catalogue build `VESSEL_CATALOGUE|PASS` (5 vessels); EditMode 169 passed; PlayMode
23 passed; `SEA_TRIALS|PASS`; `MENU_SMOKE|PASS`.

## Volgo-Balt and Volgoneft Vessels, Vessel Selection, 2026-09-13

Two more vessels are playable, chosen in the start menu (**New voyage**, then vessel, then passage).

- **Volgo-Balt 2-95A/R** (`VolgoBalt295AR.json`): the largest Volgo-Balt series (79 built at Komárno,
  1973 to 1984). Published: 113.87 m, design length 110.52 m, 13.0 m moulded beam, 5.5 m depth, 3.86 m
  draft, 4761 t, 3474 t deadweight, 2 x 515 kW Skoda 6-27.5 A2L, 10 kn.
- **Volgoneft 1577** (`Volgoneft1577.json`): with its Bulgarian twin 550A the largest Volgoneft group
  (about 70 + 65 built). Published: 132.6 m, design length 128.6 m, 16.5 m moulded beam, 5.5 m depth,
  3.515 m draft, 6477 t, 4803 t deadweight, 2 x 736 kW SKL 8NVD48A-2U with reduction gears, 20 km/h.
- Physics uses the existing model. Clarke and Söding coefficients are computed per hull; the residual
  resistance coefficient and rated propeller speed are calibrated together to the published speed
  (script in the session scratchpad, reproducing the C# formulas; it gives 257 rpm for the 507B). Other
  values are estimated and listed in `VolgoBalt295AR_Sources.md` and `Volgoneft1577_Sources.md`. No bow
  thruster was found for either type.
- Models: no usable existing models were found, so `ProceduralVesselBuilder` (with `ProceduralMesh`)
  generates them from each vessel's data: hull with spoon bow and stern cut up over the propellers,
  forecastle, poop, aft house, wheelhouse with wings, funnel, masts, railings, rudders and propellers,
  plus four hatch covers (Volgo-Balt) or walkway, pipelines, manifold, hose cranes, tank hatches and
  vents (Volgoneft). Superstructure proportions and liveries are estimated. `Ship Simulator > Build
  Vessel Catalogue` (batch `VesselCatalogueBuilder.Run`) regenerates meshes, materials, prefabs, the
  catalogue and preview renders in `Logs/Vessels/`.
- Selection: `VesselCatalogue` (Resources asset), `VesselSelection` (session only), `VesselSwap`
  (replaces the scene's 507B before `Start`, retargets camera, HUD, grounding and mission),
  `VoyageSave.vesselId` (empty in older saves means the 507B). A different vessel for the open scene
  reloads it.
- `VesselLayout` per prefab holds navigation light positions, navigator eye and camera scale;
  `NavigationLightRig` and `ShipFollowCamera` read it. The HUD rudder scale uses the vessel's maximum
  rudder angle. The 507B prefab got a layout with its previous values.

Virtual sea trials, loaded, deep water: Volgo-Balt 9.9 kn full ahead, advance 3.13 L, tactical
diameter 3.52 L, 10/10 overshoots 4.1/4.9 deg, crash stop 4.54 L; Volgoneft 10.7 kn, 3.31 L, 3.76 L,
3.9/4.5 deg, 4.99 L. In the 4.6 m Gorodets reach the Volgo-Balt's tactical diameter grows to 8.75 L,
because the depth is only 1.19 times its draft.

Verified: vessel catalogue build `VESSEL_CATALOGUE|PASS`; EditMode 141 passed; PlayMode 21 passed
(including the swap test and each vessel floating level at design draft); `SEA_TRIALS|PASS`;
`MENU_SMOKE|PASS` with a Gorodets start in the Volgoneft and a load that restores the 507B.

Other work in the tree at the same time (graphics phase 2 files: water shader, reflection filter,
`GraphicsPhaseTwoCheck`) was not touched by this change.

## Buoy Visuals and Lantern Flashes, 2026-09-13

`DayNightController` now creates an idempotent `BuoyVisualRig` for each existing buoy.
The runtime rig replaces the old cylinder/sphere renderers with a shaped steel float,
a dark waterline belt, weathered paint, a braced tower and ribbed lantern housing.
It combines the body into three material meshes. Small visual heave and roll move the
body and lantern together; existing buoy positions and collision geometry stay fixed.
This path also applies after scene builders regenerate the original buoy layout.

Buoy lights reuse the fog-aware all-round navigation lens with a surface offset so the
opaque lantern glass does not hide the flash. The physical housing stays visible when the
light is off. Local point-light intensity/range are reduced to 1.8/12 m, while the emissive
lens remains readable at distance. Ship lights keep their default zero surface offset.

Flashes repeat every 2.5 s with a 0.35 s light interval and stable position-based phase.
Night activation evaluates the current phase immediately. Timing follows simulation time,
including pause and acceleration. Existing red-right/white-left paint and red/green light
assignments are preserved. These are estimated prototype characteristics, not charted
Gorodets timings; no claim of local navigation certification is made. The reference for
clear light/dark intervals is [IALA's flashing-light definition](https://www.iala.int/wiki/dictionary/index.php/Flashing_Light).

Verification: 118 EditMode tests passed (`TestResults/buoy-editmode.xml`), including repeatable
flash timing, daylight shutdown, idempotent visual creation and preserved colliders.
`BuoyGraphicsCheck.Run` captures daylight, nearby flash on/off and distant flash on/off to
`Logs/Buoys/`, with runtime errors treated as failure. Run it using the same dedicated,
rendering-enabled batch command as the other graphics checks.

## Literature-Based Ship Dynamics, 2026-09-13

Phases 1 to 6 of `ShipDynamicsRealismApproach.md` are implemented; phase 7 (ship-ship
interaction, locks, mooring, anchors) is not. `ShipSimulator_Physics.md` describes the model.

- `PropulsionController`, `RudderController`, `HydrodynamicResistance` and `BuoyancyPoint` were
  deleted. The vessel prefab was cleaned of their components, the `PropulsionPoint` object and the
  `BuoyancyPoints` hierarchy; `ShipSimulatorPrototypeBuilder` no longer creates them.
- New plain C# models under `Scripts/Physics/`: `VesselParameters`, `HullDerivativeEstimate`,
  `ManoeuvringModel` (3-DOF MMG with the full added-mass matrix), `HullForceModel` (cross-flow drag
  blend, current shear), `ResistanceModel` (ITTC-1957, Lackenby), `EngineShaft`, `PropellerModel`,
  `RudderModel`, `WindLoadModel` (Blendermann), `RestrictedWaterModel` (depth factors, ICORELS
  squat with blockage, estimated bank suction), `HydrostaticsModel` (station prisms),
  `ManoeuvringSimulator` and `ManoeuvringTrials`.
- `ShipPhysicsController` applies the MMG accelerations with `ForceMode.Acceleration`, station
  buoyancy and heel moments, and steps `GroundingController` (keel springs plus bottom friction
  through the added-mass solve). `ManualStepping` and `SetDepthProvider` support tests.
- The vessel JSON schema was rewritten (`VolgoDon507B.json`), and `KVLCC2_MMG_Benchmark.json`
  holds the published KVLCC2 MMG set for verification. Most 507B values are estimated; see
  `VolgoDon507B_Sources.md`. The old JSON had roll and pitch inertia swapped on Unity's axes.
- Twin engines are independent: HUD `Q`/`Z` port and `E`/`X` starboard telegraphs, `W`/`S` both;
  the HUD shows real shaft RPM and load per engine, and the radar prediction uses the actual rate
  of turn. `VoyageSave` stores `engineCommands` and `shaftRps` (older saves fall back to the shared
  throttle). `WeatherController` now pushes its gusting wind to the vessel.
- Reversing a running engine cuts fuel at once, brakes the shaft and restarts astern after a delay
  (regression test `EngineReversal_BrakesBeforeDrivingAstern`).
- `ShipWakeRuntimeCheck` waited for 4 m/s through the water. The new model gathers way slowly and
  settles near 3.5 m/s at full ahead in the 4.6 m Gorodets reach, so the unsteered ship sailed
  straight past the starboard bend at z 260 to 470, grounded on the shoal inside it at (42, 540),
  and the check still passed. The straight capture now fires at 3 m/s or after 380 m, before the
  bend, and the check fails if the vessel is not under way or is aground at a capture. `VoyageMenuSmokeCheck` clears the per-engine save arrays so it keeps
  testing the shared-throttle fallback.

Virtual sea trials (`Logs/SeaTrials/sea-trials.md`, `SEA_TRIALS|PASS`), all simulated with
estimated coefficients: 507B loaded deep water slow/half/full 3.0/6.3/9.8 kn, advance 3.22 L,
tactical diameter 3.70 L, 10/10 overshoots 3.6/4.2 deg, 20/20 8.2 deg, crash stop 4.48 L in 263 s.
Gorodets 4.6 m: full ahead 6.9 kn, tactical diameter 6.59 L, bow squat 0.29 m. KVLCC2 benchmark:
15.4 kn, advance 3.00 L, tactical diameter 3.09 L, 10/10 overshoots 5.7/10.3 deg.

Bow thruster (added on request; the owner reports 507B vessels carry one): `BowThrusterModel` gives
actuator disc bollard thrust with an estimated figure of merit, a 5 s ramp, an estimated loss of
effect with ship speed and zero thrust with the tunnel out of the water (lightship). 507B values
(160 kW, 1.0 m tunnel, 60 m forward) are estimated; about 21 kN bollard thrust. HUD `J`/`L` step
it 50 % to port or starboard, `K` stops it; its state is saved. From rest it turns the loaded ship
at 19.3 deg/min after 180 s in deep water and 9.2 deg/min in the Gorodets reach.

Verified: compile clean (`Logs/dyn-compile.log`); EditMode 116 passed
(`TestResults/EditMode.xml`); PlayMode 17 passed (`TestResults/PlayMode.xml`);
`VoyageMenuSmokeCheck` `MENU_SMOKE|PASS`; `ShipWakeRuntimeCheck` `WAKE_RUNTIME|PASS` with the ship
under way and clear of the bottom at both captures. A `MissingReferenceException` from
`RiverLighting.CaptureSky` during scene switches in the menu smoke log predates this work.

Known limits: quadratic, not four-quadrant, propeller curves; estimated bank model (the Lataire
papers were not accessible); B/T <= 4 depth factor branch used for all hulls; wall-sided
hydrostatics; Unity collider contacts bypass the added-mass solve. Calibration against 507B trial
data is the next step and needs real documentation.

## Water Weather, Bank Wake and Running Lights, 2026-09-13

- Water fog was missing because shared HLSL contained the fog variant pragmas but was loaded
  with a normal include. Both water passes now use `include_with_pragmas`; surface fog uses
  URP's per-fragment eye depth, including reflections, foam and highlights.
- Rain particles now use explicit alpha blending and soft impact textures. Small water impacts
  are emitted only inside the bank waterlines and below unobstructed air; rain also adds expanding
  normal-map rings and increases surface roughness. Impact rings remain normal-only under TAA.
- `RiverShoreProfile` samples the actual zero-height crossings of the generated natural-bank
  meshes in each scene. Wake crests flatten and dissipate in shallow water, with breaking foam
  and a weak mirrored incident wave near the bank. Current and previous water displacement use
  the same bank response, preserving motion-vector consistency. The exaggerated normal gain
  was reduced from 1.9 to 1.15.
- Ship running lights are steady, with red port and green starboard 112.5-degree sectors,
  two forward-facing white 225-degree mastheads, and a white 135-degree stern light. The aft
  masthead is 4 m higher than the forward one; the extra all-round white bow light was removed.
  Point-light cookies and emissive lenses share these sectors. The lens keeps a small minimum
  pixel size to reduce aliasing and handles flipped render-texture projections. Its shader lives
  in Resources so player builds include it. Buoy flashers remain separate.

Reference pattern: [UNECE CEVNI Article 3.08](https://wiki.unece.org/spaces/TransportSustainableCEVNIv5/pages/25265334/Article%2B3.08%2B-%2BMarking%2Bfor%2Bmotorized%2Bvessels%2Bproceeding%2Balone),
with light sectors as defined in [navigation Rule 21](https://www.navcen.uscg.gov/navigation-rules-amalgamated).
Fixture positions are estimated, not a verified Project 507B electrical plan or certification
against local navigation rules. This is an underway pattern; anchor, towing, special-status and
signal-light modes are not implemented. A stopped engine does not change the running pattern.

The bank effect is an analytic visual approximation, not a shallow-water solver: it does not
model propagated wave energy after a stopped track, full refraction, overtopping or reflected
wave travel around bends. Its near-bank depth slope and reflection strength are estimated;
physical bathymetry and vessel forces are unchanged. Missing natural-bank meshes disable the
shore response and water impact emission. Rain impact rejection uses collision geometry for
hulls and structures; it does not create rain impacts on decks or land.

Verified: 76 EditMode tests and 10 PlayMode tests passed in
`TestResults/water-weather-{editmode,playmode}.xml`. `WaterWeatherCheck.Run` checks dense-fog
attenuation, live rain impacts, and steady night light state over 400 frames, and captures both
weather and four viewing directions in `Logs/WaterWeather/`. The bank-wake capture uses a
synthetic wake fixture, leaving vessel physics frozen. Fog detail contrast dropped from 0.0068
to about 0.0001. It also rejects oversized night lenses, a flipped-projection bug caught during
visual inspection. `GraphicsPhaseOneCheck.Run` also passed (`Logs/water-weather-phase-one.log`):
water motion reached 90.8% of open-water pixels with a static camera, the cloud cookie and sky
ambient passed, and rainy-night sun intensity remained 0.127. Run graphics checks with rendering
enabled, in a dedicated batch editor.

## Wind-Driven Water and Vegetation, 2026-09-13

A stationary scene no longer looks frozen. `WindGustModel` varies the configured wind for
visuals only: speed gusts with occasional bursts, a slow direction meander and a shift of up to
20 degrees roughly every two minutes, eased over 25 s, with a 0.8 m/s light-air floor.
`WeatherController` publishes the gusting wind, its accumulated travel and the previous frame's
values; ripples, clouds and cloud shadows move by travel, so gusts change speed without jumps.
The vessel's wind force still uses the configured mean wind.

- Water: short wind waves (2.3 m and 5.1 m) on six fixed headings weighted toward the wind,
  moving at deep-water phase speed, with drifting wave groups; gust patches (cat's paws) roughen
  the surface. Both are normal-only, so water motion vectors are unchanged.
- Vegetation: `RiverVegetationWind.hlsl` bends whole plants downwind with height, rocks them on
  individual timing and flutters leaves, driven by `RiverGust`, the same gust field as the water.
  Plants mostly stand in lulls and bend when a gust passes. `RiverBark.shader` gives branches the
  same motion (with shadow, depth and motion vector passes); the bark materials were switched from
  URP Lit and their inherited disabled motion pass removed.

Amplitudes are estimated visual values. Verified: EditMode 64 passed (`TestResults/wind-editmode.xml`),
PlayMode 10 passed (`TestResults/wind-playmode.xml`); `GraphicsPhaseOneCheck.Run` passed with a new
still-water stage (mean image change 0.0245 over 80 frames with vessel and camera still) and motion
vectors on 94.7 % of open water and 23 % of the tree line (`Logs/wind-check.log`).

## Graphics Roadmap Phase 1 and Review Fixes, 2026-09-13

Phase 1 of `GraphicsRealismApproach.md` (commit f959559) added `RiverLighting` (sky ambient,
live sky reflection probe, histogram auto exposure through `RiverExposureFeature`), cloud
shadows, temporal AA with foliage and water motion vectors (`RiverTemporalFeature`), LOD
cross-fade on vegetation, and removed the saturation boost. Menu: `Apply Graphics Phase One`.

A review of that commit found six problems, all now fixed:

- **Post-processing never reached the screen.** `ConfigurePostProcessing` added volume components
  without making them sub-assets, so `RiverVisualProfile.asset` reloaded with five null entries:
  no tonemapping, bloom, vignette, white balance or colour adjustments. Components are now saved
  as sub-assets; ACES and zero saturation apply.
- **Night was never detected in Gorodets.** `RiverLighting` looked for the clock on its own
  object, but Gorodets serializes the weather system while the HUD adds the clock to itself.
  Controllers are now found scene-wide and `DayNightController` refreshes the active instance.
- **TAA kept 20 % history** (`m_FrameInfluence` 0.8). The builder now sets `baseBlendFactor` 0.85.
- **Cloud shadows skipped URP Lit objects.** `Resources/RiverSkyLighting.compute` renders the
  same cloud function into a 512 px main light cookie spanning 6 km around the camera, so the
  vessel, buoys and marks darken with the terrain and water.
- **Water motion vectors were never written.** Requesting per-object motion data filtered out
  the stationary water renderer, and URP's `CalcNdcMotionVectorFromCsPositions` returns zero
  without that data. The water pass now draws without it and computes the vector itself.
  `_RiverMotionProbe` is a verification hook.
- **Sky ambient duplicated the sky gradient in C#** and its weighting was about 1.9 times too
  bright. Ambient is now projected from the live sky cubemap through the compute shader, scaled
  so a uniform sky of radiance L evaluates to L. Temporal AA is selected by whether the active
  renderer carries `RiverTemporalFeature`, not by asset name.

`GraphicsPhaseOneCheck.Run` now also verifies sky ambient, the cookie, motion vectors with a
still camera (the water and tree line must move, and a probe proves the water pass draws) and
night lighting in Gorodets.

Verified with Windows Unity 6000.6.0f1:

- EditMode: 56 passed, 0 failed (`TestResults/phase1-fix-editmode.xml`), including profile
  sub-assets, TAA history, cookie projection and ambient normalization.
- PlayMode: 10 passed, 0 failed (`TestResults/phase1-fix-playmode.xml`), including night with
  the clock on a different object.
- `GraphicsPhaseOneCheck.Run` passed (`Logs/phase1-fix-check.log`): with a still camera 90.9 %
  of the open-water region and 23 % of the tree line carry motion; night sun 0.127 in rain.
  Captures in `Logs/GraphicsPhaseOne/` inspected.

Limits: sky ambient and the cloud cookie need compute shaders (otherwise ambient stays at the
scene default and Lit objects get no cloud shadow); scrolling ripple normals have no motion
vectors and may soften under TAA; check timings are editor frame intervals on one RTX 4090, not
isolated GPU cost.

## Softer Natural Shoreline, 2026-09-13

Both scenes now have irregular wet-sand margins, a gently sloping submerged shelf,
and reed patches extending into the shallows. Bank rows are spaced at 3 m; plant
roots interpolate the actual bank triangles. Additional reeds use a separate seeded
random sequence so the woodland layout stays stable.

Reference: the Volga bank at Podgornoye, https://www.tursar.ru/page-joy.php?j=809,
showing exposed sediment, a visible submerged bed and uneven reed patches. No external
image assets were added to the game.

RiverGround lacked the DepthNormals pass needed by the SSAO prepass. This omitted the
bed from camera depth and prevented shallow-water blending. The added pass restores
bed detection. Water now blends over wet sediment with depth-based transmission,
fades shallow colour before the shelf ends, and reduces quiet-bank foam. Ground has
height-dependent wetness and a patchy sand-to-grass transition. Gorodets collision
boxes and scenario bathymetry remain unchanged; the familiarisation scene's existing
mesh colliders follow the regenerated banks. These remain procedural visual estimates.

Verified: 46 EditMode tests passed (`TestResults/shore-editmode.xml`), including
wet-margin area, submerged shelf, depth-pass presence and plant grounding. Final
shaders rendered without errors (`Logs/shore-final.log`). Both shore and close
waterline previews were inspected (`Logs/Landscape/*-after-shore.png` and
`*-after-waterline.png`). Runtime daylight, rain/fog and night checks passed with
planar reflections active (`Logs/shore-runtime.log`). The first test editor failed
during native initialization; the successful EditMode retry used `-nographics`,
with rendered shaders checked separately.

## Research Documents: Graphics and Ship Dynamics, 2026-09-13

Two research documents were added under `Assets/ShipSimulator/Documentation/`; no code
changed. `GraphicsRealismApproach.md` surveys sky, water, terrain, vegetation and
post-processing techniques and proposes a phased URP roadmap. `ShipDynamicsRealismApproach.md`
audits the current physics, collects MMG, Clarke, Blendermann, squat and bank-effect
formulations, and proposes an implementation and validation plan. Its audit found that
configured thrust (620 kN, speed independent) is about three times a power-based estimate and
that no physical speed limit exists; these are findings for later work, not fixed.

## Ship Waves, Cloud Sky and Realistic Water, 2026-09-13

Shader-drawn ship waves replace the old trail-renderer wake. `ShipWakeController` feeds
`ShipWakeTrack`, which records the stern track in the water frame (drifted by the
effective current, cleared when the vessel jumps more than 40 m, as on reset or load) and
publishes bow, stern and 46 history samples to `RiverWater.shader`. The shader draws:

- a deep-water Kelvin wake by stationary phase: transverse and diverging waves inside the
  19.47 degree wedge, build-up at the cusp, spreading loss and age decay;
- a bow pressure crest, midship drawdown and stern rise around a stadium-shaped hull;
- propeller wash as a long pale aerated band with shorter-lived streaky foam, plus hull,
  crest and shoreline foam.

Waves displace the surface and tilt normals, follow the curved track in turns, and keep
spreading after the vessel stops. The track search runs per pixel, because interpolating it
from vertices left jagged seams. For displacement the water mesh is subdivided three times
per edge at runtime only; stored scene meshes are unchanged.

All wake amplitudes are **estimated visual values** (amplitude 0.07 x U^2/g, slope gain
1.9, hull wave shape tuned by eye), not a validated wave height, wash or bank-erosion model.
Nothing feeds back into physics: no wave forces on the vessel or buoys, no shallow-water
or bank reflection of waves, and the Kelvin pattern assumes deep water.

Water: a generated seamless ripple normal map (`Settings/Water/RiverRippleNormal.png`,
integer wave numbers per tile) replaces per-pixel value noise; Schlick Fresnel,
distance-widened sun glint, view-aligned reflection distortion and an olive turbid colour.
`RiverSky.shader` replaces the procedural skybox with a fog-matched horizon, sun glow and
disc, drifting lit clouds that close to overcast under rain or fog, wind drift and night
stars. The ground shader uses rotated fBm and fades aliasing detail. Cameras use SMAA High,
the PC URP asset grades in HDR, and SSAO uses a 0.9 m radius at 0.6 intensity.

`RiverWaterAndSkyBuilder.ApplyBoth` (menu `Apply Realistic Water And Sky`) updates both
scenes; `RiverLandscapeBuilder.Apply` calls it, so scene rebuilds keep the result.
`WeatherController` drives the sky through the `_RiverCloudWeather` and `_RiverWind` shader
globals instead of editing the sky material.

Cost: up to 47 segment tests per water pixel inside the wake bounds, and about 0.6 million
water vertices in Gorodets after subdivision. Not profiled on low-end hardware.

Verified with Windows Unity 6000.6.0f1:

- Compilation: exit 0, no blocking errors.
- EditMode: 43 passed, 0 failed (`TestResults/wake-editmode.xml`).
- PlayMode: 9 passed, 0 failed (`TestResults/wake-playmode.xml`).
- `ShipWakeRuntimeCheck.Run` passed (`Logs/wake-runtime.log`); captures in `Logs/Wake/`
  inspected. The straight run reached 4.0 m/s through the water. Under helm the vessel
  slowed to 0.42 m/s near a bank, so the turning captures show a slow curved wake.
- `LandscapeRuntimeCheck.Run` passed with the cloud sky in daylight, rain/fog and night.

Known limitations: the environment reflection probe was not rebaked for the new sky, so
water reflections are clamped to the planar image at screen edges; a visible seam can
remain on the inner side of very tight turns; vegetation and vessel materials are unchanged.

## Empty Editor Startup Fix, 2026-09-12

The user editor log showed Play entered an `untitled` scene, with no voyage camera
or menu, and `Library/LastSceneManagerSetup.txt` contained an empty scene list.
`TrainingSceneStartup` now opens Gorodets on interactive startup when only a clean
untitled scene is open. From other non-voyage scenes, Play uses `playModeStartScene`
to launch Gorodets while preserving editor work. Either selected training scene
still plays directly. Automatic setup is disabled in batch mode for test/build isolation.
The explicit Play Training Scene command now offers to save modified scenes.

Verified: 33 EditMode tests passed (`TestResults/startup-editmode.xml`), including
unsaved-work preservation and both selected scenarios. The dedicated
`LandscapeRuntimeCheck.RunFromEmptyScene` passed empty-scene startup through the
menu into daylight, rain/fog and night rendering (`Logs/startup-runtime.log`).
Interactive verification also opened Gorodets successfully (`Logs/startup-editor.log`).
Startup waits for Unity to restore its scene setup before selecting the default scene;
the first editor delay callback can run too early.

## Water and Natural Landscape, 2026-09-12

Both scenes now use continuous riverbank meshes, alluvial soil and meadow shading,
branched foliage meshes, bushes, reeds and deeper woodland. RiverTrainingScene has
782 trees and 255 bushes; Gorodets has 1,036 trees and 361 bushes, plus reed clumps.
Plant roots interpolate the actual bank triangles, avoiding floating trees on hills.
Six tree variants, three bush variants and a reed prefab share generated mesh assets
with three LOD levels. Leaves use procedural cutouts, colour variation, translucency
and wind motion. No external vegetation packages or texture downloads were added.

`RiverLandscapeBuilder.ApplyBoth` updates existing scenes, and both original scene
builders call `Apply` so the upgrade survives regeneration. Assets are under
`Settings/NaturalLandscape/`; existing generated asset GUIDs are preserved on rebuild.
The Gorodets box-bank renderers are hidden while their existing colliders remain.
The familiarisation scene uses the new bank meshes for shore collision. Fairway,
bathymetry and vessel physics parameters are unchanged.

Water now uses scene-depth shoreline shading, distance-filtered ripple normals,
shadowed sun highlights and planar reflections of the vessel, banks and vegetation.
`RiverPlanarReflection` renders a 768-pixel HDR reflection with a 1,400 m far limit,
excludes water/UI layers and guards recursive rendering. It adds one scene render per
visible camera, so it has a GPU cost; tree LODs and reflection mipmaps limit detail.
DirectX reflection UVs require a vertical flip. Runtime assembly references now include
URP/Core to submit the reflection render request. The PC shadow distance is 180 m and
daylight sky tint is neutral to remove the previous green horizon cast.

Verification:

- Unity compilation and rendered shaders: no blocking errors.
- EditMode: 30 passed, 0 failed (`TestResults/landscape-editmode.xml`).
- PlayMode: 8 passed, 0 failed (`TestResults/landscape-playmode.xml`).
- `LandscapeRuntimeCheck.Run` passed daylight, rain/fog and night captures with planar
  reflections active (`Logs/landscape-runtime.log`).
- Both scenarios' rendered previews and in-game captures inspected in `Logs/Landscape/`.

Rendering was checked on this Windows machine's desktop URP configuration. This is
an improved procedural environment, not scanned terrain or validated geography.

## Maritime Menu and Saved Voyages, 2026-09-12

Both training scenes now open a runtime maritime start menu with a procedural river
chart, compass and vessel motif, navy panels and brass accents. New voyage selects
River familiarisation or Gorodets; Continue restores the saved passage. Escape opens
the pause menu and returns from submenus before resuming. Menu pause blocks gameplay
input, hides the HUD, pauses simulation audio and restores the prior time scale.

`Scripts/UI/VoyageMenu.cs` bootstraps without scene edits, so builders preserve the
feature. `VoyageSettings` persists volume, camera sensitivity, quality, VSync and
standalone display mode. `Scripts/Persistence/VoyageSave.cs` provides versioned JSON,
validation and atomic replacement with a previous-save backup. The default save is
`Application.persistentDataPath/voyage.json`; saving is manual, with one slot.

Save state includes scene, Rigidbody pose and velocities, commanded and actual engine
and rudder state, camera orbit/view, weather and day/night, simulation speed, current
and water-level multipliers, grounding damage, Gorodets phase, score and penalties.
Particles and radar trails restart. Normalize Unity's Rigidbody quaternion when
capturing: floating-point drift can otherwise fail validation on a valid rotation.

Verified with Windows Unity 6000.6.0f1 from WSL:

- Compilation successful; no blocking compiler errors.
- EditMode: 25 passed, 0 failed (`TestResults/menu-editmode.xml`).
- PlayMode: 8 passed, 0 failed (`TestResults/menu-playmode.xml`), including actual
  injected Escape input, blocked gameplay keys, pause speed and state restoration.
- Dedicated `VoyageMenuSmokeCheck.Run`: passed start, save, switch to Gorodets,
  load the river save, pause and resume at the saved speed (`Logs/menu-smoke.log`).
- Menu screenshots inspected at `Logs/MenuScreenshots/`: start, settings and pause.

The smoke check enables background execution and queues editor player-loop updates;
a batch editor without a focused Game view otherwise stalls. PlayMode input tests use
Unity's isolated `InputTestFixture` to avoid the editor consuming injected keyboard
events. Screenshot tests render the menu through an offscreen camera because a batch
editor does not produce normal `ScreenCapture` output without a Game view.

Standalone fullscreen switching has not been exercised in a built player. Historical
verification notes below describe earlier work; the counts above are the latest runs.

## Purpose

ShipSim159 is a Unity 6 URP prototype for simulating a Project 507B Volgo-Don
river cargo vessel. It is an engineering prototype, not a validated maritime
training product. Published vessel particulars and estimated physics parameters
are documented separately.

## Current Architecture

- Unity version: `6000.4.0f1`
- Main scene: `Assets/ShipSimulator/Scenes/RiverTrainingScene.unity`
- Runtime assembly: `ShipSimulator.Runtime`
- Editor assembly: `ShipSimulator.Editor`
- Tests: `ShipSimulator.EditModeTests` and `ShipSimulator.PlayModeTests`
- Vessel data: `Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json`
- Vessel prefab: `Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab`

The vessel uses a Rigidbody with point buoyancy, linear/quadratic resistance,
aggregate propulsion, rudder lift, wind force, and current-relative water
velocity. Keyboard controls are W/S telegraph, A/D rudder, Space Stop, R reset,
C rudder midships, V camera cycle, and number keys 1-9 for direct camera views.

## Work Completed

### Repository Documentation

The repository root now contains a public-facing `README.md` covering the
prototype scope, features, Unity version, startup steps, complete controls,
radar legend, editor tools, test workflow, project structure, simulation model,
known limitations, and links to the detailed project documentation.

### Detailed Vessel Model

The vessel model was imported from the CRYENGINE project
`G:\Projects\VolgoDon159`:

- source FBX: `Assets\props\volgo_don\volgo_don.fbx`
- imported into `Assets/ShipSimulator/Models/VolgoDon507/`
- CRYENGINE color/AO DDS maps were reused as URP base maps
- model axes are converted from CRYENGINE `X forward / Y starboard / Z up`
  to Unity vessel `Z forward / X starboard / Y up`
- scale was corrected to 138.3 m length
- width is approximately 16.4 m
- hull bottom was aligned for the configured 3.53 m loaded draft
- CRYENGINE proxy geometry was hidden
- a simple `CollisionHull` BoxCollider was retained for dynamic physics

`VolgoDonModelIntegrator.cs` can rebuild materials, alignment, prefab
integration, and a preview. Do not replace the dynamic collision hull with a
non-convex MeshCollider.

### Build Configuration

`RiverTrainingScene` is first and enabled in `EditorBuildSettings`.
The template `SampleScene` remains present but disabled. The prototype builder
also preserves this ordering when regenerating the scene.

### River Current

`RiverCurrentZone` registers vessels through trigger enter/stay/exit callbacks.
`ShipPhysicsController` uses:

- `ambientCurrentMps` when outside all zones
- the average velocity when inside overlapping zones

The current affects resistance and rudder-relative water velocity.

### Vessel Data Validation

`VesselDataValidator` validates all required JSON sections before physics starts.
It checks finite positive dimensions, mass ordering, load fraction, inertia,
engine/propeller/rudder configuration, position-array counts, hydrodynamic
coefficients, buoyancy, controls, and calibration multipliers.

Invalid JSON disables `ShipPhysicsController` through a failed load and logs a
specific error. Required propulsion, rudder, and buoyancy components are also
checked during `Awake`.

## Verification Status

Unity compilation completed without C# compiler errors.

Latest verified tests:

- EditMode: 9 passed, 0 failed
- PlayMode: 2 passed, 0 failed

EditMode tests cover the production JSON, invalid draft, mismatched propeller
arrays, startup scene order, required HUD/camera scene configuration, the
curved fairway depth profile, the compound vessel collision hull, and the
runtime ship navigation-light rig, and the buoy beacon flash timing.
PlayMode tests cover current-zone entry/exit and overlapping-zone averaging.

Verify test results in:

`C:\Users\User\AppData\LocalLow\DefaultCompany\ShipSim159\TestResults.xml`

The `mcp-unity` package used to run these and frequently timed out after
completing a request; timeouts and `TestRunnerService` exceptions observed then
belonged to that package, not to the project tests.

## Unity 6000.4 to 6000.6 upgrade, 2026-09-05

The editor was upgraded from `6000.4.0f1` to `6000.6.0f1`, which rewrote
`ProjectVersion.txt`, `Packages/manifest.json`, `packages-lock.json`,
`QualitySettings.asset` and `UniversalRenderPipelineGlobalSettings.asset`, and
added `PhysicsCoreProjectSettings2D.asset` and `ProjectAuditorSettings.asset`.
Package bumps of note: URP 17.4.0 to 17.6.0, Input System 1.19.0 to 1.20.0,
Test Framework 1.6.0 to 1.8.0, Timeline 1.8.11 to 6.6.0.

The upgrade left the project unable to enter Play Mode. **No ShipSimulator code
was affected**: all 75 logged `error CS` lines came from the third-party
`com.gamelovers.mcp-unity` package, which used two APIs that Unity 6.6 made
hard-obsolete, `EditorUtility.InstanceIDToObject(int)` and
`Object.GetInstanceID()`, reported as CS0619 at 25 call sites.

Resolved by removing `com.gamelovers.mcp-unity` from `Packages/manifest.json`.
It was a dev-only editor bridge; no code under `Assets/` referenced it. The
package had been declared as an unpinned git URL, so the resolved revision was
whatever it last fetched (`cce8b57de9cb`).

That cleared all 75 `CS` errors but Play Mode was still blocked, by a second
incompatible package that the first pass had missed: `com.unity.ai.assistant`,
pinned to the stale pre-release `2.12.0-pre.2`, failing Unity 6.6's own analyzer
with `error UAC0020` in `DynamicAssemblyBuilder.cs(191,20)`, because
`System.Reflection.Assembly.Load(byte[])` loads into a non-Unity
`AssemblyLoadContext`. It was a direct manifest entry (`depth: 0`) with no
reverse dependencies and nothing under `Assets/` referencing it, so it was
removed the same way.

The lesson worth keeping: **grep for `error [A-Z]`, not `error CS`.** Unity 6.6
analyzers report blocking errors under prefixes like `UAC`, so a `CS`-only
search reported this project as clean while it could not enter Play Mode.

Not yet verified: a clean compile and the EditMode/PlayMode suites have not been
run since either removal. That is the next thing to confirm.

## Visual Upgrade

The training scene now uses a procedural URP river shader, a subdivided water
mesh, tuned vessel materials, softer daylight/fog, post-processing, layered
grass banks, rocks, reeds, and lightweight procedural vegetation. The follow
camera has eight views, damped movement/rotation, speed-based FOV, mouse orbit,
wheel zoom, and improved look targeting.

The visual pass is implemented in
`Assets/ShipSimulator/Scripts/Editor/ShipSimulatorVisualUpgrade.cs` and is also
called by `ShipSimulatorPrototypeBuilder` when rebuilding the prototype. Its
Unity menu commands are:

- `Ship Simulator/Apply Visual Upgrade`
- `Ship Simulator/Render Visual Preview`

The water shader is
`Assets/ShipSimulator/Shaders/RiverWater.shader`; generated visual settings and
the water mesh are under `Assets/ShipSimulator/Settings`.

The river surface uses a procedural, texture-free multi-scale flow model rather
than large ocean waves. It combines low-amplitude irregular displacement,
heading-biased current motion, broken cross-ripples, sparse current streaks,
turbid green-brown depth colors, environment reflection, Fresnel response, and
fog. The tuned values are stored in `Materials/RiverWater.mat` and reproduced
by `ShipSimulatorVisualUpgrade.CreateWaterMaterial()`.

The river environment now covers the full orbit-camera view. Both banks use
generated terrain meshes with an irregular shoreline, sloped soil, rolling
grass heights, meadow patches, rocks, reeds, and vegetation extending behind
the vessel. The water and terrain continue far enough in both directions to
end in atmospheric fog rather than at a visible world edge. The original box
banks remain as invisible physics colliders.

## Control HUD

`ShipTelemetryUI` builds a compact simulator-style bridge HUD at runtime. The
center view is kept clear by separate bottom rudder, telegraph, and camera
panels. It includes a heading tape, objective and distance, speed, course,
drift angle and side slip, estimated depth and under-keel clearance, current
direction, cargo load, estimated RPM/engine load, warnings, and active command
highlighting.

The HUD uses larger type and stronger hierarchy for primary values. The
objective is a wider card, the heading tape has a fixed center marker, normal
system status is not duplicated, warnings appear only when active, telegraph
buttons use full command names, and F1 toggles the full shortcut strip. Bottom
controls have larger safe-area margins and the camera panel is compact.

The river radar is vessel-centered and heading-up. The ship remains fixed while
the curved route, waypoint, channel edges, and paired buoys move and rotate
around it. A continuous color bathymetry grid, range frames, crosshairs, and a
rotating sweep combine navigation and depth information in one modern display.
It shows current depth, minimum depth ahead, draft, and a safe/caution/shallow
legend. `M` toggles the radar and `F1` emphasizes the shortcut strip. Depth,
warning thresholds, bank geometry, and radar layout share `FairwayModel`.

Radar line meanings are explicit and visually distinct. A thin cyan dashed line
is the fixed ship heading, a yellow dashed route follows the curved
`FairwayModel` centerline, a gray trail records recent actual vessel positions,
and a short white dashed prediction uses forward speed, lateral drift, and the
actual rudder angle. The legend names all four line types; the radar size remains
420x420 because depth and fairway navigation are primary gameplay information.

UI controls call public command methods on `ShipPhysicsController`; they do not
simulate keyboard input. Engine and rudder response delays remain governed by
the vessel JSON. `W/S` and arrow keys step the telegraph, `A/D` and arrow keys
command the rudder, `C` or Enter centers it, Space selects Stop, and `H` sounds
the horn. Number keys `1` through `9` select Chase, Bridge, Top, Port,
Starboard, Bow, Stern, Docking, and Navigator views. Navigator is a fixed
forward-facing viewpoint from inside the wheelhouse. `V` cycles views. `N`
toggles day and night lighting, sky, ambient light, and fog. At night, each
buoy gains a short flashing beacon with staggered phases; fixed navigation
markers remain continuously lit. The vessel gains port and starboard
sidelights, forward and aft masthead lights, a stern light, and a white bow
light near the foredeck. Each vessel light now has a visible dark lantern
housing, mounting plate, and vertical support tied back to the deck,
wheelhouse, or mast base. The fixtures remain visible during daylight while
only their lenses and light sources are disabled.

`ShipWakeController` creates two propeller-wash, two hull-wake, and two bow-wave
trails at runtime. Wake width and opacity respond to speed and throttle.
Navigation markers are enlarged for visibility.

The training fairway uses 10 paired lateral buoy stations rather than two
straight decorative rows. The scene models Russian inland-river lateral
marking relative to the downstream `+Z` direction: the right edge is marked by
red buoys with red flashing lights, and the left edge by white buoys with dark
top marks and green flashing lights. Buoys are placed on normals to a curved
fairway centerline, with closer spacing through the bends and a slightly
narrower marked channel upstream. The radar mirrors the paired curved layout.
The simulated flash is 0.32 seconds in a 1.5-second period with staggered phases;
it is a prototype characteristic rather than a published local notice to
mariners.
Use `Ship Simulator/Arrange Navigation Buoys` to rebuild only the navigation
layout without rebuilding the full prototype.

Depth is an explicitly estimated curved channel profile because the scene has no
surveyed bathymetry. The minimap is a local fairway indicator, not a route
chart.
Mooring lines, anchors, thrusters, fuel, damage, and autopilot are not exposed
until their simulation systems exist.

Editor automation commands:

- `Ship Simulator/Play Training Scene`
- `Ship Simulator/Stop Play Mode`

After the latest HUD work, Unity compiled without C# errors. The current
EditMode result is 8/8 passed; the latest completed PlayMode result is 2/2
passed.

The latest water shader imported without shader errors and was checked using
front and rear 1600x900 visual previews. The MCP Test Runner timed out during
the water-only visual change, so the test counts above remain the latest
completed functional runs.

After the radar line clarification, EditMode completed again at 8/8. Runtime
Play Mode inspection found and fixed a hot-reload null-array issue; a clean
rerun produced no HUD exceptions. The PlayMode Test Runner request timed out
before replacing the XML, so 2/2 remains the latest completed PlayMode suite.

After correcting Russian river buoy sides and adding flashing beacons, EditMode
completed at 9/9. Runtime inspection confirmed night mode and observed the same
green beacon in both lit and unlit phases. The PlayMode Test Runner timed out
without replacing the XML, so 2/2 remains the latest completed PlayMode suite.

After mounting the vessel navigation lights and adding the bow light, Unity
compiled without C# errors and EditMode completed at 9/9. The navigation-light
test now verifies six sources plus physical support and housing objects. The
latest completed PlayMode suite remains 2/2.

The latest polish pass replaces the escaped objective speed text with
`Speed limit: max 8 km/h`, keeps the camera status on one compact line, and
moves all radar line meanings into an explicit color legend. Night moonlight,
ambient visibility, buoy beacon range/glow, and the existing six-trail vessel
wake were strengthened without changing the HUD or radar footprint. Unity
compiled the runtime and EditMode assemblies successfully. The focused HUD
formatting regression test passed; the suite now contains 10 EditMode tests,
but the MCP service timed out before completing a fresh full-suite run.

## Gorodets Scenario

`Assets/ShipSimulator/Scenes/GorodetsTrainingScene.unity` is a separate
2.27 km high-difficulty scenario for the existing Project 507B vessel. It is
enabled in Build Settings after the primary training scene.

The scenario includes a sampled curved route, asymmetric procedural
bathymetry, rocky shoal patches, paired lateral marks, three leading-mark
pairs, six blended current regions, differential bow/stern current yaw,
estimated squat and shallow-water resistance, keel-clearance grounding,
mission phases, warnings, and scoring.

Use `Ship Simulator/Build Gorodets Scenario` to regenerate its scene-owned
content. The geometry, depth, current, squat, damage, and scoring parameters
are estimated training-game values, not current navigation data.

The HUD now provides simulation time controls at 1x, 2x, and 4x. `T` cycles
the speed and `Shift+T` returns to 1x.

The Gorodets scene now reuses the procedural `RiverWater` shader and subdivided
water mesh instead of its former stretched URP Lit cube. `WeatherController`
adds configurable wind direction/force, camera-following rain, and fog. Wind
is passed to vessel physics and also adjusts water ripple strength. The HUD
uses `F2` for wind force, `F3` for direction, `F4` for rain, and `F5` for fog.

Leading marks now receive long-range lights plus large emissive night boards
and alignment stripes. These are created at runtime by `DayNightController`.

Latest verification after this work:

- Unity compilation: successful
- EditMode: 19 passed, 0 failed
- PlayMode: 5 passed, 0 failed
- Gorodets Play Mode smoke check: no runtime exceptions

## Known Limitations

- Hydrodynamic coefficients are estimates and are not training validated.
- Buoyancy uses 15 points rather than sectional or volume hydrostatics.
- Twin-screw propulsion is aggregated at one centerline force point.
- The Gorodets scenario has limited estimated shallow-water resistance, squat,
  grounding drag, and abstract damage points. It does not model cavitation,
  flooding, structural damage, or validated bottom interaction. Wake is visual
  only and does not affect physics.
- Static shore collisions use the generated bank slope meshes. The vessel uses
  three overlapping box colliders for bow, midship, and stern, with continuous
  dynamic collision detection.
- Input reads `Keyboard.current` directly; no rebinding or gamepad workflow.
- `maxLoadedSpeedMps` is data only and is not enforced.
- UI uses legacy `UnityEngine.UI.Text`.
- This workspace was not a Git repository during the initial analysis.

## Recommended Next Work

1. Add tests for loader parse errors and missing required ship components.
2. Replace direct keyboard polling with an Input Actions asset.
3. Split aggregate propulsion into port/starboard forces and controls.
4. Add real bathymetry and collision look-ahead warnings.
5. Calibrate acceleration, stopping, and turning against real trial data.

## Files to Read First

- `AGENTS.md`
- `Assets/ShipSimulator/Documentation/README.md`
- `Assets/ShipSimulator/Documentation/OperatorGuide.md`
- `Assets/ShipSimulator/Documentation/ShipSimulator_Physics.md`
- `Assets/ShipSimulator/Documentation/VolgoDon507B_Sources.md`
- `Assets/ShipSimulator/Scripts/Physics/ShipPhysicsController.cs`
- `Assets/ShipSimulator/Scripts/Physics/VesselDataValidator.cs`
- `Assets/ShipSimulator/Scripts/Editor/VolgoDonModelIntegrator.cs`

## Windows tag releases (2026-09-20)

- Release version integration: `PlayerSettings.bundleVersion` in
  `ProjectSettings/ProjectSettings.asset` is now the single version source. Both release
  scripts bump that value, commit only that file as `Release vX.Y.Z`, verify the committed
  value, then tag and optionally push branch before tag. Dry runs are side-effect free.
  This supersedes the earlier tag-derived, no-commit release behavior below.
- CI rejects tag/bundle-version mismatches before building and uses GameCI
  `versioning: None` to preserve the committed setting. The menu displays
  `v{Application.version}` in muted text at the lower-right of its panel.
- Verified: 10 Bash/CI regression tests and 9 native Windows PowerShell release tests
  using copied settings and fake Git executables, including commit/tag/push failures,
  version-only staging/commit, dry-run immutability and LF/CRLF preservation. Unity
  PlayMode passed 40/40, including the version-label assertion; inspected
  `Logs/MenuScreenshots/menu-start.png` and confirmed the unobtrusive `v0.1.0` label.
  No actual release commit, tag, push or hosted CI build was performed.
- Added native Windows PowerShell equivalents, `scripts/release.ps1` and
  `scripts/release-smoke.ps1`, preserving the Bash scripts and their argument names.
  PowerShell usage and process-local execution policy examples are in `scripts/README.md`.
  Verified under Windows PowerShell 5.1: real Git dry run, mocked SemVer/argument and
  preflight checks, branch-before-tag push ordering and failure handling, caller-directory
  restoration, plus complete/missing/empty build fixtures with spaces and brackets in paths.
  Git mutations were mocked throughout those regression checks; no tags or pushes were made.
- Added `scripts/release.sh` for stable SemVer bumps or explicit versions, dry runs,
  confirmation, annotated tags and optional branch-first, then tag push. It never edits project
  versions or creates commits; real releases require a clean tree.
- Added `.github/workflows/release.yml`: pushed `vX.Y.Z` tags build with GameCI and
  Unity `6000.6.0f1`, targeting StandaloneWindows64 with the current Mono backend.
  The complete build is zipped, extracted and checked before GitHub Release upload.
- `scripts/release-smoke.sh` checks nonempty Windows player, UnityPlayer, Mono,
  data and runtime assembly files. `scripts/README.md` documents license secrets,
  first-release commands and the limits of file-layout verification.
- Verified Bash syntax, workflow YAML parsing, ten mocked version/dirty-tree cases,
  complete/missing/empty player fixtures and a ZIP extraction round trip. No Git
  mutations, Unity build or GitHub workflow run were performed. The first remote
  run must verify license activation and editor image availability; Windows gameplay
  still needs a manual launch check. Existing SerpukhovZaton.json edits were preserved.

## Shared agent check tools (2026-09-20)

- Moved reusable Unity launching and XML summaries to `Tools/shipsim-check.ps1` and
  `Tools/read-test-results.py`; usage and platform limits are in `Tools/README.md`.
- The Windows/WSL launcher derives the checkout and Hub version automatically, supports
  a custom editor location and dry runs, quotes paths with spaces, and rejects compiler
  errors or missing/failed test results after Unity exits. Personal approved entry points
  remain fixed-argument compatibility wrappers under `.codex/tools/`.
- Validation: ten mocked launcher regression cases passed, including paths with spaces,
  a busy project, compiler errors, missing outputs and exit propagation. Actual FastCrafts
  dry-run and XML summary parsing passed. No Unity gameplay or rendering checks were run
  for this tooling-only change. Native macOS/Linux launching remains unsupported.

## Authored Luch 14352 visual, 2026-09-20

- Replaced only the Luch prefab visual with an actual Blender MCP-authored FBX, based on
  the updated 14352 archive photograph (FleetPhoto 478451). Source:
  `Art/Source/Luch14352/Luch14352.blend`; reference decisions and six retained renders
  are alongside it. Export: `Assets/ShipSimulator/Models/Luch14352/Luch14352.fbx`.
- LOD0/1/2: 59,946 / 26,974 / 7,930 triangles. LOD0 dimensions: 23.7201 m length,
  4.5300 m outer beam, 5.1279 m including mast. Main hull beam 3.85 m; wheelhouse
  top 3.60 m above baseline. The disputed beam definitions are documented as an
  interpretation, not a verified source resolution. All LOD topology audits passed.
- `Luch14352ModelIntegrator` preserves Rigidbody, physics scripts and three box
  colliders, updates only `DetailedVisual` and model-dependent camera/light layout.
  The procedural builder routes Luch through the integrator on catalogue rebuild.
  Physics JSON and six serialized physics component blocks are unchanged.
- Corrected source documentation: 14352 has one waterjet; 1.25 m is hull depth,
  0.45 m is skeg height, published aft cushion draft is 0.60 m. Existing simulation
  coefficients/dimensions remain explicitly estimated and were not recalibrated.
- Verified Unity 6000.6.0f1 import, axes, unit scale, waterline and no reflection;
  EditMode 199/199, PlayMode 40/40, FastCrafts shakedown PASS (both vessels). Luch
  reached 6.79 m/s in the short reach, 99% configured cushion support, 0.21 m rise,
  0.1 degree heel. Gameplay port/chase/navigator screenshots inspected. No compile,
  shader or unexpected runtime exception errors in the four current check logs.
- Detailed evidence and log paths: `Assets/ShipSimulator/Documentation/Luch14352_Sources.md`.
  Underbody, jet details, hydraulics and small fittings are reconstructions; the
  model is a game approximation, not a survey or training-validated vessel.

## Authored Meteor 342U visual, 2026-09-20

- Replaced only the Meteor prefab visual with a Blender MCP-authored FBX. Source:
  `Art/Source/Meteor342U/Meteor342U.blend`, rebuilt end to end by seven numbered
  passes plus `common.py`; reference decisions, the two-state captures and twelve
  retained renders sit alongside it. Export:
  `Assets/ShipSimulator/Models/Meteor342U/Meteor342U.fbx`.
- Identification: FleetPhoto lists exactly one 342U, yard 062 `Meteor-191`, built
  31.07.1984 as 342E, modernised in the early 1990s, renamed `Prepodobnyy Serafim`
  in 2006. Hull, foils and arrangement come from the project 342 family general
  arrangement (Ris. 139) and foil drawings (Ris. 140); appearance is checked against
  twenty-six photographs of that ship plus a CC BY-SA Wikimedia foilborne shot and a
  RussianShips bow-on view. No external feature could be attributed to the
  modernisation, so nothing was invented to separate 342U from 342E.
- Ris. 139 was calibrated three independent ways that agreed at 31.58 px/m: the
  station scale, the 9.5 m foil span in plan and the 1.05 m hull draft in profile.
  Every hull line table was measured off it rather than guessed.
- LOD0/1/2: 46,830 / 21,073 / 7,582 triangles, 9.54 x 34.60 x 7.54 m. The model
  reproduces published LOA 34.6 m, foil span 9.5 m, height above baseline 6.25 m,
  draft afloat 2.35 m, hull draft 1.05 m, freeboard 0.42 m, and 0.71 m five-bladed
  propellers at the JSON shaft positions.
- Finding recorded but not applied: the plan gives a hull moulded breadth near 6.0 m
  and extreme breadth over the side decks near 6.7 m, against the 5.2 m estimate in
  `Meteor342U.json`. Recalibrating hydrostatics is outside an art replacement, so the
  physics JSON is untouched.
- `Meteor342UModelIntegrator` preserves the Rigidbody, physics scripts and three box
  colliders and updates only `DetailedVisual` and the model-dependent camera/light
  layout. It refuses the import if the exported axes are reflected or if the exported
  propeller landmark disagrees with the JSON. The procedural builder routes Meteor
  through the integrator on catalogue rebuild.
- Navigation light layout corrected: the after masthead light now sits on the mast
  above the forward one on the bow saloon roof, which the catalogue test requires.
- Verified: batch compile exit 0 with no `error [A-Z]`; EditMode 199/199; PlayMode
  40/40; `METEOR_MODEL|PASS`; FastCrafts shakedown PASS with the Meteor at 24.8 kn,
  99 per cent foil support, 1.04 m hull rise, 0.1 degree heel, no collision;
  `MENU_SMOKE|PASS`. Displacement and foilborne attitudes were checked against a
  water plane using the measured hull rise.
- Evidence and log paths: `Assets/ShipSimulator/Documentation/Meteor342U_Sources.md`
  and `Art/Source/Meteor342U/README.md`. Foil sections, propeller blade shape, mast
  and fitting sizes and the interior proxy are reconstructions; the model is a game
  approximation, not a survey or training-validated vessel.

## Meteor visual correction after user review, 2026-09-21

- Supersedes the earlier visual-quality and exact-reconstruction claims above. The old
  uncommitted source had a missing wheelhouse shell, overwritten saloon glazing faces,
  excessive cabin height, small side windows and an unsupported tall bow railing.
- Rechecked the Meteor-191/Prepodobnyy Serafim photographs, the Alexxx1979 Wikimedia
  foilborne photograph and Ris. 139 family illustration. The latter is a model-building
  reference, not a dimensioned shipyard plan. Cabin and fitting dimensions are estimates.
- Rebuilt the saloon surfaces and swept wheelhouse with explicit glazing panels; lowered
  and rounded the cabin, raised the bow deck, enlarged side windows, corrected paint
  bands, raft placement, foil-support paint and underbody colour. Retained hull/foil
  sections and the lightweight interior are approximate. No physics recalibration.
- Added Blender regression checks for missing glass, bridge roof, outward glass normals,
  patch-corner holes, landmarks and navigator sightline. The old eye was behind a new
  pillar; the exported camera landmark now looks through the clear front windscreen.
- Saved the revised source in the user's open Blender session and exported the FBX.
  LOD0/1/2: 76,926 / 34,615 / 9,620 triangles, eleven material slots, zero degenerate
  triangles. The render shell is not watertight. Lights use exported source landmarks.
- Final Unity import exit 0 and METEOR_MODEL|PASS; EditMode 199/199; PlayMode 40/40;
  FastCrafts shakedown PASS for both vessels. Meteor 12.74 m/s, 99% configured support,
  1.04 m rise in the short test reach. Six physics/collider component blocks match HEAD
  exactly; vessel JSON unchanged. git diff --check passed.
- Current evidence and limits: Art/Source/Meteor342U/README.md and
  Assets/ShipSimulator/Documentation/Meteor342U_Sources.md. Current preview:
  Art/Source/Meteor342U/Previews/corrected-quarter.png. Historical source backup:
  Logs/Meteor342U/before-correction.blend. All changes remain uncommitted.

## Meteor geometry repair after user inspection, 2026-10-18

- Closed the hull stem and deck, cabin nose and aft screen. Fitted every mirrored
  foil strut root to the evaluated hull surface, with 0.063 to 0.123 m embedding.
- Replaced mismatched glazing patches with shared constrained triangulation of glass,
  frames, shell and sheer paint. Fixed ceiling/seat protrusions, deck overlap, stepped
  hull paint, foil tips, shaft/hub gaps, rudder connections and fitting brackets.
- Source regression checks pass; four deliberate defects are rejected on temporary
  meshes. Closed hull/foil components have no nonmanifold edges; the cabin skin has
  no internal boundary edges. The complete render assembly is not a displacement solid.
- Unity previews exposed glass shards from collapse decimation. Distance LODs now use
  material-delimited planar dissolution, with glass/paint area guards. Final counts:
  68,596 / 25,245 / 14,057 triangles; zero degenerate triangles. LOD images inspected.
- Saved source, regenerated FBX and reintegrated the prefab. Import exit 0 and
  METEOR_MODEL|PASS in Logs/Meteor342U/geometry-repair-import-lods.log. EditMode 199/199
  in TestResults/check-EditMode-20261018-144334-448.xml. Final FastCrafts exit 0 and
  VESSEL_SHAKEDOWN|PASS for both craft in Logs/check-FastCrafts-20261018-145907-552.log.
  Meteor: 12.74 m/s, 99% support, 1.04 m rise, 0.1 degree heel. No physics recalibration.
- Six physics/collider prefab blocks match HEAD; vessel JSON unchanged. No commits.
  Source backup: Logs/Meteor342U/before-geometry-repair.blend. Current review images:
  Art/Source/Meteor342U/Previews/repair-final-* and repair-unity-*. Updated the README,
  vessel sources and the user's existing MODEL_PROVENANCE.md without discarding edits.


## Meteor reference appearance review, 2026-10-18

Compared classic Meteor illustrations and the 2016 Meteor-191 photograph.
Shortened and tapered the central roof cover; added paired blue-banded ventilation
trunks. Dimensions are photo estimates. MODEL_PROVENANCE.md section 15 records
sources and limits. Blender source, FBX and Unity prefab updated through the
registered Blender MCP and Unity integrator. Geometry regression guards pass;
Unity integration and FastCrafts both pass. Meteor: 12.72 m/s, 99% foil support,
1.04 m rise. Physics components and colliders unchanged. LOD triangles:
68554 / 25215 / 14008. Previews/reference-{quarter,side}.png show the revision.
Backup: Logs/Meteor342U/ReferenceReview/before-reference-adjustment.blend.
Exact hull/window/foil reconstruction remains approximate. No commits.


## Meteor silhouette revision, 2026-10-18

After user supplied MeteorHydrofoil.jpg (Meteor-247), corrected main silhouette:
lower bow roof nose, aft-raked bow mullions, window heads raised from 1.95 to
2.46 section units, wider/longer wheelhouse base, flat aft cabin crown and visor
rise reduced from .70 to .18 m. Photo-derived estimates, not exact reconstruction.
MCP reconnected after user reopened Blender; all scene operations used registered
Blender MCP. Backup: Logs/Meteor342U/ReferenceReview/before-silhouette.blend.
Source guards and four defect regressions pass. LOD export removes zero-area
triangles after triangulation; LOD2 dissolution reduced to 1 degree to preserve
paint area. LOD counts 69994 / 24994 / 16971. Saved blend and FBX, Unity prefab
integrated. silhouette-import-final.log reports PASS, exit 0, no compile errors.
FastCrafts check-FastCrafts-20261018-210448-756.log passed both vessels, exit 0.
Meteor 12.72 m/s, 99% support, 1.04 m rise. Physics/colliders unchanged.
Previews/silhouette-{quarter,side,afloat}.png are current. Provenance section 16
and README describe limits. Changes remain uncommitted.


## Luch 14352 reference reconstruction, 2026-10-19

User confirmed the 1994 version, not older 14351 or modern Luch concept.
Primary reference Vympel archive photo 478451; bureau specification and family
arrangement provide controls. New 04_reference_shape.py rebuilds rounded bow,
foredeck, shallow cambered light roof and closed swept wheelhouse with rounded
glazing and clipped corners. 03_finish_export.py invokes this pass and validation,
regenerating existing LODs/landmarks without duplicates. Source and FBX saved.
validate_model.py checks shells, window count/clearance, roof height; temporary
open-hull/missing-glass mutations prove guards in test_geometry.py. All pass.
LOD triangles 65148 / 29316 / 9888, zero open/nonmanifold edges or degenerates.
Unity integration reference-import.log passed, exit 0, no compile errors.
FastCrafts check-FastCrafts-20261019-222401-638.log passed both vessels, exit 0;
Luch 6.81 m/s, 99% support, .21 m rise. Physics/colliders unchanged against HEAD.
Previews/reference-*.png are current. MODEL_PROVENANCE.md records estimation
limits. Backup Logs/Luch14352/ReferenceReview/before-reference-rebuild.blend.
All Blender operations used registered MCP. Changes remain uncommitted.


## Luch PBR texture verification, 2026-10-19

Seven original deterministic 1024px material sets (21 PNGs) now cover paint,
non-slip deck, rubber, aluminium and fabric. Base color, normal and packed
metal/smoothness maps use matching two-metre UVs in Blender and Unity. Glass
has neutral tint and reduced opacity. Source script: 05_materials.py. Runtime
PNGs and Unity metadata are in Models/Luch14352/Textures; no external image license.

Texture imports now finish before prefab editing, preventing instance overrides
from being reset by reimport. Imported duplicates from the intermediate run were
removed; the final prefab has one DetailedVisual and three renderers. Physics
components/colliders match HEAD. LOD2 omits fine railings/lights and uses 7,820
triangles with no open/nonmanifold edges or degenerates. LOD0/1: 65,148 / 29,316.
Unity textures-checked.log import passed, exit 0, no compile errors. All 200
EditMode tests passed in check-EditMode-20261019-223113-246.xml, including map
presence, UV, color-space, normal importer, shader-keyword and renderer-count checks.
FastCrafts passed both vessels, exit 0, in check-FastCrafts-20261019-223158-397.log.
Luch reached 6.81 m/s with 99% support and .21 m rise. Blender close-up and Unity
LOD previews were inspected. Textured source is saved with relative image paths
and material preview enabled. Previews/textured-{quarter,bow-detail}.png show
this revision. Colors and wear are authored estimates, not surveyed paint data.

Changes remain uncommitted.


## Serpukhov R-96 reconstruction and integration, 2026-10-26

Added the original 1973 R-96 as sixth playable vessel, id `serpukhov-r96`.
Source: `Art/Source/Serpukhov_R96/Serpukhov_R96.blend`, authored through Blender
MCP from the original six-page GA and actual vessel photos, especially July 1989.
README.md there records sources and uncertainty. No copyrighted photos are shipped.
487 editable source meshes, three LODs (77,154 / 30,120 / 8,072 triangles),
30 original packed PBR maps, transparent bridge with controls, single waterjet,
eight box colliders. Final FBX, URP materials, prefab, JSON and shared-library
audio profile are integrated. No existing ship assets or scenes were replaced.

SerpukhovR96ModelIntegrator rebuilds the asset and catalogue entry. Catalogue
builder preserves it. Existing cameras, buoyancy and signed single-drive physics
are reused. Single masthead is optional in VesselLayout; other ships retain two.
Six-vessel menu spacing is tested. Select Серпухов, Р-96 (1973) then a passage;
the existing Serpukhov zaton admission limits allow this shallow-draft tug.

Final import integration-clean.log exited 0. Final EditMode 273/273 passed in
check-EditMode-20261026-075827-050.xml. Full PlayMode 165/165 passed in
check-PlayMode-20261025-234437-344.xml before final material-slot cleanup and
new menu test; focused final menu suite 5/5 passed in serpukhov-menu-final.xml.
Final shakedown passed all six vessels, check-Shakedown-20261026-075432-341.log.
Saved Blender geometry validation passed; eight Blender views, Unity LODs,
runtime chase/bridge and sixth-vessel menu were inspected. VALIDATION.md contains
the evidence inventory. Changes remain uncommitted.

Limits: monochrome period photos do not establish colors; fine offsets and hidden
fittings are inferred. Estimated waterjet force surrogate is not a flap transient
model. Trial speed 14.44 km/h is close to 14.9, but 61.01 m tactical diameter and
47.22 m crash stop differ from the original sheet. Not training validated.
Lower accommodation is not detailed; moving parts have pivots but no animation;
no new towing-coupling or freewalk system.

## 2026-10-26: MN-52 T-77 passive barge and Serpukhov push unit

Authored through registered Blender MCP, source and scripts in
`Art/Source/MN52_T77/`. Exact MN-52 photos examined: 89189, 32577, 4585,
35342, 508862. The underway photo is RT-291 alongside, not proof of the
requested Serpukhov centreline pairing. No verified lines plan recovered.
README documents period paint, fitted offsets, pipework and rigging uncertainty.
No reference photographs redistributed. Hull 28.8 x 6 x 1.5 m, guards about
6.29 m. LODs 41,264 / 23,393 / 13,552 triangles, 11 materials, 18 original maps.
Source has 386 meshes, UVs, three batched LODs and nine collision references.

MN52_T77.prefab has independent PassiveVessel/Rigidbody, no player controller,
zero actuators, 153000 kg loaded, 19 hydrostatic stations and 16 strips. Fitted
loaded/empty drafts 1.2/0.5 m. Shared MMG resistance/current/weather/bathymetry.
Serpukhov_MN52.prefab is a variant of the unchanged solo tug. PushUnit creates
a separate barge root and locks a ConfigurableJoint with pair-only collision
suppression, reversible Attach/Detach, save/reset support. Seven catalogue
commands; choose Серпухов + МН-52 for River or Gorodets. The 50.65 m unit is
excluded by the existing zaton 40 m limit. Existing ships/settings unchanged.
External camera look target is optional in VesselLayout; solo defaults preserved.

Final camera import exited 0. EditMode 277/277 passed in
check-EditMode-20261026-132724-794.xml. Full PlayMode 173/173 passed in
check-PlayMode-20261026-131701-439.xml before the camera-only correction.
Forward/reverse, both turns, coasting, barge world collisions, detached drift,
save/reset tested. Calm-water speeds solo4.012/pushed2.593/reverse-2.034 m/s.
Initial actual-scene RunPushUnits passed both vessels. See VALIDATION.md for
final camera capture evidence. No player build or human handling trial claimed.

Limits: fitted underwater offsets, period paint and hidden fittings inferred;
coupling not documented historical hardware. Cabin blocks navigator forward
view; external views frame the combination. HUD/grounding scoring remains
tug-specific. No cargo-transfer UI, tank slosh or shader-wave force sampling.
No player-facing detach controls. Prototype physics is not training validated.
Changes remain uncommitted; ChangedFiles.txt inventories the handoff.
