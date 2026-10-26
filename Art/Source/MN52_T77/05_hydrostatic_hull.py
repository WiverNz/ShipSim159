"""Match the submerged visual surface to the inferred hydrostatic section table."""
import bpy,bmesh,json
from pathlib import Path
ROOT=Path('G:/Projects/ShipSim159');scene=bpy.data.scenes['MN52_T77'];bpy.context.window.scene=scene
rows=json.loads((ROOT/'Assets/ShipSimulator/Data/Vessels/MN52_T77.json').read_text(encoding='utf-8'))['hydrostatics']['sectionTable']['stations']
verts=[];faces=[];n=21
for row in rows:
 hs=row['heightsAboveKeelM'];ws=row['halfBreadthsM'];levels=[hs[0]+(1.5-hs[0])*i/(n-1) for i in range(n)]
 widths=[]
 for z in levels:
  k=next((i for i in range(len(hs)-1) if hs[i]<=z<=hs[i+1]),len(hs)-2)
  widths.append(ws[k]+(ws[k+1]-ws[k])*(z-hs[k])/(hs[k+1]-hs[k]))
 for x,z in list(zip([-w for w in widths],levels))[::-1]+list(zip(widths,levels)):
  verts.append((x,row['localZ'],z))
ring=n*2
for i in range(len(rows)-1):
 for j in range(ring):faces.append((i*ring+j,i*ring+(j+1)%ring,(i+1)*ring+(j+1)%ring,(i+1)*ring+j))
faces.extend([tuple(range(ring-1,-1,-1)),tuple((len(rows)-1)*ring+j for j in range(ring))])
mesh=bpy.data.meshes.new('Inferred displacement-matched spoon hull');mesh.from_pydata(verts,[],faces);mesh.update()
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
o=bpy.data.objects['MN52_Hull'];o.data=mesh;mesh.materials.append(bpy.data.materials['MN52_Hull']);mesh.materials.append(bpy.data.materials['MN52_LowerHull'])
for p in mesh.polygons:p.material_index=1 if p.center.z<.65 else 0
print('Submerged hull reconciled with estimated hydrostatics')
