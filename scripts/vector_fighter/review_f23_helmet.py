"""Comparable actual source/GLB close-ups, optional clay override."""
import bpy,sys,math,json,hashlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
SOURCE=Path(sys.argv[sys.argv.index('--source')+1])
OUT=Path(sys.argv[sys.argv.index('--output')+1]);OUT.mkdir(parents=True,exist_ok=True)
if SOURCE.suffix=='.blend':bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
else:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(SOURCE))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rig.animation_data_clear()
for p in rig.pose.bones:p.matrix_basis.identity()
for o in bpy.context.scene.objects:
    if o.name=='weapon:joined':o.hide_render=True
clay='--clay' in sys.argv
for m in bpy.data.materials:
    if not m.use_nodes:continue
    bs=m.node_tree.nodes.get('Principled BSDF')
    if bs is None:continue
    if clay:
        for socket in ['Base Color','Roughness','Metallic','Normal']:
            for link in list(bs.inputs[socket].links):m.node_tree.links.remove(link)
        bs.inputs['Base Color'].default_value=(.20,.22,.23,1)
        bs.inputs['Roughness'].default_value=.62;bs.inputs['Metallic'].default_value=0
        if 'visor' in m.name:bs.inputs['Base Color'].default_value=(.012,.016,.019,1)
    elif m.name.startswith('vector-fighter-identity'):
        base=bs.inputs['Base Color'];tint=(.009,.20,.23,1)
        if base.is_linked:
            src=base.links[0].from_socket
            mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
            reference=.15 if m.name.startswith('vector-fighter-identity-f23-helmet') else .41
            mix.inputs[2].default_value=tuple(v/reference for v in tint[:3])+(1,)
            m.node_tree.links.new(src,mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],base)
        else:base.default_value=tint
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=24;s.cycles.use_denoising=True
s.render.resolution_x=800;s.render.resolution_y=800;s.render.resolution_percentage=100
s.world=bpy.data.worlds.new('Neutral studio');s.world.use_nodes=True
background=next(n for n in s.world.node_tree.nodes if n.type=='BACKGROUND')
background.inputs[0].default_value=(.11,.12,.13,1)
background.inputs[1].default_value=.5
s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast';s.view_settings.exposure=-.2
for pos,power,size in [((1,-2,3),250,2),((-2,-1,2.5),180,2),((1,2,3),350,2)]:
    bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.size=size
    o.rotation_euler=(Vector((0,0,1.6))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=.33
views=[('front',(0,-1,.06)),('side',(1,0,.06)),('three-quarter',(.55,-1,.17)),('back',(.3,1,.14))]
for name,offset in views:
    center=Vector((0,.0,1.650));cam.location=center+Vector(offset)
    cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    s.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
(OUT/'source.json').write_text(json.dumps({'source':str(SOURCE),'sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'clay':clay,'renderer':'Blender Cycles actual geometry'},indent=2))
