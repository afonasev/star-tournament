"""Original VECTOR suit over the compatible, attributed Trooper rig and gloves.
Offline authored mesh dimensions are in metres, +Z up/-Y forward in Blender.
Reproducible source inputs are pinned Git blobs; never rebuild from the output.
Run: blender -b -noaudio --python scripts/vector_fighter/build.py
"""
import bpy, bmesh, math, json, hashlib, subprocess, sys
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
WORK=ROOT/'.local/vector-fighter';WORK.mkdir(parents=True,exist_ok=True)
BASE='00fbd33209eb755dd5f86499aa8ea12867a2efd0'


def material(name,color,metal,rough):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=rough
 return m


def finish(name,verts,faces,mat,bone,bevel=0,weights=None):
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
 o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);mesh.materials.append(mat)
 for f in mesh.polygons:f.use_smooth=True
 uv=mesh.uv_layers.new(name='UVMap')
 for p in mesh.polygons:
  n=p.normal;axes=(0,2) if abs(n.y)>.5 else (1,2) if abs(n.x)>.5 else (0,1)
  for li in p.loop_indices:
   c=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(c[axes[0]]*8,c[axes[1]]*8)
 if weights:
  groups={b:o.vertex_groups.new(name=b) for b in sorted({b for ws in weights for b in ws})}
  for i,ws in enumerate(weights):
   for b,w in ws.items():groups[b].add([i],w,'REPLACE')
 else:o.vertex_groups.new(name=bone).add(list(range(len(verts))),1,'REPLACE')
 if bevel:
  bpy.context.view_layer.objects.active=o;o.select_set(True)
  mod=o.modifiers.new('Soft manufactured edges','BEVEL');mod.width=bevel;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
  mod=o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name);o.select_set(False)
 mod=o.modifiers.new('Skin','ARMATURE');mod.object=rig;o.parent=rig
 parts.append(o);return o


def box(name,pos,size,mat,bone,bevel=.004):
 x,y,z=pos;w,d,h=[v/2 for v in size]
 return finish(name,[(x+sx*w,y+sy*d,z+sz*h) for sz in [-1,1] for sy in [-1,1] for sx in [-1,1]],[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)],mat,bone,bevel)


def tube(name,points,radius,mat,bone,radial=8):
 verts=[];faces=[];ps=[Vector(p) for p in points]
 for j,p in enumerate(ps):
  tangent=(ps[min(j+1,len(ps)-1)]-ps[max(0,j-1)]).normalized();q=Vector((0,0,1)).rotation_difference(tangent)
  for i in range(radial):verts.append(p+q@Vector((radius*math.cos(i*math.tau/radial),radius*math.sin(i*math.tau/radial),0)))
 for j in range(len(ps)-1):
  for i in range(radial):a=j*radial+i;b=j*radial+(i+1)%radial;faces.append((a,b,b+radial,a+radial))
 return finish(name,verts,faces,mat,bone)


def ellipsoid(name,c,r,mat,bone):
 verts=[];faces=[];N=32;M=16
 for j in range(M+1):
  v=math.pi*(j+.01)/(M+.02)
  for i in range(N):
   u=math.tau*i/N;verts.append((c[0]+r[0]*math.sin(v)*math.cos(u),c[1]+r[1]*math.sin(v)*math.sin(u),c[2]+r[2]*math.cos(v)))
 for j in range(M):
  for i in range(N):a=j*N+i;b=j*N+(i+1)%N;faces.append((a,b,b+N,a+N))
 return finish(name,verts,faces,mat,bone)


def panel(name,outline,mat,bone,thickness=.012,bevel=.004):
 # A contoured front/back plate: outline specifies exact 3D surface points.
 sign=-1 if sum(v[1] for v in outline)<.04*len(outline) else 1
 n=len(outline);center=sum((Vector(v) for v in outline),Vector())/n
 if name=='VECTOR breastplate':center.y=min(v[1] for v in outline)-.018
 # Explicit surface topology avoids arbitrary triangulation of non-planar ngons.
 inset=[Vector(v).lerp(center,.12)+Vector((0,sign*thickness*.28,0)) for v in outline]
 verts=list(outline)+inset+[(x,y-sign*thickness,z) for x,y,z in outline]+[center+Vector((0,sign*thickness*.45,0))]
 faces=[]
 for i in range(n):
  j=(i+1)%n
  faces.extend([(i,j,j+n,i+n),(i+n,j+n,3*n),(i,i+2*n,j+2*n,j)])
 faces.append(tuple(reversed(range(2*n,3*n))))
 o=finish(name,verts,faces,mat,bone,min(bevel,.002))
 return o


def loft(name,rings,mat,bone,weights=None,N=40):
 # rings: centre x/y/z and x/y radii, axis vertical.
 verts=[];faces=[];ws=[]
 for j,(x,y,z,rx,ry) in enumerate(rings):
  for i in range(N):
   a=math.tau*i/N;verts.append((x+rx*math.sin(a),y-ry*math.cos(a),z));ws.append(weights[j] if weights else {bone:1})
 for j in range(len(rings)-1):
  for i in range(N):a=j*N+i;b=j*N+(i+1)%N;faces.append((a,b,b+N,a+N))
 faces.extend([tuple(reversed(range(N))),tuple(range((len(rings)-1)*N,len(rings)*N))])
 return finish(name,verts,faces,mat,bone,weights=ws)


def limb(name,a,b,width,depth,mat,bone):
 a,b=Vector(a),Vector(b);axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();side=axis.cross(front).normalized()
 verts=[];faces=[];N=32
 stations=[(-.035,.80),(0,.97),(.10,1.01),(.30,1),(.55,.94),(.78,.86),(.94,.78),(1.035,.75)]
 for t,r in stations:
  for i in range(N):
   angle=math.tau*i/N;verts.append(a.lerp(b,t)+side*(math.sin(angle)*width*r/2)+front*(math.cos(angle)*depth*r/2))
 for j in range(len(stations)-1):
  for i in range(N):q=j*N+i;v=j*N+(i+1)%N;faces.append((q,v,v+N,q+N))
 faces.extend([tuple(reversed(range(N))),tuple(range((len(stations)-1)*N,len(stations)*N))])
 return finish(name,verts,faces,mat,bone)


def shell(name,a,b,width,depth,bone,start=.1,end=.9,back=False):
 a,b=Vector(a),Vector(b);axis=(b-a).normalized();front=Vector((0,1 if back else -1,0));front=(front-axis*front.dot(axis)).normalized();side=axis.cross(front).normalized()
 verts=[];faces=[];N=20
 stations=[(start,.38),(start+.04,.76),(start+.14,1),(end-.19,.91),(end-.06,.64),(end,.27)]
 for t,span in stations:
  for i in range(N):
   angle=(i/(N-1)-.5)*2.65*span
   radius=1.0 - .20*max(0,(t-start)/(end-start))
   dome=.94+.10*math.sin(math.pi*(t-start)/(end-start))
   verts.append(a.lerp(b,t)+side*(math.sin(angle)*width*radius/2)+front*(math.cos(angle)*depth*dome/2))
 for j in range(len(stations)-1):
  for i in range(N-1):q=j*N+i;faces.append((q,q+1,q+1+N,q+N))
 o=finish(name,verts,faces,paint,bone)
 bpy.context.view_layer.objects.active=o
 # Apply solidify before armature; rigid shell plates do not rubber-stretch.
 o.modifiers.remove(o.modifiers['Skin']);mod=o.modifiers.new('Ceramic thickness','SOLIDIFY');mod.thickness=.009;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=o.modifiers.new('Shell edge radius','BEVEL');mod.width=.003;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=o.modifiers.new('Skin','ARMATURE');mod.object=rig
 # Fine edge bead stays subordinate to the ceramic surface.
 for edge in [0,N-1]:tube(name+' edge', [verts[j*N+edge] for j in range(len(stations))],.0008,trim,bone)
 # Recessed latch and seams, spaced to leave most of the paint uninterrupted.
 for t in [start+.16,end-.13]:
  pos=a.lerp(b,t)+front*(depth/2+.002)
  ellipsoid(name+' flush fastener',pos,(.003,.003,.003),metal,bone)
 # Angled recessed seam divides the large shell into manufactured panels.
 seam=[]
 for t,angle in [(start+.08,-.50),(start+.22,-.27),(end-.18,-.25),(end-.07,.05)]:
  radius=1-.20*(t-start)/(end-start)
  dome=.94+.10*math.sin(math.pi*(t-start)/(end-start))
  seam.append(a.lerp(b,t)+side*(math.sin(angle)*width*radius/2)+front*(math.cos(angle)*depth*dome/2+.001))
 tube(name+' panel division',seam,.0015,rubber,bone)
 return o


def suit():
 loft('VECTOR fitted torso',[(0,.035,z,rx,ry) for z,rx,ry in [(.82,.09,.065),(.88,.155,.10),(1.0,.16,.10),(1.08,.132,.09),(1.17,.14,.10),(1.28,.181,.116),(1.38,.205,.115),(1.43,.172,.092),(1.465,.074,.06)]],cloth,'Spine',
 weights=[{'Hips':1},{'Hips':1},{'Hips':.6,'Spine':.4},{'Spine':1},{'Spine':.7,'Chest':.3},{'Chest':1},{'Chest':.75,'UpperChest':.25},{'UpperChest':1},{'UpperChest':1}])
 loft('VECTOR neck seal',[(0,.025,z,r,r*.85) for z,r in [(1.43,.067),(1.47,.061),(1.52,.056),(1.57,.058)]],rubber,'Neck')
 for z in [1.464,1.477,1.490,1.503,1.516,1.529]:
  points=[(.059*math.sin(i*math.tau/48),.025-.051*math.cos(i*math.tau/48),z) for i in range(49)]
  tube('VECTOR neck fabric rib',points,.0012,cloth,'Neck')
 # The clean broad breastplate and lower floating abdominal shell are central to VECTOR.
 panel('VECTOR breastplate',[(-.17,-.066,1.395),(-.115,-.091,1.434),(-.058,-.104,1.425),(.058,-.104,1.425),(.115,-.091,1.434),(.17,-.066,1.395),(.174,-.08,1.315),(.126,-.105,1.255),(.065,-.113,1.24),(-.065,-.113,1.24),(-.126,-.105,1.255),(-.174,-.08,1.315)],paint,'Chest',.016,.007)
 panel('VECTOR abdominal shell',[(-.103,-.084,1.224),(.103,-.084,1.224),(.105,-.075,1.17),(.067,-.071,1.105),(-.067,-.071,1.105),(-.105,-.075,1.17)],paint,'Spine',.011,.005)
 for s in [-1,1]:
  panel('VECTOR flank ceramic',[(s*.127,-.070,1.26),(s*.171,-.052,1.29),(s*.155,-.041,1.205),(s*.118,-.060,1.17)],paint,'Chest',.01,.003)
  panel('VECTOR collar inlay',[(s*.06,-.040,1.465),(s*.13,-.038,1.454),(s*.171,-.037,1.407),(s*.155,-.049,1.401),(s*.105,-.061,1.439),(s*.062,-.060,1.445)],metal,'UpperChest',.008,.002)
  for z in [1.09,1.12,1.15,1.18]:tube('VECTOR side flex rib',[(s*.116,-.018,z),(s*.13,.025,z+.01),(s*.117,.092,z+.012)],.004,trim,'Spine')
  box('VECTOR hip clasp',(s*.135,-.012,1.035),(.067,.099,.046),metal,'Hips',.013)
 panel('VECTOR back left',[(-.045,.132,1.425),(-.151,.114,1.405),(-.164,.133,1.337),(-.132,.148,1.268),(-.038,.149,1.245),(-.022,.149,1.31)],paint,'Chest',.013,.005)
 panel('VECTOR back right',[(.045,.132,1.425),(.151,.114,1.405),(.164,.133,1.337),(.132,.148,1.268),(.038,.149,1.245),(.022,.149,1.31)],paint,'Chest',.013,.005)
 box('VECTOR spinal bridge',(0,.156,1.33),(.026,.014,.19),metal,'Chest',.006)
 for z in [1.27,1.3,1.33,1.36,1.39]:box('VECTOR spine recess',(0,.165,z),(.018,.004,.009),rubber,'Chest',.002)
 panel('VECTOR lumbar',[(-.105,.12,1.19),(-.06,.132,1.22),(.06,.132,1.22),(.105,.12,1.19),(.065,.116,1.10),(-.065,.116,1.10)],paint,'Spine')
 panel('VECTOR pelvis shield',[(-.076,-.07,1.03),(.076,-.07,1.03),(.054,-.076,.945),(0,-.068,.897),(-.054,-.076,.945)],paint,'Hips',.009,.004)
 for side in ['Left','Right']:
  sign=1 if side=='Left' else -1
  for region,width,depth in [('UpperLeg',.172,.185),('LowerLeg',.125,.143),('UpperArm',.123,.137),('LowerArm',.102,.112)]:
   name=side+region;bone=rig.data.bones[name];a=bone.head_local;b=rig.data.bones[side+'Hand'].head_local if region=='LowerArm' else bone.tail_local
   limb('VECTOR '+name+' fabric',a,b,width,depth,cloth,name)
   shell('VECTOR '+name+' ceramic',a,b,width+.022,depth+.024,name,.10,.88)
   if region in ['LowerArm','LowerLeg']:shell('VECTOR '+name+' rear',a,b,width+.008,depth+.012,name,.18,.75,True)
   if region in ['UpperLeg','UpperArm']:
    joint=side+('LowerLeg' if region=='UpperLeg' else 'LowerArm');p=b
    ellipsoid('VECTOR '+joint+' hinge',p,(width*.43,depth*.42,width*.40),rubber,joint)
   if region=='UpperArm':
    # Shoulder cup sits close to the athlete instead of oversized military pauldrons.
    shoulder_cap(side,a,b)
   if region=='LowerLeg':
    p=a+Vector((0,-.081,-.012));panel('VECTOR '+side+' knee',[(p.x-.048,p.y,p.z+.039),(p.x+.048,p.y,p.z+.039),(p.x+.056,p.y-.01,p.z-.017),(p.x+.029,p.y-.013,p.z-.072),(p.x-.029,p.y-.013,p.z-.072),(p.x-.056,p.y-.01,p.z-.017)],metal,name,.012,.006)
    for dz in [.012,-.015]:box('VECTOR knee inset',(p.x,p.y-.019,p.z+dz),(.05,.007,.009),trim,name,.002)
  foot=rig.data.bones[side+'Foot'];x=foot.head_local.x
  # Shaped soles and instep, with an articulated toe cap.
  box('VECTOR '+side+' outsole',(x,-.064,.035),(.138,.283,.054),rubber,side+'Foot',.023)
  loft('VECTOR '+side+' boot',[(x,y,z,rx,ry) for y,z,rx,ry in [(-.067,.046,.063,.131),(-.060,.07,.064,.132),(-.043,.105,.060,.11),(.015,.17,.051,.064)]],cloth,side+'Foot')
  panel('VECTOR '+side+' instep',[(x-.048,-.032,.16),(x+.048,-.032,.16),(x+.062,-.116,.09),(x+.053,-.171,.076),(x-.053,-.171,.076),(x-.062,-.116,.09)],paint,side+'Foot',.009,.005)
  box('VECTOR '+side+' heel',(x,.062,.093),(.102,.035,.067),paint,side+'Foot',.014)


def shoulder_cap(side,a,b):
 name=side+'UpperArm';sign=1 if side=='Left' else -1
 # Longitudinal dome over deltoid. The boundary curves down over the shoulder;
 # it never creates the upward cut edge of the rejected early strip.
 axis=(Vector(b)-Vector(a)).normalized();front=Vector((0,-1,0));sideaxis=axis.cross(front).normalized()
 ellipsoid('VECTOR '+side+' shoulder cushion',Vector(a)+axis*.018,(.083,.080,.080),cloth,name)
 verts=[];faces=[];N=28;M=12
 for j in range(M+1):
  t=j/M;z=-.035+.145*t;r=.032+.069*math.sin(math.pi*(.10+.77*t))
  for i in range(N):
   ang=(i/(N-1)-.5)*3.85
   verts.append(Vector(a)+axis*z+sideaxis*(math.sin(ang)*r)+front*(math.cos(ang)*r*.98))
 for j in range(M):
  for i in range(N-1):q=j*N+i;faces.append((q,q+1,q+1+N,q+N))
 o=finish('VECTOR '+side+' shoulder',verts,faces,paint,name)
 bpy.context.view_layer.objects.active=o;o.modifiers.remove(o.modifiers['Skin'])
 mod=o.modifiers.new('Cup thickness','SOLIDIFY');mod.thickness=.008;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=o.modifiers.new('Cup radius','BEVEL');mod.width=.004;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=o.modifiers.new('Skin','ARMATURE');mod.object=rig
 # Neutral lower guard and flush shoulder latches.
 for t in [.22,.75]:
  p=Vector(a)+axis*(.145*t)+front*.095
  ellipsoid('VECTOR '+side+' shoulder latch',p,(.008,.004,.009),metal,name)


def tailoring_details():
 for side in ['Left','Right']:
  sign=1 if side=='Left' else -1
  hip=rig.data.bones[side+'UpperLeg'].head_local
  ellipsoid('VECTOR '+side+' hip continuity',hip+Vector((0,0,.02)),(.094,.09,.115),cloth,side+'UpperLeg')
  # Floating lateral hip plate bridges belt to thigh without spanning the joint.
  panel('VECTOR '+side+' upper thigh blade',[(sign*.184,-.010,.982),(sign*.214,-.007,.943),(sign*.226,-.025,.866),(sign*.211,-.053,.773),(sign*.177,-.061,.748),(sign*.157,-.066,.853),(sign*.164,-.046,.934)],paint,side+'UpperLeg',.009,.002)
  for z in [.947,.895]:
   ellipsoid('VECTOR '+side+' hip blade rivet',(sign*.195,-.030,z),(.003,.003,.003),metal,side+'UpperLeg')
  # Nested articulated knee frame and boot straps.
  knee=rig.data.bones[side+'LowerLeg'].head_local
  for sx in [-1,1]:
   ellipsoid('VECTOR '+side+' knee pivot',knee+Vector((sx*.059,-.003,0)),(.013,.027,.027),metal,side+'LowerLeg')
   ellipsoid('VECTOR '+side+' pivot inset',knee+Vector((sx*.071,-.003,0)),(.003,.015,.015),trim,side+'LowerLeg')
  ankle=rig.data.bones[side+'Foot'].head_local
  box('VECTOR '+side+' ankle band',(ankle.x,-.003,.164),(.11,.129,.025),trim,side+'Foot',.010)
  box('VECTOR '+side+' ankle clasp',(ankle.x,-.071,.164),(.040,.011,.022),metal,side+'Foot',.004)
  for sx in [-1,1]:
   panel('VECTOR '+side+' boot sidewall',[(ankle.x+sx*.065,-.176,.047),(ankle.x+sx*.071,-.109,.048),(ankle.x+sx*.064,.026,.050),(ankle.x+sx*.054,.048,.088),(ankle.x+sx*.059,-.041,.097),(ankle.x+sx*.064,-.115,.078)],metal,side+'Foot',.004,.001)
  for region in ['UpperArm','LowerArm','UpperLeg','LowerLeg']:
   bone=rig.data.bones[side+region];a=bone.head_local;b=rig.data.bones[side+'Hand'].head_local if region=='LowerArm' else bone.tail_local
   width={'UpperArm':.123,'LowerArm':.102,'UpperLeg':.172,'LowerLeg':.125}[region]
   depth={'UpperArm':.137,'LowerArm':.112,'UpperLeg':.185,'LowerLeg':.143}[region]
   axis=(b-a).normalized();front=Vector((0,-1,0));sideaxis=axis.cross(front).normalized()
   # Dense textile seams follow the garment length and stop before the joint.
   for sg in [-1,1]:
    points=[a.lerp(b,t)+front*(depth*.33)+sideaxis*(sg*width*.39*(1-.16*t)) for t in [.08,.25,.50,.75,.94]]
    tube('VECTOR '+side+region+' sewn piping',points,.0011,trim,side+region)
   for t in [.02,.055,.92,.955]:
    center=a.lerp(b,t);points=[]
    for i in range(17):
     theta=-1.5+i*3/16
     points.append(center+front*(math.cos(theta)*depth*.405)+sideaxis*(math.sin(theta)*width*.41))
    tube('VECTOR '+side+region+' flex seam',points,.0016,rubber,side+region)
 # Chest segmentation echoes the distinctive downward V of the selected concept.
 for s in [-1,1]:
  tube('VECTOR breastplate recessed V',[(s*.151,-.093,1.358),(s*.102,-.115,1.329),(s*.071,-.117,1.274),(s*.027,-.118,1.268)],.0027,rubber,'Chest')
  for x,z in [(s*.142,1.39),(s*.115,1.277)]:ellipsoid('VECTOR breastplate screw',(x,-.105,z),(.003,.002,.003),metal,'Chest')
  panel('VECTOR side locking bracket',[(s*.138,-.084,1.25),(s*.167,-.059,1.264),(s*.181,-.058,1.228),(s*.143,-.077,1.205),(s*.125,-.086,1.22)],metal,'Chest',.007,.002)
 # Abdominal quilted inset panels and tailored seam rows.
 for z,w in [(1.062,.073),(1.093,.064),(1.123,.061)]:
  box('VECTOR abdominal fabric pad',(0,-.067,z),(w*2,.021,.025),cloth,'Spine',.009)
  tube('VECTOR abdominal stitch',[(-w,-.079,z),(0,-.081,z-.002),(w,-.079,z)],.0008,trim,'Spine')
 # Shorten exposed neck and broaden crown to the reference silhouette.
 for o in parts:
  if all(g.name=='Head' for g in o.vertex_groups):
   for v in o.data.vertices:v.co.x*=1.12;v.co.z-=.024

def helmet():
 # Continuous shell under separated armor plates. The visor and its gasket share
 # one boundary; independently fitted surfaces caused intersections in v1.
 rings=[(.020,1.537,.065,.067),(.009,1.568,.089,.095),(.008,1.63,.102,.109),(.014,1.7,.105,.110),(.016,1.727,.099,.104),(.018,1.748,.082,.089),(.019,1.762,.058,.064),(.020,1.770,.028,.034),(.020,1.773,.001,.001)]
 loft('VECTOR helmet inner seal',[(0,y,z,rx,ry) for y,z,rx,ry in rings],paint,'Head',N=64)
 rows=[(1.570,.66,.095,.122),(1.586,1.04,.104,.128),(1.610,1.72,.109,.134),(1.645,2.34,.112,.134),(1.686,2.40,.112,.130),(1.714,2.23,.106,.122)]
 verts=[];faces=[];N=48
 for z,span,rx,ry in rows:
  for i in range(N):
   angle=(i/(N-1)-.5)*span
   verts.append((math.sin(angle)*rx,.006-math.cos(angle)*ry,z))
 for j in range(len(rows)-1):
  for i in range(N-1):q=j*N+i;faces.append((q,q+1,q+1+N,q+N))
 finish('VECTOR opaque visor',verts,faces,glass,'Head')
 boundary=verts[:N]+[verts[j*N+N-1] for j in range(1,len(rows))]+list(reversed(verts[-N:-1]))+[verts[j*N] for j in reversed(range(1,len(rows)-1))]+[verts[0]]
 tube('VECTOR visor perimeter seal',boundary,.0028,rubber,'Head',12)
 # Armor cheekbones follow the taper of the lens, with dark recessed vents.
 for sign in [-1,1]:
  panel('VECTOR cheek sculpt',[(sign*.044,-.112,1.581),(sign*.077,-.099,1.614),(sign*.100,-.050,1.649),(sign*.105,-.022,1.627),(sign*.084,-.035,1.577),(sign*.034,-.099,1.544)],paint,'Head',.009,.002)
  panel('VECTOR cheek air intake',[(sign*.073,-.088,1.608),(sign*.094,-.047,1.633),(sign*.094,-.037,1.615),(sign*.076,-.075,1.588)],rubber,'Head',.002,.0005)
  panel('VECTOR temple bridge',[(sign*.076,-.082,1.713),(sign*.095,-.052,1.729),(sign*.107,-.003,1.714),(sign*.112,.008,1.691),(sign*.102,-.024,1.684),(sign*.096,-.047,1.704)],metal,'Head',.006,.001)
  for radius,xx,mat,n in [(.034,.104,metal,8),(.028,.109,rubber,32),(.021,.111,trim,8)]:
   vs=[]
   for x in [sign*xx,sign*(xx+.003)]:
    for i in range(n):angle=i*math.tau/n;vs.append((x,.020+radius*math.cos(angle),1.673+radius*math.sin(angle)))
   fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
   finish('VECTOR helmet ear frame',vs,fs,mat,'Head',.001)
 # Raised central crown inset with a narrow dark gasket. Explicit surface heights
 # are slightly outside the fitted shell, including its curved front slope.
 outline=[(-.035,-.084,1.743),(-.040,-.045,1.766),(-.041,.022,1.774),(-.040,.078,1.753),(-.028,.106,1.729),(.028,.106,1.729),(.040,.078,1.753),(.041,.022,1.774),(.040,-.045,1.766),(.035,-.084,1.743)]
 def crown_height(x,y):
  for j in range(3,len(rings)-1):
   a,b=rings[j:j+2]
   if x*x/(a[2]*a[2])+(y-a[0])**2/(a[3]*a[3])<=1 and x*x/(b[2]*b[2])+(y-b[0])**2/(b[3]*b[3])>=1:
    low,high=0.,1.
    for _ in range(20):
     t=(low+high)/2;cy=a[0]+(b[0]-a[0])*t;rx=a[2]+(b[2]-a[2])*t;ry=a[3]+(b[3]-a[3])*t
     if x*x/(rx*rx)+(y-cy)**2/(ry*ry)>1:high=t
     else:low=t
    return a[1]+(b[1]-a[1])*(low+high)/2
  return 1.7
 contour=[]
 for k in range(len(outline)):
  a,b=Vector(outline[k]),Vector(outline[(k+1)%len(outline)])
  for j in range(8):
   p=a.lerp(b,j/8);contour.append((p.x,p.y,crown_height(p.x,p.y)+.0003))
 tube('VECTOR crown gasket',contour+[contour[0]],.001,rubber,'Head')
 # Chin uses a sculpted five-point shield rather than a floating flat lip.
 panel('VECTOR chin shield',[(-.033,-.111,1.575),(0,-.125,1.568),(.033,-.111,1.575),(.029,-.109,1.544),(0,-.115,1.532),(-.029,-.109,1.544)],paint,'Head',.009,.002)
 box('VECTOR rear helmet panel',(0,.109,1.652),(.075,.008,.053),paint,'Head',.006)
 for x in [-.022,0,.022]:box('VECTOR rear helmet vent',(x,.115,1.651),(.007,.003,.027),trim,'Head',.001)


def export(path,objects):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='ACTIONS',export_force_sampling=True,export_extras=True,export_yup=True,export_skins=True,export_all_influences=False)


def build():
 global rig,paint,metal,rubber,cloth,trim,glass,parts
 bpy.ops.wm.read_factory_settings(use_empty=True)
 bpy.context.preferences.filepaths.save_version=0
 # Use body rig/actions/equipment unchanged; import the matching detailed hand geometry separately.
 for kind in ['body','arms']:
  source=WORK/('baseline-'+kind+'.glb');source.write_bytes(subprocess.check_output(['git','show',BASE+':unity/Assets/StarTournament/Art/trooper-'+kind+'-animated.glb'],cwd=ROOT))
 bpy.ops.import_scene.gltf(filepath=str(WORK/'baseline-body.glb'))
 rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 for t in rig.animation_data.nla_tracks:t.mute=True
 rig.animation_data.action=None
 for p in rig.pose.bones:p.matrix_basis.identity()
 # Keep equipment and hierarchy, replace all original costume geometry.
 for o in list(bpy.context.scene.objects):
  if o.type=='MESH' and o.name!='weapon:joined':bpy.data.objects.remove(o,do_unlink=True)
 oldobjects=set(bpy.context.scene.objects)
 bpy.ops.import_scene.gltf(filepath=str(WORK/'baseline-arms.glb'))
 additions=set(bpy.context.scene.objects)-oldobjects
 handparts=[]
 for o in list(additions):
  if o.type!='MESH' or not o.find_armature():continue
  if o.name.startswith('weapon:'):continue
  world=o.matrix_world.copy();names={g.index:g.name for g in o.vertex_groups}
  keep={v.index for v in o.data.vertices if sum(g.weight for g in v.groups if any(names[g.group].startswith(s+d) for s in ['Left','Right'] for d in ['Hand','Thumb','Index','Middle','Ring','Little']))>.10}
  bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index not in keep],context='VERTS');bm.to_mesh(o.data);bm.free()
  if not o.data.polygons:continue
  o.parent=rig;o.matrix_world=world
  for mod in o.modifiers:
   if mod.type=='ARMATURE':mod.object=rig
  handparts.append(o);o.name='VECTOR articulated gloves'
 for o in list(additions):
  if o not in handparts:bpy.data.objects.remove(o,do_unlink=True)
 # Remove .001 duplicate actions imported with the arms. Canonical body actions stay intact.
 for a in list(bpy.data.actions):
  if a.name.rsplit('.',1)[-1].isdigit():bpy.data.actions.remove(a)
 bpy.context.view_layer.update()
 paint=material('vector-fighter-identity',(.55,.55,.55),.18,.39)
 metal=material('vector-fighter-titanium',(.16,.19,.21),.72,.38)
 rubber=material('vector-fighter-rubber',(.014,.019,.022),.08,.66)
 cloth=material('vector-fighter-fabric',(.009,.012,.015),.02,.81)
 trim=material('vector-fighter-trim',(.045,.052,.059),.42,.46)
 glass=material('vector-fighter-visor',(.006,.011,.015),.68,.16)
 # Portable packed tangent-space weave; no procedural shader dependency in Unity.
 img=bpy.data.images.new('vector-fighter-weave-normal',width=128,height=128);img.colorspace_settings.name='Non-Color';pixels=[]
 for y in range(128):
  for x in range(128):
   v=Vector((.20*math.sin(x*math.tau/4),.20*math.sin(y*math.tau/4),1)).normalized();pixels.extend((v.x*.5+.5,v.y*.5+.5,v.z*.5+.5,1))
 img.pixels=pixels;img.pack();tex=cloth.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img
 normal=cloth.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.35;cloth.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color']);cloth.node_tree.links.new(normal.outputs['Normal'],cloth.node_tree.nodes.get('Principled BSDF').inputs['Normal'])
 parts=[];suit();helmet();tailoring_details()
 # Every weighted channel must resolve to a real bone; duplicate group names
 # silently export unbound geometry and cannot be accepted as a working skin.
 for o in parts+handparts:
  names={g.index:g.name for g in o.vertex_groups}
  for v in o.data.vertices:
   weights=[g for g in v.groups if g.weight>0]
   assert weights and all(names[g.group] in rig.data.bones for g in weights),(o.name,v.index)
   assert abs(sum(g.weight for g in weights)-1)<.015,(o.name,v.index,'weight sum')
 # Reuse the actual same generated forearms and gloves in first-person, not an unrelated hand design.
 allparts=parts[:]+handparts
 equipment=[o for o in bpy.context.scene.objects if o.type!='MESH' or o.name=='weapon:joined']
 rig['character']='VECTOR original suit v1; compatible attributed Trooper rig and glove derivative'
 # Preserve separate authored plates in the editable source, before renderer batching.
 bpy.ops.wm.save_as_mainfile(filepath=str(WORK/'vector-fighter.blend'),compress=True)
 if '--source-only' in sys.argv:
  print('VECTOR_EDITABLE_SOURCE_SAVED')
  return
 # Merge costume meshes to bounded renderer count, retaining material slots and skin weights.
 def join(items,name):
  bpy.ops.object.select_all(action='DESELECT')
  for o in items:o.select_set(True)
  bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();o=items[0];o.name=name;return o
 armnames=('LowerArm','gloves')
 arm_parts=[o for o in allparts if (any(n in o.name for n in armnames) or
   'UpperArm' in o.name and any(n in o.name for n in ['fabric','sewn piping','flex seam'])) and 'hinge' not in o.name]
 body_parts=[o for o in allparts if o not in arm_parts]
 # Preserve the established FPS node contract used by equipment presentation.
 arms=join(arm_parts,'vector-armored-hands');body=join(body_parts,'VECTOR body')
 if '--candidate' in sys.argv:
  arms.name='VECTOR world forearms'
  export(WORK/'candidate-body.glb',equipment+[arms,body])
  arms.name='vector-armored-hands'
  export(WORK/'candidate-arms.glb',equipment+[arms])
  print('VECTOR_CANDIDATE_EXPORTED')
  return
 arms.name='VECTOR world forearms'
 export(ART/'trooper-body-animated.glb',equipment+[arms,body])
 arms.name='vector-armored-hands'
 export(ART/'trooper-arms-animated.glb',equipment+[arms])
 manifest=json.loads((ART/'asset-manifest.json').read_text())
 for e in manifest['entries']:
  if e['key'] in ['player-body','player-hands']:
   e['sha256']=hashlib.sha256((ROOT/'unity'/e['path']).read_bytes()).hexdigest();e['source']='VECTOR original suit v1; scripts/vector_fighter/build.py; compatible attributed Trooper skeleton and articulated glove derivative'
 (ART/'asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
 mounts=json.loads((ART/'trooper-mounts.json').read_text());mounts['character']='vector-fighter-v1'
 for key,kind in [('body','body'),('arms','arms')]:mounts[key]['sha256']=hashlib.sha256((ART/('trooper-'+kind+'-animated.glb')).read_bytes()).hexdigest()
 (ART/'trooper-mounts.json').write_text(json.dumps(mounts,indent=2)+'\n')
 print('VECTOR_FIGHTER_EXPORTED')

if __name__=='__main__':build()
