"""Validate and export the Part 3 candidate without changing the playable prefab."""
import bpy
import bmesh
import math
import json
from pathlib import Path
from mathutils import Vector

ROOT=Path('G:/Projects/ShipSim159')
SOURCE=ROOT/'Art/Source/VolgoDon507B'
TARGET=ROOT/'Assets/ShipSimulator/Models/VolgoDon507B_Improved'
scene=bpy.data.scenes['VolgoDon507B_Source']
meshes=[o for o in scene.objects if o.type=='MESH']
assert len(meshes)==18
report={'status':'PASS','meshes':len(meshes),'rebuilt_components':[]}
for o in meshes:
    assert o.data.library is None and not o.animation_data
    assert len(o.data.uv_layers)==2
    for a,b in zip(o.data.uv_layers[0].data,o.data.uv_layers[1].data):
        assert abs(a.uv.x-b.uv.x)<1e-5 and abs(a.uv.y+b.uv.y-1)<1e-5
    for p in o.data.polygons:
        assert len(p.vertices)==3
        a,b,c=[o.data.vertices[i].co for i in p.vertices]
        assert (b-a).cross(c-a).length>2e-10, (o.name,p.index)
    if o.get('part3_rebuilt'):
        bm=bmesh.new();bm.from_mesh(o.data)
        assert all(e.is_manifold for e in bm.edges),o.name
        bm.free()
        report['rebuilt_components'].append({'name':o.name,'triangles':len(o.data.polygons),'nonmanifold_edges':0})
    if o.get('blade_count'):
        assert o['blade_count']==4
        radius=max(math.hypot(v.co.x,v.co.z) for v in o.data.vertices)
        assert abs(radius-.9)<.002, radius
        assert .92-radius>.018
        report.setdefault('propeller_checks',[]).append({'name':o.name,'radius_m':radius,'radial_clearance_m':.92-radius})
points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
size=[max(p[i] for p in points)-min(p[i] for p in points) for i in range(3)]
assert abs(size[0]-16.7)<.001 and abs(size[1]-138.3)<.001
hull=bpy.data.objects['VD507B_hull']
assert abs(min((hull.matrix_world@v.co).z for v in hull.data.vertices)+3.53)<.001
deck=bpy.data.objects['VD507B_deck']; inv=deck.matrix_world.inverted()
hit,p,normal,index=deck.ray_cast(inv@Vector((7.2,0,20)),(inv.to_3x3()@Vector((0,0,-1))).normalized())
assert hit and abs((deck.matrix_world@p).z-1.97)<.002
images={n.image for o in meshes for m in o.data.materials for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image}
assert all(i.packed_file for i in images)
triangles=sum(len(o.data.polygons) for o in meshes)
report.update(triangles=triangles,bounds_xyz=size,main_deck_z=1.97,hull_keel_z=-3.53,
              packed_images=len(images),uv_regression='PASS',degenerate_triangles=0)
manifest=json.load(open(TARGET/'materials.json'))
manifest['triangles']=triangles
with open(TARGET/'materials.json','w') as f:json.dump(manifest,f,indent=2)
with open(SOURCE/'part3-validation.json','w') as f:json.dump(report,f,indent=2)

camera=scene.camera
scene.render.resolution_percentage=100
views={'side':((180,0,8),(0,0,3),154),
       'front':((0,180,8),(0,0,3),35),
       'top':((0,0,180),(0,0,0),154),
       'perspective':((110,114,80),(0,0,3),154),
       'superstructure':((30,-20,17),(0,-50,6),40),
       'propulsion':((9,-74,-1),(0,-64.65,-2),13),
       'waterline':((30,40,5),(8,30,0),25)}
sun=next(o for o in scene.objects if o.type=='LIGHT')
for name,(position,target,scale) in views.items():
    camera.location=position
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    if name=='top':camera.rotation_euler=(0,0,math.pi/2)
    camera.data.ortho_scale=scale
    sun.rotation_euler=camera.rotation_euler.copy();sun.rotation_euler.x+=.25
    scene.render.filepath=str(SOURCE/'Previews'/('part3-'+name+'.png'))
    bpy.ops.render.render(write_still=True)
for o in scene.objects:o.select_set(o.type in {'MESH','EMPTY'})
bpy.ops.export_scene.fbx(filepath=str(TARGET/'VolgoDon507B.fbx'),use_selection=True,
    object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,add_leaf_bones=False,
    bake_anim=False,use_custom_props=True,path_mode='RELATIVE')
for w in bpy.context.window_manager.windows:
    for area in w.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.region_3d.view_distance=170
            area.spaces.active.region_3d.view_location=(0,0,3)
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'VolgoDon507B.blend'))
print(json.dumps(report))
