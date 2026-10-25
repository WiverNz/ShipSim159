# Built Windows player performance

Measured 2026-10-25. Status: completed on the available host with documented limitations.
Target-hardware acceptance and elimination of cold-path stalls remain open.

## Build and conditions

`LocalWindowsBuild.Run`, Unity 6000.6.0f1, Windows x64 Mono Development, no script
debugging/deep profiling, frame timing enabled. Three existing enabled scenes,
Gorodets first. Output `build/StandaloneWindows64/ShipSim159.exe`.
`Logs/Part17/build-final.log`: PASS; BuildReport Succeeded, 0 errors, 13 warnings
(deprecated FindObjectsByType overloads among compiler warnings), 318378761 bytes.
Package `release-smoke` passed. No release script, commit, tag or push.
Actual Windows GUI run, not editor/batchmode/nographics: `player-final.log` and
`PlayerFinal/result.txt` PASS. All 27 measured cases and 15 vessel/scenario switches finished.

Host: 13th Gen Intel(R) Core(TM) i9-13900K, NVIDIA GeForce RTX 4090 (24138 MB), 32451 MB RAM.
Windows 11  (10.0.22631), Direct3D 12 [level 12.2].
1920x1080 windowed, quality PC, VSync 0, targetFrameRate -1. Audio enabled at 0.8,
background mute off. Fixed timestep 0.02 s, simulation 1x during sampling.
The whole-scene target is 60 Hz / 16.67 ms at 1080p on project target hardware.
RTX 3060 target hardware is unavailable: these RTX 4090 results do not establish
its acceptance. The isolated editor vessel budget remains 8 ms p95 and is checked
separately; Camera.Render/readback figures are not player GPU time.

Twelve main cases: 10 s warmup and 30 s measurement. Moving-audio has 20 s extra
acceleration; quay-lines 30 s hauling; anchor-grounding 30 s payout/veer. Fifteen
soak cases: 5 s warmup and 15 s measurement, alternating Gorodets/River, three rounds
of all five vessels. Total process run was about 15 minutes. Camera positions,
load/mass, weather, source/voice counts and all settings are in `part17-player.json`.
507B is fully loaded except empty-open. LOD path moves 100-550 m and back.
Anchor-grounding uses a synthetic 3.48 m sand bottom with Gorodets visuals, not a
claim about real geography. This test depth function is not saved as scenario data.

Frame interval uses unscaledDeltaTime. CPU/GPU use timestamp-deduplicated
FrameTimingManager samples from the actual player. GPU samples may be delayed or
unavailable on individual frames; do not correlate a single row's GPU timing with
that row's CPU spike. ProfilerRecorder measures CPU subsystem time. Storage is
preallocated; CSV/JSON/screenshot writes and scene transitions are outside samples.
No graphical or physical system was disabled to meet a numerical budget.

## Final frame-time results

| Case | Frames | Frame median/p95/p99/max ms | CPU p95 ms | GPU p95 ms | Frames >50 ms |
|---|---:|---|---:|---:|---:|
| anchor-grounding | 12116 | 2.23/3.04/10.10/24.22 | 3.04 | 1.07 | 0 |
| empty-open | 11456 | 2.22/3.44/10.53/23.47 | 3.43 | 1.13 | 0 |
| far-auto-lod | 10669 | 2.45/3.47/11.95/26.20 | 3.46 | 1.15 | 0 |
| full-open | 11372 | 2.24/3.48/10.57/25.52 | 3.48 | 1.13 | 0 |
| heavy-weather | 11030 | 2.31/3.56/10.77/24.25 | 3.55 | 1.21 | 0 |
| lod-transition | 10554 | 2.42/3.60/11.57/26.68 | 3.59 | 1.28 | 0 |
| mechanisms | 11641 | 2.24/3.24/10.54/35.41 | 3.23 | 1.13 | 0 |
| moving-audio | 11348 | 2.27/3.39/10.53/24.55 | 3.39 | 1.15 | 0 |
| navigator | 7570 | 3.60/4.55/16.17/33.97 | 4.55 | 1.82 | 0 |
| near-day-full | 12188 | 2.18/3.11/10.33/24.42 | 3.11 | 1.05 | 0 |
| night | 11657 | 2.22/3.27/10.50/24.64 | 3.26 | 1.16 | 0 |
| quay-lines | 10840 | 2.41/3.46/11.35/26.27 | 3.46 | 1.15 | 0 |
| soak-0-luch-14352 | 3896 | 3.58/4.36/15.24/24.21 | 4.36 | 1.98 | 0 |
| soak-0-meteor-342u | 5026 | 2.64/3.41/13.22/20.59 | 3.40 | 1.42 | 0 |
| soak-0-volgobalt-295ar | 5213 | 2.57/3.34/12.71/19.76 | 3.34 | 1.33 | 0 |
| soak-0-volgodon-507b | 3569 | 3.80/4.83/17.35/26.08 | 4.82 | 1.94 | 0 |
| soak-0-volgoneft-1577 | 3875 | 3.53/4.65/15.77/24.45 | 4.62 | 1.80 | 0 |
| soak-1-luch-14352 | 5191 | 2.60/3.31/12.92/19.91 | 3.31 | 1.43 | 0 |
| soak-1-meteor-342u | 3842 | 3.55/4.38/16.36/17.62 | 4.39 | 1.91 | 0 |
| soak-1-volgobalt-295ar | 3929 | 3.50/4.30/15.81/17.04 | 4.29 | 1.78 | 0 |
| soak-1-volgodon-507b | 4684 | 2.83/3.71/13.55/20.30 | 3.71 | 1.40 | 0 |
| soak-1-volgoneft-1577 | 5137 | 2.61/3.42/12.69/19.36 | 3.42 | 1.35 | 0 |
| soak-2-luch-14352 | 3892 | 3.58/4.32/15.46/31.43 | 4.32 | 1.97 | 0 |
| soak-2-meteor-342u | 5153 | 2.62/3.36/12.92/22.95 | 3.36 | 1.43 | 0 |
| soak-2-volgobalt-295ar | 5207 | 2.57/3.36/12.72/20.52 | 3.36 | 1.35 | 0 |
| soak-2-volgodon-507b | 3478 | 3.85/4.92/17.31/25.59 | 4.91 | 1.93 | 0 |
| soak-2-volgoneft-1577 | 3944 | 3.50/4.27/15.53/31.05 | 4.26 | 1.82 | 0 |

Final p95 is 3.04-4.92 ms, but one soak p99 is 17.35 ms and rare frames exceed
16.67 ms. This is not an invariant 60 Hz floor. No final-main-run sample exceeded
50 ms; the independent cold-path repeats below did, and must not be omitted.

## GC, memory and render counters

| Case | GC bytes/frame median/p95 | Gen0 collections | Unity used peak MiB | SetPass p95 | Triangles p95 |
|---|---:|---:|---:|---:|---:|
| anchor-grounding | 11764/11865 | 133 | 870.7 | 113 | 3426012 |
| empty-open | 11448/11549 | 122 | 852.3 | 103 | 3476951 |
| far-auto-lod | 11264/11365 | 112 | 858.6 | 109 | 4129698 |
| full-open | 11430/11531 | 121 | 853.7 | 103 | 3488183 |
| heavy-weather | 11284/11385 | 116 | 873.4 | 116 | 3545047 |
| lod-transition | 11264/11369 | 111 | 857.3 | 183 | 4371970 |
| mechanisms | 11484/11593 | 126 | 851.9 | 109 | 3486059 |
| moving-audio | 11328/11429 | 120 | 872.1 | 124 | 3523057 |
| navigator | 11272/11373 | 80 | 890.9 | 219 | 6544685 |
| near-day-full | 11264/11365 | 128 | 807.4 | 103 | 3486931 |
| night | 11264/11365 | 122 | 869.3 | 107 | 3551964 |
| quay-lines | 14876/14977 | 152 | 877.2 | 147 | 4020424 |
| soak-0-luch-14352 | 6947/7124 | 25 | 996.2 | 246 | 7068726 |
| soak-0-meteor-342u | 7066/7127 | 31 | 945.4 | 190 | 5068154 |
| soak-0-volgobalt-295ar | 6918/6979 | 34 | 931.7 | 143 | 4878008 |
| soak-0-volgodon-507b | 11328/11429 | 32 | 992.7 | 231 | 6975679 |
| soak-0-volgoneft-1577 | 7144/7205 | 27 | 972.5 | 204 | 6779032 |
| soak-1-luch-14352 | 6664/6812 | 31 | 939.3 | 181 | 5109540 |
| soak-1-meteor-342u | 7378/7451 | 21 | 991.6 | 247 | 7056508 |
| soak-1-volgobalt-295ar | 7144/7205 | 21 | 990.9 | 199 | 6734772 |
| soak-1-volgodon-507b | 11102/11203 | 44 | 948.9 | 174 | 4864953 |
| soak-1-volgoneft-1577 | 6918/6979 | 33 | 940.9 | 155 | 4934348 |
| soak-2-luch-14352 | 6947/7124 | 19 | 997.3 | 249 | 7066370 |
| soak-2-meteor-342u | 7066/7127 | 30 | 942.7 | 190 | 5072188 |
| soak-2-volgobalt-295ar | 6918/6979 | 32 | 942.5 | 143 | 4878008 |
| soak-2-volgodon-507b | 11328/11429 | 31 | 1002.9 | 231 | 6975811 |
| soak-2-volgoneft-1577 | 7144/7205 | 20 | 991.4 | 204 | 6779060 |

Draw Calls Count reported unusable zero despite nonzero SetPass/triangles on D3D12:
**draw calls are unavailable**, not zero work. SetPass/triangles are whole-scene
counts including extra cameras, not vessel mesh budgets. Mono process memory
properties also returned zero; independent Windows process counters were sampled.
Process samples: 2026-10-25T10:24:21.4554155Z to 2026-10-25T10:39:13.5224278Z.
Windows working_set_bytes: peak 1552.4 MiB.
Windows private_bytes: peak 2589.5 MiB.

Matched Gorodets cases grew from 957.0 to 968.8 MiB Unity used memory for 507B,
and 958.3 to 971.2 MiB for Luch from rounds 0 to 2. This small increase is retained,
not described as zero growth or proof of no leak. This is not an hours-long soak.
Every measured case had one AudioListener and one VesselSoundscape. GC values
include the whole game/UI and instrumentation overhead. No existing whole-game
GC byte budget was found, so no zero-allocation acceptance threshold was invented.

## CPU subsystem p95, milliseconds per frame

Zero frames without a physics step are included. Markers nest: tackle is included
in ship physics and must not be added a second time. Water is reflection submission
and filter-dispatch CPU cost, not water GPU cost. Audio is control-layer Tick cost,
not DSP/mixer cost. Isolated water GPU and audio DSP costs remain unavailable.

| Case | PhysX | Ship physics | Lines | Mechanisms | Audio control | Tackle | Water submission |
|---|---:|---:|---:|---:|---:|---:|---:|
| anchor-grounding | 0.1000 | 0.4301 | 0.0062 | 0.0180 | 0.0222 | 0.0049 | 0.7205 |
| empty-open | 0.0985 | 0.4505 | 0.0030 | 0.0185 | 0.0210 | 0.0013 | 0.7301 |
| far-auto-lod | 0.1016 | 0.4833 | 0.0034 | 0.0211 | 0.0235 | 0.0015 | 0.8374 |
| full-open | 0.0989 | 0.4805 | 0.0031 | 0.0188 | 0.0216 | 0.0014 | 0.7389 |
| heavy-weather | 0.0989 | 0.4833 | 0.0032 | 0.0192 | 0.0209 | 0.0013 | 0.7485 |
| lod-transition | 0.1010 | 0.4843 | 0.0033 | 0.0206 | 0.0231 | 0.0015 | 0.8173 |
| mechanisms | 0.0988 | 0.4813 | 0.0030 | 0.0189 | 0.0225 | 0.0013 | 0.7269 |
| moving-audio | 0.0984 | 0.4824 | 0.0030 | 0.0181 | 0.0217 | 0.0013 | 0.7499 |
| navigator | 0.1074 | 0.5178 | 0.0040 | 0.0252 | 0.0297 | 0.0022 | 1.1821 |
| near-day-full | 0.0987 | 0.4760 | 0.0030 | 0.0184 | 0.0205 | 0.0013 | 0.7267 |
| night | 0.0993 | 0.4781 | 0.0030 | 0.0181 | 0.0203 | 0.0013 | 0.7533 |
| quay-lines | 0.1106 | 0.5246 | 0.0188 | 0.0195 | 0.0273 | 0.0425 | 0.8038 |

## Rejected runs, corrections and matched comparison

The first startup failed with a missing navigation shader. Explicit Resources
materials now retain navigation and transparent rain variants referenced only by
Shader.Find. The next full run failed while switching to Luch: reflected-camera
frustum reconstruction. Conservative ordinary mirrored culling fixes that error;
oblique water-plane raster clipping and reflection filtering remain enabled.
Final EditMode 269/269 and PlayMode 161/161 pass. The entire player run was repeated.
The rejected runs remain in Logs/Part17 and are not counted as successful endurance.

| Matched case | Before frame p95 ms | After frame p95 ms | Before GPU p95 ms | After GPU p95 ms |
|---|---:|---:|---:|---:|
| anchor-grounding | 3.19 | 3.04 | 1.28 | 1.07 |
| empty-open | 3.21 | 3.44 | 1.15 | 1.13 |
| far-auto-lod | 3.60 | 3.47 | 1.31 | 1.15 |
| full-open | 3.35 | 3.48 | 1.14 | 1.13 |
| heavy-weather | 4.16 | 3.56 | 1.42 | 1.21 |
| lod-transition | 3.69 | 3.60 | 1.31 | 1.28 |
| mechanisms | 3.75 | 3.24 | 1.33 | 1.13 |
| moving-audio | 3.94 | 3.39 | 1.31 | 1.15 |
| navigator | 4.79 | 4.55 | 2.12 | 1.82 |
| near-day-full | 3.21 | 3.11 | 1.23 | 1.05 |
| night | 3.52 | 3.27 | 1.18 | 1.16 |
| quay-lines | 3.72 | 3.46 | 1.34 | 1.15 |
| soak-0-meteor-342u | 3.52 | 3.41 | 1.66 | 1.42 |
| soak-0-volgobalt-295ar | 3.54 | 3.34 | 1.62 | 1.33 |
| soak-0-volgodon-507b | 4.92 | 4.83 | 2.14 | 1.94 |
| soak-0-volgoneft-1577 | 4.76 | 4.65 | 2.22 | 1.80 |

These are matched host/scenario conditions, not a controlled GPU microbenchmark.
No broad performance optimization was claimed from normal run-to-run variation.

## Independent LOD repeats and unresolved cold-path latency

New player process, same hardware/quality/load/camera path, three repetitions,
each 10 s fixed-view warmup and 30 s measured movement. No editor ran alongside.

| Case | Frames | Frame median/p95/p99/max ms | CPU p95 ms | GPU p95 ms | Frames >50 ms |
|---|---:|---|---:|---:|---:|
| lod-transition-repeat-0 | 11039 | 2.29/3.39/11.10/234.93 | 3.39 | 1.33 | 2 |
| lod-transition-repeat-1 | 11223 | 2.29/3.32/11.16/31.97 | 3.31 | 1.35 | 0 |
| lod-transition-repeat-2 | 11571 | 2.28/3.15/10.89/26.46 | 3.14 | 1.51 | 0 |

The first rejected full run had 175/259 ms stalls. The new-process first LOD repeat
again had two >50 ms frames, maximum 234.93 ms; subsequent repeats peaked at
31.97/26.46 ms. This supports a first-use-path association but does not identify
shader/driver/resource preparation as the cause. No speculative quality reduction
or unrelated optimization was made. Cold-path stall elimination remains an open
performance limitation requiring a detailed CPU/driver capture. Warm p95 passing
must not conceal these first-pass stalls.

Other retained limits: native JobTempAlloc shutdown warnings without a diagnosed
project leak; Unity's warning for stripped unused Gaussian DOF (volume mode off);
cloud telemetry DNS warnings in logs. Target RTX 3060 acceptance, trustworthy draw
calls, isolated GPU subsystem/DSP cost and subjective audio listening remain open.
The final runtime PASS means all scenario/error/lifecycle checks finished, not
that every frame met 16.67 ms or every native shutdown allocation was explained.

## Reproduction and evidence

See Tools/README.md for build/run commands and Tools/summarize-player-profile.py.
Full: `--shipsim-profile --profile-output <folder>`.
Repeat: `--shipsim-profile --profile-case lod-transition --profile-repeat 3 --profile-output <folder>`.
JSON summaries: `part17-player.json`, `part17-player-before-reflection.json`,
`part17-player-lod-repeat.json`. Raw final/repeat CSVs and process counters:
`Performance/*.csv.gz`. Local original logs, saves, all screenshots: `Logs/Part17/`.

[Day](Previews/part17-player-near-day-full.png),
[bridge](Previews/part17-player-navigator.png),
[night](Previews/part17-player-night.png),
[weather](Previews/part17-player-heavy-weather.png),
[moored](Previews/part17-player-quay-lines.png),
[anchor/ground](Previews/part17-player-anchor-grounding.png),
[Luch after switching](Previews/part17-player-soak-2-luch-14352.png).
