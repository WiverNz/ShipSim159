"""Final reference review corrections; apply once through Blender MCP."""
import bpy,ast,math
from pathlib import Path
from mathutils import Vector
SOURCE=Path('G:/Projects/ShipSim159/Art/Source/MN52_T77')
scene=bpy.data.scenes['MN52_T77'];bpy.context.window.scene=scene
groups={c.name.removeprefix('MN52_'):c for c in bpy.data.collections['MN52'].children}
mats={m.name.removeprefix('MN52_'):m for m in bpy.data.materials if m.name.startswith('MN52_')}
for node in ast.parse((SOURCE/'01_model.py').read_text(encoding='utf-8')).body:
 if isinstance(node,ast.FunctionDef):exec(compile(ast.Module(body=[node],type_ignores=[]),'helpers','exec'))
for ob in groups['PhysicsReferencePoints'].objects:
 ob.name='MN52_'+ob.name.removeprefix('MN52_').split('.')[0]
if not scene.get('refined'):
 box('ForwardCabinDoor',(1.70,-5.44,4.48),(.78,.06,1.76),'Superstructure','House',.07)
 rod('ForwardDoorHandle',(1.95,-5.39,4.35),(1.95,-5.39,4.57),.021,'Superstructure','Metal')
 # Photo 89189 shows flanking transom ribs and open anchor pockets.
 for x in [-2.25,-.85,.85,2.25]:box('TransomStiffener',(x,14.40,1.26),(.12,.12,.90),'Hull','Hull',.02)
 for s in [-1,1]:
  for y in [-12.8,11.7]:
   ring('Fairlead',(s*2.8,y,2.03),.14,.045,'MooringEquipment','Metal',(math.pi/2,0,math.pi/2))
 # Retain collision references as non-rendering source objects, separate from export.
 for name,loc,size in [('Middle',(0,0,.75),(5.85,20,1.5)),('Cabin',(0,-8.5,4.6),(5.3,6,2.3)),('PumpCabinet',(0,5.25,2.94),(2.4,2.25,2.5))]:
  ob=box('Collision_'+name,loc,size,'Collision','Metal',0);ob.hide_render=True;ob.hide_set(True)
 for sign in [-1,1]:
  for name,y,z,w,l,h in [('SpoonInner',11.25,.9,5.72,2.5,1.2),('SpoonOuter',13,1.2,5.35,1.1,.6),('Transom',13.96,1.45,4.88,.88,.5)]:
   ob=box('Collision_'+name,(0,sign*y,z),(w,l,h),'Collision','Metal',0);ob.hide_render=True;ob.hide_set(True)
 scene['refined']=True
print('Reference refinements and collision references saved in scene')
