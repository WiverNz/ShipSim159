"""Build batched static LODs while preserving editable source and moving pivots."""
if scene.get('source_phase') not in ('finished_source','exported'): raise RuntimeError('Source not finished')
glass=mats['Glass']; glass.surface_render_method='BLENDED'
bsdf=next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
bsdf.inputs['Transmission Weight'].default_value=0
modeldir=ROOT/'Assets/ShipSimulator/Models/Serpukhov_R96'
for o in list(groups['LODs'].objects): bpy.data.objects.remove(o,do_unlink=True)
source=[o for c in root.children if c.name not in ('References','LODs','Collision_Reference') for o in c.objects if o.type=='MESH']
for obj in source:
    for index in range(len(obj.data.materials)-1,-1,-1):
        if obj.data.materials[index] is None:
            assert not any(p.material_index==index for p in obj.data.polygons),obj.name
            obj.data.materials.pop(index=index)
deps=bpy.context.evaluated_depsgraph_get()
report={'lods':[],'source_objects':len(source),'source_closed_shell_checks':{}}
for name in ('Hull_StationLoft','RaisedForecastle','BridgeShell'):
    obj=bpy.data.objects[name]; bm=bmesh.new(); bm.from_mesh(obj.data)
    report['source_closed_shell_checks'][name]={'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges)}
    bm.free()

moving_names={o.name for o in source if o.get('moving_part')}
moving_names.update(o.name for o in source if o.parent and o.parent.name in moving_names)
all_exports=[]
for level in range(3):
    batches={}; copied=[]
    for src in source:
        if level>=1 and (src.name.startswith(('Gauge','Floorboard','HullWeld','AnchorChain','WinchWire','ConsoleLabel','Knee_ContactBolt','VentLouvre.'))): continue
        if level==2 and (src.users_collection[0].name=='Wheelhouse_Interior' or src.name.startswith(('Rail','MastStay','WindowWiper','DoorHandle','HatchHandle','BollardCross','LightBracket','VisorBracket','LifeRingBand','Fairlead','IntakeGrate','ImpellerBlade'))): continue
        if level==2 and src.name.startswith(('Name_','Forecastle_PortholeRim','Engine_Porthole','DoorJamb','DoorHeader','BridgeFrame','FrontFrame','BridgeMullion','FrontMullion','House_External','BridgeRearStiff','VentLouvre','FittingWear','HullWeld','WheelhouseDoorHandle','Floorboard','Gauge','Lever','WinchMotor')): continue
        ob=bpy.data.objects.new(src.name+'_LOD'+str(level),src.data.copy())
        groups['LODs'].objects.link(ob); ob.matrix_world=src.matrix_world.copy()
        ob.parent=None
        if level>0:
            bpy.context.view_layer.objects.active=ob
            dec=ob.modifiers.new('Preserve material boundaries','DECIMATE'); dec.decimate_type='DISSOLVE'
            dec.angle_limit=math.radians(8 if level==1 else 25); dec.delimit={'MATERIAL'}
            bpy.ops.object.modifier_apply(modifier=dec.name)
        # Flat UVs have many seams; a conservative collapse on small fittings only.
        if level>0 and len(ob.data.polygons)>30 and not any(m==glass for m in src.data.materials) and not src.name.startswith(('Hull_Station','BridgeShell','MainDeck','Raised','ForecastleDeck','EngineCasing','LowerHouse','LifeRing')):
            bpy.context.view_layer.objects.active=ob
            d=ob.modifiers.new('Distant fitting reduction','DECIMATE'); d.ratio=.5 if level==1 else .12
            bpy.ops.object.modifier_apply(modifier=d.name)
        key='Glass' if any(m==glass for m in ob.data.materials) else 'Opaque'
        if src.name in moving_names and level==0:
            key=src.name
            ob['moving_part']=src.get('moving_part','impeller blade')
        batches.setdefault(key,[]).append(ob)
    export=[]
    for key,parts in batches.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in parts: o.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]
        if len(parts)>1: bpy.ops.object.join()
        ob=bpy.context.object
        ob.name='R96_LOD'+str(level)+'_'+key
        bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
        bm=bmesh.new(); bm.from_mesh(ob.data)
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bad=[f for f in bm.faces if f.calc_area()<1e-10]
        if bad: bmesh.ops.delete(bm,geom=bad,context='FACES')
        bm.to_mesh(ob.data); bm.free(); ob.data.update(); ob.data.calc_loop_triangles()
        export.append(ob)
    count=sum(len(o.data.loop_triangles) for o in export)
    report['lods'].append({'level':level,'triangles':count,'renderers':len(export),'degenerate_triangles':0})
    all_exports.extend(export)

# Source objects stay editable, exported batches live only in the LOD collection.
anchors=[o for o in scene.objects if o.type=='EMPTY']
bpy.ops.object.select_all(action='DESELECT')
for o in all_exports+anchors: o.hide_set(False); o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(modeldir/'Serpukhov_R96.fbx'),use_selection=True,
 object_types={'MESH','EMPTY'},axis_forward='-Y',axis_up='Z',global_scale=1,
 apply_unit_scale=True,bake_space_transform=False,use_mesh_modifiers=True,add_leaf_bones=False,
 bake_anim=False,path_mode='RELATIVE',use_custom_props=True)
for o in all_exports: o.hide_set(True)
groups['LODs'].hide_render=True
report['axes']='Blender X starboard, Y forward, Z up; baseline origin; landmarks used by Unity'
(SOURCE/'model-report.json').write_text(json.dumps(report,indent=2))
scene['source_phase']='exported'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Serpukhov_R96.blend'))
print(json.dumps(report))
