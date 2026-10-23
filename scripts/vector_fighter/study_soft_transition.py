"""F10 form/material study only. Does not modify runtime GLBs or grip profiles."""
import bpy, math, json
from pathlib import Path
from mathutils import Vector, Quaternion
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'docs/evidence/vector-fighter/f10'
OUT.mkdir(exist_ok=True,parents=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f9-fp-detail.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rig.animation_data_clear()
for p in rig.pose.bones:p.matrix_basis.identity()

# Redraw each authored contour ring with rounded corners. Same topology and
# thickness pairs; keep the central crown and smooth the actual silhouette.
for side in ['Left','Right']:
    shell=next(o for o in bpy.context.scene.objects if side+'LowerArm contoured shell' in o.name)
    count=48
    assert len(shell.data.vertices)==count*16
    for ring in range(16):
        points=[shell.data.vertices[ring*count+i].co.copy() for i in range(count)]
        for iteration in range(5):
            points=[points[i]*.5+(points[(i-1)%count]+points[(i+1)%count])*.25 for i in range(count)]
        for i,p in enumerate(points):shell.data.vertices[ring*count+i].co=p
    for p in shell.data.polygons:p.use_smooth=True
    a=rig.data.bones[side+'LowerArm'].head_local;b=rig.data.bones[side+'Hand'].head_local
    axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lateral=axis.cross(front).normalized();sign=1 if side=='Left' else -1
    for loop in shell.data.loops:
        p=shell.data.vertices[loop.vertex_index].co;t=(p-a).dot(axis)/(b-a).length;rel=p-(a+(b-a)*t)
        angle=math.atan2(rel.dot(lateral),rel.dot(front))+.12*sign
        shell.data.uv_layers[0].data[loop.index].uv=((sign*angle+1.5)/3,t)

# Relax the existing low-resolution lower sleeve only. Subdivision supplies
# vertices for the shaped transition; the actual form edit is localized below.
cloth=bpy.data.objects['VECTOR tailored UpperArm fabric']
bpy.ops.object.select_all(action='DESELECT');cloth.select_set(True);bpy.context.view_layer.objects.active=cloth
sub=cloth.modifiers.new('Study lower sleeve interpolation','SUBSURF');sub.subdivision_type='SIMPLE';sub.levels=2
bpy.ops.object.modifier_apply(modifier=sub.name)
neighbours=[set() for _ in cloth.data.vertices]
for e in cloth.data.edges:
    a,b=e.vertices;neighbours[a].add(b);neighbours[b].add(a)
mask=[]
for v in cloth.data.vertices:
    side='Left' if v.co.x>0 else 'Right';a=rig.data.bones[side+'LowerArm'].head_local;b=rig.data.bones[side+'Hand'].head_local
    t=(v.co-a).dot(b-a)/(b-a).length_squared
    mask.append(max(0,min(1,(t+.10)/.20)))
for _ in range(12):
    points=[v.co.copy() for v in cloth.data.vertices]
    for i,v in enumerate(cloth.data.vertices):
        if neighbours[i] and mask[i]>0:
            avg=sum((points[j] for j in neighbours[i]),Vector())/len(neighbours[i])
            v.co=points[i].lerp(avg,.48*mask[i])
for p in cloth.data.polygons:p.use_smooth=True

def textile(mat,color,rough,scale,depth):
    nodes=mat.node_tree.nodes;nodes.clear();links=mat.node_tree.links
    bs=nodes.new('ShaderNodeBsdfPrincipled');bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=rough
    out=nodes.new('ShaderNodeOutputMaterial');links.new(bs.outputs[0],out.inputs[0])
    coord=nodes.new('ShaderNodeTexCoord');wave=nodes.new('ShaderNodeTexWave');wave.wave_type='BANDS';wave.bands_direction='DIAGONAL';wave.inputs['Scale'].default_value=scale;wave.inputs['Distortion'].default_value=.4
    links.new(coord.outputs['Object'],wave.inputs['Vector'])
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.45;bump.inputs['Distance'].default_value=depth
    links.new(wave.outputs['Color'],bump.inputs['Height']);links.new(bump.outputs[0],bs.inputs['Normal'])

glove=bpy.data.objects['VECTOR articulated gloves']
for m in glove.data.materials:
    if m.name=='material.001' or m.name.startswith('vector-glove'):
        bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');base=bs.inputs['Base Color']
        if base.is_linked:
            old=base.links[0].from_socket;mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(.10,.13,.16,1)
            m.node_tree.links.new(old,mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],base)
        for link in list(bs.inputs['Roughness'].links):m.node_tree.links.remove(link)
        bs.inputs['Roughness'].default_value=.70
        for node in m.node_tree.nodes:
            if node.type=='NORMAL_MAP':node.inputs['Strength'].default_value=.45
    elif m.name.startswith('orbital-seam'):
        bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(.075,.09,.10,1);bs.inputs['Roughness'].default_value=.8
for m in cloth.data.materials:textile(m,(.022,.030,.038),.84,950,.00010)

# Same display blue as the game; neutral runtime source remains untouched.
for m in bpy.data.materials:
    if m.name.startswith('vector-fighter-identity-forearm') or m.name=='vector-ceramic.001':
        bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');base=bs.inputs['Base Color']
        if base.is_linked:
            old=base.links[0].from_socket;mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(.07,.55,1,1)
            m.node_tree.links.new(old,mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],base)
        else:base.default_value=(.025,.18,.32,1)
for o in bpy.context.scene.objects:
    if o.type=='MESH':o.hide_render=not ('LeftLowerArm' in o.name or 'gloves' in o.name or 'UpperArm fabric' in o.name)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=48;s.cycles.use_denoising=True
s.render.resolution_x=1400;s.render.resolution_y=900;s.render.resolution_percentage=100
s.world=bpy.data.worlds.new('F10 studio');s.world.use_nodes=True
wn=s.world.node_tree.nodes;wl=s.world.node_tree.links;wn.clear()
bg=wn.new('ShaderNodeBackground');wo=wn.new('ShaderNodeOutputWorld');wl.new(bg.outputs[0],wo.inputs[0]);bg.inputs[0].default_value=(.055,.065,.08,1);bg.inputs[1].default_value=.5
s.view_settings.view_transform='AgX'
center=Vector((.535,.055,1.045))
for offset,power,size in [((.2,-.8,.7),9,.65),((.5,.4,.3),6.5,.5),((-.5,-.2,.2),4.5,.4)]:
    bpy.ops.object.light_add(type='AREA',location=center+Vector(offset));o=bpy.context.object;o.data.energy=power;o.data.size=size;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=center+Vector((.45,-.9,.45)))
cam=bpy.context.object;s.camera=cam;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.rotation_euler=(cam.rotation_euler.to_quaternion()@Quaternion((0,0,1),math.radians(-45))).to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=.55
s.render.filepath=str(OUT/'soft-transition-study.png')
bpy.context.preferences.filepaths.save_version=0;bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'soft-transition-study.blend'),compress=True);bpy.ops.render.render(write_still=True)
(OUT/'study.json').write_text(json.dumps({'source':'scripts/vector_fighter/f9-fp-detail.blend','status':'shape/material study only; not approved or exported','changes':['rounded existing shell contour rings','localized relaxation of existing lower sleeve','preserved glove texture with moderated normals and separated textile finish'],'not_implemented':'elbow swivel correction; actual FPS verification'},indent=2)+'\n')
