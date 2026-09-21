"""Meteor project 342U, pass 1: studio scene, materials, hull shell and side deck.

Run inside Blender. Metres, +Y forward, +X starboard, +Z up.
Loaded waterline Z = 0, baseline (keel) Z = -1.05, origin midship.
Hull lines come from the calibrated Ris. 139 general arrangement and live in
common.py; principal dimensions come from the SPK Fleet Meteor particulars.
"""
import bpy, bmesh, math

exec(compile(open('G:/Projects/ShipSim159/Art/Source/Meteor342U/common.py',
                  encoding='utf-8').read(), 'common.py', 'exec'))


def material(name, rgba, metallic, roughness):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = rgba
    bsdf.inputs['Metallic'].default_value = metallic
    bsdf.inputs['Roughness'].default_value = roughness
    if 'Alpha' in bsdf.inputs:
        bsdf.inputs['Alpha'].default_value = rgba[3]
    mat.diffuse_color = rgba
    return mat


# ---------------------------------------------------------------- scene

if SCENE in bpy.data.scenes:
    bpy.data.scenes.remove(bpy.data.scenes[SCENE])
scene = bpy.data.scenes.new(SCENE)
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.unit_settings.length_unit = 'METERS'

for name in ('Meteor342U_Model', 'GameReady_LODs', 'Meteor342U_Studio_Rig'):
    if name in bpy.data.collections:
        col = bpy.data.collections[name]
        for ob in list(col.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.collections.remove(col)
model = bpy.data.collections.new('Meteor342U_Model')
scene.collection.children.link(model)

root = bpy.data.objects.new('Meteor342U_ROOT', None)
root.empty_display_size = 2.0
model.objects.link(root)


# ---------------------------------------------------------------- materials
# Painted duralumin topsides, unpainted alloy foil system, tinted saloon glass.

MATS = {
    'Meteor_HullPaint':      material('Meteor_HullPaint',      (0.855, 0.867, 0.862, 1.0), 0.15, 0.36),
    'Meteor_SecondaryPaint': material('Meteor_SecondaryPaint', (0.055, 0.243, 0.494, 1.0), 0.12, 0.34),
    'Meteor_Glass':          material('Meteor_Glass',          (0.130, 0.200, 0.235, 0.60), 0.05, 0.08),
    'Meteor_Underbody':      material('Meteor_Underbody',      (0.105, 0.125, 0.120, 1.0), 0.20, 0.66),
    'Meteor_Rubber':         material('Meteor_Rubber',         (0.055, 0.058, 0.062, 1.0), 0.00, 0.80),
    'Meteor_Steel':          material('Meteor_Steel',          (0.640, 0.660, 0.680, 1.0), 0.85, 0.30),
    'Meteor_FoilSteel':      material('Meteor_FoilSteel',      (0.520, 0.545, 0.560, 1.0), 0.55, 0.38),
    'Meteor_InteriorDark':   material('Meteor_InteriorDark',   (0.040, 0.045, 0.052, 1.0), 0.00, 0.85),
    'Meteor_Lights':         material('Meteor_Lights',         (0.900, 0.870, 0.720, 1.0), 0.05, 0.22),
    'Meteor_Deck':           material('Meteor_Deck',           (0.300, 0.318, 0.330, 1.0), 0.10, 0.62),
    'Meteor_InteriorSeats':  material('Meteor_InteriorSeats',  (0.150, 0.175, 0.205, 1.0), 0.00, 0.78),
}
HULL = MATS['Meteor_HullPaint']


# ---------------------------------------------------------------- hull shell

verts, faces = [], []
ring = len(hull_section(0.0))
for a in STATIONS:
    y = y_of(a)
    for (x, z) in hull_section(a):
        verts.append((x, y, z))
for s in range(len(STATIONS) - 1):
    for i in range(ring - 1):
        p, q = s * ring + i, (s + 1) * ring + i
        faces.append((p, p + 1, q + 1, q))
hull = new_mesh('Hull', verts, faces, model)
hull.parent = root
recalc(hull)
assign(hull, [HULL, MATS['Meteor_Underbody']])
for poly in hull.data.polygons:
    if max(hull.data.vertices[i].co.z for i in poly.vertices) < 0.10:
        poly.material_index = 1
add_mirror(hull)

# Projecting side deck (obnosnoy bort) and its fascia, outboard of the hull side.
verts, faces = [], []
for a in STATIONS:
    y = y_of(a)
    bh = lerp_table(B_HULL, a)
    zd = lerp_table(Z_DECK, a)
    led = lerp_table(LEDGE, a)
    verts.extend([(bh, y, zd), (bh + led, y, zd), (bh + led, y, zd - 0.12),
                  (bh * 0.998, y, zd - 0.14)])
for s in range(len(STATIONS) - 1):
    for i in range(3):
        p, q = s * 4 + i, (s + 1) * 4 + i
        faces.append((p, p + 1, q + 1, q))
ledge = new_mesh('Hull_SideDeck', verts, faces, model)
ledge.parent = root
recalc(ledge)
assign(ledge, [HULL])
add_mirror(ledge)

print('PASS1|hull built, stations %d, ring %d' % (len(STATIONS), ring))
