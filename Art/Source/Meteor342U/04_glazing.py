"""Build curved saloon glazing and the swept wheelhouse as explicit surfaces.

Panel rings share the cabin surface with their window reveals. This avoids
Boolean operations on an open half-shell and keeps glass face indices local.
Window sizes and pitch are estimates from Meteor-191 photographs.
"""
import bpy, math
from mathutils import Vector

exec(compile(open('G:/Projects/ShipSim159/Art/Source/Meteor342U/common.py',
                  encoding='utf-8').read(), 'common.py', 'exec'))
model = bpy.data.collections['Meteor342U_Model']
root = bpy.data.objects['Meteor342U_ROOT']
HULL = bpy.data.materials['Meteor_HullPaint']
GLASS = bpy.data.materials['Meteor_Glass']
STEEL = bpy.data.materials['Meteor_Steel']
DARK = bpy.data.materials['Meteor_InteriorDark']
for name in ('Superstructure', 'Windows_Glass', 'Windows_Frames', 'Windows_Cutters',
             'Wheelhouse', 'Wheelhouse_Glass', 'Wheelhouse_Cutters', 'SternScreen_Glass',
             'SternScreen_Frames', 'Cabin_FrontClosure', 'Paint_SheerBand'):
    ob = bpy.data.objects.get(name)
    if ob:
        bpy.data.objects.remove(ob, do_unlink=True)


def surface(a, h, offset=0.0):
    for (f0, h0), (f1, h1) in zip(SUP_SECTION, SUP_SECTION[1:]):
        if h <= h1:
            t = (h - h0) / (h1 - h0)
            return Vector(sup_xyz(a, f0 + (f1 - f0) * t, h)) + Vector((offset, 0, 0))
    return Vector(sup_xyz(a, 0, SUP_SECTION[-1][1]))


class Parts:
    def __init__(self):
        self.vertices, self.faces = [], []

    def face(self, points):
        start = len(self.vertices)
        self.vertices.extend(tuple(p) for p in points)
        self.faces.append(tuple(range(start, start + len(points))))

    def build(self, name, material, mirror=True, thickness=None):
        ob = new_mesh(name, self.vertices, self.faces, model, smooth=name in ('Superstructure', 'Windows_Glass', 'Windows_Frames', 'Paint_SheerBand'))
        ob.parent = root
        assign(ob, [material])
        recalc(ob)
        if name in ('Superstructure', 'Windows_Glass', 'Windows_Frames', 'Wheelhouse_Glass',
                    'Cabin_FrontClosure', 'SternScreen_Glass', 'SternScreen_Frames', 'Paint_SheerBand'):
            bm = bmesh.new()
            bm.from_mesh(ob.data)
            for face in bm.faces:
                outward = (face.calc_center_median() - Vector((0, y_of(8.65), 3.2))
                           if name == 'Wheelhouse_Glass' else
                           Vector((0,1,0)) if name == 'Cabin_FrontClosure' else
                           Vector((0,-1,0)) if name.startswith('SternScreen') else Vector((1,0,0)))
                if face.normal.dot(outward) < 0:
                    face.normal_flip()
            bm.to_mesh(ob.data)
            bm.free()
        if mirror:
            add_mirror(ob)
        if thickness:
            sol = ob.modifiers.new('Panel thickness', 'SOLIDIFY')
            sol.thickness = thickness
            sol.offset = -1.0
        return ob


# One constrained triangulation gives glass, seals and shell identical seams.
# Grid constraints preserve every bend in the cabin's longitudinal and section tables.
from mathutils.geometry import delaunay_2d_cdt

shell, glass, frames, paint = Parts(), Parts(), Parts(), Parts()
windows = []


def panel(a0, a1, sill, head, margin=.085, radius=.09):
    windows.append((a0 + margin, a1 - margin, sill, head, radius))


bow_edges = [1.74, 2.08, 2.62, 3.32, 4.15, 5.02, 5.86, 6.68]
for a, b in zip(bow_edges, bow_edges[1:]):
    panel(a, b, .11, 2.47, margin=.042, radius=.075)
panel(6.90, 8.08, .05, 2.45, margin=.075, radius=.13)
for i in range(7):
    panel(9.55+i*1.08, 9.55+(i+1)*1.08, .34, 1.95, radius=.14)
panel(18.03,18.65,1.14,1.82,margin=.025,radius=.28)
for i in range(5):
    panel(23.60+i*1.02,23.60+(i+1)*1.02,.34,1.95,radius=.14)


def rounded(bounds, inset=0):
    left,right,bottom,top,radius=bounds
    left+=inset; right-=inset; bottom+=inset; top-=inset
    r=min(max(radius-inset,.005),(right-left)/2,(top-bottom)/2)
    points=[]
    for x,y,angle in ((right-r,top-r,0),(left+r,top-r,90),
                       (left+r,bottom+r,180),(right-r,bottom+r,270)):
        for i in range(7):
            t=math.radians(angle+i*15)
            points.append((x+r*math.cos(t),y+r*math.sin(t)))
    return points


def inside(point, polygon):
    x,y=point
    return all((q[0]-p[0])*(y-p[1])-(q[1]-p[1])*(x-p[0])>=-1e-8
               for p,q in zip(polygon,polygon[1:]+polygon[:1]))


uvs,edges=[],[]

def chain(points, closed=False):
    base=len(uvs)
    uvs.extend(Vector(p) for p in points)
    edges.extend((base+i,base+i+1) for i in range(len(points)-1))
    if closed:
        edges.append((base+len(points)-1,base))


height=SUP_SECTION[-1][1]
na=math.ceil((SUP_AFT-SUP_FWD)/.20)
asamples=sorted(set([SUP_FWD+(SUP_AFT-SUP_FWD)*i/na for i in range(na+1)]+
                    [a for table in (B_SUP,Z_DECK,H_SCALE) for a,_ in table
                     if SUP_FWD<=a<=SUP_AFT]))
hsamples=sorted(set([height*i/24 for i in range(25)]+[h for _,h in SUP_SECTION]))
for a in asamples:
    chain([(a,0),(a,height)])
for h in hsamples:
    chain([(SUP_FWD,h),(SUP_AFT,h)])
rings=[]
for window in windows:
    outer,inner=rounded(window),rounded(window,.028)
    chain(outer,True); chain(inner,True)
    rings.append((outer,inner))
coords,_,triangles,_,_,_=delaunay_2d_cdt(uvs,edges,[],0,1e-7)
for triangle in triangles:
    centre=sum((coords[i] for i in triangle),Vector((0,0)))/len(triangle)
    part=paint if 2.52 < centre.y < 2.64 else shell
    for outer,inner in rings:
        if inside(centre,outer):
            part=glass if inside(centre,inner) else frames
            break
    points=[surface(*coords[i]) for i in triangle]
    if (points[1]-points[0]).cross(points[2]-points[0]).x<0:
        points.reverse()
    part.face(points)
shell.build('Superstructure',HULL)
glass.build('Windows_Glass',GLASS)
frames.build('Windows_Frames',STEEL)
paint.build('Paint_SheerBand',bpy.data.materials['Meteor_SecondaryPaint'])

# Close the narrow forward end; the mirror seam is a real welded edge.
front=Parts()
for low,high in zip(hsamples,hsamples[1:]):
    p,q=surface(SUP_FWD,low),surface(SUP_FWD,high)
    front.face([(0,p.y,p.z),p,q,(0,q.y,q.z)])
front.build('Cabin_FrontClosure',HULL)

# A closed, swept wheelhouse with real front, rear, side and roof panels.
# Each cross-section has the same perimeter topology, including chamfered corners.
base_half = [(0,6.68), (0.80,6.75), (1.28,7.45), (1.40,8.25),
             (1.40,9.15), (1.30,9.85), (0.94,10.65), (0,10.76)]
top_half = [(0,7.40), (0.75,7.43), (1.12,7.78), (1.20,8.30),
            (1.20,9.12), (1.12,9.57), (0.80,9.98), (0,10.02)]
base_plan=base_half + [(-x,a) for x,a in reversed(base_half[1:-1])]
top_plan=top_half + [(-x,a) for x,a in reversed(top_half[1:-1])]
wh, wg = Parts(), Parts()


def wp(i,t):
    x0,a0=base_plan[i]; x1,a1=top_plan[i]
    return Vector((x0+(x1-x0)*t, y_of(a0+(a1-a0)*t), WH_BASE+(WH_TOP-WH_BASE)*t))


for i in range(len(base_plan)):
    j=(i+1)%len(base_plan)
    wh.face([wp(i,0),wp(j,0),wp(j,.36),wp(i,.36)])
    wh.face([wp(i,.92),wp(j,.92),wp(j,1),wp(i,1)])
    bl,br,tr,tl=wp(i,.36),wp(j,.36),wp(j,.92),wp(i,.92)
    width=(br-bl).length
    f=min(.06/max(width,.01),.22)
    il,ir=bl.lerp(br,f),bl.lerp(br,1-f)
    itl,itr=tl.lerp(tr,f),tl.lerp(tr,1-f)
    wh.face([bl,il,itl,tl]); wh.face([ir,br,tr,itr])
    wg.face([il,ir,itr,itl])
wh.face([wp(i,1) for i in range(len(base_plan))])
wh.face(list(reversed([wp(i,0) for i in range(len(base_plan))])))
wh.build('Wheelhouse',HULL,mirror=False,thickness=.035)
wg.build('Wheelhouse_Glass',GLASS,mirror=False)

# Close the aft saloon on its actual curved section, including sills and mullions.
screen,back=Parts(),Parts()
levels=sorted(set([h for _,h in SUP_SECTION]+[.22,2.30]))
for low,high in zip(levels,levels[1:]):
    p,q=surface(SUP_AFT,low),surface(SUP_AFT,high)
    for i in range(8):
        for f0,f1,is_glass in ((i/8,(i+.91)/8,True),((i+.91)/8,(i+1)/8,False)):
            part=screen if is_glass and low>=.22 and high<=2.30 else back
            part.face([(p.x*f0,p.y,p.z),(p.x*f1,p.y,p.z),
                       (q.x*f1,q.y,q.z),(q.x*f0,q.y,q.z)])
screen.build('SternScreen_Glass',GLASS)
back.build('SternScreen_Frames',HULL)
assert len(glass.faces)>500, 'Saloon glazing was overwritten'
print('PASS4|shared curved cabin seams, closed ends and wheelhouse built')
