# Part 15: audio production and integration

Date: 2026-10-24. Status: complete as prototype audio driven by the simulation state, with the
listening limitation in section 9. Physics, controls, machinery, saves, anchors and mooring (Parts
10-14) are unchanged: the numerical sea trials give the same rows as Part 14.
Part 16 (collision) is not started.

**No sound here is a recording of a Volgo-Don 507B, of its 6ChRN 36/45 main engines or of its deck
machinery.** The recordings are licensed (CC0) recordings of comparable equipment, labelled
"analogue recording"; some layers are original synthesis, labelled "synthesized approximation".
Levels, distances and the wheelhouse filter are estimated listening choices, not measurements.

## 1. Audit before the change

| Found | State |
|---|---|
| Audio assets | None in the project |
| `ShipTelemetryUI` horn | A synthesized 1.2 s two-tone clip on a 2D source, key H |
| Master volume | `VoyageSettings.Volume` on `AudioListener.volume`, one slider in Settings |
| Pause | `VoyageMenu` sets `AudioListener.pause` with `Time.timeScale = 0` |
| AudioMixer | None |
| Emitters | `DetailedVisual/AudioPoints`, eight empty transforms added in Part 5 (engines, bow water, propellers, two hatch points, windlass) |
| Event points | `GroundTackle.Changed` (Part 14, kept for this part); machinery state from `VesselMechanisms`; engine state on `EngineShaft` |
| Listeners | One `AudioListener` on the main camera of each scene (scene builders) |

The existing system was extended: the Part 5 points carry the new emitters, the master volume and
pause stay where they were, the horn is routed to a mixer bus.

## 2. Sounds and the state that drives them

Equipment from the archive (`doc257.pdf`, PART13_REVIEW and PART14_REVIEW): main engines 2 x
6ChRN 36/45, direct reversing, 375 rpm (shaft = engine); four-bladed propellers (sourced); hatch
covers lifted and lowered by hydraulic jacks from an EMN-1.5/95-1 pump, moved by two LE-61 electric
winches; windlass BE16-4 and stern capstan ShER (electric); mooring winches LEGShA2-1
(electrohydraulic). Nothing is played for equipment or operations the simulation does not have.

| Layer or event | Driven by (computed state) | Emitter |
|---|---|---|
| Engine firing, low and high layers (crossfade 150-230 rpm) | `ShaftRpm(i)`: pitch = rpm / reference rpm, so 6 cylinders four-stroke fire at rpm / 20 Hz; level from `EngineShaft.FuelFraction` (torque in the running direction) | `Engine_Port/Starboard` |
| Engine room | Shaft rpm (level and pitch) | `Engine_*` |
| Engine exterior / exhaust | Shaft rpm and fuel | `Exhaust_*` (estimated uptake) |
| Starting-air brake (loop) | `EngineShaft.IsAirBraking` during the model's reversal | `Engine_*` |
| Starting air (event) | `EngineShaft.Direction` changes (engaged or reversed) | `Engine_*` |
| Blade rate | Shaft rpm (four blades) | `Propeller_*_Audio` |
| Propeller wash | `PropellerThrustN(i)` over the bollard thrust at rated rpm; more spray as `PropellerImmersion` falls | `Propeller_*_Audio` |
| Hull flow, port and starboard | Speed through the water (`RelativeWaterVelocity`) | `Hull_Port/Starboard` |
| Bow wave | Surge through the water, ahead only | `Water_Bow` |
| Lapping | Low speed, more with true wind | `Water_Bow`, `Hull_Starboard` |
| Wind steady and gale | Wind relative to the ship (`WindVelocityMps` minus ship velocity) | non-positional |
| Hatch hydraulic pump | Covers moving vertically (lift, inner lift, lowering) | `HatchMachinery_Aft/Fore` |
| Cover winch and rolling | Covers sliding or the stack traversing | `HatchMachinery_*` |
| Hatch events | Drive starts and stops; direction change (clunk); end positions Closed, Stacked, HoldOpen (seat) | `HatchMachinery_*` |
| Chain running out | `ChainRateMps(i) > 0`: let go, veer, brake rendering | `Windlass_Port/Starboard`, `Capstan_Stern` |
| Chain over the gypsy | Heaving rate, at the link rate (rate / 4 chain diameters) | same |
| Windlass motor | Heave commanded and not stalled; pitch with speed | same |
| Brake released / set | Command enters or leaves let go or veer | same |
| Windlass start / stop | Motor state edges, including a stall | same |
| Anchor stowed, chain parted | `GroundTackle.ChangedAt` events | same |
| Mooring winch (hydraulic pump) | Line command haul or slack; pitch with line speed | ship `MooringPoint` |
| Line strain and creaks | Tension over breaking load, change of tension | ship `MooringPoint` |
| Line parted | `ChangedAt(LineParted)` | ship `MooringPoint` |

Not sounded, because not computed: cavitation, propeller ventilation noise, hull damage, chain
dragging on the bottom, engine start and stop as operations (the model has none: a stopped
command coasts the shaft down), auxiliary diesel generators, rain on deck.

## 3. Assets

36 game files, now in the shared library `Assets/ShipSimulator/Audio/Library/` (section 10; 26.1 MB
WAV, 16-bit): 18 loops, 18 one-shots. The registry with author, URL, exact license, attribution,
edits and status of every file is `Art/Source/Audio/AUDIO_SOURCES.md` (human) and
`Art/Source/Audio/audio-manifest.json` (machine), both written by `Art/Source/Audio/build_ship_audio.py
--build`; `Art/Source/Audio/sources.json` pins each source by preview URL and SHA-256.

- Sources: 19 Freesound recordings, each checked on its own page on 2026-10-24: all CC0 1.0
  (no attribution required; authors credited). Freesound's high-quality Ogg previews were used
  (CC0 covers them; originals need an account). They are kept in `Art/Source/Audio/Sources/`
  (21.8 MB). Raw HTML snapshots of the sound pages were removed again: GitHub push protection
  rejected them because the pages embed site tokens.
- Excluded: the craigsmith "Vintage" library (stated to be copies of 1930s-60s studio effects,
  rights unclear), a recording with a large DC offset (Stena Danica engine room), anything from
  video.
- Analogues: engine room (Cape May-Lewes ferry), engine exterior (a 135 m Danube cargo vessel),
  propeller wash (ferry wake), hull flow (Amazon cargo boat), lapping (ship's bow), bow wave
  (steel barge), wind (open air; gale over reservoirs), hydraulic pack (vehicle ramp), winch
  motor (geared lighthouse motor), cover rolling (heavy trolley), chain heave (a ship's anchor
  being raised), chain running (chain-ferry chain), mooring hawsers and rope creaks, metal and
  steel-door impacts, air releases.
- Synthesized: the firing rhythm (six-cylinder four-stroke, firing order 1-5-3-6-2-4, per-cylinder
  and per-cycle variation, references 100 and 300 rpm), the blade rate (four blades, 300 rpm) and
  the line and chain parting snaps. The recorded engines fire at 10-16 cycles per second (about
  1200-1600 rpm), far faster than 375 rpm, so their own firing lines were high-passed away and the
  slow-engine rhythm is synthesized from the simulated rpm. The 507B's engines therefore sound
  like an approximation, not like a 6ChRN 36/45.

Processing (`build_ship_audio.py`): mono sum (wind kept stereo), high-pass 15-220 Hz per layer
(removes DC and the source boats' own engine lines), notches at hum and engine lines, de-click of
isolated impulses, 0.25-3 s equal-power cross-fade loops (the chain-heave seam placed in a gap
between clanks), circular filtering of the synthesized loops, fades on one-shots. Levels are set
per category (-17 to -26 LUFS for loops, peak -3 to -9 dBFS for events), deliberately not equal;
every file peaks at or below -1 dBFS without limiting. Report: `part15-audio-assets.json` (peak,
loudness, DC, seam step ratio 0.02-1.03 of the signal's own 99th-percentile step).

Loops are 4-26 s; each voice starts at a seeded random point, the two engines and the two hull
sides never play in phase, and the levels move with the state, so repetition is broken up.

Import (`ShipAudioImportRules`, by file name): loops Vorbis quality 0.5, Compressed In Memory;
one-shots ADPCM, Compressed In Memory; all preloaded, none streamed (all short; streaming would
cost a file stream and buffer per voice for no memory gain). Force To Mono is off: Unity also
normalizes when it downmixes, which would undo the authored levels; the files are mono already.
Measured runtime memory of the 36 clips: 3.2 MB.

## 4. Integration

- `Scripts/Audio/VesselSoundscape` on the 507B root, profile `Audio/Profiles/volgodon-507b.asset`;
  `LoopVoice` per layer (volume and pitch glide with attack and release time constants of
  0.05-0.8 s; a silent voice is stopped), `OneShotPool` of eight sources (oldest stolen),
  `AudioMix` for the mixer. It reads the state in `LateUpdate` at execution order 150 (after the
  physics, the machinery and the cameras) and writes nothing back.
- Physics additions, read-only: `EngineShaft.Direction`, `FuelFraction`, `IsAirBraking`;
  `ShipPhysicsController.PropellerThrustN`, `PropellerImmersion`, `RestoreCount`;
  `GroundTackle.ChainRateMps`, `LineRateMps`, `StationSpec` and the `ChangedAt` event (anchor or
  station index). `Changed` is unchanged.
- Emitters: the eight Part 5 points plus `Exhaust_Port/Starboard` (estimated: the model has no
  funnel object; behind the wheelhouse at 11.5 m), `Hull_Port/Starboard`, `Windlass_Port/Starboard`
  (the gypsies in `mechanisms.json`) and `Capstan_Stern` (estimated). Mooring winches sound at the
  ship mooring points. `VolgoDonAudioIntegration` adds them and is called by the mechanism
  integrator, so a prefab rebuild keeps them.
- Spatial: logarithmic roll-off, minimum distance 8-25 m by source size, spread up to 140 degrees
  for the hull; Doppler 0, because every camera rides with the ship and Doppler would only report
  view changes and orbiting. Wind is non-positional around the listener.
- Wheelhouse: with the listener inside an estimated box around the navigator eye (half extents
  6 x 1.6 x 3.2 m) the mixer low-passes engines (1.8 kHz, -3 dB), water (700 Hz, -10 dB), wind
  (500 Hz, -14 dB) and machinery (1.2 kHz, -8 dB) over 0.12 s. This is an approximation, not a
  model of the superstructure; there is no interior geometry or reverb.
- Mixer `Resources/Audio/ShipSimMixer.mixer`, built by `ShipAudioBuilder` (reflection on the
  editor's mixer controller; it fails with the member names if Unity changes them): groups
  Engines, Water, Wind, Machinery, Signals (horn) with exposed `*Volume`, and low-pass `*Cutoff`
  on the first four.
- Settings: Settings > Sound with master volume, four bus levels and Mute, saved in PlayerPrefs.
  Mute sets the listener volume to 0 and keeps the mix. A standalone player mutes while unfocused
  (setting `MuteInBackground`, default on); the editor is left to its own controls.

## 5. Pause, time and lifecycle

| Situation | Behaviour |
|---|---|
| Pause | `AudioListener.pause` (unchanged menu behaviour) stops all output at once. Measured silence while paused. |
| 2x / 4x | States follow the simulation (machinery moves faster, rpm changes faster); smoothing runs in real time; pitch is never multiplied by the speed. Event cooldown 0.25 s real time per event and place. |
| Focus loss | Standalone player: muted while unfocused (setting). Editor: unchanged. |
| Save | Nothing audio is saved: every layer is derived from saved state. |
| Load, reset | `RestoreCount` changes: pitch takes the restored state at once, levels glide from where they were to the restored state without stopping a playing loop, no events for 0.3 s and the event queue is cleared. |
| Vessel or scenario change | The scene reloads; voices and pool are children of the vessel and go with it; `OnDestroy` disposes them and unsubscribes even when only the component is removed. |
| New voyage | As a scene load; layers fade in from the start state. |
| Other vessels | No soundscape, no sources: unchanged (horn only). |

## 6. Defects found and fixed during the part

- Synthesized loops were filtered linearly, which broke their seams; filters and resampling now
  run on one period with wrapped neighbours (seam ratio 1.03 to 0.6 of the signal's own steps).
- The synthesized thump started at a non-zero phase, a step at every pulse; an attack ramp fixes it.
- `Force To Mono` would have normalized every clip; disabled, with a test.
- Exposed mixer parameters were renamed by list position, but the controller sorts them; renamed
  by GUID now, and the mapping is verified.
- A hatch leaving Closed was reported as a winch drive (the phase reads Closed until 1e-4 of
  travel); the drive now follows the motion.
- Mooring winches were first a geared electric motor and flickered on and off as the line load
  crossed the stall pull; the archive gives electrohydraulic LEGShA2-1 winches, so the pump runs
  while hauling or slackening is ordered and only its note follows the line speed.
- A load stopped every playing loop (a click in the recording at the load); loops now glide.
- A fade of the soundscape's own sources before the pause was tried and removed: the listener
  pause stops the whole mixer output, so it could not help.

## 7. Tests and checks

| Check | Result |
|---|---|
| Compile | 0 errors (`Logs/check-Compile-20261024-124021-032.log`, rebuilt by every later run) |
| EditMode | 265/265 (`TestResults/check-EditMode-20261024-133435-448.xml`) |
| PlayMode | 100/100 (`TestResults/check-PlayMode-20261024-133120-283.xml`) |
| `ShipAudioBuilder.Run` | `SHIP_AUDIO_BUILD|PASS` |
| `ShipAudioRuntimeCheck.Run` | `SHIP_AUDIO_RUNTIME|PASS` (`part15-runtime.json`) |
| Menu, save, load | `MENU_SMOKE|PASS`, `VOLGODON_RUNTIME|PASS`, `VOLGODON_LOADING_UI|PASS` (`Logs/check-Menu-20261024-132851-625.log`) |
| Five vessels | `VESSEL_SHAKEDOWN|PASS: 5 vessels` (`Logs/check-Shakedown-20261024-132921-434.log`) |
| Sea trials | `SEA_TRIALS|PASS`, every table row identical to Part 14 |

New tests. EditMode `ShipAudioAssetTests`: manifest and files match, every recording CC0 with
author, URL, SHA-256 and a kept source, no status claiming a 507B recording; WAVs mono (wind
stereo), peak at or below -0.95 dBFS, no DC, loop seams no larger than 1.5 times the signal's own
99th-percentile step; import settings; mixer groups and parameters; profile complete; only the
507B carries a soundscape and no prefab saves an AudioSource; mute and bus settings persist and
apply. PlayMode `VesselSoundscapeTests` (`part15-cases.jsonl`):

| Case | Result |
|---|---|
| Lever moved, no physics step | no engine sound change |
| Port 0.9, starboard 0.35 after 40 s | 293 / 130 rpm; firing pitch 0.977 (= 293/300) and 1.305 (= 130/100); independent |
| Full ahead to full astern | firing silent while fuel cut, air brake heard, one starting-air event per engine; largest step between 20 ms ticks: volume 0.165, pitch 0.012 |
| Let go, heave, stow | exactly 5 events (brake released, set, windlass start, stop, anchor seated); layers follow the chain rate and stop when stowed |
| Hatches to half open | drives hydraulic, winch, hydraulic, stopped; 14 events (7 per hold); 2 s of paused frames is not a stop; loops stopped at the end |
| Stern line hauled, then parted under full ahead | one pump start while hauling; strain loud above 40 % load; snap; silent after |
| Restore | no events replayed; pitch at the restored rpm at once; level glides by one smoothing step |
| Wheelhouse | interior 1, water cutoff below 1 kHz; outside back to 22 kHz |
| Sources | all spatial except wind, Doppler 0, all routed; pitch unchanged at 4x |
| Busy deck under way | at most 29 voices playing (32 real voices), 0 bytes per 1000 ticks, 6.4 microseconds per tick |
| Component removed, vessel destroyed | no AudioSource left, no stale subscription |

`VoyageMenuTests` also captures the Sound page and checks its five sliders.

Runtime check (Gorodets, real time, the final mix recorded at the AudioListener):

| Segment | Length | Peak | Voices | Note |
|---|---|---|---|---|
| 01 stopped, then dead slow (0.27) | 20 s | -14.5 dBFS | 17 | |
| 02 full ahead, 4x then 1x | 25 s | -9.4 | 15 | |
| 03 port full, starboard half, stern view | 15 s | -7.4 | 17 | left channel 0.7 dB louder |
| 04 full ahead to full astern | 35 s | -10.9 | 19 | |
| 05 bridge then outside, 0.8 ahead | 26 s | -9.8 | 17 | -25.0 / -26.0 dBFS RMS |
| 06 wind 15 m/s, outside then bridge | 16 s | -13.4 | 16 | -31.4 / -45.0 dBFS RMS |
| 07 hatches: open, reverse, seat; load into slide; lowering, seat | 53 s | -13.5 | 15 | 0 events on the load frames |
| 08 anchor: let go, veer, hold, heave at 4x | 33 s | -7.7 | 9 | |
| 09 mooring: two lines hauled, held in 14 m/s off the quay, slack, cast off | 39 s | -14.0 | 15 | |
| 10 pause, 4x, save, astern, load, mute | 37 s | -10.4 | 17 | paused and muted: silence; 0 events after load |

No clipped sample anywhere (master at 100 %); one listener and one soundscape in every segment.
After changing to the Volgo-Balt in Gorodets: no soundscape, no ship audio source, one listener.
After changing to the 507B in the River scene: one soundscape, one listener. Tick cost in the
scene 5.7 microseconds, 0 bytes allocated. Analysis (`part15-demo-analysis.json`, `--analyse`):
no click detections except three onsets of the cover-seat impacts in segment 07 (smooth rises
into the recorded impact, not discontinuities). The 20-27 dB second-to-second jumps are event
onsets (engines engaging from silence, chain let go) and the pause and mute windows.

## 8. Recordings and images

- Demonstration recordings: `Audio/Demo/part15-demo-01...10.ogg` (3.4 MB, Ogg Vorbis copies of the
  captured WAVs; the WAVs are in `Logs/Part15/demo/`).
- `Previews/part15-demo-spectrograms.png`: dead-slow firing pulses, full ahead, the reversal (fuel
  cut, starting-air hiss, restart) and the hatch sequence.
- `Previews/part15-menu-settings.png`, `Previews/part15-menu-sound.png`.

Reproduce: `ShipSimulator.Editor.ShipAudioRuntimeCheck.Run` in a dedicated batch editor with
rendering (no `-quit`, no `-nographics`), about 8 minutes; then
`python -I Art/Source/Audio/build_ship_audio.py --analyse Logs/Part15/demo/*.wav`.

## 9. Listening, limits and remaining work

- **Not listened to.** The agent that made this part cannot hear. Every judgement above is from
  waveform, spectrum, level and click analysis of the files and of the in-game recordings. Their
  subjective quality, the balance between layers, the realism of the synthesized engine rhythm
  and the wheelhouse filter need a person to listen. Listening steps: play the ten demo
  recordings; then in the editor, Gorodets passage, 507B: stay stopped, then W in small steps and
  listen from CHASE, STERN and NAVIGATOR (V); split the engines (Q/Z, E/X); full astern from full
  ahead; the hatch buttons at 4x; anchor G, B, Y, U; at the demonstration quay pass lines (O, P,
  I) and haul (,); Escape for pause and Settings > Sound; save and load.
- Analogues, not the 507B: no recording of the vessel, its 6ChRN 36/45 engines or its machinery
  was found. The engines are faster ferry and Danube diesels with a synthesized slow-engine rhythm;
  the hydraulics are a vehicle-ramp pack; the winches a lighthouse motor.
- Pause and resume cut the sound at once (Unity's listener pause; the menu sets it immediately and
  its tests rely on that). The measured step at the cut is 0.025 at about -30 dBFS.
- Positions of the exhaust uptake, the hull water points and the stern capstan are estimated.
- No room acoustics, occlusion or reverb; the wheelhouse is a filter. No sound of rain, of the bow
  thruster (the 507B has none), of the steering gear or of the auxiliary generators.
- Busy scenes reach 29 of Unity's 32 real voices; above that Unity virtualizes the least
  important (priorities: one-shots, engines, machinery, water, wind).
- The windlass, stern capstan and cover winches share one motor recording at different pitches.
- Other vessels are unchanged and silent apart from the horn; the infrastructure is optional and
  could carry their profiles later.

## 10. Shared library for every vessel (refactor, 2026-10-24)

The sounds were never recordings of the 507B, so they no longer belong to it. Architecture now:

- `Assets/ShipSimulator/Audio/Library/{Engines,Propulsion,Water,Wind,Machinery,Impacts,Anchor,Mooring}/`
  holds the 36 files, moved with their `.meta`: every GUID and every byte unchanged (checked against
  SHA-256 taken before the move; a rebuild into a scratch folder is byte-identical). Neutral names
  where the old ones named a 507B use, for example `Loop_CoverRolling` is now
  `Machinery/Loop_SteelWheelsRolling`, and the synthesized clips name the engine they were made for:
  `Engines/Loop_DieselFiring_6Cyl4Stroke_5Hz` and `_15Hz`, `Propulsion/Loop_BladeRate_4Blade_20Hz`.
- `Assets/ShipSimulator/Audio/Profiles/<vessel id>.asset`: one profile per catalogue vessel, built by
  `ShipAudioBuilder`; the 507B's is the moved original (same GUID, same clips, levels and distances).
- `Art/Source/Audio/`: `build_ship_audio.py` (was `part15_audio.py`), `Sources/`, `sources.json` and the
  registry. `Audio/Demo/` here keeps the 507B recordings.
- `VesselSoundscape` is the one implementation. The vessel data states equipment facts (optional,
  zero meaning not established): engine `cylinders`, `strokesPerCycle`, `engineRpmPerShaftRpm`,
  `directReversingAirStart`, propeller `bladeCount`, ground tackle `windlassDrive` and
  `mooringWinchDrive`. Only the 507B has them, with S/D labels in `VolgoDon507B_Sources.md`. The
  firing pitch is now engine rpm x cylinders x 2 / strokes / 60 over the clip's rate, so a geared
  engine is never confused with its shaft. Machinery clips follow the named drive; hull water is
  scaled by `SupportModel.ImmersionAtFraction` of the computed support fraction. Missing model points
  are estimated from the data (propeller positions, beam, length). `DisabledLayers` gives each
  reason.
- `ShipAudioBuilder.InstallSoundscape` is called by the 507B, procedural, Meteor and Luch builders,
  so a prefab rebuild keeps the sound. The mixer, buses, settings, pause and load policy are shared.

What each vessel plays now. The diesel engine-room and exterior layers are generic texture for all
of them: level and pitch follow the shaft speed as a fraction of rated speed (with a fixed gear ratio
the same fraction as the engine's) and the fuel, but they are not synchronized with any vessel's
firing. The recordings keep their own engines' rhythm (about 7.8 Hz and 15.5 Hz, moved by a pitch of
0.75 to 1.1). For the high-speed engines of the Meteor (M-400, 1700 rpm) and the Luch this is an
acceptable stand-in until recordings or synthesis of a high-speed diesel exist: those engines fire at
well over 100 Hz, and the clips carry no claim about that rate. The propeller wash clip also keeps the
recorded ferry's tones (99 Hz, a 19 to 28 Hz modulation), unsynchronized; for the 507B, Volgo-Balt,
Volgoneft and Meteor it stays as a generic propeller wake.

| Layer | 507B | Volgo-Balt | Volgoneft | Meteor | Luch |
|---|---|---|---|---|---|
| Engine room, exterior (shaft speed, fuel) | yes | yes | yes | yes | yes (1 engine) |
| Propeller wash (thrust) | yes | yes | yes | yes | off: the real craft has a waterjet; the clip carries the recorded ferry's propeller tones |
| Hull flow, bow wave, lapping (x hull in water) | yes | yes | yes | yes, fading foilborne | yes, reduced on cushion |
| Wind | yes | yes | yes | yes | yes |
| Firing rhythm | yes | off: cycle, speed, gearing not established | off: ratio and speed not established | off: cylinders and gearing not established | off: speed and gearing not established |
| Starting air on reversal | yes | off: reversing gear not established | off | off | off |
| Blade rate | yes | off: blade count unknown | off: unknown | off: five blades, clip is four | off: unknown |
| Hatches, anchors, mooring | yes | off: not modelled | off | off | off |
| Wheelhouse filter | yes | off: no interior in the model | off | off | off |

Verification: EditMode 267/267 (`TestResults/check-EditMode-20261024-142728-516.xml`), PlayMode
107/107 (`TestResults/check-PlayMode-20261024-142759-868.xml`), including new tests: every vessel plays
only what its data and model support, no reversal sound for the other four; the Meteor's hull flow
drops from 0.593 floating to 0.098 at 93 % foil support; the same commands give a bit-identical
trajectory with and without the soundscape; no duplicate WAV and no clip outside the library; every
prefab carries its own profile. The 507B cases repeat their earlier numbers: 5, 14, 2 and 38 events,
largest steps 0.165 volume and 0.012 pitch. `SHIP_AUDIO_RUNTIME|PASS`: the same event counts in all
ten recorded segments and the same window levels; after the vessel change the Volgo-Balt plays its
own profile, 9 voices, none of the 507B's equipment. `MENU_SMOKE|PASS`, `VESSEL_SHAKEDOWN|PASS: 5
vessels`, sea-trial rows identical. The 507B recordings in `Audio/Demo/` are from before the refactor;
the rerun's are in `Logs/Part15/demo/`, analysed in `Logs/Part15/refactor-demo-analysis.json`.
