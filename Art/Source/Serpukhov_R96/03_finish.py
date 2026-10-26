"""Original texture maps, refinement, UV mapping and export-ready source."""
import numpy as np
if scene.get('source_phase') != 'details': raise RuntimeError('Unexpected phase')
# The photographed bow has a relieved underside between the pushing knees.
cutter=mesh('BowReliefCut',[(-1.65,8.9,-.2),(1.65,8.9,-.2),(0,8.9,1.9),
 (-1.65,10.7,-.2),(1.65,10.7,-.2),(0,10.7,1.9)],
 [(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],'Hull',None)
cut(bpy.data.objects['Hull_StationLoft'],cutter)

# A separately hinged wheelhouse door under the aftermost side pane.
bridge=bpy.data.objects['BridgeShell']
cut(bridge,box('BridgeDoorCut',(1.57,3.24,4.16),(.4,.57,.89),'Wheelhouse_Exterior',None,.02))
door=box('WheelhouseDoor',(1.575,3.24,4.16),(.045,.55,.87),'Wheelhouse_Exterior','House_Ivory',.02)
door['moving_part']='bridge access door leaf; upper pane is fixed in this reconstruction'
rod('WheelhouseDoorHandle',(1.63,3.42,4.15),(1.63,3.42,4.30),.016,'Wheelhouse_Exterior','Steel_Dark')

# Thin plate seams and bridge roof brackets visible in the photographs.
for s in (-1,1):
    for y in (-4.5,-1,3,5.5,7.5):
        x=s*(2.5 if y<5 else 2.43 if y<7 else 2.32)
        rod('HullWeld',(x,y,.65),(x,y,1.23),.0035,'Hull','Hull_Grey',6)
    for y in (3.2,4.0,4.75):
        yzprism('VisorBracket',[(y-.07,5.58),(y+.08,5.66),(y+.08,5.5)],s*1.6,s*1.79,'Wheelhouse_Exterior','House_Ivory',.005)

folder=ROOT/'Assets/ShipSimulator/Models/Serpukhov_R96/Textures'
N=512
rng=np.random.default_rng(1973)
Y,X=np.mgrid[0:N,0:N].astype(np.float32)/N
manifest={}
for key,mat in mats.items():
    p=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    color=list(p.inputs['Base Color'].default_value)
    metal=p.inputs['Metallic'].default_value; rough=p.inputs['Roughness'].default_value
    manifest[mat.name]={'color':color,'metallic':metal,'smoothness':1-rough}
    if key in ['Glass','Light_Red','Light_Green','Light_White','Dial','Instrument','Wear']: continue
    grain=rng.normal(0,.008,(N,N)).astype(np.float32)
    cloud=.018*np.sin(X*math.tau*3)*np.sin(Y*math.tau*4)+.007*np.cos(Y*math.tau*23)
    if key=='Wood': cloud+=.05*np.sin(X*math.tau*54+np.sin(Y*math.tau*3))
    height=grain+.35*cloud
    if key=='Deck_Oxide': height+=.015*((np.sin(X*math.tau*150)>0)&(np.sin(Y*math.tau*150)>0))
    # Base maps are stored in sRGB, matching Unity's texture importer.
    linear=np.clip(np.array(color[:3])[None,None,:]*(1+cloud[:,:,None]+grain[:,:,None]),0,1)
    srgb=np.where(linear<=.0031308,linear*12.92,1.055*linear**(1/2.4)-.055)
    base=np.ones((N,N,4),np.float32); base[:,:,:3]=srgb
    norm=np.ones((N,N,4),np.float32); norm[:,:,:3]=(.5,.5,1)
    norm[:,:,0]=.5-(np.roll(height,-1,1)-np.roll(height,1,1))*.6
    norm[:,:,1]=.5-(np.roll(height,-1,0)-np.roll(height,1,0))*.6
    packed=np.zeros((N,N,4),np.float32); packed[:,:,0]=metal; packed[:,:,3]=np.clip(1-rough+cloud,0,1)
    for suffix,pixels,cs in [('BaseColor',base,'sRGB'),('Normal',norm,'Non-Color'),('MetalSmooth',packed,'Non-Color')]:
        img=bpy.data.images.new(mat.name+'_'+suffix,width=N,height=N,alpha=True)
        img.colorspace_settings.name=cs
        img.pixels.foreach_set(pixels.ravel()); img.filepath_raw=str(folder/(img.name+'.png')); img.file_format='PNG'; img.save()
        tex=mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=img
        if suffix=='BaseColor': mat.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
        elif suffix=='Normal':
            normal=mat.node_tree.nodes.new('ShaderNodeNormalMap')
            mat.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color'])
            mat.node_tree.links.new(normal.outputs['Normal'],p.inputs['Normal'])
    manifest[mat.name]['textured']=True
(SOURCE/'materials.json').write_text(json.dumps(manifest,indent=2))

for obj in list(scene.objects):
    if obj.type!='MESH': continue
    apply_mods(obj)
    bm=bmesh.new(); bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data); bm.free()
    uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
    for poly in obj.data.polygons:
        axis=max(range(3),key=lambda i:abs(poly.normal[i]))
        axes=[i for i in range(3) if i!=axis]
        for loop in poly.loop_indices:
            pos=obj.matrix_world@obj.data.vertices[obj.data.loops[loop].vertex_index].co
            uv.data[loop].uv=(pos[axes[0]]/2,pos[axes[1]]/2)
    obj.data.calc_loop_triangles()

# Keep copyright research outside the .blend and game; calibrated original traces remain.
ref=bpy.data.objects.get('R96_Page1_PrivateReference')
if ref: bpy.data.objects.remove(ref,do_unlink=True)
refcol=groups['References']
for s in (-1,1):
    st=json.loads((SOURCE/'measurements.json').read_text())['stations_estimated_from_general_arrangement']
    o=tube('OriginalPlanTrace',[(s*w,y,0) for y,w,k in st],.008,'References','Life_Orange')
    o.hide_render=True; o.hide_viewport=True

scene['source_phase']='finished_source'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Serpukhov_R96.blend'))
print('R96_SOURCE_TRIANGLES',sum(len(o.data.loop_triangles) for o in scene.objects if o.type=='MESH'))
