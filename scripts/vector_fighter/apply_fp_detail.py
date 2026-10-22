"""Selected F9 satin ceramic finish; exports candidates, never installs them.

Uses neutral albedo and real tangent-space normal/roughness textures. F8 mesh,
skin, animation and equipment remain intact. No scene-camera changes.
"""
import bpy, math, json, hashlib, importlib.util
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'.local/vector-f9';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'scripts/vector_fighter/f8-glove-repair.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
before={o.name:len(o.data.vertices) for o in bpy.context.scene.objects if o.type=='MESH'}

def texture(name,rgb):
    h,w=rgb.shape[:2]
    im=bpy.data.images.new(name,width=w,height=h)
    im.colorspace_settings.name='Non-Color'
    rgba=np.dstack((rgb,np.ones((h,w))))
    im.pixels.foreach_set(rgba.astype(np.float32).ravel());im.pack()
    return im

n=2048
u,t=np.meshgrid(np.linspace(-1.5,1.5,n),np.linspace(0,1,n))
# Authored longitudinal inlay and wrist seat, in physical metres. The slopes
# become a tangent normal so light direction controls highlights and shadow.
edge=.72-.14*np.cos(t*math.pi)
groove=np.exp(-((np.abs(u)-edge)/.018)**2)
wrist=np.exp(-((t-.84)/.009)**2)
lip=np.exp(-((np.abs(u)-edge-.035)/.022)**2)
height=-.00048*groove-.00042*wrist+.00012*lip
# Low-amplitude irregular coating avoids embossed weave on a ceramic panel.
grain=(np.sin(u*997+t*463)*np.sin(u*719-t*1131))
height+=.0000015*grain
dy,dx=np.gradient(height, .209/(n-1), .045*3/(n-1))
normal=np.dstack((-dx,-dy,np.ones_like(height)))
normal/=np.linalg.norm(normal,axis=2)[...,None]
normal=normal*.5+.5
mask=np.clip(groove+wrist,0,1)
value=.43*(1-.62*mask)
flank=np.abs(u)>edge+.035
value[flank]*=.72
ticks=(t>.18)&(t<.34)&(np.abs(u+.48)<.11)&((t*100)%1<.16)
value[ticks]=.52
rough=np.full_like(value,.43);rough[flank]=.61
rough+=.045*mask+.007*grain
base=texture('vector-f9-neutral-ceramic-albedo',np.dstack((value,)*3))
norm=texture('vector-f9-recessed-ceramic-normal',normal)
rm=texture('vector-f9-ceramic-roughness',np.dstack((rough,)*3))
m=bpy.data.materials.new('vector-fighter-identity-forearm-f9');m.use_nodes=True
nodes=m.node_tree.nodes;nodes.clear();links=m.node_tree.links
bs=nodes.new('ShaderNodeBsdfPrincipled');output=nodes.new('ShaderNodeOutputMaterial')
links.new(bs.outputs[0],output.inputs['Surface']);bs.inputs['Metallic'].default_value=.16
for im,socket in [(base,'Base Color'),(rm,'Roughness')]:
    tex=nodes.new('ShaderNodeTexImage');tex.image=im;links.new(tex.outputs['Color'],bs.inputs[socket])
tex=nodes.new('ShaderNodeTexImage');tex.image=norm
nm=nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=1
links.new(tex.outputs['Color'],nm.inputs['Color']);links.new(nm.outputs['Normal'],bs.inputs['Normal'])
for side,sign in [('Left',1),('Right',-1)]:
    shell=next(o for o in bpy.context.scene.objects if side+'LowerArm contoured shell' in o.name)
    a=rig.data.bones[side+'LowerArm'].head_local;b=rig.data.bones[side+'Hand'].head_local
    axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lateral=axis.cross(front).normalized()
    uv=shell.data.uv_layers[0] if shell.data.uv_layers else shell.data.uv_layers.new(name='UVMap')
    for loop in shell.data.loops:
        p=rig.matrix_world.inverted()@shell.matrix_world@shell.data.vertices[loop.vertex_index].co
        along=(p-a).dot(axis)/(b-a).length;rel=p-(a+(b-a)*along)
        angle=math.atan2(rel.dot(lateral),rel.dot(front))+.12*sign
        uv.data[loop.index].uv=((sign*angle+1.5)/3,along)
    shell.data.materials[0]=m

# Keep existing glove albedo, stitched geometry and normal maps. Distinguish
# textile from coated panels without flattening the established hand skin.
glove=next(o for o in bpy.context.scene.objects if 'gloves' in o.name)
for mat in glove.data.materials:
    if not mat or not mat.use_nodes:continue
    bs=next((node for node in mat.node_tree.nodes if node.type=='BSDF_PRINCIPLED'),None)
    if not bs:continue
    if mat.name.startswith('vector-glove'):
        for link in list(bs.inputs['Roughness'].links):mat.node_tree.links.remove(link)
        bs.inputs['Roughness'].default_value=.72
    elif mat.name.startswith('vector-ceramic'):
        bs.inputs['Roughness'].default_value=.43

after={o.name:len(o.data.vertices) for o in bpy.context.scene.objects if o.type=='MESH'}
assert before==after
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f9-fp-detail.blend'),compress=True)
spec=importlib.util.spec_from_file_location('vector_build',ROOT/'scripts/vector_fighter/build.py')
g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g);g.rig=rig
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='weapon:joined']
arm_parts=[o for o in meshes if ('LowerArm' in o.name or 'gloves' in o.name or ('UpperArm' in o.name and 'fabric' in o.name))]
body_parts=[o for o in meshes if o not in arm_parts]
def join(items,name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in items:o.select_set(True)
    bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();items[0].name=name;return items[0]
arms=join(arm_parts,'VECTOR world forearms');body=join(body_parts,'VECTOR body')
equipment=[o for o in bpy.context.scene.objects if o.type!='MESH' or o.name=='weapon:joined']
g.export(OUT/'candidate-body.glb',equipment+[arms,body]);arms.name='vector-armored-hands'
g.export(OUT/'candidate-arms.glb',equipment+[arms])
(OUT/'surface-report.json').write_text(json.dumps({'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'mesh_vertex_counts_unchanged':before==after,'map_resolution':n,'normal':'tangent; physical height gradients, not albedo shadows','height_metres':{'margin':-.00048,'wrist_seat':-.00042,'edge_lip':.00012},'runtime_changes':'none; candidate assets only'},indent=2)+'\n')
print('F9_CANDIDATE_EXPORTED')
