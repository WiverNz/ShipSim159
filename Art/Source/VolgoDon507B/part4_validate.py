"""Validate source mechanisms and sampled movement without exporting."""
import bpy
import math
import json
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path('G:/Projects/ShipSim159')
SOURCE=ROOT/'Art/Source/VolgoDon507B'
scene=bpy.data.scenes['VolgoDon507B_Source']
manifest=json.load(open(SOURCE/'mechanisms.json'))

def tree(objects):
    vertices=[];polygons=[]
    for o in objects:
        base=len(vertices);vertices.extend(o.matrix_world@v.co for v in o.data.vertices)
        polygons.extend(tuple(base+i for i in p.vertices) for p in o.data.polygons)
    return BVHTree.FromPolygons(vertices,polygons)

scene.frame_set(1)
meshes=[o for o in scene.objects if o.type=='MESH']
triangles=0
for obj in meshes:
    assert obj.data.library is None and len(obj.data.uv_layers)==2
    obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
    for tri in obj.data.loop_triangles:
        a,b,c=[obj.matrix_world@obj.data.vertices[i].co for i in tri.vertices]
        assert (b-a).cross(c-a).length>1e-9,(obj.name,'degenerate')
    for a,b in zip(obj.data.uv_layers[0].data,obj.data.uv_layers[1].data):
        assert abs(a.uv.x-b.uv.x)<1e-4 and abs(a.uv.y+b.uv.y-1)<1e-4,obj.name
for spec in manifest['mechanisms']:
    obj=bpy.data.objects[spec['name']]
    assert obj.parent.name=='Mechanisms' and obj.animation_data
    assert (obj.location-Vector(spec['rest_position'])).length<1e-5

root=bpy.data.objects['Mechanisms']
moving={o for r in root.children if r.animation_data for o in [r,*r.children_recursive] if o.type=='MESH'}
fixed=tree([o for o in meshes if o not in moving])
keys=['AftOuter','AftInner','ForeInner','ForeOuter']
closed_contacts={k:len(tree([bpy.data.objects['Panel_'+k]]).overlap(fixed)) for k in keys}
minimum_stack_gap=100
sample_count=0
for frame in range(120,661):
    scene.frame_set(frame)
    panels={k:tree([o for o in bpy.data.objects['Hatch_'+k].children if o.type=='MESH']) for k in keys}
    for i,a in enumerate(keys):
        for b in keys[i+1:]:
            assert not panels[a].overlap(panels[b]),('panel collision',frame,a,b)
        assert not tree([bpy.data.objects['Panel_'+a]]).overlap(fixed),('fixed obstruction',frame,a)
    if frame==210:
        for outer,inner in [('AftOuter','AftInner'),('ForeOuter','ForeInner')]:
            low=min((o.matrix_world@v.co).z for o in bpy.data.objects['Hatch_'+outer].children if o.type=='MESH' for v in o.data.vertices)
            high=max((o.matrix_world@v.co).z for o in bpy.data.objects['Hatch_'+inner].children if o.type=='MESH' for v in o.data.vertices)
            minimum_stack_gap=min(minimum_stack_gap,low-high)
    sample_count+=1
assert minimum_stack_gap>.04,minimum_stack_gap

nozzle_samples=0
for side in ['Port','Starboard']:
    scene.frame_set(1)
    prop=bpy.data.objects['Propeller_'+side];nozzle=bpy.data.objects['Nozzle_'+side]
    for angle in range(-35,36,5):
        nozzle.rotation_euler.z=math.radians(angle)
        for spin in range(0,360,15):
            prop.rotation_euler.y=math.radians(spin);bpy.context.view_layer.update()
            assert not tree(prop.children).overlap(tree(nozzle.children)),('propeller/nozzle collision',side,angle,spin)
            nozzle_samples+=1
scene.frame_set(740)
hull=tree([bpy.data.objects[n] for n in ['VD507B_hull','VD507B_hull_detali','VD507B_deck']])
for side in ['Port','Starboard','Stern']:
    assert not tree([bpy.data.objects['AnchorMesh_'+side]]).overlap(hull),('unseat blocked',side)

# Deliberately remove the stacking lift; the overlap guard must reject it.
scene.frame_set(210)
outer=bpy.data.objects['Hatch_AftOuter'];original=outer.location.copy()
try:
    outer.location.z-=.93;bpy.context.view_layer.update()
    assert tree(outer.children).overlap(tree(bpy.data.objects['Hatch_AftInner'].children)), 'Broken stack not detected'
finally:outer.location=original

scene.frame_set(1)
pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
size=[max(v[k] for v in pts)-min(v[k] for v in pts) for k in range(3)]
assert abs(size[0]-16.7)<.001 and abs(size[1]-138.3)<.001
report={'status':'PASS','mesh_count':len(meshes),'triangles':triangles,'mechanism_bindings':len(manifest['mechanisms']),
        'hatch_motion_frames_checked':sample_count,'propeller_nozzle_configurations_checked':nozzle_samples,
        'minimum_stacked_geometry_gap_m':minimum_stack_gap,'closed_seal_contacts':closed_contacts,
        'deliberate_stack_collision_detected':True,'unseated_anchors_clear':True,'bounds_xyz':size,
        'limits':'Discrete motion checks; closed seal contact and anchor stow/hawse contact are intentional. No chain payout or hydraulic hose simulation.'}
with open(SOURCE/'part4-validation.json','w') as f:json.dump(report,f,indent=2)
print(json.dumps(report))
