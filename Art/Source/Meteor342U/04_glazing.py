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
             'Wheelhouse', 'Wheelhouse_Glass', 'Wheelhouse_Cutters', 'SternScreen_Glass'):
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
        ob = new_mesh(name, self.vertices, self.faces, model, smooth=name in ('Superstructure', 'Windows_Glass', 'Windows_Frames'))
        ob.parent = root
        assign(ob, [material])
        recalc(ob)
        if name == 'Wheelhouse_Glass':
            bm = bmesh.new()
            bm.from_mesh(ob.data)
            for face in bm.faces:
                outward = face.calc_center_median() - Vector((0, y_of(8.65), 3.2))
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


shell, glass, frames = Parts(), Parts(), Parts()


def patch(a0, a1, h0, h1):
    ns = max(1, math.ceil((a1 - a0) / 0.35))
    levels = [h0] + [h for _, h in SUP_SECTION if h0 < h < h1] + [h1]
    for j in range(ns):
        a, b = a0 + (a1-a0)*j/ns, a0 + (a1-a0)*(j+1)/ns
        for low, high in zip(levels, levels[1:]):
            shell.face([surface(a, low), surface(b, low), surface(b, high), surface(a, high)])


def panel(a0, a1, sill, head, margin=0.085, radius=0.09):
    # The ring has a rounded aperture inside a rectangular cabin patch.
    left, right = a0 + margin, a1 - margin
    cx, cy = (left+right)/2, (sill+head)/2
    hw, hh = (right-left)/2, (head-sill)/2
    r = min(radius, hw*0.98, hh*0.98)
    outline = []
    for ox, oy, start in ((1,1,0), (-1,1,90), (-1,-1,180), (1,-1,270)):
        for i in range(6):
            ang = math.radians(start + i*90/5)
            outline.append((cx + ox*(hw-r) + r*math.cos(ang),
                            cy + oy*(hh-r) + r*math.sin(ang)))
    # Include rays through every patch corner so the surround has no clipped corners.
    def cross(a, b):
        return a[0]*b[1] - a[1]*b[0]
    complete = []
    for i, p in enumerate(outline):
        q = outline[(i+1) % len(outline)]
        edge = (q[0]-p[0], q[1]-p[1])
        origin = (p[0]-cx, p[1]-cy)
        splits = [(0, p)]
        for corner in ((a0,0), (a1,0), (a1,2.52), (a0,2.52)):
            direction = (corner[0]-cx, corner[1]-cy)
            denominator = cross(direction, edge)
            if abs(denominator) < 1e-10:
                continue
            t = cross(origin, edge) / denominator
            u = cross(origin, direction) / denominator
            if t > 0 and 1e-7 < u < 1-1e-7:
                splits.append((u, (p[0]+u*edge[0], p[1]+u*edge[1])))
        complete.extend(point for _, point in sorted(splits))
    outline = complete
    outer, rim, pane, outer_uv = [], [], [], []
    for a, h in outline:
        dx, dy = a-cx, h-cy
        tx = ((a1 if dx > 0 else a0)-cx)/dx if abs(dx)>1e-8 else 1e8
        ty = ((2.52 if dy > 0 else 0)-cy)/dy if abs(dy)>1e-8 else 1e8
        t = min(tx, ty)
        outer_uv.append((cx+dx*t, cy+dy*t))
        outer.append(surface(cx+dx*t, cy+dy*t))
        rim.append(surface(a, h, 0.018))
        pane.append(surface(cx+dx*(1-0.028/hw), cy+dy*(1-0.028/hh), 0.014))
    center = surface(cx, cy, 0.014)
    for i in range(len(outline)):
        j=(i+1)%len(outline)
        # Tessellate the reveal surround on the cabin surface, including its camber.
        corners = [outer_uv[i], outer_uv[j], outline[j], outline[i]]
        def uv(u,v):
            return tuple((1-v)*((1-u)*corners[0][k]+u*corners[1][k]) +
                         v*((1-u)*corners[3][k]+u*corners[2][k]) for k in (0,1))
        for row in range(5):
            for col in range(3):
                coords=[uv(col/3,row/5),uv((col+1)/3,row/5),
                        uv((col+1)/3,(row+1)/5),uv(col/3,(row+1)/5)]
                shell.face([surface(a,h) for a,h in coords])
        frames.face([rim[i], rim[j], pane[j], pane[i]])
        p0=(cx+(outline[i][0]-cx)*(1-.028/hw), cy+(outline[i][1]-cy)*(1-.028/hh))
        p1=(cx+(outline[j][0]-cx)*(1-.028/hw), cy+(outline[j][1]-cy)*(1-.028/hh))
        def gp(p,t):
            return surface(cx+(p[0]-cx)*t,cy+(p[1]-cy)*t,.014)
        glass.face([center,gp(p0,.25),gp(p1,.25)])
        for row in range(1,4):
            glass.face([gp(p0,row/4),gp(p0,(row+1)/4),
                        gp(p1,(row+1)/4),gp(p1,row/4)])


# The raised bow saloon has nearly full-height wrap-around glazing.
last = SUP_FWD
bow_edges = [1.74, 2.08, 2.62, 3.32, 4.15, 5.02, 5.86, 6.68]
patch(last, bow_edges[0], 0, 2.52)
for a, b in zip(bow_edges, bow_edges[1:]):
    panel(a, b, 0.11, 2.47, margin=0.042, radius=0.075)
last=bow_edges[-1]
# Boarding door and seven large windows in the middle saloon.
patch(last, 6.90, 0, 2.52)
panel(6.90, 8.08, 0.05, 2.45, margin=0.075, radius=0.13)
patch(8.08, 9.55, 0, 2.52)
for i in range(7):
    a=9.55+i*1.08
    panel(a, a+1.08, 0.34, 1.95, margin=0.085, radius=0.14)
patch(17.11, 18.03, 0, 2.52)
# The circular machinery port has its own local, undistorted round ring.
a0,a1=18.03,18.65
panel(a0,a1,1.14,1.82,margin=0.025,radius=0.28)
patch(a1, 23.60, 0, 2.52)
for i in range(5):
    a=23.60+i*1.02
    panel(a, a+1.02, 0.34, 1.95, margin=0.085, radius=0.14)
patch(28.70, SUP_AFT, 0, 2.52)
patch(SUP_FWD, SUP_AFT, 2.52, SUP_SECTION[-1][1])
shell.build('Superstructure', HULL)
glass.build('Windows_Glass', GLASS)
frames.build('Windows_Frames', STEEL)

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

# The after saloon closes under the visor with a raked wrap-around screen.
screen=Parts()
for i in range(8):
    x0=lerp_table(B_SUP,SUP_AFT)*i/8
    x1=lerp_table(B_SUP,SUP_AFT)*(i+1)/8
    z0=deck_z(SUP_AFT)+.22
    z1=roof_crown_z(SUP_AFT)-.27
    screen.face([(x0,y_of(SUP_AFT),z0),(x1-.045,y_of(SUP_AFT),z0),
                 ((x1-.045)*.88,y_of(SUP_AFT)-.30,z1),
                 (x0*.88,y_of(SUP_AFT)-.30,z1)])
screen.build('SternScreen_Glass',GLASS)

assert len(glass.faces) > 500, 'Saloon glazing was overwritten'
assert len(wh.faces) > 40 and len(wg.faces) == len(base_plan)
print('PASS4|explicit shell, rounded glazing and closed swept wheelhouse built')
