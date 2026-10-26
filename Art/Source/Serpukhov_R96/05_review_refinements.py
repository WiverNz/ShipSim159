"""Corrections from the 1989 and 2010 photographs and the first Unity review."""
if scene.get('review_refinements'): raise RuntimeError('Refinements already applied')
for name in ['RaisedForecastle','MainDeck']:
    cutter=mesh('BowReliefCut',[(-1.65,8.9,-.2),(1.65,8.9,-.2),(0,8.9,1.9),
        (-1.65,10.7,-.2),(1.65,10.7,-.2),(0,10.7,1.9)],
        [(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],'Hull',None)
    cut(bpy.data.objects[name],cutter)
bpy.data.objects['BridgeFloor'].location.z+=.05
for o in groups['Wheelhouse_Interior'].objects:
    if o.name.startswith('Floorboard'): o.location.z+=.05
for o in scene.objects:
    if o.type=='MESH' and o.name.startswith(('BridgeMullion','FrontMullion','BridgeFrame','FrontFrame')):
        o.data.materials.clear(); o.data.materials.append(mats['Rubber'])
for s in (-1,1):
    for z in (3.95,4.25,4.55):
        aft=2.25+(z-3.66)*.68/1.99
        box('BridgeSideStiffener',(s*1.60,(aft+5)/2,z),(.035,5-aft,.035),'Wheelhouse_Exterior','House_Ivory',.008)
box('BridgeFrontStiffener',(0,5.035,3.98),(3.15,.035,.035),'Wheelhouse_Exterior','House_Ivory',.008)
bpy.data.objects['CameraNavigator'].location=(0,3.8,5.12)
for name,loc in [('SteeringWheel',(-.65,4.07,4.58)),('Waterjet_ReverseFlap_-1',(-.57,-10.49,.7)),('Waterjet_ReverseFlap_1',(.57,-10.49,.7))]:
    o=bpy.data.objects[name]; bpy.ops.object.select_all(action='DESELECT');o.select_set(True)
    bpy.context.view_layer.objects.active=o;scene.cursor.location=loc;bpy.ops.object.origin_set(type='ORIGIN_CURSOR')

# Collision references mirror the independent compound Unity boxes, baseline coordinates.
boxes=[('Midship',(0,-1,.70),(4.9,14,1.4)),('Forecastle',(0,8.6,1.22),(4,3.8,2.44)),
 ('Stern',(0,-8.5,.97),(3.9,3,.7)),('SternTip',(0,-10,1.2),(2,1.2,.4)),
 ('KneePort',(-1.97,10.82,1.96),(.26,.16,3.35)),('KneeStarboard',(1.97,10.82,1.96),(.26,.16,3.35)),
 ('House',(0,1.85,2.53),(3.3,5.7,2.14)),('Bridge',(0,3.8,4.64),(3.14,2.5,2))]
for name,loc,size in boxes:
    o=box('Collision_'+name,loc,size,'Collision_Reference',None,0)
    o.display_type='WIRE'; o.hide_render=True; o.hide_set(True)
for o in groups['Wheelhouse_Exterior'].objects:
    if o.type!='MESH': continue
    apply_mods(o)
    uv=o.data.uv_layers.active or o.data.uv_layers.new(name='UVMap')
    for poly in o.data.polygons:
        axis=max(range(3),key=lambda i:abs(poly.normal[i])); axes=[i for i in range(3) if i!=axis]
        for loop in poly.loop_indices:
            p=o.matrix_world@o.data.vertices[o.data.loops[loop].vertex_index].co
            uv.data[loop].uv=(p[axes[0]]/2,p[axes[1]]/2)
for img in list(bpy.data.images):
    if img.name.startswith('R96_'): img.pack()
    elif 'p96-page1' in img.name: bpy.data.images.remove(img)
scene['review_refinements']=True
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Serpukhov_R96.blend'))
