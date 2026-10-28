"""Actual exported GLB studio renders, not generated concept imagery."""
import bpy,math,sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'.local/pulse-mk2';OUT.mkdir(parents=True,exist_ok=True)
asset='pulse-projectile.glb' if '--projectile' in sys.argv else 'pulse-launcher-lod0.glb'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=str(ROOT/'unity/Assets/StarTournament/Art'/asset))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.world.use_nodes=True;scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.08,.09,.11,1);scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.35
scene.view_settings.view_transform='AgX'
# real exported model sits in Blender +Z-up/-Y forward.
projectile='--projectile' in sys.argv;target=Vector((0,-.10 if not projectile else 0,.045 if not projectile else 0))
def aim(obj,at):obj.rotation_euler=(at-obj.location).to_track_quat('-Z','Y').to_euler()
def light(name,loc,power,size):
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;aim(o,target)
light('key',(1,-.6,1.8),45,1.4);light('soft fill',(-1,-.1,.65),25,1.2);light('rim',(.1,1,1.3),60,1)
bpy.ops.object.camera_add(location=(1.15,-1.10,.55) if not projectile else (1.4,-.6,.55));camera=bpy.context.object;aim(camera,target);camera.data.type='ORTHO';camera.data.ortho_scale=1.35 if not projectile else 1.5;scene.camera=camera
# matte floor for shape contact, never part of export
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.23 if not projectile else -.36));floor=bpy.context.object
m=bpy.data.materials.new('studio neutral');m.diffuse_color=(.075,.08,.09,1);m.use_nodes=True;n=m.node_tree.nodes;p=n.get('Principled BSDF') or n.new('ShaderNodeBsdfPrincipled');out=n.get('Material Output') or n.new('ShaderNodeOutputMaterial');m.node_tree.links.new(p.outputs['BSDF'],out.inputs['Surface']);p.inputs['Base Color'].default_value=(.075,.08,.09,1);p.inputs['Roughness'].default_value=.8;floor.data.materials.append(m)
for name,loc in [('hero',camera.location.copy()),('opposite',Vector((-1.15,.9,.45))),('side',Vector((1.8,-.15,.12)))]:
 camera.location=loc;aim(camera,target);scene.render.filepath=str(OUT/(('projectile-' if projectile else 'launcher-')+name+'.png'));bpy.ops.render.render(write_still=True)
