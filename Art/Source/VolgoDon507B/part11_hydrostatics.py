"""Part 11 hydrostatic tables for the Volgo-Don 507B prototype.

Reads part11-hydrostatic-surface.json (the closed Part 10 surface exported through
Blender MCP) and derives two things:

1. Reference hydrostatics by exact clipping of the closed triangle mesh at a plane
   waterline, using the divergence theorem with fields that vanish on the plane.
2. An offset table (half-breadth by station and height above keel) for the vessel
   JSON, then the same quantities integrated the way HydrostaticsModel does at
   runtime: trapezoidal stations, vertical columns across the breadth.

The two methods share only the mesh, so their agreement checks slicing, table
resolution, interpolation and integration. Neither checks the mesh against the
real ship: the surface is a derived family-guided reconstruction.

Run: python3 -I part11_hydrostatics.py [--write-vessel]
Pure Python 3, no third-party packages.
"""
import json
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
VESSEL = ROOT / 'Assets/ShipSimulator/Data/Vessels/VolgoDon507B.json'
KEEL_Z = -3.53
CAP_ABOVE_KEEL = 5.5
RHO = 1000.0

# Table resolution chosen by the convergence study below, not by fitting a displacement.
AREA_TOLERANCE = 0.05
HALF_STRIPS = 6
STRIP_HALF_WIDTH = 8.35
RUNTIME_DZ = 0.05

# Declared range for mean draft and trim; see PART11_REVIEW.md.
MIN_DRAFT = 0.6
MAX_DRAFT = 4.0
MAX_TRIM = 1.5
LPP = 135.0


def load_mesh():
    data = json.loads((HERE / 'part11-hydrostatic-surface.json').read_text())
    return data['vertices'], data['triangles']


def sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def clip(poly, value):
    """Keep the part of a polygon with value(p) <= 0."""
    out = []
    n = len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        va, vb = value(a), value(b)
        if va <= 0:
            out.append(a)
        if (va < 0 < vb) or (vb < 0 < va):
            t = va / (va - vb)
            out.append((a[0] + t * (b[0] - a[0]), a[1] + t * (b[1] - a[1]), a[2] + t * (b[2] - a[2])))
    return out


def exact(vertices, triangles, draft, trim=0.0):
    """Integrals below the plane z = keel + draft + slope * y, slope = trim / LPP (bow down positive)."""
    slope = trim / LPP
    level = KEEL_Z + draft

    def plane(p):
        return level + slope * p[1]

    def below(p):
        return p[2] - plane(p)

    sums = dict(v=0.0, my=0.0, mz=0.0, awp=0.0, fy=0.0, ix=0.0, iy=0.0)
    for tri in triangles:
        poly = clip([tuple(vertices[i]) for i in tri], below)
        for k in range(1, len(poly) - 1):
            p0, p1, p2 = poly[0], poly[k], poly[k + 1]
            e1, e2 = sub(p1, p0), sub(p2, p0)
            area_z = 0.5 * (e1[0] * e2[1] - e1[1] * e2[0])
            if area_z == 0:
                continue
            mids = [tuple(0.5 * (a[c] + b[c]) for c in range(3)) for a, b in ((p0, p1), (p1, p2), (p2, p0))]

            def quad(f):
                return area_z * sum(f(m) for m in mids) / 3.0

            sums['v'] += quad(lambda m: m[2] - plane(m))
            sums['my'] += quad(lambda m: m[1] * (m[2] - plane(m)))
            sums['mz'] += quad(lambda m: 0.5 * (m[2] * m[2] - plane(m) ** 2))
            sums['awp'] -= quad(lambda m: 1.0)
            sums['fy'] -= quad(lambda m: m[1])
            sums['ix'] -= quad(lambda m: m[0] * m[0])
            sums['iy'] -= quad(lambda m: m[1] * m[1])
    v = sums['v']
    lcf = sums['fy'] / sums['awp']
    return dict(volume=v, lcb=sums['my'] / v, kb=sums['mz'] / v - KEEL_Z, awp=sums['awp'], lcf=lcf,
                it=sums['ix'], il=sums['iy'] - sums['awp'] * lcf * lcf)


def slice_offsets(vertices, triangles, y, tolerance=0.002):
    """Starboard half-section at station y as (height above keel, half-breadth) pairs, rising in height.

    Breadth is zero below the first pair, so a flat bottom keeps its full width at the keel line.
    """
    points = {}
    for tri in triangles:
        pts = [vertices[i] for i in tri]
        for a, b in ((pts[0], pts[1]), (pts[1], pts[2]), (pts[2], pts[0])):
            if (a[1] - y) * (b[1] - y) < 0:
                t = (y - a[1]) / (b[1] - a[1])
                x, z = a[0] + t * (b[0] - a[0]), a[2] + t * (b[2] - a[2])
            elif a[1] == y:
                x, z = a[0], a[2]
            else:
                continue
            key = round(z - KEEL_Z, 5)
            points[key] = max(points.get(key, 0.0), abs(x))
    rows = sorted(points.items())
    rows = [(max(0.0, min(CAP_ABOVE_KEEL, h)), x) for h, x in rows]
    if not rows:
        return [(CAP_ABOVE_KEEL, 0.0)]
    return simplify(rows, tolerance)


def simplify(rows, tolerance):
    if len(rows) < 3:
        return rows
    (h0, x0), (h1, x1) = rows[0], rows[-1]
    worst, index = 0.0, 0
    for i in range(1, len(rows) - 1):
        h, x = rows[i]
        t = (h - h0) / (h1 - h0) if h1 > h0 else 0.0
        error = abs(x - (x0 + t * (x1 - x0)))
        if error > worst:
            worst, index = error, i
    if worst <= tolerance:
        return [rows[0], rows[-1]]
    return simplify(rows[:index + 1], tolerance)[:-1] + simplify(rows[index:], tolerance)


def candidate_stations(vertices, spacing):
    """A regular grid plus every ring of the sectional surface, where its sections change slope."""
    ys = [p[1] for p in vertices]
    lo, hi = min(ys), max(ys)
    n = int(math.ceil((hi - lo) / spacing))
    grid = [lo + (hi - lo) * i / n for i in range(n + 1)] + [round(y, 4) for y in ys]
    result = []
    for y in sorted(grid):
        if not result or y - result[-1] > 0.005:
            result.append(y)
    return result


def area_curve(row, dz=0.1):
    """Full sectional area at heights 0, dz, ... up to the cap: the Bonjean curve of one station."""
    count = int(round(CAP_ABOVE_KEEL / dz)) + 1
    area, _ = column_table(row, 0.0, 100.0, dz, count)
    return [2.0 * a for a in area]


def select_stations(ys, curves, tolerance):
    """Douglas-Peucker along the length: drop stations whose Bonjean curve is linear in its neighbours."""
    def error(i, j, k):
        t = (ys[k] - ys[i]) / (ys[j] - ys[i])
        return max(abs(curves[i][h] + t * (curves[j][h] - curves[i][h]) - curves[k][h]) for h in range(len(curves[k])))

    keep = {0, len(ys) - 1}
    stack = [(0, len(ys) - 1)]
    while stack:
        i, j = stack.pop()
        if j - i < 2:
            continue
        worst, index = max((error(i, j, k), k) for k in range(i + 1, j))
        if worst > tolerance:
            keep.add(index)
            stack += [(i, index), (index, j)]
    return sorted(keep)


def build_table(vertices, triangles, tolerance, offset_tolerance=0.002, spacing=0.25, max_spacing=2.0):
    ys = candidate_stations(vertices, spacing)
    offsets = [slice_offsets(vertices, triangles, y, offset_tolerance) for y in ys]
    curves = [area_curve(row) for row in offsets]
    kept = select_stations(ys, curves, tolerance)
    # Long panels pass the even-keel area test but lose accuracy once trim tilts the waterline along them.
    filled = [kept[0]]
    for index in kept[1:]:
        gap = int(math.ceil((ys[index] - ys[filled[-1]]) / max_spacing))
        start = filled[-1]
        for g in range(1, gap):
            filled.append(start + round((index - start) * g / gap))
        filled.append(index)
    return [ys[i] for i in filled], [offsets[i] for i in filled]


class Runtime:
    """Mirror of SectionHydrostatics in C#: per-column area tables on a uniform height grid."""

    def __init__(self, ys, offsets, half_strips, dz):
        self.ys = ys
        self.dz = dz
        self.count = int(round(CAP_ABOVE_KEEL / dz)) + 1
        self.weights = [0.0] * len(ys)
        for i in range(len(ys) - 1):
            w = ys[i + 1] - ys[i]
            self.weights[i] += 0.5 * w
            self.weights[i + 1] += 0.5 * w
        edges = [STRIP_HALF_WIDTH * j / half_strips for j in range(half_strips + 1)]
        self.strips = list(zip(edges, edges[1:]))
        self.centres = [0.5 * (a + b) for a, b in self.strips]
        self.area = []
        self.moment = []
        for row in offsets:
            for xa, xb in self.strips:
                area, moment = column_table(row, xa, xb, dz, self.count)
                self.area.append(area)
                self.moment.append(moment)

    def lookup(self, table, h):
        if h <= 0:
            return 0.0, 0.0
        u = min(h, CAP_ABOVE_KEEL) / self.dz
        k = min(int(u), self.count - 2)
        t = u - k
        return table[k] + t * (table[k + 1] - table[k]), (table[k + 1] - table[k]) / self.dz

    def integrate(self, draft, trim=0.0):
        slope = trim / LPP
        v = my = mz = awp = fy = ix = iy = 0.0
        for i, y in enumerate(self.ys):
            h = draft + slope * y
            for j, x in enumerate(self.centres):
                cell = i * len(self.centres) + j
                a, width = self.lookup(self.area[cell], h)
                m, _ = self.lookup(self.moment[cell], h)
                w = 2.0 * self.weights[i]
                v += w * a
                my += w * a * y
                mz += w * m
                awp += w * width
                fy += w * width * y
                ix += w * width * x * x
                iy += w * width * y * y
        lcf = fy / awp if awp else 0.0
        return dict(volume=v, lcb=my / v, kb=mz / v, awp=awp, lcf=lcf, it=ix, il=iy - awp * lcf * lcf)


def column_table(row, xa, xb, dz, count):
    """Cumulative area and keel moment of the section part with |x| in [xa, xb], at heights k * dz.

    Breadth is linear between offsets and zero outside them, so the overlap with the strip is linear
    between offsets, grid heights and the heights where the breadth crosses a strip edge: each piece
    integrates exactly.
    """
    breaks = set(k * dz for k in range(count))
    breaks.update(h for h, _ in row)
    for (h0, b0), (h1, b1) in zip(row, row[1:]):
        for edge in (xa, xb):
            if (b0 - edge) * (b1 - edge) < 0:
                breaks.add(h0 + (edge - b0) / (b1 - b0) * (h1 - h0))
    breaks = sorted(b for b in breaks if 0.0 <= b <= CAP_ABOVE_KEEL)
    area, moment = [0.0] * count, [0.0] * count
    a = m = 0.0
    for p, q in zip(breaks, breaks[1:]):
        if q > p:
            mid = 0.5 * (p + q)
            op = overlap(row, mid, p, xa, xb)
            oq = overlap(row, mid, q, xa, xb)
            a += 0.5 * (op + oq) * (q - p)
            m += (q - p) / 6.0 * ((2 * p + q) * op + (p + 2 * q) * oq)
        k = int(round(q / dz))
        if abs(q - k * dz) < 1e-9 and k < count:
            area[k], moment[k] = a, m
    return area, moment


def overlap(row, mid, z, xa, xb):
    """Strip overlap at z, using the offset segment that contains mid so steps stay on their own side."""
    if len(row) < 2 or mid < row[0][0] or mid > row[-1][0]:
        return 0.0
    for (h0, b0), (h1, b1) in zip(row, row[1:]):
        if h0 <= mid <= h1 and h1 > h0:
            b = b0 + (z - h0) / (h1 - h0) * (b1 - b0)
            return max(0.0, min(xb, b) - xa)
    return 0.0


def compare(reference, candidate):
    return dict(volume_pct=100 * (candidate['volume'] - reference['volume']) / reference['volume'],
                lcb_m=candidate['lcb'] - reference['lcb'], kb_m=candidate['kb'] - reference['kb'],
                awp_pct=100 * (candidate['awp'] - reference['awp']) / reference['awp'],
                lcf_m=candidate['lcf'] - reference['lcf'],
                it_pct=100 * (candidate['it'] - reference['it']) / reference['it'],
                il_pct=100 * (candidate['il'] - reference['il']) / reference['il'])


def worst(rows):
    keys = rows[0].keys()
    return {k: max(rows, key=lambda r: abs(r[k]))[k] for k in keys}


def main():
    vertices, triangles = load_mesh()
    full = exact(vertices, triangles, CAP_ABOVE_KEEL + 1.0)
    at_design = exact(vertices, triangles, 3.53)
    checks = dict(closed_volume_m3=full['volume'], design_volume_m3=at_design['volume'],
                  part10_closed_volume_m3=11013.688746684815, part10_design_volume_m3=6824.740919612833)

    drafts = [round(MIN_DRAFT + 0.1 * i, 2) for i in range(int(round((MAX_DRAFT - MIN_DRAFT) / 0.1)) + 1)]
    trims = [-MAX_TRIM, -0.75, 0.75, MAX_TRIM]
    reference = {d: exact(vertices, triangles, d) for d in drafts}
    trimmed = {(d, t): exact(vertices, triangles, d, t) for d in (1.0, 2.2, 3.5) for t in trims}

    ys, offsets = build_table(vertices, triangles, AREA_TOLERANCE)
    runtime = Runtime(ys, offsets, HALF_STRIPS, RUNTIME_DZ)
    even = [compare(reference[d], runtime.integrate(d)) for d in drafts]
    trim_rows = [compare(trimmed[k], runtime.integrate(*k)) for k in trimmed]

    # Convergence: quarter the area tolerance, halve the candidate spacing, the offset tolerance and the runtime height step.
    fys, foffsets = build_table(vertices, triangles, AREA_TOLERANCE / 4, 0.001, 0.125, 1.0)
    fine = Runtime(fys, foffsets, HALF_STRIPS, RUNTIME_DZ / 2)
    convergence = [compare(fine.integrate(d), runtime.integrate(d)) for d in drafts]

    curves = []
    for d in drafts:
        r = reference[d]
        bmt, bml = r['it'] / r['volume'], r['il'] / r['volume']
        curves.append(dict(draft_m=d, volume_m3=round(r['volume'], 2), displacement_t=round(r['volume'] * RHO / 1000, 1),
                           lcb_m=round(r['lcb'], 3), kb_m=round(r['kb'], 3), awp_m2=round(r['awp'], 1),
                           lcf_m=round(r['lcf'], 3), tpc_t_per_cm=round(r['awp'] * RHO / 1e5, 3),
                           bmt_m=round(bmt, 3), bml_m=round(bml, 1), kmt_m=round(r['kb'] + bmt, 3),
                           block_coefficient_lpp_moulded=round(r['volume'] / (LPP * 16.5 * d), 4),
                           waterplane_coefficient=round(r['awp'] / (LPP * 16.5), 4)))

    report = dict(
        method=('Reference: exact plane clipping of the closed mesh with divergence-theorem integrands '
                'vanishing on the waterplane (quadratic-exact edge-midpoint quadrature). Runtime mirror: '
                'offset table, trapezoidal stations, vertical columns, linear interpolation on a uniform '
                'height grid. Fresh water, rho 1000 kg/m3.'),
        units='metres, square metres, cubic metres, tonnes; heights above keel; longitudinal positions '
              'forward-positive from the model origin (Unity z)',
        classification='derived from an estimated surface; not a certified hydrostatic booklet',
        mesh_checks=checks,
        table=dict(bonjean_interpolation_tolerance_m2=AREA_TOLERANCE, stations=len(ys), offsets=sum(len(r) for r in offsets), offset_tolerance_m=0.002,
                   half_strips=HALF_STRIPS, runtime_height_step_m=RUNTIME_DZ),
        declared_range=dict(mean_draft_m=[MIN_DRAFT, MAX_DRAFT], trim_m=[-MAX_TRIM, MAX_TRIM]),
        runtime_vs_reference_worst_even_keel=worst(even),
        runtime_vs_reference_worst_trimmed=worst(trim_rows),
        refinement_change_worst=worst(convergence),
        tolerances=dict(volume_pct=0.5, lcb_m=0.02, kb_m=0.02),
        curves=curves)
    (HERE / 'part11-hydrostatics.json').write_text(json.dumps(report, indent=2))
    with open(HERE / 'part11-hydrostatic-curves.csv', 'w') as out:
        out.write(','.join(curves[0].keys()) + '\n')
        for row in curves:
            out.write(','.join(str(v) for v in row.values()) + '\n')

    failures = []
    for name, rows in (('runtime', [worst(even), worst(trim_rows)]), ('refinement', [worst(convergence)])):
        for row in rows:
            if abs(row['volume_pct']) > 0.5 or abs(row['lcb_m']) > 0.02 or abs(row['kb_m']) > 0.02:
                failures.append((name, row))
    print(json.dumps(dict(checks=checks, table=report['table'], even=worst(even), trimmed=worst(trim_rows),
                          refinement=worst(convergence)), indent=1))

    lightship = lightship_lcg(vertices, triangles)
    report['lightship_lcg_derivation'] = lightship
    (HERE / 'part11-hydrostatics.json').write_text(json.dumps(report, indent=2))
    print(json.dumps(lightship))
    if '--write-vessel' in sys.argv:
        write_vessel(ys, offsets)
    if failures:
        print('PART11_HYDROSTATICS|FAIL', failures)
        sys.exit(1)
    print('PART11_HYDROSTATICS|PASS')


def even_keel_draft(vertices, triangles, volume):
    lo, hi = 0.01, CAP_ABOVE_KEEL
    for _ in range(40):
        mid = 0.5 * (lo + hi)
        if exact(vertices, triangles, mid)['volume'] < volume:
            lo = mid
        else:
            hi = mid
    return 0.5 * (lo + hi)


def lightship_lcg(vertices, triangles):
    """Lightship LCG for which the default full-load inventory floats on even keel.

    The archive gives no lightship LCG. Floating level at the design full-load condition is a design
    convention, so this is an estimate derived from it, not a measured mass distribution.
    """
    data = json.loads(VESSEL.read_text())
    mass = data['massProperties']
    loaded = mass['loadedMassKg'] / 1000.0
    draft = even_keel_draft(vertices, triangles, loaded * 1000.0 / RHO)
    lcb = exact(vertices, triangles, draft)['lcb']
    payload = sum(c['initialKg'] / 1000.0 * c['centreLocalM']['z'] for c in data['loading']['compartments'])
    light = mass['lightshipMassKg'] / 1000.0
    return dict(full_load_t=loaded, even_keel_draft_m=draft, lcb_m=lcb,
                lightship_lcg_m=(loaded * lcb - payload) / light)


def write_vessel(ys, offsets):
    """Insert or replace hydrostatics.sectionTable without reformatting the hand-written file."""
    text = VESSEL.read_text()
    marker = '"sectionTable": {'
    if marker in text:
        start = text.index(marker)
        end = text.index('\n      ]\n    }', start) + len('\n      ]\n    }')
        text = text[:start].rstrip().rstrip(',') + text[end:]
    rows = ',\n'.join('        ' + json.dumps(dict(localZ=round(y, 4),
                                                   heightsAboveKeelM=[round(h, 4) for h, _ in row],
                                                   halfBreadthsM=[round(b, 4) for _, b in row]),
                                              separators=(', ', ': '))
                       for y, row in zip(ys, offsets))
    header = dict(enabled=True, estimated=True,
                  source='Art/Source/VolgoDon507B/part11_hydrostatics.py from the Part 10 closed hydrostatic surface',
                  halfStripsAcross=HALF_STRIPS, stripHalfWidthM=STRIP_HALF_WIDTH, tableHeightStepM=RUNTIME_DZ,
                  capHeightAboveKeelM=CAP_ABOVE_KEEL, minimumMeanDraftM=MIN_DRAFT, maximumMeanDraftM=MAX_DRAFT,
                  maximumTrimM=MAX_TRIM)
    block = ',\n    "sectionTable": {\n' + ',\n'.join(
        '      ' + json.dumps(k) + ': ' + json.dumps(v) for k, v in header.items()) + \
        ',\n      "stations": [\n' + rows + '\n      ]\n    }'
    anchor = text.index('"hydrostatics": {')
    close = text.index('\n  }', anchor)
    text = text[:close] + block + text[close:]
    json.loads(text)
    VESSEL.write_text(text)


if __name__ == '__main__':
    main()
