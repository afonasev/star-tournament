"""Weapon-coherent upper-body adaptation of the existing trooper v2 clips.
All coordinates below are an immutable offline asset recipe in metres, not gameplay tuning.
"""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector, Matrix, Euler
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts/trooper'))
import anatomy
from unity_grip_util import set_digits
LIB=ROOT/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/animation-pipeline-v2'
OUT=ROOT/'.local/trooper-shipping';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(LIB/'trooper-rig.blend'))
rig=bpy.data.objects['TrooperHumanoid'];arm=rig.data;scene=bpy.context.scene
for o in list(scene.objects):
    if o!=rig and not(o.type=='MESH' and o.find_armature()==rig):bpy.data.objects.remove(o,do_unlink=True)
meshes=[o for o in scene.objects if o.type=='MESH']
audit=json.loads((LIB/'source-audit.json').read_text());scale=audit['scaleFactor'];floor=audit['sourceFloorZ']
anatomy.GRIP_ANGLES=json.loads((ROOT/'docs/evidence/trooper-animation-pipeline/grip-recipe.json').read_text())['anglesDegrees']
def point(p):return Vector((p[0]*scale,p[1]*scale,(p[2]-floor)*scale))
def orient(name,direction):
    pb=rig.pose.bones[name];rest=arm.bones[name].matrix_local.to_quaternion()
    q=(rest@Vector((0,1,0))).rotation_difference(Vector(direction).normalized())@rest
    pb.matrix=Matrix.LocRotScale(pb.head.copy(),q,Vector((1,1,1)));bpy.context.view_layer.update()
def apply_left_shoulder_policy(wrist,hand_q):
    """Keep 20% of the solved support-arm swing at the shoulder seam.
    Offline rig policy from the exported deformation audit; not gameplay tuning.
    """
    shoulder=rig.pose.bones['LeftShoulder'];upper=rig.pose.bones['LeftUpperArm']
    shoulder_rest=arm.bones['LeftShoulder'].matrix_local.to_quaternion()
    upper_rest=arm.bones['LeftUpperArm'].matrix_local.to_quaternion()
    ds=shoulder.matrix.to_quaternion()@shoulder_rest.inverted()
    du=upper.matrix.to_quaternion()@upper_rest.inverted()
    q=ds.slerp(du,.2)@shoulder_rest
    shoulder.matrix=Matrix.LocRotScale(shoulder.head.copy(),q,Vector((1,1,1)));bpy.context.view_layer.update()
    limb('Left',wrist)
    hand=rig.pose.bones['LeftHand'];hand.matrix=Matrix.LocRotScale(hand.head.copy(),hand_q,Vector((1,1,1)));bpy.context.view_layer.update()
def limb(side,target):
    upper=side+'UpperArm';lower=side+'LowerArm';a=rig.pose.bones[upper].head.copy()
    l1=arm.bones[upper].length;l2=arm.bones[lower].length;vec=target-a
    d=max(abs(l1-l2)+1e-5,min(vec.length,l1+l2-1e-5));u=vec.normalized()
    pole=Vector((1 if side=='Left' else -1,.3,-1));v=(pole-u*pole.dot(u)).normalized()
    h=(l1*l1-l2*l2+d*d)/(2*d);mid=a+u*h+v*math.sqrt(max(0,l1*l1-h*h))
    orient(upper,mid-a);orient(lower,a+u*d-mid)
# Same source shotgun in both derivatives; original GLB metres and -Y Blender forward.
before=set(scene.objects);bpy.ops.import_scene.gltf(filepath=str(ROOT/'unity/Assets/StarTournament/Art/double-barrel-shotgun-lod0.glb'))
gun_objects=[o for o in scene.objects if o not in before]
gun=bpy.data.objects.new('trooper:rig:weapon-mount',None);scene.collection.objects.link(gun)
for o in gun_objects:
    if not o.parent:o.parent=gun
# Thickness derived from source gauge diameter relative to source grip cross section.
# The asset is a compact shotgun; original source dimensions remain unchanged in Git.
weapon_scale=.32
gun.scale=(weapon_scale,)*3;gun.location=(.055,-.29,1.30);bpy.context.view_layer.update()
def mesh_center(name):
    o=next(o for o in gun_objects if o.name==name)
    return sum((o.matrix_world@v.co for v in o.data.vertices),Vector())/len(o.data.vertices)
right=mesh_center('weapon:grip')
left=mesh_center('weapon:bridge')+Vector((0,-.015,-.012))
# Preserve an independently inspectable contact recipe before joining geometry.
base_gun=gun.matrix_world.copy()
rig.animation_data.action=bpy.data.actions['aim'];scene.frame_set(1);bpy.context.view_layer.update()
chest=rig.pose.bones['Chest'].matrix.copy()
fit_bones=[pb.name for pb in rig.pose.bones if any(x in pb.name for x in ['Shoulder','UpperArm','LowerArm','Hand','Thumb','Index','Middle','Ring','Little'])]
# Source-frame bases let the contact fitter reset to exactly the same authored
# aim sample that production baking starts from, before any IK/shoulder policy.
fit_baseline={name:list(map(list,rig.pose.bones[name].matrix_basis)) for name in fit_bones}
refs={};local={}
for side,sign in [('Left',1),('Right',-1)]:
    anatomy.orient_combat_hand(rig,side);bpy.context.view_layer.update()
    refs[side]=rig.pose.bones[side+'Hand'].matrix.to_quaternion()
    local[side]=arm.bones[side+'Hand'].matrix_local.inverted()@point(anatomy.xyz((.485,.07,.445),sign))
# Tilt trigger palm with the real source pistol-grip longitudinal axis.
from mathutils import Quaternion
refs['Right']=Quaternion(Vector((1,0,0)),-.18)@refs['Right']
# Cache all old bone samples first, so overwritten curves cannot feed into later frames.
clips=['idle','walk','run','aim','fire','hit','death'];sampled={}
for name in clips:
    action=bpy.data.actions[name];rig.animation_data.action=action;a,b=map(int,action.frame_range);rows=[]
    for f in range(a,b+1):
        scene.frame_set(f);rows.append({pb.name:pb.matrix_basis.copy() for pb in rig.pose.bones})
    sampled[name]=(a,b,rows)
fit=json.loads((OUT/'contact-fit.json').read_text()) if '--fitted' in sys.argv else None
changed=[pb for pb in rig.pose.bones if any(x in pb.name for x in ['Shoulder','UpperArm','LowerArm','Hand','Thumb','Index','Middle','Ring','Little'])]
for name in clips:
    rig.animation_data.action=bpy.data.actions[name];a,b,rows=sampled[name]
    for f,row in zip(range(a,b+1),rows):
        scene.frame_set(f)
        for pb in rig.pose.bones:pb.matrix_basis=row[pb.name]
        bpy.context.view_layer.update()
        if name=='death':continue # Original authored fall retained; gun follows original right hand.
        delta=rig.pose.bones['Chest'].matrix@chest.inverted();rot=delta.to_quaternion();phase=(f-a)/(b-a)
        recoil=.026*math.exp(-((phase-.22)/.10)**2) if name=='fire' else 0
        shift=rot@Vector((0,recoil,0)); G=delta@base_gun;G.translation+=shift
        for side,target in [('Right',right),('Left',left)]:
            q=rot@refs[side];g=delta@target+shift
            if fit:
                q=rot@(Euler(tuple(math.radians(a) for a in fit[side].get('rotationDegrees',[0,0,0])),'XYZ').to_quaternion()@refs[side])
            wrist=g-q@local[side]
            if fit:wrist+=rot@Vector(fit[side]['offset'])
            limb(side,wrist)
            pb=rig.pose.bones[side+'Hand'];pb.matrix=Matrix.LocRotScale(pb.head.copy(),q,Vector((1,1,1)))
            bpy.context.view_layer.update()
            if side=='Left':apply_left_shoulder_policy(wrist,q)
        anatomy.pose_twists(rig)
        if fit:
            for side in ['Left','Right']:set_digits(rig,side,fit[side]['angles'],fit[side]['opposition'])
        else:anatomy.pose_digits(rig,'fire' if name=='fire' else 'aim',phase)
        bpy.context.view_layer.update()
        for pb in changed:
            pb.keyframe_insert('location',frame=f,group=pb.name);pb.keyframe_insert('rotation_quaternion',frame=f,group=pb.name)
    print('REBAKED',name,flush=True)
rig.animation_data.action=bpy.data.actions['aim'];scene.frame_set(1);bpy.context.view_layer.update()
# Bone parenting preserves world transform through Blender's bone-tail offset.
gun.parent=rig;gun.parent_type='BONE';gun.parent_bone='RightHand';bpy.context.view_layer.update();gun.matrix_world=base_gun
bpy.context.view_layer.update()
# Join static meshes into one mesh; no new character/gun geometry is created.
static=[o for o in gun_objects if o.type=='MESH'];bpy.ops.object.select_all(action='DESELECT')
for o in static:o.select_set(True)
bpy.context.view_layer.objects.active=static[0];bpy.ops.object.join();joined=bpy.context.object;mw=joined.matrix_world.copy();joined.parent=gun;joined.matrix_world=mw;joined.name='weapon:joined'
for o in list(gun_objects):
    try:
        if o!=joined and o.name in bpy.data.objects:bpy.data.objects.remove(o,do_unlink=True)
    except ReferenceError:pass
recipe={'version':'actual-shotgun-grip-v2','gunScale':weapon_scale,'gunMatrix':list(map(list,base_gun)),'rightGrip':list(right),'leftSupport':list(left),'handLocalGauge':{k:list(v) for k,v in local.items()},'fitBaselineMatrixBasis':fit_baseline,'handRefs':{k:list(v) for k,v in refs.items()},'sourceScale':scale,'sourceFloor':floor,'method':'one rigid existing shotgun, measured grip and bridge, two-bone IK from hand-local grasp centers, original digits/twists and lower-body clips'}
(OUT/'grip-recipe.json').write_text(json.dumps(recipe,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'trooper-gripped.blend'))
print('GRIP BAKE SAVED',OUT,flush=True)
