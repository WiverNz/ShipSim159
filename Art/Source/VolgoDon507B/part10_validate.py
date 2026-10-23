"""Validate the saved sectional hull using registered Blender MCP."""
import bpy
import bmesh
import csv
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=Path('G:/Projects/ShipSim159'); OUT=ROOT/'Art/Source/VolgoDon507B'
scene=bpy.data.scenes['VolgoDon507B_Source'];bpy.context.window.scene=scene;scene.frame_set(1)
baseline=json.loads((OUT/'part9-geometry-baseline.json').read_text())
unchanged=[]
for record in baseline['meshes']:
    if record['name']=='VD507B_hull':continue
    obj=scene.objects[record['name']]
    geometry=[tuple(round(x,6) for x in v.co) for v in obj.data.vertices]
    geometry += [tuple(p.vertices) for p in obj.data.polygons]
    assert hashlib.sha256(repr(geometry).encode()).hexdigest()==record['geometry_sha256'],obj.name
    unchanged.append(obj.name)

obj=bpy.data.objects['VD507B_HydrostaticSurface'];mesh=obj.data
bm=bmesh.new();bm.from_mesh(mesh)
assert all(e.is_manifold for e in bm.edges)
assert min(f.calc_area() for f in bm.faces)>1e-10
assert bm.calc_volume(signed=True)>0
remaining=set(bm.verts);stack=[next(iter(remaining))];connected=set()
while stack:
    v=stack.pop()
    if v in connected:continue
    connected.add(v);stack.extend(e.other_vert(v) for e in v.link_edges)
assert len(connected)==len(bm.verts)
bvh=BVHTree.FromBMesh(bm)
pairs=[(a,b) for a,b in bvh.overlap(bvh) if a<b and not set(mesh.polygons[a].vertices)&set(mesh.polygons[b].vertices)]
assert not pairs,pairs[:10]
full_volume=bm.calc_volume(signed=True)
bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,
    plane_co=(0,0,0),plane_no=(0,0,1),clear_outer=True)
bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
assert all(e.is_manifold for e in bm.edges)
wet_volume=bm.calc_volume(signed=True);bm.free()

def tree(obj,ring_only=False,blade_only=False):
    vertices=[obj.matrix_world@v.co for v in obj.data.vertices]
    faces=[tuple(p.vertices) for p in obj.data.polygons if
        (not ring_only or all(vertices[i].z < -1.1 for i in p.vertices)) and
        (not blade_only or all(abs(vertices[i].y+64.65)<.45 for i in p.vertices))]
    return BVHTree.FromPolygons(vertices,faces)

hull_obj=bpy.data.objects['VD507B_hull'];hull=tree(hull_obj)
bm=bmesh.new();bm.from_mesh(hull_obj.data)
for v in bm.verts:v.co=hull_obj.matrix_world@v.co
assert not any(not e.is_manifold and max(v.co.z for v in e.verts)<-1e-4 for e in bm.edges)
bm.free();checks=0
try:
    for side in ['Port','Starboard']:
        nozzle=bpy.data.objects['Nozzle_'+side];prop=bpy.data.objects['Propeller_'+side]
        for angle in range(-35,36,5):
            nozzle.rotation_euler.z=math.radians(angle)
            for spin in range(0,360,15):
                prop.rotation_euler.y=math.radians(spin);bpy.context.view_layer.update()
                assert not hull.overlap(tree(bpy.data.objects['NozzleMesh_'+side],ring_only=True)),(side,angle,'duct')
                assert not hull.overlap(tree(bpy.data.objects['PropellerMesh_'+side],blade_only=True)),(side,angle,spin,'blade')
                checks+=1
finally:scene.frame_set(1)

sections=json.loads((OUT/'part10-derived-sections.json').read_text())['sections']
def width(half,z):
    points=[]
    for x,h in half:
        if points and abs(points[-1][0]-h)<1e-8:points[-1]=(h,x)
        else:points.append((h,x))
    if z<points[0][0]-1e-6:return 0
    for (a,x),(b,y) in zip(points,points[1:]):
        if z<=b:return x+(y-x)*(z-a)/max(b-a,1e-8)
    return points[-1][1]

raw=list(csv.DictReader(open(OUT/'part10-traced-sections.csv')))
errors=[];panel_data=[]
for label in sorted(set(float(r['station_label']) for r in raw)):
    rows=[r for r in raw if float(r['station_label'])==label]
    y=float(rows[0]['estimated_y_m'])
    if y<sections[0]['y'] or y>sections[-1]['y']:continue
    fitted=min(sections,key=lambda s:abs(s['y']-y))
    assert abs(fitted['y']-y)<1e-4
    for row in rows:
        z=float(row['height_above_keel_m'])-3.53
        if z>0:continue
        residual=width(fitted['half_section'],z)-float(row['half_breadth_m'])
        point=Vector((float(row['half_breadth_m']),z))
        distance=float('inf')
        for a,b in zip(fitted['half_section'],fitted['half_section'][1:]):
            a,b=Vector(a),Vector(b);delta=b-a
            t=max(0,min(1,(point-a).dot(delta)/max(delta.length_squared,1e-20)))
            distance=min(distance,(point-(a+t*delta)).length)
        errors.append(dict(station=label,pixel_x=int(row['pixel_x']),pixel_y=int(row['pixel_y']),
            z_m=z,trace_half_breadth_m=float(row['half_breadth_m']),fitted_half_breadth_m=width(fitted['half_section'],z),
            horizontal_residual_m=residual,nearest_section_distance_m=distance))
    panel_data.append((label,rows,fitted['half_section']))
(OUT/'part10-trace-residuals.json').write_text(json.dumps(dict(
    note='Residuals compare estimated scaled family traces with the waterline-constrained hull, not error against real 507B offsets.',
    maximum_nearest_section_distance_m=max(r['nearest_section_distance_m'] for r in errors),
    horizontal_residual_warning='Same-height width residual is not meaningful on flat bottom spans; use nearest section distance.',samples=errors),indent=2))
with open(OUT/'part10-derived-waterlines.csv','w',newline='') as f:
    w=csv.writer(f);w.writerow(['estimated_y_m','height_above_keel_m','half_breadth_m','classification'])
    for section in sections:
        for height in [.1,.25,.5,1,1.5,2,2.5,3,3.53]:
            w.writerow([section['y'],height,round(width(section['half_section'],height-3.53),6),'derived interpolation, not traced'])
with open(OUT/'part10-derived-buttocks.csv','w',newline='') as f:
    w=csv.writer(f);w.writerow(['estimated_y_m','half_breadth_m','height_above_keel_m','classification'])
    for section in sections:
        half=section['half_section']
        for breadth in [0,1,2,3,4,5,6,7,8]:
            for (x,z),(xx,zz) in zip(half,half[1:]):
                if x<=breadth<=xx and xx>x:
                    w.writerow([section['y'],breadth,round(z+(zz-z)*(breadth-x)/(xx-x)+3.53,6),'derived interpolation, not traced']);break

# Original vector diagnostic, not a reproduction of the scanned drawing.
svg=['<svg xmlns="http://www.w3.org/2000/svg" width="1400" height="1000" viewBox="0 0 1400 1000">',
     '<rect width="1400" height="1000" fill="white"/>',
     '<text x="30" y="30" font-family="sans-serif" font-size="20">Family trace (blue) vs constrained section (orange), metres above keel</text>']
for i,(label,rows,half) in enumerate(panel_data):
    ox=35+(i%4)*345;oy=65+(i//4)*300
    svg.append(f'<text x="{ox}" y="{oy}" font-family="sans-serif" font-size="16">Station {label:g}</text>')
    def poly(points,colour):
        coords=' '.join(f'{ox+x*31:.2f},{oy+210-h*31:.2f}' for x,h in points)
        svg.append(f'<polyline points="{coords}" fill="none" stroke="{colour}" stroke-width="2"/>')
    poly([(0,0),(8.25,0),(8.25,5.5)],'#cccccc')
    poly([(float(r['half_breadth_m']),float(r['height_above_keel_m'])) for r in rows if float(r['height_above_keel_m'])<=5.5],'#1565c0')
    poly([(x,z+3.53) for x,z in half],'#e65100')
svg.append('</svg>');(OUT/'Previews/part10-section-comparison.svg').write_text('\n'.join(svg))
report=dict(status='PASS',unchanged_other_meshes=unchanged,hydrostatic=dict(
    watertight=True,connected=True,non_adjacent_self_intersections=0,
    full_volume_to_cap_m3=full_volume,submerged_volume_at_selected_waterline_m3=wet_volume,
    cap_height_above_keel_m=5.5,volume_note='Geometric result only; no mass or coefficient fitting; physics unchanged'),
    hull_underwater_nonmanifold_edges=0,propeller_duct_hull_clearance_configurations=checks,
    clearance_scope='Duct rings below Z=-1.1 and blade disk within 0.45 m of shaft centre; shafts/stocks intentionally enter hull',
    trace_maximum_nearest_section_distance_m=max(r['nearest_section_distance_m'] for r in errors))
(OUT/'part10-validation.json').write_text(json.dumps(report,indent=2))
print('PART10_VALIDATION_PASS',wet_volume,'max_trace_distance',report['trace_maximum_nearest_section_distance_m'],checks)
