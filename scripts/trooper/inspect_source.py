"""Preserved source-only geometry inspection and orthographic QA renders."""
import bpy,json
from mathutils import Vector
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/trooper-2k.glb'))
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in meshes:
 pts=[o.matrix_world@v.co for v in o.data.vertices]
 print('MESH',o.name,len(pts),len(o.data.polygons),'bounds',tuple(min(p[i] for p in pts) for i in range(3)),tuple(max(p[i] for p in pts) for i in range(3)))
points=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]; lo=Vector([min(p[i] for p in points) for i in range(3)]); hi=Vector([max(p[i] for p in points) for i in range(3)]); center=(lo+hi)/2; height=(hi-lo).z
print('TOTAL BOUNDS',tuple(lo),tuple(hi))
scene=bpy.context.scene; scene.render.engine='BLENDER_EEVEE'; scene.render.resolution_x=800;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(0.14,0.17,0.22,1)
for name,offset,power,size in [('Key',(2,-3,4),1600,4),('Fill',(-3,-1,2),1000,3),('Rim',(0,3,3),1800,3)]:
 bpy.ops.object.light_add(type='AREA',location=center+Vector(offset)*height/2);l=bpy.context.object;l.data.energy=power*(height/2)**2;l.data.shape='DISK';l.data.size=size*height/2;l.rotation_euler=(center-l.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=height*1.3
for name,offset in [('front',(0,-3,0.1)),('side',(3,0,0.1)),('back',(0,3,0.1))]:
 cam.location=center+Vector(offset)*height;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();scene.render.filepath=str(ROOT/f'docs/evidence/trooper-animation-pipeline/source-{name}.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/inspected-source.blend'))
