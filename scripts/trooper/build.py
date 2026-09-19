"""Offline ART_LOLL candidate. Run from repository root with Blender --background --python.
All recipe numbers are DCC authoring coordinates/tolerances; none reach gameplay.
No source geometry is replaced, no production asset path is written.
"""
import bpy, bmesh, math, json, hashlib, sys
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'scripts/trooper'))
from anatomy import finger_specs, harmonic_glove, harmonic_pelvis, orient_combat_hand, pose_digits, fit_grip, twist_specs, distribute_forearm_twist, pose_twists
LIB = ROOT / '.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper'
OUT = LIB / 'animation-pipeline-v2'
EVIDENCE = ROOT / 'docs/evidence/trooper-animation-pipeline'
SOURCE = LIB / 'trooper-2k.glb'
EXPECTED = '53add01552d98244b75039c33319293c53e2eecc046b11890d6fa247fd13ef53'
OUT.mkdir(parents=True, exist_ok=True)
EVIDENCE.mkdir(parents=True, exist_ok=True)
assert hashlib.sha256(SOURCE.read_bytes()).hexdigest() == EXPECTED, 'Unreviewed source revision'
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(SOURCE))
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
# Baking world transforms before unparenting keeps the imported A-pose exactly.
source_points = []
for o in meshes:
    mw = o.matrix_world.copy()
    source_points.extend(mw @ v.co for v in o.data.vertices)
    o.data.transform(mw)
    o.parent = None
    o.matrix_world = Matrix.Identity(4)
for o in list(bpy.context.scene.objects):
    if o not in meshes:
        bpy.data.objects.remove(o, do_unlink=True)
floor = min(p.z for p in source_points)
scale = 1.8 / (max(p.z for p in source_points) - floor)
def point(x, y, z):
    return Vector((x*scale, y*scale, (z-floor)*scale))

audit = {'sourceSha256': EXPECTED, 'sourceBytes': SOURCE.stat().st_size,
         'blender': bpy.app.version_string, 'sourceFloorZ': floor,
         'scaleFactor': scale, 'targetHeightMeters': 1.8, 'meshes': [],
         'images':[{'name':im.name,'width':im.size[0],'height':im.size[1]} for im in bpy.data.images]}
components = {}
for o in meshes:
    original = len(o.data.vertices)
    bm = bmesh.new(); bm.from_mesh(o.data)
    # Weld geometric seams only; bmesh retains face-corner UVs and materials.
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bm.to_mesh(o.data); bm.free()
    vertices = o.data.vertices
    adjacency = [set() for _ in vertices]
    for e in o.data.edges:
        a,b=e.vertices; adjacency[a].add(b); adjacency[b].add(a)
    seen=set(); islands=[]
    for v in vertices:
        if v.index in seen: continue
        ids=[]; stack=[v.index]; seen.add(v.index)
        while stack:
            i=stack.pop();ids.append(i)
            for j in adjacency[i]:
                if j not in seen:seen.add(j);stack.append(j)
        islands.append(ids)
    components[o.name]=islands
    o.data.calc_loop_triangles()
    edge_faces = {}
    for poly in o.data.polygons:
        for edge in poly.edge_keys:edge_faces[edge]=edge_faces.get(edge,0)+1
    audit['meshes'].append({'name':o.name, 'material':o.data.materials[0].name,
        'sourceVertices':original,'weldedVertices':len(vertices),
        'triangles':len(o.data.loop_triangles),'components':len(islands),
        'boundaryEdges':sum(v==1 for v in edge_faces.values()),
        'nonManifoldEdges':sum(v>2 for v in edge_faces.values())})

# Rest coordinates measured in source Blender Z-up/-Y-forward space.
spec = [
 ('Root',(0,0,floor),(0,0,floor+.12),None),
 ('Hips',(0,.045,-.08),(0,.045,.10),'Root'),
 ('Spine',(0,.045,.10),(0,.045,.29),'Hips'),
 ('Chest',(0,.045,.29),(0,.04,.49),'Spine'),
 ('UpperChest',(0,.04,.49),(0,.035,.575),'Chest'),
 ('Neck',(0,.035,.575),(0,.015,.66),'UpperChest'),
 ('Head',(0,.015,.66),(0,.015,.84),'Neck')]
for side,sign in [('Left',1),('Right',-1)]:
    spec += [
      (side+'Shoulder',(sign*.065,.04,.49),(sign*.225,.065,.46),'UpperChest'),
      (side+'UpperArm',(sign*.225,.065,.46),(sign*.409,.078,.275),side+'Shoulder'),
      (side+'LowerArm',(sign*.409,.078,.275),(sign*.566,.08,.113),side+'UpperArm'),
      (side+'Hand',(sign*.566,.08,.113),(sign*.675,.076,.002),side+'LowerArm'),
      (side+'UpperLeg',(sign*.14,.045,-.10),(sign*.155,.022,-.49),'Hips'),
      (side+'LowerLeg',(sign*.155,.022,-.49),(sign*.177,.052,-.925),side+'UpperLeg'),
      (side+'Foot',(sign*.177,.052,-.925),(sign*.177,-.080,-.994),side+'LowerLeg'),
      (side+'Toes',(sign*.177,-.080,-.994),(sign*.177,-.145,-.994),side+'Foot')]
spec += finger_specs()+twist_specs()
arm = bpy.data.armatures.new('TrooperHumanoid');rig=bpy.data.objects.new('TrooperHumanoid',arm)
bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,a,b,parent in spec:
    bone=arm.edit_bones.new(name);bone.head=point(*a);bone.tail=point(*b)
    if parent:bone.parent=arm.edit_bones[parent]
    bone.use_deform=name!='Root'
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front=True
rig['candidateOnly']=True
rig['source']='https://sketchfab.com/3d-models/sci-fi-soldier-futuristic-combat-trooper-de876bfdce1c47a4aa67670faee7208e'
rig['attribution']='Sci-Fi Soldier / Futuristic Combat Trooper by ART_LOLL. CC BY 4.0. Modified: normalization, weld, humanoid skin rig, authored animation clips.'
rig['license']='https://creativecommons.org/licenses/by/4.0/'
rig['rigVersion']='trooper-humanoid-v2'
rig['unityAvatarValidated']=False

def smooth(x,a,b):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def mix(a,b,t):
    result={k:v*(1-t) for k,v in a.items()}
    for k,v in b.items():result[k]=result.get(k,0)+v*t
    return {k:v for k,v in result.items() if v>1e-6}
def weights(o,p,island):
    x,y,z=p;side='Left' if x>=0 else 'Right';ax=abs(x)
    center,minz,maxz,island_count=island
    mat=o.data.materials[0].name
    if mat=='TECI_helmet':
        if minz<.52 and maxz<.70:return mix({'Neck':1},{'Head':1},smooth(z,.55,.65))
        return {'Head':1}
    if mat in ('gear','jumpjet'):return {'Chest':1}
    if mat=='torso':
        if maxz<.05:return {'Hips':1}
        # Rigid vest plates and collar; subtle breathing belongs to Chest.
        if island_count<600:return {'Chest':1}
        return mix({'Spine':1},{'Chest':1},smooth(z,.08,.26))
    if mat=='material':
        if island_count<100: # Hard shell islands: bind as one unit.
            if abs(center.x)<.37:return {side+'UpperArm':1}
            if abs(center.x)>.54:return {side+'Hand':1}
            return {side+'LowerArm':1}
        a=mix({side+'UpperArm':1},{side+'LowerArm':1},smooth(ax,.32,.50))
        a=mix(a,{side+'Hand':1},smooth((ax-z)/math.sqrt(2),.275,.345))
        return mix({side+'Shoulder':1},a,smooth(ax,.14,.25))
    if mat=='pants':
        if island_count<100:
            if center.z<-.86:return {side+'Foot':1}
            if center.z<-.34:return mix({side+'LowerLeg':1},{side+'UpperLeg':1},smooth(center.z,-.63,-.35))
            return {side+'UpperLeg':1}
        if maxz<-.84:return {side+'Foot':1}
        a=mix({side+'LowerLeg':1},{side+'UpperLeg':1},smooth(z,-.63,-.35))
        a=mix({side+'Foot':1},a,smooth(z,-.95,-.845))
        # Crotch seam remains attached to hips, avoiding side discontinuity.
        hip=smooth(z,-.26,-.035)*(1-.3*smooth(ax,.06,.18))
        if maxz>.05:hip=max(hip,(1-smooth(ax,.015,.16))*smooth(z,-.34,-.23))
        return mix(a,{'Hips':1},hip)
    raise ValueError(mat)

max_influences=0
for o in meshes:
    groups={b.name:o.vertex_groups.new(name=b.name) for b in arm.bones if b.use_deform}
    for island in components[o.name]:
        coords=[o.data.vertices[i].co for i in island]
        meta=(sum(coords,Vector())/len(coords), min(v.z for v in coords), max(v.z for v in coords), len(island))
        harmonic=harmonic_pelvis(o,island) if o.data.materials[0].name=='pants' and meta[1]<-.3 and meta[2]>.05 else None
        if o.data.materials[0].name=='material' and abs(meta[0].x)>.60 and meta[3]>100:
            harmonic=harmonic_glove(o,island,{i:weights(o,o.data.vertices[i].co,meta) for i in island})
        for i in island:
            v=o.data.vertices[i];w=harmonic[i] if harmonic else weights(o,v.co,meta)
            if o.data.materials[0].name=='material':w=distribute_forearm_twist(meta[0] if meta[3]<100 else v.co,w)
            total=sum(w.values())
            max_influences=max(max_influences,len(w))
            for name,value in w.items():groups[name].add([i],value/total,'REPLACE')
    for v in o.data.vertices:v.co=point(*v.co)
    mod=o.modifiers.new('HumanoidSkin','ARMATURE');mod.object=rig
    # Use linear skinning in DCC too: dual quaternion would conceal GLB artifacts.
    mod.use_deform_preserve_volume=False
    o.parent=rig
    o['sourceMesh']=o.name
assert max_influences<=4

audit['maxInfluences']=max_influences
grip_fit=fit_grip(rig,next(o for o in meshes if o.data.materials[0].name=='material'),scale,floor)
(OUT/'grip-recipe.json').write_text(json.dumps(grip_fit,indent=2))
print('GRIP FIT',json.dumps(grip_fit),flush=True)
bone_json={n:{'head':list(arm.bones[n].head_local),'tail':list(arm.bones[n].tail_local),'parent':p} for n,a,b,p in spec}
(OUT/'humanoid-map.json').write_text(json.dumps({'version':'trooper-humanoid-v2','dccAxes':'Z up, -Y forward','gltfAxes':'Y up, +Z forward','restPose':'source A pose','bones':bone_json},indent=2))
scene=bpy.context.scene;scene.render.fps=30
for pb in rig.pose.bones:pb.rotation_mode='QUATERNION'

def reset():
    for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()

def local_rotate(name,axis,angle):
    rig.pose.bones[name].rotation_quaternion=Quaternion(Vector(axis),math.radians(angle))
    bpy.context.view_layer.update()

def orient(name,direction):
    pb=rig.pose.bones[name];rest=arm.bones[name].matrix_local.to_quaternion()
    q=(rest@Vector((0,1,0))).rotation_difference(Vector(direction).normalized())@rest
    pb.matrix=Matrix.LocRotScale(pb.head.copy(),q,Vector((1,1,1)))
    bpy.context.view_layer.update()

def limb(upper,lower,target,pole):
    a=rig.pose.bones[upper].head.copy();target=Vector(target)
    l1=arm.bones[upper].length;l2=arm.bones[lower].length
    vec=target-a;d=min(vec.length,l1+l2-1e-5);d=max(d,abs(l1-l2)+1e-5);u=vec.normalized()
    pole=Vector(pole);v=(pole-u*pole.dot(u)).normalized()
    h=(l1*l1-l2*l2+d*d)/(2*d);bend=math.sqrt(max(0,l1*l1-h*h));mid=a+u*h+v*bend
    orient(upper,mid-a);orient(lower,a+u*d-mid)

def pose(kind,t):
    reset();phase=2*math.pi*t
    if kind=='rest':return
    # Grounded, in-place clips; horizontal locomotion remains the caller's job.
    bob=.003*math.sin(phase) if kind=='idle' else 0
    if kind in ('walk','run'):bob=(.012 if kind=='walk' else .024)*math.cos(phase*2)
    crouch=.045 if kind not in ('walk','run') else (.055 if kind=='walk' else .095)
    rig.pose.bones['Hips'].location=arm.bones['Hips'].matrix_local.to_3x3().inverted() @ Vector((0,0,bob-crouch))
    bpy.context.view_layer.update()
    lean=0
    if kind=='run':lean=7
    if kind in ('aim','fire'):lean=3
    if kind=='fire':lean-=2.5*math.exp(-((t-.22)/.10)**2)
    if kind=='hit':lean=-12*math.sin(math.pi*t)**2
    local_rotate('Spine',(1,0,0),lean)
    local_rotate('Chest',(0,1,0),1.5*math.sin(phase) if kind in ('walk','run') else 0)
    for side,sign in [('Left',1),('Right',-1)]:
        q=phase+(0 if sign==1 else math.pi)
        stride=(.15 if kind=='walk' else .25) if kind in ('walk','run') else 0
        lift=(.07 if kind=='walk' else .14)*max(0,math.sin(q)) if stride else 0
        ankle=arm.bones[side+'LowerLeg'].tail_local.copy()
        ankle.y+=stride*math.cos(q);ankle.z+=lift
        if kind in ('aim','fire'):
            ankle.x+=sign*.025;ankle.y+=sign*.075
        limb(side+'UpperLeg',side+'LowerLeg',ankle,(0,-1,0))
        # Flat sole through stance; articulated toe reserved for future pass.
        pb=rig.pose.bones[side+'Foot'];pb.matrix=Matrix.LocRotScale(pb.head.copy(),arm.bones[side+'Foot'].matrix_local.to_quaternion(),Vector((1,1,1)))
        bpy.context.view_layer.update()
        shoulder=rig.pose.bones[side+'UpperArm'].head.copy()
        if kind in ('aim','fire'):
            recoil=.026*math.exp(-((t-.22)/.10)**2) if kind=='fire' else 0
            target=Vector((.07,-.325+recoil,1.25)) if side=='Left' else Vector((-.075,-.23+recoil,1.27))
            pole=(sign*1,0,-1)
        else:
            swing=.12*math.cos(q+math.pi) if stride else 0
            target=shoulder+Vector((sign*.07,-.04+swing,-.42 if kind!='run' else -.30))
            if kind=='run':target.y-=.15
            if kind=='hit':target.y-=.07*math.sin(math.pi*t)
            pole=(sign*.25,1,0)
        limb(side+'UpperArm',side+'LowerArm',target,pole)
        if kind in ('aim','fire'):
            orient_combat_hand(rig,side)
            bpy.context.view_layer.update()
        else:
            orient(side+'Hand',rig.pose.bones[side+'LowerArm'].tail-rig.pose.bones[side+'LowerArm'].head)
    pose_twists(rig)
    bpy.context.view_layer.update()
    pose_digits(rig,kind,t)
    bpy.context.view_layer.update()
    if kind=='death':
        u=smooth(t,.12,.92)
        # Authored fall, not ragdoll; floor correction is baked from actual mesh.
        rig.pose.bones['Root'].rotation_quaternion=Quaternion(Vector((1,0,0)),math.radians(-88)*u)
        rig.pose.bones['Root'].location=arm.bones['Root'].matrix_local.to_3x3().inverted() @ Vector((0,.10*u,0))
        bpy.context.view_layer.update()
        deps=bpy.context.evaluated_depsgraph_get()
        min_z=min((o.matrix_world@v.co).z for o in meshes for v in o.evaluated_get(deps).data.vertices)
        rig.pose.bones['Root'].location += arm.bones['Root'].matrix_local.to_3x3().inverted() @ Vector((0,0,-min_z))
        bpy.context.view_layer.update()

clips=[('idle',2.4,True),('walk',1.2,True),('run',.8,True),('aim',1.6,True),('fire',.4,False),('hit',.6,False),('death',1.6,False)]
rig.animation_data_create()
for name,duration,loop in clips:
    action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action
    count=round(duration*scene.render.fps)
    for f in range(count+1):
        scene.frame_set(f+1);pose(name,f/count)
        for pb in rig.pose.bones:
            pb.keyframe_insert('location',frame=f+1,group=pb.name)
            pb.keyframe_insert('rotation_quaternion',frame=f+1,group=pb.name)
    action['loop']=loop;action['durationSeconds']=duration
    print('BAKED',name,count+1,flush=True)
rig.animation_data.action=None;reset()
for name,duration,loop in clips:
    track=rig.animation_data.nla_tracks.new();track.name=name
    strip=track.strips.new(name,1,bpy.data.actions[name]);track.mute=True
    strip.action_frame_start=1;strip.action_frame_end=1+round(duration*30)
# glTF exporter enumerates actions on this armature via ACTIONS mode.
clip_doc={'version':'trooper-authored-clips-v2','source':'Locally authored; no third-party animation pack or retarget dependency.',
          'clips':[{'name':n,'durationSeconds':d,'loop':l,'rootMotion':n=='death'} for n,d,l in clips],
          'limitations':['Finger rig and local grip fixture; final game-weapon fit not certified','In-place locomotion is illustrative; not calibrated to game speed','No Unity Avatar/import validation','No LOD/performance acceptance','Death is authored floor-corrected motion, not ragdoll']}
(OUT/'clips.json').write_text(json.dumps(clip_doc,indent=2))
(OUT/'source-audit.json').write_text(json.dumps(audit,indent=2))
bpy.ops.object.select_all(action='DESELECT')
for o in meshes+[rig]:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.gltf(filepath=str(OUT/'trooper-skinned-animated.glb'),export_format='GLB',use_selection=True,
    export_animations=True,export_animation_mode='ACTIONS',export_force_sampling=True,
    export_extras=True,export_yup=True,export_skins=True,export_all_influences=False)

if '--export-only' in sys.argv:
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'trooper-rig.blend'))
    raise SystemExit(0)

# Reusable neutral presentation stage, no game or audio initialization.
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=760;scene.render.resolution_y=840;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('StudioWorld');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.09,.115,.16,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.005));ground=bpy.context.object;ground.name='QA_Ground'
mat=bpy.data.materials.new('QA_Ground');mat.diffuse_color=(.06,.08,.115,1);ground.data.materials.append(mat)
for name,loc,power,size in [('Key',(2,-3,4),900,3),('Fill',(-3,-1,2),600,3),('Rim',(0,3,3),1200,2)]:
    bpy.ops.object.light_add(type='AREA',location=loc);light=bpy.context.object;light.name='QA_'+name;light.data.energy=power;light.data.size=size
    light.rotation_euler=(Vector((0,0,.9))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add();cam=bpy.context.object;cam.name='QA_Camera';scene.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.2

def camera(view,death=False):
    target=Vector((0,.4,.4)) if death else Vector((0,0,.92))
    offset={'front':(0,-5,.1),'side':(5,0,.1),'three-quarter':(3,-5,1.2)}[view]
    cam.location=target+Vector(offset);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=2.4 if death else 2.2

def render(name,view):
    camera(view,name.startswith('death'));scene.render.filepath=str(EVIDENCE/(name+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
rig.animation_data.action=None;reset()
for view in ['front','side','three-quarter']:render('rig-rest',view)
for name,duration,loop in clips:
    rig.animation_data.action=bpy.data.actions[name]
    fractions=[0,.25,.5,.75,1] if name in ('walk','run','death') else [.25,.5]
    for t in fractions:
        scene.frame_set(1+round(t*duration*30));render(name+'-'+str(round(t*100)).zfill(3),'three-quarter')
    if name in ('aim','fire','hit'):
        scene.frame_set(1+round(.25*duration*30));render(name+'-025','side')
rig.animation_data.action=bpy.data.actions['idle'];scene.frame_set(1);scene.frame_start=1;scene.frame_end=73
camera('three-quarter');bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'trooper-rig.blend'))
print('PIPELINE COMPLETE',OUT,flush=True)
