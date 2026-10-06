"""Bounded offline fit against the actual existing shotgun mesh, never a proxy."""
import bpy,json,sys,math
from mathutils import Vector,Quaternion,Euler,Matrix
from mathutils.bvhtree import BVHTree
from pathlib import Path
root=Path(__file__).resolve().parents[2];sys.path.insert(0,str(root/'scripts/trooper'))
import anatomy
from unity_grip_util import set_digits
bpy.ops.wm.open_mainfile(filepath=str(root/'.local/trooper-shipping/trooper-gripped.blend'))
rig=bpy.data.objects['TrooperHumanoid'];s=bpy.context.scene;rig.animation_data.action=bpy.data.actions['aim'];s.frame_set(1);bpy.context.view_layer.update()
arm=rig.data
gun=bpy.data.objects['weapon:joined'];glove=next(o for o in s.objects if o.type=='MESH' and o.get('sourceMesh')=='Object_3')
meta=json.loads((root/'.asset-library/art-loll/sci-fi-soldier-futuristic-combat-trooper/animation-pipeline-v2/source-audit.json').read_text());scale=meta['scaleFactor'];floor=meta['sourceFloorZ']
# Detach fixed gun from hand for fitting, preserving its actual world placement.
gunmw=gun.matrix_world.copy();gun.parent=None;gun.matrix_world=gunmw;bpy.context.view_layer.update()
bvh=BVHTree.FromPolygons([gun.matrix_world@v.co for v in gun.data.vertices],[tuple(p.vertices) for p in gun.data.polygons])
digits=['Thumb','Index','Middle','Ring','Little'];selection={}
for side in ['Left','Right']:
 ids=[];bydigit={d:[] for d in digits}
 for i,v in enumerate(glove.data.vertices):
  q=anatomy.uvw(Vector((v.co.x/scale,v.co.y/scale,v.co.z/scale+floor)));groups=[(glove.vertex_groups[g.group].name,g.weight) for g in v.groups]
  if q.x<.29 or sum(w for n,w in groups if n.startswith(side))<.5:continue
  ids.append(i)
  for d in digits:
   if any(n.startswith(side+d) and w>.35 for n,w in groups):bydigit[d].append(i)
 selection[side]=(ids,bydigit)
rest_world=[glove.matrix_world@v.co for v in glove.data.vertices]
recipe=json.loads((root/'.local/trooper-shipping/grip-recipe.json').read_text())
baseline={name:Matrix(rows) for name,rows in recipe['fitBaselineMatrixBasis'].items()}
# Reconstruct the authored pre-IK aim sample before taking the fixed wrist
# references.  Every candidate below resets these exact matrix bases.
for name,matrix in baseline.items():rig.pose.bones[name].matrix_basis=matrix
bpy.context.view_layer.update()
refs={side:Quaternion(recipe['handRefs'][side]) for side in ['Left','Right']}
targets={'Left':Vector(recipe['leftSupport']),'Right':Vector(recipe['rightGrip'])}
gauges={side:Vector(recipe['handLocalGauge'][side]) for side in ['Left','Right']}
def orient(name,direction):
 pb=rig.pose.bones[name];rest=arm.bones[name].matrix_local.to_quaternion()
 q=(rest@Vector((0,1,0))).rotation_difference(Vector(direction).normalized())@rest
 pb.matrix=Matrix.LocRotScale(pb.head.copy(),q,Vector((1,1,1)));bpy.context.view_layer.update()
def limb(side,target):
 upper=side+'UpperArm';lower=side+'LowerArm';a=rig.pose.bones[upper].head.copy();l1=arm.bones[upper].length;l2=arm.bones[lower].length;vec=target-a
 d=max(abs(l1-l2)+1e-5,min(vec.length,l1+l2-1e-5));u=vec.normalized();pole=Vector((1 if side=='Left' else -1,.3,-1));v=(pole-u*pole.dot(u)).normalized();h=(l1*l1-l2*l2+d*d)/(2*d);mid=a+u*h+v*math.sqrt(max(0,l1*l1-h*h))
 orient(upper,mid-a);orient(lower,a+u*d-mid)
def apply_left_shoulder_policy(wrist,hand_q):
 shoulder=rig.pose.bones['LeftShoulder'];upper=rig.pose.bones['LeftUpperArm'];shoulder_rest=arm.bones['LeftShoulder'].matrix_local.to_quaternion();upper_rest=arm.bones['LeftUpperArm'].matrix_local.to_quaternion()
 ds=shoulder.matrix.to_quaternion()@shoulder_rest.inverted();du=upper.matrix.to_quaternion()@upper_rest.inverted();q=ds.slerp(du,.2)@shoulder_rest
 shoulder.matrix=Matrix.LocRotScale(shoulder.head.copy(),q,Vector((1,1,1)));bpy.context.view_layer.update();limb('Left',wrist)
 hand=rig.pose.bones['LeftHand'];hand.matrix=Matrix.LocRotScale(hand.head.copy(),hand_q,Vector((1,1,1)));bpy.context.view_layer.update()
def pose_twists():
 for side in ['Left','Right']:
  fa=rig.pose.bones[side+'LowerArm'];hand=rig.pose.bones[side+'Hand'];delta=fa.matrix.to_quaternion().inverted()@hand.matrix.to_quaternion()@hand.bone.matrix_local.to_quaternion().inverted()@fa.bone.matrix_local.to_quaternion();twist=Quaternion((delta.w,0,delta.y,0));twist.normalize()
  if twist.w<0:twist.negate()
  for suffix,fraction in [('Twist1',.5),('Twist2',1.)]:rig.pose.bones[side+'LowerArm'+suffix].rotation_quaternion=Quaternion((1,0,0,0)).slerp(twist,fraction)
result={}
for side in ['Left','Right']:
 seed=json.loads((root/'docs/evidence/trooper-animation-pipeline/grip-recipe.json').read_text())['anglesDegrees']
 angles={d:([0,0,0] if side=='Left' else [seed[d][0],seed[d][1]*.5,seed[d][2]*.5]) for d in digits}
 if side=='Right':
  angles['Index']=[8,12,8]
  # Imported shipping audit identifies the unchanged-threshold regression as
  # the Index/Thumb web.  Keep this thumb inside a modest flexion envelope;
  # middle/ring remain the load-bearing trigger-grip contact digits.
  angles['Thumb']=[0,0,0]
 offset=[0,0,0];rotation=[0,0,0];ids,bydigit=selection[side]
 edge_pairs=[tuple(e.vertices) for e in glove.data.edges if e.vertices[0] in set(ids) and e.vertices[1] in set(ids)]
 edge_rest=[(rest_world[a]-rest_world[b]).length for a,b in edge_pairs]
 # This is a local hand-orientation correction in degrees.  It is deliberately
 # bounded: the elbow IK owns wrist placement and this fit may only settle the
 # palm on the actual support/grip surface, never create a new pose convention.
 def evaluate(offset,rotation,angles,details=False):
  # Match unity_grip_bake exactly: reset authored sample, solve the arm to the
  # offset wrist, set hand, distribute forearm twist, then articulate digits.
  for name,matrix in baseline.items():rig.pose.bones[name].matrix_basis=matrix
  bpy.context.view_layer.update()
  # Exact aim-frame bake contract: grasp target minus the rotated hand-local
  # gauge, then the fitted wrist offset; qDesired = EulerXYZ @ hand ref.
  q=Euler(tuple(math.radians(a) for a in rotation),'XYZ').to_quaternion()@refs[side]
  wrist=targets[side]-q@gauges[side]+Vector(offset)
  limb(side,wrist)
  hand=rig.pose.bones[side+'Hand'];hand.matrix=Matrix.LocRotScale(hand.head.copy(),q,Vector((1,1,1)))
  if side=='Left':apply_left_shoulder_policy(wrist,q)
  pose_twists()
  set_digits(rig,side,angles,opposition=0 if side=='Left' else 30)
  bpy.context.view_layer.update();ev=glove.evaluated_get(bpy.context.evaluated_depsgraph_get());dist={}
  for i in ids:
   p=glove.matrix_world@ev.data.vertices[i].co;near,n,idx,d=bvh.find_nearest(p);dist[i]=d if (p-near).dot(n)>=0 else -d
  minby={d:min(dist[i] for i in idx) for d,idx in bydigit.items()}
  # Joined weapon pieces can carry mixed winding, therefore a nearest-face
  # normal is only a diagnostic.  Triangle/BVH overlap is the collision truth.
  # Full hand surface is the collision truth, including palm and thumb web.
  selected=set(ids)
  faces=[tuple(p.vertices) for p in glove.data.polygons if all(v in selected for v in p.vertices)]
  hand_bvh=BVHTree.FromPolygons([glove.matrix_world@v.co for v in ev.data.vertices],faces)
  overlaps=len(hand_bvh.overlap(bvh))
  current=[glove.matrix_world@v.co for v in ev.data.vertices]
  ratios=[(current[a]-current[b]).length/max(length,1e-6) for (a,b),length in zip(edge_pairs,edge_rest) if length>=1e-6]
  max_stretch=max(ratios) if ratios else 0
  absolute={i:abs(v) for i,v in dist.items()}
  finger_clearance={d:min(absolute[i] for i in idx) for d,idx in bydigit.items()}
  required={'Left':{'Index','Middle','Ring'},'Right':{'Thumb','Middle','Ring'}}[side]
  contact=sum((max(0,v-.004)/.004)**2+(max(0,.00025-v)/.00025)**2 for d,v in finger_clearance.items() if d in required)
  # Collision-free surfaces come first; retained fingers then settle within a
  # 0.25-3 mm contact band without relying on invalid signed normals.
  cost=overlaps*1000+contact+max(0,max_stretch-2)**2*1e8
  if details:return {'minimumSignedVertexDistanceDiagnostic':min(dist.values()),'fingerMinimumAbsoluteDistances':finger_clearance,'triangleOverlapPairs':overlaps,'maxFullHandEdgeStretch':max_stretch,'cost':cost}
  return cost
 for step,rotation_step,angle_step in [(.012,18,20),(.006,9,10),(.003,4.5,5),(.0015,2.25,2.5),(.0005,1,1)]:
  for iteration in range(4):
   best=evaluate(offset,rotation,angles);changed=False
   for k in range(3):
    for sign in [-1,1]:
     candidate=list(offset);candidate[k]+=sign*step
     if abs(candidate[k])>.15:continue
     cost=evaluate(candidate,rotation,angles)
     if cost<best:best=cost;offset=candidate;changed=True
   if side=='Left':
    for k in range(3):
     for sign in [-1,1]:
      candidate=list(rotation);candidate[k]=max(-35,min(35,candidate[k]+sign*rotation_step))
      cost=evaluate(offset,candidate,angles)
      if cost<best:best=cost;rotation=candidate;changed=True
   for d in digits:
    if side=='Right' and d=='Index':continue
    for k in range(3):
     for sign in [-1,1]:
      candidate={n:list(a) for n,a in angles.items()}
      # Anatomical curl range: no hyperextension in the contact solve.  Thumb
      # opposition is controlled separately, so its three flexion joints share
      # the same conservative range.
      lower=-10 if k==0 else 0
      upper=80
      if side=='Right' and d=='Thumb': lower=0;upper=(15 if k==0 else 25)
      candidate[d][k]=max(lower,min(upper,candidate[d][k]+sign*angle_step))
      cost=evaluate(offset,rotation,candidate)
      if cost<best:best=cost;angles=candidate;changed=True
   print('FIT',side,step,iteration,best,offset,flush=True)
   if not changed:break
 result[side]={'offset':offset,'rotationDegrees':rotation,'angles':angles,'opposition':0 if side=='Left' else 30,'diagnostic':evaluate(offset,rotation,angles,True)}
 print('RESULT',side,json.dumps(result[side]),flush=True)
(root/'.local/trooper-shipping/contact-fit.json').write_text(json.dumps(result,indent=2))
