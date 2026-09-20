"""Final proportions, review views, evaluated LODs, and FBX export from Blender."""
exec(compile(open('G:/Projects/ShipSim159/Art/Source/Luch14352/resume.py',encoding='utf-8').read(),'resume.py','exec'))
import json
tree=ast.parse(open(BASE+'/Art/Source/Luch14352/02_detail.py',encoding='utf-8').read())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef)],type_ignores=[]),'detail_helpers','exec'))
for o in list(model.objects):
    if o.name.startswith('Nameplate_'):bpy.data.objects.remove(o,do_unlink=True)
if not bpy.data.objects.get('Wheelhouse_RearFrame'):
    outer=[(-1.54,7.78,2.22),(1.54,7.78,2.22),(1.47,7.55,2.84),(-1.47,7.55,2.84)]
    center=sum((Vector(p) for p in outer),Vector())/4
    inner=[tuple(center+(Vector(p)-center)*.76) for p in outer]
    ring3('Wheelhouse_RearFrame',outer,inner,(0,.06,0),white)
    prism('Wheelhouse_RearGlass',[(x,y+.025,z) for x,y,z in inner],(0,.008,0),glass)
# The transom deck overhang includes the aft boarding platform in the overall length.
for v in bpy.data.objects['Deck_Obnose'].data.vertices:
    if v.co.y < -11.5:v.co.y=-11.846
engine=bpy.data.objects['EngineRoom']
for v in engine.data.vertices:
    if v.co.y < -9.99 and v.co.z>1:v.co.z=.72
next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Transmission Weight'].default_value=.15
scene.eevee.taa_render_samples=96
for name,position in [('BowReference',(0,11.86,0)),('SternReference',(0,-11.56,0)),('StarboardReference',(2.265,0,0)),('UpReference',(0,0,1)),('Waterline',(0,0,0)),('CameraNavigator',(-.65,9.18,2.38)),('WaterJetPoint',(0,-11.76,-.12))]:
    o=bpy.data.objects.new(name,None);model.objects.link(o);o.parent=root;o.location=position

for name,loc in [('port',(-32,0,1.3)),('starboard',(32,0,1.3)),('front-quarter',(22,30,12)),('rear-quarter',(-23,-30,12)),('top-front',(13,17,30)),('skegs',(-17,-24,-8))]:
    view('final-'+name,loc)

export=bpy.data.collections.new('GameReady_LODs');scene.collection.children.link(export)
deps=bpy.context.evaluated_depsgraph_get()
source=[o for o in model.objects if o.type in {'MESH','FONT'}]
materials=[]
for ob in source:
    for slot in ob.material_slots:
        if slot.material not in materials:materials.append(slot.material)

report={'units':'metres','blender_axes':'+Y forward, +X starboard, +Z up','waterline_z':0,'baseline_z':-.66,'wheelhouse_top_above_baseline':3.60,'lods':[]}
for level,ratio in [(0,1.0),(1,.45),(2,1.0)]:
    disabled=[]
    if level==2:
        for ob in source:
            for mod in ob.modifiers:
                if mod.type=='BEVEL' and mod.show_viewport:
                    mod.show_viewport=False;disabled.append(mod)
        bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get()
    verts=[];faces=[];indices=[];smooth=[]
    for ob in source:
        if level==2 and any(s in ob.name for s in ['InteriorProxy_Seat','InteriorProxy_Back','Wiper','DoorHandle','DoorSeam','WindowTransom','WindowSeal','Roof_Handrail','EngineRoom_Strake','EngineRoom_VentLouvre','MooringCleat','BowRamp_HydraulicCylinder','BowRamp_Tread','IntakeGrille','Cushion_SealRib','Nameplate']):continue
        ev=ob.evaluated_get(deps);data=ev.to_mesh();offset=len(verts)
        clean=bmesh.new();clean.from_mesh(data)
        bmesh.ops.remove_doubles(clean,verts=list(clean.verts),dist=.000001)
        bmesh.ops.dissolve_degenerate(clean,edges=list(clean.edges),dist=.0000001)
        clean.to_mesh(data);clean.free()
        verts.extend(tuple(ob.matrix_world@v.co) for v in data.vertices)
        for p in data.polygons:
            faces.append(tuple(offset+i for i in p.vertices))
            indices.append(materials.index(ob.material_slots[p.material_index].material));smooth.append(p.use_smooth)
        ev.to_mesh_clear()
    for mod in disabled:mod.show_viewport=True
    bpy.context.view_layer.update()
    data=bpy.data.meshes.new('Luch14352_LOD'+str(level));data.from_pydata(verts,[],faces);data.update()
    for m in materials:data.materials.append(m)
    for p,i,s in zip(data.polygons,indices,smooth):p.material_index=i;p.use_smooth=s
    ob=bpy.data.objects.new('Luch14352_LOD'+str(level),data);export.objects.link(ob)
    bpy.context.view_layer.objects.active=ob;ob.select_set(True)
    bm=bmesh.new();bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(data);bm.free()
    if ratio<1:
        mod=ob.modifiers.new('Distance simplification','DECIMATE');mod.ratio=ratio
        bpy.ops.object.modifier_apply(modifier=mod.name)
    triangulate=ob.modifiers.new('Export triangles','TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    # A UV channel is included for future material authoring; no reference photo is a texture.
    data=ob.data;uv=data.uv_layers.new(name='Planar_Metres')
    for p in data.polygons:
        axis=max(range(3),key=lambda i:abs(p.normal[i]));a,b=[i for i in range(3) if i!=axis]
        for li in p.loop_indices:
            co=data.vertices[data.loops[li].vertex_index].co;uv.data[li].uv=(co[a]/8,co[b]/8)
    bm=bmesh.new();bm.from_mesh(data)
    boundary=sum(1 for e in bm.edges if e.is_boundary)
    nonmanifold=sum(1 for e in bm.edges if not e.is_manifold)
    degenerate=sum(1 for f in bm.faces if f.calc_area()<1e-12)
    bm.free()
    points=[ob.matrix_world@v.co for v in data.vertices]
    minimum=[min(v[i] for v in points) for i in range(3)];maximum=[max(v[i] for v in points) for i in range(3)]
    report['lods'].append({'name':ob.name,'triangles':len(data.polygons),'dimensions_xyz':[maximum[i]-minimum[i] for i in range(3)],'min_xyz':minimum,'max_xyz':maximum,'boundary_edges':boundary,'nonmanifold_edges':nonmanifold,'degenerate_faces':degenerate})
    ob.select_set(False)

folder=BASE+'/Assets/ShipSimulator/Models/Luch14352'
bpy.ops.object.select_all(action='DESELECT')
for ob in export.objects:ob.select_set(True)
for ob in model.objects:
    if ob.type=='EMPTY':ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=folder+'/Luch14352.fbx',use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,add_leaf_bones=False,bake_anim=False,use_custom_props=True)
export.hide_render=True;export.hide_viewport=True
with open(BASE+'/Art/Source/Luch14352/model-report.json','w',encoding='utf-8') as f:json.dump(report,f,indent=2)
# Source geometry and reference objects remain editable; photographs are linked viewport references.
view('final-front-quarter',(22,30,12))
bpy.ops.wm.save_as_mainfile(filepath=BASE+'/Art/Source/Luch14352/Luch14352.blend')
print(json.dumps(report,indent=2))
