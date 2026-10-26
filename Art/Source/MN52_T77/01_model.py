"""Run only through Blender MCP. Photo-derived MN-52, not surveyed offsets."""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector
ROOT=Path('G:/Projects/ShipSim159')
SOURCE=ROOT/'Art/Source/MN52_T77'
MODEL=ROOT/'Assets/ShipSimulator/Models/MN52_T77'
SOURCE.mkdir(parents=True,exist_ok=True)
(SOURCE/'Previews').mkdir(exist_ok=True)
(MODEL/'Textures').mkdir(parents=True,exist_ok=True)
if bpy.data.scenes.get('MN52_T77'): raise RuntimeError('MN52 scene already exists; edit it instead of duplicating')
scene=bpy.data.scenes.new('MN52_T77'); bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1
root=bpy.data.collections.new('MN52');scene.collection.children.link(root)
groups={}
for name in ['Hull','MainDeck','Superstructure','Wheelhouse','DeckEquipment','TankerPiping','Railings','MastsAndVents','MooringEquipment','Markings','Collision','PhysicsReferencePoints','LODs','Review']:
 c=bpy.data.collections.new('MN52_'+name);root.children.link(c);groups[name]=c
mats={}
palette={'Hull':(.50,.54,.53,1),'LowerHull':(.22,.075,.055,1),'Deck':(.20,.23,.22,1),'House':(.78,.79,.73,1),'Pipe':(.53,.58,.57,1),'Metal':(.13,.16,.17,1),'Rubber':(.023,.029,.028,1),'Orange':(.66,.13,.025,1),'Letter':(.022,.03,.026,1),'Glass':(.36,.49,.50,.25),'Wear':(.22,.115,.06,1)}
for name,color in palette.items():
 m=bpy.data.materials.new('MN52_'+name);m.diffuse_color=color;m.use_nodes=True
 p=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');p.inputs['Base Color'].default_value=color;p.inputs['Roughness'].default_value=.68
 p.inputs['Metallic'].default_value=.25 if name in ('Metal','Pipe') else .05
 if name=='Glass':p.inputs['Alpha'].default_value=.25;p.inputs['Roughness'].default_value=.17;m.surface_render_method='BLENDED'
 mats[name]=m

def finish(o,name,group,mat,bevel=0):
 o.name='MN52_'+name
 for c in list(o.users_collection):c.objects.unlink(o)
 groups[group].objects.link(o);o.data.materials.append(mats[mat])
 if bevel:
  mod=o.modifiers.new('Steel edge','BEVEL');mod.width=bevel;mod.segments=2
  bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return o
def box(name,loc,size,group='DeckEquipment',mat='House',bevel=.025):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 return finish(o,name,group,mat,bevel)
def rod(name,a,b,r=.025,group='Railings',mat='House',vertices=10):
 a,b=Vector(a),Vector(b);d=b-a
 bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=d.length,location=(a+b)*.5)
 o=bpy.context.object;o.rotation_euler=d.to_track_quat('Z','Y').to_euler()
 return finish(o,name,group,mat)
def path(name,points,r=.06,group='TankerPiping',mat='Pipe'):
 curve=bpy.data.curves.new('Pipe route','CURVE');curve.dimensions='3D';curve.resolution_u=2;curve.bevel_depth=r;curve.bevel_resolution=2
 s=curve.splines.new('POLY');s.points.add(len(points)-1)
 for p,co in zip(s.points,points):p.co=(*co,1)
 o=bpy.data.objects.new('Route',curve);groups[group].objects.link(o)
 bpy.context.view_layer.objects.active=o;o.select_set(True);bpy.ops.object.convert(target='MESH');o=bpy.context.object
 return finish(o,name,group,mat)
def ring(name,loc,major,minor,group='DeckEquipment',mat='Metal',rotation=(0,0,0)):
 bpy.ops.mesh.primitive_torus_add(major_radius=major,minor_radius=minor,major_segments=24,minor_segments=8,location=loc,rotation=rotation)
 return finish(bpy.context.object,name,group,mat)
def text(name,body,loc,size,rotation):
 bpy.ops.object.text_add(location=loc,rotation=rotation);o=bpy.context.object;o.data.body=body;o.data.size=size;o.data.align_x='CENTER';o.data.extrude=.001
 o.data.font=bpy.data.fonts.load('C:/Windows/Fonts/arialbd.ttf');bpy.ops.object.convert(target='MESH')
 return finish(bpy.context.object,name,'Markings','Letter')

# Flat middle bottom, broad transoms and rising spoon ends, inferred from 89189.
sections=[(-14.4,2.38,1.03),(-14.1,2.65,.72),(-13.5,2.84,.39),(-12.5,2.96,.16),(-11,3,0),(-9,3,0),(-6,3,0),(-3,3,0),(0,3,0),(3,3,0),(6,3,0),(9,3,0),(11,3,.06),(12.5,2.96,.24),(13.5,2.84,.60),(14.1,2.66,.94),(14.4,2.44,1.20)]
verts=[];faces=[]
for y,w,b in sections:
 for x,z in [(-w,1.5),(-w, max(b+.16, .30)),(-w+.18,b),(w-.18,b),(w,max(b+.16,.30)),(w,1.5)]:verts.append((x,y,z))
for i in range(len(sections)-1):
 for j in range(6):faces.append((i*6+j,i*6+(j+1)%6,(i+1)*6+(j+1)%6,(i+1)*6+j))
faces.extend([tuple(range(5,-1,-1)),tuple((len(sections)-1)*6+j for j in range(6))])
mesh=bpy.data.meshes.new('Lofted spoon hull');mesh.from_pydata(verts,[],faces);mesh.update()
o=bpy.data.objects.new('Hull',mesh);groups['Hull'].objects.link(o);finish(o,'Hull','Hull','Hull',.025)
o.data.materials.append(mats['LowerHull'])
for p in o.data.polygons:
 if p.center.z<.68:p.material_index=1
box('WorkingDeck',(0,0,1.51),(5.7,25.4,.07),'MainDeck','Deck')
for s in [-1,1]:
 path('SideRubbingStrake',[(s*(w+.035),y,1.22) for y,w,b in sections],.085,'Hull','Hull')
 path('SheerCap',[(s*w,y,1.54) for y,w,b in sections],.055,'Hull','House')
 for y in range(-10,12,2):box('SideStiffener',(s*3.015,y,1.0),(.035,.055,.94),'Hull','Hull',.01)
 # Forward and after end bulwarks, leaving the long working deck open.
 box('ForwardBulwark',(s*2.78,11.9,1.92),(.10,3.6,.76),'Hull','Hull')
 box('AfterBulwark',(s*2.78,-12.7,1.83),(.10,2.0,.58),'Hull','Hull')
box('ForwardTransomBulwark',(0,14.25,1.98),(4.9,.12,.80),'Hull','Hull')
box('AfterTransomBulwark',(0,-14.25,1.86),(4.9,.12,.56),'Hull','Hull')

# The exact vessel has a raised cabin on open diagonal framing, not a modern tug bridge.
box('CabinFloor',(0,-8.5,3.45),(5.48,6.15,.16),'Superstructure','Deck')
box('LowerServiceRoom',(0,-9,2.46),(1.9,3.7,1.8),'Superstructure','House')
for x in [-2.48,2.48]:
 for y in [-11.35,-8.5,-5.65]:
  rod('CabinColumn',(x,y,1.55),(x,y,3.4),.065,'Superstructure','House')
 for ya,yb in [(-11.3,-8.5),(-8.5,-5.7)]:
  rod('CabinDiagonal',(x,ya,1.55),(x,yb,3.4),.055,'Superstructure','House')
for y in [-11.35,-5.65]:
 rod('CrossBrace',(-2.48,y,1.55),(-.9,y,3.4),.055,'Superstructure','House')
 rod('CrossBrace',(2.48,y,1.55),(.9,y,3.4),.055,'Superstructure','House')
box('CabinRoof',(0,-8.5,5.74),(5.6,6.25,.14),'Superstructure','House')

# Panel-built walls leave real openings; windows are separate glass, gaskets and frames.
def wall(name,axis,coord,lo,hi,openings):
 z0,z1=3.53,5.67
 cuts=sorted(set([lo,hi]+[v for c,w,h,zc in openings for v in [c-w/2,c+w/2]]))
 def panel(c,w,z,h):
  loc=(coord,c,z) if axis=='x' else (c,coord,z);size=(.08,w,h) if axis=='x' else (w,.08,h)
  box(name,loc,size,'Superstructure','House',.008)
 for a,b in zip(cuts,cuts[1:]):
  op=next((q for q in openings if q[0]-q[1]/2-.001<=(a+b)/2<=q[0]+q[1]/2+.001),None)
  if op:
   c,w,h,zc=op;bottom=zc-h/2;top=zc+h/2
   if bottom>z0:panel((a+b)/2,b-a,(z0+bottom)/2,bottom-z0)
   if top<z1:panel((a+b)/2,b-a,(top+z1)/2,z1-top)
  else:panel((a+b)/2,b-a,(z0+z1)/2,z1-z0)
 for c,w,h,zc in openings:
  loc=(coord,c,zc) if axis=='x' else (c,coord,zc);size=(.035,w-.055,h-.055) if axis=='x' else (w-.055,.035,h-.055)
  box('CabinWindow',loc,size,'Wheelhouse','Glass',.005)
  for edge in [-1,1]:
   cc=c+edge*w/2;loc=(coord,cc,zc) if axis=='x' else (cc,coord,zc);size=(.12,.055,h+.07) if axis=='x' else (.055,.12,h+.07)
   box('WindowFrame',loc,size,'Wheelhouse','Metal',.008)
   loc=(coord,c,zc+edge*h/2) if axis=='x' else (c,coord,zc+edge*h/2);size=(.12,w,.055) if axis=='x' else (w,.12,.055)
   box('WindowFrame',loc,size,'Wheelhouse','Metal',.008)
wall('SideWall','x',-2.65,-11.5,-5.5,[(-10.1,.44,.57,4.79),(-7.65,.44,.57,4.79)])
wall('SideWall','x',2.65,-11.5,-5.5,[(-10.1,.44,.57,4.79),(-7.65,.44,.57,4.79)])
wall('ForwardWall','y',-5.5,-2.65,2.65,[(-1.5,.42,.48,4.95),(-.45,.62,.80,5.05),(.65,.43,.48,4.95)])
wall('AfterWall','y',-11.5,-2.65,2.65,[(1.64,.44,.57,4.79)])
for x in [-.48,.48]:
 box('AfterDoor',(x,-11.58,4.48),(.89,.065,1.76),'Superstructure','House',.08)
 rod('DoorHandle',(x+.25,-11.64,4.34),(x+.25,-11.64,4.57),.02,'Superstructure','Metal')
for y in [-10.8,-9.6,-8.4,-7.2,-6]:
 for s in [-1,1]:rod('CabinPanelSeam',(s*2.699,y,3.6),(s*2.699,y,5.6),.008,'Superstructure','Pipe',6)
# The roof box/coaming is visible; its internal equipment is not established.
for s in [-1,1]:box('RoofCoaming',(s*.92,-8.25,6.19),(.06,1.7,.75),'Wheelhouse','Hull')
box('RoofCoaming',(0,-7.4,6.19),(1.9,.06,.75),'Wheelhouse','Hull')
box('RoofCoaming',(0,-9.1,6.19),(1.9,.06,.75),'Wheelhouse','Hull')
box('RoofHatch',(0,-8.25,5.85),(1.65,1.35,.08),'Wheelhouse','Deck')

def stair(name,x,y,direction,width=1):
 for i in range(9):box(name+'Tread',(x,y+direction*i*.28,1.65+i*.21),(width,.30,.065),'Railings','Deck',.01)
 for s in [-1,1]:
  rod(name+'Stringer',(x+s*width/2,y,1.55),(x+s*width/2,y+direction*2.3,3.43),.05,'Railings','House')
  rod(name+'Handrail',(x+s*width/2,y,2.5),(x+s*width/2,y+direction*2.3,4.38),.025)
  for i in [0,4,8]:rod(name+'Post',(x+s*width/2,y+direction*i*.28,1.65+i*.21),(x+s*width/2,y+direction*i*.28,2.5+i*.21),.024)
stair('AfterStair',0,-13.9,1,1.35);stair('ForwardStair',-1.45,-3.15,-1,1)
for y in [-5.15,-11.85]:
 box('Landing',(0,y,3.46),(5.4,.65,.08),'Railings','Deck')
 for x in [-2.6,-1.8,1.8,2.6]:rod('LandingPost',(x,y,3.5),(x,y,4.4),.025)
 for a,b in [(-2.6,-.85),(.85,2.6)]:rod('LandingRail',(a,y,4.4),(b,y,4.4),.025)

box('PumpCabinet',(0,5.25,2.94),(2.4,2.25,2.5),'DeckEquipment','House',.06)
box('PumpCabinetBase',(0,5.25,1.64),(2.65,2.55,.16),'DeckEquipment','Pipe')
for x in [-1.12,1.12]:
 for y in [4.22,6.28]:rod('CabinetLeg',(x,y,1.55),(x,y,1.83),.05,'DeckEquipment','Pipe')
for s in [-1,1]:
 box('CabinetDoor',(s*1.23,5.25,3),(.06,1.35,1.98),'DeckEquipment','House',.05)
 rod('CabinetHandle',(s*1.28,5.5,2.8),(s*1.28,5.5,3.05),.025,'DeckEquipment','Metal')
for x in [-1.45,1.45]:
 box('PumpHousing',(x,-1.7,2.25),(.75,.9,1.35),'DeckEquipment','Pipe',.04)
 box('MeterFace',(x,-1.23,2.55),(.48,.04,.40),'DeckEquipment','Metal')
box('LowServiceLocker',(-1.8,-4,1.94),(1.2,1.3,.78),'DeckEquipment','House')

# Routes follow visible longitudinal trunks and central manifold; hidden routing is inferred.
for x in [-.62,.62]:
 path('MainCargoPipe',[(x,-7,1.72),(x,-3,1.72),(x,-2.7,1.95),(x,2.8,1.95),(x,3.2,1.72),(x,10,1.72)],.08)
 for y in [-5,-2,1,4,8]:
  rod('Flange',(x,y-.04,1.8 if y in [-5,4,8] else 1.95),(x,y+.04,1.8 if y in [-5,4,8] else 1.95),.13,'TankerPiping','Pipe',16)
  box('PipeSaddle',(x,y,1.64),(.27,.16,.22),'TankerPiping','Metal',.01)
for y in [-2.6,.1,2.4]:
 path('CrossManifold',[(-2.35,y,2.05),(-1.7,y,2.05),(-1.4,y,1.95),(1.4,y,1.95),(1.7,y,2.05),(2.35,y,2.05)],.065)
 for x in [-1.15,1.15]:
  rod('ValveBody',(x,y,1.88),(x,y,2.12),.12,'TankerPiping','Pipe',12)
  rod('ValveSpindle',(x,y,2.1),(x,y,2.36),.023,'TankerPiping','Metal')
  ring('ValveWheel',(x,y,2.38),.18,.016,'TankerPiping','Metal')
  for a in [0,math.pi/2]:rod('ValveSpoke',(x-.17*math.cos(a),y-.17*math.sin(a),2.38),(x+.17*math.cos(a),y+.17*math.sin(a),2.38),.012,'TankerPiping','Metal',6)
for y in [-1,3,8]:
 for x in [-1.65,1.65]:
  rod('TankHatch',(x,y,1.55),(x,y,1.68),.36,'DeckEquipment','Deck',24)
  rod('HatchHandle',(x-.14,y,1.73),(x+.14,y,1.73),.02,'DeckEquipment','Metal')
for x,y,height in [(-2.2,8.1,3.8),(2.2,8.1,3.8),(-2.25,-5,4.1),(2.25,-5,4.1),(-2,-10.3,6.4),(2,-10.3,6.4)]:
 rod('VentPipe',(x,y,1.55),(x,y,height),.052,'MastsAndVents','House')
 rod('VentCap',(x,y,height),(x,y,height+.08),.09,'MastsAndVents','Metal')
for x in [-1.8,1.8]:
 rod('HoseDavit',(x,6.8,1.6),(x,6.8,4.1),.055,'DeckEquipment','House')
 rod('HoseDavitArm',(x,6.8,4.1),(x*.4,6.8,4.75),.055,'DeckEquipment','House')
 points=[(x,6.6,2.8+1.1*math.cos(i*math.pi/18)) for i in range(37)]
 # Hanging hose loop in a vertical plane, not a decorative coil.
 points=[(x+.40*math.sin(i*math.pi/18),6.6,2.85+1.1*math.cos(i*math.pi/18)) for i in range(37)]
 path('CargoHose',points,.075,'TankerPiping','Rubber')

for s in [-1,1]:
 for y in [-10,-8,-6,-4,-2,0,2,4,6,8,10]:rod('DeckRailPost',(s*2.86,y,1.55),(s*2.86,y,2.4),.022)
 for z in [1.98,2.4]:rod('DeckRail',(s*2.86,-10,z),(s*2.86,10,z),.021)
 for y in [-12.7,11.85]:
  box('BollardBase',(s*2.0,y,1.59),(.68,.7,.10),'MooringEquipment','Metal')
  for yy in [y-.2,y+.2]:
   rod('Bollard',(s*2,yy,1.62),(s*2,yy,2.04),.105,'MooringEquipment','Metal',16)
   rod('BollardCap',(s*2,yy,2.04),(s*2,yy,2.10),.15,'MooringEquipment','Metal',16)
 for y in [-11,7.6]:
  ring('TyreFender',(s*3.02,y,1.30),.40,.12,'MooringEquipment','Rubber',(math.pi/2,0,math.pi/2))
  rod('FenderLashing',(s*3,y,1.65),(s*2.8,y,2.15),.018,'MooringEquipment','Metal')
 text('ForwardName','МН-52',(s*2.84,11.9,1.76),.40,(math.pi/2,0,math.pi/2 if s>0 else -math.pi/2))
 text('AfterName','МН-52',(s*2.84,-12.6,1.68),.32,(math.pi/2,0,math.pi/2 if s>0 else -math.pi/2))
 for y in [-6.0]:ring('Lifebuoy',(s*2.74,y,4.96),.32,.065,'MooringEquipment','Orange',(math.pi/2,0,math.pi/2))
for x in [-1.5,1.5]:
 # Stockless anchor silhouettes visible on forward transom.
 rod('AnchorShank',(x,14.40,.75),(x,14.40,1.65),.055,'MooringEquipment','Metal')
 rod('AnchorCrown',(x-.22,14.4,.8),(x+.22,14.4,.8),.055,'MooringEquipment','Metal')
 for s in [-1,1]:
  o=box('AnchorFluke',(x+s*.21,14.40,.93),(.21,.12,.40),'MooringEquipment','Metal');o.rotation_euler.y=s*.35
for y,z in [(-8.3,5.8),(8.9,1.55)]:
 height=2.65 if y<0 else 3.6
 rod('Mast',(0,y,z),(0,y,z+height),.045,'MastsAndVents','House')
 rod('CrossTree',(-.85,y,z+height-.6),(.85,y,z+height-.6),.025,'MastsAndVents','House')
 for s in [-1,1]:rod('MastStay',(s*1.8,y-1.4,z),(0,y,z+height-.15),.009,'MastsAndVents','Metal',6)
 rod('MastLamp',(0,y,z+height),(0,y,z+height+.15),.065,'MastsAndVents','Metal',12)

# Sparse operational streaks, never the broad corrosion in the later photos.
for s in [-1,1]:
 for y in [-10.8,-6.1,3.8,7.5]:box('LocalizedWear',(s*3.045,y,1.04),(.006,.035,.22),'Hull','Wear',0)
for name,co in {'Waterline':(0,0,1.2),'BowReference':(0,14.4,1.2),'SternReference':(0,-14.4,1.2),'StarboardReference':(3.15,0,1.2),'UpReference':(0,0,2.2),'PushConnection':(0,-14.45,1.85)}.items():
 o=bpy.data.objects.new('MN52_'+name,None);groups['PhysicsReferencePoints'].objects.link(o);o.location=co;o.empty_display_size=.3
scene['phase']='geometry'
print('MN52 geometry created',sum(o.type=='MESH' for o in scene.objects))
