"""Conform shell stiffeners and rubbing strips to the final inferred offsets."""
import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path('G:/Projects/ShipSim159');s=bpy.data.scenes['MN52_T77'];bpy.context.window.scene=s
rows=json.loads((ROOT/'Assets/ShipSimulator/Data/Vessels/MN52_T77.json').read_text(encoding='utf-8'))['hydrostatics']['sectionTable']['stations']
def breadth(y,z):
 def at(row):
  hs=row['heightsAboveKeelM'];ws=row['halfBreadthsM'];z0=max(hs[0],min(z,hs[-1]));i=next((i for i in range(len(hs)-1) if hs[i]<=z0<=hs[i+1]),len(hs)-2)
  return ws[i]+(ws[i+1]-ws[i])*(z0-hs[i])/(hs[i+1]-hs[i])
 i=next((i for i in range(len(rows)-1) if rows[i]['localZ']<=y<=rows[i+1]['localZ']),0 if y<0 else len(rows)-2)
 t=max(0,min(1,(y-rows[i]['localZ'])/(rows[i+1]['localZ']-rows[i]['localZ'])))
 return at(rows[i])*(1-t)+at(rows[i+1])*t
if not s.get('shell_fittings_fit'):
 for ob in list(bpy.data.collections['MN52_Hull'].objects):
  if ob.name.startswith('MN52_SideStiffener'):
   sign=1 if ob.location.x>0 else -1
   for v in ob.data.vertices:
    co=ob.matrix_world@v.co;co.x=sign*(breadth(co.y,co.z)+.026)+v.co.x;v.co=ob.matrix_world.inverted()@co
  if ob.name.startswith('MN52_SideRubbingStrake'):
   sign=1 if sum(v.co.x for v in ob.data.vertices)>0 else -1
   # Each mesh ring has the original tube centre at the average of its vertices.
   for v in ob.data.vertices:
    co=ob.matrix_world@v.co
    oldwidth=3 if abs(co.y)<=11 else (3-.56*max(0,(abs(co.y)-11)/3.4))
    co.x+=sign*(breadth(co.y,1.22)-oldwidth);v.co=ob.matrix_world.inverted()@co
 s['shell_fittings_fit']=True
print('Shell fittings fitted')
