"""Run through registered Blender MCP. Rebuild derived LOD scenes and export the editable rig."""
import bpy
import hashlib
import json
from pathlib import Path

ROOT = Path('G:/Projects/ShipSim159')
SOURCE = ROOT / 'Art/Source/VolgoDon507B'
TARGET = ROOT / 'Assets/ShipSimulator/Models/VolgoDon507B_Improved'
scene = bpy.data.scenes['VolgoDon507B_Source']
stage = 10 if scene.get('part10_hull') else 8
bpy.context.window.scene = scene
scene.frame_set(1)
originals = [o for o in scene.objects if o.type in {'MESH', 'EMPTY'}]
protected = {'VD507B_hull', 'VD507B_deck', 'VD507B_hold', 'VD507B_rubka', 'VD507B_rubka_detali'}

def signature(objects):
    records = []
    for obj in sorted(objects, key=lambda o: o.name):
        if obj.type != 'MESH':
            continue
        obj.data.calc_loop_triangles()
        assert len(obj.data.uv_layers) == 2, obj.name
        assert all(t.area > 1e-10 for t in obj.data.loop_triangles), obj.name
        geometry = [(tuple(round(x, 6) for x in v.co)) for v in obj.data.vertices]
        geometry += [tuple(p.vertices) for p in obj.data.polygons]
        records.append(dict(name=obj.name, triangles=len(obj.data.loop_triangles),
                            materials=[m.name for m in obj.data.materials],
                            geometry_sha256=hashlib.sha256(repr(geometry).encode()).hexdigest()))
    return records

def export(target_scene, path, animated):
    bpy.context.window.scene = target_scene
    target_scene.frame_set(1)
    for obj in target_scene.objects:
        obj.select_set(obj.type in {'MESH', 'EMPTY'})
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True,
        object_types={'MESH', 'EMPTY'}, axis_forward='-Z', axis_up='Y', apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=False, add_leaf_bones=False,
        bake_anim=animated, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
        bake_anim_simplify_factor=0, use_custom_props=True, path_mode='RELATIVE')

report = {'levels': [dict(level=0, meshes=signature(originals))]}
for level, ratio in [(1, .55), (2, .22)]:
    name = f'VolgoDon507B_LOD{level}'
    old = bpy.data.scenes.get(name)
    if old:
        for obj in list(old.objects):
            mesh = obj.data if obj.type == 'MESH' else None
            bpy.data.objects.remove(obj, do_unlink=True)
            if mesh and mesh.users == 0:
                bpy.data.meshes.remove(mesh)
        bpy.data.scenes.remove(old)
    derived = bpy.data.scenes.new(name)
    derived.unit_settings.system = 'METRIC'
    mapping = {}
    for obj in originals:
        if level == 2 and (obj.name == 'VD507B_interior' or obj.name.startswith(('JackHeads_', 'JackRods_', 'Seal_'))):
            continue
        copy = obj.copy()
        copy.name = f'LOD{level}__{obj.name}'
        copy.animation_data_clear()
        if obj.type == 'MESH':
            copy.data = obj.data.copy()
            copy.data.name = copy.name
        derived.collection.objects.link(copy)
        mapping[obj] = copy
    for original, copy in mapping.items():
        copy.parent = mapping.get(original.parent)
        copy.matrix_parent_inverse = original.matrix_parent_inverse.copy()
    bpy.context.window.scene = derived
    for original, copy in mapping.items():
        if copy.type != 'MESH' or (original.name in protected and not (stage == 10 and original.name == 'VD507B_hull')):
            continue
        triangles = sum(len(p.vertices) - 2 for p in copy.data.polygons)
        if triangles < 300:
            continue
        for obj in derived.objects:
            obj.select_set(False)
        copy.select_set(True)
        bpy.context.view_layer.objects.active = copy
        modifier = copy.modifiers.new('Derived distance simplification', 'DECIMATE')
        modifier.ratio = max(ratio, .5) if original.name == 'VD507B_boats' else ratio
        if stage == 10 and original.name == 'VD507B_hull':
            modifier.ratio = .30 if level == 1 else .12
        if original.name == 'VD507B_detali':
            modifier.ratio = max(ratio, .30)
        if original.name == 'VD507B_pipes':
            modifier.ratio = max(ratio, .55)
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    meshes = signature(derived.objects)
    report['levels'].append(dict(level=level, meshes=meshes))
    export(derived, TARGET / f'VolgoDon507B_LOD{level}.fbx', False)

bpy.context.window.scene = scene
scene.frame_set(1)
assert signature(originals) == report['levels'][0]['meshes'], 'LOD generation changed LOD0'
export(scene, TARGET / 'VolgoDon507B.fbx', True)
for image in bpy.data.images:
    if image.source == 'FILE' and image.has_data and not image.packed_file:
        image.pack()
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / 'VolgoDon507B.blend'))
with open(SOURCE / f'part{stage}-source.json', 'w') as stream:
    json.dump(report, stream, indent=2)
print(f'PART{stage}_EXPORT', [(r['level'], sum(m['triangles'] for m in r['meshes']), len(r['meshes'])) for r in report['levels']])
