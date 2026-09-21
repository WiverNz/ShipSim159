"""Review studio and measurement helpers for the Meteor 342U source file.

Kept separate from the modelling passes so it can be re-run at any time.
Renders go to the git-ignored Logs directory.
"""
import bpy, bmesh, math, os, json
from mathutils import Vector

BASE = 'G:/Projects/ShipSim159'
OUT = BASE + '/Logs/Meteor342U'
SCENE = 'Meteor342U_Studio'

scene = bpy.data.scenes[SCENE]
bpy.context.window.scene = scene
model = bpy.data.collections['Meteor342U_Model']


def ensure_rig():
    rig = bpy.data.collections.get('Meteor342U_Studio_Rig')
    if rig is None:
        rig = bpy.data.collections.new('Meteor342U_Studio_Rig')
        scene.collection.children.link(rig)
    if 'ReviewSun' not in bpy.data.objects:
        data = bpy.data.lights.new('ReviewSun', 'SUN')
        data.energy = 4.2
        data.angle = math.radians(3.0)
        sun = bpy.data.objects.new('ReviewSun', data)
        rig.objects.link(sun)
        sun.rotation_euler = (math.radians(52), 0, math.radians(38))
    if 'ReviewFill' not in bpy.data.objects:
        data = bpy.data.lights.new('ReviewFill', 'SUN')
        data.energy = 0.8
        fill = bpy.data.objects.new('ReviewFill', data)
        rig.objects.link(fill)
        fill.rotation_euler = (math.radians(66), 0, math.radians(-130))
    if 'ReviewCam' not in bpy.data.objects:
        data = bpy.data.cameras.new('ReviewCam')
        cam = bpy.data.objects.new('ReviewCam', data)
        rig.objects.link(cam)
    scene.camera = bpy.data.objects['ReviewCam']
    world = scene.world or bpy.data.worlds.new('Meteor342U_World')
    scene.world = world
    world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = (0.44, 0.52, 0.60, 1.0)
    bg.inputs['Strength'].default_value = 0.55
    return rig


def water_plane(visible=True):
    """A plane at Z = 0 so the floating and foilborne attitudes can be compared."""
    rig = ensure_rig()
    water = bpy.data.objects.get('WaterPlane')
    if water is None:
        mesh = bpy.data.meshes.new('WaterPlane')
        mesh.from_pydata([(-70, -70, 0), (70, -70, 0), (70, 70, 0), (-70, 70, 0)],
                         [], [(0, 1, 2, 3)])
        mesh.update()
        water = bpy.data.objects.new('WaterPlane', mesh)
        wm = bpy.data.materials.get('ReviewWater') or bpy.data.materials.new('ReviewWater')
        wm.use_nodes = True
        bsdf = next(n for n in wm.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
        bsdf.inputs['Base Color'].default_value = (0.055, 0.11, 0.155, 1.0)
        bsdf.inputs['Roughness'].default_value = 0.12
        mesh.materials.append(wm)
    if water.name not in rig.objects:
        rig.objects.link(water)
    water.hide_render = not visible
    water.hide_viewport = not visible
    return water


def render(name, loc, target=(0, 0, 1.2), ortho=None, res=(1800, 760), roll=0):
    ensure_rig()
    cam = bpy.data.objects['ReviewCam']
    cam.location = Vector(loc)
    direction = Vector(target) - Vector(loc)
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    cam.rotation_euler.rotate_axis('Z', math.radians(roll))
    cam.data.type = 'ORTHO' if ortho else 'PERSP'
    if ortho:
        cam.data.ortho_scale = ortho
    else:
        cam.data.lens = 70
    cam.data.clip_end = 400
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.film_transparent = False
    os.makedirs(OUT, exist_ok=True)
    scene.render.filepath = OUT + '/' + name + '.png'
    bpy.ops.render.render(write_still=True)
    return scene.render.filepath


def views(prefix):
    """The standard control set: side, top, bow, stern, quarters, underside."""
    out = []
    out.append(render(prefix + '-side', (-70, 0, 1.2), ortho=36.5))
    out.append(render(prefix + '-top', (0, 0, 70), target=(0, 0, 0), ortho=36.5,
                      res=(1800, 620), roll=90))
    out.append(render(prefix + '-bottom', (0, 0, -70), target=(0, 0, 0), ortho=36.5,
                      res=(1800, 620), roll=90))
    out.append(render(prefix + '-bow', (0, 62, 1.6), target=(0, 0, 1.6), ortho=11.5,
                      res=(1100, 900)))
    out.append(render(prefix + '-stern', (0, -62, 1.6), target=(0, 0, 1.6), ortho=11.5,
                      res=(1100, 900)))
    out.append(render(prefix + '-front-quarter', (-34, 34, 11), target=(2, 4, 1.4),
                      res=(1500, 1000)))
    out.append(render(prefix + '-rear-quarter', (32, -33, 11), target=(-1, -4, 1.4),
                      res=(1500, 1000)))
    out.append(render(prefix + '-underside', (-26, 16, -14), target=(0, 6, -1.4),
                      res=(1500, 1000)))
    return out


def measure():
    """Report the evaluated bounds of every visible model mesh."""
    deps = bpy.context.evaluated_depsgraph_get()
    report = {}
    lo = [1e9] * 3
    hi = [-1e9] * 3
    for ob in model.objects:
        if ob.type != 'MESH':
            continue
        ev = ob.evaluated_get(deps)
        me = ev.to_mesh()
        pts = [ob.matrix_world @ v.co for v in me.vertices]
        if pts:
            omin = [min(p[i] for p in pts) for i in range(3)]
            omax = [max(p[i] for p in pts) for i in range(3)]
            report[ob.name] = {'tris': sum(len(p.vertices) - 2 for p in me.polygons),
                               'min': [round(v, 3) for v in omin],
                               'max': [round(v, 3) for v in omax]}
            for i in range(3):
                lo[i] = min(lo[i], omin[i])
                hi[i] = max(hi[i], omax[i])
        ev.to_mesh_clear()
    report['_overall'] = {'min': [round(v, 3) for v in lo],
                          'max': [round(v, 3) for v in hi],
                          'size': [round(hi[i] - lo[i], 3) for i in range(3)],
                          'tris': sum(v['tris'] for k, v in report.items() if k != '_overall')}
    return report
