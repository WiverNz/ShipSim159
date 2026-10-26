import bpy,math
from pathlib import Path
from mathutils import Vector
scene=bpy.data.scenes['MN52_T77'];bpy.context.window.scene=scene
directory=Path('G:/Projects/ShipSim159/Art/Source/MN52_T77')
review=bpy.data.collections['MN52_Review']
if not scene.camera:
 data=bpy.data.cameras.new('MN52_ReviewCamera');camera=bpy.data.objects.new(data.name,data);review.objects.link(camera);scene.camera=camera
 for name,energy,angle in [('Key',2.4,(30,-25,-35)),('Fill',1,(55,0,125))]:
  data=bpy.data.lights.new('MN52_'+name,'SUN');data.energy=energy;data.angle=.15;o=bpy.data.objects.new(data.name,data);review.objects.link(o);o.rotation_euler=tuple(math.radians(v) for v in angle)
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('MN52_ReviewWorld') if not scene.world else scene.world;scene.world.color=(.23,.26,.30)
scene.render.image_settings.file_format='PNG'
for name,pos in {'starboard':(38,0,8),'port':(-38,0,8),'bow':(0,38,9),'stern':(0,-38,9),'bow-quarter':(26,34,20),'stern-quarter':(-26,-34,19),'top':(0,0,50),'deck':(8,10,9)}.items():
 camera=scene.camera;camera.location=pos;camera.rotation_euler=(Vector((0,0,3.2))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=43
 camera.data.type='ORTHO' if name=='top' else 'PERSP';camera.data.ortho_scale=50
 if name=='deck':camera.rotation_euler=(Vector((0,-3,2.5))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.lens=32
 scene.render.filepath=str(directory/'Previews'/('blender-'+name+'.png'));bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(directory/'MN52_T77.blend'),{scene},path_remap='RELATIVE',fake_user=True)
print('Eight inspection renders saved')
