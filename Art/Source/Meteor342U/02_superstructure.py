"""Meteor project 342U, pass 2: superstructure, weather deck, engine casing,
wheelhouse and the cantilevered aft roof visor.

Estimated cabin proportions follow the Ris. 139 illustration and
photographs of Meteor-191 as 342U.
"""
import bpy, bmesh, math

exec(compile(open('G:/Projects/ShipSim159/Art/Source/Meteor342U/common.py',
                  encoding='utf-8').read(), 'common.py', 'exec'))

model = bpy.data.collections['Meteor342U_Model']
root = bpy.data.objects['Meteor342U_ROOT']
HULL = bpy.data.materials['Meteor_HullPaint']
DECK = bpy.data.materials['Meteor_Deck']
DARK = bpy.data.materials['Meteor_InteriorDark']

for name in ('Superstructure', 'Roof_Casing', 'Wheelhouse', 'Roof_Visor',
             'Roof_VisorFin', 'Deck_Weather', 'Deck_Aft'):
    if name in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)

SUP_STATIONS = ([SUP_FWD, 1.95, 2.2, 2.5, 2.8, 3.2, 3.6, 4.1, 4.6, 5.2,
                 5.8, 6.4, 7.0, 7.75, 8.5, 9.25, 10.0, 11.0, 12.0, 13.0, 14.0,
                 15.0] + [16.0 + i for i in range(11)] +
                [27.0, 28.0, 29.0, 29.8, 30.5, SUP_AFT])


def roof_ring(a):
    return [sup_xyz(a, frac, h) for (frac, h) in SUP_SECTION[ROOF_RING:]]



verts, faces = [], []
ring = len(SUP_SECTION)
for a in SUP_STATIONS:
    for (frac, h) in SUP_SECTION:
        verts.append(sup_xyz(a, frac, h))
for s in range(len(SUP_STATIONS) - 1):
    for i in range(ring - 1):
        p, q = s * ring + i, (s + 1) * ring + i
        faces.append((p, p + 1, q + 1, q))
sup = new_mesh('Superstructure', verts, faces, model)
sup.parent = root
recalc(sup)
assign(sup, [HULL])
add_mirror(sup)


# ------------------------------------------------- weather deck

verts, faces = [], []
walk = [a for a in STATIONS if 1.0 <= a <= 34.2]
for a in walk:
    y = y_of(a)
    zd = lerp_table(Z_DECK, a)
    inner = lerp_table(B_SUP, a) - 0.02 if SUP_FWD <= a <= SUP_AFT else 0.0
    verts.append((max(inner, 0.0), y, zd))
    verts.append((lerp_table(B_HULL, a) + lerp_table(LEDGE, a), y, zd))
for s in range(len(walk) - 1):
    p, q = s * 2, (s + 1) * 2
    faces.append((p, p + 1, q + 1, q))
deck = new_mesh('Deck_Weather', verts, faces, model, smooth=False)
deck.parent = root
sol = deck.modifiers.new('Deck thickness', 'SOLIDIFY')
sol.thickness = 0.06
sol.offset = 1.0
recalc(deck)
assign(deck, [DECK])
add_mirror(deck)


# ------------------------------------------------- engine casing on the roof

CASING = [(18.6, 0.0), (19.3, 0.64), (20.4, 0.88), (26.4, 0.88), (27.6, 0.62),
          (28.3, 0.0)]
CAS_SECTION = [(1.00, 0.00), (0.99, 0.14), (0.93, 0.26), (0.78, 0.35),
               (0.50, 0.40), (0.00, 0.42)]
stations = [18.6 + 0.35 * i for i in range(int((28.3 - 18.6) / 0.35) + 1)] + [28.3]
verts, faces = [], []
ring = len(CAS_SECTION)
for a in stations:
    w = lerp_table(CASING, a)
    for (frac, h) in CAS_SECTION:
        verts.append((max(w * frac, 0.0), y_of(a), roof_crown_z(a) - 0.06 + h))
for s in range(len(stations) - 1):
    for i in range(ring - 1):
        p, q = s * ring + i, (s + 1) * ring + i
        faces.append((p, p + 1, q + 1, q))
casing = new_mesh('Roof_Casing', verts, faces, model)
casing.parent = root
recalc(casing)
assign(casing, [HULL])
add_mirror(casing)


# The glazed wheelhouse is built as closed panels in pass 4.

# ------------------------------------------------- cantilevered aft roof visor
# Continues the roof rings aft of the superstructure body, sweeping up to a
# point, with the swept side fin that the photographs and Ris. 139 both show.

VIS_PLAN = [(SUP_AFT, 1.000), (32.0, 0.86), (32.9, 0.66), (33.7, 0.40),
            (34.2, 0.18), (VISOR_TIP, 0.02)]
VIS_RISE = [(SUP_AFT, 0.0), (32.0, 0.20), (32.9, 0.38), (33.7, 0.54),
            (34.2, 0.64), (VISOR_TIP, 0.70)]
vis_stations = [SUP_AFT + 0.25 * i
                for i in range(int((VISOR_TIP - SUP_AFT) / 0.25) + 1)] + [VISOR_TIP]
base_ring = roof_ring(SUP_AFT)
verts, faces = [], []
ring = len(base_ring)
for a in vis_stations:
    scale = lerp_table(VIS_PLAN, a)
    rise = lerp_table(VIS_RISE, a)
    z0 = base_ring[0][2]
    for (x, _, z) in base_ring:
        verts.append((x * scale, y_of(a), z0 + (z - z0) * scale + rise))
for s in range(len(vis_stations) - 1):
    for i in range(ring - 1):
        p, q = s * ring + i, (s + 1) * ring + i
        faces.append((p, p + 1, q + 1, q))
visor = new_mesh('Roof_Visor', verts, faces, model)
visor.parent = root
sol = visor.modifiers.new('Visor thickness', 'SOLIDIFY')
sol.thickness = 0.10
sol.offset = 1.0
recalc(visor)
assign(visor, [HULL])
add_mirror(visor)

# Side fin under the visor edge.
FIN = [(29.4, 0.34), (30.2, 0.62), (31.2, 0.78), (32.1, 0.66), (32.9, 0.40)]
verts, faces = [], []
for (a, drop) in FIN:
    scale = lerp_table(VIS_PLAN, a) if a >= SUP_AFT else 1.0
    rise = lerp_table(VIS_RISE, a) if a >= SUP_AFT else 0.0
    edge = sup_xyz(min(a, SUP_AFT), SUP_SECTION[ROOF_RING + 1][0],
                   SUP_SECTION[ROOF_RING + 1][1])
    x = edge[0] * scale - 0.03
    z = edge[2] + rise
    verts.extend([(x, y_of(a), z), (x - 0.05, y_of(a), z - drop)])
for s in range(len(FIN) - 1):
    p, q = s * 2, (s + 1) * 2
    faces.append((p, p + 1, q + 1, q))
fin = new_mesh('Roof_VisorFin', verts, faces, model)
fin.parent = root
sol = fin.modifiers.new('Fin thickness', 'SOLIDIFY')
sol.thickness = 0.06
sol.offset = 0.0
recalc(fin)
assign(fin, [HULL])
add_mirror(fin)

print('PASS2|superstructure, deck, casing, wheelhouse and visor built')
