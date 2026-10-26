"""Continue 01_geometry.py in the same Blender MCP Python namespace."""
if scene.get('source_phase') != 'geometry': raise RuntimeError('Unexpected modelling phase')

# Thin rails, with clear breaks at access stairs and the working pushing face.
for s in (-1,1):
    paths=[[(s*2.25,-7.8,1.46),(s*2.43,-5.7,1.46),(s*2.48,-3.3,1.46),(s*2.48,-1.2,1.46)],
           [(s*2.42,4.4,2.55),(s*2.36,6.4,2.55),(s*2.2,8.3,2.55)]]
    for path in paths:
        for x,y,z in path: rod('RailStanchion',(x,y,z),(x,y,z+.89),.026,'Railings','House_Ivory')
        for dz in (.3,.59,.89): tube('RailCourse',[(x,y,z+dz) for x,y,z in path],.019,'Railings','House_Ivory')
    for y in (-7.7,-4.9,7.8):
        z=2.54 if y>0 else 1.46; x=s*(2.1 if y>0 else 2.05)
        box('BollardFoundation',(x,y,z+.04),(.48,.72,.08),'Mooring_Equipment','Hull_Grey')
        for yy in (y-.22,y+.22):
            rod('BollardPost',(x,yy,z+.08),(x,yy,z+.4),.095,'Mooring_Equipment','Steel_Dark',16)
            rod('BollardCap',(x,yy,z+.39),(x,yy,z+.44),.13,'Mooring_Equipment','Steel_Dark',16)
        rod('BollardCrossbar',(x,y-.4,z+.28),(x,y+.4,z+.28),.042,'Mooring_Equipment','Steel_Dark')
    for y,z in ((-6.8,1.5),(8.2,2.55)):
        x=s*(2.25 if y<0 else 2.16)
        box('FairleadBed',(x,y,z),(.22,.6,.10),'Mooring_Equipment','Steel_Dark')
        for yy in (y-.2,y+.2): rod('FairleadRoller',(x,yy,z),(x,yy,z+.22),.065,'Mooring_Equipment','Steel_Dark')

# Electric towing winch, on the after working deck behind the machinery casing.
box('TowingWinchBed',(0,-5.9,1.54),(1.9,.9,.16),'Mooring_Equipment','Hull_Grey')
for x in (-.64,.64): box('WinchCheek',(x,-5.9,1.85),(.13,.65,.58),'Mooring_Equipment','Steel_Dark')
rod('WinchDrum',(-.62,-5.9,1.96),(.62,-5.9,1.96),.24,'Mooring_Equipment','Steel_Dark',32)
for x in (-.55,.55): rod('DrumFlange',(x-.03,-5.9,1.96),(x+.03,-5.9,1.96),.35,'Mooring_Equipment','Hull_Grey',32)
for i in range(20): ring('WinchWire',(i*.045-.44,-5.9,1.96),.255,.018,(1,0,0),'Mooring_Equipment','Steel_Dark')
rod('WinchMotor',(.77,-5.9,1.78),(1.15,-5.9,1.78),.18,'Mooring_Equipment','Interior_Green',20)
tube('TowWire',[(0,-5.9,2.23),(0,-7.2,2.05),(0,-9.6,1.65)],.016,'Mooring_Equipment','Steel_Dark')
for y in (-7.0,-8.6):
    tube('TowArch',[(-1.8,y,1.45),(-1.6,y,1.95),(0,y,2.07),(1.6,y,1.95),(1.8,y,1.45)],.045,'Mooring_Equipment','Steel_Dark')

# Foredeck manual capstan and anchor tackle, documented in the original sheet.
rod('AnchorCapstan',(0,8.55,2.54),(0,8.55,3.02),.18,'Mooring_Equipment','Steel_Dark',24)
for z,r in ((2.6,.28),(2.95,.24),(3.06,.30)):
    rod('CapstanFlange',(0,8.55,z),(0,8.55,z+.05),r,'Mooring_Equipment','Steel_Dark',24)
rod('CapstanHandbar',(-.65,8.55,3.10),(.65,8.55,3.10),.022,'Mooring_Equipment','Steel_Dark')
for i in range(26):
    o=ring('AnchorChain',(0,8.8+i*.057,2.59),.045,.009,(0,0,1) if i%2 else (1,0,0),'Mooring_Equipment','Steel_Dark')
box('ForepeakHatch',(.85,8.55,2.57),(.6,.7,.1),'Deck','Hull_Grey',.06)
for x,y,z in ((0,-9.1,1.48),(-.9,-3.3,2.4),(.9,-3.3,2.4),(0,6.1,2.56)):
    box('InspectionHatch',(x,y,z),(.7,.85,.09),'Deck','House_Ivory',.03)
    rod('HatchHandle',(x-.14,y,z+.07),(x+.14,y,z+.07),.015,'Deck','Steel_Dark')

# Goose-neck vents and exhaust, avoiding later improvised aft canopy.
for s in (-1,1):
    x=s*1.18
    tube('EngineVent',[(x,-.28,3.66),(x,-.28,4.04),(x,-.35,4.18),(x,-.52,4.22),(x,-.66,4.19)],.125,'Superstructure','House_Ivory',16)
    rod('VentDarkMouth',(x,-.67,4.19),(x,-.68,4.19),.106,'Superstructure','Steel_Dark',24)
rod('Exhaust',(0,-.34,2.5),(0,-.34,4.15),.075,'Superstructure','Steel_Dark',20)
rod('ExhaustRainCap',(0,-.34,4.15),(0,-.34,4.21),.12,'Superstructure','Steel_Dark',20)
for s in (-1,1):
    for y in (.7,3.8):
        box('VentLouvrePanel',(s*1.70,y,3.36),(.06,.3,.24),'Superstructure','House_Ivory')
        for z in (3.28,3.33,3.38,3.43): box('VentLouvre',(s*1.74,y,z),(.025,.23,.014),'Superstructure','Steel_Dark',.004)
    ring('LifeRing',(s*1.76,4.8,3.3),.29,.075,(s,0,0),'Navigation_Equipment','Life_Orange')
    for a in (0,math.pi/2,math.pi,3*math.pi/2):
        box('LifeRingBand',(s*1.78,4.8+.29*math.cos(a),3.3+.29*math.sin(a)),(.16,.11,.11),'Navigation_Equipment','House_Ivory',.03)

# Original compact pole mast, cross tree, searchlight and side light trays.
rod('Mast',(0,3.38,5.8),(0,3.18,7.24),.035,'Navigation_Equipment','House_Ivory',16)
rod('MastCrossTree',(-.58,3.28,6.6),(.58,3.28,6.6),.025,'Navigation_Equipment','House_Ivory')
for x in (-.55,.55):
    rod('MastLamp',(x,3.28,6.62),(x,3.28,6.75),.055,'Navigation_Equipment','Light_White',16)
rod('MastHeadLamp',(0,3.18,7.23),(0,3.18,7.36),.06,'Navigation_Equipment','Light_White',16)
rod('Aerial',(.93,4.6,5.79),(.93,4.6,7.6),.009,'Navigation_Equipment','Steel_Dark',8)
for x in (-1.45,1.45): rod('MastStay',(x,3.9,5.8),(0,3.2,7.1),.006,'Navigation_Equipment','Steel_Dark',6)
box('SearchlightPedestal',(0,4.55,5.9),(.23,.25,.25),'Navigation_Equipment','Steel_Dark')
rod('SearchlightBody',(0,4.4,6.15),(0,4.74,6.15),.20,'Navigation_Equipment','House_Ivory',32)
rod('SearchlightLens',(0,4.745,6.15),(0,4.755,6.15),.175,'Navigation_Equipment','Light_White',32)
ring('SearchlightRim',(0,4.76,6.15),.19,.022,(0,1,0),'Navigation_Equipment','Steel_Dark')
for s in (-1,1):
    box('SideLightTray',(s*1.94,3.5,5.77),(.32,.58,.06),'Navigation_Equipment','Steel_Dark')
    rod('SideLight',(s*1.94,3.5,5.8),(s*1.94,3.5,5.98),.075,'Navigation_Equipment','Light_Green' if s>0 else 'Light_Red',16)
    rod('LightBracket',(s*1.58,3.5,5.5),(s*1.94,3.5,5.76),.02,'Navigation_Equipment','House_Ivory')
    rod('Horn',(s*.62,4.65,5.85),(s*.62,4.95,5.85),.055,'Navigation_Equipment','Steel_Dark',16)

# Enclosed duct wall and four-blade impeller, with separate steering and reversing parts.
duct=rod('WaterjetDuct',(0,-10.45,.70),(0,-7.05,.70),.48,'Waterjet_System','Steel_Dark',40)
cut(duct,rod('DuctVoid',(0,-10.6,.70),(0,-6.9,.70),.435,'Waterjet_System',None,40))
ring('JetOutletLip',(0,-10.47,.7),.46,.035,(0,1,0),'Waterjet_System','Steel_Dark')
for x in (-.35,-.175,0,.175,.35): rod('IntakeGrate',(x,-7.4,.04),(x,-6.3,.04),.018,'Waterjet_System','Steel_Dark')
rod('ImpellerShaft',(0,-9.1,.7),(0,-7.1,.7),.075,'Waterjet_System','Steel_Dark')
impeller=rod('Waterjet_Impeller',(0,-8.75,.7),(0,-8.5,.7),.11,'Waterjet_System','Steel_Dark',20)
impeller['moving_part']='single four-blade impeller, diameter 0.846 m'
for i in range(4):
    a=i*math.pi/2
    v=[]
    for r,t,dy in ((.10,-.2,-.1),(.423,-.10,.04),(.40,.30,.13),(.12,.5,.04)):
        v.append((r*math.cos(a+t),-8.62+dy,.7+r*math.sin(a+t)))
    blade=mesh('ImpellerBlade',v,[(0,1,2,3)],'Waterjet_System','Steel_Dark')
    m=blade.modifiers.new('Blade thickness','SOLIDIFY'); m.thickness=.014
    blade.parent=impeller; blade.matrix_parent_inverse=impeller.matrix_world.inverted()
rudder=box('Waterjet_Rudder',(0,-10.22,.68),(.055,.66,.79),'Waterjet_System','Steel_Dark',.02)
rudder['moving_part']='balanced centreline rudder, 45 degrees either side'
for s in (-1,1):
    flap=box('Waterjet_ReverseFlap_'+str(s),(s*.32,-10.51,.7),(.58,.055,.82),'Waterjet_System','Steel_Dark',.025)
    flap.rotation_euler.z=s*.5
    flap['moving_part']='paired hydrodynamic reversing flap, estimated closed geometry'
    rod('FlapHinge',(s*.57,-10.49,.26),(s*.57,-10.49,1.15),.035,'Waterjet_System','Steel_Dark')

# Reconstructed bridge controls based on the surviving 2014 console, restored to service.
box('ConsoleCabinet',(.65,4.5,4.12),(1.18,.64,.76),'Wheelhouse_Interior','Interior_Green',.04)
panel=box('ConsoleSlopingPanel',(.65,4.56,4.61),(1.18,.58,.055),'Wheelhouse_Interior','Interior_Green')
panel.rotation_euler.x=math.radians(42)
for x,z,r in ((.28,4.68,.10),(.57,4.68,.08),(.85,4.68,.08),(1.07,4.68,.055)):
    center=(x,4.50,z)
    ring('GaugeBezel',center,r,.014,(0,-1,.8),'Wheelhouse_Interior','Steel_Dark')
    rod('GaugeFace',(x,4.502,z),(x,4.494,z),r-.013,'Wheelhouse_Interior','Dial',24)
    rod('GaugeNeedle',(x,4.48,z),(x+.03,4.48,z+.04),.004,'Wheelhouse_Interior','Instrument',6)
for x in (.25,.6,.95):
    box('LeverSlot',(x,4.25,4.53),(.09,.25,.012),'Wheelhouse_Interior','Instrument')
    lever=rod('EngineLever',(x,4.27,4.54),(x,4.12,4.77),.016,'Wheelhouse_Interior','Steel_Dark')
    rod('LeverGrip',(x-.05,4.12,4.77),(x+.05,4.12,4.77),.025,'Wheelhouse_Interior','Rubber')
    lever['moving_part']='bridge control'
box('HelmPedestal',(-.65,4.35,4.13),(.27,.31,.78),'Wheelhouse_Interior','Interior_Green')
rod('HelmSpindle',(-.65,4.4,4.46),(-.65,4.1,4.57),.045,'Wheelhouse_Interior','Steel_Dark')
wheel=ring('SteeringWheel',(-.65,4.07,4.58),.30,.024,(0,-1,.35),'Wheelhouse_Interior','Wood')
wheel['moving_part']='helm wheel'
for i in range(6):
    a=i*math.pi/3
    rod('WheelSpoke',(-.65,4.07,4.58),(-.65+.3*math.cos(a),4.07,.3*math.sin(a)+4.58),.012,'Wheelhouse_Interior','Steel_Dark')
box('RadioCabinet',(-1.10,3.35,4.22),(.48,.32,.32),'Wheelhouse_Interior','Interior_Green')
for x in (-1.24,-1.08,-.94): rod('RadioKnob',(x,3.17,4.20),(x,3.14,4.20),.026,'Wheelhouse_Interior','Instrument')
box('ChartShelf',(-.1,3.29,4.25),(1.1,.42,.045),'Wheelhouse_Interior','Wood')
box('ChartPaper',(-.1,3.29,4.278),(.8,.30,.004),'Wheelhouse_Interior','Dial',0)
rod('StoolStem',(-.65,3.6,3.75),(-.65,3.6,4.08),.035,'Wheelhouse_Interior','Steel_Dark')
rod('StoolSeat',(-.65,3.6,4.08),(-.65,3.6,4.14),.19,'Wheelhouse_Interior','Wood',24)
label('ConsoleLabel','ХОД',(.65,4.21,4.55),(0,0,0),.06,'Wheelhouse_Interior','Dial')

# Limited local wear around working fittings, never abandoned-vessel damage.
for o in list(groups['Mooring_Equipment'].objects):
    if o.name.startswith('BollardFoundation'):
        x,y,z=o.location
        box('FittingWear',(x,y,z+.044),(.52,.75,.003),'Deck','Wear',.01)

# Landmarks use the baseline; export converts relative to the loaded waterline.
anchors={'Waterline':(0,0,.62),'BowReference':(0,10.9,.62),'StarboardReference':(2.65,0,.62),
 'UpReference':(0,0,1.62),'CameraNavigator':(-.45,3.95,5.12),'CameraChaseTarget':(0,0,2.0),
 'ShipCentre':(0,0,.62),'CameraForward':(0,7,3.5),'WaterjetOutlet':(0,-10.5,.7),
 'WaterjetIntake':(0,-6.8,.03),'WakeAttachment':(0,-10.65,.62),
 'PortLight':(-1.94,3.5,5.9),'StarboardLight':(1.94,3.5,5.9),
 'MastheadLight':(0,3.18,7.3),'SternLight':(0,-9.8,2.25)}
for name,p in anchors.items():
    o=bpy.data.objects.new(name,None); groups['Navigation_Equipment'].objects.link(o); o.location=p
    o.empty_display_size=.15

scene['source_phase']='details'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Serpukhov_R96.blend'))
print('R96_DETAILS',len(scene.objects),'objects')
