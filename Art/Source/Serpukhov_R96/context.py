"""Load modelling helpers without rebuilding geometry, through Blender MCP."""
import bpy, bmesh, math, json, ast
from mathutils import Vector
from pathlib import Path
ROOT=Path('G:/Projects/ShipSim159')
SOURCE=ROOT/'Art/Source/Serpukhov_R96'
scene=bpy.data.scenes['Serpukhov_R96']
bpy.context.window.scene=scene
root=bpy.data.collections['Serpukhov_R96']
groups={c.name:c for c in root.children}
mats={m.name.removeprefix('R96_'):m for m in bpy.data.materials if m.name.startswith('R96_')}
tree=ast.parse((SOURCE/'01_geometry.py').read_text(encoding='utf-8'))
for node in tree.body:
    if isinstance(node,ast.FunctionDef): exec(compile(ast.Module(body=[node],type_ignores=[]),'helpers','exec'))
