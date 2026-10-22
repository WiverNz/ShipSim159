"""Export the closed rig and one baked review clip, preserving playable assets."""
import bpy
import json
from pathlib import Path
from mathutils import Vector

ROOT=Path('G:/Projects/ShipSim159')
SOURCE=ROOT/'Art/Source/VolgoDon507B'
TARGET=ROOT/'Assets/ShipSimulator/Models/VolgoDon507B_Improved'
scene=bpy.data.scenes['VolgoDon507B_Source']
report=json.load(open(SOURCE/'part4-validation.json'));assert report['status']=='PASS'
manifest=json.load(open(SOURCE/'mechanisms.json'))
manifest.update(fps=24,frame_start=1,frame_end=770,
    timing='Accelerated review sequence, not calibrated machinery rates',
    unity_axes='+X starboard, +Z bow, +Y up',
    import_note='FBX root aligned by existing datums; preserve hierarchy and sample the review clip')
for spec in manifest['mechanisms']:
    x,y,z=spec['rest_position'];spec['unity_rest_position']=[x,z,y]
    x,y,z=spec['source_axis']
    spec['unity_axis']=[-x,-z,-y] if spec['kind']=='rotation' else [x,z,y]
for path in [SOURCE/'mechanisms.json',TARGET/'mechanisms.json']:
    with open(path,'w') as f:json.dump(manifest,f,indent=2)
materials=json.load(open(TARGET/'materials.json'))
materials.update(stage=4,triangles=report['triangles'],meshCount=report['mesh_count'])
with open(TARGET/'materials.json','w') as f:json.dump(materials,f,indent=2)

camera=scene.camera;sun=next(o for o in scene.objects if o.type=='LIGHT')
scene.render.resolution_percentage=100
views=[('closed',1,(100,100,85),(0,0,3),154),
       ('lift',100,(30,-10,17),(0,-28,3),36),
       ('slide-under',150,(30,-10,17),(0,-28,3),50),
       ('both-half-open',210,(85,25,95),(0,5,0),140),
       ('fore-open',300,(85,25,95),(0,5,0),140),
       ('aft-open',480,(85,25,95),(0,5,0),140),
       ('nozzles-port',30,(9,-74,-1),(0,-64.65,-2),13),
       ('nozzles-starboard',60,(9,-74,-1),(0,-64.65,-2),13),
       ('anchors-unseated',740,(12,80,6),(0,65,1.5),12)]
for name,frame,pos,target,scale in views:
    scene.frame_set(frame);camera.location=pos
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=scale;sun.rotation_euler=camera.rotation_euler.copy();sun.rotation_euler.x+=.25
    scene.render.filepath=str(SOURCE/'Previews'/('part4-'+name+'.png'))
    bpy.ops.render.render(write_still=True)
scene.frame_set(1);scene.render.fps=24
for o in scene.objects:o.select_set(o.type in {'MESH','EMPTY'})
bpy.ops.export_scene.fbx(filepath=str(TARGET/'VolgoDon507B.fbx'),use_selection=True,
    object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
    apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=False,add_leaf_bones=False,
    bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,
    bake_anim_simplify_factor=0,use_custom_props=True,path_mode='RELATIVE')
camera.location=(110,114,80)
camera.rotation_euler=(Vector((0,0,3))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale=154
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'VolgoDon507B.blend'))
print('Part 4 source and animated candidate exported')
