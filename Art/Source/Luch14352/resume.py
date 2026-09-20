"""Restore modelling helpers between independent MCP execution namespaces."""
import bpy, bmesh, math, os, ast
from mathutils import Vector, Quaternion
BASE='G:/Projects/ShipSim159'
OUT=BASE+'/Logs/Luch14352'
scene=bpy.data.scenes['Luch14352_Studio']
bpy.context.window.scene=scene
model=bpy.data.collections['Luch14352_Model']
root=bpy.data.objects['Luch14352_ROOT']
cam=scene.camera
for variable,name in [('white','Paint_Ivory'),('blue','Paint_DeckTeal'),('hullmat','Paint_HullCharcoal'),('rubber','Skeg_Rubber'),('metal','Rail_Aluminium'),('dark','Vent_Dark'),('glass','Glass_BlueGrey'),('inside','Interior_Charcoal'),('seatmat','Interior_Seats')]:
    globals()[variable]=bpy.data.materials[name]
tree=ast.parse(open(BASE+'/Art/Source/Luch14352/01_blockout.py',encoding='utf-8').read())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef)],type_ignores=[]),'helpers','exec'))
