"""Finish the prepared scene, export a candidate and save an independent source."""
from pathlib import Path
import bpy
import json
import math
from mathutils import Vector

ROOT = Path('G:/Projects/ShipSim159')
SOURCE = ROOT / 'Art/Source/VolgoDon507B'
CANDIDATE = ROOT / 'Assets/ShipSimulator/Models/VolgoDon507B_Improved'
scene = bpy.data.scenes['VolgoDon507B_Source']
bpy.context.window.scene = scene
meshes = [o for o in scene.objects if o.type == 'MESH']
accommodation = next(o for o in meshes if o['legacy_object'].split('.')[0] == 'ship_type_507_rubka')
glass = bpy.data.materials.get('VD507B_Glass') or bpy.data.materials.new('VD507B_Glass')
glass.use_nodes = True
bs = next(n for n in glass.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
bs.inputs['Base Color'].default_value = (.055, .095, .11, 1)
bs.inputs['Roughness'].default_value = .14
bs.inputs['Metallic'].default_value = 0
bs.inputs['IOR'].default_value = 1.45
# Existing panes have paired triangles. Guards prevent an index-only reassignment
# from affecting walls if the source topology changes.
starts = [1815, 1825, 1835, 1845, 1855, 1867, 1877, 1887, 1897,
          1905, 1915, 1927, 1937, 1947, 1957, 1969, 1980, 1991,
          2001, 2011, 2021, 2031, 2041, 2051, 2061, 2071, 2083,
          2093, 2103, 2113]
if glass.name not in accommodation.data.materials:
    accommodation.data.materials.append(glass)
slot = list(accommodation.data.materials).index(glass)
for index in [i for start in starts for i in (start, start + 1)]:
    p = accommodation.data.polygons[index]
    c = accommodation.matrix_world @ p.center
    assert 8.85 < c.z < 9.6 and -50 < c.y < -42 and p.area > .12
    p.material_index = slot

# Material response remains simple and portable in this first pass.
for o in meshes:
    for mat in o.data.materials:
        if not mat: continue
        for node in mat.node_tree.nodes:
            if node.type == 'TEX_IMAGE' and node.image:
                node.image.pack()
                node.image.filepath = bpy.path.relpath(node.image.filepath, start=str(SOURCE))

studio = next(c for c in scene.collection.children if c.name == 'VD507B_Studio')
for obj in list(studio.objects):
    bpy.data.objects.remove(obj, do_unlink=True)
world = bpy.data.worlds.new('VD507B_StudioWorld')
world.use_nodes = True
next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND').inputs['Color'].default_value = (.3, .35, .4, 1)
scene.world = world
light_data = bpy.data.lights.new('VD507B_Sun', 'SUN')
light = bpy.data.objects.new('VD507B_Sun', light_data)
studio.objects.link(light)
light_data.energy = 2
camera_data = bpy.data.cameras.new('VD507B_Camera')
camera = bpy.data.objects.new('VD507B_Camera', camera_data)
studio.objects.link(camera)
camera_data.type = 'ORTHO'
camera_data.clip_end = 1000
scene.camera = camera
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
views = {'side': ((180, 0, 8), (0, 0, 3), 154),
         'front': ((0, 180, 8), (0, 0, 3), 35),
         'top': ((0, 0, 180), (0, 0, 0), 154),
         'perspective': ((110, 114, 80), (0, 0, 3), 154),
         'superstructure': ((30, -20, 17), (0, -50, 6), 40)}
for name, (position, target, scale) in views.items():
    camera.location = position
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    if name == 'top': camera.rotation_euler = (0, 0, math.pi / 2)
    camera_data.ortho_scale = scale
    light.rotation_euler = camera.rotation_euler.copy()
    light.rotation_euler.x += .25
    scene.render.filepath = str(SOURCE / 'Previews' / ('part2-' + name + '.png'))
    bpy.ops.render.render(write_still=True)

for o in scene.objects: o.select_set(o.type == 'MESH' or o.type == 'EMPTY')
bpy.ops.export_scene.fbx(filepath=str(CANDIDATE / 'VolgoDon507B.fbx'),
    use_selection=True, object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y',
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True,
    add_leaf_bones=False, bake_anim=False, use_custom_props=True, path_mode='RELATIVE')
materials = {}
for o in meshes:
    for mat in o.data.materials:
        bs = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        tex = next((n.image for n in mat.node_tree.nodes if n.type == 'TEX_IMAGE'), None)
        materials[mat.name] = {'name': mat.name, 'color': list(bs.inputs['Base Color'].default_value),
                              'metallic': bs.inputs['Metallic'].default_value,
                              'smoothness': 1 - bs.inputs['Roughness'].default_value,
                              'texture': Path(tex.filepath).name if tex else ''}
with open(CANDIDATE / 'materials.json', 'w') as f:
    json.dump({'materials': list(materials.values())}, f, indent=2)
report = json.load(open(SOURCE / 'part2-report.json'))
report['glass_pane_triangles'] = 60
report['triangles'] = sum(len(o.data.polygons) for o in meshes)
report['bounds'] = {'length': 138.3, 'beam': 16.7, 'hull_keel': -3.53}
with open(SOURCE / 'part2-report.json', 'w') as f: json.dump(report, f, indent=2)
for script in ['prepare_source.py', 'finish_source.py']:
    text = bpy.data.texts.load(str(SOURCE / script))
    text.use_fake_user = True
scene['part2_materials'] = 'Corrected atlas UV; opaque tinted bridge panes; PBR scalar responses'
# Write only the source scene and its dependencies, preserving other scenes in memory.
bpy.data.libraries.write(str(SOURCE / 'VolgoDon507B.blend'), {scene}, path_remap='RELATIVE', fake_user=True)
print('Saved independent source and candidate FBX;', report['triangles'], 'triangles')
