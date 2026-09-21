"""Meteor project 342U, pass 5: paint bands, deck fittings, mast and rails.

The blue bands are thin raised strips rather than a texture, so the livery
survives without a painted atlas and cannot z-fight. Fittings follow the
photographs of Meteor-191 as 342U: roof mushroom vents, four life raft
canisters a side, a boarding fender, blue guard rails aft, and the
wheelhouse mast with its open-array radar.
"""
import bpy, bmesh, math
from mathutils import Vector

exec(compile(open('G:/Projects/ShipSim159/Art/Source/Meteor342U/common.py',
                  encoding='utf-8').read(), 'common.py', 'exec'))

model = bpy.data.collections['Meteor342U_Model']
root = bpy.data.objects['Meteor342U_ROOT']
HULL = bpy.data.materials['Meteor_HullPaint']
BLUE = bpy.data.materials['Meteor_SecondaryPaint']
STEEL = bpy.data.materials['Meteor_Steel']
RUBBER = bpy.data.materials['Meteor_Rubber']
DARK = bpy.data.materials['Meteor_InteriorDark']
LIGHTS = bpy.data.materials['Meteor_Lights']
GLASS = bpy.data.materials['Meteor_Glass']

PREFIX = ('Paint_', 'Roof_Vent', 'Mast', 'Radar', 'Antenna', 'Searchlight',
          'Railing', 'LifeRaft', 'Fender', 'Bollard', 'NavLight', 'Handrail',
          'RoofIntake', 'Ladder', 'Landmark_', 'Anchor', 'Light_', 'EngineHatch', 'RaftStrap')
EXACT = ('Waterline', 'BowReference', 'SternReference', 'StarboardReference',
         'UpReference', 'CameraNavigator', 'PropPoint_S', 'PropPoint_P',
         'FoilPoint_Bow', 'FoilPoint_Stern', 'Handrail_Side')
for ob in list(model.objects):
    if ob.name.startswith(PREFIX) or ob.name in EXACT:
        bpy.data.objects.remove(ob, do_unlink=True)


def sup_frac_at(h):
    """Half-breadth fraction of the superstructure section at height h."""
    for i in range(len(SUP_SECTION) - 1):
        (f0, h0), (f1, h1) = SUP_SECTION[i], SUP_SECTION[i + 1]
        if h0 <= h <= h1:
            t = 0.0 if h1 == h0 else (h - h0) / (h1 - h0)
            return f0 + (f1 - f0) * t
    return SUP_SECTION[-1][0]


def strip(name, samples, mat, offset=0.014, thickness=0.016):
    """samples: [(x, y, z_low, z_high)] along the ship, outward +X."""
    verts, faces = [], []
    for (x, y, z0, z1) in samples:
        verts.append((x + offset, y, z0))
        verts.append((x + offset, y, z1))
    for s in range(len(samples) - 1):
        p, q = s * 2, (s + 1) * 2
        faces.append((p, p + 1, q + 1, q))
    ob = new_mesh(name, verts, faces, model, smooth=True)
    ob.parent = root
    sol = ob.modifiers.new('Paint thickness', 'SOLIDIFY')
    sol.thickness = thickness
    sol.offset = -1.0
    recalc(ob)
    assign(ob, [mat])
    add_mirror(ob)
    return ob


# ------------------------------------------------------ superstructure band
verts, faces = [], []
for i in range(149):
    a = SUP_FWD + (SUP_AFT - SUP_FWD) * i / 148
    for h in (2.30, 2.51):
        x, y, z = sup_xyz(a, sup_frac_at(h), h)
        verts.append((x + 0.020, y, z))
    if i:
        p = 2 * (i - 1)
        faces.append((p, p + 1, p + 3, p + 2))
band = new_mesh('Paint_SheerBand', verts, faces, model)
band.parent = root
assign(band, [BLUE])
add_mirror(band)

# ------------------------------------------------------ deck edge band
band = []
for a in [x for x in STATIONS if 0.9 <= x <= 33.9]:
    zd = deck_z(a)
    band.append((lerp_table(B_HULL, a) + lerp_table(LEDGE, a), y_of(a),
                 zd - 0.105, zd - 0.020))
strip('Paint_DeckEdgeBand', band, BLUE, offset=0.004, thickness=0.010)

# ------------------------------------------------------ boot top
BOOT = [(0.9, 0.64), (2.0, 0.40), (4.0, 0.22), (7.0, 0.10), (12.0, 0.03),
        (26.0, 0.03), (29.0, 0.10), (31.0, 0.30), (32.4, 0.62), (33.2, 0.86)]
verts, faces = [], []
for i in range(161):
    a = .9 + (33.2-.9)*i/160
    z0 = lerp_table(BOOT,a)
    for z in (z0,z0+.14):
        verts.append((hull_x_at_z(a,z)+.026,y_of(a),z))
    if i:
        p=2*(i-1)
        faces.append((p,p+1,p+3,p+2))
boot=new_mesh('Paint_BootTop',verts,faces,model)
boot.parent=root
assign(boot,[BLUE])
add_mirror(boot)

# ------------------------------------------------------ roof mushroom vents
for i in range(19):
    a = 3.2 + i * 1.45
    if a > 30.0:
        break
    hs = lerp_table(H_SCALE, a)
    x = lerp_table(B_SUP, a) * sup_frac_at(2.70)
    z = deck_z(a) + 2.70 * hs
    stem = tube('Roof_VentStem%02d' % i, model, (x, y_of(a), z),
                (x, y_of(a), z + 0.10), 0.042, 0.042, HULL, 8)
    stem.parent = root
    add_mirror(stem)
    cap = tube('Roof_VentCap%02d' % i, model, (x, y_of(a), z + 0.08),
               (x, y_of(a), z + 0.155), 0.082, 0.060, HULL, 8)
    cap.parent = root
    add_mirror(cap)

# ------------------------------------------------------ wheelhouse mast group
MAST_A = 9.95
mast_base = WH_TOP - 0.03
mast = tube('Mast', model, (0.0, y_of(MAST_A), mast_base),
            (0.0, y_of(MAST_A + 0.55), mast_base + 1.31), 0.055, 0.035, HULL, 8)
mast.parent = root
for i, sign in enumerate((1.0, -1.0)):
    whip = tube('Antenna%d' % i, model,
                (sign * 0.95, y_of(MAST_A + 0.15), mast_base - 0.02),
                (sign * 1.18, y_of(MAST_A + 0.65), mast_base + 1.37),
                0.022, 0.010, STEEL, 6)
    whip.parent = root

# Open-array radar scanner over the wheelhouse roof.
scanner_z = mast_base + 0.62
pedestal = tube('RadarPedestal', model, (0.0, y_of(8.55), mast_base - 0.04),
                (0.0, y_of(8.55), scanner_z - 0.10), 0.12, 0.10, HULL, 10)
pedestal.parent = root
array = box('RadarArray', model, (0.0, y_of(8.55), scanner_z), (1.95, 0.20, 0.13), HULL)
array.parent = root
add_bevel(array, width=0.03, segments=2, angle=40.0)
dome = tube('RadarDome', model, (0.0, y_of(9.35), mast_base - 0.02),
            (0.0, y_of(9.35), mast_base + 0.40), 0.22, 0.14, HULL, 10)
dome.parent = root

for i, sign in enumerate((1.0, -1.0)):
    lamp = tube('Searchlight%d' % i, model,
                (sign * 0.52, y_of(7.32), mast_base + 0.05),
                (sign * 0.52, y_of(7.10), mast_base + 0.08), 0.075, 0.088, HULL, 10)
    lamp.parent = root
    lens = tube('SearchlightLens%d' % i, model,
                (sign * 0.52, y_of(7.11), mast_base + 0.08),
                (sign * 0.52, y_of(7.07), mast_base + 0.08), 0.086, 0.086, LIGHTS, 10)
    lens.parent = root

# Vertical ladder on the after face of the wheelhouse.
for i in range(2):
    rail = tube('LadderRail%d' % i, model,
                (0.20 - 0.40 * i, y_of(10.62), WH_BASE - 0.30),
                (0.20 - 0.40 * i, y_of(10.62), WH_TOP + 0.10), 0.022, 0.022, STEEL, 6)
    rail.parent = root
for i in range(5):
    z = WH_BASE - 0.20 + i * 0.30
    rung = tube('LadderRung%d' % i, model, (-0.20, y_of(10.62), z),
                (0.20, y_of(10.62), z), 0.016, 0.016, STEEL, 6)
    rung.parent = root


# ------------------------------------------------------ guard rails
def railing(name, path, height=0.92, posts=None, mat=BLUE):
    """path: [(x, y, z_deck)] along one side; mirrored afterwards."""
    for level, frac in ((0, 1.0), (1, 0.62), (2, 0.30)):
        verts, faces = [], []
        for (x, y, z) in path:
            verts.append((x, y, z + height * frac))
        edges = [(i, i + 1) for i in range(len(path) - 1)]
        mesh = bpy.data.meshes.new('%s_rail%d' % (name, level))
        mesh.from_pydata(verts, edges, [])
        mesh.update()
        ob = bpy.data.objects.new('%s_rail%d' % (name, level), mesh)
        model.objects.link(ob)
        ob.parent = root
        skin = ob.modifiers.new('Rail tube', 'SKIN')
        for v in ob.data.skin_vertices[0].data:
            v.radius = (0.022, 0.022)
        assign(ob, [mat])
        add_mirror(ob)
    step = max(1, len(path) // max(1, (posts or len(path))))
    for i in range(0, len(path), step):
        x, y, z = path[i]
        post = tube('%s_post%02d' % (name, i), model, (x, y, z),
                    (x, y, z + height), 0.026, 0.022, mat, 6)
        post.parent = root
        add_mirror(post)


bow_path = []
for a in [0.95, 1.25, 1.65, 2.15, 2.75, 3.45, 4.25, 5.15, 6.05, 6.85]:
    x = lerp_table(B_HULL, a) + lerp_table(LEDGE, a) - 0.09
    bow_path.append((x, y_of(a), deck_z(a)))
# Photographs show an unobstructed bow saloon, without a tall foredeck rail.

aft_path = []
for a in [30.4, 31.0, 31.7, 32.4, 33.0, 33.5]:
    x = lerp_table(B_HULL, a) + lerp_table(LEDGE, a) - 0.10
    aft_path.append((x, y_of(a), deck_z(a)))
railing('Railing_Aft', aft_path, height=0.95, posts=6)

# Handrail along the saloon side, on short brackets.
hand = []
for a in [8.4 + 1.6 * i for i in range(14)]:
    if a > 30.0:
        break
    hand.append((lerp_table(B_SUP, a) + 0.11, y_of(a), deck_z(a) + 0.13))
verts = [(x, y, z) for (x, y, z) in hand]
mesh = bpy.data.meshes.new('Handrail_Side')
mesh.from_pydata(verts, [(i, i + 1) for i in range(len(verts) - 1)], [])
mesh.update()
hr = bpy.data.objects.new('Handrail_Side', mesh)
model.objects.link(hr)
hr.parent = root
skin = hr.modifiers.new('Rail tube', 'SKIN')
for v in hr.data.skin_vertices[0].data:
    v.radius = (0.026, 0.026)
assign(hr, [STEEL])
add_mirror(hr)


# ------------------------------------------------------ life rafts
for i, a in enumerate((9.30, 10.75, 28.0, 29.45)):
    x = lerp_table(B_SUP, a) + 0.50
    z = deck_z(a) + 0.82
    can = tube('LifeRaft%d' % i, model, (x, y_of(a - 0.62), z),
               (x, y_of(a + 0.62), z), 0.30, 0.30, HULL, 12)
    can.parent = root
    add_mirror(can)
    cradle = box('LifeRaftCradle%d' % i, model, (x, y_of(a), z - 0.31),
                 (0.66, 1.10, 0.09), STEEL)
    cradle.parent = root
    add_mirror(cradle)


# Canister retaining straps and machinery access door.
for i, a in enumerate((9.30, 10.75, 28.0, 29.45)):
    x = lerp_table(B_SUP, a) + 0.50
    z = deck_z(a) + 0.82
    for j, shift in enumerate((-.40, .40)):
        strap = tube('RaftStrap%d_%d' % (i, j), model,
                     (x, y_of(a + shift - .025), z),
                     (x, y_of(a + shift + .025), z), .306, .306, STEEL, 12)
        strap.parent = root
        add_mirror(strap)
hatch_a = 22.90
hatch_x = lerp_table(B_SUP, hatch_a) + .012
hatch = box('EngineHatch', model, (hatch_x, y_of(hatch_a), deck_z(hatch_a)+.80),
            (.035, .83, 1.28), HULL)
hatch.parent = root
add_bevel(hatch, width=.05, segments=3)
add_mirror(hatch)
for i in range(2):
    handle = tube('EngineHatchHandle%d' % i, model,
                  (hatch_x+.06,y_of(hatch_a-.28),deck_z(hatch_a)+.53+i*.54),
                  (hatch_x+.06,y_of(hatch_a-.12),deck_z(hatch_a)+.53+i*.54),
                  .018,.018,STEEL,6)
    handle.parent=root
    add_mirror(handle)

# ------------------------------------------------------ boarding fender
fender = tube('Fender_Boarding', model, (lerp_table(B_HULL, 8.3) + 0.30, y_of(8.3), 0.34),
              (lerp_table(B_HULL, 8.3) + 0.30, y_of(8.3), -0.18), 0.17, 0.17, RUBBER, 10)
fender.parent = root
add_mirror(fender)


# ------------------------------------------------------ deck fittings
for i, (a, x) in enumerate(((1.6, 0.52), (1.6, -0.52), (33.2, 0.42), (33.2, -0.42))):
    bol = tube('Bollard%d' % i, model, (x, y_of(a), deck_z(a)),
               (x, y_of(a), deck_z(a) + 0.30), 0.075, 0.065, STEEL, 8)
    bol.parent = root
    cap = tube('BollardCap%d' % i, model, (x, y_of(a), deck_z(a) + 0.28),
               (x, y_of(a), deck_z(a) + 0.34), 0.10, 0.085, STEEL, 8)
    cap.parent = root

anchor = box('AnchorHousing', model, (0.0, y_of(1.05), deck_z(1.05) + 0.10),
             (0.46, 0.70, 0.22), HULL)
anchor.parent = root


# ------------------------------------------------------ navigation lights
# Fixtures only. The functional lights stay with the Unity light rig.
nav = [('NavLight_Port', -1.0, 7.0, LIGHTS), ('NavLight_Stbd', 1.0, 7.0, LIGHTS)]
for (name, sign, a, mat) in nav:
    x = lerp_table(B_SUP, a) * sup_frac_at(2.10) + 0.05
    z = deck_z(a) + 2.10 * lerp_table(H_SCALE, a)
    lamp = box(name, model, (sign * x, y_of(a), z), (0.16, 0.22, 0.30), mat)
    lamp.parent = root
stern_lamp = box('NavLight_Stern', model, (0.0, y_of(33.6), deck_z(33.6) + 0.36),
                 (0.18, 0.18, 0.26), LIGHTS)
stern_lamp.parent = root
# One masthead light is enough for this length, but the rig carries a pair:
# the after light on the mast, the forward one on a staff on the bow saloon roof.
aft_masthead = box('NavLight_MastheadAft', model,
                   (0.0, y_of(MAST_A + 0.38), mast_base + 0.74), (0.18, 0.18, 0.26), LIGHTS)
aft_masthead.parent = root
fwd_z = roof_crown_z(4.5)
staff = tube('NavLight_MastheadStaff', model, (0.0, y_of(4.5), fwd_z),
             (0.0, y_of(4.5), fwd_z + 0.32), 0.030, 0.026, HULL, 6)
staff.parent = root
fwd_masthead = box('NavLight_MastheadFwd', model, (0.0, y_of(4.5), fwd_z + 0.42),
                   (0.16, 0.16, 0.24), LIGHTS)
fwd_masthead.parent = root
print('NAVLIGHTS|forward masthead z=%.3f, aft masthead z=%.3f'
      % (fwd_z + 0.42, mast_base + 0.74))


# ------------------------------------------------------ export landmarks
LANDMARKS = {
    'Waterline': (0.0, 0.0, 0.0),
    'Light_Port': (-lerp_table(B_SUP, 7.0) * sup_frac_at(2.10) - 0.05, y_of(7.0), deck_z(7.0) + 2.10 * lerp_table(H_SCALE, 7.0)),
    'Light_MastheadFwd': (0.0, y_of(4.5), fwd_z + 0.42),
    'Light_MastheadAft': (0.0, y_of(MAST_A + 0.38), mast_base + 0.74),
    'Light_Stern': (0.0, y_of(33.6), deck_z(33.6) + 0.36),
    'BowReference': (0.0, Y_STEM, 0.0),
    'SternReference': (0.0, -Y_STEM, 0.0),
    'StarboardReference': (3.0, 0.0, 0.0),
    'UpReference': (0.0, 0.0, 1.0),
    'CameraNavigator': (-0.38, y_of(8.10), WH_TOP - 0.62),
    'PropPoint_S': (SHAFT_X, y_of(PROP_A), PROP_Z),
    'PropPoint_P': (-SHAFT_X, y_of(PROP_A), PROP_Z),
    'FoilPoint_Bow': (0.0, y_of(8.9), -2.14),
    'FoilPoint_Stern': (0.0, y_of(29.9), -1.80),
}
for name, position in LANDMARKS.items():
    ob = bpy.data.objects.new(name, None)
    ob.empty_display_size = 0.5
    model.objects.link(ob)
    ob.parent = root
    ob.location = position

print('PASS5|paint bands, fittings, mast, rails and landmarks built')
