"""Run through registered Blender MCP; preserves other open scenes."""
from pathlib import Path
import bpy
import bmesh
import json
from mathutils import Matrix, Vector

ROOT = Path('G:/Projects/ShipSim159')
SOURCE = ROOT / 'Art/Source/VolgoDon507B'
CANDIDATE = ROOT / 'Assets/ShipSimulator/Models/VolgoDon507B_Improved'
LEGACY = ROOT / 'Assets/ShipSimulator/Models/VolgoDon507'

if bpy.data.scenes.get('VolgoDon507B_Source'):
    raise RuntimeError('Source scene exists; reopen the saved source rather than rebuilding over edits.')
scene = bpy.data.scenes.new('VolgoDon507B_Source')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
bpy.ops.import_scene.fbx(filepath=str(LEGACY / 'volgo_don.fbx'), use_anim=False)
bpy.context.view_layer.update()
imported = list(scene.objects)
meshes = [o for o in imported if o.type == 'MESH' and not o.name.startswith('proxy')]
# Mirrored radars, propellers and nozzles share imported mesh datablocks.
# Make each editable before changing UVs or material slots once per object.
for o in meshes:
    o.data = o.data.copy()
world_matrices = {o: o.matrix_world.copy() for o in meshes}
points = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
low = Vector([min(v[i] for v in points) for i in range(3)])
high = Vector([max(v[i] for v in points) for i in range(3)])
hull = next(o for o in meshes if o.name.split('.')[0] == 'ship_type_507_hull')
keel = min((hull.matrix_world @ v.co).z for v in hull.data.vertices)
sx = 16.7 / (high.x - low.x)
sy = 138.3 / (high.y - low.y)
sz = sy
normalization = Matrix.Diagonal((sx, sy, sz, 1))
normalization.translation = Vector((-(low.x + high.x) * sx / 2,
                                    -(low.y + high.y) * sy / 2,
                                    -3.53 - keel * sz))
groups = {}
for name in ['Hull', 'Decks', 'HoldsAndCovers', 'Accommodation', 'WheelhouseInterior',
             'Propulsion', 'Steering', 'Lifesaving', 'DeckEquipment', 'LightsAndRadar',
             'AnchoringAndMooring', 'ReferenceDatums', 'Studio']:
    col = bpy.data.collections.new('VD507B_' + name)
    scene.collection.children.link(col)
    groups[name] = col

def group_for(name):
    if 'turbine' in name: return 'Propulsion'
    if 'rudder' in name: return 'Steering'
    if 'boats' in name: return 'Lifesaving'
    if 'hold' in name: return 'HoldsAndCovers'
    if 'interior' in name: return 'WheelhouseInterior'
    if 'rubka' in name: return 'Accommodation'
    if 'lights' in name or 'radar' in name: return 'LightsAndRadar'
    if 'hull' in name: return 'Hull'
    if 'deck' in name: return 'Decks'
    return 'DeckEquipment'

report = {'normalization': {'x': sx, 'y': sy, 'z': sz, 'keel_before': keel},
          'uv_correction': 'V = 1 - V, original retained in LegacyUV', 'objects': []}
material_map = {}
(CANDIDATE / 'Textures').mkdir(parents=True, exist_ok=True)

def material_for(original):
    key = original.name.split('.')[0]
    if key in material_map: return material_map[key]
    mat = bpy.data.materials.new('VD507B_' + key.removeprefix('ship_type_507_'))
    mat.use_nodes = True
    bs = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bs.inputs['Roughness'].default_value = .65 if 'deck' in key or 'hold' in key else .48
    bs.inputs['Metallic'].default_value = 0
    bs.inputs['Base Color'].default_value = (.24, .26, .28, 1)
    src = LEGACY / (key + '_ao.dds')
    if 'lights' in key: src = LEGACY / (key + '.hdr')
    if src.exists():
        im = bpy.data.images.load(str(src), check_existing=False)
        assert len(im.pixels) > 0, 'Texture failed to decode: ' + str(src)
        pixel = im.pixels[0]
        im.filepath_raw = str(CANDIDATE / 'Textures' / (mat.name + '_BaseColor.png'))
        im.file_format = 'PNG'
        im.save()
        tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = im
        mat.node_tree.links.new(tex.outputs['Color'], bs.inputs['Base Color'])
        if 'lights' in key:
            mat.node_tree.links.new(tex.outputs['Color'], bs.inputs['Emission Color'])
            bs.inputs['Emission Strength'].default_value = .5
    else:
        bs.inputs['Metallic'].default_value = .75
        bs.inputs['Roughness'].default_value = .32
    material_map[key] = mat
    return mat

for o in meshes:
    original_name = o.name
    world = world_matrices[o]
    o.parent = None
    o.matrix_world = normalization @ world
    for col in list(o.users_collection): col.objects.unlink(o)
    groups[group_for(original_name)].objects.link(o)
    o.name = 'VD507B_' + original_name.removeprefix('ship_type_507_')
    m = o.data
    uv = m.uv_layers.active
    original_uv = [tuple(x.uv) for x in uv.data]
    legacy_uv = m.uv_layers.new(name='LegacyUV')
    for i, value in enumerate(original_uv):
        legacy_uv.data[i].uv = value
        uv.data[i].uv = (value[0], 1 - value[1])
    m.uv_layers.active_index = 0
    uv.active_render = True
    bm = bmesh.new()
    bm.from_mesh(m)
    bad = [f for f in bm.faces if f.calc_area() < 1e-10 or
           (len(f.verts) == 3 and
            (f.verts[1].co - f.verts[0].co).cross(f.verts[2].co - f.verts[0].co).length < 2e-10)]
    count = len(bad)
    if bad:
        bmesh.ops.delete(bm, geom=bad, context='FACES_ONLY')
        bm.to_mesh(m)
    bm.free()
    for i, mat in enumerate(list(m.materials)):
        if mat: m.materials[i] = material_for(mat)
    o['legacy_object'] = original_name
    report['objects'].append({'name': o.name, 'removed_degenerate_faces': count})

for o in imported:
    if o not in meshes: bpy.data.objects.remove(o, do_unlink=True)
for name, pos in [('Waterline', (0, 0, 0)), ('BowReference', (0, 69.15, 0)),
                  ('StarboardReference', (8.35, 0, 0)), ('UpReference', (0, 0, 1)),
                  ('KeelReference', (0, 0, -3.53))]:
    o = bpy.data.objects.new(name, None)
    groups['ReferenceDatums'].objects.link(o)
    o.location = pos
    o.empty_display_size = .5
scene['variant'] = 'Classic unshortened 507B, two covered holds, exact vessel unknown'
scene['datums'] = 'Metres; +Y bow, +X starboard, +Z up; loaded waterline Z=0; keel Z=-3.53'
scene['part2_limits'] = 'Merged equipment remains editable by mesh selection; mechanism separation is Part 4'
with open(SOURCE / 'part2-report.json', 'w') as f:
    json.dump(report, f, indent=2)
print('Source created:', len(meshes), 'meshes;', sum(o['removed_degenerate_faces'] for o in report['objects']), 'degenerates removed')
