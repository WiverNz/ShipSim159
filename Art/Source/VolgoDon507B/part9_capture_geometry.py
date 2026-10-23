"""Read-only source measurement. Execute through registered Blender MCP."""
import bpy
import bmesh
import hashlib
import json
from pathlib import Path

root = Path('G:/Projects/ShipSim159')
folder = root / 'Art/Source/VolgoDon507B'
assert not (folder / 'part9-geometry-baseline.json').exists(), 'Preserve the accepted baseline'
assert Path(bpy.data.filepath).resolve() == (folder / 'VolgoDon507B.blend').resolve()
assert not bpy.data.is_dirty, 'Save or resolve user edits before baseline capture'
scene = bpy.data.scenes['VolgoDon507B_Source']
assert scene.frame_current == 1, 'Baseline requires closed mechanisms at frame 1'
records = []
all_points = []
for obj in sorted(scene.objects, key=lambda o: o.name):
    if obj.type != 'MESH':
        continue
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    all_points.extend(points)
    obj.data.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    geometry = [tuple(round(x, 6) for x in v.co) for v in obj.data.vertices]
    geometry += [tuple(p.vertices) for p in obj.data.polygons]
    records.append(dict(name=obj.name, triangles=len(obj.data.loop_triangles),
        bounds_min=[min(p[i] for p in points) for i in range(3)],
        bounds_max=[max(p[i] for p in points) for i in range(3)],
        boundary_edges=sum(e.is_boundary for e in bm.edges),
        non_manifold_edges=sum(not e.is_manifold for e in bm.edges),
        uv_layers=len(obj.data.uv_layers),
        geometry_sha256=hashlib.sha256(repr(geometry).encode()).hexdigest()))
    bm.free()
prior = json.loads((folder / 'part8-source.json').read_text())['levels'][0]['meshes']
assert {r['name']: r['geometry_sha256'] for r in records} == {
    r['name']: r['geometry_sha256'] for r in prior}, 'Source differs from Part 8'
report = dict(scene=scene.name, frame=1, units='metres',
    axes='Blender world X starboard, Y forward, Z up; origin at selected loaded waterline',
    source_sha256=hashlib.sha256((folder / 'VolgoDon507B.blend').read_bytes()).hexdigest(),
    matches_part8_geometry=True, meshes=records,
    overall_min=[min(p[i] for p in all_points) for i in range(3)],
    overall_max=[max(p[i] for p in all_points) for i in range(3)],
    landmarks={o.name: list(o.matrix_world.translation) for o in scene.objects if o.type == 'EMPTY'})
(folder / 'part9-geometry-baseline.json').write_text(json.dumps(report, indent=2))
print('PART9_GEOMETRY_PASS', len(records), sum(r['triangles'] for r in records))
print('HULL', next(r for r in records if r['name'] == 'VD507B_hull'))
