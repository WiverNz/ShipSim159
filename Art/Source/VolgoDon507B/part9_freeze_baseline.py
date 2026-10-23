"""Freeze existing assets and the freshly completed numerical trial report."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[3]
folder = Path(__file__).resolve().parent
target = folder / 'part9-baseline.json'
assert not target.exists(), 'Preserve the accepted baseline; do not overwrite it'
log = root / 'Logs/VolgoDon507B/part9-trials.log'
assert 'SEA_TRIALS|PASS' in log.read_text(errors='replace')
paths = [folder / 'VolgoDon507B.blend',
         root / 'Assets/ShipSimulator/Prefabs/Vessels/VolgoDon507B.prefab']
for directory, pattern in [('Assets/ShipSimulator/Data/Vessels', '*.json'),
                           ('Assets/ShipSimulator/Models/VolgoDon507B_Improved', '*.fbx'),
                           ('Assets/ShipSimulator/Scripts/Physics', '*.cs')]:
    paths.extend((root / directory).glob(pattern))
paths.extend(folder / name for name in ['part8-source.json', 'part8-unity.json',
    'part9-geometry-baseline.json', 'part7-loading.json', 'part6-after.json'])
paths.extend((root / 'Assets/ShipSimulator/Models/VolgoDon507').rglob('*'))
paths = sorted(set(p for p in paths if p.is_file()))
hashes = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
baseline = json.loads((folder / 'baseline-checksums.json').read_text())
fallback = [p for p in baseline if '/Models/VolgoDon507/' in p]
assert all(hashes[p] == baseline[p] for p in fallback)
report = dict(date='2026-10-23', file_sha256=hashes, unchanged_fallback_files=len(fallback),
    vessel=json.loads((root / 'Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json').read_text()),
    fresh_sea_trials=(root / 'Logs/SeaTrials/sea-trials.md').read_text(),
    historical_loading_report=json.loads((folder / 'part7-loading.json').read_text()),
    historical_loading_note='Preserved Part 7 evidence, not rerun in Part 9. Fresh planar trials are above.',
    archive=dict(url='https://russrivership.ru/public/files/doc257.pdf', pages=9,
        sha256='841d1aeb590193e92ab2a848d6aaa516ec7c1cc498fa2e3dfee6f7db21979e4b'))
target.write_text(json.dumps(report, indent=2) + '\n')
print('PART9_BASELINE_PASS', len(hashes), 'files;', len(fallback), 'fallback hashes')
