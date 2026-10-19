"""Deterministic original PBR tiles, shared by Blender and Unity URP.

One tile spans two metres. No downloaded photographs or licensed textures.
Run after reference geometry and before export. Images are saved beside the FBX.
"""
import bpy, os, numpy as np
from mathutils import Vector

BASE = 'G:/Projects/ShipSim159'
folder = BASE+'/Assets/ShipSimulator/Models/Luch14352/Textures'
os.makedirs(folder,exist_ok=True)
model = bpy.data.collections['Luch14352_Model']
size = 1024
rng = np.random.default_rng(14352)
yy,xx = np.mgrid[0:size,0:size]/size

def noise(scale_x,scale_y):
    field = rng.normal(size=(size,size))
    fx = np.fft.fftfreq(size)[None,:]
    fy = np.fft.fftfreq(size)[:,None]
    field = np.fft.ifft2(np.fft.fft2(field)*np.exp(-((fx*scale_x)**2+(fy*scale_y)**2))).real
    return field/max(field.std(),1e-8)

cloud = noise(100,100)
streak = noise(15,250)
grain = noise(1.5,1.5)
brushed = noise(1.5,90)

def save(name,values,color=False):
    image = bpy.data.images.get(name)
    if image:
        bpy.data.images.remove(image)
    image = bpy.data.images.new(name,width=size,height=size,alpha=True)
    image.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
    image.pixels.foreach_set(np.asarray(values,dtype=np.float32).ravel())
    image.filepath_raw = folder+'/'+name+'.png'
    image.file_format = 'PNG'
    image.save()
    image.filepath = bpy.path.relpath(image.filepath)
    return image

def rgba(rgb,alpha=1):
    result = np.ones((size,size,4),dtype=np.float32)
    result[:,:,:3] = rgb
    result[:,:,3] = alpha
    return result

deck = bpy.data.materials.get('Deck_Nonslip') or bpy.data.materials.new('Deck_Nonslip')
deck.use_nodes = True
ob = bpy.data.objects['Deck_Obnose']
if deck not in list(ob.data.materials):
    ob.data.materials.append(deck)
for face in ob.data.polygons:
    face.material_index = list(ob.data.materials).index(deck) if face.normal.z > .7 else 0

# Linear albedo, metal fraction, roughness, relief depth in metres, texture family.
specs = {
    'Paint_Ivory': ((.72,.74,.69),0,.38,.00014,'paint'),
    'Paint_DeckTeal': ((.055,.18,.19),0,.48,.00020,'paint'),
    'Paint_HullCharcoal': ((.028,.038,.041),0,.42,.00025,'paint'),
    'Deck_Nonslip': ((.075,.19,.20),0,.78,.00065,'grit'),
    'Skeg_Rubber': ((.018,.021,.022),0,.78,.00045,'rubber'),
    'Rail_Aluminium': ((.52,.56,.59),.90,.28,.00005,'metal'),
    'Interior_Seats': ((.075,.15,.17),0,.85,.00025,'fabric')}

for name,(color,metal,rough,depth,family) in specs.items():
    variation = .014*cloud+.007*grain
    relief = grain*.25
    wear = np.maximum(streak-.4,0)*.015
    if family == 'paint':
        variation -= wear
        relief = noise(3,3)*.25
    elif family == 'grit':
        variation += .025*grain
        relief = np.maximum(grain,0)*.4
    elif family == 'rubber':
        variation += .025*brushed
        relief = brushed*.25
    elif family == 'metal':
        variation = .012*brushed
        relief = brushed*.25
    elif family == 'fabric':
        weave = np.sin(xx*np.pi*512)*np.sin(yy*np.pi*512)
        variation += weave*.025
        relief = weave*.35
    albedo = np.clip(np.asarray(color)[None,None,:]*(1+variation[:,:,None]),.002,1)
    base = save(name+'_BaseColor',rgba(albedo),True)
    roughness = np.clip(rough+.025*cloud+.025*grain+wear,.08,.96)
    packed = rgba(np.zeros((size,size,3)),1-roughness)
    packed[:,:,0] = metal
    mask = save(name+'_MetalSmooth',packed)
    height = relief*depth
    dx = (np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))/(4/size)
    dy = (np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))/(4/size)
    normal = np.stack((-dx,-dy,np.ones_like(dx)),axis=2)
    normal /= np.linalg.norm(normal,axis=2)[:,:,None]
    norm = save(name+'_Normal',rgba(normal*.5+.5))
    mat = bpy.data.materials[name]
    mat.use_nodes = True
    nodes,links = mat.node_tree.nodes,mat.node_tree.links
    for node in list(nodes):
        if node.name.startswith('LuchPBR_'):
            nodes.remove(node)
    shader = next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    uv = nodes.new('ShaderNodeTexCoord');uv.name='LuchPBR_UV'
    textures = []
    for label,img in (('Base',base),('Mask',mask),('Normal',norm)):
        node = nodes.new('ShaderNodeTexImage');node.name='LuchPBR_'+label;node.image=img
        links.new(uv.outputs['UV'],node.inputs['Vector']);textures.append(node)
    links.new(textures[0].outputs['Color'],shader.inputs['Base Color'])
    shader.inputs['Metallic'].default_value=metal
    invert = nodes.new('ShaderNodeMath');invert.name='LuchPBR_Roughness';invert.operation='SUBTRACT'
    invert.inputs[0].default_value=1
    links.new(textures[1].outputs['Alpha'],invert.inputs[1])
    links.new(invert.outputs[0],shader.inputs['Roughness'])
    nm = nodes.new('ShaderNodeNormalMap');nm.name='LuchPBR_NormalDecode'
    links.new(textures[2].outputs['Color'],nm.inputs['Color'])
    links.new(nm.outputs['Normal'],shader.inputs['Normal'])

for ob in model.objects:
    if ob.type != 'MESH':
        continue
    uv = ob.data.uv_layers.get('Planar_Metres') or ob.data.uv_layers.new(name='Planar_Metres')
    ob.data.uv_layers.active = uv
    for p in ob.data.polygons:
        axis = max(range(3),key=lambda i:abs(p.normal[i]))
        a,b = [i for i in range(3) if i!=axis]
        for li in p.loop_indices:
            co=ob.matrix_world@ob.data.vertices[ob.data.loops[li].vertex_index].co
            uv.data[li].uv = (co[a]/2,co[b]/2)

glass = bpy.data.materials['Glass_BlueGrey']
shader=next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
shader.inputs['Base Color'].default_value=(.13,.17,.18,1)
shader.inputs['Metallic'].default_value=0
shader.inputs['Roughness'].default_value=.12
shader.inputs['Transmission Weight'].default_value=.65
print('LUCH_PBR|7 original 1024px tile sets; two-metre UV scale; deck grip and neutral glass')
