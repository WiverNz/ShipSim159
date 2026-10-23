# Part 8: distance detail and final quality review

Date: 2026-10-23. Classic two-covered-hold 507B remains the target. No new
vessel identity, archive interpretation or hydrodynamic coefficients were introduced.

## Delivered assets

`VolgoDon507B.blend` contains the original editable source scene and two derived
LOD scenes. All 33 file images are packed and decode after reopening. The primary
49-mesh geometry is unchanged. `part8_export.py`, run through registered Blender
MCP, regenerates the derived scenes and exports all three FBXs under
`Assets/ShipSimulator/Models/VolgoDon507B_Improved/`. The legacy FBX is untouched.

| Level | Triangles | Renderers | Material slots / potential forward submissions |
|---|---:|---:|---:|
| LOD0 | 53,130 | 49 | 50 |
| LOD1 | 34,429 | 49 | 50 |
| LOD2 | 24,072 | 42 | 43 |

Budgets: 54,000 / 36,000 / 25,000 triangles, at most 64 material submissions per
single level, and 128 MiB shared texture allocation. Measured texture allocation
is 82,087,012 bytes (78.28 MiB), 25 unique textures. Slots are not a whole-scene
draw-call measurement: shadows, depth passes and overlapping cross-fades add work.
Two adjacent levels can render during a transition.

Hull, main deck, hold and wheelhouse geometry retain their original silhouette.
Boats retain at least half their triangles. Pipework and miscellaneous details use
less aggressive reduction after bounds checks caught disappearing extrema.
LOD2 omits interior and small hatch jacks/seals. Material boundaries and UV pairs
remain separate. Derived propellers, nozzles, anchors and covers attach as identity
children of the original moving meshes, so they follow the existing mechanisms.

`VolgoDonLodIntegration` is called by the normal mechanism integrator, including
catalogue rebuilds. LOD screen-height thresholds are 0.55, 0.20 and 0.003 with
10% distance cross-fade bands. URP Lit already supports LOD cross-fade. Navigation
lights remain outside the LOD group. No new runtime motion system was added.

## Reproducibility and resource checks

`part8-reproducibility.json` records reopening the saved source, identical semantic
geometry on regeneration, packed resources and all 45 unchanged fallback resource
hashes. The two remaining Part 1 baseline entries are the subsequently changed
vessel JSON and playable prefab, so their old hashes are deliberately not expected.
FBX binary timestamps are not used as geometry identity.

`ShipSimulator.Editor.VolgoDonQualityGate.Run` imports the candidate, installs it
twice, requires the same serialized physics root, Rigidbody and collider settings,
then saves the playable prefab. It validates mesh attributes, supported shaders,
material references, LOD bounds, budgets and animated attachment transforms.
`part8-unity.json` records the actual counts and bounds: 138.30 m long, 16.70 m beam.

The existing three overlapping box colliders remain. They span roughly -65 to
+62 m longitudinally, with a narrower bow and stern; they are intentionally coarse
and omit the finest tips and equipment. Their low faces are around -3.2 to -3.55 m.
They are suitable for the existing broad collision response, not accurate hull
contact, propeller collision or walkable deck simulation. No non-convex dynamic
mesh collider was introduced.

## Performance interpretation

`part8-render-cost.json` measures each forced LOD at 1920x1080 with a dedicated
camera, 20 warmups and 100 samples. Camera submission plus synchronous pixel
readback is measured, including GPU synchronization, not a standalone player frame.

| Level | Median ms | p95 ms |
|---|---:|---:|
| LOD0 | 1.395 | 1.806 |
| LOD1 | 1.319 | 1.613 |
| LOD2 | 1.240 | 1.479 |

`part8-runtime-performance.json` records 600 advancing batch player frames during
the River scenario shakedown. On the RTX 4090 / i9-13900K host at the batch editor's
640x480 viewport, median wall interval was 1.336 ms, p95 1.507 ms and reported CPU
median 1.335 ms. GPU frame timings were unavailable. Batch presentation and editor
work make these unsuitable for estimating normal desktop FPS.

The practical target remains 60 Hz (16.67 ms total frame budget) at 1920x1080 on
the project's target hardware. That target is not established by this host check.
The isolated vessel budget is 8 ms p95 including synchronous readback on this host;
the whole-scene target needs a built-player GPU capture on the target machine.

`part8-loading-cost.json` profiles 1,000 warmed dimensional-model rebuilds while
alternating cargo mass. This includes the station array and preserved manoeuvring
state, but excludes Rigidbody property assignments. No model construction occurs
while inventory is unchanged. Measurements must be interpreted as host estimates,
not guarantees for every loading rate or computer.

## Verification

- EditMode: 228/228, `TestResults/check-EditMode-20261023-031918-126.xml`.
- PlayMode: 47/47, `TestResults/check-PlayMode-20261023-031956-404.xml`.
- Five-vessel shakedown: `VESSEL_SHAKEDOWN|PASS`,
  `Logs/VolgoDon507B/part8-shakedown.log`, captures/report in `Logs/Shakedown/`.
- Save/load/menu: exit 0, `MENU_SMOKE|PASS`,
  `Logs/check-Menu-20261023-081632-457.log`.
- Pure manoeuvring trials: exit 0, `SEA_TRIALS|PASS`,
  `Logs/VolgoDon507B/part8-trials.log`; numerical report `Logs/SeaTrials/sea-trials.md`.
  Passing this prototype envelope does not establish full-scale accuracy.
- Final import/integration/render audit: `VOLGODON_QUALITY|PASS`,
  `Logs/VolgoDon507B/part8-quality.log`.

The isolated draw-call counters return unavailable (`-1`) inside synchronous
editor rendering. The shakedown reports 48 draw calls and 48 SetPass calls at its
sample point, but batch presentation is not a representative whole-scene player
capture. Use the structural submission budgets above until a player Frame Debugger
capture is available. The Mono thread-allocation counter also returns unavailable
(`-1` bytes), not zero-allocation. Rebuild time is about 0.007 ms on this host;
allocation/GC behavior still needs a supported player profiler.

`Previews/part8-*.png` contains front, side, top, perspective, underwater machinery,
bridge, half-open covers, lightship, night, forced LOD comparisons and transition
views. Side/lightship pictures use a static reference plane at water level; the
lightship vertical offset illustrates the calculated draft, not a simulated pose.
Rigidbody loading/flotation behavior is covered by the existing PlayMode suite.
In-game shakedown/menu captures provide the water, HUD and active-vessel context.

## Final discrepancy ledger

| Remaining difference | Impact / next evidence needed |
|---|---|
| Exact vessel identity unknown | Do not combine later modernization details with this classic arrangement |
| Hull sectional offsets are not fully reconstructed | Source is editable, but exact hydrostatic geometry needs digitized approved offsets |
| Railings and small fittings alias at distance | Thin geometry needs temporal AA in the player; inspect transitions on target hardware |
| Coarse box collision hull | Fine contact and walkable surfaces remain separate future work |
| Source family dimensions differ from the archive's specific condition | Retain the documented 138.3 m / 6750 t / 3.53 m baseline; do not silently retune |
| No validated stopping/turning calibration | Approximate 19.8 km/h full speed; about 719 m stopping distance still differs from archival data |
| Wall-sided loading hydrostatics | Free-surface effects, realistic low-draft stability and certified loading limits are absent |
| Shaft ratio and machinery rates estimated | Obtain vessel-specific reduction and operating data |
| Anchors are visual mechanisms | No chain catenary, anchor holding force or mooring-line dynamics |
| Audio emitters have no production recordings | Source and license machinery/water audio separately |
| Target GPU frame budget unverified | Run a 1080p built-player profile on target hardware; batch CPU results are insufficient |

This is an editable, integrated simulator prototype, not validated maritime training.
The remaining work is evidence-driven refinement rather than replacement of the ship.
