"""Validate the final saved scene via Blender MCP; no external Blender process."""
import bpy,bmesh,json,math
from pathlib import Path
s=bpy.data.scenes['MN52_T77'];report={'result':'PASS','source_meshes':0,'lods':[]}
for o in s.objects:
 if o.type!='MESH':continue
 if 'LOD' in o.name:
  o.data.calc_loop_triangles();assert o.data.uv_layers.active is not None,o.name
  assert all(t.area>1e-10 for t in o.data.loop_triangles),o.name
  report['lods'].append({'name':o.name,'triangles':len(o.data.loop_triangles)})
 elif not o.name.startswith('MN52_Collision'):report['source_meshes']+=1
 assert all(math.isfinite(v) for vert in o.data.vertices for v in vert.co),o.name
bm=bmesh.new();bm.from_mesh(bpy.data.objects['MN52_Hull'].data)
report['hull_nonmanifold_edges']=sum(not e.is_manifold for e in bm.edges)
report['hull_signed_volume_m3']=bm.calc_volume(signed=True)
assert report['hull_nonmanifold_edges']==0;assert report['hull_signed_volume_m3']>0;bm.free()
report['packed_maps']=sum(im.name.startswith('MN52_') and bool(im.packed_file) for im in bpy.data.images)
assert report['packed_maps']==18
report['limitations']='Submerged offsets fitted to published displacement/draft pairs, not surveyed shipyard lines.'
Path('G:/Projects/ShipSim159/Art/Source/MN52_T77/geometry-validation.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
