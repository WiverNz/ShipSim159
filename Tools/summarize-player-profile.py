#!/usr/bin/env python3
"""Summarize opt-in PlayerPerformanceCheck CSVs; negative counters mean unavailable."""
import argparse
import csv
import json
import math
from pathlib import Path


def percentile(values, fraction):
    values = sorted(values)
    position = (len(values) - 1) * fraction
    lower = math.floor(position)
    upper = math.ceil(position)
    return values[lower] + (values[upper] - values[lower]) * (position - lower)


def summarize(folder):
    cases = {}
    for path in sorted(folder.glob('*.csv')):
        if path.name == 'transitions.csv':
            continue
        with path.open() as stream:
            rows = list(csv.DictReader(stream))
        if not rows or 'frame_ms' not in rows[0]:
            continue
        metrics = {}
        for key in rows[0]:
            values = [float(row[key]) for row in rows if float(row[key]) >= 0]
            metrics[key] = None if not values else {
                'samples': len(values), 'median': percentile(values, .5),
                'p95': percentile(values, .95), 'p99': percentile(values, .99),
                'max': max(values), 'mean': sum(values) / len(values),
            }
        frames = [float(row['frame_ms']) for row in rows]
        cases[path.stem] = {
            'conditions': json.loads(path.with_suffix('.json').read_text()),
            'frames': len(rows), 'over_16_67ms': sum(x > 16.67 for x in frames),
            'over_50ms': sum(x > 50 for x in frames), 'metrics': metrics,
        }
    return cases


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('folder', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    cases = summarize(args.folder)
    if not cases:
        parser.error('No completed case CSV/JSON pairs found')
    args.output.write_text(json.dumps(cases, indent=2) + '\n')
    print('| Case | Frames | Frame median/p95/p99/max ms | CPU p95 ms | GPU p95 ms | >50 ms |')
    print('|---|---:|---|---:|---:|---:|')
    for name, case in cases.items():
        m = case['metrics']
        frame = '/'.join(f"{m['frame_ms'][p]:.2f}" for p in ('median', 'p95', 'p99', 'max'))
        cpu = f"{m['cpu_frame_ms']['p95']:.2f}" if m['cpu_frame_ms'] else 'N/A'
        gpu = f"{m['gpu_ms']['p95']:.2f}" if m['gpu_ms'] else 'N/A'
        print(f"| {name} | {case['frames']} | {frame} | {cpu} | {gpu} | {case['over_50ms']} |")
