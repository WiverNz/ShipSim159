"""Eight fixed inspection views; private references are not included."""
camera=scene.camera
camera.data.type='PERSP'; camera.data.lens=43
views={
 'port':((-31,0,4),(0,0,3)),
 'starboard':((31,0,4),(0,0,3)),
 'bow':((0,30,5),(0,0,3)),
 'stern':((0,-30,5),(0,0,3)),
 'bow-quarter':((-17,24,12),(0,0,2.5)),
 'stern-quarter':((17,-24,12),(0,0,2.5)),
 'top':((0,0,35),(0,0,0)),
 'bridge-interior':((-.98,3.23,5.05),(.55,4.55,4.45)),
}
for name,(pos,target) in views.items():
    camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.lens=24 if name=='bridge-interior' else 43
    camera.data.type='ORTHO' if name=='top' else 'PERSP'
    if name=='top': camera.data.ortho_scale=34
    scene.render.filepath=str(SOURCE/('Previews/blender-'+name+'.png'))
    bpy.ops.render.render(write_still=True)
print('Eight Blender inspection images saved')
