"""Meteor project 342U, pass 6: a lightweight interior proxy.

The bow saloon glazing is very large, so an empty white shell would read badly
from outside. This builds a floor, a dark deckhead, saloon bulkheads and simple
seat rows: enough to fill the view through the windows, not a modelled cabin.
It deliberately spends very little of the polygon budget.
"""
import bpy, bmesh, math

exec(compile(open('G:/Projects/ShipSim159/Art/Source/Meteor342U/common.py',
                  encoding='utf-8').read(), 'common.py', 'exec'))

model = bpy.data.collections['Meteor342U_Model']
root = bpy.data.objects['Meteor342U_ROOT']
DARK = bpy.data.materials['Meteor_InteriorDark']
SEATS = bpy.data.materials['Meteor_InteriorSeats']
DECK = bpy.data.materials['Meteor_Deck']

for ob in list(model.objects):
    if ob.name.startswith('InteriorProxy'):
        bpy.data.objects.remove(ob, do_unlink=True)

SALOONS = [(1.95, 6.70), (8.30, 17.80), (23.45, 30.90)]
FLOOR_H = 0.22            # saloon sole above the weather deck
HEAD_H = 2.30             # deckhead height above the weather deck


def inner(a, h):
    """Half-breadth just inside the superstructure shell."""
    frac = 1.0
    for i in range(len(SUP_SECTION) - 1):
        (f0, h0), (f1, h1) = SUP_SECTION[i], SUP_SECTION[i + 1]
        if h0 <= h <= h1:
            t = 0.0 if h1 == h0 else (h - h0) / (h1 - h0)
            frac = f0 + (f1 - f0) * t
            break
    return max(lerp_table(B_SUP, a) * frac - 0.10, 0.02)


# ------------------------------------------------------------ sole, deckhead
verts, faces = [], []
stations = []
for (a0, a1) in SALOONS:
    n = max(3, int((a1 - a0) / 1.1))
    stations += [a0 + (a1 - a0) * i / n for i in range(n + 1)]
stations = sorted(set(round(a, 3) for a in stations))
for a in stations:
    hs = lerp_table(H_SCALE, a)
    zf = deck_z(a) + FLOOR_H
    zh = deck_z(a) + min(HEAD_H * hs, HEAD_H)
    verts.extend([(0.0, y_of(a), zf), (inner(a, FLOOR_H), y_of(a), zf),
                  (inner(a, min(HEAD_H * hs, HEAD_H)), y_of(a), zh),
                  (0.0, y_of(a), zh)])
for s in range(len(stations) - 1):
    a_mid = 0.5 * (stations[s] + stations[s + 1])
    if not any(a0 - 0.01 <= a_mid <= a1 + 0.01 for (a0, a1) in SALOONS):
        continue
    for i in (0, 2):
        p, q = s * 4 + i, (s + 1) * 4 + i
        faces.append((p, p + 1, q + 1, q))
sole = new_mesh('InteriorProxy_Sole', verts, faces, model, smooth=False)
sole.parent = root
recalc(sole)
assign(sole, [DARK])
add_mirror(sole)

# Bulkheads closing each saloon, so nothing shows through end to end.
verts, faces = [], []
for (a0, a1) in SALOONS:
    for a in (a0, a1):
        hs = lerp_table(H_SCALE, a)
        zf = deck_z(a) + FLOOR_H
        zh = deck_z(a) + min(HEAD_H * hs, HEAD_H)
        base = len(verts)
        verts.extend([(0.0, y_of(a), zf), (inner(a, FLOOR_H), y_of(a), zf),
                      (inner(a, min(HEAD_H * hs, HEAD_H)), y_of(a), zh),
                      (0.0, y_of(a), zh)])
        faces.append((base, base + 1, base + 2, base + 3))
bulk = new_mesh('InteriorProxy_Bulkheads', verts, faces, model, smooth=False)
bulk.parent = root
recalc(bulk)
assign(bulk, [DARK])
add_mirror(bulk)


# ------------------------------------------------------------ seat rows
# Two seats a side in the bow and aft saloons, two a side amidships; one row
# per window pitch, which is what the real arrangement looks like from outside.
verts, faces = [], []


def seat(cx, cy, cz, w=0.46, d=0.44, h=0.52):
    base = len(verts)
    verts.extend([(cx - w / 2, cy - d / 2, cz), (cx + w / 2, cy - d / 2, cz),
                  (cx + w / 2, cy + d / 2, cz), (cx - w / 2, cy + d / 2, cz),
                  (cx - w / 2, cy - d / 2, cz + h * 0.32),
                  (cx + w / 2, cy - d / 2, cz + h * 0.32),
                  (cx + w / 2, cy + d / 2, cz + h * 0.32),
                  (cx - w / 2, cy + d / 2, cz + h * 0.32),
                  (cx - w / 2, cy + d / 2 - 0.10, cz + h),
                  (cx + w / 2, cy + d / 2 - 0.10, cz + h),
                  (cx + w / 2, cy + d / 2, cz + h),
                  (cx - w / 2, cy + d / 2, cz + h)])
    faces.extend([(base + 4, base + 5, base + 6, base + 7),
                  (base, base + 1, base + 5, base + 4),
                  (base + 1, base + 2, base + 6, base + 5),
                  (base + 3, base + 7, base + 6, base + 2),
                  (base, base + 4, base + 7, base + 3),
                  (base + 7, base + 6, base + 9, base + 8),
                  (base + 11, base + 10, base + 6 + 0, base + 7)])
    faces.append((base + 8, base + 9, base + 10, base + 11))


for (a0, a1) in SALOONS:
    rows = int((a1 - a0 - 0.6) / 0.76)
    for r in range(rows):
        a = a0 + 0.55 + r * 0.76
        zf = deck_z(a) + FLOOR_H
        width = inner(a, 0.75)
        if width < 0.9:
            continue
        for x in (width * 0.40, width * 0.78):
            if x + 0.24 < width:
                seat(x, y_of(a), zf)
seats = new_mesh('InteriorProxy_Seats', verts, faces, model, smooth=False)
seats.parent = root
recalc(seats)
assign(seats, [SEATS])
add_mirror(seats)

print('PASS6|interior proxy built: sole, deckhead, bulkheads and %d seat quads'
      % (len(seats.data.polygons)))
