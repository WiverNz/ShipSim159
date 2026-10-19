"""1994 project 14352 appearance correction, run before export.

Controls follow Vympel's archive photo 478451 and the family arrangement.
Individual radii and panel dimensions are estimates, not surveyed offsets.
"""
exec(compile(open('G:/Projects/ShipSim159/Art/Source/Luch14352/resume.py',
                  encoding='utf-8').read(), 'resume.py', 'exec'))
tree = ast.parse(open(BASE+'/Art/Source/Luch14352/02_detail.py', encoding='utf-8').read())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n, ast.FunctionDef)],
                        type_ignores=[]), 'detail_helpers', 'exec'))

replace = ('Hull', 'Deck_Obnose', 'Roof_Saloon', 'Roof_Wheelhouse')
prefixes = ('Wheelhouse_Lower', 'Wheelhouse_Frame', 'Wheelhouse_Glass',
            'Wheelhouse_Cheek', 'Wheelhouse_FrontLower', 'Wheelhouse_Rear',
            'Windscreen_', 'Wiper_', 'Refit_', 'Railings_Bow')
for ob in list(model.objects):
    if ob.name in replace or ob.name.startswith(prefixes):
        bpy.data.objects.remove(ob, do_unlink=True)

# Rounded plan corners and a rising wet deck, retaining the published envelope.
stations = [(-11.56,1.62,-.10), (-10.8,1.925,-.21), (-8,1.925,-.21),
            (5.8,1.925,-.21), (8.8,1.92,-.12), (10.3,1.82,.02),
            (10.8,1.70,.12), (11.2,1.51,.25), (11.5,1.28,.36),
            (11.72,1.02,.44), (11.86,.80,.49)]
loft('Hull', [(y,[(0,low),(w*.9,low),(w,low+.16),(w,.59),(0,.59)])
              for y,w,low in stations], hullmat, True, .012)
loft('Deck_Obnose', [(y if y > -11.5 else -11.86,
    [(0,.59),(min(w+.34,2.265),.59),(min(w+.34,2.265),.68),(0,.68)])
    for y,w,_ in stations], white, True, .01)

# A shallow continuous camber replaces the three broad planar roof facets.
roof_ring = [(-1.80,2.08)]
for i in range(17):
    x = -1.84 + 3.68*i/16
    roof_ring.append((x,2.16+.11*(1-(x/1.84)**2)))
roof_ring.append((1.80,2.08))
loft('Roof_Saloon', [(y,roof_ring) for y in (-8.92,7.98)], white, bevel=.012)

# The same perimeter controls connect the lower body, glass and roof.
# Small front corner panes wrap around the bow instead of a square box corner.
bottom_half = [(0,10.48),(1.12,10.48),(1.48,10.32),(1.64,10.06),
               (1.64,8.80),(1.30,8.60),(0,8.60)]
sill_half = [(0,10.20),(1.10,10.20),(1.44,10.06),(1.57,9.82),
             (1.57,8.00),(1.25,7.90),(0,7.90)]
top_half = [(0,9.96),(1.06,9.96),(1.36,9.84),(1.47,9.62),
            (1.47,7.55),(1.17,7.48),(0,7.48)]
def full(half):
    return half + [(-x,y) for x,y in reversed(half[1:-1])]
bottom, sill, top = map(full, (bottom_half,sill_half,top_half))
n = len(bottom)
verts = [(x,y,z) for ring,z in ((bottom,.68),(sill,1.91)) for x,y in ring]
faces = [tuple(reversed(range(n))),tuple(range(n,2*n))]
faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
mesh('Refit_WheelhouseBody', verts, faces, white, .012)

for i in range(n):
    j = (i+1)%n
    corners = [Vector((*sill[i],1.91)),Vector((*sill[j],1.91)),
               Vector((*top[j],2.84)),Vector((*top[i],2.84))]
    width = (corners[1]-corners[0]).length
    if width < .52:
        prism('Refit_CornerPost_%02d'%i, corners, (0,0,-.035), white)
        continue
    def point(u,v):
        return corners[0].lerp(corners[1],u).lerp(corners[3].lerp(corners[2],u),v)
    # Matching rounded frame and glass borders, inset in the common panel plane.
    margin = min(.065/width,.18)
    outer_uv = rounded(.5,.5,1,1,.075,5)
    inner_uv = rounded(.5,.5,1-2*margin,.82,.065,5)
    outer = [point(.5+(u-.5)*.5/max(abs(u-.5),abs(v-.5)),
                   .5+(v-.5)*.5/max(abs(u-.5),abs(v-.5))) for u,v in outer_uv]
    inner = [point(u,v) for u,v in inner_uv]
    normal = (corners[1]-corners[0]).cross(corners[3]-corners[0]).normalized()
    center = sum(corners,Vector())/4
    if normal.dot(Vector((center.x,center.y-8.8,0))) < 0:
        normal = -normal
    ring3('Refit_BridgeFrame_%02d'%i, outer,inner,-normal*.055,white)
    prism('Refit_BridgeGlass_%02d'%i,[p-normal*.014 for p in inner],-normal*.008,glass)

roof = [(x*1.035, y, 2.85) for x,y in top]
prism('Roof_Wheelhouse',roof,(0,0,.09),white,.018)
# A closed swept shoulder overlaps the saloon wall and bridge rear body.
for sign in (-1,1):
    prism('Refit_Shoulder',[(sign*1.84,8.82,.68),
          (sign*1.70,7.91,2.15),(sign*1.48,7.48,2.84),
          (sign*1.57,8.0,1.91)],(-sign*.14,0,0),white,.009)
    points = [(sign*1.98,9.85),(sign*1.94,10.35),
              (sign*1.76,10.85),(sign*1.48,11.25),(sign*.66,11.69)]
    for x,y in points:
        tube('Railings_BowPost',(x,y,.69),(x,y,1.64),.022,white)
    for z in (1.12,1.64):
        for a,b in zip(points,points[1:]):
            tube('Railings_Bow',(*a,z),(*b,z),.022,white)

# Neutral glazing makes the interior readable without the previous opaque teal.
shader = next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
shader.inputs['Base Color'].default_value = (.11,.15,.16,1)
shader.inputs['Roughness'].default_value = .20
shader.inputs['Transmission Weight'].default_value = .35
print('LUCH_REFERENCE|rounded bow, continuous roof and swept glazed bridge rebuilt')
