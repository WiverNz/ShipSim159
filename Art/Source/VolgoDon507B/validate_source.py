"""Validate the independently reopened source through registered Blender MCP."""
import bpy
import json
from pathlib import Path

scene = bpy.data.scenes['VolgoDon507B_Source']
meshes = [o for o in scene.objects if o.type == 'MESH']
assert len(meshes) == 18, 'Missing or duplicated assembly'
for obj in meshes:
    assert not obj.animation_data, 'Legacy animation can override normalized transforms'
    mesh = obj.data
    assert mesh.library is None, 'Geometry must be local and editable'
    assert len(mesh.uv_layers) == 2
    for corrected, legacy in zip(mesh.uv_layers[0].data, mesh.uv_layers[1].data):
        assert abs(corrected.uv.x - legacy.uv.x) < 1e-5
        assert abs(corrected.uv.y + legacy.uv.y - 1) < 1e-5, 'UV flip applied twice'
    for polygon in mesh.polygons:
        a, b, c = [mesh.vertices[i].co for i in polygon.vertices]
        assert (b - a).cross(c - a).length > 2e-10, 'Degenerate triangle'
images = [im for im in bpy.data.images if im.source == 'FILE']
assert len(images) == 9 and all(im.packed_file for im in images), 'Missing packed resources'
points = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
size = [max(p[i] for p in points) - min(p[i] for p in points) for i in range(3)]
assert abs(size[0] - 16.7) < .001 and abs(size[1] - 138.3) < .001, size
hull = next(o for o in meshes if o.name == 'VD507B_hull')
keel = min((hull.matrix_world @ v.co).z for v in hull.data.vertices)
assert abs(keel + 3.53) < .001
triangles = sum(len(o.data.polygons) for o in meshes)
assert triangles == 42299
report = {'status': 'PASS', 'editable_meshes': len(meshes), 'packed_images': len(images),
          'triangles': triangles, 'bounds_xyz': size, 'hull_keel_z': keel,
          'uv_flip_regression': 'PASS', 'degenerate_triangles': 0,
          'legacy_animation': False, 'file': bpy.data.filepath}
with open(Path(bpy.data.filepath).parent / 'source-validation.json', 'w') as f:
    json.dump(report, f, indent=2)
print(json.dumps(report))
