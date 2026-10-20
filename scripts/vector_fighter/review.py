"""Render the actual exported asset; never use concept art as implementation evidence."""
import bpy,math,json,hashlib,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'docs/evidence/vector-fighter';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
source=ROOT/'.local/vector-fighter/candidate-body.glb' if '--candidate' in sys.argv else ROOT/'unity/Assets/StarTournament/Art/trooper-body-animated.glb'
bpy.ops.import_scene.gltf(filepath=str(source))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
for t in rig.animation_data.nla_tracks:t.mute=True
rig.animation_data.action=None
for p in rig.pose.bones:p.matrix_basis.identity()
# Bone-space rotation is computed from model axes, for the neutral review pose only.
from mathutils import Quaternion
for side,sign in [('Left',1),('Right',-1)]:
 b=rig.pose.bones[side+'UpperArm'];axis=b.bone.matrix_local.to_3x3().inverted()@Vector((0,1,0));b.rotation_mode='QUATERNION';b.rotation_quaternion=Quaternion(axis,math.radians(28*sign))
for o in bpy.context.scene.objects:
 if o.name=='weapon:joined':o.hide_render=True
for m in bpy.data.materials:
 if m.name.startswith('vector-fighter-identity'):m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.009,.23,.26,1)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=24;s.cycles.use_denoising=True;s.render.resolution_x=1200;s.render.resolution_y=1200;s.render.resolution_percentage=100
s.world=bpy.data.worlds.new('Studio');s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs[0].default_value=(.40,.40,.40,1);s.world.node_tree.nodes['Background'].inputs[1].default_value=.5
s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast';s.view_settings.exposure=-.65
bpy.ops.mesh.primitive_plane_add(size=200);floor=bpy.context.object
m=bpy.data.materials.new('studio floor');m.diffuse_color=(.35,.35,.35,1);floor.data.materials.append(m)
for pos,power,size in [((2,-3,4),480,3),((-2,-1,2.5),290,2),((1,2,3),600,2)]:
 bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;s.camera=cam;cam.data.type='ORTHO'
for name,center,offset,scale in [('model-front',(0,0,.9),(2,-4,1.2),2.05),('model-back',(0,0,.9),(-2,4,1),2.05),('helmet',(0,0,1.66),(.35,-.65,.14),.43)]:
 center=Vector(center);cam.location=center+Vector(offset);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale;s.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
(OUT/'render-source.json').write_text(json.dumps({'source':str(source.relative_to(ROOT)),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'blender':bpy.app.version_string,'view':'studio; display teal override; imported actual mesh; no gameplay evidence'},indent=2)+'\n')
print('VECTOR_REVIEW_RENDERED')
