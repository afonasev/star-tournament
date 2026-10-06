"""Read exported Orbital GLBs, audit contracts, render real asset and articulated hands."""
import bpy,json,hashlib,math
from pathlib import Path
from mathutils import Vector,Quaternion
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'unity/Assets/StarTournament/Art';OUT=ROOT/'docs/evidence/orbital-f3';OUT.mkdir(parents=True,exist_ok=True)
report={}
def scene():
 s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=32
 s.render.resolution_x=1600;s.render.resolution_y=1000;s.render.resolution_percentage=100
 s.world=bpy.data.worlds.new('Studio');s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs[0].default_value=(.11,.145,.20,1);s.world.node_tree.nodes['Background'].inputs[1].default_value=.45
 s.view_settings.view_transform='AgX'
 return s
def camera(s,center,offset,scale):
 bpy.ops.object.camera_add(location=center+Vector(offset));cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=scale;s.camera=cam
 for off,power,size in [((1,-1,2),110,1.5),((-1,-.5,.7),70,1),((.3,1,1),130,1)]:
  bpy.ops.object.light_add(type='AREA',location=center+Vector(off));l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(center-l.location).to_track_quat('-Z','Y').to_euler()
def render(s,name):s.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
for kind in ['weapon','arms','body']:
 bpy.ops.wm.read_factory_settings(use_empty=True)
 file='vector-shotgun-lod0.glb' if kind=='weapon' else 'trooper-'+kind+'-animated.glb'
 path=ART/file;bpy.ops.import_scene.gltf(filepath=str(path));s=scene();meshes=[o for o in s.objects if o.type=='MESH']
 triangles=0
 assert all(math.isfinite(c) for o in meshes for v in o.data.vertices for c in v.co), 'Nonfinite asset geometry'
 for o in meshes:o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
 report[kind]={'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'triangles':triangles,'materials':sorted({m.name for o in meshes for m in o.data.materials if m}),'clips':sorted(a.name for a in bpy.data.actions)}
 if kind=='weapon':
  camera(s,Vector((0,-.155,.025)),(.65,-.8,.46),.47);render(s,'equipment-model')
  center=Vector((0,-.155,.025));s.camera.location=center+Vector((.8,0,0));s.camera.rotation_euler=(center-s.camera.location).to_track_quat('-Z','Y').to_euler();render(s,'equipment-side');continue
 rig=next(o for o in s.objects if o.type=='ARMATURE');assert len(rig.data.bones)==57
 assert {'idle','walk','run','aim','fire','hit','death'} <= {a.name for a in bpy.data.actions}
 assert all(bpy.data.objects.get('vector-muzzle-'+str(i)) for i in range(2))
 if kind=='body':continue
 for track in rig.animation_data.nla_tracks:track.mute=True
 action=bpy.data.actions['aim'];rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];a,b=action.frame_range;s.frame_set(int(a+(b-a)*.25));bpy.context.view_layer.update()
 # Mirror the authored additive native support-grip correction, using right-handed Blender axes.
 rig.animation_data.action=None
 handbone=rig.data.bones['LeftHand']
 axis=handbone.matrix_local.to_3x3().inverted()@(rig.data.bones['LeftMiddleProximal'].head_local-handbone.head_local).normalized()
 rig.pose.bones['LeftHand'].rotation_quaternion @= Quaternion(axis,math.radians(55))
 row=(rig.data.bones['LeftLittleProximal'].head_local-rig.data.bones['LeftIndexProximal'].head_local).normalized()
 for digit in ['Thumb','Index','Middle','Ring','Little']:
  for segment in ['Proximal','Intermediate','Distal']:
   name='Left'+digit+segment;axis=rig.data.bones[name].matrix_local.to_3x3().inverted()@row
   rig.pose.bones[name].rotation_quaternion @= Quaternion(axis,math.radians(5 if digit=='Thumb' else 12))
 bpy.context.view_layer.update()
 gun=bpy.data.objects['weapon:joined'];center=gun.matrix_world@Vector((0,-.4,.1))
 camera(s,center,(.7,-.8,.6),.67);render(s,'hands-and-weapon')
 hand=rig.matrix_world@rig.pose.bones['LeftHand'].head
 s.camera.location=hand+Vector((.4,-.4,.32));s.camera.rotation_euler=(hand-s.camera.location).to_track_quat('-Z','Y').to_euler();s.camera.data.ortho_scale=.28;render(s,'hand-detail')
 report[kind]['weightedVertices']=sum(bool(v.groups) for o in meshes if o.find_armature() for v in o.data.vertices)
(OUT/'asset-audit.json').write_text(json.dumps(report,indent=2)+'\n')
print('ORBITAL_AUDIT_COMPLETE',report)
