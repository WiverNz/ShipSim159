"""Local lower-shell reconstruction; run only through registered Blender MCP."""
import bpy
import bmesh
import csv
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import barycentric_transform

ROOT = Path('G:/Projects/ShipSim159')
OUT = ROOT / 'Art/Source/VolgoDon507B'
scene = bpy.data.scenes['VolgoDon507B_Source']
bpy.context.window.scene = scene
scene.frame_set(1)
obj = bpy.data.objects['VD507B_hull']
archive_scene = bpy.data.scenes.get('VolgoDon507B_Part9_Hull')
if not archive_scene:
    assert not scene.get('part10_hull')
    archive_scene = bpy.data.scenes.new('VolgoDon507B_Part9_Hull')
    old = obj.copy()
    old.data = obj.data.copy()
    old.name = 'Part9_ArchivedHull'
    archive_scene.collection.objects.link(old)
else:
    old = archive_scene.objects['Part9_ArchivedHull']

trace = json.loads((OUT / 'part10-trace.json').read_text())
rect = trace['rectification']
converted = []
for section in trace['sections']:
    side = 0 if section['side'] == 'port' else 2
    span = abs(rect['centre_x'] - rect['port_outer_x' if side == 0 else 'starboard_outer_x'])
    samples = []
    for px, py in section['pixels']:
        fraction = abs(px - rect['centre_x']) / span
        baseline = 249 + fraction * (rect['baseline_y_at_port_centre_starboard'][side] - 249)
        wl = 147 + fraction * (rect['design_waterline_y_at_port_centre_starboard'][side] - 147)
        height = max(0, (baseline-py) * 3.53/(baseline-wl))
        samples.append([height, min(8.25, fraction*8.25), px, py])
    samples.sort()
    converted.append(dict(label=section['label'], y=67.5-6.75*section['label'], samples=samples))
with open(OUT / 'part10-traced-sections.csv', 'w', newline='') as f:
    writer = csv.writer(f)
    writer.writerow(['station_label','estimated_y_m','pixel_x','pixel_y','height_above_keel_m','half_breadth_m','uncertainty_px'])
    for s in converted:
        for h,x,px,py in s['samples']:
            writer.writerow([s['label'],s['y'],px,py,round(h,6),round(x,6),3])

def linear(points, t):
    if t <= points[0][0]: return points[0][1]
    for (a,x),(b,y) in zip(points,points[1:]):
        if t <= b: return x+(y-x)*(t-a)/max(b-a,1e-9)
    return points[-1][1]

def pchip(points, t):
    if t <= points[0][0]: return points[0][1]
    if t >= points[-1][0]: return points[-1][1]
    slopes=[(b[1]-a[1])/(b[0]-a[0]) for a,b in zip(points,points[1:])]
    derivatives=[slopes[0]]
    for i in range(1,len(points)-1):
        left,right=slopes[i-1],slopes[i]
        if left*right <= 0: derivatives.append(0)
        else:
            h0=points[i][0]-points[i-1][0]; h1=points[i+1][0]-points[i][0]
            w0=2*h1+h0; w1=h1+2*h0
            derivatives.append((w0+w1)/(w0/left+w1/right))
    derivatives.append(slopes[-1])
    for i,((a,x),(b,y)) in enumerate(zip(points,points[1:])):
        if t <= b:
            u=(t-a)/(b-a)
            return (2*u**3-3*u*u+1)*x+(u**3-2*u*u+u)*(b-a)*derivatives[i]+(-2*u**3+3*u*u)*y+(u**3-u*u)*(b-a)*derivatives[i+1]

vertices=[old.matrix_world@v.co for v in old.data.vertices]
polygons=[tuple(p.vertices) for p in old.data.polygons]
tree=BVHTree.FromPolygons(vertices,polygons)
cuts=[]
for face in polygons:
    for ia,ib in zip(face,face[1:]+face[:1]):
        a,b=vertices[ia],vertices[ib]
        if a.z*b.z < 0: cuts.append(a+(b-a)*(-a.z/(b.z-a.z)))
ymin=min(p.y for p in cuts); ymax=max(p.y for p in cuts)
ys=sorted(set([round(p.y,5) for p in cuts]+[round(ymin+(ymax-ymin)*i/72,5) for i in range(73)]+
              [s['y'] for s in converted if ymin<s['y']<ymax]))
ys[0]=ymin; ys[-1]=ymax

def old_width(y,z,sign=1):
    hit=tree.ray_cast(Vector((sign*20,y,z)),Vector((-sign,0,0)),40)[0]
    return abs(hit.x) if hit else None

for s in converted:
    # Monotone envelope removes sub-pixel reversals, retaining the raw samples above.
    points=[]; width=0
    for h,x,*_ in s['samples']:
        width=max(width,x)
        if points and abs(h-points[-1][0])<1e-5: points[-1]=(h,width)
        else: points.append((h,width))
    s['curve']=points
    s['bottom']=min(3.525,points[0][0])
    s['waterline_width']=max(.001,linear(points,3.53))

def section_at(y):
    bottom_points=sorted((s['y'],s['bottom']-3.53) for s in converted)
    bottom_points=[(ymin,0)]+[(a,b) for a,b in bottom_points if ymin<a<ymax]+[(ymax,0)]
    bottom=pchip(bottom_points,y)
    top=old_width(y,0)
    if top is None: top=0
    fractions=[0,.002,.006,.015,.035,.07,.13,.23,.38,.58,.78,1]
    half=[(0,bottom)]
    for fraction in fractions:
        ratio_points=[]
        for s in converted:
            height=s['bottom']+(3.53-s['bottom'])*fraction
            ratio_points.append((s['y'],min(1,linear(s['curve'],height)/s['waterline_width'])))
        ratio=max(0,min(1,pchip(sorted(ratio_points),y)))
        half.append((ratio*top,bottom+(0-bottom)*fraction))
    return half

bm=bmesh.new(); bm.from_mesh(old.data)
for v in bm.verts: v.co=old.matrix_world@v.co
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,
    plane_co=(0,0,0),plane_no=(0,0,1),clear_inner=True,clear_outer=False)
# Match every new waterline point with an edge split in the retained upper shell.
for edge in list(bm.edges):
    a,b=edge.verts
    if abs(a.co.z)>1e-5 or abs(b.co.z)>1e-5 or abs(a.co.y-b.co.y)<1e-6: continue
    if a.co.y>b.co.y: a,b=b,a
    middle=[y for y in ys if a.co.y+1e-4<y<b.co.y-1e-4]
    current=a
    for y in middle:
        remaining=next((e for e in current.link_edges if b in e.verts),None)
        if remaining is None: break
        _,new=bmesh.utils.edge_split(remaining,current,(y-current.co.y)/(b.co.y-current.co.y))
        current=new

rings=[]; output_sections=[]
for y in ys:
    half=section_at(y)
    ring=[bm.verts.new((x,y,z)) for x,z in reversed(half)]
    ring += [bm.verts.new((-x,y,z)) for x,z in half[1:]]
    rings.append(ring)
    output_sections.append(dict(y=y,half_section=half))
new_faces=[]
for first,second in zip(rings,rings[1:]):
    for i in range(len(first)-1):
        face=bm.faces.new((first[i],second[i],second[i+1],first[i+1]))
        face.smooth=True; new_faces.append(face)
uv0=bm.loops.layers.uv.get(old.data.uv_layers[0].name)
uv1=bm.loops.layers.uv.get(old.data.uv_layers[1].name)
for face in new_faces:
    for loop in face.loops:
        co,_,index,_=tree.find_nearest(loop.vert.co)
        p=old.data.polygons[index]
        a,b,c=[vertices[i] for i in p.vertices]
        uv=[Vector((*old.data.uv_layers[0].data[i].uv,0)) for i in p.loop_indices]
        result=barycentric_transform(co,a,b,c,*uv)
        loop[uv0].uv=(result.x,result.y)
        loop[uv1].uv=(result.x,1-result.y)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bmesh.ops.triangulate(bm,faces=list(bm.faces))
inverse=obj.matrix_world.inverted()
for v in bm.verts: v.co=inverse@v.co
mesh=bpy.data.meshes.new('VD507B_hull_Part10')
bm.to_mesh(mesh);bm.free();mesh.materials.append(old.data.materials[0]);mesh.update()
obj.data=mesh

# A sectional derivative excludes overlapping cosmetic upper-shell islands.
hydro_scene=bpy.data.scenes.get('VolgoDon507B_Hydrostatic') or bpy.data.scenes.new('VolgoDon507B_Hydrostatic')
for o in list(hydro_scene.objects): bpy.data.objects.remove(o,do_unlink=True)
cap=1.97
cap_cuts=[]
for face in polygons:
    for ia,ib in zip(face,face[1:]+face[:1]):
        a,b=vertices[ia],vertices[ib]
        if (a.z-cap)*(b.z-cap)<0: cap_cuts.append(a+(b-a)*((cap-a.z)/(b.z-a.z)))
cap_min=min(p.y for p in cap_cuts); cap_max=max(p.y for p in cap_cuts)
hydro_ys=sorted(set([cap_min,cap_max]+ys+[cap_min+(ymin-cap_min)*i/8 for i in range(1,8)]+
    [ymax+(cap_max-ymax)*i/8 for i in range(1,8)]))
filtered=[]
for y in hydro_ys:
    if not filtered or y-filtered[-1]>.01:filtered.append(y)
filtered[-1]=cap_max
hydro_ys=filtered
bm=bmesh.new();hydro_rings=[]
for yi,y in enumerate(hydro_ys):
    inside=ymin<y<ymax
    lower=section_at(y) if inside else None
    if inside: bottom=lower[0][1]
    else:
        hit=tree.ray_cast(Vector((0,y,-20)),Vector((0,0,1)),40)[0]
        bottom=min(cap,hit.z) if hit else cap
    if yi in (0,len(hydro_ys)-1): bottom=cap
    half=[(0,bottom)]
    for t in [0,.001,.003,.008,.02,.04,.07,.1,.15,.22,.32,.45,.6,.75,.9,1]:
        z=bottom+(cap-bottom)*t
        if inside and z<=0:
            points=[]
            for x,h in lower:
                if points and abs(points[-1][0]-h)<1e-7:points[-1]=(h,x)
                else:points.append((h,x))
            width=linear(points,z)
        else: width=old_width(y,z) or 0
        if yi in (0,len(hydro_ys)-1):width=0
        half.append((width,z))
    ring=[bm.verts.new((x,y,z)) for x,z in reversed(half)]
    ring += [bm.verts.new((-x,y,z)) for x,z in half[1:]]
    hydro_rings.append(ring)
for first,second in zip(hydro_rings,hydro_rings[1:]):
    for i in range(len(first)):
        j=(i+1)%len(first)
        bm.faces.new((first[i],second[i],second[j],first[j]))
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
seen=set(); duplicates=[]
bm.verts.index_update()
for face in bm.faces:
    key=tuple(sorted(v.index for v in face.verts))
    if key in seen: duplicates.append(face)
    seen.add(key)
if duplicates:bmesh.ops.delete(bm,geom=duplicates,context='FACES_ONLY')
bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0)
bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=1e-7)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
if bm.calc_volume(signed=True)<0:bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
bmesh.ops.triangulate(bm,faces=list(bm.faces))
bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.00001)
hydro_report=dict(vertices=len(bm.verts),triangles=len(bm.faces),boundary_edges=sum(e.is_boundary for e in bm.edges),
    non_manifold_edges=sum(not e.is_manifold for e in bm.edges),volume_to_deck_cap_m3=bm.calc_volume(signed=True))
hydro=bpy.data.meshes.new('VD507B_HydrostaticSurface')
bm.to_mesh(hydro);bm.free()
hydro_obj=bpy.data.objects.new('VD507B_HydrostaticSurface',hydro);hydro_scene.collection.objects.link(hydro_obj)
hydro_obj['purpose']='Closed geometry for future hydrostatics; never export as a gameplay renderer or collider'
hydro_obj['cap_z_m']=1.97
scene['part10_hull']=True
scene.frame_set(1)
mesh.calc_loop_triangles()
(OUT/'part10-derived-sections.json').write_text(json.dumps(dict(method='Interpolated and constrained to retained visual waterline; not measured offsets',sections=output_sections),indent=2))
(OUT/'part10-build.json').write_text(json.dumps(dict(hull_triangles=len(mesh.loop_triangles),section_count=len(ys),hydrostatic=hydro_report),indent=2))
print('PART10_BUILD',len(mesh.loop_triangles),len(ys),hydro_report)
