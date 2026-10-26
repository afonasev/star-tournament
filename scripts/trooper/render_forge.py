"""Render six views of the actual shipped FORGE GLB in a neutral Blender studio. No source writes."""
import bpy, math, json, hashlib
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
out=root/'.local/forge-review';out.mkdir(parents=True,exist_ok=True)
asset=root/'unity/Assets/StarTournament/Art/forge-plasma-lod0.glb'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(asset))
scene=bpy.context.scene
mesh=[o for o in scene.objects if o.type=='MESH']
pts=[o.matrix_world@Vector(p) for o in mesh for p in o.bound_box]
low=Vector([min(p[i] for p in pts) for i in range(3)])
high=Vector([max(p[i] for p in pts) for i in range(3)])
center=(low+high)*.5
scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.world=bpy.data.worlds.new('Neutral studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.055,.065,.08,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
scene.view_settings.view_transform='AgX'
bpy.ops.object.camera_add();cam=bpy.context.object;scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=max(high-low)*1.62
lights=[]
for i,(energy,size) in enumerate([(170,2),(100,2),(130,1.5)]):
 bpy.ops.object.light_add(type='AREA');light=bpy.context.object;light.data.energy=energy;light.data.shape='DISK';light.data.size=size;lights.append(light)
views=[('01-front-quarter',(1.35,-1.6,.85)),('02-side',(1.9,0,.12)),('03-rear-quarter',(-1.35,1.6,.8)),('04-front',(0,-2,.05)),('05-top',(0,0,2.2)),('06-under-quarter',(1.4,-1.2,-1.15))]
for name,offset in views:
 d=Vector(offset).normalized();cam.location=center+d*2.3;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
 right=cam.rotation_euler.to_matrix()@Vector((1,0,0));up=cam.rotation_euler.to_matrix()@Vector((0,1,0))
 for light,position in zip(lights,[d*1.4+up*1.3-right*.8,d*.5-right*1.5+up*.3,-d*.8+right*1.2+up]):
  light.location=center+position;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler()
 scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
(out/'source.json').write_text(json.dumps({'asset':str(asset),'sha256':hashlib.sha256(asset.read_bytes()).hexdigest(),'views':[a for a,b in views],'note':'Actual shipped GLB geometry and materials, Blender studio render; no asset writes.'},indent=2))
