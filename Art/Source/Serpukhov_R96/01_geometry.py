"""Run in Blender through the registered MCP. Original reconstruction, metres."""
import bpy, bmesh, math, json
from mathutils import Vector
from pathlib import Path

ROOT = Path('G:/Projects/ShipSim159')
SOURCE = ROOT / 'Art/Source/Serpukhov_R96'
if bpy.data.scenes.get('Serpukhov_R96'):
    raise RuntimeError('Reconstruction already exists; inspect before rebuilding.')
scene = bpy.data.scenes.new('Serpukhov_R96')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
root = bpy.data.collections.new('Serpukhov_R96')
scene.collection.children.link(root)
groups = {}
for name in ['Hull','Deck','Pushing_Equipment','Superstructure','Wheelhouse_Exterior',
             'Wheelhouse_Interior','Railings','Mooring_Equipment','Navigation_Equipment',
             'Waterjet_System','Interior','Collision_Reference','References','LODs']:
    c = bpy.data.collections.new(name)
    root.children.link(c)
    groups[name] = c

palette = {
 'Hull_Grey': ((.34,.39,.40,1),.25,.48),
 'Bottom_Charcoal': ((.075,.09,.092,1),.2,.58),
 'Deck_Oxide': ((.25,.13,.095,1),.15,.68),
 'House_Ivory': ((.80,.81,.75,1),.08,.48),
 'Frame_Cream': ((.64,.66,.60,1),.25,.4),
 'Steel_Dark': ((.095,.12,.125,1),.65,.42),
 'Rubber': ((.025,.029,.028,1),0,.8),
 'Glass': ((.39,.53,.55,.22),0,.12),
 'Wood': ((.24,.125,.055,1),0,.66),
 'Interior_Green': ((.17,.29,.25,1),.1,.55),
 'Instrument': ((.025,.033,.031,1),.25,.4),
 'Dial': ((.79,.77,.65,1),0,.5),
 'Life_Orange': ((.7,.16,.035,1),0,.55),
 'Light_Red': ((.65,.015,.008,1),0,.25),
 'Light_Green': ((.008,.45,.13,1),0,.25),
 'Light_White': ((.9,.82,.58,1),0,.18),
 'Wear': ((.23,.105,.045,1),.2,.85),
}
mats = {}
for name,(color,metal,rough) in palette.items():
    m = bpy.data.materials.new('R96_'+name)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value = color
    p.inputs['Metallic'].default_value = metal
    p.inputs['Roughness'].default_value = rough
    if name == 'Glass':
        p.inputs['Alpha'].default_value = .22
        p.inputs['Transmission Weight'].default_value = .3
    m.diffuse_color = color
    mats[name] = m

def move(obj,name,group,mat=None):
    obj.name = name
    for c in list(obj.users_collection): c.objects.unlink(obj)
    groups[group].objects.link(obj)
    if mat: obj.data.materials.append(mats[mat])
    return obj

def mesh(name,verts,faces,group,mat,bevel=0):
    data = bpy.data.meshes.new(name)
    data.from_pydata(verts,[],faces); data.update()
    obj = bpy.data.objects.new(name,data); groups[group].objects.link(obj)
    if mat: data.materials.append(mats[mat])
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(data); bm.free()
    if bevel:
        mod=obj.modifiers.new('Welded edge radius','BEVEL'); mod.width=bevel; mod.segments=2
    return obj

def box(name,loc,size,group,mat,bevel=.015):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=move(bpy.context.object,name,group,mat); o.dimensions=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        m=o.modifiers.new('Edge radius','BEVEL'); m.width=bevel; m.segments=2
    return o

def rod(name,a,b,r,group,mat,segments=12):
    a,b=Vector(a),Vector(b)
    bpy.ops.mesh.primitive_cylinder_add(vertices=segments,radius=r,depth=(b-a).length,location=(a+b)/2)
    o=move(bpy.context.object,name,group,mat)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    for f in o.data.polygons: f.use_smooth=len(f.vertices)==4
    return o

def tube(name,points,r,group,mat,sides=8):
    verts=[]
    for i,p in enumerate(points):
        tangent=Vector(points[min(i+1,len(points)-1)])-Vector(points[max(0,i-1)])
        q=tangent.to_track_quat('Z','Y')
        verts += [Vector(p)+q@Vector((r*math.cos(j*2*math.pi/sides),r*math.sin(j*2*math.pi/sides),0)) for j in range(sides)]
    faces=[tuple(range(sides-1,-1,-1))]
    for i in range(len(points)-1):
        for j in range(sides): faces.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
    faces.append(tuple(range((len(points)-1)*sides,len(points)*sides)))
    o=mesh(name,verts,faces,group,mat)
    for f in o.data.polygons: f.use_smooth=len(f.vertices)==4
    return o

def ring(name,center,r,minor,normal,group,mat):
    q=Vector(normal).to_track_quat('Z','Y')
    pts=[Vector(center)+q@Vector((r*math.cos(i*2*math.pi/32),r*math.sin(i*2*math.pi/32),0)) for i in range(33)]
    return tube(name,pts,minor,group,mat)

def prism(name,outline,z0,z1,group,mat,bevel=.015):
    n=len(outline); v=[(x,y,z) for z in (z0,z1) for x,y in outline]
    f=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,v,f,group,mat,bevel)

def yzprism(name,outline,x0,x1,group,mat,bevel=.01):
    n=len(outline); v=[(x,y,z) for x in (x0,x1) for y,z in outline]
    f=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,v,f,group,mat,bevel)

def apply_mods(obj):
    bpy.context.view_layer.objects.active=obj
    for m in list(obj.modifiers): bpy.ops.object.modifier_apply(modifier=m.name)

def cut(obj,cutter):
    apply_mods(obj)
    bpy.context.view_layer.objects.active=obj
    m=obj.modifiers.new('Opening','BOOLEAN'); m.operation='DIFFERENCE'; m.object=cutter
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(cutter,do_unlink=True)

# Plan half-breadths traced from original page 1, interpolated, not shipyard offsets.
stations=[(-10.65,.06,.84),(-10.5,.65,.72),(-10.1,1.3,.62),(-9.5,1.85,.50),
 (-8.7,2.18,.31),(-7.5,2.35,.12),(-6,2.43,.02),(-4.5,2.48,0),(-3,2.5,0),
 (-1,2.5,0),(1,2.5,0),(3,2.49,0),(5,2.46,0),(7,2.38,.02),(8.5,2.23,.06),
 (9.6,2.08,.13),(10.25,1.98,.24),(10.5,1.96,.38)]
verts=[]
for y,w,k in stations:
    # Flat floor, rounded short bilges, near-vertical topsides.
    section=[(-w,1.4),(-w*.998,.62+k*.25),(-w*.98,.22+k),(-w*.93,.06+k),
             (-w*.85,k),(0,k),(w*.85,k),(w*.93,.06+k),(w*.98,.22+k),(w*.998,.62+k*.25),(w,1.4)]
    verts += [(x,y,z) for x,z in section]
n=11; faces=[]
for i in range(len(stations)-1):
    for j in range(n-1): faces.append((i*n+j,(i+1)*n+j,(i+1)*n+j+1,i*n+j+1))
    faces.append((i*n,(i+1)*n,(i+1)*n+n-1,i*n+n-1))
faces += [tuple(range(n-1,-1,-1)),tuple(range((len(stations)-1)*n,len(stations)*n))]
hull=mesh('Hull_StationLoft',verts,faces,'Hull','Hull_Grey')
for f in hull.data.polygons: f.use_smooth=len(f.vertices)==4
hull.data.materials.append(mats['Bottom_Charcoal'])
for f in hull.data.polygons:
    if sum(hull.data.vertices[v].co.z for v in f.vertices)/len(f.vertices)<.57: f.material_index=1

# A genuine centreline intake/duct tunnel, enclosed by the remaining hull shell.
cutter=box('IntakeCut',(0,-8.4,.43),(1.05,4.6,1.05),'Hull',None,.1)
apply_mods(cutter); cut(hull,cutter)
outline=[(-w,y) for y,w,k in stations]+[(w,y) for y,w,k in reversed(stations)]
prism('MainDeck',outline,1.4,1.445,'Deck','Deck_Oxide')
fore=[(-2.5,1.0),(-2.49,3),(-2.46,5),(-2.38,7),(-2.23,8.5),(-1.96,10.5),
      (1.96,10.5),(2.23,8.5),(2.38,7),(2.46,5),(2.49,3),(2.5,1.0)]
forebody=prism('RaisedForecastle',fore,1.42,2.47,'Hull','Hull_Grey')
prism('ForecastleDeck',fore,2.47,2.53,'Deck','Deck_Oxide')
for s in (-1,1):
    for j,y in enumerate([1.55,2.12,3.55,4.12,6.1]):
        x=s*(2.5 if y<3 else 2.48 if y<5 else 2.42)
        cutter=rod('PortholeCut',(x-s*.3,y,2.03),(x+s*.3,y,2.03),.145,'Hull',None,24)
        cut(forebody,cutter)
        ring('Forecastle_PortholeRim', (x+s*.012,y,2.03),.155,.023,(s,0,0),'Hull','Frame_Cream')
        rod('Forecastle_PortholeGlass',(x-s*.035,y,2.03),(x-s*.025,y,2.03),.14,'Hull','Glass',24)
    tube('Main_RubbingStrake',[(s*(w+.035),y,1.29) for y,w,k in stations],.065,'Hull','Steel_Dark')
    tube('Forecastle_Gunwale',[(s*w,y,2.51) for y,w in [(1,2.5),(3,2.49),(5,2.46),(7,2.38),(8.5,2.23),(10.5,1.96)]],.06,'Hull','Steel_Dark')
    yzprism('Bow_Diagonal_Reinforcement',[(6.5,1.45),(10.45,.30),(10.45,1.0),(7.1,1.70)],s*1.97,s*2.05,'Hull','Hull_Grey')
    # Photograph-specific broad cheek and rounded upper contact face.
    yzprism('PushingKnee_'+str(s),[(8.55,2.5),(9.95,3.65),(10.72,3.65),(10.75,.28),(10.3,.28),(10.3,2.5)],s*1.87,s*2.07,'Pushing_Equipment','Hull_Grey',.045)
    box('Knee_ContactPad',(s*1.97,10.82,1.96),(.26,.16,3.35),'Pushing_Equipment','Rubber',.04)
    for z in (.55,1.1,1.7,2.3,2.9,3.4):
        rod('Knee_ContactBolt',(s*1.97,10.90,z),(s*1.97,10.915,z),.022,'Pushing_Equipment','Steel_Dark',8)
box('PushingCrossbeam',(0,10.38,2.4),(3.8,.36,.24),'Pushing_Equipment','Hull_Grey')
box('CouplingBed',(0,9.9,2.65),(1.0,.8,.22),'Pushing_Equipment','Steel_Dark')
for s in (-1,1): box('CouplingJaw',(s*.32,10.35,2.88),(.14,.85,.38),'Pushing_Equipment','Steel_Dark')
rod('CouplingPin',(-.5,10.45,2.9),(.5,10.45,2.9),.08,'Pushing_Equipment','Steel_Dark')

# Lower house and long low engine casing; upper bridge has an aft sloped face.
house=prism('LowerHouse',[(-1.65,-.6),(1.65,-.6),(1.65,4.75),(1.35,5.1),(-1.35,5.1),(-1.65,4.75)],1.46,3.6,'Superstructure','House_Ivory')
box('LowerHouseRoof',(0,1.85,3.63),(3.55,5.65,.12),'Superstructure','House_Ivory',.05)
yzprism('EngineCasing',[(-5.25,1.45),(-4.3,2.2),(-.6,2.43),(-.6,1.45)],-1.58,1.58,'Superstructure','House_Ivory',.04)
for s in (-1,1):
    for y in (-3.9,-2.5):
        ring('Engine_Porthole',(s*1.60,y,1.9),.135,.025,(s,0,0),'Superstructure','Frame_Cream')
        rod('Engine_PortholeGlass',(s*1.60,y,1.9),(s*1.61,y,1.9),.122,'Superstructure','Glass',24)
    for z in (1.7,2.05,2.4,2.78,3.18,3.5):
        box('House_ExternalStiffener',(s*1.674,1.9,z),(.04,4.9,.034),'Superstructure','House_Ivory',.009)
    # Two real door openings, glazed leaves, frames and handles.
    for y in (-.05,2.45):
        cut(house,box('DoorCut',(s*1.65,y,2.42),(.5,.75,1.83),'Superstructure',None,.055))
        door=box('AccessDoor',(s*1.66,y,2.42),(.055,.71,1.77),'Superstructure','House_Ivory',.04)
        cut(door,box('DoorWindowCut',(s*1.66,y,2.91),(.3,.47,.48),'Superstructure',None,.04))
        box('DoorGlass',(s*1.67,y,2.91),(.012,.46,.47),'Superstructure','Glass',.035)
        for yy in (y-.39,y+.39): box('DoorJamb',(s*1.7,yy,2.42),(.065,.045,1.89),'Superstructure','Frame_Cream')
        box('DoorHeader',(s*1.7,y,3.36),(.065,.82,.045),'Superstructure','Frame_Cream')
        rod('DoorHandle',(s*1.73,y+.22,2.36),(s*1.73,y+.22,2.52),.018,'Superstructure','Steel_Dark')
    for y in (.85,1.55):
        cut(house,box('HouseWindowCut',(s*1.65,y,2.99),(.5,.5,.48),'Superstructure',None,.045))
        box('HouseWindowGlass',(s*1.66,y,2.99),(.015,.49,.47),'Superstructure','Glass',.035)
    # Bridge access staircase beside the forward doorway.
    for j in range(7):
        box('BridgeStairTread',(s*1.98,2.7+j*.18,2.54+j*.16),(.57,.25,.065),'Superstructure','Deck_Oxide')
    for x in (s*1.72,s*2.23):
        rod('StairStringer',(x,2.57,2.47),(x,3.9,3.62),.035,'Superstructure','Steel_Dark')
        rod('StairHandrail',(x,2.57,3.25),(x,3.9,4.4),.023,'Railings','House_Ivory')

# Wheelhouse walls are pierced meshes with true glazing, not dark rectangles.
bridge=yzprism('BridgeShell',[(2.25,3.66),(5.0,3.66),(5.0,5.65),(2.93,5.65)],-1.57,1.57,'Wheelhouse_Exterior','House_Ivory',.045)
cut(bridge,yzprism('BridgeVoid',[(2.4,3.74),(4.90,3.74),(4.90,5.72),(3.02,5.72)],-1.47,1.47,'Wheelhouse_Exterior',None,0))
for s in (-1,1):
    for i,y in enumerate((3.24,3.91,4.58)):
        cut(bridge,box('BridgeWindowCut',(s*1.57,y,5.12),(.45,.56,.93),'Wheelhouse_Exterior',None,.07))
        box('BridgeSideGlass',(s*1.575,y,5.12),(.012,.55,.92),'Wheelhouse_Exterior','Glass',.065)
        for yy in (y-.293,y+.293): box('BridgeMullion',(s*1.59,yy,5.12),(.04,.035,.94),'Wheelhouse_Exterior','Frame_Cream')
        for z in (4.64,5.60): box('BridgeFrame',(s*1.59,y,z),(.04,.59,.035),'Wheelhouse_Exterior','Frame_Cream')
for x in (-.98,0,.98):
    cut(bridge,box('FrontWindowCut',(x,5.0,5.12),(.85,.5,.93),'Wheelhouse_Exterior',None,.07))
    box('BridgeFrontGlass',(x,5.005,5.12),(.84,.012,.92),'Wheelhouse_Exterior','Glass',.065)
    for xx in (x-.44,x+.44): box('FrontMullion',(xx,5.025,5.12),(.036,.04,.95),'Wheelhouse_Exterior','Frame_Cream')
    for z in (4.64,5.60): box('FrontFrame',(x,5.025,z),(.89,.04,.035),'Wheelhouse_Exterior','Frame_Cream')
    rod('WindowWiper',(x-.25,5.05,4.68),(x+.12,5.05,5.32),.012,'Wheelhouse_Exterior','Steel_Dark',8)
# Rear glazing follows the inclined plate.
for x in (-.98,0,.98):
    cutter=box('RearWindowCut',(x,2.75,5.12),(.84,1.2,.9),'Wheelhouse_Exterior',None,.045)
    cut(bridge,cutter)
    o=box('RearGlass',(x,2.75,5.12),(.83,.012,.91),'Wheelhouse_Exterior','Glass',.035)
    o.rotation_euler.x=math.atan(.68/1.99)
for z in (3.92,4.23,4.53):
    y=2.25+(z-3.66)*.68/1.99
    box('BridgeRearStiffener',(0,y-.025,z),(3.16,.04,.035),'Wheelhouse_Exterior','House_Ivory')
box('BridgeRoof',(0,3.99,5.72),(3.67,2.92,.12),'Wheelhouse_Exterior','House_Ivory',.07)
box('BridgeFloor',(0,3.78,3.69),(3.1,2.48,.07),'Wheelhouse_Interior','Wood')
box('BridgeCeiling',(0,4.0,5.63),(3.08,2.5,.04),'Wheelhouse_Interior','House_Ivory')
for j in range(12): box('FloorboardSeam',(-1.38+j*.25,3.81,3.73),(.008,2.25,.004),'Wheelhouse_Interior','Steel_Dark',0)

def label(name,text,loc,rotation,size,group='Wheelhouse_Exterior',mat='Steel_Dark'):
    curve=bpy.data.curves.new(name,'FONT'); curve.body=text; curve.size=size; curve.align_x='CENTER'
    font=Path('C:/Windows/Fonts/arialbd.ttf')
    if font.exists(): curve.font=bpy.data.fonts.load(str(font))
    curve.extrude=.0008; curve.resolution_u=4
    obj=bpy.data.objects.new(name,curve); groups[group].objects.link(obj)
    obj.location=loc; obj.rotation_euler=rotation; curve.materials.append(mats[mat])
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH')
    return bpy.context.object
label('Name_Front','СЕРПУХОВ',(0,5.065,4.31),(math.pi/2,0,math.pi),.29)
for s in (-1,1):
    label('Name_Side','СЕРПУХОВ',(s*1.625,3.92,4.31),(math.pi/2,0,s*math.pi/2),.25)

# Preserve a readable original numeric station record with the source.
(SOURCE/'measurements.json').write_text(json.dumps({'units':'metres','baseline_to_waterline':.62,
 'stations_estimated_from_general_arrangement':stations,'bridge_floor_above_baseline':3.6,
 'draft':.62,'loaded_mass_kg':45230},indent=2))
ref=bpy.data.objects.new('R96_Page1_PrivateReference',None); groups['References'].objects.link(ref)
ref.empty_display_type='IMAGE'
ref.data=bpy.data.images.load(str(ROOT/'Logs/Serpukhov_R96/References/p96-page1.png'))
ref.empty_display_size=26.4; ref.color[3]=.35
ref.location=(-3,0,3); ref.rotation_euler=(math.pi/2,0,math.pi/2)
ref.hide_render=True; ref.hide_viewport=True
scene['historical_status']='Evidence-based reconstruction; inferred offsets and colors, see README.'
scene['source_phase']='geometry'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Serpukhov_R96.blend'))
print('R96_GEOMETRY',len(scene.objects),'objects')
