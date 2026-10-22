"""Localized reference corrections. Run in the saved Part 2 scene via Blender MCP."""
import bpy
import bmesh
import math
import json
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path('G:/Projects/ShipSim159')
SOURCE = ROOT / 'Art/Source/VolgoDon507B'
scene = bpy.data.scenes['VolgoDon507B_Source']
assert not scene.get('part3_geometry'), 'Already applied; use the milestone backup to rebuild'
report = {'reference': 'doc257.pdf pages 1, 2, 3, 5, 7, 8', 'changes': []}

# The ray-tested side deck was 2.143 m, not the lower internal hold vertices.
deck_before = 2.143
deck_after = 5.5 - 3.53
delta = deck_before - deck_after
for obj in [o for o in scene.objects if o.type == 'MESH']:
    inv = obj.matrix_world.inverted()
    for v in obj.data.vertices:
        p = obj.matrix_world @ v.co
        p.z -= delta * min(max(p.z / deck_before, 0), 1)
        v.co = inv @ p
    obj.data.update()
report['changes'].append({'main_deck_before_m': deck_before, 'main_deck_after_m': deck_after,
                          'method': 'Continuous vertical correction above waterline; underwater hull retained'})

def finish_mesh(obj, vertices, faces, center, mat):
    mesh = bpy.data.meshes.new(obj.name + '_ReferenceGeometry')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    bm = bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.to_mesh(mesh); bm.free()
    obj.data = mesh
    obj.matrix_world = Matrix.Translation(Vector(center))
    mesh.materials.append(mat)
    uv = mesh.uv_layers.new(name='SurfaceUV')
    legacy = mesh.uv_layers.new(name='LegacyUV')
    for p in mesh.polygons:
        p.use_smooth = True
        for li in p.loop_indices:
            v = mesh.vertices[mesh.loops[li].vertex_index].co
            u, w = v.x / 2.5 + .5, v.y / 4 + .5
            uv.data[li].uv = (u, w)
            legacy.data[li].uv = (u, 1-w)
    obj['part3_rebuilt'] = True

def cylinder(vertices, faces, a, b, radius, segments=32):
    a, b = Vector(a), Vector(b)
    direction = (b-a).normalized()
    u = direction.orthogonal().normalized(); v = direction.cross(u)
    start = len(vertices)
    for c in (a, b):
        for i in range(segments):
            p = c + radius*(u*math.cos(i*math.tau/segments)+v*math.sin(i*math.tau/segments))
            vertices.append(tuple(p))
    for i in range(segments):
        j = (i+1)%segments
        faces.append((start+i,start+j,start+segments+j,start+segments+i))
    faces.append(tuple(start+i for i in reversed(range(segments))))
    faces.append(tuple(start+segments+i for i in range(segments)))

steel = bpy.data.materials['VD507B_ship_gms_axalp_main']
bs = next(n for n in steel.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
bs.inputs['Base Color'].default_value = (.24,.27,.28,1)
bs.inputs['Metallic'].default_value = .85
bs.inputs['Roughness'].default_value = .3
paint = bpy.data.materials.get('VD507B_NozzlePaint') or bpy.data.materials.new('VD507B_NozzlePaint')
paint.use_nodes = True
bs = next(n for n in paint.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
bs.inputs['Base Color'].default_value = (.16,.032,.022,1)
bs.inputs['Roughness'].default_value = .58

for side, suffix, sign, pitch in [('port','',-1,1.52),('starboard','.001',1,1.47)]:
    center = (sign*3.60,-64.65,-2.18)
    vertices, faces = [], []
    cylinder(vertices,faces,(0,-.31,0),(0,2.35,0),.14)
    cylinder(vertices,faces,(0,-.32,0),(0,.32,0),.215)
    for blade in range(4):
        start = len(vertices); rows, ring = 15, 16
        radii=[.18+.7198*i/(rows-1) for i in range(rows)]
        chords=[.075+.49*math.sin(math.pi*i/(rows-1))**.65 for i in range(rows)]
        chords[-1]=.025
        area=sum((chords[i]+chords[i+1])*.5*(radii[i+1]-radii[i]) for i in range(rows-1))
        chord_scale=(.62*math.pi*.9**2)/(4*area)
        for i in range(rows):
            t = i/(rows-1); r = .18 + .7198*t
            chord = chords[i]*chord_scale
            beta = math.atan(pitch/(math.tau*r))
            skew = .14*t*t
            angle = blade*math.pi/2 + sign*skew
            radial = Vector((math.cos(angle),0,math.sin(angle)))
            tangent = Vector((-math.sin(angle),0,math.cos(angle)))
            for j in range(ring):
                q = j*math.tau/ring
                c = chord*.5*math.cos(q)
                thickness = (.018*(1-t)+.002)*math.sin(q)
                p = radial*r + tangent*(c*math.cos(beta)-thickness*math.sin(beta))
                p.y = sign*(c*math.sin(beta)+thickness*math.cos(beta))
                vertices.append(tuple(p))
        for i in range(rows-1):
            for j in range(ring):
                k=(j+1)%ring
                faces.append((start+i*ring+j,start+i*ring+k,start+(i+1)*ring+k,start+(i+1)*ring+j))
        faces.append(tuple(start+j for j in reversed(range(ring))))
        faces.append(tuple(start+(rows-1)*ring+j for j in range(ring)))
    obj = bpy.data.objects['VD507B_lod_1_turbine1'+suffix]
    finish_mesh(obj,vertices,faces,center,steel)
    obj['blade_count']=4; obj['diameter_m']=1.8; obj['pitch_m']=pitch
    obj['expanded_area_ratio']=.62
    obj['geometry_confidence']='Diameter/blades/pitch sourced; blade sections, shaft axis and handedness approximate'

    vertices, faces = [], []
    # Closed rounded duct section, throat radius 0.92 m at the propeller plane.
    profile = [(-.83,.97),(-.80,.94),(-.66,.926),(-.4,.92),(0,.92),
               (.4,.932),(.68,.97),(.80,1.01),(.83,1.04),(.80,1.07),
               (.64,1.09),(.3,1.085),(-.1,1.06),(-.5,1.025),(-.77,.995)]
    segments=96
    for y,r in profile:
        for i in range(segments):
            a=i*math.tau/segments
            vertices.append((r*math.cos(a),y,r*math.sin(a)))
    for j in range(len(profile)):
        for i in range(segments):
            a=j*segments+i; b=j*segments+(i+1)%segments
            c=((j+1)%len(profile))*segments+(i+1)%segments
            d=((j+1)%len(profile))*segments+i
            faces.append((a,b,c,d))
    cylinder(vertices,faces,(0,0,1.01),(0,0,2.46),.13)
    # A thin closed stabilizer aft, kept outside the blade swept volume.
    start=len(vertices)
    vertices += [(x,y,z) for x in (-.035,.035) for y,z in [(-.83,-.96),(-1.16,-.94),(-1.16,.96),(-.83,.96)]]
    faces += [tuple(start+i for i in f) for f in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]]
    obj=bpy.data.objects['VD507B_rudder'+suffix]
    finish_mesh(obj,vertices,faces,center,paint)
    obj['throat_diameter_m']=1.84; obj['duct_length_m']=1.66
    obj['geometry_confidence']='Archive diameter interpreted as clear throat; duct profile, stock and stabilizer estimated'
    report['changes'].append({'side':side,'propeller_diameter_m':1.8,'blades':4,'pitch_m':pitch,
                              'nozzle_throat_m':1.84,'nozzle_length_m':1.66,'radial_clearance_m':.02})

scene['part3_geometry']=True
with open(SOURCE/'part3-geometry.json','w') as f: json.dump(report,f,indent=2)
print(json.dumps(report))
