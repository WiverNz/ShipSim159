"""Run in the live Blender MCP session. Dimensions in metres, +Y bow, +Z up."""
import bpy
import bmesh
import math
import os
from mathutils import Vector, Quaternion

BASE = 'G:/Projects/ShipSim159'
OUT = BASE + '/Logs/Luch14352'
os.makedirs(OUT, exist_ok=True)

def enum(owner, prop, value):
    items = owner.bl_rna.properties[prop].enum_items
    assert value in {i.identifier for i in items}, (prop, value)
    setattr(owner, prop, value)

scene = bpy.data.scenes.new('Luch14352_Studio')
bpy.context.window.scene = scene
enum(scene.unit_settings, 'system', 'METRIC')
scene.unit_settings.scale_length = 1
model = bpy.data.collections.new('Luch14352_Model')
scene.collection.children.link(model)
root = bpy.data.objects.new('Luch14352_ROOT', None)
model.objects.link(root)
root['coordinates'] = 'Metres. +Y bow, +X starboard, +Z up. Origin at loaded waterline.'
root['baseline_z'] = -0.66
root['source'] = 'FleetPhoto 478451, updated project 14352, approximately 1994'

def material(name, color, metallic=0, roughness=.4):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    p = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    p.inputs['Base Color'].default_value = (*color, 1)
    p.inputs['Metallic'].default_value = metallic
    p.inputs['Roughness'].default_value = roughness
    return m

white = material('Paint_Ivory', (.79,.81,.77), .12, .34)
blue = material('Paint_DeckTeal', (.065,.24,.27), .15, .4)
hullmat = material('Paint_HullCharcoal', (.035,.048,.055), .22, .32)
rubber = material('Skeg_Rubber', (.018,.022,.024), 0, .73)
metal = material('Rail_Aluminium', (.48,.53,.55), .75, .29)
dark = material('Vent_Dark', (.025,.035,.04), .2, .6)
glass = material('Glass_BlueGrey', (.07,.16,.19), .12, .13)
p = next(n for n in glass.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
p.inputs['Transmission Weight'].default_value = .48
p.inputs['IOR'].default_value = 1.46
inside = material('Interior_Charcoal', (.055,.075,.078), 0, .85)
seatmat = material('Interior_Seats', (.13,.22,.25), 0, .8)

def mesh(name, vertices, faces, mat, bevel=0):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    bm = bmesh.new(); bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data); bm.free()
    o = bpy.data.objects.new(name, data)
    model.objects.link(o); o.parent = root
    o.data.materials.append(mat)
    if bevel:
        b = o.modifiers.new('Edge highlights', 'BEVEL')
        b.width = bevel; b.segments = 3
    return o

def box(name, center, size, mat, bevel=.012):
    x,y,z=center; a,b,c=[v/2 for v in size]
    vs=[(x+i*a,y+j*b,z+k*c) for i,j,k in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    return mesh(name,vs,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],mat,bevel)

def loft(name, stations, mat, mirror=False, bevel=.015):
    # Each station is (longitudinal Y, [(X,Z), ...]), with identical ring topology.
    n=len(stations[0][1]); vs=[(x,y,z) for y,ring in stations for x,z in ring]
    fs=[]
    for s in range(len(stations)-1):
        for i in range(n if not mirror else n-1):
            j=(i+1)%n; a=s*n; b=a+n
            fs.append((a+i,a+j,b+j,b+i))
    fs += [tuple(range(n-1,-1,-1)),tuple((len(stations)-1)*n+i for i in range(n))]
    o=mesh(name,vs,fs,mat,0)
    if mirror:
        m=o.modifiers.new('Port starboard symmetry','MIRROR')
        m.use_axis=(True,False,False); m.use_clip=True
    if bevel:
        m=o.modifiers.new('Edge highlights','BEVEL');m.width=bevel;m.segments=3
    return o

# A wet deck spans the skegs. There is deliberately no central displacement keel.
stations=[(-11.56,1.62,-.1),(-10.8,1.925,-.21),(-8,1.925,-.21),(5.8,1.925,-.21),(8.8,1.92,-.12),(10.55,1.7,.08),(11.86,1.22,.49)]
loft('Hull',[(y,[(0,low),(w*.9,low),(w,low+.16),(w,.59),(0,.59)]) for y,w,low in stations],hullmat,True)
loft('Deck_Obnose',[(y,[(0,.59),(min(w+.34,2.265),.59),(min(w+.34,2.265),.68),(0,.68)]) for y,w,_ in stations],white,True,.01)
for side,label in [(1,'R'),(-1,'L')]:
    rings=[]
    for y,w,bottom in [(-11.4,1.60,-.55),(-10.5,1.78,-.66),(-7,1.78,-.66),(6.4,1.78,-.66),(8.8,1.72,-.56),(10.5,1.42,.10)]:
        rings.append((y,[(side*(w-.23),bottom+.08),(side*(w+.12),bottom),(side*(w+.145),-.19 if y<8 else .23),(side*(w-.30),-.16 if y<8 else .25)]))
    loft('Skeg_'+label,rings,rubber,False,.025)

box('Blockout_Saloon',(0,1.75,1.42),(3.68,13.7,1.48),white)
loft('EngineRoom',[(y,[(-w,.68),(w,.68),(top,2.05),(-top,2.05)]) for y,w,top in [(-10,1.77,1.28),(-8.9,1.84,1.65),(-5.1,1.84,1.65)]],white)
loft('Blockout_Wheelhouse',[(7.7,[(-1.45,2.06),(1.45,2.06),(1.53,2.86),(-1.53,2.86)]),(10.48,[(-1.62,.68),(1.62,.68),(1.53,2.86),(-1.53,2.86)])],white)
loft('Roof_Saloon',[(y,[(-1.71,2.05),(-1.76,2.15),(-1.2,2.23),(0,2.27),(1.2,2.23),(1.76,2.15),(1.71,2.05)]) for y in [-8.9,7.95]],blue)
box('Roof_Wheelhouse',(0,9.08,2.90),(3.35,3.1,.08),white,.025)

# References are viewport-only and kept outside the exported model collection.
refs=bpy.data.collections.new('ReferenceImages_NotExported');scene.collection.children.link(refs)
for i,filename in enumerate(['n201.jpg','2012.jpg','2022.jpg','arrangement.jpg']):
    ob=bpy.data.objects.new('Reference_'+filename,None);refs.objects.link(ob)
    enum(ob,'empty_display_type','IMAGE')
    ob.data=bpy.data.images.load(OUT+'/References/'+filename,check_existing=True)
    ob.empty_display_size=23.72;ob.location=(8+i*3,0,3)
    ob.rotation_euler=(math.pi/2,0,math.pi/2)
    ob.hide_render=True; ob.hide_viewport=True

world=bpy.data.worlds.new('Luch_StudioWorld');scene.world=world;world.use_nodes=True
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs[0].default_value=(.17,.21,.26,1)
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs[1].default_value=.55
studio=bpy.data.collections.new('Studio_NotExported');scene.collection.children.link(studio)
for name,loc,power,size in [('Key',(-8,8,15),2300,12),('Fill',(10,0,9),1700,10),('Rim',(0,-12,10),2100,8)]:
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc
    o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('ReviewCamera',bpy.data.cameras.new('ReviewCamera'));studio.objects.link(cam);scene.camera=cam
enum(cam.data,'type','ORTHO');cam.data.ortho_scale=28
scene.render.resolution_x=1600;scene.render.resolution_y=900;scene.render.resolution_percentage=100
enum(scene.render.image_settings,'file_format','PNG')
scene.render.film_transparent=False

def view(name,loc,target=(0,0,1)):
    cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=OUT+'/'+name+'.png'
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion()
            area.spaces.active.region_3d.view_distance=28
            area.spaces.active.region_3d.view_location=Vector(target)
            area.spaces.active.region_3d.view_perspective='ORTHO'
            area.spaces.active.shading.color_type='MATERIAL'
    bpy.ops.render.render(write_still=True)

view('01-blockout-port',(-30,0,6))
bpy.ops.wm.save_as_mainfile(filepath=BASE+'/Art/Source/Luch14352/Luch14352.blend')
print('Blockout saved. Hull overall length 23.72 m; outside deck beam 4.53 m.')
