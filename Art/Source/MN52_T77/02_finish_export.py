"""Original maps, UVs, LODs and FBX. Execute in live Blender through MCP."""
import bpy,bmesh,math,json,numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path('G:/Projects/ShipSim159');SOURCE=ROOT/'Art/Source/MN52_T77';MODEL=ROOT/'Assets/ShipSimulator/Models/MN52_T77'
scene=bpy.data.scenes['MN52_T77'];bpy.context.window.scene=scene
groups={c.name.removeprefix('MN52_'):c for c in bpy.data.collections['MN52'].children}
mats={m.name.removeprefix('MN52_'):m for m in bpy.data.materials if m.name.startswith('MN52_')}
for ob in list(groups['LODs'].objects):bpy.data.objects.remove(ob,do_unlink=True)
groups['LODs'].hide_render=False
rng=np.random.default_rng(694)
for key in ['Hull','LowerHull','Deck','House','Pipe','Rubber']:
 if bpy.data.images.get('MN52_'+key+'_BaseColor'):continue
 m=mats[key];p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');base=np.array(p.inputs['Base Color'].default_value[:3])
 n=512;noise=rng.normal(0,.012,(n,n));yy,xx=np.mgrid[:n,:n]
 mottling=.012*np.sin(xx*.035)*np.sin(yy*.041)+noise
 color=np.zeros((n,n,4),np.float32);color[:,:,:3]=np.clip(base[None,None,:]+mottling[:,:,None],0,1);color[:,:,3]=1
 normal=np.zeros_like(color);normal[:,:,:3]=(.5,.5,1);normal[:,:,0]+=(np.roll(noise,1,0)-noise)*.4;normal[:,:,1]+=(np.roll(noise,1,1)-noise)*.4;normal[:,:,3]=1
 metal=np.zeros_like(color);metal[:,:,:3]=.18 if key=='Pipe' else .04;metal[:,:,3]=np.clip(.30+noise,.1,.5)
 for suffix,arr,socket in [('BaseColor',color,'Base Color'),('Normal',normal,None),('MetalSmooth',metal,None)]:
  im=bpy.data.images.new('MN52_'+key+'_'+suffix,n,n,alpha=True);im.colorspace_settings.name='sRGB' if suffix=='BaseColor' else 'Non-Color'
  im.pixels.foreach_set(arr.ravel());im.filepath_raw=str(MODEL/'Textures'/(im.name+'.png'));im.file_format='PNG';im.save();im.pack()
  node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=im
  if socket:m.node_tree.links.new(node.outputs['Color'],p.inputs[socket])
  elif suffix=='Normal':
   nn=m.node_tree.nodes.new('ShaderNodeNormalMap');m.node_tree.links.new(node.outputs['Color'],nn.inputs['Color']);m.node_tree.links.new(nn.outputs['Normal'],p.inputs['Normal'])
source=[o for k,c in groups.items() if k not in ['LODs','Collision','Review','PhysicsReferencePoints'] for o in c.objects if o.type=='MESH']
for o in source:
 bpy.context.view_layer.objects.active=o
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 uv=o.data.uv_layers.active or o.data.uv_layers.new(name='UVMap')
 for poly in o.data.polygons:
  normal=o.matrix_world.to_3x3()@poly.normal;axis=max(range(3),key=lambda i:abs(normal[i]));axes=[i for i in range(3) if i!=axis]
  for li in poly.loop_indices:
   co=o.matrix_world@o.data.vertices[o.data.loops[li].vertex_index].co;uv.data[li].uv=(co[axes[0]]*.5,co[axes[1]]*.5)
report={'source_meshes':len(source),'lods':[],'dimensions_m':[6.29,28.92,8.60],'nominal_hull_length_m':28.8,'nominal_hull_breadth_m':6.0,'estimated':True}
exports=[]
for level in range(3):
 batches={}
 for src in source:
  if level>0 and any(t in src.name for t in ['LocalizedWear','ValveSpoke','CabinPanelSeam','HatchHandle','DoorHandle']):continue
  if level==2 and any(t in src.name for t in ['Rail','Flange','Saddle','Spindle','Stay','Lashing','WindowFrame','CabinetHandle','Stiffener']):continue
  ob=bpy.data.objects.new('Copy',src.data.copy());groups['LODs'].objects.link(ob);ob.matrix_world=src.matrix_world.copy()
  bpy.context.view_layer.objects.active=ob
  if level>0:
   mod=ob.modifiers.new('Simplify flat faces','DECIMATE');mod.decimate_type='DISSOLVE';mod.angle_limit=math.radians(10);mod.delimit={'MATERIAL'};bpy.ops.object.modifier_apply(modifier=mod.name)
   if len(ob.data.polygons)>32 and not any(t in src.name for t in ['Hull','Name','Wall','Glass']):
    mod=ob.modifiers.new('Distant detail','DECIMATE');mod.ratio=.5 if level==1 else .2;bpy.ops.object.modifier_apply(modifier=mod.name)
  key='Glass' if mats['Glass'] in ob.data.materials[:] else 'Opaque';batches.setdefault(key,[]).append(ob)
 count=0
 for key,parts in batches.items():
  bpy.ops.object.select_all(action='DESELECT')
  for ob in parts:ob.select_set(True)
  bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();ob=bpy.context.object;ob.name=f'MN52_LOD{level}_{key}'
  bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
  bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bad=[f for f in bm.faces if f.calc_area()<1e-10]
  if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
  bm.to_mesh(ob.data);bm.free();ob.data.calc_loop_triangles();count+=len(ob.data.loop_triangles);exports.append(ob)
 report['lods'].append({'level':level,'triangles':count,'renderers':2})
bpy.ops.object.select_all(action='DESELECT')
for ob in exports+list(groups['PhysicsReferencePoints'].objects):ob.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(MODEL/'MN52_T77.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Y',axis_up='Z',global_scale=1,apply_unit_scale=True,bake_space_transform=False,use_mesh_modifiers=True,add_leaf_bones=False,bake_anim=False,path_mode='RELATIVE')
for ob in exports:ob.hide_set(True)
groups['LODs'].hide_render=True
(SOURCE/'model-report.json').write_text(json.dumps(report,indent=2))
scene['phase']='exported'
# A separate source file keeps the existing Serpukhov file untouched.
bpy.data.libraries.write(str(SOURCE/'MN52_T77.blend'),{scene},path_remap='RELATIVE',fake_user=True)
print(json.dumps(report))
