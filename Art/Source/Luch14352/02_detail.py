"""Second live modelling pass, after visual inspection of the blockout."""
exec(compile(open('G:/Projects/ShipSim159/Art/Source/Luch14352/resume.py',encoding='utf-8').read(),'resume.py','exec'))
for name in ['Blockout_Saloon','Blockout_Wheelhouse','Roof_Wheelhouse']:
    if bpy.data.objects.get(name): bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)

def tube(name, a, b, radius, mat=metal, segments=10):
    a,b=Vector(a),Vector(b);axis=(b-a).normalized()
    u=axis.cross(Vector((0,0,1)))
    if u.length<.01:u=axis.cross(Vector((0,1,0)))
    u.normalize();v=axis.cross(u)
    verts=[tuple(p+radius*(math.cos(i*2*math.pi/segments)*u+math.sin(i*2*math.pi/segments)*v)) for p in (a,b) for i in range(segments)]
    faces=[(i,(i+1)%segments,(i+1)%segments+segments,i+segments) for i in range(segments)]
    faces += [tuple(range(segments-1,-1,-1)),tuple(range(segments,2*segments))]
    o=mesh(name,verts,faces,mat)
    for f in o.data.polygons:
        f.use_smooth=len(f.vertices)==4
    return o

def rounded(y,z,w,h,r=.12,n=6):
    points=[]
    for cy,cz,start in [(y+w/2-r,z+h/2-r,0),(y-w/2+r,z+h/2-r,90),(y-w/2+r,z-h/2+r,180),(y+w/2-r,z-h/2+r,270)]:
        for j in range(n+1):
            a=math.radians(start+j*90/n)
            points.append((cy+r*math.cos(a),cz+r*math.sin(a)))
    return points

def side_x(z): return 1.84-(z-.68)*(.17/1.47)

def prism(name, polygon, offset, mat, bevel=0):
    v=[Vector(p) for p in polygon];d=Vector(offset);n=len(v)
    return mesh(name,[tuple(p) for p in v]+[tuple(p+d) for p in v],
        [tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],mat,bevel)

def ring3(name,outer,inner,offset,mat):
    n=len(outer);d=Vector(offset)
    vs=[tuple(p) for p in outer]+[tuple(p) for p in inner]+[tuple(Vector(p)+d) for p in outer]+[tuple(Vector(p)+d) for p in inner]
    fs=[]
    for i in range(n):
        j=(i+1)%n
        fs.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
    return mesh(name,vs,fs,mat,.004)

def apply_modifier(ob,mod):
    bpy.context.view_layer.objects.active=ob
    bpy.ops.object.modifier_apply(modifier=mod.name)

for side,label in [(-1,'L'),(1,'R')]:
    wall=prism('Superstructure_'+label,[(side*side_x(z),y,z) for y,z in [(-5.1,.68),(8.8,.68),(7.91,2.15),(-5.1,2.15)]],(-side*.06,0,0),white)
    openings=[(-4.42+i*1.29,1.39,1.15,1.02) for i in range(8)]+[(7.3,1.39,1.18,1.02)]
    for i,(y,z,w,h) in enumerate(openings):
        contour=rounded(y,z,w,h)
        cutter=prism('Temporary_WindowCutter',[(side*1.3,a,b) for a,b in contour],(side*1.0,0,0),white)
        mod=wall.modifiers.new('Window opening','BOOLEAN');enum(mod,'operation','DIFFERENCE');mod.object=cutter
        apply_modifier(wall,mod);bpy.data.objects.remove(cutter,do_unlink=True)
        outer=rounded(y,z,w+.045,h+.045,.14)
        inner=rounded(y,z,w-.045,h-.045,.1)
        ring3('WindowSeal_'+label+'_'+str(i),[(side*(side_x(b)+.007),a,b) for a,b in outer],[(side*(side_x(b)+.007),a,b) for a,b in inner],(-side*.045,0,0),rubber)
        prism('Windows_'+label+'_'+str(i),[(side*(side_x(b)-.022),a,b) for a,b in inner],(-side*.008,0,0),glass)
        tube('WindowTransom_'+label+'_'+str(i),(side*(side_x(z+.27)+.005),y-w/2+.04,z+.27),(side*(side_x(z+.27)+.005),y+w/2-.04,z+.27),.013,metal,8)
    m=wall.modifiers.new('Soft plate edges','BEVEL');m.width=.008;m.segments=2
    # Door occupies the opaque bay between the forward window and the saloon windows.
    door=prism('Door_'+label,[(side*(side_x(z)+.012),y,z) for y,z in [(5.76,.7),(6.48,.7),(6.48,2.03),(5.76,2.03)]],(-side*.045,0,0),white,.015)
    hole=rounded(6.12,1.75,.42,.29,.065)
    cutter=prism('Temporary_DoorCutter',[(side*1.3,a,b) for a,b in hole],(side,0,0),white)
    for obj in [door,wall]:
        mod=obj.modifiers.new('Door glazing opening','BOOLEAN');enum(mod,'operation','DIFFERENCE');mod.object=cutter;apply_modifier(obj,mod)
    bpy.data.objects.remove(cutter,do_unlink=True)
    prism('DoorGlass_'+label,[(side*(side_x(b)-.012),a,b) for a,b in hole],(-side*.008,0,0),glass)
    tube('DoorHandle_'+label,(side*1.85,5.87,1.25),(side*1.85,5.87,1.4),.018)
    for y in [5.80,6.45]:
        tube('DoorSeam_'+label,(side*(side_x(.73)+.02),y,.73),(side*(side_x(2.01)+.02),y,2.01),.006,dark,6)

# Wheelhouse: forward-raked side cheek, two aft-raked windscreen panes and clipped corners.
for side,label in [(-1,'L'),(1,'R')]:
    poly=[(side*1.64,8.8,.68),(side*1.64,10.48,.68),(side*1.57,10.20,1.96),(side*1.57,8.0,1.96)]
    prism('Wheelhouse_Lower_'+label,poly,(-side*.07,0,0),white,.012)
    outer=[(side*1.57,8.0,1.96),(side*1.57,10.20,1.96),(side*1.47,9.96,2.83),(side*1.47,7.55,2.83)]
    center=sum((Vector(p) for p in outer),Vector())/4
    inner=[tuple(center+(Vector(p)-center)*.84) for p in outer]
    ring3('Wheelhouse_Frame_'+label,outer,inner,(-side*.07,0,0),white)
    prism('Wheelhouse_Glass_'+label,[(x-side*.022,y,z) for x,y,z in inner],(-side*.008,0,0),glass)
    # Diagonal rear cheek merges into the passenger roof.
    prism('Wheelhouse_Cheek_'+label,[(side*1.64,8.8,.68),(side*1.57,8.0,1.96),(side*1.47,7.55,2.83),(side*1.60,7.8,2.15)],(-side*.08,0,0),white,.012)
prism('Wheelhouse_FrontLower',[(-1.64,10.48,.68),(1.64,10.48,.68),(1.57,10.20,1.96),(-1.57,10.20,1.96)],(0,-.07,0),white,.016)
for side,label in [(-1,'L'),(1,'R')]:
    outer=[(side*.035,10.20,1.96),(side*1.57,10.20,1.96),(side*1.47,9.96,2.83),(side*.035,9.96,2.83)]
    c=sum((Vector(p) for p in outer),Vector())/4
    inner=[tuple(c+(Vector(p)-c)*.87) for p in outer]
    ring3('Windscreen_Frame_'+label,outer,inner,(0,-.07,0),white)
    prism('Windscreen_Glass_'+label,[(x,y-.025,z) for x,y,z in inner],(0,-.008,0),glass)
    tube('Wiper_'+label,(side*.65,10.21,2.05),(side*1.03,10.08,2.58),.011,dark,8)
loft('Roof_Wheelhouse',[(y,[(-w,2.84),(w,2.84),(w,2.92),(0,2.94),(-w,2.92)]) for y,w in [(7.39,1.64),(9.96,1.65),(10.12,1.47)]],white,False,.018)

# The interior is a visibility proxy, not a detailed passenger fit-out.
box('InteriorProxy_Floor',(0,1.5,.74),(3.52,13,.10),inside,.008)
box('InteriorProxy_RearBulkhead',(0,-5.03,1.43),(3.55,.1,1.38),inside,.008)
box('InteriorProxy_ForwardBulkhead',(0,7.75,1.35),(3.2,.1,1.2),inside,.008)
for row in range(10):
    y=-4.45+row*1.03
    for side in [-1,1]:
        for x in [.64,1.22]:
            box('InteriorProxy_Seat',(side*x,y,1.0),(.49,.51,.10),seatmat,.025)
            box('InteriorProxy_Back',(side*x,y-.21,1.24),(.49,.10,.52),seatmat,.03)
box('Wheelhouse_Console',(0,9.83,1.51),(2.55,.4,.48),inside,.035)
box('Wheelhouse_Floor',(0,9.0,1.04),(2.9,2.0,.08),inside)
box('Wheelhouse_Seat',(-.68,8.66,1.38),(.52,.48,.65),seatmat,.035)

# Foredeck, stowed hydraulic boarding ramp, side walks and railings.
box('BowRamp',(0,11.04,.714),(1.1,1.6,.068),blue,.012)
for x in [-.49,.49]:
    tube('BowRamp_Hinge',(x-.08,10.27,.75),(x+.08,10.27,.75),.045,metal,12)
    tube('BowRamp_HydraulicCylinder',(x,10.32,.77),(x,11.1,.84),.034,metal,12)
for y in [10.5,10.7,10.9,11.1,11.3,11.5]:
    box('BowRamp_Tread',(0,y,.753),(.93,.024,.016),metal,.003)
for side in [-1,1]:
    points=[(side*1.95,9.85),(side*1.9,10.4),(side*1.65,11.03),(side*.66,11.69)]
    for x,y in points:
        tube('Railings_BowPost',(x,y,.69),(x,y,1.64),.022,white)
    for z in [1.12,1.64]:
        for a,b in zip(points,points[1:]):tube('Railings_Bow',(*a,z),(*b,z),.022,white)
    points=[(side*1.91,-9.77),(side*1.87,-10.65),(side*1.60,-11.42),(side*.45,-11.42)]
    for x,y in points:tube('Railings_SternPost',(x,y,.69),(x,y,1.48),.022,white)
    for z in [1.08,1.48]:
        for a,b in zip(points,points[1:]):tube('Railings_Stern',(*a,z),(*b,z),.022,white)
    for y in [-10.5,10.65]:
        box('MooringCleat_Base',(side*1.5,y,.71),(.27,.19,.05),metal)
        tube('MooringCleat',(side*1.62,y,.81),(side*1.38,y,.81),.035,metal)
    # Horizontal engine room stiffeners, characteristic of the updated 14352 photographs.
    for z in [.88,1.12,1.36,1.60,1.84]:
        x=side*(1.84-(z-.68)*(.19/1.37)+.009)
        tube('EngineRoom_Strake',(x,-9.1,z),(x,-5.2,z),.017,white,8)
    box('EngineRoom_VentFrame',(side*1.80,-7.13,1.40),(.04,1.05,.47),dark,.015)
    for i in range(7):
        box('EngineRoom_VentLouvre',(side*1.837,-7.13,1.2+i*.064),(.035,1.00,.025),white,.005)
    for y in [-6.1,-.8,4.2]:
        tube('Roof_Handrail',(side*1.45,y-.40,2.29),(side*1.45,y+.40,2.29),.019,metal)
        for yy in [y-.40,y+.40]:tube('Roof_HandrailFoot',(side*1.45,yy,2.18),(side*1.45,yy,2.29),.019,metal)

for y in [-6.5,-1.6,3.5]:
    box('Roof_HatchFrame',(0,y,2.29),(1.05,.85,.07),white,.035)
    box('Roof_Hatch',(0,y,2.335),(.93,.73,.035),blue,.025)
for x in [-1.03,1.03]:
    tube('EngineRoom_VentPipe',(x,-8.5,2.13),(x,-8.5,2.49),.09,white,12)
    box('EngineRoom_VentHood',(x,-8.5,2.51),(.32,.44,.13),white,.04)

# Single centreline jet and intake, reconstructed from the family arrangement.
tube('WaterJet_NozzleOuter',(0,-10.96,-.12),(0,-11.76,-.12),.30,metal,24)
# Open outlet rim replaces the tube end cap.
jet=bpy.data.objects['WaterJet_NozzleOuter'];bm=bmesh.new();bm.from_mesh(jet.data)
caps=[f for f in bm.faces if len(f.verts)>4];bmesh.ops.delete(bm,geom=caps,context='FACES');bm.to_mesh(jet.data);bm.free()
m=jet.modifiers.new('Nozzle thickness','SOLIDIFY');m.thickness=.04
box('WaterJet_ReverseBucket',(0,-11.63,.22),(.83,.35,.22),hullmat,.05)
box('WaterJet_Intake',(0,-9.87,-.28),(.66,1.38,.18),dark,.04)
for x in [-.25,-.125,0,.125,.25]:tube('WaterJet_IntakeGrille',(x,-10.44,-.384),(x,-9.3,-.384),.012,metal,8)
for y in [-10.6,9.0]:
    box('Cushion_FlexibleSeal',(0,y,-.30),(2.75,.08,.28),rubber,.02)
    for x in [-1.2,-.9,-.6,-.3,0,.3,.6,.9,1.2]:
        tube('Cushion_SealRib',(x,y-.048,-.43),(x,y-.048,-.18),.016,rubber,8)

tube('Mast',(0,8.32,2.94),(0,7.84,4.46),.045,white,12)
tube('Mast_Brace',(0,7.66,2.94),(0,7.84,4.13),.027,white,10)
tube('Mast_Crossbar',(-.48,7.99,3.99),(.48,7.99,3.99),.023,metal,10)
tube('Antenna',(.62,8.35,2.94),(.62,8.35,4.00),.009,metal,8)
lamp=material('Lights_WarmLens',(.84,.78,.54),.05,.19)
for x in [-1.05,1.05]:
    tube('Searchlight_Support',(x,9.67,2.94),(x,9.67,3.11),.025,metal)
    tube('Searchlight_Housing',(x,9.54,3.15),(x,9.79,3.15),.115,white,16)
    tube('Searchlight_Lens',(x,9.795,3.15),(x,9.801,3.15),.094,lamp,16)

# Raised lettering is original geometry, never a photograph baked into a texture.
for side in [-1,1]:
    d=bpy.data.curves.new('Nameplate_LUCH','FONT');d.body='ЛУЧ';d.size=.30;d.extrude=.0008
    ob=bpy.data.objects.new('Nameplate_LUCH',d);model.objects.link(ob);ob.parent=root
    ob.location=(side*1.81,-7.8,1.00)
    ob.rotation_euler=(math.pi/2,0,side*math.pi/2)
    ob.data.materials.append(blue)

for a in bpy.context.screen.areas:
    if a.type=='VIEW_3D':a.spaces.active.overlay.show_overlays=False
view('02-detail-port',(-30,0,3.0))
view('02-detail-front',(20,29,14))
bpy.ops.wm.save_as_mainfile(filepath=BASE+'/Art/Source/Luch14352/Luch14352.blend')
print('Detail pass complete',len(model.objects),'objects')
