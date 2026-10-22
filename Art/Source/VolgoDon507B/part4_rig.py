"""Separate retained mechanisms and author a review-only mechanical sequence."""
import bpy
import math
import json
from collections import defaultdict
from pathlib import Path
from mathutils import Vector, Matrix

ROOT=Path('G:/Projects/ShipSim159')
SOURCE=ROOT/'Art/Source/VolgoDon507B'
TARGET=ROOT/'Assets/ShipSimulator/Models/VolgoDon507B_Improved'
scene=bpy.data.scenes['VolgoDon507B_Source']
assert not scene.get('part4_rig'), 'Use before-part4.blend to rebuild'
scene.frame_set(1)
collection=bpy.data.collections.new('VD507B_Mechanisms');scene.collection.children.link(collection)
manifest={'version':1,'stage':4,'source_axes':'+X starboard, +Y bow, +Z up','units':'metres, degrees',
          'reference':'doc257.pdf pp.4,7,8','mechanisms':[], 'states':[]}

def empty(name,position,parent=None):
    o=bpy.data.objects.new(name,None);collection.objects.link(o)
    o.empty_display_size=.3;o.location=position
    if parent:o.parent=parent
    return o

root=empty('Mechanisms',(0,0,0))

def parent_keep(o,parent):
    world=o.matrix_world.copy();o.parent=parent;o.matrix_parent_inverse=Matrix.Identity(4)
    o.matrix_world=world
    bpy.context.view_layer.update()

def subset(o,indices,name):
    old=o.data; indices=sorted(indices)
    verts=[];faces=[];normals=[];uvs=[[] for layer in old.uv_layers];mats=[]
    for index in indices:
        p=old.polygons[index];face=[]
        for li in p.loop_indices:
            face.append(len(verts));verts.append(tuple(old.vertices[old.loops[li].vertex_index].co))
            normals.append(tuple(old.corner_normals[li].vector))
            for j,layer in enumerate(old.uv_layers):uvs[j].append(tuple(layer.data[li].uv))
        faces.append(face);mats.append(p.material_index)
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    for mat in old.materials:mesh.materials.append(mat)
    for j,layer in enumerate(old.uv_layers):
        dst=mesh.uv_layers.new(name=layer.name)
        for li,uv in enumerate(uvs[j]):dst.data[li].uv=uv
    for p,mi in zip(mesh.polygons,mats):p.material_index=mi;p.use_smooth=True
    mesh.normals_split_custom_set(normals)
    result=bpy.data.objects.new(name,mesh);collection.objects.link(result)
    result.matrix_world=o.matrix_world.copy();return result

def partition(o,groups):
    used=set(); result={}
    for name,ids in groups.items():
        ids=set(ids);assert not used.intersection(ids);used.update(ids)
        if ids:result[name]=subset(o,ids,name)
    rest=set(range(len(o.data.polygons)))-used
    if rest:
        new=subset(o,rest,o.name+'_remainder');o.data=new.data;bpy.data.objects.remove(new,do_unlink=True)
    else:bpy.data.objects.remove(o,do_unlink=True)
    return result

def bounds(o):
    ps=[o.matrix_world@v.co for v in o.data.vertices]
    return [Vector([fn(p[k] for p in ps) for k in range(3)]) for fn in (min,max)]

def register(o,kind,axis,limits,binding,confidence):
    manifest['mechanisms'].append({'name':o.name,'path':'Mechanisms/'+o.name,'kind':kind,
        'source_axis':axis,'rest_position':list(o.location),'limits':limits,
        'binding':binding,'confidence':confidence})

for side,suffix,sign in [('Port','',-1),('Starboard','.001',1)]:
    prop=bpy.data.objects['VD507B_lod_1_turbine1'+suffix]
    pivot=empty('Propeller_'+side,tuple(prop.location),root);bpy.context.view_layer.update()
    parent_keep(prop,pivot);prop.name='PropellerMesh_'+side
    register(pivot,'rotation',[0,1,0],{'continuous':True},'shaft_angle_'+side.lower(),
             'Shaft center from Part 3; ahead handedness must be calibrated in Part 5')
    nozzle=bpy.data.objects['VD507B_rudder'+suffix]
    pivot=empty('Nozzle_'+side,(sign*3.6,-64.65,.28),root);bpy.context.view_layer.update()
    parent_keep(nozzle,pivot);nozzle.name='NozzleMesh_'+side
    register(pivot,'rotation',[0,0,1],{'minimum_deg':-35,'maximum_deg':35},'nozzle_angle_'+side.lower(),
             'Page 7 travel; stock position estimated')

panel_ids={
 'AftOuter':[2,3,28,29,30,31,40,41,104,105,122,123,124,125,126,127],
 'AftInner':[14,15,16,17,26,27,34,35,42,43,44,45,54,55,106,107],
 'ForeInner':[18,19,36,37,70,71,74,75,76,77,78,79,108,109,114,115],
 'ForeOuter':[0,1,20,21,22,23,24,25,82,83,88,89,94,95,96,97,110,111,116,117,118,119,120,121]}
panels=partition(bpy.data.objects['VD507B_hold'],{'Panel_'+k:v for k,v in panel_ids.items()})
rigs={}; ranges={}
for key in panel_ids:
    mesh=panels['Panel_'+key];lo,hi=bounds(mesh);cy=(lo.y+hi.y)*.5
    pivot=empty('Hatch_'+key,(0,cy,2.65),root);bpy.context.view_layer.update()
    parent_keep(mesh,pivot);rigs[key]=pivot;ranges[key]=(lo.y,hi.y)
    register(pivot,'lift_and_slide',[0,1,0],{'lift_max_m':1.05,'travel_estimated':True},'hatch_sequence',
             'Page 4 sequence; measured legacy panel lengths and estimated strokes')

detail=bpy.data.objects['VD507B_hold_detali'];groups={}
for p in detail.data.polygons:
    c=detail.matrix_world@p.center
    key='AftOuter' if c.y<-17.6 else 'AftInner' if c.y<6 else 'ForeInner' if c.y<32.9 else 'ForeOuter'
    # Cover handles and inner rolling gear travel with their panel; jack bases stay fixed.
    if p.index<2592 or p.index>=3552:groups.setdefault('PanelDetails_'+key,[]).append(p.index)
    elif (p.index-2592)%80>=60:groups.setdefault('JackHeads_'+key,[]).append(p.index)
for name,obj in partition(detail,groups).items():
    key=name.split('_',1)[1]
    if name.startswith('PanelDetails'):parent_keep(obj,rigs[key])
    else:
        pivot=empty('JackLift_'+key,(0,0,0),root);parent_keep(obj,pivot)
        register(pivot,'lift',[0,0,1],{'minimum_m':0,'maximum_m':1.05},'outer_panel_lift',
                 'Existing visible jack heads; fixed pedestals retained')

# The legacy transverse cover seals were in the deck material mesh.
deck=bpy.data.objects['VD507B_deck'];seams={}
for p in deck.data.polygons:
    ps=[deck.matrix_world@deck.data.vertices[i].co for i in p.vertices]
    if all(abs(v.x)<5.84 and 2.60<v.z<2.86 for v in ps):
        if all(-17.78<v.y<-17.44 for v in ps):seams.setdefault('Seal_AftInner',[]).append(p.index)
        if all(32.74<v.y<33.08 for v in ps):seams.setdefault('Seal_ForeOuter',[]).append(p.index)
for name,obj in partition(deck,seams).items():parent_keep(obj,rigs[name.split('_')[1]])

# Cut real apertures in the legacy unbroken deck, interpolating both UV layers.
def clip(poly,axis,value,positive):
    result=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        da=(a[0][axis]-value)*(1 if positive else -1);db=(b[0][axis]-value)*(1 if positive else -1)
        if da>=-1e-7:result.append(a)
        if (da>0 and db<0) or (da<0 and db>0):
            t=da/(da-db);result.append(tuple(x.lerp(y,t) for x,y in zip(a,b)))
    return result

apertures=[(-5.72,5.72,-40.80,3.82),(-5.72,5.72,10.09,53.35)]
verts=[];faces=[];uv0=[];uv1=[];norms=[];materials=[]
for p in deck.data.polygons:
    poly=[(deck.matrix_world@deck.data.vertices[deck.data.loops[li].vertex_index].co,
           deck.data.uv_layers[0].data[li].uv.copy(),deck.data.uv_layers[1].data[li].uv.copy(),
           deck.data.corner_normals[li].vector.copy()) for li in p.loop_indices]
    pieces=[poly]
    if all(1.96<item[0].z<2.01 for item in poly):
        for xmin,xmax,ymin,ymax in apertures:
            kept=[]
            for piece in pieces:
                inside=piece
                for axis,value,pos in [(0,xmin,True),(0,xmax,False),(1,ymin,True),(1,ymax,False)]:
                    if not inside:break
                    outside=clip(inside,axis,value,not pos)
                    if len(outside)>=3:kept.append(outside)
                    inside=clip(inside,axis,value,pos)
            pieces=kept
    for piece in pieces:
        for i in range(1,len(piece)-1):
            tri=[piece[0],piece[i],piece[i+1]]
            if (tri[1][0]-tri[0][0]).cross(tri[2][0]-tri[0][0]).length<1e-8:continue
            face=[]
            for pos,a,b,n in tri:
                face.append(len(verts));verts.append(tuple(deck.matrix_world.inverted()@pos))
                uv0.append(a);uv1.append(b);norms.append(n)
            faces.append(face);materials.append(p.material_index)
old=deck.data;mesh=bpy.data.meshes.new('DeckWithCargoApertures');mesh.from_pydata(verts,[],faces);mesh.update()
for m in old.materials:mesh.materials.append(m)
for name,values in [('SurfaceUV',uv0),('LegacyUV',uv1)]:
    layer=mesh.uv_layers.new(name=name)
    for li,uv in enumerate(values):layer.data[li].uv=uv
for p,mi in zip(mesh.polygons,materials):p.material_index=mi;p.use_smooth=True
mesh.normals_split_custom_set(norms);deck.data=mesh

def simple_mesh(name,verts,faces,mat):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);collection.objects.link(obj);mesh.materials.append(mat)
    a=mesh.uv_layers.new(name='SurfaceUV');b=mesh.uv_layers.new(name='LegacyUV')
    for li,loop in enumerate(mesh.loops):
        p=mesh.vertices[loop.vertex_index].co;u,v=p.x*.2,p.y*.2
        a.data[li].uv=(u,v);b.data[li].uv=(u,1-v)
    obj.parent=root;return obj

def box(name,lo,hi,mat):
    vs=[(x,y,z) for x in (lo[0],hi[0]) for y in (lo[1],hi[1]) for z in (lo[2],hi[2])]
    fs=[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)]
    return simple_mesh(name,vs,fs,mat)

mat=bpy.data.materials['VD507B_NozzlePaint']
# Lower inner surfaces make opening a cover reveal a hold, not the ocean or a deck cap.
for label,(_,_,a,b) in zip(['Aft','Fore'],apertures):
    box('HoldFloor_'+label,(-5.85,a,-2.64),(5.85,b,-2.60),mat)
    for side in (-1,1):box('HoldLining_'+label+str(side),(side*5.72-.03,a,-2.6),(side*5.72+.03,b,2.60),mat)
    for end,y in enumerate([a,b]):box('HoldEnd_'+label+str(end),(-5.72,y-.03,-2.6),(5.72,y+.03,2.60),mat)
for side in (-1,1):box('HatchRail_'+str(side),(side*6.0-.045,-40.9,2.84),(side*6.0+.045,53.48,2.89),mat)

for key,ys in [('AftOuter',[-37.54,-29.52,-21.53]),('ForeOuter',[34.31,42.33,50.32])]:
    vertices=[];faces=[]
    for side in (-1,1):
        for y in ys:
            start=len(vertices);n=16
            for z in (0,.31):
                vertices += [(side*6.45+.045*math.cos(i*math.tau/n),y+.045*math.sin(i*math.tau/n),z) for i in range(n)]
            for i in range(n):faces.append((start+i,start+(i+1)%n,start+n+(i+1)%n,start+n+i))
            faces.append(tuple(start+i for i in reversed(range(n))))
            faces.append(tuple(start+n+i for i in range(n)))
    rod=simple_mesh('JackRods_'+key,vertices,faces,bpy.data.materials['VD507B_ship_gms_axalp_main'])
    rod.location.z=2.65

detail=bpy.data.objects['VD507B_detali']
anchors=partition(detail,{'AnchorMesh_Stern':range(7179,7334),'AnchorMesh_Port':range(7334,7489),
                          'AnchorMesh_Starboard':range(7489,7644)})
for side,obj in [(n.split('_')[1],o) for n,o in anchors.items()]:
    lo,hi=bounds(obj);pivot=empty('Anchor_'+side,((lo.x+hi.x)/2,(lo.y+hi.y)/2,hi.z),root)
    bpy.context.view_layer.update();parent_keep(obj,pivot)
    register(pivot,'unseat',[0,0,-1],{'unseat_m':.35,'payout_enabled':False},'anchor_unseat_'+side.lower(),
             'Original anchor separated; payout chain routing/catenary deliberately deferred')
    empty('ChainAttachment_'+side,(0,0,0),pivot)

# Identify the inherited circular windlass cheeks without rotating their pedestals.
detail=bpy.data.objects['VD507B_detali'];mesh=detail.data;at=defaultdict(list)
for p in mesh.polygons:
    for vi in p.vertices:at[tuple(round(v,4) for v in mesh.vertices[vi].co)].append(p.index)
adj=[set() for p in mesh.polygons]
for ids in at.values():
    for i in ids:adj[i].update(ids)
remaining=set(range(len(mesh.polygons)));groups={}
while remaining:
    ids={remaining.pop()};todo=list(ids)
    while todo:
        for i in adj[todo.pop()]:
            if i in remaining:remaining.remove(i);ids.add(i);todo.append(i)
    if len(ids)!=300:continue
    ps=[detail.matrix_world@mesh.vertices[vi].co for i in ids for vi in mesh.polygons[i].vertices]
    if min(p.y for p in ps)>56 and max(p.y for p in ps)<59:
        side='Port' if ps[0].x<0 else 'Starboard'
        groups.setdefault('WindlassMesh_'+side,[]).extend(ids)
assert all(len(v)==600 for v in groups.values()) and len(groups)==2
for name,obj in partition(detail,groups).items():
    lo,hi=bounds(obj);pivot=empty(name.replace('Mesh',''),tuple((lo+hi)*.5),root)
    bpy.context.view_layer.update();parent_keep(obj,pivot)
    register(pivot,'rotation',[1,0,0],{'continuous':True},'anchor_windlass_angle',
             'Retained circular drive cheeks; gearing and chain speed uncalibrated')

rest={o.name:o.location.copy() for o in root.children}
manifest['apertures']=apertures
manifest['panel_ranges']={k:list(v) for k,v in ranges.items()}
manifest['estimated_strokes']={'outer_lift_m':1.05,'inner_rolling_lift_m':.25,'outer_stacked_lift_m':.93}
manifest['derived_geometry']={'JackRods_AftOuter':'scale Z = 1 + JackLift_AftOuter lift / 0.31',
                              'JackRods_ForeOuter':'scale Z = 1 + JackLift_ForeOuter lift / 0.31'}
manifest['anchor_limit']='Unseat preview only; no seabed deployment or chain payout is claimed'
manifest['hydraulics_limit']='Twelve inherited jack pedestals; archive lists sixteen vertical and four horizontal jacks. Unlocated missing units are not invented.'

def pose(frame,offsets=None,rotations=None):
    offsets=offsets or {};rotations=rotations or {}
    for o in root.children:
        if o.name in rest:
            o.location=rest[o.name]+Vector(offsets.get(o.name,(0,0,0)))
            o.rotation_euler=rotations.get(o.name,(0,0,0))
            if o.name.startswith(('Propeller_','Nozzle_','Hatch_','JackLift_','Anchor_','Windlass_')):
                o.keyframe_insert('location',frame=frame);o.keyframe_insert('rotation_euler',frame=frame)
            if o.name.startswith('JackRods_'):
                lift=offsets.get(o.name.replace('JackRods_','JackLift_'),(0,0,0))[2]
                o.scale.z=1+lift/.31;o.keyframe_insert('scale',frame=frame)

cy={k:rigs[k].location.y for k in rigs}
stack_fore=41.99
lift={'Hatch_AftOuter':(0,0,1.05),'Hatch_ForeOuter':(0,stack_fore-cy['ForeOuter'],1.05),
      'JackLift_AftOuter':(0,0,1.05),'JackLift_ForeOuter':(0,0,1.05)}
rolling=dict(lift,Hatch_AftInner=(0,0,.25),Hatch_ForeInner=(0,0,.25))
stack=dict(rolling,Hatch_AftInner=(0,cy['AftOuter']-cy['AftInner'],.25),
           Hatch_ForeInner=(0,stack_fore-cy['ForeInner'],.25))
both=dict(stack,Hatch_AftOuter=(0,0,.93),Hatch_ForeOuter=(0,stack_fore-cy['ForeOuter'],.93),
          JackLift_AftOuter=(0,0,0),JackLift_ForeOuter=(0,0,0))
fore_open=dict(both)
for k in ['ForeInner','ForeOuter']:fore_open['Hatch_'+k]=(0,-6.10-cy[k],both['Hatch_'+k][2])
aft_open=dict(both)
for k in ['AftInner','AftOuter']:aft_open['Hatch_'+k]=(0,18.95-cy[k],both['Hatch_'+k][2])
poses=[(1,'closed',{}),(100,'lift_outer',lift),(120,'release_inner',rolling),(180,'slide_under',stack),
       (210,'both_half_open',both),(300,'fore_open',fore_open),(390,'both_half_open_return',both),
       (480,'aft_open',aft_open),(570,'both_half_open_again',both),(600,'lift_to_unstack',stack),
       (660,'slide_closed',rolling),(680,'lower_inner',lift),(710,'closed_return',{})]
for frame,name,offset in poses:
    pose(frame,offset);manifest['states'].append({'name':name,'frame':frame,'offsets':offset})
for frame,angle in [(1,0),(30,-35),(60,35),(90,0)]:
    pose(frame,rotations={'Nozzle_Port':(0,0,math.radians(angle)),
                         'Nozzle_Starboard':(0,0,-math.radians(angle)),
                         'Propeller_Port':(0,math.radians((frame-1)*8),0),
                         'Propeller_Starboard':(0,-math.radians((frame-1)*8),0)})
anchor_offsets={'Anchor_Port':(-.22,.14,-.23),'Anchor_Starboard':(.22,.14,-.23),'Anchor_Stern':(0,-.25,-.24)}
pose(740,anchor_offsets,{'Windlass_Port':(.5,0,0),'Windlass_Starboard':(-.5,0,0)});pose(770,{})
manifest['states'] += [{'name':'anchors_unseated','frame':740,'offsets':anchor_offsets},
                       {'name':'nozzles_port_limit','frame':30},{'name':'nozzles_starboard_limit','frame':60}]
for o in root.children:
    if not o.animation_data or not o.animation_data.action:continue
    action=o.animation_data.action
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    for key in curve.keyframe_points:key.interpolation='LINEAR'
scene.frame_start=1;scene.frame_end=770;scene.frame_set(1)
scene['part4_rig']=True
with open(SOURCE/'mechanisms.json','w') as f:json.dump(manifest,f,indent=2)
with open(TARGET/'mechanisms.json','w') as f:json.dump(manifest,f,indent=2)
print('Rig prepared',len(manifest['mechanisms']),'bindings')
