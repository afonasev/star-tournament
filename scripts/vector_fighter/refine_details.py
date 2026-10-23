"""Authored assembly detail, not subdivision. A/B are local preview directions."""
import math
from mathutils import Vector
from mathutils.bvhtree import BVHTree

def build(g,style):
 parts=g.parts; metal=g.metal; trim=g.trim; rubber=g.rubber; paint=g.paint; cloth=g.cloth
 def surface(name):
  o=next(o for o in parts if o.name==name)
  return BVHTree.FromPolygons([v.co for v in o.data.vertices],[p.vertices[:] for p in o.data.polygons])
 chest=surface('VECTOR breastplate')
 def on(tree,x,z,offset=.003):
  hit=tree.ray_cast(Vector((x,-2,z)),Vector((0,1,0)))[0]
  if hit is None:raise ValueError(('detail off authored surface',x,z))
  return Vector((x,hit.y-offset,z))
 def hatch(name,tree,outline,bone,kind):
  points=[on(tree,x,z,.002) for x,z in outline]
  g.panel(name+' recessed bed',points,rubber,bone,.001,.0003)
  center=sum(points,Vector())/len(points)
  inner=[center+(p-center)*.85+Vector((0,-.002,0)) for p in points]
  g.panel(name+' inset insert',inner,kind,bone,.001,.0004)
  g.tube(name+' perimeter seal',points+[points[0]],.0012,trim,bone,6)
  return center
 # Two shaped intake/service cassettes actually follow the compound chest surface.
 for sign in [-1,1]:
  if style=='tailored':
   outline=[(sign*.066,1.364),(sign*.132,1.367),(sign*.144,1.346),(sign*.073,1.337)]
  else:
   outline=[(sign*.045,1.377),(sign*.136,1.374),(sign*.151,1.343),(sign*.122,1.318),(sign*.045,1.332)]
  c=hatch('VECTOR F5 chest '+str(sign),chest,outline,'Chest',trim)
  for i in range(5 if style=='technical' else 4):
   x=sign*(.077+i*.012);z=1.350
   a=on(chest,x,z-.004,.007);b=on(chest,x,z+.006,.007)
   g.tube('VECTOR F5 recessed intake louver',[a,b],.0016,metal,'Chest',6)
  for x,z in [(sign*.073,1.361),(sign*.128,1.345)]:
   p=on(chest,x,z,.008);g.ellipsoid('VECTOR F5 captive quarter turn',p,(.002,.001,.002),metal,'Chest')
   g.tube('VECTOR F5 fastener drive',[p+Vector((-.001,-.001,0)),p+Vector((.001,-.001,0))],.00035,rubber,'Chest',6)
 # Lamination line connects chest perimeter to a genuinely split lower insert.
 for sign in [-1,1]:
  pts=[on(chest,sign*x,z,.002) for x,z in [(.027,1.395),(.030,1.376),(.026,1.343),(.022,1.302),(.012,1.270)]]
  g.tube('VECTOR F5 sternum assembly seam',pts,.0009,rubber,'Chest',6)
 # Full textile articulation at elbows/knees: short independent accordion ribs.
 # Ribs stay on their adjacent limb; no rigid part bridges a bending joint.
 for side in ['Left','Right']:
  for region in ['LowerArm','LowerLeg']:
   name=side+region;b=g.rig.data.bones[name];a=b.head_local;end=g.rig.data.bones[side+'Hand'].head_local if region=='LowerArm' else b.tail_local
   axis=(end-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lat=axis.cross(front).normalized();w,d=g.LIMBS[region]
   for n in range(6):
    center=a.lerp(end,.025+n*.018)
    pts=[center+front*(math.cos(t)*d*.49)+lat*(math.sin(t)*w*.49) for t in [1.15+j*(math.tau-2.3)/20 for j in range(21)]]
    g.tube('VECTOR F5 '+name+' accordion gusset',pts,.0017,cloth,name,6)
   # Side gusset stitch pair follows the actual cuff width, adding no cuff bulk.
   for sg in [-1,1]:
    for n in range(7):
     t=.035+n*.015;center=a.lerp(end,t)+lat*(sg*w*.48)+front*(d*.12)
     g.tube('VECTOR F5 '+name+' stitch', [center-axis*.0013,center+axis*.0013],.0005,trim,name,5)
  # Nested knee mechanical axis and protective retaining straps.
  knee=g.rig.data.bones[side+'LowerLeg'].head_local
  for sign in [-1,1]:
   for radius,x,mat in [(.019,.073,trim),(.012,.077,metal),(.006,.079,rubber)]:
    pts=[knee+Vector((sign*x,math.cos(i*math.tau/24)*radius,math.sin(i*math.tau/24)*radius)) for i in range(25)]
    g.tube('VECTOR F5 '+side+' knee concentric retaining ring',pts,.0015,mat,side+'LowerLeg',6)
  # Shin identity plate gets recessed diagonal assembly lines and small service lid.
  o=next(o for o in parts if o.name=='VECTOR '+side+'LowerLeg ceramic');tree=surface(o.name)
  b=g.rig.data.bones[side+'LowerLeg'];mid=(b.head_local+b.tail_local)*.5
  for off in [-.021,.021]:
   pts=[on(tree,mid.x+off,z,.002) for z in [mid.z-.07,mid.z-.025,mid.z+.02,mid.z+.07]]
   g.tube('VECTOR F5 '+side+' shin seam',pts,.001,trim,side+'LowerLeg',6)
  if style=='technical':
   outline=[(mid.x-.017,mid.z+.044),(mid.x+.017,mid.z+.044),(mid.x+.017,mid.z-.025),(mid.x-.017,mid.z-.025)]
   hatch('VECTOR F5 '+side+' shin service lid',tree,outline,side+'LowerLeg',metal)
 # Helmet lower seal: breathing slots cut visually into inset bed, thin jaw rails.
 for sign in [-1,1]:
  g.panel('VECTOR F5 cheek ventilation bed',[(sign*.076,-.090,1.594),(sign*.093,-.061,1.615),(sign*.095,-.052,1.603),(sign*.079,-.077,1.580)],trim,'Head',.001,.0003)
  for n in range(4):
   p=Vector((sign*(.079+n*.004),-.084+n*.007,1.592+n*.003))
   g.tube('VECTOR F5 cheek grille',[p,p+Vector((sign*.004,.003,-.005))],.0008,metal,'Head',6)
  g.tube('VECTOR F5 jaw panel seam',[(sign*.041,-.123,1.551),(sign*.065,-.109,1.566),(sign*.085,-.076,1.581)],.0009,trim,'Head',6)
 # Technical direction adds a low-profile connector panel to the back, no hoses.
 if style=='technical':
  for sign in [-1,1]:
   g.box('VECTOR F5 dorsal service housing',(sign*.086,.176,1.36),(.065,.016,.105),trim,'Chest',.006)
   for n in range(5):g.box('VECTOR F5 dorsal exchanger fin',(sign*.086,.187,1.328+n*.014),(.049,.004,.004),metal,'Chest',.0005)
 else:
  for sign in [-1,1]:
   for n in range(5):
    z=1.31+n*.017
    g.tube('VECTOR F5 dorsal articulated seam',[(sign*.040,.178,z),(sign*.088,.179,z+.008),(sign*.118,.170,z+.018)],.0012,trim,'Chest',6)
