"""Audio pipeline for the ShipSimulator sound library (Part 15).

One file holds the source registry, the edits and the synthesis, so the license manifest, the game
files and the asset report cannot drift apart. Steps:

  --download   fetch any missing source recording into Sources/ and verify its SHA-256
  --build      write the game files to Assets/ShipSimulator/Audio/Library/<category>/, the manifest
               audio-manifest.json, AUDIO_SOURCES.md and the measurement report part15-audio-assets.json
               (--game-dir writes the WAVs elsewhere instead, to compare a rebuild with the library)
  --analyse    measure captured demonstration recordings (peak, clipping, clicks, level per second)
  --demo-ogg   write compact Ogg copies of the captured demonstration to Demo/

Requires numpy, scipy and soundfile (libsndfile 1.1 or newer for Ogg Vorbis). For example:
  uv venv .venv && uv pip install --python .venv/bin/python numpy scipy soundfile
  .venv/bin/python -I build_ship_audio.py --download --build

No source recording is of any vessel in the catalogue or of its engines. Every recording is an analogue of
the equipment, and the engine firing rhythm, the propeller blade rate and the parting snaps are original
synthesis. The status column of the manifest says which. The library is shared: vessels choose clips in their
sound profiles (Assets/ShipSimulator/Audio/Profiles); nothing here is copied per vessel.

Replacing a clip: keep its library path and overwrite the WAV in place (Unity keeps the .meta and its GUID, so
every profile keeps referring to it), then reimport; update the entry here and in the registry.
"""

import argparse
import hashlib
import json
import os
import subprocess
import sys

import numpy as np
import soundfile as sf
from scipy import signal

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..', '..'))
SOURCES = os.path.join(HERE, 'Sources')
GAME = os.path.join(ROOT, 'Assets', 'ShipSimulator', 'Audio', 'Library')
REPORTS = os.path.join(ROOT, 'Art', 'Source', 'VolgoDon507B')
RATE = 44100
CC0 = ('CC0 1.0 Universal (public domain dedication)', 'https://creativecommons.org/publicdomain/zero/1.0/')

# Freesound recordings. The license was read from each sound's own page on 2026-10-24; all are CC0, so no
# attribution is required, but the manifest credits every author anyway. The files are Freesound's high-quality
# Ogg previews (a lossy transcode of the upload): CC0 covers them, and downloading the originals needs an account.
RECORDINGS = {
    '74915': ('ferry engine room2.wav', 'mrmayo', 'Engine of the Cape May-Lewes ferry, recorded at the top of the engine-room stairs'),
    '322960': ('riverboat1.wav', 'vumseplutten1709', 'A 135 m Danube cargo vessel under way, recorded from the bank'),
    '708043': ('air compressor hose', 'mycompasstv', 'Air released from an industrial compressor hose'),
    '131934': ('Slips air release v1', 'mcpable', 'Air pressure release on oil-rig equipment'),
    '74916': ('back of ferry boat.wav', 'mrmayo', 'Water churned by the propellers behind a ferry'),
    '871472': ('Amazon River Cargo Boat- Water against Hull', 'edfrederick', 'Water along the bow of a river cargo boat under way'),
    '551436': ("Choppy waves against a ship's bow", 'tiennotg', 'Small waves against a moored ship\'s bow'),
    '570927': ('metallic barge bow.wav', 'bruno.auzet', 'Water slapping at the bow of a steel barge under way'),
    '376533': ('Strong Wind Recorded with Soundfield Microphone Manchester 3', 'keirofinch', 'Strong wind, open air'),
    '830338': ('Wind howling over Reservoir No 2', 'pnwheeler', 'Gale over open water (Walthamstow reservoirs)'),
    '784981': ('Motors_Hydraulic_Handycap_Ramp', 'SoundsofTheMachine', 'Electric hydraulic power pack driving a vehicle ramp'),
    '718387': ('electric lighthouse motor', 'bruno.auzet', 'Geared electric motor turning a lighthouse lens, contact microphones'),
    '216605': ('heavy metal trolley rolling rumbling', 'martian', 'Heavy steel trolley rolling'),
    '407301': ('door metal heavy close both sides same time impact rattle1 on offmic +bg voices.wav', 'kyles', 'Heavy steel doors closing'),
    '569555': ('Foley_Impact_Metal_Long_Mono.wav', 'Nox_Sound', 'Impacts on heavy metal'),
    '477129': ('anchor raising', 'mycompasstv', 'Ship\'s anchor chain clanking over the gypsy while the anchor is raised'),
    '199282': ('Big Metal Chain', 'qubodup', 'Heavy chain running through a mechanism (slowed segment of a CC0 chain-ferry recording by miguelstar2)'),
    '500143': ('liner mooring hawsers creak, gangplank groans.WAV', 'phonoflora', 'Synthetic mooring ropes of a cruise liner creaking on a bollard'),
    '145721': ('Mooring Rope.wav', 'Rmutt', 'Mooring rope on a cleat holding a ferry at a San Francisco dock'),
}
REGISTRY = os.path.join(HERE, 'sources.json')


def load_registry():
    return json.load(open(REGISTRY)) if os.path.exists(REGISTRY) else {}


def download():
    registry = load_registry()
    os.makedirs(SOURCES, exist_ok=True)
    for sid in RECORDINGS:
        entry = registry[sid]
        path = os.path.join(SOURCES, f'freesound-{sid}.ogg')
        if not os.path.exists(path):
            subprocess.run(['curl', '-sfL', '-A', 'Mozilla/5.0', '-o', path, entry['preview_url']], check=True)
        digest = hashlib.sha256(open(path, 'rb').read()).hexdigest()
        if digest != entry['sha256']:
            raise SystemExit(f'{sid}: SHA-256 {digest} does not match the registry {entry["sha256"]}')
    print('sources verified:', len(RECORDINGS))


# ---------------------------------------------------------------- signal helpers

def source(sid, start, end):
    x, sr = sf.read(os.path.join(SOURCES, f'freesound-{sid}.ogg'), always_2d=True)
    x = x[int(start * sr):int(end * sr)]
    if sr != RATE:
        x = signal.resample_poly(x, RATE // np.gcd(RATE, sr), sr // np.gcd(RATE, sr), axis=0)
    return x


def mono(x):
    return x.mean(axis=1) if x.ndim == 2 else x


def sos_filter(x, sos):
    return signal.sosfiltfilt(sos, x, axis=0)


def highpass(x, hz, order=4):
    return sos_filter(x, signal.butter(order, hz, 'highpass', fs=RATE, output='sos'))


def lowpass(x, hz, order=4):
    return sos_filter(x, signal.butter(order, hz, 'lowpass', fs=RATE, output='sos'))


def bandpass(x, lo, hi, order=2):
    return sos_filter(x, signal.butter(order, [lo, hi], 'bandpass', fs=RATE, output='sos'))


def notch(x, hz, q=12):
    b, a = signal.iirnotch(hz, q, fs=RATE)
    return signal.filtfilt(b, a, x, axis=0)


def declick(x, threshold=8.0):
    """Replace isolated impulses (a sample far outside the local high-band spread) by interpolation."""
    y = np.array(x, dtype=float)
    channels = y if y.ndim == 2 else y[:, None]
    removed = 0
    for c in range(channels.shape[1]):
        ch = channels[:, c]
        hi = highpass(ch, 3000)
        spread = signal.medfilt(np.abs(hi), 401) + 1e-9
        bad = np.abs(hi) > threshold * spread * 1.4826
        bad = np.convolve(bad, np.ones(9), 'same') > 0
        idx = np.arange(len(ch))
        if bad.any():
            ch[bad] = np.interp(idx[bad], idx[~bad], ch[~bad])
            removed += int(bad.sum())
        channels[:, c] = ch
    return (channels if y.ndim == 2 else channels[:, 0]), removed


def loop(x, crossfade_s):
    """Seamless loop: the tail is cross-faded (equal power) into the material that precedes the loop start."""
    n = int(crossfade_s * RATE)
    body = x[n:]
    head = x[:n]
    t = np.linspace(0, 1, n, endpoint=False)
    fade_in, fade_out = np.sin(0.5 * np.pi * t), np.cos(0.5 * np.pi * t)
    if x.ndim == 2:
        fade_in, fade_out = fade_in[:, None], fade_out[:, None]
    out = body.copy()
    out[-n:] = body[-n:] * fade_out + head * fade_in
    return out


def fade(x, in_s=0.002, out_s=0.05):
    y = x.copy()
    a, b = int(in_s * RATE), int(out_s * RATE)
    if a:
        ramp = np.linspace(0, 1, a)
        y[:a] *= ramp[:, None] if y.ndim == 2 else ramp
    if b:
        ramp = np.linspace(1, 0, b)
        y[-b:] *= ramp[:, None] if y.ndim == 2 else ramp
    return y


def k_weighted(x):
    # ITU-R BS.1770 pre-filter (high shelf and RLB high-pass), designed for this sample rate.
    f0, gain, q = 1681.974450955533, 3.999843853973347, 0.7071752369554196
    k = np.tan(np.pi * f0 / RATE)
    vh, vb = 10 ** (gain / 20), 10 ** (gain / 20) ** 0.4996667741545416
    a0 = 1 + k / q + k * k
    b1 = [(vh + vb * k / q + k * k) / a0, 2 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0]
    a1 = [1, 2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0]
    f0, q = 38.13547087602444, 0.5003270373238773
    k = np.tan(np.pi * f0 / RATE)
    a2 = [1, 2 * (k * k - 1) / (1 + k / q + k * k), (1 - k / q + k * k) / (1 + k / q + k * k)]
    y = signal.lfilter(b1, a1, x, axis=0)
    return signal.lfilter([1, -2, 1], a2, y, axis=0)


def loudness(x):
    """Ungated BS.1770 loudness in LUFS (channel sum), a comparison figure for steady loops."""
    y = k_weighted(x)
    power = np.mean(y ** 2, axis=0)
    return float(-0.691 + 10 * np.log10(np.sum(np.atleast_1d(power)) + 1e-20))


def dbfs(v):
    return float(20 * np.log10(max(v, 1e-12)))


def level(x, target_lufs=None, peak_dbfs=None, ceiling=-1.0):
    """Scale to a loudness or peak target; a peak above the ceiling lowers the gain rather than limiting."""
    peak = np.max(np.abs(x))
    gain = 10 ** ((target_lufs - loudness(x)) / 20) if target_lufs is not None else 10 ** (peak_dbfs / 20) / peak
    gain = min(gain, 10 ** (ceiling / 20) / peak)
    return x * gain


def clean(x, hp=20):
    x = highpass(x, hp, 2)
    return x - np.mean(x, axis=0)


def circular(fn, x, pad_s=1.0):
    """Apply a filter to one period of a periodic signal, so the loop seam stays continuous."""
    n = min(len(x), int(pad_s * RATE))
    y = fn(np.concatenate([x[-n:], x, x[:n]]))
    return y[n:n + len(x)]


# ---------------------------------------------------------------- synthesis

RNG = np.random.default_rng(507)


def damped(freq, tau, length, phase=0.0, attack=0.002):
    # The attack ramp keeps a non-zero starting phase from becoming a step.
    t = np.arange(length) / RATE
    return np.sin(2 * np.pi * freq * t + phase) * np.exp(-t / tau) * (1 - np.exp(-t / attack))


def noise_burst(length, tau, lo, hi):
    t = np.arange(length) / RATE
    n = bandpass(RNG.standard_normal(length), lo, hi)
    return n / (np.std(n) + 1e-12) * np.exp(-t / tau) * (1 - np.exp(-t / 0.0015))


def circular_add(buffer, start, pulse):
    n = len(buffer)
    idx = (start + np.arange(len(pulse))) % n
    np.add.at(buffer, idx, pulse)


def engine_firing(reference_rpm, seconds):
    """Six-cylinder four-stroke firing at a reference speed: one exhaust blowdown per cylinder per two
    revolutions in the 1-5-3-6-2-4 order at 120 degree intervals. Each pulse is a pressure thump, an exhaust-pipe
    resonance and a short combustion click; fixed cylinder-to-cylinder differences and small cycle-to-cycle
    scatter give the half-order beat of a real engine. Played at pitch = shaft rpm / reference_rpm, the firing
    rate equals the simulated engine's (rpm / 20 Hz)."""
    cycle = 120.0 / reference_rpm
    cycles = max(1, int(round(seconds / cycle)))
    n = int(round(cycles * cycle * RATE))
    out = np.zeros(n)
    strength = np.array([1.0, 0.93, 1.08, 0.97, 1.04, 0.9])
    timbre = np.array([0.0, 0.4, 1.1, 2.0, 2.7, 3.5])
    slow = reference_rpm < 200
    for c in range(cycles):
        for k in range(6):
            start = int(round((c * cycle + k * cycle / 6 + RNG.normal(0, 0.0004)) * RATE)) % n
            a = strength[k] * (1 + RNG.normal(0, 0.05))
            length = int((0.32 if slow else 0.16) * RATE)
            thump = damped(31 if slow else 46, 0.045 if slow else 0.022, length, timbre[k], 0.006) * 1.6
            pipe = noise_burst(length, 0.05 if slow else 0.025, 55, 190) * 0.55
            body = noise_burst(length, 0.02 if slow else 0.011, 180, 900) * 0.28
            click = noise_burst(length, 0.004, 1200, 4500) * 0.06
            circular_add(out, start, a * (thump + pipe + body + click))
    # A mechanical floor that turns with the crank (valve gear, pumps), modulated at the firing rate.
    t = np.arange(n) / RATE
    floor = circular(lambda v: bandpass(v, 300, 2500), RNG.standard_normal(n)) * 0.05
    floor *= 1 + 0.5 * np.sin(2 * np.pi * (reference_rpm / 20) * t)
    return circular(lambda v: clean(lowpass(v, 5000), 18), out + floor)


def blade_rate(reference_rpm, seconds, blades=4):
    """Propeller blade-passing pressure at a reference shaft speed: smooth pulses at shaft rpm x 4 / 60 Hz with a
    slightly stronger blade (once-per-revolution modulation), shaping a band of flow noise. Seamless because the
    loop holds whole revolutions."""
    shaft = reference_rpm / 60.0
    revs = max(1, int(round(seconds * shaft)))
    n = int(round(revs / shaft * RATE))
    t = np.arange(n) / RATE
    phase = 2 * np.pi * shaft * blades * t
    pulse = 0.6 * (1 + np.cos(phase)) ** 2 / 4 + 0.15 * np.cos(2 * np.pi * shaft * t)
    hum = sum(np.sin(h * phase + 0.7 * h) / h for h in (1, 2, 3)) * 0.35
    flow = circular(lambda v: bandpass(v, 60, 600), RNG.standard_normal(n))
    flow /= np.std(flow)
    out = hum + flow * (0.25 + pulse)
    return circular(lambda v: clean(lowpass(v, 1200), 15), out)


def snap(kind):
    """Parting of a line or chain: an instantaneous crack, then a whip (rope) or ringing links (chain)."""
    n = int((0.9 if kind == 'rope' else 1.6) * RATE)
    out = np.zeros(n)
    out[0] = 1.0
    out += noise_burst(n, 0.012, 2500, 9000) * 0.7
    if kind == 'rope':
        t = np.arange(n) / RATE
        whip = RNG.standard_normal(n)
        # Band sweeping down as the recoiling ends slow.
        bands = [(4000, 7000), (2000, 4000), (900, 2000), (400, 900)]
        for i, (lo, hi) in enumerate(bands):
            seg = bandpass(whip, lo, hi) * np.exp(-((t - 0.03 * (i + 1)) / 0.05) ** 2)
            out += seg * 0.5
        out += damped(70, 0.08, n) * 0.6
    else:
        for f, tau, a in ((418, 0.55, 0.5), (1131, 0.35, 0.4), (2207, 0.22, 0.3), (3468, 0.12, 0.25), (5120, 0.06, 0.2)):
            out += damped(f * (1 + RNG.normal(0, 0.01)), tau, n, RNG.uniform(0, 6.28)) * a
        out += damped(55, 0.15, n) * 0.7
    return clean(out, 30)


# ---------------------------------------------------------------- game files

def entry(name, purpose, kind, status, sources, edits, data, channels_note=None, rate=RATE):
    return dict(file=name, purpose=purpose, kind=kind, status=status, sources=sources, edits=edits,
                data=data, channels_note=channels_note, rate=rate)


def build_all():
    out = []

    def rec(sid, segment):
        return dict(id=sid, segment_s=segment)

    # Engines -------------------------------------------------------------------
    out.append(entry('Engines/Loop_DieselFiring_6Cyl4Stroke_5Hz.wav', 'Firing rhythm of a six-cylinder four-stroke diesel: 5 firings per second at pitch 1 (100 engine rpm); low-speed layer',
                     'loop', 'synthesized approximation', [],
                     'Original synthesis: 6-cylinder four-stroke firing at a 100 rpm reference, 12 cycles; low-passed 5 kHz, high-passed 18 Hz.',
                     level(engine_firing(100, 14), -18), rate=22050))
    out.append(entry('Engines/Loop_DieselFiring_6Cyl4Stroke_15Hz.wav', 'Firing rhythm of a six-cylinder four-stroke diesel: 15 firings per second at pitch 1 (300 engine rpm); high-speed layer',
                     'loop', 'synthesized approximation', [],
                     'Original synthesis: as the low layer at a 300 rpm reference, 30 cycles, shorter pulses.',
                     level(engine_firing(300, 12), -18), rate=22050))
    x = clean(mono(source('74915', 4, 21.5)), 90)
    out.append(entry('Engines/Loop_DieselEngineRoom.wav', 'Diesel engine-room mechanical texture (valve gear, auxiliaries), per engine',
                     'loop', 'analogue recording (ferry engine room, engine type unknown)', [rec('74915', [4, 21.5])],
                     'Mono sum, high-pass 90 Hz to remove the ferry engine\'s own firing fundamentals, 1.5 s cross-faded loop.',
                     level(loop(x, 1.5), -20)))
    x, clicks = declick(mono(source('322960', 2, 19.5)))
    x = clean(x, 110)
    out.append(entry('Engines/Loop_DieselExterior.wav', 'Diesel engine heard outside the hull and from the exhaust, per engine',
                     'loop', 'analogue recording (135 m Danube cargo vessel, engine type unknown)', [rec('322960', [2, 19.5])],
                     f'Mono sum, {clicks} impulsive samples interpolated (de-click), high-pass 110 Hz, 1.5 s cross-faded loop.',
                     level(loop(x, 1.5), -20)))
    x = clean(mono(source('708043', 12, 21)), 120)
    out.append(entry('Engines/Loop_StartingAirBrake.wav', 'Starting air braking the shaft of a direct-reversing diesel during reversal',
                     'loop', 'analogue recording (compressor hose)', [rec('708043', [12, 21])],
                     'Mono sum, high-pass 120 Hz, 1 s cross-faded loop.', level(loop(x, 1.0), -24)))
    x = clean(mono(source('131934', 0.32, 3.2)), 60)
    out.append(entry('Engines/Shot_StartingAir.wav', 'Starting air admitted when a direct-reversing diesel engages a running direction',
                     'oneshot', 'analogue recording (rig air release)', [rec('131934', [0.32, 3.2])],
                     'Mono sum, high-pass 60 Hz, 2 ms fade in, 0.6 s fade out.', level(fade(x, 0.002, 0.6), peak_dbfs=-6)))

    # Propellers and water --------------------------------------------------------
    x = clean(mono(source('74916', 20, 38)), 40)
    out.append(entry('Propulsion/Loop_PropellerWash.wav', 'Water churned behind a propeller, scaled by its thrust',
                     'loop', 'analogue recording (ferry wake)', [rec('74916', [20, 38])],
                     'Mono sum, high-pass 40 Hz, 2 s cross-faded loop.', level(loop(x, 2.0), -20)))
    out.append(entry('Propulsion/Loop_BladeRate_4Blade_20Hz.wav', 'Blade-passing pressure of a four-bladed propeller: 20 Hz at pitch 1 (300 shaft rpm)',
                     'loop', 'synthesized approximation', [],
                     'Original synthesis: four blades at a 300 rpm reference, 60 revolutions.',
                     level(blade_rate(300, 12), -24), rate=22050))
    x = mono(source('871472', 2, 20))
    for f in (131.9, 133.2, 146.7, 148.0, 264.0, 293.5):
        x = notch(x, f, 20)
    x = clean(x, 80)
    out.append(entry('Water/Loop_HullFlow.wav', 'Water streaming along an immersed hull, scaled by speed through the water',
                     'loop', 'analogue recording (river cargo boat bow, Amazon)', [rec('871472', [2, 20])],
                     'Mono sum, notches at the recording boat\'s engine lines (132, 133, 147, 148, 264, 294 Hz), high-pass 80 Hz, 2 s cross-faded loop.',
                     level(loop(x, 2.0), -22)))
    x, clicks = declick(mono(source('551436', 3.9, 21.4)))
    x = clean(x, 220)
    out.append(entry('Water/Loop_HullLap.wav', 'Small waves lapping an immersed hull when slow or stopped',
                     'loop', 'analogue recording (waves against a ship\'s bow)', [rec('551436', [3.9, 21.4])],
                     f'High-pass 220 Hz to remove engine lines in the background, {clicks} impulsive samples interpolated, 1.5 s cross-faded loop.',
                     level(loop(x, 1.5), -26)))
    x = clean(mono(source('570927', 2, 20)), 60)
    out.append(entry('Water/Loop_BowWave.wav', 'Bow wave of a displacement hull making way ahead',
                     'loop', 'analogue recording (steel barge bow under way)', [rec('570927', [2, 20])],
                     'Mono sum, high-pass 60 Hz, 2 s cross-faded loop.', level(loop(x, 2.0), -22)))

    # Wind ----------------------------------------------------------------------
    x = source('376533', 18, 41)
    for f in (58.6, 60.1, 61.5, 120.0, 180.0, 280.0):
        x = notch(x, f, 15)
    x = clean(x, 40)
    out.append(entry('Wind/Loop_WindSteady.wav', 'Wind around the listener, scaled by wind relative to the vessel (stereo, non-positional)',
                     'loop', 'recording of the phenomenon (strong wind, open air)', [rec('376533', [18, 41])],
                     'Stereo kept, notches at hum lines (59-62, 120, 180, 280 Hz), high-pass 40 Hz, 3 s cross-faded loop.',
                     level(loop(x, 3.0), -24), 'stereo'))
    x = clean(mono(source('830338', 40, 63)), 35)
    out.append(entry('Wind/Loop_WindStrong.wav', 'Gusting gale layer above about 10 m/s relative wind',
                     'loop', 'recording of the phenomenon (gale over open water)', [rec('830338', [40, 63])],
                     'High-pass 35 Hz, 3 s cross-faded loop.', level(loop(x, 3.0), -22)))

    # Hatch machinery -------------------------------------------------------------
    x = clean(mono(source('784981', 5.3, 10.25)), 50)
    out.append(entry('Machinery/Loop_HydraulicPump.wav', 'Electric hydraulic power pack running (jacks, electrohydraulic winches)',
                     'loop', 'analogue recording (vehicle-ramp hydraulic pack)', [rec('784981', [5.3, 10.25])],
                     'Mono, high-pass 50 Hz, 0.8 s cross-faded loop.', level(loop(x, 0.8), -22)))
    x = clean(mono(source('784981', 0.0, 2.6)), 50)
    out.append(entry('Machinery/Shot_HydraulicStart.wav', 'Hydraulic power pack starting', 'oneshot', 'analogue recording', [rec('784981', [0.0, 2.6])],
                     'Mono, high-pass 50 Hz, 5 ms fade in, 0.8 s fade out.', level(fade(x, 0.005, 0.8), peak_dbfs=-9)))
    x = clean(mono(source('784981', 10.1, 12.0)), 50)
    out.append(entry('Machinery/Shot_HydraulicStop.wav', 'Hydraulic power pack stopping, valve and stop clunk', 'oneshot', 'analogue recording',
                     [rec('784981', [10.1, 12.0])], 'Mono, high-pass 50 Hz, 5 ms fade in, 0.4 s fade out.',
                     level(fade(x, 0.005, 0.4), peak_dbfs=-6)))
    x = clean(mono(source('718387', 10, 27.5)), 45)
    out.append(entry('Machinery/Loop_ElectricMotor.wav', 'Geared electric motor running (electric winches, windlass, capstan), pitched per machine',
                     'loop', 'analogue recording (geared lighthouse motor, contact microphones)', [rec('718387', [10, 27.5])],
                     'Mono, high-pass 45 Hz, 1.5 s cross-faded loop.', level(loop(x, 1.5), -22)))
    x = clean(mono(source('718387', 1.0, 3.6)), 45)
    out.append(entry('Machinery/Shot_ElectricMotorStart.wav', 'Geared electric motor starting', 'oneshot', 'analogue recording', [rec('718387', [1.0, 3.6])],
                     'Mono, high-pass 45 Hz, 5 ms fade in, 1 s fade out.', level(fade(x, 0.005, 1.0), peak_dbfs=-9)))
    x = clean(mono(source('718387', 95.9, 100.4)), 45)
    out.append(entry('Machinery/Shot_ElectricMotorStop.wav', 'Geared electric motor stopping and its brake setting', 'oneshot', 'analogue recording',
                     [rec('718387', [95.9, 100.4])], 'Mono, high-pass 45 Hz, 5 ms fade in, 1 s fade out.',
                     level(fade(x, 0.005, 1.0), peak_dbfs=-6)))
    x = clean(mono(source('216605', 3, 19)), 35)
    out.append(entry('Machinery/Loop_SteelWheelsRolling.wav', 'Heavy steel wheels rolling on rails (rolling hatch covers)',
                     'loop', 'analogue recording (heavy steel trolley)', [rec('216605', [3, 19])],
                     'Mono, high-pass 35 Hz, 2 s cross-faded loop.', level(loop(x, 2.0), -22)))
    for i, t in enumerate((4.95, 11.7, 23.2, 34.45)):
        x = clean(mono(source('407301', t, t + 1.4)), 30)
        out.append(entry(f'Impacts/Shot_HeavySteelImpact{i + 1}.wav', 'Heavy steel impact (a cover seating, an anchor home in its pocket, an end stop)', 'oneshot',
                         'analogue recording (heavy steel doors)', [rec('407301', [t, t + 1.4])],
                         'Mono sum, high-pass 30 Hz, 1 ms fade in, 0.5 s fade out.', level(fade(x, 0.001, 0.5), peak_dbfs=-3)))
    for i, t in enumerate((2.98, 5.98, 10.98, 13.73)):
        x = clean(mono(source('569555', t, t + 1.6)), 40)
        out.append(entry(f'Impacts/Shot_MetalClunk{i + 1}.wav', 'Metal clunk (a brake, a stopper, a change of direction)', 'oneshot',
                         'analogue recording (metal impact foley)', [rec('569555', [t, t + 1.6])],
                         'High-pass 40 Hz, 1 ms fade in, 0.6 s fade out.', level(fade(x, 0.001, 0.6), peak_dbfs=-6)))

    # Ground tackle ---------------------------------------------------------------
    x = clean(mono(source('477129', 15.15, 28.6)), 50)
    out.append(entry('Anchor/Loop_ChainOverGypsy.wav', 'Anchor chain links clanking over a gypsy while heaving, about 1.05 links per second at pitch 1',
                     'loop', 'analogue recording (ship\'s anchor being raised, Alaska; ship and windlass unknown)', [rec('477129', [15.15, 28.6])],
                     'Mono sum, high-pass 50 Hz, 0.25 s cross-fade placed in a gap between clanks.', level(loop(x, 0.25), -18)))
    x = clean(mono(source('199282', 5, 19)), 40)
    out.append(entry('Anchor/Loop_ChainRunning.wav', 'Anchor chain running out through a hawse',
                     'loop', 'analogue recording (chain-ferry chain, slowed by the uploader)', [rec('199282', [5, 19])],
                     'Mono sum, high-pass 40 Hz, 1.5 s cross-faded loop.', level(loop(x, 1.5), -17)))
    x = clean(mono(source('500143', 3, 21)), 60)
    x, clicks = declick(x, 10.0)
    out.append(entry('Mooring/Loop_LineStrain.wav', 'Mooring lines creaking under strain',
                     'loop', 'analogue recording (cruise-liner synthetic hawsers on a bollard)', [rec('500143', [3, 21])],
                     f'Mono sum, high-pass 60 Hz, {clicks} impulsive samples interpolated, 2 s cross-faded loop.',
                     level(loop(x, 2.0), -26)))
    for i, (a, b) in enumerate(((31.3, 34.1), (36.0, 38.7), (54.6, 56.9))):
        x = clean(mono(source('145721', a, b)), 70)
        out.append(entry(f'Mooring/Shot_LineCreak{i + 1}.wav', 'Single creak of a loaded mooring line', 'oneshot',
                         'analogue recording (ferry mooring rope on a cleat)', [rec('145721', [a, b])],
                         'Mono sum, high-pass 70 Hz, 20 ms fade in, 0.3 s fade out.', level(fade(x, 0.02, 0.3), peak_dbfs=-8)))
    out.append(entry('Mooring/Shot_LineParted.wav', 'A mooring line parting',
                     'oneshot', 'synthesized approximation', [],
                     'Original synthesis: impulse crack, high band burst, downward-sweeping whip noise, 70 Hz thump.',
                     level(snap('rope'), peak_dbfs=-3)))
    out.append(entry('Anchor/Shot_ChainParted.wav', 'An anchor chain parting', 'oneshot', 'synthesized approximation', [],
                     'Original synthesis: impulse crack and inharmonic link modes (418 to 5120 Hz), 55 Hz thump.',
                     level(snap('chain'), peak_dbfs=-3)))
    return out


def seam_click(x):
    """Discontinuity at the loop seam, against the signal's typical sample-to-sample step."""
    m = mono(x)
    step = np.abs(np.diff(m))
    seam = abs(m[0] - m[-1])
    return float(seam / (np.percentile(step, 99) + 1e-12))


def seam_flux(x):
    """High-band energy in 20 ms windows straddling the seam, against the loop's median window."""
    m = highpass(np.concatenate([mono(x)[-RATE:], mono(x)[:RATE]]), 2000, 2)
    w = int(0.02 * RATE)
    frames = [np.mean(m[i:i + w] ** 2) for i in range(0, len(m) - w, w)]
    centre = len(frames) // 2
    return float(dbfs(np.sqrt(max(frames[centre - 1:centre + 1]))) - dbfs(np.sqrt(np.percentile(frames, 90))))


def build(game=GAME):
    registry = load_registry()
    entries = build_all()
    report, manifest = [], []
    for e in entries:
        data = e.pop('data')
        path = os.path.join(game, e['file'])
        os.makedirs(os.path.dirname(path), exist_ok=True)
        rate = e.pop('rate')
        if rate != RATE:
            # Resample one period with its neighbours on both sides, so the seam stays continuous.
            data = signal.resample_poly(np.concatenate([data[-RATE:], data, data[:RATE]]), rate, RATE)[rate:-rate]
        sf.write(path, data.astype(np.float32), rate, subtype='PCM_16')
        written, _ = sf.read(path, always_2d=True)
        e['sample_rate'] = rate
        row = dict(file=e['file'], seconds=round(len(written) / rate, 3), channels=written.shape[1],
                   peak_dbfs=round(dbfs(np.max(np.abs(written))), 2), lufs=round(loudness(written), 1),
                   dc=round(float(np.mean(written)), 6), clipped_samples=int(np.sum(np.abs(written) >= 0.9999)),
                   bytes=os.path.getsize(path))
        if e['kind'] == 'loop':
            row['seam_step_ratio'] = round(seam_click(written), 2)
            row['seam_high_band_db'] = round(seam_flux(written), 1)
        report.append(row)
        for s in e['sources']:
            r = registry[s['id']]
            s.update(title=r['title'], author=r['user'], url=f"https://freesound.org/people/{r['user']}/sounds/{s['id']}/",
                     license=CC0[0], license_url=CC0[1], attribution_required=False, preview_url=r['preview_url'],
                     sha256=r['sha256'], description=RECORDINGS[s['id']][2])
        manifest.append(e)
    if game != GAME:
        print('written to', game, '- manifest and reports left unchanged')
        return
    json.dump(dict(generated_by='Art/Source/Audio/build_ship_audio.py', library='Assets/ShipSimulator/Audio/Library',
                   sample_rate=RATE, files=manifest),
              open(os.path.join(HERE, 'audio-manifest.json'), 'w'), indent=1)
    json.dump(dict(files=report), open(os.path.join(REPORTS, 'part15-audio-assets.json'), 'w'), indent=1)
    write_markdown(manifest)
    for row in report:
        print(row)


def write_markdown(manifest):
    lines = ['# ShipSimulator audio library: sources and licenses', '',
             'Generated by `build_ship_audio.py --build` from its registry; edit the script, not this file. '
             'Game files are in `Assets/ShipSimulator/Audio/Library/`, shared by every vessel profile.', '',
             'No recording here is of any catalogue vessel, of its engines or of its deck machinery. '
             '"Analogue recording" means a recording of comparable equipment used as a substitute; '
             '"recording of the phenomenon" is wind recorded in the open; "synthesized approximation" is original '
             'synthesis by this project, released with the project. Every source below is CC0 1.0: no attribution is '
             'required, and the authors are credited as a courtesy.', '',
             '| Game file | Purpose | Status | Source (author, license) | Edits |', '|---|---|---|---|---|']
    for e in manifest:
        src = '<br>'.join(f"[{s['title']}]({s['url']}) by {s['author']}, [CC0 1.0]({s['license_url']}), "
                          f"{s['segment_s'][0]}-{s['segment_s'][1]} s" for s in e['sources']) or 'Original synthesis (this project)'
        lines.append(f"| `{e['file']}` | {e['purpose']} | {e['status']} | {src} | {e['edits']} |")
    lines += ['', '## Source files', '',
              'The Freesound high-quality Ogg previews actually used are kept in `Sources/` as downloaded '
              '(`freesound-<id>.ogg`); `sources.json` records each preview URL and SHA-256, and `--download` '
              'verifies them. Previews are a lossy transcode by Freesound; CC0 applies to them as to the upload. '
              'Raw HTML snapshots of the sound pages are not kept in the repository: GitHub push protection rejected '
              'them, because the pages embed site tokens.', '',
              '| Id | Title | Author | Page |', '|---|---|---|---|']
    for sid, (title, user, _) in RECORDINGS.items():
        lines.append(f'| {sid} | {title} | {user} | https://freesound.org/people/{user}/sounds/{sid}/ |')
    open(os.path.join(HERE, 'AUDIO_SOURCES.md'), 'w').write('\n'.join(lines) + '\n')


# ---------------------------------------------------------------- demonstration analysis

def analyse(paths, out):
    rows = []
    for path in paths:
        x, sr = sf.read(path, always_2d=True)
        m = x.mean(axis=1)
        step = np.abs(np.diff(x, axis=0)).max(axis=1)
        # A click is a sample-to-sample step far above the local 50 ms spread of steps.
        w = int(0.05 * sr)
        local = np.sqrt(np.convolve(step ** 2, np.ones(w) / w, 'same')) + 1e-6
        clicks = int(np.sum((step > 12 * local) & (step > 0.02)))
        per_second = [round(dbfs(np.sqrt(np.mean(m[i:i + sr] ** 2))), 1) for i in range(0, len(m) - sr + 1, sr)]
        jumps = [round(b - a, 1) for a, b in zip(per_second, per_second[1:])]
        rows.append(dict(file=os.path.basename(path), seconds=round(len(m) / sr, 2), peak_dbfs=round(dbfs(np.max(np.abs(x))), 2),
                         clipped_samples=int(np.sum(np.abs(x) >= 0.999)), clicks=clicks,
                         rms_dbfs_per_second=per_second, largest_second_to_second_jump_db=max(map(abs, jumps), default=0)))
    json.dump(dict(recordings=rows), open(out, 'w'), indent=1)
    for r in rows:
        print({k: v for k, v in r.items() if k != 'rms_dbfs_per_second'})


def demo_ogg(paths):
    """Short Ogg Vorbis copies of the captured demonstration for the repository; the analysis uses the WAVs."""
    folder = os.path.join(REPORTS, 'Audio', 'Demo')
    os.makedirs(folder, exist_ok=True)
    for path in paths:
        x, sr = sf.read(path, always_2d=True)
        out = os.path.join(folder, 'part15-demo-' + os.path.splitext(os.path.basename(path))[0] + '.ogg')
        # Written in blocks: libsndfile's Vorbis encoder crashes on one very large write.
        with sf.SoundFile(out, 'w', sr, x.shape[1], format='OGG', subtype='VORBIS') as f:
            for i in range(0, len(x), sr):
                f.write(np.clip(x[i:i + sr], -1, 1).astype(np.float32))
        print(out, os.path.getsize(out))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--download', action='store_true')
    parser.add_argument('--build', action='store_true')
    parser.add_argument('--analyse', nargs='*')
    parser.add_argument('--demo-ogg', nargs='*')
    parser.add_argument('--out', default=os.path.join(REPORTS, 'part15-demo-analysis.json'))
    parser.add_argument('--game-dir', default=GAME)
    args = parser.parse_args()
    if args.download:
        download()
    if args.build:
        build(os.path.abspath(args.game_dir))
    if args.analyse:
        analyse(args.analyse, args.out)
    if args.demo_ogg:
        demo_ogg(args.demo_ogg)
