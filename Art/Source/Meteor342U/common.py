"""Shared helpers and hull tables for the Meteor 342U source file.

Each Blender MCP execution gets its own namespace, so every modelling pass
execs this file first instead of relying on state left by the previous one.

Frame: metres, +Y forward, +X starboard, +Z up. Loaded waterline Z = 0,
baseline Z = -1.05, origin midship. a is metres aft of the stem.
"""
import bpy, bmesh, math

BASE = 'G:/Projects/ShipSim159'
LOA = 34.6
Y_STEM = LOA / 2.0
SCENE = 'Meteor342U_Studio'


def lerp_table(table, a):
    if a <= table[0][0]:
        return table[0][1]
    if a >= table[-1][0]:
        return table[-1][1]
    for i in range(len(table) - 1):
        a0, v0 = table[i]
        a1, v1 = table[i + 1]
        if a0 <= a <= a1:
            t = 0.0 if a1 == a0 else (a - a0) / (a1 - a0)
            return v0 + (v1 - v0) * t
    return table[-1][1]


def y_of(a):
    return Y_STEM - a


def a_of(y):
    return Y_STEM - y


def new_mesh(name, verts, faces, collection, smooth=True):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    if smooth:
        for p in mesh.polygons:
            p.use_smooth = True
    ob = bpy.data.objects.new(name, mesh)
    collection.objects.link(ob)
    return ob


def recalc(ob, dist=1e-5):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=dist)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-7)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(ob.data)
    bm.free()
    ob.data.update()


def add_mirror(ob):
    m = ob.modifiers.new('Mirror', 'MIRROR')
    m.use_axis = (True, False, False)
    m.use_clip = True
    m.merge_threshold = 1e-4
    return m


def add_bevel(ob, width=0.018, segments=2, angle=55.0):
    m = ob.modifiers.new('Edge bevel', 'BEVEL')
    m.width = width
    m.segments = segments
    m.limit_method = 'ANGLE'
    m.angle_limit = math.radians(angle)
    m.harden_normals = True
    return m


def assign(ob, mats):
    ob.data.materials.clear()
    for m in mats:
        ob.data.materials.append(m)


def box(name, collection, centre, size, mat, smooth=False):
    cx, cy, cz = centre
    sx, sy, sz = (s * 0.5 for s in size)
    verts = [(cx - sx, cy - sy, cz - sz), (cx + sx, cy - sy, cz - sz),
             (cx + sx, cy + sy, cz - sz), (cx - sx, cy + sy, cz - sz),
             (cx - sx, cy - sy, cz + sz), (cx + sx, cy - sy, cz + sz),
             (cx + sx, cy + sy, cz + sz), (cx - sx, cy + sy, cz + sz)]
    faces = [(0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5),
             (2, 3, 7, 6), (3, 0, 4, 7)]
    ob = new_mesh(name, verts, faces, collection, smooth=smooth)
    assign(ob, [mat])
    return ob


def tube(name, collection, p0, p1, r0, r1, mat, segments=12, caps=True):
    """A tapered cylinder between two points."""
    from mathutils import Vector
    a = Vector(p0)
    b = Vector(p1)
    axis = (b - a)
    length = axis.length
    quat = axis.to_track_quat('Z', 'Y')
    verts, faces = [], []
    for i in range(segments):
        t = 2.0 * math.pi * i / segments
        for (r, base) in ((r0, a), (r1, b)):
            local = Vector((math.cos(t) * r, math.sin(t) * r, 0.0))
            verts.append(tuple(base + quat @ local))
    for i in range(segments):
        j = (i + 1) % segments
        faces.append((i * 2, i * 2 + 1, j * 2 + 1, j * 2))
    if caps:
        verts.append(tuple(a))
        verts.append(tuple(b))
        ci, cj = len(verts) - 2, len(verts) - 1
        for i in range(segments):
            j = (i + 1) % segments
            faces.append((ci, j * 2, i * 2))
            faces.append((cj, i * 2 + 1, j * 2 + 1))
    ob = new_mesh(name, verts, faces, collection)
    assign(ob, [mat])
    return ob


def aerofoil(chord, thickness, station_count=14):
    """Half a symmetric NACA-style section, nose to tail, as (x, z) pairs."""
    pts = []
    for i in range(station_count + 1):
        t = i / station_count
        x = t * chord
        yt = 5.0 * thickness * chord * (0.2969 * math.sqrt(max(t, 0.0)) - 0.1260 * t
                                        - 0.3516 * t ** 2 + 0.2843 * t ** 3
                                        - 0.1015 * t ** 4)
        pts.append((x, yt))
    return pts


def foil_ring(chord, thickness, segments=14):
    """Closed aerofoil outline, counter-clockwise, centred on the leading edge."""
    upper = aerofoil(chord, thickness, segments)
    ring = [(x, +z) for (x, z) in upper]
    ring += [(x, -z) for (x, z) in reversed(upper[1:-1])]
    return ring


# ------------------------------------------------------------------ hull lines

B_HULL = [(0.00, 0.035), (0.50, 0.42), (1.00, 0.86), (1.50, 1.24), (2.00, 1.56),
          (3.00, 2.02), (4.00, 2.33), (5.00, 2.55), (6.00, 2.71), (7.00, 2.82),
          (8.00, 2.90), (9.00, 2.95), (10.00, 2.98), (12.00, 3.00), (24.00, 3.00),
          (26.00, 2.99), (28.00, 2.94), (29.50, 2.84), (31.00, 2.58),
          (32.00, 2.22), (33.00, 1.62), (34.00, 0.78), (34.60, 0.05)]

Z_DECK = [(0.00, 1.40), (1.00, 1.35), (2.00, 1.30), (5.50, 1.20),
          (6.80, 1.10), (8.20, 0.42), (27.00, 0.42), (29.00, 0.47),
          (30.50, 0.58), (32.00, 0.80), (33.20, 1.02), (34.60, 1.24)]

Z_KNUCK = [(0.00, 0.80), (1.00, 0.34), (2.00, 0.10), (3.00, -0.07), (4.00, -0.17),
           (6.00, -0.26), (8.00, -0.30), (12.00, -0.32), (26.00, -0.32),
           (28.00, -0.28), (30.00, -0.16), (31.50, 0.06), (33.00, 0.50),
           (34.00, 0.92), (34.60, 1.24)]

K_FRAC = [(0.00, 0.60), (1.00, 0.63), (2.00, 0.70), (3.00, 0.76), (4.00, 0.81),
          (6.00, 0.88), (8.00, 0.92), (11.00, 0.955), (14.00, 0.972),
          (27.00, 0.972), (30.00, 0.94), (32.00, 0.88), (34.00, 0.80),
          (34.60, 0.70)]

Z_KEEL = [(0.00, 0.80), (0.60, 0.44), (1.00, 0.24), (1.50, 0.02), (2.00, -0.16),
          (3.00, -0.47), (4.00, -0.72), (5.00, -0.89), (6.00, -0.98),
          (7.00, -1.025), (8.00, -1.045), (10.00, -1.05), (24.00, -1.05),
          (25.50, -1.04), (27.00, -0.99), (28.50, -0.88), (30.00, -0.68),
          (31.50, -0.36), (32.50, -0.02), (33.50, 0.52), (34.20, 0.95),
          (34.60, 1.24)]

FLARE = [(0.00, 2.30), (2.00, 2.05), (5.00, 1.70), (9.00, 1.35), (13.00, 1.12),
         (27.00, 1.10), (31.00, 1.25), (34.60, 1.50)]

BOTTOM = [(0.00, 1.10), (3.00, 1.28), (6.00, 1.42), (10.00, 1.58), (16.00, 1.70),
          (26.00, 1.68), (30.00, 1.50), (33.00, 1.25), (34.60, 1.10)]

LEDGE = [(0.00, 0.0), (1.60, 0.0), (3.00, 0.16), (5.00, 0.30), (7.00, 0.35),
         (29.00, 0.35), (31.00, 0.30), (32.50, 0.16), (33.60, 0.0), (34.60, 0.0)]

N_BOTTOM = 7
N_SIDE = 5
STATIONS = ([0.0, 0.25, 0.5, 0.8, 1.2, 1.7, 2.3, 3.0, 3.8, 4.6, 5.5, 6.5, 7.5,
             8.5, 9.5, 10.5, 11.5] + [12.5 + i for i in range(15)] +
            [27.5, 28.5, 29.5, 30.4, 31.2, 32.0, 32.7, 33.3, 33.8, 34.2, 34.6])


def hull_section(a):
    bh = lerp_table(B_HULL, a)
    zd = lerp_table(Z_DECK, a)
    zk = lerp_table(Z_KNUCK, a)
    zc = lerp_table(Z_KEEL, a)
    bk = bh * lerp_table(K_FRAC, a)
    pb = lerp_table(BOTTOM, a)
    pf = lerp_table(FLARE, a)
    pts = []
    for i in range(N_BOTTOM + 1):
        t = i / N_BOTTOM
        pts.append((bk * t, zc + (zk - zc) * (t ** pb)))
    for i in range(1, N_SIDE + 1):
        t = i / N_SIDE
        pts.append((bk + (bh - bk) * (t ** pf), zk + (zd - zk) * t))
    return pts


def hull_bottom_z(a):
    return lerp_table(Z_KEEL, a)


# --------------------------------------------------------- superstructure

B_SUP = [(1.70, 0.09), (1.82, 0.46), (2.00, 0.83), (2.25, 1.19), (2.60, 1.51),
         (3.10, 1.81), (3.75, 2.06), (4.60, 2.25), (5.60, 2.40), (6.90, 2.54),
         (8.30, 2.65), (10.00, 2.74), (12.00, 2.82), (14.50, 2.89), (16.50, 2.92),
         (26.00, 2.92), (28.00, 2.86), (29.50, 2.74), (31.20, 2.46)]

# Photographic silhouette controls, estimated above the loaded waterline.
ROOF_HEIGHT = [(1.70, 2.18), (2.00, 2.44), (2.60, 2.65), (3.50, 2.76),
               (5.20, 2.78), (8.20, 2.72), (27.00, 2.72), (29.50, 2.82),
               (31.20, 2.94)]
H_SCALE = [(a, (lerp_table(ROOF_HEIGHT, a) - lerp_table(Z_DECK, a)) / 2.835)
           for a in sorted(set([a for a, _ in ROOF_HEIGHT] +
                               [a for a, _ in Z_DECK if 1.7 <= a <= 31.2]))]

SUP_SECTION = [(1.000, 0.00), (0.998, 0.72), (0.985, 1.10), (0.967, 1.36),
               (0.938, 1.68), (0.896, 2.02), (0.848, 2.30), (0.795, 2.52),
               (0.750, 2.64), (0.670, 2.735), (0.530, 2.795), (0.300, 2.825),
               (0.000, 2.835)]
ROOF_RING = 7
SUP_FWD = 1.70
SUP_AFT = 31.2
VISOR_TIP = 34.45
BAND_FRAC = 0.978

WH_FWD, WH_AFT, WH_TOP = 6.75, 10.55, 3.85
WH_BASE = 2.67
WH_PLAN = [(6.75, 0.72), (7.05, 1.06), (7.55, 1.28), (8.30, 1.38), (9.40, 1.38),
           (10.10, 1.30), (10.55, 1.08)]

SHAFT_X = 1.6
PROP_A = 30.8
PROP_Z = -1.78


def deck_z(a):
    return lerp_table(Z_DECK, a)


def sup_xyz(a, frac, h):
    return (lerp_table(B_SUP, a) * frac, y_of(a),
            deck_z(a) + h * lerp_table(H_SCALE, a))


def roof_crown_z(a):
    a = min(a, SUP_AFT)
    return deck_z(a) + SUP_SECTION[-1][1] * lerp_table(H_SCALE, a)


def band_x(a):
    return lerp_table(B_SUP, a) * BAND_FRAC


def hull_x_at_z(a, z):
    """Half-breadth of the hull surface at station a and height z."""
    pts = hull_section(a)
    for i in range(len(pts) - 1):
        (x0, z0), (x1, z1) = pts[i], pts[i + 1]
        lo, hi = (z0, z1) if z0 <= z1 else (z1, z0)
        if lo - 1e-9 <= z <= hi + 1e-9:
            t = 0.0 if z1 == z0 else (z - z0) / (z1 - z0)
            return x0 + (x1 - x0) * t
    return pts[-1][0] if z > pts[-1][1] else pts[0][0]
