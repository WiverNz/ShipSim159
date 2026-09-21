"""Meteor project 342U, pass 3: the shallow-immersion hydrofoil systems,
shafting, five-bladed propellers and rudders.

Bow assembly: a straight main foil on two broad swept struts, two outer struts
near the hull sides, and strongly swept-back outer stabiliser arms reaching the
9.5 m overall breadth. Stern assembly: a straight foil on two S-curved outer
brackets plus two inner struts on the shaft line, with downturned outer shoes.
Geometry follows Ris. 140 (foil assembly drawings and the bow/stern elevations)
and photographs of Meteor-191 as 342U; propeller and rudder positions match the
existing vessel JSON so the physics stays valid.
"""
import bpy, bmesh, math
from mathutils import Vector

exec(compile(open('G:/Projects/ShipSim159/Art/Source/Meteor342U/common.py',
                  encoding='utf-8').read(), 'common.py', 'exec'))

model = bpy.data.collections['Meteor342U_Model']
root = bpy.data.objects['Meteor342U_ROOT']
FOIL = bpy.data.materials['Meteor_FoilSteel']
STEEL = bpy.data.materials['Meteor_Steel']
HULL = bpy.data.materials['Meteor_HullPaint']

NAMES = ('FrontFoil', 'FrontFoil_Struts', 'FrontFoil_OuterStruts', 'FrontFoil_Fins',
         'RearFoil', 'RearFoil_Struts', 'RearFoil_InnerStruts', 'RudderArm',
         'PropShaft_S', 'PropShaft_P', 'Propeller_S', 'Propeller_P',
         'Rudder_S', 'Rudder_P', 'ShaftBossing')
for name in NAMES:
    if name in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)


def loft(name, sections, closed_ring=True, smooth=True, cap_ends=True):
    """sections: list of lists of (x, y, z); all the same length."""
    verts, faces = [], []
    ring = len(sections[0])
    for sec in sections:
        verts.extend(sec)
    for s in range(len(sections) - 1):
        for i in range(ring if closed_ring else ring - 1):
            j = (i + 1) % ring
            p, q = s * ring + i, (s + 1) * ring + i
            faces.append((p, s * ring + j, (s + 1) * ring + j, q))
    if cap_ends and closed_ring:
        for s, flip in ((0, True), (len(sections) - 1, False)):
            base = s * ring
            fan = [base + i for i in range(ring)]
            faces.append(tuple(reversed(fan)) if flip else tuple(fan))
    ob = new_mesh(name, verts, faces, model, smooth=smooth)
    ob.parent = root
    recalc(ob)
    return ob


def wing_section(x, y_le, z, chord, thick, twist=0.0, segments=12):
    """Closed aerofoil ring in the XY plane at half-breadth x, nose at y_le."""
    ring = foil_ring(chord, thick, segments)
    out = []
    for (c, t) in ring:
        yy = y_le - c * math.cos(twist) + t * math.sin(twist)
        zz = z - c * math.sin(twist) - t * math.cos(twist)
        out.append((x, yy, zz))
    return out


def strut_section(z, y_le, half_x, chord, thick, segments=10):
    """Closed aerofoil ring in the YZ plane, a vertical strut at half_x."""
    ring = foil_ring(chord, thick, segments)
    return [(half_x + t, y_le - c, z) for (c, t) in ring]


# --------------------------------------------------------------- bow foil
# Main span, then the swept-back outer stabiliser arms.

BF_LE = 8.55                 # leading edge, metres aft of the stem
BF_Z = -2.14                 # chord line depth
BF_HALF = 3.55               # half span of the straight section
BF_TIP = 4.75                # extreme half breadth, giving 9.50 m overall
BF_TIP_LE = 11.85            # leading edge at the tip: strongly swept back
BF_CHORD = 1.05

sections = []
for (x, sweep) in [(0.0, 0.0), (1.2, 0.0), (2.4, 0.0), (BF_HALF, 0.0),
                   (3.85, 0.26), (4.20, 0.52), (4.52, 0.78), (BF_TIP, 1.0)]:
    le = BF_LE + (BF_TIP_LE - BF_LE) * sweep
    chord = BF_CHORD - 0.30 * sweep
    z = BF_Z + 0.16 * sweep      # the outer arms rise towards the surface
    sections.append(wing_section(x, y_of(le), z, chord, 0.115))
front_foil = loft('FrontFoil', sections, cap_ends=True)
assign(front_foil, [FOIL])
add_mirror(front_foil)

# Tip stabiliser plates.
verts, faces = [], []
for i, (dz, scale) in enumerate([(0.0, 1.0), (-0.37, 0.55)]):
    le = BF_TIP_LE
    for (c, t) in foil_ring(0.75 * scale, 0.10, 8):
        verts.append((BF_TIP + 0.02, y_of(le) - c, BF_Z + 0.16 + dz))
ring = len(verts) // 2
for i in range(ring):
    j = (i + 1) % ring
    faces.append((i, j, ring + j, ring + i))
fins = new_mesh('FrontFoil_Fins', verts, faces, model)
fins.parent = root
recalc(fins)
assign(fins, [FOIL])
add_mirror(fins)

# Two broad main struts: wide where they fair into the hull, tapering to the
# foil. Their swept leading edge is the most recognisable underwater detail.
BF_STRUT_X = 1.72
sections = []
for (t, chord, le, thick, x) in [
        (0.00, 2.85, 7.95, 0.155, BF_STRUT_X + 0.10),   # hull bottom, faired
        (0.22, 2.35, 8.10, 0.190, BF_STRUT_X + 0.03),
        (0.55, 1.70, 8.30, 0.230, BF_STRUT_X),
        (0.85, 1.28, 8.46, 0.245, BF_STRUT_X),
        (1.00, 1.15, 8.52, 0.250, BF_STRUT_X)]:
    z = lerp_table(Z_KEEL, 8.4) - t * (lerp_table(Z_KEEL, 8.4) - BF_Z)
    sections.append(strut_section(z, y_of(le), x, chord, thick))
struts = loft('FrontFoil_Struts', sections)
assign(struts, [HULL])
add_mirror(struts)

# Outer struts, raked outboard from the hull side down to the foil.
sections = []
for (t, chord, le, x, z) in [
        (0.0, 1.55, 8.30, 2.62, -0.86), (0.4, 1.35, 8.42, 2.90, -1.40),
        (0.75, 1.20, 8.50, 3.10, -1.82), (1.0, 1.10, 8.55, 3.22, -2.12)]:
    sections.append(strut_section(z, y_of(le), x, chord, 0.16))
outer = loft('FrontFoil_OuterStruts', sections)
assign(outer, [HULL])
add_mirror(outer)


# --------------------------------------------------------------- stern foil

RF_LE = 29.35
RF_Z = -1.80
RF_HALF = 3.35
RF_TIP = 4.25
RF_CHORD = 1.15

sections = []
for (x, t) in [(0.0, 0.0), (1.6, 0.0), (2.6, 0.0), (RF_HALF, 0.0),
               (3.72, 0.4), (4.02, 0.72), (RF_TIP, 1.0)]:
    z = RF_Z - 0.30 * t                  # the outer shoes turn down
    le = RF_LE + 0.35 * t
    sections.append(wing_section(x, y_of(le), z, RF_CHORD - 0.25 * t, 0.12))
rear_foil = loft('RearFoil', sections, cap_ends=True)
assign(rear_foil, [FOIL])
add_mirror(rear_foil)

# S-curved outer brackets up to the hull side.
sections = []
for (t, chord, le, x, z) in [
        (0.0, 1.80, 28.95, 2.72, -0.52), (0.3, 1.55, 29.10, 3.02, -0.95),
        (0.6, 1.35, 29.22, 3.26, -1.38), (1.0, 1.20, 29.32, 3.35, -1.78)]:
    sections.append(strut_section(z, y_of(le), x, chord, 0.17))
rear_struts = loft('RearFoil_Struts', sections)
assign(rear_struts, [HULL])
add_mirror(rear_struts)

# Inner struts on the shaft line, carrying the shaft bearing.
sections = []
for (t, chord, le, z) in [(0.0, 2.40, 28.85, lerp_table(Z_KEEL, 29.6)),
                          (0.35, 2.05, 29.00, -1.15), (0.7, 1.65, 29.20, -1.52),
                          (1.0, 1.45, 29.30, -1.78)]:
    sections.append(strut_section(z, y_of(le), SHAFT_X, chord, 0.20))
rear_inner = loft('RearFoil_InnerStruts', sections)
assign(rear_inner, [HULL])
add_mirror(rear_inner)


# --------------------------------------------------------------- shafting

SHAFT_EXIT = 26.9             # SHAFT_X, PROP_A and PROP_Z come from common.py
exit_z = lerp_table(Z_KEEL, SHAFT_EXIT) + 0.06
for side, sign in (('S', 1.0), ('P', -1.0)):
    p0 = (sign * SHAFT_X, y_of(SHAFT_EXIT - 0.9), exit_z + 0.16)
    p1 = (sign * SHAFT_X, y_of(PROP_A - 0.30), PROP_Z + 0.03)
    shaft = tube('PropShaft_' + side, model, p0, p1, 0.115, 0.075, STEEL, 12)
    shaft.parent = root
    boss = tube('ShaftBossing_' + side, model,
                (sign * SHAFT_X, y_of(SHAFT_EXIT - 1.5), exit_z + 0.26),
                (sign * SHAFT_X, y_of(SHAFT_EXIT + 0.7), exit_z + 0.02),
                0.30, 0.20, HULL, 12)
    boss.parent = root

# Five-bladed propellers, 0.71 m diameter as published.
PROP_R = 0.355
for side, sign in (('S', 1.0), ('P', -1.0)):
    verts, faces = [], []
    hub_y = y_of(PROP_A)
    for b in range(5):
        ang = 2.0 * math.pi * b / 5.0
        base = len(verts)
        for (r, twist, chord) in [(0.085, 0.62, 0.16), (0.16, 0.50, 0.24),
                                  (0.24, 0.38, 0.27), (0.31, 0.27, 0.23),
                                  (PROP_R, 0.20, 0.10)]:
            cz = math.cos(ang) * r
            cx = math.sin(ang) * r * sign
            for s in (-1.0, 1.0):
                verts.append((sign * SHAFT_X + cx, hub_y + s * chord * 0.5 * math.cos(twist)
                              - s * 0.0, PROP_Z + cz + s * chord * 0.5 * math.sin(twist)))
        for i in range(4):
            p = base + i * 2
            faces.append((p, p + 1, p + 3, p + 2))
    prop = new_mesh('Propeller_' + side, verts, faces, model)
    prop.parent = root
    sol = prop.modifiers.new('Blade thickness', 'SOLIDIFY')
    sol.thickness = 0.022
    sol.offset = 0.0
    recalc(prop)
    assign(prop, [STEEL])
    hub = tube('PropHub_' + side, model,
               (sign * SHAFT_X, hub_y + 0.16, PROP_Z), (sign * SHAFT_X, hub_y - 0.20, PROP_Z),
               0.105, 0.055, STEEL, 12)
    hub.parent = root


# --------------------------------------------------------------- rudders
# Y = -15.2 and the published 0.9 m span, matching the vessel JSON.

RUD_A = 32.5
for side, sign in (('S', 1.0), ('P', -1.0)):
    sections = []
    for (z, chord, le) in [(-1.06, 0.70, 32.28), (-1.50, 0.70, 32.30),
                           (-1.88, 0.66, 32.34), (-1.96, 0.46, 32.44)]:
        sections.append(strut_section(z, y_of(le), sign * SHAFT_X, chord, 0.13))
    rud = loft('Rudder_' + side, sections)
    assign(rud, [STEEL])
    arm = tube('RudderArm_' + side, model,
               (sign * SHAFT_X, y_of(30.6), -1.72),
               (sign * SHAFT_X, y_of(RUD_A - 0.4), -1.24), 0.10, 0.09, FOIL, 10)
    arm.parent = root
    stock = tube('RudderStock_' + side, model,
                 (sign * SHAFT_X, y_of(32.34), -1.06),
                 (sign * SHAFT_X, y_of(32.34), lerp_table(Z_KEEL, 32.34) + 0.02),
                 0.075, 0.075, STEEL, 10)
    stock.parent = root

print('PASS3|foil systems, shafting, propellers and rudders built')
