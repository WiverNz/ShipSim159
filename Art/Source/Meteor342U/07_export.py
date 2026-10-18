"""Meteor project 342U, pass 7: LOD generation, FBX export and geometry audit.

Evaluates every modifier, joins the model into one mesh per level, cleans and
triangulates it, gives it a planar metre UV channel for future texturing, and
writes the FBX that Unity imports plus a JSON measurement report. Source
objects stay editable; the export collection is hidden in the studio file.
"""
import bpy, bmesh, math, json, os

exec(compile(open('G:/Projects/ShipSim159/Art/Source/Meteor342U/common.py',
                  encoding='utf-8').read(), 'common.py', 'exec'))

exec(compile(open(BASE + '/Art/Source/Meteor342U/validate_model.py',
                  encoding='utf-8').read(), 'validate_model.py', 'exec'), {})

scene = bpy.data.scenes[SCENE]
bpy.context.window.scene = scene
model = bpy.data.collections['Meteor342U_Model']

MATERIALS = ['Meteor_HullPaint', 'Meteor_SecondaryPaint', 'Meteor_Glass',
             'Meteor_Rubber', 'Meteor_Steel', 'Meteor_FoilSteel',
             'Meteor_InteriorDark', 'Meteor_Lights', 'Meteor_Deck',
             'Meteor_InteriorSeats', 'Meteor_Underbody']
mats = [bpy.data.materials[n] for n in MATERIALS]

# Fittings that stop being readable at LOD2 distance.
LOD2_DROP = ('Railing_', 'Handrail', 'Roof_Vent', 'Antenna', 'Ladder', 'Bollard',
             'NavLight', 'Searchlight', 'InteriorProxy_Seats', 'LifeRaftCradle',
             'Paint_DeckEdgeBand', 'AnchorHousing', 'RoofIntakeCap')

export = bpy.data.collections.get('GameReady_LODs')
if export is None:
    export = bpy.data.collections.new('GameReady_LODs')
    scene.collection.children.link(export)
export.hide_viewport = False
export.hide_render = False
for ob in list(export.objects):
    bpy.data.objects.remove(ob, do_unlink=True)

source = [ob for ob in model.objects
          if ob.type == 'MESH' and not ob.hide_render and not ob.hide_viewport]
landmarks = [ob for ob in model.objects if ob.type == 'EMPTY'
             and ob.name != 'Meteor342U_ROOT']

report = {'vessel': 'Meteor project 342U',
          'units': 'metres',
          'blender_axes': '+Y forward, +X starboard, +Z up',
          'waterline_z': 0.0,
          'baseline_z': -1.05,
          'origin': 'midship on the loaded waterline',
          'source_objects': len(source),
          'lods': []}

for level, ratio in ((0, 1.0), (1, 0.45), (2, 0.16)):
    disabled = []
    if level == 2:
        for ob in source:
            for mod in ob.modifiers:
                if mod.type == 'BEVEL' and mod.show_viewport:
                    mod.show_viewport = False
                    disabled.append(mod)
        bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()

    verts, faces, indices, smooth = [], [], [], []
    for ob in source:
        if level == 2 and ob.name.startswith(LOD2_DROP):
            continue
        ev = ob.evaluated_get(deps)
        data = ev.to_mesh()
        offset = len(verts)
        clean = bmesh.new()
        clean.from_mesh(data)
        bmesh.ops.remove_doubles(clean, verts=list(clean.verts), dist=1e-6)
        bmesh.ops.dissolve_degenerate(clean, edges=list(clean.edges), dist=1e-7)
        clean.to_mesh(data)
        clean.free()
        verts.extend(tuple(ob.matrix_world @ v.co) for v in data.vertices)
        for p in data.polygons:
            faces.append(tuple(offset + i for i in p.vertices))
            slot = ob.material_slots[p.material_index].material if ob.material_slots else mats[0]
            indices.append(mats.index(slot) if slot in mats else 0)
            smooth.append(p.use_smooth)
        ev.to_mesh_clear()
    for mod in disabled:
        mod.show_viewport = True
    bpy.context.view_layer.update()

    name = 'Meteor342U_LOD%d' % level
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts, [], faces)
    data.update()
    for m in mats:
        data.materials.append(m)
    for p, i, sm in zip(data.polygons, indices, smooth):
        p.material_index = i
        p.use_smooth = sm
    ob = bpy.data.objects.new(name, data)
    export.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)

    bm = bmesh.new()
    bm.from_mesh(data)
    # Shared cabin seams must stay welded when the distance LODs collapse edges.
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=2e-5)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-7)
    # Preserve the authored outward winding of open glass and shell panels.
    bm.to_mesh(data)
    bm.free()

    protected = {mats.index(bpy.data.materials[name]): name
                 for name in ('Meteor_Glass', 'Meteor_SecondaryPaint')}
    before_area = {index: sum(p.area for p in data.polygons if p.material_index == index)
                   for index in protected}
    if ratio < 1.0:
        mod = ob.modifiers.new('Distance simplification', 'DECIMATE')
        # Collapse crosses glass/paint boundaries and turns windows into long shards.
        mod.decimate_type = 'DISSOLVE'
        mod.angle_limit = math.radians(.6 if level == 1 else 2.5)
        mod.delimit = {'MATERIAL'}
        mod.use_dissolve_boundaries = False
        bpy.ops.object.modifier_apply(modifier=mod.name)
    tri = ob.modifiers.new('Export triangles', 'TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=tri.name)

    # A planar metre UV channel for future material authoring. No reference
    # photograph is used as a texture anywhere in this model.
    data = ob.data
    area_drift = {}
    for index, material_name in protected.items():
        after_area = sum(p.area for p in data.polygons if p.material_index == index)
        drift = abs(after_area / before_area[index] - 1)
        assert drift < .02, name + ': damaged glass/paint boundary: ' + material_name
        area_drift[material_name] = drift
    uv = data.uv_layers.new(name='Planar_Metres')
    for p in data.polygons:
        axis = max(range(3), key=lambda i: abs(p.normal[i]))
        a, b = [i for i in range(3) if i != axis]
        for li in p.loop_indices:
            co = data.vertices[data.loops[li].vertex_index].co
            uv.data[li].uv = (co[a] / 8.0, co[b] / 8.0)

    bm = bmesh.new()
    bm.from_mesh(data)
    boundary = sum(1 for e in bm.edges if e.is_boundary)
    nonmanifold = sum(1 for e in bm.edges if not e.is_manifold)
    degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-12)
    bm.free()
    points = [ob.matrix_world @ v.co for v in data.vertices]
    lo = [min(p[i] for p in points) for i in range(3)]
    hi = [max(p[i] for p in points) for i in range(3)]
    assert degenerate == 0, name + ': degenerate triangles'
    assert abs((hi[1] - lo[1]) - LOA) < .01, name + ': length changed'
    report['lods'].append({
        'name': name,
        'triangles': len(data.polygons),
        'vertices': len(data.vertices),
        'dimensions_xyz': [round(hi[i] - lo[i], 4) for i in range(3)],
        'min_xyz': [round(v, 4) for v in lo],
        'max_xyz': [round(v, 4) for v in hi],
        'boundary_edges': boundary,
        'nonmanifold_edges': nonmanifold,
        'degenerate_faces': degenerate,
        'material_slots': len(data.materials)})
    report['lods'][-1]['protected_material_area_drift'] = area_drift
    ob.select_set(False)

report['landmarks'] = {ob.name: [round(v, 4) for v in ob.matrix_world.translation]
                       for ob in landmarks}
report['published_controls'] = {
    'length_overall_m': 34.6, 'breadth_over_foils_m': 9.5,
    'height_above_baseline_m': 6.25, 'draft_afloat_over_foils_m': 2.35,
    'hull_draft_loaded_m': 1.05, 'freeboard_m': 0.42,
    'propeller_diameter_m': 0.71, 'propeller_blades': 5}

folder = BASE + '/Assets/ShipSimulator/Models/Meteor342U'
os.makedirs(folder, exist_ok=True)
bpy.ops.object.select_all(action='DESELECT')
for ob in export.objects:
    ob.select_set(True)
for ob in landmarks:
    ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=folder + '/Meteor342U.fbx', use_selection=True,
                         object_types={'MESH', 'EMPTY'}, axis_forward='-Z',
                         axis_up='Y', apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_UNITS',
                         bake_space_transform=True, add_leaf_bones=False,
                         bake_anim=False, use_custom_props=True)
export.hide_render = True
export.hide_viewport = True
bpy.ops.object.select_all(action='DESELECT')

with open(BASE + '/Art/Source/Meteor342U/model-report.json', 'w', encoding='utf-8') as f:
    json.dump(report, f, indent=2)
bpy.ops.wm.save_as_mainfile(filepath=BASE + '/Art/Source/Meteor342U/Meteor342U.blend')
print('PASS7|' + json.dumps({'lods': [(l['name'], l['triangles'],
                                       l['dimensions_xyz']) for l in report['lods']]}))
