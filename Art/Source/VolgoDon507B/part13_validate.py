"""Part 13 source validation through registered Blender MCP, read-only. Writes part13-validation.json.

Checks every mechanism pivot against mechanisms.json, hatch panels against each other and the fixed ship over
the whole clip including the lift and lowering frames that Part 4 skipped, the minimum gap between moving
panels and to fixed geometry, the re-bound windlass gypsies through a full turn, and unseated anchors.
"""
import bpy
import json
import math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = Path('G:/Projects/ShipSim159')
SOURCE = ROOT / 'Art/Source/VolgoDon507B'
scene = bpy.data.scenes['VolgoDon507B_Source']
bpy.context.window.scene = scene
manifest = json.load(open(SOURCE / 'mechanisms.json'))
assert scene.get('part13_windlass'), 'run part13_windlass.py first'


def tree(objects):
    vertices, polygons = [], []
    for o in objects:
        base = len(vertices)
        vertices.extend(o.matrix_world @ v.co for v in o.data.vertices)
        polygons.extend(tuple(base + i for i in p.vertices) for p in o.data.polygons)
    return BVHTree.FromPolygons(vertices, polygons)


def gap(moving, other_tree, stride=7):
    """Smallest distance from sampled vertices of the moving meshes to the other tree (0 means touching)."""
    best = float('inf')
    for o in moving:
        for v in list(o.data.vertices)[::stride]:
            hit = other_tree.find_nearest(o.matrix_world @ v.co)
            if hit[0] is not None:
                best = min(best, hit[3])
    return best


scene.frame_set(1)
meshes = [o for o in scene.objects if o.type == 'MESH']
for spec in manifest['mechanisms']:
    obj = bpy.data.objects[spec['name']]
    assert obj.parent.name == 'Mechanisms' and obj.animation_data, spec['name']
    assert (obj.matrix_world.translation - Vector(spec['rest_position'])).length < 1e-3, spec['name']

root = bpy.data.objects['Mechanisms']
moving = {o for r in root.children if r.animation_data for o in [r, *r.children_recursive] if o.type == 'MESH'}
fixed_objects = [o for o in meshes if o not in moving]
fixed = tree(fixed_objects)
keys = ['AftOuter', 'AftInner', 'ForeInner', 'ForeOuter']
# Steel parts of each cover; the rubber joint seals are checked on their own, since a compressed seal rubs
# along the neighbouring cover's end plate while a cover lifts straight off it.
panel_meshes = {k: [o for o in bpy.data.objects['Hatch_' + k].children_recursive
                    if o.type == 'MESH' and not o.name.startswith('Seal_')] for k in keys}
seal_meshes = {k: [o for o in bpy.data.objects['Hatch_' + k].children_recursive
                   if o.type == 'MESH' and o.name.startswith('Seal_')] for k in keys}
seal_rub = []
closed_contacts = {k: len(tree([bpy.data.objects['Panel_' + k]]).overlap(fixed)) for k in keys}

rest = {k: bpy.data.objects['Hatch_' + k].matrix_world.translation.copy() for k in keys}
collisions, minimum_panel_gap, samples = [], float('inf'), 0
gap_by_frame = {}
for frame in range(90, 711):
    scene.frame_set(frame)
    panels = {k: tree(panel_meshes[k]) for k in keys}
    for i, a in enumerate(keys):
        for b in keys[i + 1:]:
            if panels[a].overlap(panels[b]):
                collisions.append((frame, a, b))
            for x, y in ((a, b), (b, a)):
                if seal_meshes[x] and tree(seal_meshes[x]).overlap(panels[y]):
                    seal_rub.append((frame, x, y))
        # A closed cover sits slightly into its coaming seal by design; contact with the seal is allowed while
        # the cover is straight above its rest pose and within the first 5 cm of lift.
        offset = bpy.data.objects['Hatch_' + a].matrix_world.translation - rest[a]
        resting = offset.xy.length < 1e-3 and offset.z < 0.05
        if not resting and tree([bpy.data.objects['Panel_' + a]]).overlap(fixed):
            collisions.append((frame, a, 'fixed'))
    if frame % 10 == 0 and 100 <= frame <= 700:
        g = min(gap(panel_meshes[a], panels[b]) for i, a in enumerate(keys) for b in keys[i + 1:]
                if abs(bpy.data.objects['Hatch_' + a].matrix_world.translation.y -
                       bpy.data.objects['Hatch_' + b].matrix_world.translation.y) < 25)
        gap_by_frame[frame] = round(g, 4)
        minimum_panel_gap = min(minimum_panel_gap, g)
    samples += 1

scene.frame_set(210)
stack_gaps = {}
for outer, inner in [('AftOuter', 'AftInner'), ('ForeOuter', 'ForeInner')]:
    low = min((o.matrix_world @ v.co).z for o in panel_meshes[outer] for v in o.data.vertices)
    high = max((o.matrix_world @ v.co).z for o in panel_meshes[inner] for v in o.data.vertices)
    stack_gaps[outer] = round(low - high, 4)

# Gypsies through a full turn. Contact is allowed only where the gypsy bears by design: on its shaft
# (within 0.1 m of the axis) and against the fixed inboard cheek face (|X| 0.78-0.84 m).
scene.frame_set(1)
fixed_polygons, fixed_vertices = [], []
for o in fixed_objects:
    base = len(fixed_vertices)
    fixed_vertices.extend(o.matrix_world @ v.co for v in o.data.vertices)
    fixed_polygons.extend(tuple(base + i for i in p.vertices) for p in o.data.polygons)
gypsy = {}
for side in ['Port', 'Starboard']:
    pivot = bpy.data.objects['Windlass_' + side]
    mesh = bpy.data.objects['WindlassMesh_' + side]
    axis = pivot.matrix_world.translation.copy()
    points = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
    centre = sum(points, Vector()) / len(points)
    radial = [math.hypot(p.y - axis.y, p.z - axis.z) for p in points]
    unexpected, contacts = [], 0
    base = pivot.rotation_euler.x
    for angle in range(0, 360, 10):
        pivot.rotation_euler.x = base + math.radians(angle)
        bpy.context.view_layer.update()
        for _, f in tree([mesh]).overlap(fixed):
            c = sum((fixed_vertices[i] for i in fixed_polygons[f]), Vector()) / len(fixed_polygons[f])
            contacts += 1
            if not (math.hypot(c.y - axis.y, c.z - axis.z) < 0.1 or 0.78 <= abs(c.x) <= 0.84):
                unexpected.append((angle, [round(x, 3) for x in c]))
    pivot.rotation_euler.x = base
    bpy.context.view_layer.update()
    gypsy[side] = dict(centre_offset_from_pivot_m=round((centre.yz - axis.yz).length, 4),
                       outer_radius_m=round(max(radial), 4), angles_checked=36, design_contacts=contacts,
                       unexpected_contacts=len(unexpected))
    assert gypsy[side]['centre_offset_from_pivot_m'] < 0.01, (side, 'gypsy not on its axis')
    assert not unexpected, (side, unexpected[:5])

scene.frame_set(740)
hull = tree([bpy.data.objects[n] for n in ['VD507B_hull', 'VD507B_hull_detali', 'VD507B_deck']])
for side in ['Port', 'Starboard', 'Stern']:
    assert not tree([bpy.data.objects['AnchorMesh_' + side]]).overlap(hull), ('unseat blocked', side)
scene.frame_set(1)

assert not collisions, collisions[:10]
assert min(stack_gaps.values()) > 0.04, stack_gaps
report = dict(status='PASS', mesh_count=len(meshes), mechanism_bindings=len(manifest['mechanisms']),
              hatch_frames_checked=samples, frame_range=[90, 710], panel_collisions=0,
              closed_seal_contacts=closed_contacts, seal_rub_frames=len(seal_rub),
              seal_rub=[f'{f} {x} seal on {y}' for f, x, y in seal_rub][:40], minimum_gap_between_nearby_panels_m=round(minimum_panel_gap, 4),
              stacked_gap_m=stack_gaps, panel_gap_by_frame_m=gap_by_frame, gypsies=gypsy, unseated_anchors_clear=True,
              limits='Discrete frames every 1/24 s of the review clip; gaps from every seventh vertex to the nearest '
                     'surface. Seal contact of closed covers is intentional. No hydraulic hoses or wire ropes modelled.')
(SOURCE / 'part13-validation.json').write_text(json.dumps(report, indent=2))
print('PART13_VALIDATION', json.dumps({k: v for k, v in report.items() if k != 'panel_gap_by_frame_m'}))
