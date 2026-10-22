"""Source regression checks, including temporary deliberate defects."""
import bpy
import math
import json
from pathlib import Path

def check_propeller(obj):
    mesh=obj.data
    neighbours=[set() for v in mesh.vertices]
    for edge in mesh.edges:
        a,b=edge.vertices; neighbours[a].add(b); neighbours[b].add(a)
    remaining=set(range(len(mesh.vertices))); components=[]
    while remaining:
        todo=[remaining.pop()]; count=0
        while todo:
            i=todo.pop();count+=1
            for j in neighbours[i]:
                if j in remaining:remaining.remove(j);todo.append(j)
        components.append(count)
    assert sorted(components)==[64,64,240,240,240,240], 'Expected hub, shaft and four closed blades'
    radius=max(math.hypot(v.co.x,v.co.z) for v in mesh.vertices)
    assert abs(radius-.9)<.002 and .92-radius>.018, 'Diameter/clearance mismatch'
    assert obj['blade_count']==4

def check_uv(obj):
    for a,b in zip(obj.data.uv_layers[0].data,obj.data.uv_layers[1].data):
        assert abs(a.uv.x-b.uv.x)<1e-5 and abs(a.uv.y+b.uv.y-1)<1e-5

def expect_failure(fn):
    try:fn()
    except AssertionError:return
    raise AssertionError('Regression guard accepted a deliberate defect')

props=[o for o in bpy.context.scene.objects if o.get('blade_count')]
assert len(props)==2
for obj in props:check_propeller(obj)
for obj in bpy.context.scene.objects:
    if obj.type=='MESH':check_uv(obj)
obj=props[0]; original=obj.data; obj.data=original.copy()
try:
    obj.data.vertices[128].co.x=1.2
    expect_failure(lambda:check_propeller(obj))
finally:
    temporary=obj.data;obj.data=original;bpy.data.meshes.remove(temporary)
obj=bpy.data.objects['VD507B_hull']; original=obj.data;obj.data=original.copy()
try:
    obj.data.uv_layers[0].data[0].uv.y+=.25
    expect_failure(lambda:check_uv(obj))
finally:
    temporary=obj.data;obj.data=original;bpy.data.meshes.remove(temporary)
glass=bpy.data.materials['VD507B_Glass']
bs=next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
assert 0<bs.inputs['Alpha'].default_value<1
assert all(i.packed_file for i in bpy.data.images if i.source=='FILE')
report={'status':'PASS','actual_blade_components':4,'oversized_blade_rejected':True,
        'broken_uv_rejected':True,'transparent_glass':True,'packed_images':True,
        'reopened_file':bpy.data.filepath}
with open(Path(bpy.data.filepath).parent/'part3-regression.json','w') as f:json.dump(report,f,indent=2)
print(json.dumps(report))
