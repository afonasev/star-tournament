"""Render source skin and actual rest skeleton side by side; no rig file mutation."""
import bpy, math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/animation-pipeline-v2'
bpy.ops.wm.open_mainfile(filepath=str(OUT/'trooper-rig.blend'))
rig=bpy.data.objects['TrooperHumanoid'];rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=True
for b in rig.pose.bones:b.matrix_basis.identity()
bpy.context.view_layer.update()
colors={}
for name,color in [('center',(.8,.85,.95,1)),('left',(.12,.75,.55,1)),('right',(1,.45,.09,1))]:
    mat=bpy.data.materials.new('QA_Bone_'+name);mat.diffuse_color=color;mat.use_nodes=True
    tree=mat.node_tree;tree.nodes.clear();shader=tree.nodes.new('ShaderNodeBsdfPrincipled');output=tree.nodes.new('ShaderNodeOutputMaterial')
    shader.inputs['Base Color'].default_value=color;tree.links.new(shader.outputs['BSDF'],output.inputs['Surface']);colors[name]=mat
for b in rig.data.bones:
    if b.name=='Root':continue
    a=b.head_local+Vector((.64,0,0));c=b.tail_local+Vector((.64,0,0));direction=c-a
    mat=colors['left' if b.name.startswith('Left') else 'right' if b.name.startswith('Right') else 'center']
    bpy.ops.mesh.primitive_cone_add(vertices=8,radius1=.018,radius2=.006,depth=direction.length,location=(a+c)/2)
    obj=bpy.context.object;obj.rotation_euler=direction.to_track_quat('Z','Y').to_euler();obj.data.materials.append(mat)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=.022,location=a);bpy.context.object.data.materials.append(mat)
rig.location.x=-.64
scene=bpy.context.scene;scene.render.resolution_x=1400;scene.render.resolution_y=900
cam=scene.camera;cam.location=(0,-5,1.12);cam.rotation_euler=(Vector((0,0,.9))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=3.4
scene.render.filepath=str(ROOT/'docs/evidence/trooper-animation-pipeline/rig-skeleton.png');bpy.ops.render.render(write_still=True)
