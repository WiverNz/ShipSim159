# Part 17: integrated reproducibility and acceptance

Date: 2026-10-24 to 2026-10-25. Status: completed with documented limits;
final integration and available-host built-player run passed. This is an engineering/gameplay prototype, not a
certified maritime trainer or a measured copy of an identified vessel.

## Audit of Parts 9-15

The working tree was clean at the start. Read the reference decisions, Part 10-15
reviews, JSON/CSV evidence, retained previews and current architecture before edits.
Part 15 is implemented, including the subsequent shared five-vessel audio library.
The retained final Part 15 XML proves 267/267 EditMode and 107/107 PlayMode.
Older report statements such as "other vessels silent" and "visual-only anchors"
are superseded by Part 15 section 10 and Part 14, respectively.

Mandatory follow-ups found here: contact geometry (Part 16); standalone player
performance; a stale lightship image used the pre-Part-11 0.915 m level draft.
The quality gate now derives full/lightship attitude from current loading and
section hydrostatics. No historical source coefficient or hull fit was silently
changed. The initial anchor comparison also retained a colliding previous ship;
Part 16 isolates those independent experiments and retains the original assertions.

## Blender, exports and integration

Registered Blender MCP only. Blender 5.2.2 LTS, addon 1.8, protocol 14 versus
server expectation 15 initially. After the user restarted Blender with an updated addon,
protocol 15 was confirmed with no warning. Native calls worked. No shell Blender,
socket or PowerShell connection was used.

Opened `VolgoDon507B.blend`, verified 51 local editable source meshes and all 33
file images decoded and packed/available. The separate hydrostatic mesh is closed,
manifold and positively wound, volume 11013.68875 m3. Mechanism validation checked
15 bindings and 621 hatch frames with no panel collision; two gypsies each checked
at 36 angles, no unexpected contact. Designed closed-cover seal contacts remain.

Re-exported all three FBX files with `part8_export.py`, redirecting its report to
`part17-source.json` so historical reports survive. Geometry signatures match
Part 13 at every LOD: 59543 / 36044 / 24474 triangles. Reopened the saved source
again and verified all 146 mesh signatures across three scenes. Evidence:
`part17-source-validation.json`, `part17-mechanisms-validation.json`,
`part17-source.json`, `part17-reproducibility.json`.

Unity `VolgoDonQualityGate.Run -reviewStage 17` reimports through the normal
integrators, installs twice, compares the physics root/Rigidbody/collider state,
and checks moving LOD attachment, materials, normals/UV availability, resource
budgets and axes/bounds. First run PASS: `Logs/Part17/quality.log`.
Bounds: 138.29999 x 16.70003 m; no old CRYENGINE scaling was reapplied.

## Provenance and reference comparison

All 45 protected original model assets match Part 9 SHA-256, including the original
FBX and its textures/materials: `part17-protected-hashes.json`. The new contact
meshes are deterministic derivatives of the existing corrected hull. No external
mesh, texture, photograph or audio was introduced in Parts 16-17. The original
mesh/texture redistribution license remains unestablished, not newly cleared.
Part 15's shared library retains its CC0 analogue-recording/source registry and
original synthesis, with no claim of authentic 507B recordings.

Re-downloaded the [archive](https://russrivership.ru/public/files/doc257.pdf) into
`/tmp`, matching SHA-256 `841d1aeb590193e92ab2a848d6aaa516ec7c1cc498fa2e3dfee6f7db21979e4b`.
Reviewed the page-1 arrangement and [Volgo-Don 185 photograph 7323](https://fleetphoto.ru/photo/7323/)
(Ponch, August 2003), obtained temporarily when the web viewer could not open it.
Neither reference was copied into game resources. The long low cargo run, raised
bow and aft accommodation are consistent at the arrangement level. The source
photograph has different paint, bow fittings, mast and window proportions; it does
not authenticate this model's identity. Perspective/loading prevent a dimensional
fit from that image. Part 9's source applicability and Part 10's unresolved 0.743 m
family-body-plan trace discrepancy remain unchanged.

## Limits retained

- Unknown named vessel, date, original fittings and livery; family raster, not
  authenticated 507B offsets. Estimated ends, station mapping and upper hydro cap.
- Full-load draft 3.495 m from estimated lightship LCG; unballasted lightship is
  stern-trimmed outside the declared hydrostatic-table range. No tank free-surface
  correction, cargo shift, flooding, structural strength or large-angle stability.
- Open-hold volume 5194 versus archive 6370 m3 unresolved; nominal cargo geometry
  and fixed physics cargo CG differ at partial load.
- Stopping/turning trials lack comparable historical conditions. No coefficients
  were refitted; estimated envelopes and deep/shallow/loading distinctions remain.
- Estimated machinery timings, missing hydraulic/wire details, quasi-static chains
  and ropes, estimated soil/rope properties, no fender springs or bottom suction.
- Convex contact approximation, no decorative/deck-walking collisions; Part 16
  quantifies coverage and the tested scenario/step envelope.
- Audio is analogue/synthesis, with subjective listening still outstanding.
- RTX 3060 target acceptance cannot be inferred from the available RTX 4090 host.

Blender viewport helper limitation: `look` returned exactly
`Couldn't capture the view: [Errno 2] No such file or directory: '/tmp/blender_look_1702511.png'`.
The bpy source audit/export and reopened signatures succeeded through the registered
connection; Unity image capture works. No alternate Blender connection was attempted.

After the user restart, reopened the final blend through MCP and verified the archived
Part 9 hull hash too: `a0c1ff0fdf69f44b42d8dca29873c55ca090f95a9d9de59a2b9dbdf16f71d8fb`.
The viewport helper still returned the same missing-file error. A normal bpy render
through the registered MCP succeeded: [source view](Previews/part17-blender-source.png).
Final integration gate passed: `Logs/Part17/quality-final.log`, including corrected
lightship attitude and rear elevation capture.

## Final regression evidence

- EditMode: 267/267, `TestResults/check-EditMode-20261024-222559-811.xml`.
- PlayMode: 158/158, `TestResults/check-PlayMode-20261024-222631-703.xml`.
- Menu/new voyage/save/change vessel/change scenario/load/pause: PASS,
  `Logs/check-Menu-20261024-223029-204.log`.
- Five-vessel shakedown: PASS, `Logs/check-Shakedown-20261024-223052-968.log`.
- Numerical trials: PASS, `Logs/Part17/trials.log`; all 15 table rows match the
  Part 12 after-report exactly (`part17-trial-comparison.json`). Earlier Part 9/11
  results include different hull/load models and are not interchangeable baselines.
- Mechanisms, hold loading and ground tackle runtime: PASS, respectively
  `Logs/Part17/machinery.log`, `holds.log`, `tackle.log`.
- `Logs/Part17/verify.ps1` completed with `FINAL_CHECKS|PASS`.

The suites cover old-save defaults, independent engines/reversal/nozzles, load and
ballast, intermediate mechanism saves, anchor/mooring state, pause and accelerated
simulation, audio event/lifecycle/mute checks. This is automated evidence, not a
claim that every historical save file or subjective sound quality was reviewed.

## Visual acceptance index

| View | Evidence |
|---|---|
| Front / rear | [front](Previews/part17-unity-front.png), [rear](Previews/part17-unity-back.png) |
| Side / top / perspective | [side](Previews/part17-unity-side.png), [top](Previews/part17-unity-top.png), [perspective](Previews/part17-unity-perspective.png) |
| Underbody / propellers / nozzles | [underwater](Previews/part17-unity-underwater.png), [steering limit](Previews/part17-machinery-nozzles-port-limit.png) |
| Empty / half / full hold | [empty](Previews/part17-hold-fore-open-empty.png), [half](Previews/part17-hold-fore-open-half.png), [full](Previews/part17-hold-fore-open-full.png) |
| Intermediate machinery | [lifting](Previews/part17-machinery-outer-lifting.png), [sliding](Previews/part17-machinery-inner-sliding.png), [half open](Previews/part17-machinery-half-open.png) |
| Anchor / mooring | [anchored](Previews/part17-anchor-at-anchor.png), [underwater chain](Previews/part17-anchor-at-anchor-underwater.png), [berth](Previews/part17-berth-overview.png), [springs](Previews/part17-berth-springs.png) |
| Day / night / bridge | [day](Previews/part17-unity-perspective.png), [night](Previews/part17-unity-night.png), [bridge](Previews/part17-unity-bridge.png) |
| LOD0 / LOD1 / LOD2 | [LOD0](Previews/part17-unity-lod0.png), [LOD1](Previews/part17-unity-lod1.png), [LOD2](Previews/part17-unity-lod2.png) |
| Source / corrected lightship attitude | [Blender](Previews/part17-blender-source.png), [lightship](Previews/part17-unity-lightship.png) |

The pictures retain coarse underbody faceting, generic paint/window/mast details
and the documented lightship trim limitation. Open empty holds are dry; nominal
cargo fills change with actual loading, subject to the Part 12 volume/CG discrepancy.

MCP follow-up, 2026-10-25: registered Windows `uvx.exe`,
`--host 127.0.0.1 --port 9876`: status, `execute_blender_code` and
`look(mode="viewport")` succeeded, protocol 15 matches. The earlier screenshot
transport limitation is resolved; no configuration edit was needed.


## Built-player defect and regression

The first actual player smoke failed with `ArgumentNullException: shader` in
`DayNightController.CreateUnlitMaterial`. Shader.Find-only navigation/rain shader
references were absent from build dependencies. `RuntimeShaderBuildAssets` now
retains two small Resources materials and the transparent particle keyword, through
an editor build preprocessor used by local and ordinary builds. No visual system
was disabled. EditMode after this fix passed 268/268:
`TestResults/check-EditMode-20261025-110101-601.xml`. Rebuild and package smoke passed;
`Logs/Part17/player-smoke-fixed.log` and `PlayerSmokeFixed/result.txt` prove the
actual player startup and first measured case passed. The rejected first run is
retained in `Logs/Part17/player-smoke.log`.


Second player-only defect: the first long run failed on Luch during a scene switch:
`Screen position out of view frustum`, from reflected-camera culling. Reflection
now uses an explicit conservative ordinary mirrored culling matrix while retaining
the oblique water-plane projection for rasterization. Mirrored-view baked occlusion
is disabled; water reflection remains rendered and filtered. The 40-pose camera
regression covers heights 0.11-40 m and grazing/downward/upward angles.
See Unity's [cullingMatrix API](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/camera/cullingmatrix).
Final EditMode 269/269: `TestResults/check-EditMode-20261025-111830-366.xml`.
Final PlayMode 161/161: `TestResults/check-PlayMode-20261025-111903-137.xml`.
The first long player run remains rejected; it is not used to claim a successful soak.


## Final mandatory-item disposition

| Required item | Status and evidence |
|---|---|
| Audit Parts 9-15, close mandatory local follow-ups | Completed; audit above, contact and player-only defects fixed |
| Part 16 coverage, builder, root/mass/CG/inertia | Completed with convex approximation limits; Part 16 report and integration snapshots |
| Contact, bank, light/heavy shoal, reverse/lateral recovery, taut moorings, chain/ground reaction | Completed in the tested load/step envelopes; 161 PlayMode tests; no universal refloating guarantee |
| Source reopen, editability, textures, mechanisms, hydro surface, three exports | Completed through registered MCP; signature reports, protocol 15 and working look |
| Legacy resources and archive hull hashes | Completed; 45 protected files and archived geometry match |
| New resources and provenance | Completed for project-authored contact/material assets; legacy license remains unresolved |
| Unity scale, pivots, normals/materials, LODs, mechanisms, cargo, lights, lines, audio | Completed with retained fidelity limits; integration/runtime reports and images |
| EditMode / PlayMode | Completed, 269/269 and 161/161 after player fixes |
| Numerical trials / five-vessel check / menu, save/load, loading, ballast, engines | Completed; earlier final numerical/menu/shakedown evidence unchanged by rendering fix; actual player soak repeated all five |
| Local Windows x64 build and package smoke | Completed; LocalWindowsBuild and release-smoke PASS |
| Actual Windows player performance and lifecycle | Completed with measurement limits; 27 measured cases and 15 scene/vessel switches, PLAYER_PROFILE PASS |
| Target RTX 3060 acceptance | Blocked: target machine unavailable; RTX 4090 figures are separate |
| Draw calls, isolated water GPU and audio DSP cost | Blocked by available instrumentation; other CPU/GPU/GC/memory/SetPass/triangle measurements recorded |
| Visual acceptance and discrepancy ledger | Completed with historical/geometry limits; image index above and ballast image below |
| Subjective audio quality / historic authenticity | Blocked pending listening and unavailable vessel-specific evidence; no authenticity claim |
| Documentation and verified commands | Completed; plan/checklist/README/provenance/source/physics/operator/context and local runbook updated |

Final built-player evidence: [performance report](PLAYER_PERFORMANCE.md),
`Logs/Part17/PlayerFinal/result.txt` PASS, 12 main cases plus 15 soak cases.
The corrected reflection completed all three Luch transitions. No final measured
frame exceeded 50 ms; p95 ranges 3.04-4.92 ms on the available host. Rare frames
still exceed 16.67 ms and one soak p99 is 17.35 ms, so this is not a guarantee of
an invariant 60 Hz floor. Earlier 175/259 ms stalls remain in the rejected-run record.
[Ballast waterline](Previews/part17-unity-ballast.png) is a separate static view of
current calculated cargo-empty/full-ballast draft/trim, not a new hydrodynamic fit.

The player save/load/pause/4x/volume/mute checks completed, followed by all 15
vessel/scenario switches. Source/listener counts stayed at one in measured cases.
The synthetic anchor-case depth function is benchmark setup and is not serialized
as scenario geography. Real scenario bathymetry persistence retains its existing tests.
No commit, tag or push was made.

Cold-path performance limitation: a fresh-process LOD repeat reproduced two >50 ms frames
(maximum 234.93 ms); the next two identical repeats peaked at 31.97/26.46 ms.
First-use preparation is a hypothesis, not a diagnosed cause. Stall elimination
remains open; see PLAYER_PERFORMANCE.md. Final full-run PASS does not erase this finding.
