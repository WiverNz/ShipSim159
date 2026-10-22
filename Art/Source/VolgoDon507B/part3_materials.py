"""Bake restrained geometry-directed wear into the retained atlas coordinates."""
import bpy
import numpy as np
import json
from pathlib import Path

ROOT = Path('G:/Projects/ShipSim159')
SOURCE = ROOT/'Art/Source/VolgoDon507B'
TARGET = ROOT/'Assets/ShipSimulator/Models/VolgoDon507B_Improved'
scene = bpy.data.scenes['VolgoDon507B_Source']
N = 1024

def save_image(name, pixels, color=False):
    image = bpy.data.images.get(name) or bpy.data.images.new(name,width=pixels.shape[1],height=pixels.shape[0],alpha=True)
    image.colorspace_settings.name = 'sRGB' if color else 'Non-Color'
    image.pixels.foreach_set(np.ascontiguousarray(pixels,dtype=np.float32).ravel())
    image.filepath_raw = str(TARGET/'Textures'/(name+'.png'))
    image.file_format = 'PNG'; image.save(); image.pack()
    image.filepath = bpy.path.relpath(image.filepath,start=str(SOURCE))
    return image

def node_image(mat, image, label):
    node = next((n for n in mat.node_tree.nodes if n.label==label),None)
    if node is None: node=mat.node_tree.nodes.new('ShaderNodeTexImage')
    node.label=label; node.image=image
    return node

materials = sorted({m for o in scene.objects if o.type=='MESH' for m in o.data.materials},key=lambda m:m.name)
manifest=[]
for mat in materials:
    bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    tex=next((n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image and
              (n.image.filepath.endswith('_BaseColor.png'))),None)
    entry={'name':mat.name,'color':list(bs.inputs['Base Color'].default_value),
           'metallic':bs.inputs['Metallic'].default_value,'smoothness':1-bs.inputs['Roughness'].default_value,
           'texture':'','normal':'','metallicSmoothness':'','transparent':False}
    if mat.name=='VD507B_Glass':
        bs.inputs['Base Color'].default_value=(.09,.15,.18,.32)
        bs.inputs['Alpha'].default_value=.32
        bs.inputs['Roughness'].default_value=.12
        entry.update(color=[.09,.15,.18,.32],smoothness=.88,transparent=True)
    elif tex and mat.name!='VD507B_lights':
        wear=np.zeros((N,N),np.float32); relief=np.zeros_like(wear); traffic=np.zeros_like(wear)
        # Rasterize the actual surface, so masks follow waterline, drains and walkways.
        for obj in [o for o in scene.objects if o.type=='MESH' and mat in list(o.data.materials)]:
            mesh=obj.data; uv=mesh.uv_layers[0].data
            points=np.array([tuple(obj.matrix_world@v.co) for v in mesh.vertices],np.float32)
            for p in mesh.polygons:
                if mesh.materials[p.material_index]!=mat: continue
                ids=list(p.loop_indices)
                for k in range(1,len(ids)-1):
                    ls=[ids[0],ids[k],ids[k+1]]
                    coords=np.array([tuple(uv[i].uv) for i in ls])*N
                    lo=np.maximum(np.floor(coords.min(axis=0)).astype(int),0)
                    hi=np.minimum(np.ceil(coords.max(axis=0)).astype(int),N-1)
                    if np.any(hi<lo): continue
                    a,b,c=coords
                    den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
                    if abs(den)<1e-8: continue
                    xx,yy=np.meshgrid(np.arange(lo[0],hi[0]+1)+.5,np.arange(lo[1],hi[1]+1)+.5)
                    u=((b[1]-c[1])*(xx-c[0])+(c[0]-b[0])*(yy-c[1]))/den
                    v=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den
                    inside=(u>=0)&(v>=0)&(u+v<=1)
                    if not inside.any(): continue
                    ps=points[[mesh.loops[i].vertex_index for i in ls]]
                    world=u[...,None]*ps[0]+v[...,None]*ps[1]+(1-u-v)[...,None]*ps[2]
                    x,y,z=world[...,0],world[...,1],world[...,2]
                    normal=(obj.matrix_world.to_3x3()@p.normal).normalized()
                    side=max(0,1-abs(normal.z))
                    water=np.exp(-((z+.08)/.23)**2)*side if mat.name=='VD507B_hull' else 0
                    drainage=np.exp(-(np.sin((y+1.4)*np.pi/4.8)/.055)**2)
                    drainage*=np.exp(-np.maximum(1.97-z,0)/.65)*(z<1.97)*(z>.35)*side
                    walk=np.exp(-((abs(x)-7.1)/.32)**2)*max(normal.z,0)*(z>1.85)*(z<2.6)
                    seam=np.exp(-(np.sin(y*np.pi/6)/.015)**2)*side
                    mask=(.7*water+.28*drainage)*inside
                    sl=np.s_[lo[1]:hi[1]+1,lo[0]:hi[0]+1]
                    np.maximum(wear[sl],mask,out=wear[sl])
                    np.maximum(traffic[sl],walk*inside,out=traffic[sl])
                    if mat.name=='VD507B_hull': np.maximum(relief[sl],seam*inside,out=relief[sl])
        original=tex.image
        w,h=original.size
        arr=np.empty(w*h*4,np.float32); original.pixels.foreach_get(arr); arr=arr.reshape(h,w,4)
        mask=wear.repeat(h//N,axis=0).repeat(w//N,axis=1)
        walk=traffic.repeat(h//N,axis=0).repeat(w//N,axis=1)
        base=arr.copy()
        base[:,:,:3]*=(1-.22*mask[...,None]+.035*walk[...,None])
        base[:,:,:3]+=mask[...,None]*np.array([.009,.003,.001],np.float32)
        base=np.clip(base,0,1)
        albedo=save_image(mat.name+'_P3_BaseColor',base,True)
        albedo_node=node_image(mat,albedo,'Part3 albedo')
        mat.node_tree.links.new(albedo_node.outputs['Color'],bs.inputs['Base Color'])
        rough=np.clip((.62 if mat.name in ['VD507B_deck','VD507B_hold'] else .48)+.14*wear-.08*traffic,.25,.82)
        packed=np.zeros((N,N,4),np.float32); packed[:,:,3]=1-rough
        metallic=save_image(mat.name+'_MetallicSmoothness',packed)
        rough_rgba=np.ones((N,N,4),np.float32); rough_rgba[:,:,:3]=rough[...,None]
        rough_image=save_image(mat.name+'_Roughness',rough_rgba)
        rough_node=node_image(mat,rough_image,'Part3 roughness')
        mat.node_tree.links.new(rough_node.outputs['Color'],bs.inputs['Roughness'])
        # Small authored seam relief, never derive bumps from baked AO or paint colour.
        gy,gx=np.gradient(relief*.025)
        normal=np.ones((N,N,4),np.float32)
        normal[:,:,0]=.5-gx; normal[:,:,1]=.5-gy; normal[:,:,2]=1
        if mat.name in ['VD507B_deck','VD507B_hold']:
            yy,xx=np.indices((N,N))
            normal[:,:,0]+=.018*np.sin(xx*np.pi/2)*traffic
            normal[:,:,1]+=.018*np.sin(yy*np.pi/2)*traffic
        normal_image=save_image(mat.name+'_Normal',normal)
        normal_node=node_image(mat,normal_image,'Part3 normal')
        converter=next((n for n in mat.node_tree.nodes if n.type=='NORMAL_MAP'),None) or mat.node_tree.nodes.new('ShaderNodeNormalMap')
        converter.inputs['Strength'].default_value=.5
        mat.node_tree.links.new(normal_node.outputs['Color'],converter.inputs['Color'])
        mat.node_tree.links.new(converter.outputs['Normal'],bs.inputs['Normal'])
        entry.update(texture=albedo.name+'.png',normal=normal_image.name+'.png',metallicSmoothness=metallic.name+'.png',metallic=0,smoothness=1)
        print(mat.name,'wear coverage',float((wear>.01).mean()))
    elif tex:
        entry['texture']=Path(tex.image.filepath.replace('\\','/')).name
    manifest.append(entry)

with open(TARGET/'materials.json','w') as f: json.dump({'materials':manifest,'stage':3},f,indent=2)
scene['part3_materials']=True
print('Materials prepared:',len(manifest))
