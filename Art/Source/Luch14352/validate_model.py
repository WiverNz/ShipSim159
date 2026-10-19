"""Geometry and silhouette guards for the 1994 Luch reference reconstruction."""
import bpy, bmesh
from mathutils import Vector

def validate():
    objects = bpy.data.collections['Luch14352_Model'].objects
    assert len([o for o in objects if o.name.startswith('Windows_')]) == 18
    deps = bpy.context.evaluated_depsgraph_get()
    for name in ('Hull','Deck_Obnose','Roof_Saloon','Roof_Wheelhouse',
                 'Refit_WheelhouseBody','Skeg_L','Skeg_R'):
        ev = objects[name].evaluated_get(deps)
        data = ev.to_mesh()
        bm = bmesh.new(); bm.from_mesh(data)
        assert all(e.is_manifold for e in bm.edges), name+': open shell'
        assert bm.calc_volume(signed=True) > 0, name+': inverted shell'
        bm.free(); ev.to_mesh_clear()
    # The replacement shoulder must not cover the isolated forward side window.
    for side,label in ((-1,'L'),(1,'R')):
        origin = Vector((side*4,7.3,1.39))
        direction = Vector((-side,0,0))
        glass = objects['Windows_'+label+'_8'].evaluated_get(deps)
        assert glass.ray_cast(origin,direction)[0], 'Missing forward glazing'
        for ob in objects:
            if ob.name.startswith('Refit_Shoulder'):
                assert not ob.evaluated_get(deps).ray_cast(origin,direction)[0], 'Shoulder blocks glazing'
    assert abs(max(v.co.z for v in objects['Roof_Wheelhouse'].data.vertices)-2.94) < .001
    print('LUCH_GEOMETRY|PASS: closed main shells, 18 saloon panes, clear forward glazing')

validate()
