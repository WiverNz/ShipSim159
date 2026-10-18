"""Regression checks for cabin coverage, closed hull and attached foil roots."""
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


def validate_geometry():
    import bmesh, json, os
    context = {}
    exec(compile(open('G:/Projects/ShipSim159/Art/Source/Meteor342U/common.py',
                      encoding='utf-8').read(), 'common.py', 'exec'), context)
    deps = bpy.context.evaluated_depsgraph_get()
    objects = bpy.data.objects
    closed = ('Hull', 'FrontFoil', 'RearFoil', 'FrontFoil_Fins',
              'FrontFoil_Struts', 'FrontFoil_OuterStruts',
              'RearFoil_Struts', 'RearFoil_InnerStruts')
    report = {'closed_parts': {}, 'root_embedding_m': {}, 'cabin_internal_boundary_edges': 0}
    for name in closed:
        ev = objects[name].evaluated_get(deps)
        mesh = ev.to_mesh()
        bm = bmesh.new(); bm.from_mesh(mesh)
        bad = sum(not e.is_manifold for e in bm.edges)
        volume = bm.calc_volume(signed=True)
        assert bad == 0, name + ': open or nonmanifold shell'
        assert volume > 0, name + ': inverted closed shell'
        assert all(f.calc_area() > 1e-10 for f in bm.faces), name + ': collapsed face'
        report['closed_parts'][name] = {'nonmanifold_edges': bad, 'outward_volume': volume}
        bm.free(); ev.to_mesh_clear()

    hull = objects['Hull'].evaluated_get(deps)
    for name in closed:
        if not name.endswith('Struts'):
            continue
        ob = objects[name]
        depths = []
        assert len(ob['hull_root_indices']) >= 16, name + ': root ring missing'
        for index in ob['hull_root_indices']:
            p = ob.data.vertices[index].co
            for sign in (-1, 1):
                origin = Vector((sign*p.x, p.y, -5))
                hit, position, _, _ = hull.ray_cast(origin, Vector((0, 0, 1)))
                assert hit, name + ': root is outside hull footprint'
                depth = p.z - position.z
                assert .04 < depth < .30, name + ': root detached from hull'
                depths.append(depth)
        report['root_embedding_m'][name] = [min(depths), max(depths)]

    # Materials remain editable objects but must weld into one unbroken cabin skin.
    bm = bmesh.new()
    for name in ('Superstructure', 'Windows_Glass', 'Windows_Frames', 'Paint_SheerBand'):
        ev = objects[name].evaluated_get(deps)
        mesh = ev.to_mesh(); bm.from_mesh(mesh); ev.to_mesh_clear()
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=2e-5)
    for edge in bm.edges:
        if not edge.is_boundary:
            assert edge.is_manifold, 'Overlapping cabin faces'
            continue
        points = [v.co for v in edge.verts]
        at_end = any(all(abs(p.y - context['y_of'](a)) < 3e-5 for p in points)
                     for a in (context['SUP_FWD'], context['SUP_AFT']))
        at_deck = all(abs(p.z-context['deck_z'](context['a_of'](p.y))) < 3e-5 for p in points)
        if not (at_end or at_deck):
            report['cabin_internal_boundary_edges'] += 1
    bm.free()
    assert report['cabin_internal_boundary_edges'] == 0, 'Gap between glass, frame, roof or shell'

    # Rays through the former nose slit and the rear glazing gaps must hit real closures.
    for name, start, direction in (
        ('Cabin_FrontClosure', Vector((0, 18,
            (context['deck_z'](context['SUP_FWD']) +
             context['roof_crown_z'](context['SUP_FWD'])) / 2)), Vector((0,-1,0))),
        ('SternScreen_Frames', Vector((0, -18, .7)), Vector((0,1,0)))):
        ev = objects[name].evaluated_get(deps)
        assert ev.ray_cast(start,direction)[0], name + ': end gap'

    model = bpy.data.collections['Meteor342U_Model']
    for ob in model.objects:
        assert not (ob.name[-4:-3] == '.' and ob.name[-3:].isdigit()), 'Duplicated pass object: '+ob.name
        if ob.type != 'MESH':
            continue
        if ob.name.startswith('InteriorProxy'):
            for vertex in ob.data.vertices:
                p = vertex.co
                a = context['a_of'](p.y)
                h = (p.z-context['deck_z'](a))/context['lerp_table'](context['H_SCALE'],a)
                section = [(height, fraction) for fraction, height in context['SUP_SECTION']]
                width = context['lerp_table'](context['B_SUP'],a)*context['lerp_table'](section,h)
                assert abs(p.x) < width-.02, ob.name + ': interior protrudes through cabin'
        ev=ob.evaluated_get(deps); mesh=ev.to_mesh(); mesh.calc_loop_triangles()
        assert all(t.area > 1e-12 for t in mesh.loop_triangles), ob.name + ': degenerate triangle'
        ev.to_mesh_clear()
    report['status'] = 'PASS'
    path='G:/Projects/ShipSim159/Art/Source/Meteor342U/geometry-validation.json'
    with open(path,'w',encoding='utf-8') as f:
        json.dump(report,f,indent=2)
    print('METEOR_GEOMETRY|PASS: closed hull/foils, embedded roots, continuous cabin seams')


validate_geometry()
