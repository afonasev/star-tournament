"""F9 material/surface studies on F8 source. Never exports runtime assets.

Three packed Blender studies; the camera is a studio closeup, not gameplay.
Run: blender -b -noaudio --python scripts/vector_fighter/study_fp_detail.py
"""
import bpy, math, json, hashlib
import numpy as np
from pathlib import Path
from mathutils import Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'docs/evidence/vector-fighter/f9'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = ROOT / 'scripts/vector_fighter/f8-glove-repair.blend'

def image(name, pixels):
    h,w = pixels.shape[:2]
    im = bpy.data.images.new(name, width=w, height=h)
    im.pixels.foreach_set(pixels.astype(np.float32).ravel())
    im.pack()
    return im

def surface(kind):
    n=1024
    u,t=np.meshgrid(np.linspace(-1.5,1.5,n), np.linspace(0,1,n))
    edge=.72-.14*np.cos(t*math.pi)
    seam=np.exp(-((np.abs(u)-edge)/.013)**2)
    cuff=np.exp(-((t-.84)/.009)**2)
    grain=(np.sin(u*2900)*np.sin(t*4100)+np.sin(u*1971+t*3301))*.5
    rgb=np.zeros((n,n,3)); rgb[:]=(.027,.235,.48)
    height=np.zeros_like(t); rough=np.full_like(t,.40)
    if kind==1:
        # Satin ceramic: two recessed swept margins, one integral wrist seat.
        mask=np.clip(seam+cuff,0,1)
        rgb*=1-mask[...,None]*.70
        height-=mask*.65
        flank=np.abs(u)>edge+.035
        rgb[flank]*=.62;rough[flank]=.58
        ticks=(t>.18)&(t<.34)&(np.abs(u+.48)<.11)&((t*100)%1<.16)
        rgb[ticks]=(.16,.36,.49)
        height+=grain*.018
    elif kind==2:
        # Bonded woven sidewall, retaining a broad solid identity crest.
        flank=np.abs(u)>(.44+.12*np.sin(t*math.pi))
        weave=(np.sin(u*620+t*970)*np.sin(u*620-t*970))
        rgb[flank]=(.036,.052,.065)
        rgb[flank]*=(1+.23*weave[flank,None])
        height[flank]=weave[flank]*.10;rough[flank]=.78
        border=np.exp(-((np.abs(u)-(.44+.12*np.sin(t*math.pi)))/.014)**2)
        rgb*=1-border[...,None]*.65; height-=border*.4
        # Embedded seam pick pattern, not added mesh straps.
        stitch=(border>.25)&((t*115)%1<.32)
        rgb[stitch]=(.16,.22,.25)
        height[stitch]+=.18
    else:
        # Molded flex zone flowing into the wrist, with wide quiet upper shell.
        flex=(t>.49)&(np.abs(u)<.82)
        phase=t*115+2.8*np.abs(u)
        ribs=(.5+.5*np.cos(phase*math.pi))**4
        rgb[flex]*=(.34+.5*ribs[flex,None])
        height[flex]=ribs[flex]*.85;rough[flex]=.62
        rgb*=1-seam[...,None]*.55;height-=seam*.35
        rough[~flex]=.34
    rgba=np.dstack((rgb,np.ones_like(t)))
    # RGB values are linear; Non-Color avoids applying an extra transfer curve.
    albedo=image('F9-%s-linear-albedo'%kind,rgba);albedo.colorspace_settings.name='Non-Color'
    relief=image('F9-%s-relief'%kind,np.dstack((height*.4+.5,)*3+(np.ones_like(t),)))
    relief.colorspace_settings.name='Non-Color'
    rm=image('F9-%s-roughness'%kind,np.dstack((rough,)*3+(np.ones_like(t),)))
    rm.colorspace_settings.name='Non-Color'
    m=bpy.data.materials.new('F9 surface study %s'%kind);m.use_nodes=True
    nodes=m.node_tree.nodes;links=m.node_tree.links;uvnode=nodes.new('ShaderNodeUVMap');uvnode.uv_map='F9 surface layout';bs=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
    if bs is None:
        bs=nodes.new('ShaderNodeBsdfPrincipled'); out=nodes.new('ShaderNodeOutputMaterial');links.new(bs.outputs[0],out.inputs['Surface'])
    bs.inputs['Metallic'].default_value=.08
    for im,socket in [(albedo,'Base Color'),(rm,'Roughness')]:
        node=nodes.new('ShaderNodeTexImage');node.image=im;links.new(uvnode.outputs[0],node.inputs['Vector'])
        links.new(node.outputs['Color'],bs.inputs[socket])
    node=nodes.new('ShaderNodeTexImage');node.image=relief;links.new(uvnode.outputs[0],node.inputs['Vector'])
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.55;bump.inputs['Distance'].default_value=.0012
    links.new(node.outputs['Color'],bump.inputs['Height']);links.new(bump.outputs[0],bs.inputs['Normal'])
    return m

for kind in (1,2,3):
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    rig.animation_data_clear()
    for p in rig.pose.bones:p.matrix_basis.identity()
    # Isolate the unchanged authored left arm. Hidden meshes remain in the file.
    for o in bpy.context.scene.objects:
        if o.type=='MESH':o.hide_render=not ('LeftLowerArm' in o.name or 'gloves' in o.name or 'UpperArm fabric' in o.name)
    glove=next(o for o in bpy.context.scene.objects if 'gloves' in o.name)
    # Right glove is outside the camera; leave its topology/skin untouched.
    shell=next(o for o in bpy.context.scene.objects if 'LeftLowerArm' in o.name)
    a=rig.data.bones['LeftLowerArm'].head_local
    b=rig.data.bones['LeftHand'].head_local
    axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lateral=axis.cross(front).normalized()
    uv=shell.data.uv_layers.new(name='F9 surface layout')
    for loop in shell.data.loops:
        p=rig.matrix_world.inverted()@shell.matrix_world@shell.data.vertices[loop.vertex_index].co
        t=(p-a).dot(axis)/(b-a).length
        rel=p-(a+(b-a)*t)
        angle=math.atan2(rel.dot(lateral),rel.dot(front))+.12
        uv.data[loop.index].uv=((angle+1.5)/3,t)
    shell.data.uv_layers.active=uv
    shell.data.materials[0]=surface(kind)
    # Retain authored glove maps/seams, separate rubber and woven cloth response.
    for mat in glove.data.materials:
        if not mat or not mat.use_nodes:continue
        bs=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if not bs:continue
        base=bs.inputs['Base Color']
        if base.is_linked:
            old=base.links[0].from_socket
            mix=mat.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
            mix.inputs[2].default_value=(.10,.13,.16,1) if kind!=3 else (.055,.07,.085,1)
            mat.node_tree.links.new(old,mix.inputs[1]);mat.node_tree.links.new(mix.outputs[0],base)
        if 'ceramic' in mat.name:
            bs.inputs['Roughness'].default_value=.32;bs.inputs['Base Color'].default_value=(.025,.18,.32,1)
        elif 'titanium' not in mat.name:
            for link in list(bs.inputs['Roughness'].links):mat.node_tree.links.remove(link)
            bs.inputs['Roughness'].default_value=.76 if kind==2 else .57
    s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=24;s.cycles.use_denoising=True
    s.render.resolution_x=1400;s.render.resolution_y=900;s.render.resolution_percentage=100
    s.world=bpy.data.worlds.new('F9 studio');s.world.use_nodes=True
    wn=s.world.node_tree.nodes; wl=s.world.node_tree.links
    bg=wn.new('ShaderNodeBackground');wo=wn.new('ShaderNodeOutputWorld');wl.new(bg.outputs[0],wo.inputs[0])
    bg.inputs[0].default_value=(.055,.065,.08,1);bg.inputs[1].default_value=.5
    s.view_settings.view_transform='AgX'
    center=Vector((.535,.055,1.045))
    for offset,power,size in [((.2,-.8,.7),9,.65),((.5,.4,.3),6.5,.5),((-.5,-.2,.2),4.5,.4)]:
        bpy.ops.object.light_add(type='AREA',location=center+Vector(offset));o=bpy.context.object;o.data.energy=power;o.data.size=size
        o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add(location=center+Vector((.45,-.9,.45)))
    cam=bpy.context.object;s.camera=cam;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.rotation_euler=(cam.rotation_euler.to_quaternion()@Quaternion((0,0,1),math.radians(-45))).to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=.55
    s.render.filepath=str(OUT/('direction-%s.png'%kind))
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/('direction-%s.blend'%kind)),compress=True)
    bpy.ops.render.render(write_still=True)

(OUT/'study-source.json').write_text(json.dumps({'baseline':'0bf6d8e1e5965d9eeb610ba879a6a24703e60173','source':str(SOURCE.relative_to(ROOT)),'sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'status':'material/surface concept study; not Unity/FPS acceptance','geometry':'F8 unchanged; no new overlays; procedural relief is shading only','runtime_assets':'unchanged'},indent=2)+'\n')
