"""Blender regression checks for the missing bridge and overwritten saloon glass."""
import bpy
from mathutils import Vector


def validate():
    objects = bpy.data.objects
    windows = objects['Windows_Glass'].data
    bridge = objects['Wheelhouse'].data
    bridge_glass = objects['Wheelhouse_Glass'].data
    windows.calc_loop_triangles()
    assert len(windows.polygons) >= 1000, 'Missing saloon glazing faces'
    assert sum(p.area for p in windows.polygons) > 20, 'Saloon glass area collapsed'
    used = {i for p in windows.polygons for i in p.vertices}
    assert len(used) == len(windows.vertices), 'Unreferenced glazing vertices'
    assert len(bridge.polygons) >= 50, 'Missing wheelhouse shell'
    assert any(p.normal.z > .9 and p.center.z > 3.8 for p in bridge.polygons), 'Missing bridge roof'
    assert len(bridge_glass.polygons) == 14, 'Missing wrap-around bridge panes'
    for p in bridge_glass.polygons:
        outward = p.center - Vector((0, 17.3-8.65, 3.2))
        assert p.normal.dot(outward) > 0, 'Inward-facing bridge glass'
    assert all(p.normal.x >= -.001 for p in windows.polygons), 'Inward saloon glass'
    shell = objects['Superstructure'].evaluated_get(bpy.context.evaluated_depsgraph_get())
    for a in (18.04, 18.64):
        for z in (.44, 2.45):
            hit, _, _, _ = shell.ray_cast(Vector((10, 17.3-a, z)), Vector((-1, 0, 0)))
            assert hit, 'Aperture surround left a hole at a panel corner'
    assert objects['Waterline'].location.length < 1e-6
    assert (objects['PropPoint_S'].location - Vector((1.6,-13.5,-1.78))).length < 1e-5
    assert objects['Light_MastheadAft'].location.z > objects['Light_MastheadFwd'].location.z
    bridge_object = objects['Wheelhouse'].evaluated_get(bpy.context.evaluated_depsgraph_get())
    hit, _, _, _ = bridge_object.ray_cast(objects['CameraNavigator'].location,
                                         Vector((0, 1, 0)), distance=3)
    assert not hit, 'Navigator looks through an opaque bridge pillar'
    print('METEOR_SOURCE|PASS: bridge roof, glazing coverage, winding and attachment landmarks')


validate()
