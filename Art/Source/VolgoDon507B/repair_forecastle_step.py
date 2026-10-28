"""Repair only the authored forecastle step winding; run via registered Blender MCP."""
from mathutils import Vector


def repair_forecastle_step(scene):
    scene.view_layers[0].update()
    hull = next(o for o in scene.objects if o.name == 'VD507B_hull' or
                o.name in ('LOD1__VD507B_hull', 'LOD2__VD507B_hull'))
    mesh = hull.data
    world = [hull.matrix_world @ v.co for v in mesh.vertices]
    normal_matrix = hull.matrix_world.to_3x3().inverted().transposed()
    step = [p for p in mesh.polygons if all(55.90 < world[i].y < 55.94 and
            1.98 < world[i].z < 2.94 for i in p.vertices)]
    assert len(step) >= 2, 'Forecastle step geometry was not found'
    area = sum(((world[p.vertices[1]] - world[p.vertices[0]]).cross(
        world[p.vertices[2]] - world[p.vertices[0]])).length / 2 for p in step)
    assert 13.5 < area < 14, ('Unexpected wall coverage', area)
    wrong = [p for p in step if (normal_matrix @ p.normal).y > 0]
    if not wrong:
        return dict(mesh=hull.name, flipped=0, area_m2=area)
    # Preserve corner shading and UVs by vertex identity when reversing loop order.
    normals = {(p.index, mesh.loops[i].vertex_index): mesh.corner_normals[i].vector.copy()
               for p in mesh.polygons for i in p.loop_indices}
    uvs = [{(p.index, mesh.loops[i].vertex_index): layer.data[i].uv.copy()
            for p in wrong for i in p.loop_indices} for layer in mesh.uv_layers]
    original_vertices = [v.co.copy() for v in mesh.vertices]
    indices = {p.index for p in wrong}
    for p in wrong:
        p.flip()
    mesh.update()
    new_normals = [Vector((0, 0, 0)) for _ in mesh.loops]
    for p in mesh.polygons:
        for i in p.loop_indices:
            key = (p.index, mesh.loops[i].vertex_index)
            new_normals[i] = -normals[key] if p.index in indices else normals[key]
            if p.index in indices:
                for layer, saved in zip(mesh.uv_layers, uvs):
                    layer.data[i].uv = saved[key]
    mesh.normals_split_custom_set(new_normals)
    mesh.update()
    assert all(v.co == before for v, before in zip(mesh.vertices, original_vertices))
    assert all((normal_matrix @ mesh.polygons[i].normal).normalized().dot(Vector((0, -1, 0))) > .99
               for i in indices)
    return dict(mesh=hull.name, flipped=len(wrong), area_m2=area)
