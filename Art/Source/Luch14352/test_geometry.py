"""Prove the shell and forward-glazing guards on temporary mesh copies."""
exec(compile(open('G:/Projects/ShipSim159/Art/Source/Luch14352/validate_model.py',
                  encoding='utf-8').read(),'validate_model.py','exec'))

for name,reason in (('Hull','open shell'),('Windows_L_8','Missing forward glazing')):
    ob = bpy.data.objects[name]
    original = ob.data
    trial = original.copy()
    ob.data = trial
    try:
        bm = bmesh.new(); bm.from_mesh(trial)
        targets = list(bm.faces)[:1] if name == 'Hull' else list(bm.faces)
        bmesh.ops.delete(bm,geom=targets,context='FACES')
        bm.to_mesh(trial); bm.free()
        bpy.context.view_layer.update()
        try:
            validate()
        except AssertionError as error:
            assert reason in str(error), str(error)
            print('LUCH_REGRESSION|PASS: '+reason)
        else:
            raise AssertionError('Guard failed: '+name)
    finally:
        ob.data = original
        bpy.data.meshes.remove(trial)
        bpy.context.view_layer.update()
validate()
