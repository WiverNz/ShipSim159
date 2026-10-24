# ShipSimulator audio library

One library of sound files serves every vessel. Nothing is copied per vessel.

| Path | Contents |
|---|---|
| `Assets/ShipSimulator/Audio/Library/<category>/` | The game files: Engines, Propulsion, Water, Wind, Machinery, Impacts, Anchor, Mooring |
| `Assets/ShipSimulator/Audio/Profiles/<vessel id>.asset` | One `VesselSoundProfile` per catalogue vessel: which library clips it plays, levels, distances, wheelhouse zone |
| `Assets/ShipSimulator/Audio/Vessels/<vessel id>/` | Reserved for sounds that exist for one vessel only (for example a recording of that ship). Empty today |
| `Art/Source/Audio/build_ship_audio.py` | The pipeline: verifies the sources, cuts, filters, loops, levels and synthesizes the game files, writes the registry |
| `Art/Source/Audio/Sources/` | The 19 source recordings as downloaded (Freesound high-quality Ogg previews, CC0) |
| `Art/Source/Audio/sources.json` | Each source: preview URL, SHA-256, license, date checked |
| `Art/Source/Audio/audio-manifest.json`, `AUDIO_SOURCES.md` | Every game file: purpose, status, source, license, edits (generated) |
| `Art/Source/Audio/LicenseSnapshots/` | The Freesound page of each source as fetched on 2026-10-24 at about 11:47 UTC (raw HTML) |

## What decides what a vessel hears

- **The vessel data** says what equipment exists. Engine `cylinders`, `strokesPerCycle`,
  `engineRpmPerShaftRpm` and `directReversingAirStart`, and propeller `bladeCount`, are optional:
  zero or false means "not established". `groundTackle.windlassDrive` and `mooringWinchDrive` are
  `electric`, `electrohydraulic` or empty. Hatch covers and ground tackle exist only where those
  systems are modelled. Only facts with a source go in; the sources files label them S, D or E.
- **The profile** chooses clips from the library and how they sound. A layer without a clip is off.
- **`VesselSoundscape`**, one implementation for all vessels, plays a layer only when both allow it,
  and lists the reasons for the others in `DisabledLayers`. Examples:
  - Firing rhythm: needs cylinders, cycle and gear ratio in the data, and a firing clip made for
    that cylinder count and cycle. The pitch is engine rpm (shaft rpm times the ratio) times
    cylinders times 2 / strokes / 60, over the clip's own firing rate.
  - Blade rate: needs the blade count, equal to the clip's.
  - Starting air: needs a direct-reversing engine with air start.
  - Hull flow, bow wave and lapping: scaled by the hull still in the water, from the support
    fraction the physics computes for foils and cushions.

## Replacing or adding a sound

- **Replace a clip** (a better source, an original instead of a preview, a new synthesis): keep its
  library path and file name, and overwrite the WAV in place. Unity keeps the `.meta` and its GUID, so
  every profile still points at it. Then:
  1. Update its entry in `build_ship_audio.py` (source, segment, edits, status).
  2. For a new recording, add the recording to `sources.json` and `Sources/`.
  3. Run `--build`, then `ShipAudioBuilder.Run`, and the EditMode suite.
- **Add a variant** (another engine, a waterjet, a recording of one vessel's equipment): add a new
  file. Use the library if more than one vessel could use it, otherwise `Vessels/<vessel id>/`. Point
  the profile at it in `ShipAudioBuilder`. If it describes an engine or propeller, state the reference
  rate it was made for in the profile (firing clips: Hz, cylinders, strokes; blade clip: Hz, blades).
- **Rename or move** a clip: move it together with its `.meta` (in the editor, or with the file and
  `.meta` together while the editor is closed). Then update the name in `build_ship_audio.py` and
  `ShipAudioBuilder`.
- **Never** copy a WAV to give a second vessel the same sound: reference the same library file
  from its profile. `ShipAudioAssetTests` fails on duplicate files.

To check a rebuild without touching the library:

```
<venv>/bin/python -I Art/Source/Audio/build_ship_audio.py --download --build --game-dir <scratch>
```

Then compare the files byte by byte with `Assets/ShipSimulator/Audio/Library`. The pipeline is
deterministic: on 2026-10-24 all 36 files rebuilt byte-identical.
