"""Prove the repair guards reject detached roots, glazing gaps and an open hull.

Run inside the generated Blender scene. Mutations use temporary mesh copies and
are restored even when an assertion fails.
"""
import bpy, bmesh

checks = {}
exec(compile(open('G:/Projects/ShipSim159/Art/Source/Meteor342U/validate_model.py',
                  encoding='utf-8').read(), 'validate_model.py', 'exec'), checks)


def rejects(name, mutate, expected):
    ob = bpy.data.objects[name]
    original = ob.data
    temporary = original.copy()
    ob.data = temporary
    try:
        mutate(ob)
        bpy.context.view_layer.update()
        try:
            checks['validate_geometry']()
        except AssertionError as error:
            assert expected in str(error), str(error)
            print('METEOR_REGRESSION|PASS: ' + expected)
        else:
            raise AssertionError('Guard accepted defect: ' + expected)
    finally:
        ob.data = original
        bpy.data.meshes.remove(temporary)
        bpy.context.view_layer.update()


def open_hull(ob):
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bm.faces.ensure_lookup_table()
    bmesh.ops.delete(bm,geom=[bm.faces[10]],context='FACES')
    bm.to_mesh(ob.data); bm.free()


def detach_root(ob):
    for i in ob['hull_root_indices']:
        ob.data.vertices[i].co.z -= .5
    ob.data.update()


def displace_glass(ob):
    for vertex in ob.data.vertices:
        vertex.co.x += .03
    ob.data.update()


def protrude_ceiling(ob):
    for vertex in ob.data.vertices:
        if vertex.co.x > 0:
            vertex.co.x += .5
    ob.data.update()


rejects('Hull', open_hull, 'open or nonmanifold shell')
rejects('FrontFoil_Struts', detach_root, 'root detached from hull')
rejects('Windows_Glass', displace_glass, 'Gap between glass')
rejects('InteriorProxy_Sole', protrude_ceiling, 'interior protrudes')
checks['validate_geometry']()
