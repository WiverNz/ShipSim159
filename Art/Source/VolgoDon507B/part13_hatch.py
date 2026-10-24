"""Part 13, run once through registered Blender MCP after part13_windlass.py.

Part 4 keyed the forward outer cover to rise 1.05 m and move 1.27 m aft at the same time (frames 90-100), and
the reverse at the end of closing (680-710). In the first frames it is still at its seal and drives into the
forward inner cover. The archive sequence (p4) raises the end covers on the vertical jacks first; the cover
now rises clear, then makes its 1.27 m shift while raised (frames 100-120, during the inner covers' rolling
lift), and on closing shifts back before it is lowered (660-680). Only the Y keys at frames 100 and 680 change.
"""
import bpy
import json
from pathlib import Path

SOURCE = Path('G:/Projects/ShipSim159/Art/Source/VolgoDon507B')
scene = bpy.data.scenes['VolgoDon507B_Source']
bpy.context.window.scene = scene
assert scene.get('part13_windlass') and not scene.get('part13_hatch')
cover = bpy.data.objects['Hatch_ForeOuter']
changed = []
for layer in cover.animation_data.action.layers:
    for strip in layer.strips:
        for bag in strip.channelbags:
            for curve in bag.fcurves:
                if curve.data_path != 'location' or curve.array_index != 1:
                    continue
                rest = curve.evaluate(1)
                for key in curve.keyframe_points:
                    if round(key.co.x) in (100, 680):
                        changed.append((round(key.co.x), round(key.co.y - rest, 4)))
                        key.co.y = rest
                        key.handle_left.y = rest
                        key.handle_right.y = rest
                curve.update()
assert sorted(f for f, _ in changed) == [100, 680], changed
for path in [SOURCE / 'mechanisms.json',
             Path('G:/Projects/ShipSim159/Assets/ShipSimulator/Models/VolgoDon507B_Improved/mechanisms.json')]:
    manifest = json.loads(path.read_text())
    for state in manifest['states']:
        if state['name'] in ('lift_outer', 'lower_inner'):
            state['offsets']['Hatch_ForeOuter'] = [0, 0, 1.05]
    path.write_text(json.dumps(manifest, indent=2))
scene['part13_hatch'] = True
scene.frame_set(1)
print('PART13_HATCH', changed)
