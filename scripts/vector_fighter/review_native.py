"""Render actual assets using skin matrices evaluated by native Unity presentation.
Not a Unity screenshot: Blender lighting, exact exported pose and current FPS framing.
"""
import sys,json,runpy,math,hashlib
from pathlib import Path
import bpy
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[2]
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
def arg(key,default):return args[args.index(key)+1] if key in args else default
OUT=Path(arg('--output',str(ROOT/'.local/vector-f23/review')));OUT.mkdir(parents=True,exist_ok=True)
fp='--fps' in args
SOURCE=Path(arg('--source',str(ROOT/('unity/Assets/StarTournament/Art/trooper-arms-animated.glb' if fp else '.local/vector-f23/reference/candidate-body.glb'))))
POSES=Path(arg('--poses',str(ROOT/'.local/vector-f23/native-poses.json')))
selected=arg('--views','idle,aim,run,walk').split(',')
sys.argv=['review.py','--source',str(SOURCE),'--output',str(OUT),'--views','none','--reference-studio','--reference-stage']
ns=runpy.run_path(str(ROOT/'scripts/vector_fighter/review.py'),run_name='review_setup')
rig=ns['rig'];scene=ns['s'];cam=ns['cam'];weapon=bpy.data.objects['weapon:joined'];weapon.hide_render=False
for bone in rig.pose.bones:bone.matrix_basis.identity()
bpy.context.view_layer.update()
weapon_rest=weapon.matrix_world.copy();weapon.parent=None;weapon.matrix_world=weapon_rest
scene.render.resolution_x=1600 if fp else 1000;scene.render.resolution_y=900 if fp else 1200
scene.view_settings.exposure=-.25
# glTFast reflects glTF X; Blender maps glTF Y-up to Z-up.
C=Matrix(((-1,0,0,0),(0,0,-1,0),(0,1,0,0),(0,0,0,1)));Ci=C.inverted()
def matrix(flat):return Matrix([flat[i:i+4] for i in range(0,16,4)])
colors={'idle':(.008,.29,.34),'aim':(.42,.018,.025),'run':(.50,.25,.008),'walk':(.22,.035,.42)}
rows=[]
for pose in json.loads(POSES.read_text())['poses']:
    name=pose['name'].split('-')[-1]
    if pose['firstPerson']!=fp or name not in selected:continue
    palette={b['name']:C@matrix(b['deformation'])@Ci for b in pose['bones']}
    # Parent-first updates avoid Blender's deferred pose dependency evaluation.
    for bone in sorted(rig.pose.bones,key=lambda b:len(b.parent_recursive)):
        if bone.name in palette:bone.matrix=palette[bone.name]@bone.bone.matrix_local
        else:bone.matrix_basis.identity()
        bpy.context.view_layer.update()
    weapon.matrix_world=C@matrix(pose['weapon'])@matrix(pose['weaponRest']).inverted()@Ci@weapon_rest
    bpy.context.view_layer.update()
    for material in bpy.data.materials:
        if material.name.startswith('vector-fighter-identity'):
            mixes=[n for n in material.node_tree.nodes if n.type=='MIX_RGB']
            if mixes:mixes[-1].inputs[2].default_value=tuple(c/.41 for c in colors[name])+(1,)
    if fp:
        offset=pose['viewOffset'];cam.location=-(C@Vector((offset['x'],offset['y'],offset['z'],0))).to_3d()
        cam.rotation_euler=Vector((0,-1,0)).to_track_quat('-Z','Y').to_euler()
        cam.data.type='PERSP';cam.data.sensor_fit='VERTICAL';cam.data.angle_y=math.radians(pose['verticalFov']);cam.data.clip_start=.01
        # Ground/background below the equipment; no invented world/gameplay scene.
    else:
        cam.data.type='ORTHO';cam.data.ortho_scale=2.08
        center=Vector((0,0,.92));cam.location=center+Vector((2.4,-4,.9));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    path=OUT/(pose['name']+'.png');scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
    rows.append({'file':path.name,'native_state':pose['state'],'time':pose['time'],'first_person':fp,'color':colors[name],'bone_count':len(palette)})
    if '--details' in args and not fp and name=='aim':
        center=(rig.pose.bones['LeftHand'].head+rig.pose.bones['RightHand'].head)/2+Vector((0,-.035,.025))
        for label,offset in [('grip-front',(.6,-.8,.22)),('grip-reverse',(-.65,-.5,.2))]:
            cam.location=center+Vector(offset);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.58
            scene.render.resolution_x=1200;scene.render.resolution_y=900;scene.render.filepath=str(OUT/(label+'.png'));bpy.ops.render.render(write_still=True)
        scene.render.resolution_x=1000;scene.render.resolution_y=1200

(OUT/'native-render.json').write_text(json.dumps({'source':str(SOURCE),'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'poses_sha256':hashlib.sha256(POSES.read_bytes()).hexdigest(),'renderer_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'renders':rows,'scope':'actual geometry with native evaluated skin/weapon pose; Blender studio, not game screenshot'},indent=2)+'\n')
