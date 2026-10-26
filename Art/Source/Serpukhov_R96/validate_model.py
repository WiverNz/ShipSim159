"""Read-only geometry checks in the saved Blender source, via registered MCP."""
import bpy, bmesh, json
from pathlib import Path
from mathutils import Vector
source=Path('G:/Projects/ShipSim159/Art/Source/Serpukhov_R96')
scene=bpy.data.scenes['Serpukhov_R96']
bpy.context.window.scene=scene
checks={}
for name in ['Hull_StationLoft','RaisedForecastle','BridgeShell','MainDeck']:
    obj=bpy.data.objects[name]
    bm=bmesh.new(); bm.from_mesh(obj.data)
    bad=sum(not e.is_manifold for e in bm.edges)
    assert bad==0,(name,bad)
    checks[name]={'nonmanifold_edges':bad,'signed_volume_m3':bm.calc_volume(signed=True)}
    assert bm.calc_volume(signed=True)>0,name
    bm.free()
counts=[]
for level in range(3):
    objects=[o for o in scene.objects if o.type=='MESH' and o.name.startswith('R96_LOD'+str(level)+'_')]
    assert objects
    tris=0
    for obj in objects:
        data=obj.data; data.calc_loop_triangles(); tris+=len(data.loop_triangles)
        assert data.uv_layers.active,obj.name
        assert all(t.area>1e-10 for t in data.loop_triangles),obj.name
        assert all(mat for mat in data.materials),obj.name
    counts.append(tris)
assert 80000>=counts[0]>counts[1]>counts[2]>3000,counts
assert len([o for o in scene.objects if o.name.startswith('BridgeFrontGlass')])==3
assert len([o for o in scene.objects if o.name.startswith('BridgeSideGlass')])==6
assert bpy.data.objects['WaterjetOutlet'].location.y < -10
assert bpy.data.objects['BowReference'].location.y > 10
assert bpy.data.objects['CameraNavigator'].location.z>4.8
assert len(bpy.data.collections['Collision_Reference'].objects)==8
packed=[]
for img in bpy.data.images:
    if not img.name.startswith('R96_'): continue
    assert img.packed_file,img.name
    _=img.pixels[0]
    assert img.has_data,img.name
    packed.append(img.name)
assert len(packed)==30,len(packed)
report={'result':'PASS','closed_shells':checks,'triangles':counts,'packed_original_maps':len(packed),
 'limitations':'Render assembly is not a displacement solid. Inferred sections and colors are not surveyed shipyard data.'}
(source/'saved-source-validation.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
