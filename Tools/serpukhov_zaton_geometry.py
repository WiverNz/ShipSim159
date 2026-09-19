"""Regenerate Assets/ShipSimulator/Data/Scenarios/SerpukhovZaton.json.

The Serpukhov zaton scene is built from surveyed and traced geography rather than from numbers
typed into the builder, so this script is where that geography comes from. It needs network access
and Pillow (pip install pillow), and it caches every download under --cache so a rerun is cheap.

    python Tools/serpukhov_zaton_geometry.py

Sources, all recorded in the "provenance" block of the output:
  * shorelines and land cover  - OpenStreetMap (ODbL)
  * terrain relief             - AWS terrarium terrain tiles (about 30 m source)
  * moored craft               - traced by eye from Esri World Imagery, see TRACED_MOORINGS

What this script does NOT produce is anything official: channel widths and the navigation-mark
category come from the Rosmorrechflot order quoted in SerpukhovZatonScenario_Sources.md, and the
depths, currents, shoals and speed limits are scenario design. Both are written in here by hand.
"""

import argparse
import collections
import json
import math
import os
import statistics
import sys
import urllib.request

from PIL import Image

# ---------------------------------------------------------------------------
# Local frame: origin at the mouth of the Nara on the Oka, +z up the river.
# ---------------------------------------------------------------------------
LAT0, LON0 = 54.88440, 37.41060
ENTRANCE = (54.892753, 37.396763)
MPD_LAT = 111132.0
MPD_LON = 111320.0 * math.cos(math.radians(54.889))
BETA = math.atan2((ENTRANCE[1] - LON0) * MPD_LON, (ENTRANCE[0] - LAT0) * MPD_LAT)
CB, SB = math.cos(BETA), math.sin(BETA)

OSM_API = "https://api.openstreetmap.org/api/0.6"
SAT_TILES = "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile"
DEM_TILES = "https://s3.amazonaws.com/elevation-tiles-prod/terrarium"
# Water outlines: the basin, the navigable Nara, and the Oka bank.
OSM_BASIN, OSM_NARA, OSM_OKA_BANK = 2174292, 2174294, 543276172
LANDCOVER_BBOX = (37.3845, 54.8770, 37.4275, 54.9025)


def loc(lat, lon):
    east = (lon - LON0) * MPD_LON
    north = (lat - LAT0) * MPD_LAT
    return (-north * SB + east * CB, north * CB + east * SB)


def latlon(x, z):
    east = x * CB + z * SB
    north = -x * SB + z * CB
    return (LAT0 + north / MPD_LAT, LON0 + east / MPD_LON)


def point(x, z):
    return {"x": round(x, 1), "z": round(z, 1)}


def dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def download(url, path):
    if os.path.exists(path) and os.path.getsize(path) > 400:
        return path
    os.makedirs(os.path.dirname(path), exist_ok=True)
    request = urllib.request.Request(url, headers={"User-Agent": "ShipSim159-scenario/1.0"})
    with urllib.request.urlopen(request, timeout=90) as response:
        data = response.read()
    with open(path, "wb") as handle:
        handle.write(data)
    return path


# ---------------------------------------------------------------------------
# Shorelines
# ---------------------------------------------------------------------------
def relation_ring(cache, relation_id):
    path = download(f"{OSM_API}/relation/{relation_id}/full.json", f"{cache}/rel{relation_id}.json")
    data = json.load(open(path, encoding="utf-8"))
    nodes = {e["id"]: (e["lat"], e["lon"]) for e in data["elements"] if e["type"] == "node"}
    ways = {e["id"]: e["nodes"] for e in data["elements"] if e["type"] == "way"}
    relation = [e for e in data["elements"] if e["type"] == "relation"][0]
    segments = [list(ways[m["ref"]]) for m in relation["members"]
                if m["type"] == "way" and m["ref"] in ways]
    ring = segments.pop(0)
    while segments:
        for i, segment in enumerate(segments):
            if segment[0] == ring[-1]:
                ring += segment[1:]
            elif segment[-1] == ring[-1]:
                ring += segment[::-1][1:]
            elif segment[-1] == ring[0]:
                ring = segment[:-1] + ring
            elif segment[0] == ring[0]:
                ring = segment[::-1][:-1] + ring
            else:
                continue
            segments.pop(i)
            break
        else:
            break
    return [loc(*nodes[n]) for n in ring if n in nodes]


def midline(primary, other):
    line = []
    for p in primary:
        q = min(other, key=lambda o: dist(p, o))
        line.append(((p[0] + q[0]) * 0.5, (p[1] + q[1]) * 0.5, dist(p, q) * 0.5))
    return line


def resample(line, step):
    out, acc = [line[0]], 0.0
    for i in range(1, len(line)):
        acc += dist(line[i - 1][:2], line[i][:2])
        if acc >= step:
            out.append(line[i])
            acc = 0.0
    if out[-1] is not line[-1]:
        out.append(line[-1])
    return out


def smooth(line, passes=2):
    for _ in range(passes):
        nxt = [line[0]]
        for i in range(1, len(line) - 1):
            nxt.append(tuple(0.25 * line[i - 1][k] + 0.5 * line[i][k] + 0.25 * line[i + 1][k]
                             for k in range(3)))
        nxt.append(line[-1])
        line = nxt
    return line


def nearest_index(ring, target):
    return min(range(len(ring)), key=lambda i: dist(ring[i], target))


def centrelines(cache):
    basin = relation_ring(cache, OSM_BASIN)
    nara = relation_ring(cache, OSM_NARA)

    mouth, junction = loc(54.8844, 37.4106), loc(*ENTRANCE)
    i_mouth, i_junction = nearest_index(nara, mouth), nearest_index(nara, junction)
    lo, hi = min(i_mouth, i_junction), max(i_mouth, i_junction)
    bank_a, bank_b = nara[lo:hi + 1], nara[hi:] + nara[:lo + 1]
    if bank_a[0][1] > bank_a[-1][1]:
        bank_a = bank_a[::-1]
    if bank_b[0][1] > bank_b[-1][1]:
        bank_b = bank_b[::-1]
    nara_mid = midline(bank_a, bank_b) if len(bank_a) >= len(bank_b) else midline(bank_b, bank_a)
    nara_mid.sort(key=lambda p: p[1])
    nara_mid = smooth(resample(nara_mid, 110))
    # Smoothing cuts the bend apexes, so re-centre each sample between its own two banks.
    nara_mid = [(lambda p, q: ((p[0] + q[0]) * 0.5, (p[1] + q[1]) * 0.5, dist(p, q) * 0.5))(
        min(bank_a, key=lambda r: dist(r, c)), min(bank_b, key=lambda r: dist(r, c)))
        for c in [(m[0], m[1]) for m in nara_mid]]

    # The entrance is a single ring edge; cut there and walk each bank away from it.
    e0 = nearest_index(basin, loc(54.892854, 37.396366))
    e1 = nearest_index(basin, loc(54.892651, 37.397159))
    e0, e1 = min(e0, e1), max(e0, e1)
    side_nw, side_se = basin[e1:], basin[:e0 + 1][::-1]
    if dist(side_nw[0], junction) > dist(side_nw[-1], junction):
        side_nw = side_nw[::-1]
    if dist(side_se[0], junction) > dist(side_se[-1], junction):
        side_se = side_se[::-1]
    basin_mid = smooth(resample(midline(side_nw, side_se), 90))

    # The Nara polygon stops on a straight line across its mouth. Pushed down-river it overlaps the
    # Oka instead, so the junction has no edge that would read as a bank and shoal the entrance.
    low = sorted(range(len(nara)), key=lambda i: nara[i][1])[:3]
    nara = [(p[0], p[1] - 70.0) if i in low else p for i, p in enumerate(nara)]
    return nara, basin, nara_mid, basin_mid


def oka_polygon(cache, oka_dir, oka_normal):
    path = download(f"{OSM_API}/way/{OSM_OKA_BANK}/full.json", f"{cache}/way{OSM_OKA_BANK}.json")
    data = json.load(open(path, encoding="utf-8"))
    nodes = {e["id"]: (e["lat"], e["lon"]) for e in data["elements"] if e["type"] == "node"}
    way = [e for e in data["elements"] if e["type"] == "way"][0]
    bank = [loc(*nodes[n]) for n in way["nodes"] if n in nodes]

    def along(p):
        return p[0] * oka_dir[0] + p[1] * oka_dir[1]

    def across(p):
        return p[0] * oka_normal[0] + p[1] * oka_normal[1]

    step, lo, hi = 45.0, -1150.0, 950.0
    bins = {}
    for p in bank:
        u, v = along(p), across(p)
        # The same ring also traces the far bank and the islands; keep the near bank only.
        if not lo <= u <= hi or v < -90.0:
            continue
        key = int((u - lo) // step)
        v = min(v, 30.0)
        if key not in bins or v > bins[key][1]:
            bins[key] = (u, v)

    count = int((hi - lo) // step) + 1
    known = sorted(bins)
    filled = []
    for k in range(count):
        if k in bins:
            filled.append(bins[k][1])
            continue
        before = [j for j in known if j < k]
        after = [j for j in known if j > k]
        if before and after:
            j0, j1 = before[-1], after[0]
            t = (k - j0) / float(j1 - j0)
            filled.append(bins[j0][1] * (1 - t) + bins[j1][1] * t)
        elif before:
            filled.append(bins[before[-1]][1])
        elif after:
            filled.append(bins[after[0]][1])
        else:
            filled.append(0.0)
    edge = [((lo + k * step) * oka_dir[0] + filled[k] * oka_normal[0],
             (lo + k * step) * oka_dir[1] + filled[k] * oka_normal[1]) for k in range(count)]
    far = [(hi * oka_dir[0] - 700 * oka_normal[0], hi * oka_dir[1] - 700 * oka_normal[1]),
           (lo * oka_dir[0] - 700 * oka_normal[0], lo * oka_dir[1] - 700 * oka_normal[1])]
    return edge + far


# ---------------------------------------------------------------------------
# Water tests
# ---------------------------------------------------------------------------
def contains(poly, x, z):
    inside, n = False, len(poly)
    for i in range(n):
        j = (i - 1) % n
        if (poly[i][1] > z) != (poly[j][1] > z):
            cut = (poly[j][0] - poly[i][0]) * (z - poly[i][1]) / (poly[j][1] - poly[i][1]) + poly[i][0]
            if x < cut:
                inside = not inside
    return inside


def segment_distance(x, z, a, b):
    dx, dz = b[0] - a[0], b[1] - a[1]
    length = dx * dx + dz * dz
    t = max(0.0, min(1.0, ((x - a[0]) * dx + (z - a[1]) * dz) / length)) if length > 1e-9 else 0.0
    return math.hypot(x - (a[0] + dx * t), z - (a[1] + dz * t))


def boundary_edges(polys):
    """Water bodies that meet share edges that are not banks.

    Measuring to them would raise a sill across the basin entrance and the Nara mouth, so an edge
    counts only when the water does not continue on its far side. ScenarioGeometry does the same.
    """
    flags = []
    for index, poly in enumerate(polys):
        keep, n = [], len(poly)
        for i in range(n):
            a, b = poly[(i - 1) % n], poly[i]
            dx, dz = b[0] - a[0], b[1] - a[1]
            length = math.hypot(dx, dz)
            if length < 1e-4:
                keep.append(False)
                continue
            mx, mz = (a[0] + b[0]) * 0.5, (a[1] + b[1]) * 0.5
            nx, nz = -dz / length * 0.75, dx / length * 0.75
            outer = (mx - nx, mz - nz) if contains(poly, mx + nx, mz + nz) else (mx + nx, mz + nz)
            keep.append(not any(contains(p, *outer) for j, p in enumerate(polys) if j != index))
        flags.append(keep)
    return flags


def signed_shore(x, z, polys, flags=None):
    if flags is None:
        flags = boundary_edges(polys)
    nearest, inside = 1e9, False
    for index, poly in enumerate(polys):
        n = len(poly)
        for i in range(n):
            if flags[index][i]:
                nearest = min(nearest, segment_distance(x, z, poly[(i - 1) % n], poly[i]))
        inside |= contains(poly, x, z)
    return -nearest if inside else nearest


# ---------------------------------------------------------------------------
# Tiles
# ---------------------------------------------------------------------------
def deg2tile(lat, lon, zoom):
    n = 2 ** zoom
    return ((lon + 180.0) / 360.0 * n,
            (1.0 - math.asinh(math.tan(math.radians(lat))) / math.pi) / 2.0 * n)


def tile2deg(x, y, zoom):
    n = 2 ** zoom
    return (math.degrees(math.atan(math.sinh(math.pi * (1 - 2 * y / n)))), x / n * 360.0 - 180.0)


def mosaic(cache, kind, zoom, lat0, lon0, lat1, lon1):
    x0, y0 = deg2tile(lat1, lon0, zoom)
    x1, y1 = deg2tile(lat0, lon1, zoom)
    xs = list(range(int(x0), int(x1) + 1))
    ys = list(range(int(y0), int(y1) + 1))
    canvas = Image.new("RGB", (len(xs) * 256, len(ys) * 256))
    for ix, x in enumerate(xs):
        for iy, y in enumerate(ys):
            url = (f"{SAT_TILES}/{zoom}/{y}/{x}" if kind == "sat" else f"{DEM_TILES}/{zoom}/{x}/{y}.png")
            path = download(url, f"{cache}/tiles_{kind}_{zoom}/{x}_{y}.png")
            canvas.paste(Image.open(path).convert("RGB"), (ix * 256, iy * 256))
    top, left = tile2deg(min(xs), min(ys), zoom)
    bottom, right = tile2deg(max(xs) + 1, max(ys) + 1, zoom)
    return canvas, (top, bottom, left, right)


# ---------------------------------------------------------------------------
# Moored craft, traced by eye from the Esri World Imagery mosaic below.
# Bow px, stern px, beam px in that mosaic, then the name and the kind.
# ---------------------------------------------------------------------------
MOORING_MOSAIC = dict(zoom=18, lat0=54.8925, lon0=37.3950, lat1=54.8980, lon1=37.4065)
TRACED_MOORINGS = [
    (880, 800, 947, 985, 48, "Cargo barge 1", "barge"),
    (948, 758, 988, 972, 50, "Cargo barge 2", "barge"),
    (697, 878, 900, 1122, 12, "North mooring pier", "pier"),
    (735, 945, 800, 1060, 40, "Laid-up barge 3", "barge"),
    (800, 1035, 900, 1125, 42, "Laid-up barge 4", "barge"),
    (698, 908, 716, 938, 10, "Small craft 1", "craft"),
    (788, 845, 800, 878, 9, "Small craft 2", "craft"),
    (802, 935, 818, 962, 9, "Small craft 3", "craft"),
    (1200, 612, 1218, 700, 26, "Laid-up vessel 5", "vessel"),
    (1230, 616, 1248, 708, 28, "Laid-up vessel 6", "vessel"),
    (1292, 748, 1212, 800, 24, "Laid-up vessel 7", "vessel"),
    (1272, 812, 1196, 876, 30, "Laid-up vessel 8", "vessel"),
    (1274, 864, 1192, 928, 30, "Laid-up vessel 9", "vessel"),
    (1302, 892, 1198, 952, 36, "Laid-up vessel 10", "vessel"),
    (1180, 940, 1136, 958, 14, "Small craft 4", "craft"),
    (1148, 966, 1082, 990, 16, "Small craft 5", "craft"),
    (1288, 1008, 1152, 1128, 54, "Floating dock", "dock"),
    (1470, 900, 1505, 985, 30, "East arm debarcadere", "vessel"),
    (1610, 935, 1715, 1005, 38, "East arm covered barge", "barge"),
    (1640, 958, 1860, 1108, 10, "East arm pontoon walkway", "pier"),
    (303, 1548, 373, 1652, 28, "Beached hulk", "barge"),
]


def moorings(cache, polys):
    image, (top, bottom, left, right) = mosaic(cache, "sat", **MOORING_MOSAIC)
    width, height = image.size
    metres_per_px = (right - left) * MPD_LON / width

    def px2loc(px, py):
        return loc(top - py / height * (top - bottom), left + px / width * (right - left))

    out = []
    for bx, by, sx, sy, beam, name, kind in TRACED_MOORINGS:
        bow, stern = px2loc(bx, by), px2loc(sx, sy)
        cx, cz = (bow[0] + stern[0]) * 0.5, (bow[1] + stern[1]) * 0.5
        if not any(contains(p, cx, cz) for p in polys):
            print("  skipping %s: not over water" % name)
            continue
        dx, dz = bow[0] - stern[0], bow[1] - stern[1]
        out.append({"name": name, "kind": kind, "x": round(cx, 1), "z": round(cz, 1),
                    "headingDeg": round(math.degrees(math.atan2(dx, dz)) % 360.0, 1),
                    "lengthM": round(math.hypot(dx, dz), 1),
                    "widthM": round(beam * metres_per_px, 1)})
    return out


# ---------------------------------------------------------------------------
# Relief, land cover and buildings
# ---------------------------------------------------------------------------
ELEVATION_ORIGIN = (-1700.0, -1800.0)
ELEVATION_STEP = 25.0
ELEVATION_SIZE = (149, 193)


def elevation(cache, shorelines):
    image, (top, bottom, left, right) = mosaic(cache, "dem", 14, 54.855, 37.340, 54.925, 37.470)
    width, height = image.size
    pixels = image.load()

    def sample(lat, lon):
        fx = (lon - left) / (right - left) * width
        fy = (top - lat) / (top - bottom) * height
        x0, y0 = int(fx), int(fy)
        tx, ty = fx - x0, fy - y0

        def post(x, y):
            x, y = min(max(x, 0), width - 1), min(max(y, 0), height - 1)
            r, g, b = pixels[x, y]
            return (r * 256 + g + b / 256.0) - 32768.0

        return (post(x0, y0) * (1 - tx) * (1 - ty) + post(x0 + 1, y0) * tx * (1 - ty) +
                post(x0, y0 + 1) * (1 - tx) * ty + post(x0 + 1, y0 + 1) * tx * ty)

    river = statistics.median([sample(*latlon(p["x"], p["z"]))
                               for s in shorelines for p in s["points"][::2]])
    ox, oz = ELEVATION_ORIGIN
    cols, rows = ELEVATION_SIZE
    heights = [round(sample(*latlon(ox + c * ELEVATION_STEP, oz + r * ELEVATION_STEP)) - river, 2)
               for r in range(rows) for c in range(cols)]
    return {"originX": ox, "originZ": oz, "stepM": ELEVATION_STEP,
            "columns": cols, "rows": rows, "demWaterLevelM": round(river, 2),
            "note": "metres above the river surface; row-major, z outer, x inner",
            "heightsM": heights}


COVER = {"wood": "wood", "forest": "wood", "scrub": "scrub", "grassland": "meadow",
         "meadow": "meadow", "farmland": "farmland", "orchard": "orchard",
         "allotments": "allotments", "residential": "built", "industrial": "built",
         "retail": "built", "commercial": "built", "sand": "sand", "beach": "sand",
         "quarry": "sand", "wetland": "reeds", "village_green": "meadow", "park": "meadow",
         "cemetery": "meadow", "religious": "built", "farmyard": "built"}
COVER_BOX = (-520.0, 900.0, -620.0, 1950.0)


def min_area_box(points):
    best = None
    for degrees in range(0, 90, 3):
        angle = math.radians(degrees)
        ca, sa = math.cos(angle), math.sin(angle)
        us = [p[0] * ca + p[1] * sa for p in points]
        vs = [-p[0] * sa + p[1] * ca for p in points]
        area = (max(us) - min(us)) * (max(vs) - min(vs))
        if best is None or area < best[0]:
            cu, cv = (max(us) + min(us)) / 2, (max(vs) + min(vs)) / 2
            best = (area, degrees, cu * ca - cv * sa, cu * sa + cv * ca,
                    max(us) - min(us), max(vs) - min(vs))
    _, degrees, cx, cz, du, dv = best
    return cx, cz, (90.0 - degrees) % 360.0, du, dv


def land_and_buildings(cache, route):
    west, south, east, north = LANDCOVER_BBOX
    path = download(f"{OSM_API}/map.json?bbox={west},{south},{east},{north}", f"{cache}/landcover.json")
    data = json.load(open(path, encoding="utf-8"))
    nodes = {e["id"]: (e["lat"], e["lon"]) for e in data["elements"] if e["type"] == "node"}
    route_points = [(s["x"], s["z"]) for s in route]

    cover, buildings = [], []
    for element in data["elements"]:
        if element["type"] != "way":
            continue
        tags = element.get("tags") or {}
        points = [loc(*nodes[n]) for n in element.get("nodes", []) if n in nodes]
        if len(points) < 3:
            continue
        if "building" in tags:
            cx = sum(p[0] for p in points) / len(points)
            cz = sum(p[1] for p in points) / len(points)
            if not any((cx - a) ** 2 + (cz - b) ** 2 < 650.0 ** 2 for a, b in route_points):
                continue
            bx, bz, heading, du, dv = min_area_box(points)
            if not 5 <= max(du, dv) <= 160:
                continue
            kind = tags["building"]
            try:
                levels = float(tags.get("building:levels"))
            except (TypeError, ValueError):
                levels = None
            if kind in ("church", "cathedral", "chapel"):
                height, roof = 19.0, "church"
            elif levels:
                height, roof = max(3.0, levels * 3.1 + 1.2), "flat" if levels >= 4 else "pitched"
            elif kind in ("garages", "shed", "hut", "warehouse"):
                height, roof = 3.4, "flat"
            elif kind in ("industrial", "works", "commercial", "retail"):
                height, roof = 7.0, "flat"
            else:
                height, roof = 6.2, "pitched"
            buildings.append({"name": tags.get("name", "") or kind, "roof": roof,
                              "x": round(bx, 1), "z": round(bz, 1), "headingDeg": round(heading, 1),
                              "lengthM": round(du, 1), "widthM": round(dv, 1),
                              "heightM": round(height, 1)})
            continue
        found = None
        for key in ("landuse", "natural", "leisure"):
            if tags.get(key) in COVER:
                found = COVER[tags[key]]
                break
        if found is None:
            continue
        if not any(COVER_BOX[0] <= p[0] <= COVER_BOX[1] and COVER_BOX[2] <= p[1] <= COVER_BOX[3]
                   for p in points):
            continue
        if len(points) > 90:
            points = points[::len(points) // 90 + 1]
        cover.append({"cover": found, "name": tags.get("name", "") or "",
                      "points": [point(*p) for p in points]})
    return cover, buildings


# ---------------------------------------------------------------------------
# Fairway, currents and shoals: scenario design, written here by hand.
# ---------------------------------------------------------------------------
OKA_DIR = (0.930, -0.367)
OKA_NORMAL = (0.367, 0.930)


def sample(x, z, half, centre_depth, edge_depth, limit):
    return {"x": round(x, 2), "z": round(z, 2), "leftWidthM": half, "rightWidthM": half,
            "centerDepthM": centre_depth, "leftEdgeDepthM": edge_depth,
            "rightEdgeDepthM": edge_depth, "speedLimitMps": limit}


def fairway(nara_mid, basin_mid):
    ox, oz = -44.0, -112.0
    route = [sample(ox + OKA_DIR[0] * d, oz + OKA_DIR[1] * d, 15.0, 2.8, 1.5, 4.0)
             for d in (450, 320, 200, 105)]
    route.append(sample(-2.0, -46.0, 13.0, 2.6, 1.4, 3.4))
    for x, z, _ in nara_mid[1:-1]:
        route.append(sample(x, z, 10.0, 2.4, 1.2, 3.0 if z < 1050 else 2.4))
    route.append(sample(0.0, 1283.0, 18.0, 2.4, 1.3, 1.8))
    for x, z, half in basin_mid[1:-1]:
        route.append(sample(x, z, min(60.0, max(24.0, half - 8.0)), 3.2, 1.6, 1.2))
    route.append(sample(470.0, 1406.0, 30.0, 3.0, 1.5, 1.2))
    return route


CURRENTS = [
    {"name": "Oka set", "x": 120, "z": -230, "widthM": 900, "lengthM": 460,
     "velocityXMps": round(OKA_DIR[0] * 0.55, 3), "velocityZMps": round(OKA_DIR[1] * 0.55, 3),
     "blendM": 40, "priority": 2, "overrideAmbient": True},
    {"name": "Mouth eddy", "x": -10, "z": 60, "widthM": 140, "lengthM": 170,
     "velocityXMps": 0.12, "velocityZMps": -0.05, "blendM": 30, "priority": 1,
     "overrideAmbient": True},
    {"name": "Basin still water", "x": 260, "z": 1370, "widthM": 640, "lengthM": 220,
     "velocityXMps": 0.0, "velocityZMps": 0.0, "blendM": 35, "priority": 3,
     "overrideAmbient": True},
]
HAZARDS = [
    {"name": "Mouth bar", "x": 18, "z": 78, "widthM": 90, "lengthM": 110,
     "depthReductionM": 0.9, "bottom": "Sand"},
    {"name": "Inner bend silt", "x": -118, "z": 640, "widthM": 70, "lengthM": 190,
     "depthReductionM": 0.7, "bottom": "Silt"},
    {"name": "Outer bend silt", "x": -108, "z": 1112, "widthM": 60, "lengthM": 150,
     "depthReductionM": 0.6, "bottom": "Silt"},
    {"name": "Entrance sill", "x": 6, "z": 1268, "widthM": 70, "lengthM": 60,
     "depthReductionM": 0.5, "bottom": "Silt"},
]
QUAYS = [("Passenger berth", (54.89425, 37.39668), 56, 10, 2.4),
         ("Yacht club pontoons", (54.89521, 37.39749), 40, 6, 1.3)]

PROVENANCE = {
    "shorelines": "OpenStreetMap water outlines (ODbL), extracted 2026-09-19.",
    "channelWidths": "Rosmorrechflot order ZD-496-r appendix 2: Nara 20 m, Oka 30 m guaranteed width.",
    "depths": "ESTIMATED navigation-season depths. The published guarantee is 1.00 m at the Kashira projected level.",
    "speedLimits": "ESTIMATED.",
    "currents": "ESTIMATED, scaled from the published mean Nara discharge of 5.5 m3/s.",
    "hazards": "ESTIMATED. No public survey of the basin exists.",
    "landmarks": "Positions from OpenStreetMap; shapes are placeholder massing, not surveyed buildings.",
    "elevation": ("Terrain tiles (terrarium, about 30 m source) sampled 2026-09-19 and levelled to "
                  "the river surface. Not a survey."),
    "landcover": "OpenStreetMap landuse/natural polygons (ODbL), extracted 2026-09-19.",
    "buildings": ("OpenStreetMap footprints (ODbL) reduced to minimum-area boxes; ESTIMATED heights "
                  "from building:levels or by type."),
    "moorings": ("APPROXIMATE. Traced by eye from Esri World Imagery in September 2026: positions "
                 "and sizes are read off the picture, vessel types are not established, and laid-up "
                 "craft move between seasons."),
}


def place_quays(polys, flags):
    out = []
    for name, ll, length, width, height in QUAYS:
        x, z = loc(*ll)
        for _ in range(60):
            here = signed_shore(x, z, polys, flags)
            if abs(here + 6.0) < 0.6:
                break
            gx = (signed_shore(x + 1, z, polys, flags) - signed_shore(x - 1, z, polys, flags)) * 0.5
            gz = (signed_shore(x, z + 1, polys, flags) - signed_shore(x, z - 1, polys, flags)) * 0.5
            norm = math.hypot(gx, gz) or 1.0
            step = max(-12.0, min(12.0, -6.0 - here))
            x, z = round(x + gx / norm * step, 1), round(z + gz / norm * step, 1)
        best = None
        for poly in polys:
            for i in range(len(poly)):
                a, b = poly[i - 1], poly[i]
                dx, dz = b[0] - a[0], b[1] - a[1]
                length_sqr = dx * dx + dz * dz
                if length_sqr < 1e-9:
                    continue
                t = max(0.0, min(1.0, ((x - a[0]) * dx + (z - a[1]) * dz) / length_sqr))
                d = math.hypot(x - (a[0] + dx * t), z - (a[1] + dz * t))
                if best is None or d < best[0]:
                    best = (d, dx, dz)
        out.append({"name": name, "kind": "quay", "x": x, "z": z,
                    "headingDeg": round(math.degrees(math.atan2(best[1], best[2])) % 360.0, 1),
                    "lengthM": length, "widthM": width, "heightM": height,
                    "shoreDistanceM": round(signed_shore(x, z, polys, flags), 1)})
    return out


def build(cache):
    print("shorelines...")
    nara, basin, nara_mid, basin_mid = centrelines(cache)
    oka = oka_polygon(cache, OKA_DIR, OKA_NORMAL)
    polys = [nara, basin, oka]
    shorelines = [{"name": "nara", "points": [point(*p) for p in nara]},
                  {"name": "basin", "points": [point(*p) for p in basin]},
                  {"name": "oka", "points": [point(*p) for p in oka]}]

    doc = collections.OrderedDict()
    doc["identity"] = {"id": "serpukhov-zaton", "displayName": "Serpukhov Zaton",
                       "sceneName": "SerpukhovZatonScene"}
    doc["frame"] = {"originLatitudeDeg": LAT0, "originLongitudeDeg": LON0,
                    "upRiverBearingDeg": round(math.degrees(BETA) % 360.0, 3),
                    "note": ("Local metres. +z runs up the Nara from its mouth on the Oka, "
                             "+x is starboard of it.")}
    doc["provenance"] = PROVENANCE
    print("relief...")
    doc["elevation"] = elevation(cache, shorelines)
    print("land cover and buildings...")
    doc["route"] = fairway(nara_mid, basin_mid)
    doc["landcover"], doc["buildings"] = land_and_buildings(cache, doc["route"])
    print("moored craft...")
    doc["moorings"] = moorings(cache, polys)
    doc["ambientCurrentZMps"] = -0.15
    doc["shorelines"] = shorelines
    flags = boundary_edges(polys)
    doc["landmarks"] = place_quays(polys, flags)
    doc["currentRegions"] = CURRENTS
    doc["hazards"] = HAZARDS

    # The fairway has to stay in the water it is drawn on, whatever the sources say today.
    for s in doc["route"]:
        clearance = signed_shore(s["x"], s["z"], polys, flags)
        if clearance >= -s["leftWidthM"] * 0.5:
            raise SystemExit("fairway sample at %.0f, %.0f is too close to the bank (%.1f m)"
                             % (s["x"], s["z"], clearance))
    print("route %d samples, landcover %d, buildings %d, moorings %d, elevation %dx%d"
          % (len(doc["route"]), len(doc["landcover"]), len(doc["buildings"]), len(doc["moorings"]),
             doc["elevation"]["columns"], doc["elevation"]["rows"]))
    return doc


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--out", default=os.path.join(
        here, "..", "Assets", "ShipSimulator", "Data", "Scenarios", "SerpukhovZaton.json"))
    parser.add_argument("--cache", default=os.path.join(here, ".cache", "serpukhov"))
    args = parser.parse_args()
    doc = build(args.cache)
    out = os.path.abspath(args.out)
    with open(out, "w", encoding="utf-8") as handle:
        json.dump(doc, handle, ensure_ascii=False, indent=1)
    print("written", out)


if __name__ == "__main__":
    sys.exit(main())
