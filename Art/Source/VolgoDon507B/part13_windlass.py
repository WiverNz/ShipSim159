"""Part 13, run once through registered Blender MCP on the saved source at frame 1.

Part 4 bound Windlass_Port/Starboard to the two mooring-winch drums at Y=57.5 m (asymmetric, X=-1.81 and
+3.21 m), while the anchor windlass gypsies at Y=60.6 m stayed in the static VD507B_detali mesh. This moves
the gypsy islands out of VD507B_detali into WindlassMesh_<side> under the existing Windlass_<side> pivots,
re-centred on the gypsy axis, and returns the drums to the static deck as MooringDrumMesh_<side>.
Geometry is partitioned, never rebuilt: every vertex, face, UV and material is kept. Binding names stay.
"""
import bpy
import bmesh
from mathutils import Vector

scene = bpy.data.scenes['VolgoDon507B_Source']
bpy.context.window.scene = scene
assert not scene.get('part13_windlass'), 'part13_windlass.py has already been applied'
scene.frame_set(1)
detail = bpy.data.objects['VD507B_detali']
static_parent = detail.parent
# Whelped gypsy hub (|X| 0.83-1.18 m) and its outboard cheek (1.18-1.36 m) per side, from the face survey of
# VD507B_detali (world metres). The inboard cheek stays fixed: on the port side it carries the band-brake
# handwheel, and a plain disc shows no rotation anyway.
BOX = {'Port': (-1.37, -0.80), 'Starboard': (0.80, 1.37)}
Y, Z = (59.9, 61.3), (3.8, 5.2)


def islands(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    seen, groups = set(), []
    for face in bm.faces:
        if face.index in seen:
            continue
        stack, group = [face], []
        seen.add(face.index)
        while stack:
            f = stack.pop()
            group.append(f.index)
            for edge in f.edges:
                for g in edge.link_faces:
                    if g.index not in seen:
                        seen.add(g.index)
                        stack.append(g)
        points = [obj.matrix_world @ v.co for i in group for v in bm.faces[i].verts]
        groups.append((group, points))
    bm.free()
    return groups


def inside(points, xs):
    # Faces lying wholly against the inboard cheek face (|X| <= 0.835 m) belong to the fixed cheek and handwheel.
    return all(xs[0] <= p.x <= xs[1] and Y[0] <= p.y <= Y[1] and Z[0] <= p.z <= Z[1] for p in points) and \
        max(abs(p.x) for p in points) > 0.835


def split(obj, faces, name):
    """Copy the faces into a new object with the same transform, then delete them from obj."""
    mesh = obj.data.copy()
    mesh.name = name
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    keep = set(faces)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in keep], context='FACES')
    bm.to_mesh(mesh)
    bm.free()
    part = bpy.data.objects.new(name, mesh)
    for collection in obj.users_collection:
        collection.objects.link(part)
    part.matrix_world = obj.matrix_world.copy()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[bm.faces[i] for i in faces], context='FACES')
    bm.to_mesh(obj.data)
    bm.free()
    return part


groups = islands(detail)
selected = {side: sorted(i for g, pts in groups if inside(pts, xs) for i in g) for side, xs in BOX.items()}
assert all(len(v) >= 60 for v in selected.values()), {k: len(v) for k, v in selected.items()}
assert len(selected['Port']) == len(selected['Starboard']), 'the port gypsy must be a copy of the starboard one'
order = list(selected)
report = {}
for side in order:
    pivot = bpy.data.objects['Windlass_' + side]
    drum = bpy.data.objects['WindlassMesh_' + side]
    drum_world = drum.matrix_world.copy()
    drum.name = 'MooringDrumMesh_' + side
    drum.data.name = 'MooringDrumMesh_' + side
    drum.parent = static_parent
    drum.matrix_world = drum_world
    part = split(detail, selected[side], 'WindlassMesh_' + side)
    points = [part.matrix_world @ v.co for v in part.data.vertices]
    lo = Vector([min(p[k] for p in points) for k in range(3)])
    hi = Vector([max(p[k] for p in points) for k in range(3)])
    centre = (lo + hi) * 0.5
    delta = centre - pivot.matrix_world.translation
    # The pivot's location is keyed at every Part 4 pose; shift every key by the same offset.
    action = pivot.animation_data.action
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in bag.fcurves:
                    if curve.data_path == 'location':
                        for key in curve.keyframe_points:
                            key.co.y += delta[curve.array_index]
                            key.handle_left.y += delta[curve.array_index]
                            key.handle_right.y += delta[curve.array_index]
    scene.frame_set(1)
    world = part.matrix_world.copy()
    part.parent = pivot
    part.matrix_world = world
    report[side] = dict(faces=len(selected[side]), centre=[round(c, 4) for c in centre],
                        diameter=round(hi.z - lo.z, 4), pivot_shift=[round(d, 4) for d in delta])
    selected = {s: [i - sum(1 for j in selected[side] if j < i) for i in v] if s != side else v for s, v in selected.items()}
scene['part13_windlass'] = True
scene.frame_set(1)
print('PART13_WINDLASS', report)
