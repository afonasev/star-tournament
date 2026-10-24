"""Original VECTOR suit over the compatible, attributed Trooper rig and gloves.
Offline authored mesh dimensions are in metres, +Z up/-Y forward in Blender.
Reproducible source inputs are pinned Git blobs; never rebuild from the output.
Run: blender -b -noaudio --python scripts/vector_fighter/build.py
"""
import bpy, bmesh, math, json, hashlib, subprocess, sys, random
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
WORK=ROOT/'.local/vector-fighter';WORK.mkdir(parents=True,exist_ok=True)
BASE='00fbd33209eb755dd5f86499aa8ea12867a2efd0'
# Authored costume dimensions, not camera or gameplay scale. One source for skin and seams.
LIMBS={'UpperLeg':(.172,.185),'LowerLeg':(.125,.143),'UpperArm':(.124,.131),'LowerArm':(.077,.085)}


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
 stations=[(-.035,.80),(0,.90),(.10,.98),(.30,1),(.55,.92),(.78,.80),(.94,.73),(1.035,.75)]
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
 o.modifiers.remove(o.modifiers['Skin']);mod=o.modifiers.new('Ceramic thickness','SOLIDIFY');mod.thickness=.005;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=o.modifiers.new('Shell edge radius','BEVEL');mod.width=.0018;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
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


def breastplate():
 # Contoured horizontal rows produce clean reflections, without a central triangle fan.
 levels=[(1.435,.120,-.088),(1.400,.195,-.082),(1.360,.190,-.094),(1.323,.174,-.106),(1.298,.118,-.118),(1.254,.084,-.128)]
 verts=[];faces=[];N=25
 for row,(z,w,y) in enumerate(levels):
  for i in range(N):
   u=(i/(N-1)-.5)*2
   # A shallow neckline and convex sternum follow the reference breastplate.
   zz=z-(.016*(1-abs(u)) if row==0 else 0)
   verts.append((u*w,y-.026*(1-u*u),zz))
 for j in range(len(levels)-1):
  for i in range(N-1):q=j*N+i;faces.append((q,q+1,q+1+N,q+N))
 o=finish('VECTOR breastplate',verts,faces,paint,'Chest')
 bpy.context.view_layer.objects.active=o;o.modifiers.remove(o.modifiers['Skin'])
 mod=o.modifiers.new('Breastplate thickness','SOLIDIFY');mod.thickness=.009;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=o.modifiers.new('Breastplate edge','BEVEL');mod.width=.0015;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=o.modifiers.new('Skin','ARMATURE');mod.object=rig
 edge=[verts[i] for i in range(N)]+[verts[j*N+N-1] for j in range(1,len(levels))]+list(reversed(verts[-N:-1]))+[verts[j*N] for j in reversed(range(1,len(levels)-1))]
 tube('VECTOR breastplate perimeter',edge+[edge[0]],.0015,trim,'Chest')
 # Small recessed service hatch beneath the collar and bevel-following side inlays.
 panel('VECTOR chest collar insert',[(-.046,-.116,1.416),(.046,-.116,1.416),(.036,-.120,1.398),(-.036,-.120,1.398)],trim,'Chest',.002,.001)
 for sign in [-1,1]:
  tube('VECTOR pectoral inset',[(sign*.171,-.101,1.382),(sign*.161,-.114,1.343),(sign*.108,-.132,1.314),(sign*.078,-.145,1.268)],.0015,rubber,'Chest')

def suit():
 loft('VECTOR fitted torso',[(0,.035,z,rx,ry) for z,rx,ry in [(.82,.09,.065),(.88,.155,.10),(1.0,.16,.10),(1.08,.143,.09),(1.17,.156,.10),(1.28,.195,.112),(1.38,.216,.112),(1.43,.191,.098),(1.465,.074,.06)]],cloth,'Spine',
 weights=[{'Hips':1},{'Hips':1},{'Hips':.6,'Spine':.4},{'Spine':1},{'Spine':.7,'Chest':.3},{'Chest':1},{'Chest':.75,'UpperChest':.25},{'UpperChest':1},{'UpperChest':1}])
 loft('VECTOR neck seal',[(0,.025,z,r,r*.85) for z,r in [(1.43,.067),(1.47,.061),(1.52,.056),(1.57,.058)]],rubber,'Neck')
 for z in [1.464,1.477,1.490,1.503,1.516,1.529]:
  points=[(.059*math.sin(i*math.tau/48),.025-.051*math.cos(i*math.tau/48),z) for i in range(49)]
  tube('VECTOR neck fabric rib',points,.0012,cloth,'Neck')
 # The clean broad breastplate and lower floating abdominal shell are central to VECTOR.
 panel('VECTOR lower thorax',[(-.151,-.076,1.316),(-.080,-.112,1.345),(.080,-.112,1.345),(.151,-.076,1.316),(.121,-.092,1.243),(.079,-.086,1.159),(.045,-.083,1.126),(-.045,-.083,1.126),(-.079,-.086,1.159),(-.121,-.092,1.243)],paint,'Spine',.010,.002)
 breastplate()
 for s in [-1,1]:
  panel('VECTOR flank ceramic',[(s*.127,-.070,1.26),(s*.171,-.052,1.29),(s*.155,-.041,1.205),(s*.118,-.060,1.17)],paint,'Chest',.01,.003)
  panel('VECTOR collar inlay',[(s*.06,-.040,1.465),(s*.13,-.038,1.454),(s*.171,-.037,1.407),(s*.155,-.049,1.401),(s*.105,-.061,1.439),(s*.062,-.060,1.445)],metal,'UpperChest',.008,.002)
  for z in [1.09,1.12,1.15,1.18]:tube('VECTOR side flex rib',[(s*.116,-.018,z),(s*.13,.025,z+.01),(s*.117,.092,z+.012)],.004,trim,'Spine')
  box('VECTOR hip clasp',(s*.135,-.012,1.035),(.054,.083,.039),metal,'Hips',.005)
 box('VECTOR spinal bridge',(0,.156,1.33),(.026,.014,.19),metal,'Chest',.006)
 for z in [1.27,1.3,1.33,1.36,1.39]:box('VECTOR spine recess',(0,.165,z),(.018,.004,.009),rubber,'Chest',.002)
 panel('VECTOR lumbar',[(-.105,.148,1.19),(-.06,.160,1.22),(.06,.160,1.22),(.105,.148,1.19),(.065,.144,1.10),(-.065,.144,1.10)],paint,'Spine')
 panel('VECTOR pelvis shield',[(-.076,-.07,1.03),(.076,-.07,1.03),(.054,-.076,.945),(0,-.068,.897),(-.054,-.076,.945)],paint,'Hips',.009,.004)
 for side in ['Left','Right']:
  sign=1 if side=='Left' else -1
  for region,(width,depth) in LIMBS.items():
   name=side+region;bone=rig.data.bones[name];a=bone.head_local;b=rig.data.bones[side+'Hand'].head_local if region=='LowerArm' else bone.tail_local
   limb('VECTOR '+name+' fabric',a,b,width,depth,cloth,name)
   shell('VECTOR '+name+' ceramic',a,b,width+.010,depth+.010,name,.20 if region=='LowerArm' else .10,.87)
   if region in ['LowerArm','LowerLeg']:shell('VECTOR '+name+' rear',a,b,width+.004,depth+.006,name,.24,.72,True)
   if region in ['UpperLeg','UpperArm']:
    joint=side+('LowerLeg' if region=='UpperLeg' else 'LowerArm');p=b
    ellipsoid('VECTOR '+joint+' hinge',p,(width*.43,depth*.42,width*.40),rubber,joint)
   if region=='UpperArm':
    # Broader deltoid cup defines the soldier silhouette; forearm dimensions stay slim.
    shoulder_cap(side,a,b)
   if region=='LowerLeg':
    p=a+Vector((0,-.081,-.012));panel('VECTOR '+side+' knee',[(p.x-.048,p.y,p.z+.039),(p.x+.048,p.y,p.z+.039),(p.x+.056,p.y-.01,p.z-.017),(p.x+.029,p.y-.013,p.z-.072),(p.x-.029,p.y-.013,p.z-.072),(p.x-.056,p.y-.01,p.z-.017)],metal,name,.012,.006)
    panel('VECTOR knee inset',[(p.x-.031,p.y-.019,p.z+.022),(p.x+.031,p.y-.019,p.z+.022),(p.x+.035,p.y-.022,p.z-.011),(p.x+.016,p.y-.025,p.z-.050),(p.x-.016,p.y-.025,p.z-.050),(p.x-.035,p.y-.022,p.z-.011)],trim,name,.003,.001)
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
 ellipsoid('VECTOR '+side+' shoulder cushion',Vector(a)+axis*.018,(.080,.074,.075),cloth,name)
 verts=[];faces=[];N=28;M=12
 for j in range(M+1):
  t=j/M;z=-.040+.154*t;r=.034+.066*math.sin(math.pi*(.10+.77*t))
  for i in range(N):
   ang=(i/(N-1)-.5)*3.85
   verts.append(Vector(a)+axis*z+sideaxis*(math.sin(ang)*r)+front*(math.cos(ang)*r*.95))
 for j in range(M):
  for i in range(N-1):q=j*N+i;faces.append((q,q+1,q+1+N,q+N))
 o=finish('VECTOR '+side+' shoulder',verts,faces,paint,name)
 bpy.context.view_layer.objects.active=o;o.modifiers.remove(o.modifiers['Skin'])
 mod=o.modifiers.new('Cup thickness','SOLIDIFY');mod.thickness=.005;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=o.modifiers.new('Cup radius','BEVEL');mod.width=.004;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=o.modifiers.new('Skin','ARMATURE');mod.object=rig
 # Neutral lower guard and flush shoulder latches.
 for t in [.22,.75]:
  p=Vector(a)+axis*(.145*t)+front*.092
  ellipsoid('VECTOR '+side+' shoulder latch',p,(.008,.004,.009),metal,name)


def tailoring_details():
 for side in ['Left','Right']:
  sign=1 if side=='Left' else -1
  hip=rig.data.bones[side+'UpperLeg'].head_local
  ellipsoid('VECTOR '+side+' hip continuity',hip+Vector((0,0,.02)),(.085,.082,.105),cloth,side+'UpperLeg')
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
   width,depth=LIMBS[region]
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
 # Side brackets sit on the rigid breastplate's lower corners.
 for sign in [-1,1]:
  panel('VECTOR side locking bracket',[(sign*.138,-.084,1.25),(sign*.167,-.059,1.264),(sign*.181,-.058,1.228),(sign*.143,-.077,1.205),(sign*.125,-.086,1.22)],metal,'Chest',.007,.002)
 # Abdominal quilted inset panels and tailored seam rows.
 for z,w in [(1.062,.073),(1.093,.064),(1.123,.061)]:
  box('VECTOR abdominal fabric pad',(0,-.067,z),(w*2,.021,.025),cloth,'Spine',.009)
  tube('VECTOR abdominal stitch',[(-w,-.079,z),(0,-.081,z-.002),(w,-.079,z)],.0008,trim,'Spine')
 # Shorten exposed neck and broaden crown to the reference silhouette.
 for o in parts:
  if all(g.name=='Head' for g in o.vertex_groups):
   for v in o.data.vertices:v.co.x*=1.04;v.co.z-=.012

def combat_details():
 # Chest border, recessed collar and layered belt turn plain plates into fitted armor.
 for sign in [-1,1]:
  tube('VECTOR chest lower gasket',[(sign*.167,-.101,1.326),(sign*.121,-.121,1.295),(sign*.087,-.132,1.249),(0,-.133,1.249)],.0022,rubber,'Chest')
  panel('VECTOR clavicle harness',[(sign*.071,-.047,1.466),(sign*.134,-.025,1.460),(sign*.178,-.015,1.418),(sign*.156,-.046,1.398),(sign*.128,-.062,1.438),(sign*.073,-.070,1.447)],rubber,'UpperChest',.012,.002)
  panel('VECTOR collar ceramic',[(sign*.065,.005,1.467),(sign*.100,.018,1.455),(sign*.115,.061,1.447),(sign*.080,.069,1.456)],paint,'UpperChest',.010,.002)
  panel('VECTOR flank flex plate',[(sign*.099,-.073,1.176),(sign*.131,-.037,1.191),(sign*.145,-.026,1.096),(sign*.102,-.062,1.063)],trim,'Spine',.005,.001)
  for z in [1.087,1.108,1.129,1.150]:
   tube('VECTOR flank stitching',[(sign*.112,-.077,z),(sign*.139,-.04,z+.01)],.001,metal,'Spine')
  box('VECTOR belt buckle',(sign*.112,-.073,1.037),(.042,.012,.027),trim,'Hips',.003)
  for x,z in [(sign*.148,1.384),(sign*.083,1.267),(sign*.048,1.144)]:
   ellipsoid('VECTOR recessed chest fastener',(x,-.121 if z>1.2 else -.093,z),(.002,.0015,.002),metal,'Chest' if z>1.2 else 'Spine')
 for side in ['Left','Right']:
  for region in ['UpperArm','LowerArm','UpperLeg','LowerLeg']:
   name=side+region;bone=rig.data.bones[name];a=bone.head_local;b=rig.data.bones[side+'Hand'].head_local if region=='LowerArm' else bone.tail_local
   axis=(b-a).normalized();f=Vector((0,-1,0));f=(f-axis*f.dot(axis)).normalized();l=axis.cross(f).normalized();w,d=LIMBS[region]
   # Small trapezoidal metal joint cap, with dark inset, closely follows the limb.
   if region in ['LowerArm','LowerLeg']:
    def pt(t,x,y):return a.lerp(b,t)+l*x+f*y
    outline=[pt(.015,-w*.27,d*.46),pt(.015,w*.27,d*.46),pt(.075,w*.33,d*.51),pt(.20,w*.16,d*.49),pt(.20,-w*.16,d*.49),pt(.075,-w*.33,d*.51)]
    panel('VECTOR '+name+' joint cap',outline,metal,name,.003,.001)
    tube('VECTOR '+name+' joint inset',[pt(.075,-w*.15,d*.55),pt(.09,0,d*.56),pt(.075,w*.15,d*.55)],.0015,rubber,name)
   # Separate inset edge of limb panel, with precise fastening and longitudinal vent.
   for sg in [-1,1]:
    points=[a.lerp(b,t)+l*(sg*w*.35*(1-.2*t))+f*(d*.40) for t in [.27,.37,.57,.67]]
    tube('VECTOR '+name+' inlaid seam',points,.0012,rubber,name)
  # Deltoid lower rim and metal trim echo the shoulder cutout in VECTOR.
  bone=rig.data.bones[side+'UpperArm'];a=bone.head_local;axis=(bone.tail_local-a).normalized();f=Vector((0,-1,0));l=axis.cross(f).normalized()
  pts=[a+axis*.098+l*(math.sin(theta)*.090)+f*(math.cos(theta)*.086) for theta in [-1.8,-1.3,-.8,-.3,.3,.8,1.3,1.8]]
  tube('VECTOR '+side+' shoulder rim',pts,.003,metal,side+'UpperArm')
  tube('VECTOR '+side+' shoulder separation',[a+axis*t+l*.053+f*.081 for t in [-.012,.015,.045,.085]],.0017,rubber,side+'UpperArm')
  p=a+axis*.090+l*.065+f*.065
  box('VECTOR '+side+' shoulder lock',p,(.020,.010,.019),metal,side+'UpperArm',.002)
  # Raised trapezoidal rear plates remove the old interrupted/sliver silhouette.
  sign=1 if side=='Left' else -1
  panel('VECTOR '+side+' scapula cover',[(sign*.035,.154,1.432),(sign*.122,.146,1.426),(sign*.150,.143,1.367),(sign*.118,.159,1.294),(sign*.042,.164,1.272)],paint,'Chest',.008,.001)
  tube('VECTOR '+side+' scapula seam',[(sign*.055,.172,1.414),(sign*.068,.177,1.350),(sign*.060,.175,1.292)],.0018,rubber,'Chest')

def equipment_details():
 # Flush markings and hardware communicate assembly and scale without bulky armor.
 def stencil(name,text,pos,size,bone):
  curve=bpy.data.curves.new(name,'FONT');curve.body=text;curve.size=size;curve.extrude=.0001
  curve.space_character=1.15;curve.align_x='CENTER'
  o=bpy.data.objects.new(name,curve);bpy.context.collection.objects.link(o)
  o.location=pos;o.rotation_euler=(math.pi/2,0,0)
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
  bpy.ops.object.convert(target='MESH');bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
  o.data.materials.append(metal);o.vertex_groups.new(name=bone).add(list(range(len(o.data.vertices))),1,'REPLACE')
  mod=o.modifiers.new('Skin','ARMATURE');mod.object=rig;o.parent=rig;parts.append(o);o.select_set(False)
 stencil('VECTOR chest designation','V / 01',(-.092,-.110,1.379),.012,'Chest')
 stencil('VECTOR collar serial','VECTOR',(0,-.123,1.403),.008,'Chest')
 for sign in [-1,1]:
  # Recessed intake cartridges, placed on the broad upper plate below the collar.
  for i in range(4):
   x=sign*(.105+i*.010)
   # Surface follows the convex breastplate cross section at this height.
   y=-.085-.026*(1-(x/.192)**2)-.002
   box('VECTOR chest intake',(x,y,1.390),(.004,.002,.013),rubber,'Chest',.0007)
  # Titanium corner locks and captive screw heads.
  panel('VECTOR pectoral corner lock',[(sign*.172,-.104,1.377),(sign*.187,-.095,1.388),(sign*.185,-.101,1.360),(sign*.172,-.111,1.351)],metal,'Chest',.002,.001)
  for x,y,z in [(sign*.179,-.105,1.365),(sign*.095,-.143,1.286)]:
   ellipsoid('VECTOR captive screw',(x,y,z),(.003,.0015,.003),trim,'Chest')
   tube('VECTOR screw slot',[(x-.0013,y-.0016,z),(x+.0013,y-.0016,z)],.00045,metal,'Chest',6)
  # Radial ear screws and concentric acoustic grille sit on the flat outer bezel.
  for i in range(4):
   a=math.pi/4+i*math.pi/2;y=.020+.028*math.cos(a);z=1.673+.028*math.sin(a)
   ellipsoid('VECTOR ear captive screw',(sign*.115,y,z),(.0017,.0023,.0023),rubber,'Head')
  for z in [1.663,1.669,1.675,1.681]:
   tube('VECTOR ear recessed grille',[(sign*.115,.010,z),(sign*.115,.030,z)],.001,rubber,'Head',6)
  # Segmented belt webbing, with actual recessed clasp and keeper.
  panel('VECTOR belt side plate',[(sign*.068,-.080,1.056),(sign*.149,-.046,1.065),(sign*.163,-.030,1.025),(sign*.079,-.081,1.016)],metal,'Hips',.005,.002)
  box('VECTOR belt lock insert',(sign*.113,-.073,1.040),(.025,.008,.014),rubber,'Hips',.003)
 for side in ['Left','Right']:
  b=rig.data.bones[side+'UpperArm'];a=b.head_local;axis=(b.tail_local-a).normalized();f=Vector((0,-1,0));lat=axis.cross(f).normalized()
  # Thin overlapping shoulder edge plates, rather than an enlarged spherical shoulder.
  for sg in [-1,1]:
   pts=[a+axis*t+lat*(sg*x)+f*y for t,x,y in [(.042,.074,.058),(.070,.083,.058),(.106,.067,.055),(.090,.057,.068)]]
   panel('VECTOR '+side+' deltoid edge bracket',pts,metal,side+'UpperArm',.003,.001)
   p=a+axis*.080+lat*(sg*.071)+f*.066
   ellipsoid('VECTOR '+side+' deltoid screw',p,(.003,.002,.003),trim,side+'UpperArm')


def helmet():
 # Continuous shell under separated armor plates. The visor and its gasket share
 # one boundary; independently fitted surfaces caused intersections in v1.
 rings=[(.020,1.537,.065,.067),(.009,1.568,.089,.095),(.008,1.63,.102,.109),(.014,1.7,.105,.110),(.016,1.727,.099,.104),(.018,1.748,.082,.089),(.019,1.762,.058,.064),(.020,1.770,.028,.034),(.020,1.773,.001,.001)]
 loft('VECTOR helmet inner seal',[(0,y,z,rx,ry) for y,z,rx,ry in rings],paint,'Head',N=64)
 rows=[(1.558,.66,.095,.126),(1.580,1.04,.104,.131),(1.610,1.72,.109,.134),(1.645,2.34,.112,.134),(1.686,2.40,.112,.130),(1.714,2.23,.106,.122)]
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
  panel('VECTOR cheek sculpt',[(sign*.031,-.119,1.558),(sign*.072,-.106,1.599),(sign*.103,-.051,1.649),(sign*.108,-.020,1.625),(sign*.086,-.038,1.567),(sign*.028,-.112,1.524)],paint,'Head',.009,.002)
  panel('VECTOR cheek air intake',[(sign*.073,-.088,1.608),(sign*.094,-.047,1.633),(sign*.094,-.037,1.615),(sign*.076,-.075,1.588)],rubber,'Head',.002,.0005)
  panel('VECTOR temple bridge',[(sign*.076,-.082,1.713),(sign*.095,-.052,1.729),(sign*.107,-.003,1.714),(sign*.112,.008,1.691),(sign*.102,-.024,1.684),(sign*.096,-.047,1.704)],metal,'Head',.006,.001)
  for radius,xx,mat,n in [(.039,.104,metal,8),(.032,.109,rubber,8),(.024,.111,trim,8)]:
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
 panel('VECTOR chin shield',[(-.038,-.117,1.562),(0,-.131,1.554),(.038,-.117,1.562),(.032,-.119,1.529),(0,-.124,1.517),(-.032,-.119,1.529)],paint,'Head',.009,.002)
 box('VECTOR rear helmet panel',(0,.130,1.652),(.075,.008,.053),paint,'Head',.006)
 for x in [-.022,0,.022]:box('VECTOR rear helmet vent',(x,.137,1.651),(.007,.003,.027),trim,'Head',.001)


def export(path,objects):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='ACTIONS',export_force_sampling=True,export_extras=True,export_yup=True,export_skins=True,export_all_influences=False)


def build():
 global rig,paint,metal,rubber,cloth,trim,glass,parts,source_garment
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
 # Preserve authored cloth topology for an isolated primary-form comparison.
 source_garment=[]
 if '--forms' in sys.argv:
  for o in list(bpy.context.scene.objects):
   if o.type=='MESH' and o.data.materials and o.data.materials[0].name in ['material','pants','torso']:
    copy=o.copy();copy.data=o.data.copy();source_garment.append(copy)
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
 paint=material('vector-fighter-identity',(.43,.43,.43),.24,.47)
 metal=material('vector-fighter-titanium',(.16,.19,.21),.72,.38)
 rubber=material('vector-fighter-rubber',(.010,.013,.016),0,.91)
 cloth=material('vector-fighter-fabric',(.006,.008,.010),0,1.0)
 trim=material('vector-fighter-trim',(.022,.029,.034),.25,.65)
 glass=material('vector-fighter-visor',(.006,.011,.015),.68,.16)
 # Portable packed tangent-space weave; no procedural shader dependency in Unity.
 img=bpy.data.images.new('vector-fighter-weave-normal',width=128,height=128);img.colorspace_settings.name='Non-Color';pixels=[]
 for y in range(128):
  for x in range(128):
   v=Vector((.10*math.sin(x*math.tau/4),.10*math.sin(y*math.tau/4),1)).normalized();pixels.extend((v.x*.5+.5,v.y*.5+.5,v.z*.5+.5,1))
 img.pixels=pixels;img.pack();tex=cloth.node_tree.nodes.new('ShaderNodeTexImage');tex.image=img
 normal=cloth.node_tree.nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.12;cloth.node_tree.links.new(tex.outputs['Color'],normal.inputs['Color']);cloth.node_tree.links.new(normal.outputs['Normal'],cloth.node_tree.nodes.get('Principled BSDF').inputs['Normal'])
 # Packed microtexture gives large clean paint panels a manufactured surface.
 rng=random.Random(19);size=256;grain=bpy.data.images.new('vector-coating-grain',width=size,height=size);grain.colorspace_settings.name='Non-Color';data=[]
 for y in range(size):
  for x in range(size):
   value=.39+.035*rng.random()+.008*math.sin(x*.13)*math.sin(y*.17)
   data.extend((value,value,value,1))
 grain.pixels=data;grain.pack();node=paint.node_tree.nodes.new('ShaderNodeTexImage');node.image=grain
 paint.node_tree.links.new(node.outputs['Color'],paint.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
 parts=[]
 if '--forms' in sys.argv:
  assert '--candidate' in sys.argv, 'Primary-form studies require visual approval before production export'
  import importlib.util
  sys.dont_write_bytecode=True
  sys.path.insert(0,str(ROOT/'scripts/vector_fighter'))
  formfile='remodel.py' if sys.argv[sys.argv.index('--forms')+1]=='remodel' else 'sculpt_forms.py'
  spec=importlib.util.spec_from_file_location('vector_forms',ROOT/'scripts/vector_fighter'/formfile);forms=importlib.util.module_from_spec(spec);spec.loader.exec_module(forms)
  forms.build(sys.modules[__name__],sys.argv[sys.argv.index('--forms')+1])
 else:
  suit();helmet();combat_details();equipment_details()
  if '--details' in sys.argv:
   assert '--candidate' in sys.argv, 'Additive-detail studies were rejected and must not replace production assets'
   import importlib.util
   sys.dont_write_bytecode=True
   spec=importlib.util.spec_from_file_location('vector_details',ROOT/'scripts/vector_fighter/refine_details.py');details=importlib.util.module_from_spec(spec);spec.loader.exec_module(details)
   details.build(sys.modules[__name__],sys.argv[sys.argv.index('--details')+1])
  tailoring_details()
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
 rig['character']='VECTOR original suit v2; compatible attributed Trooper rig and glove derivative'
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
   e['sha256']=hashlib.sha256((ROOT/'unity'/e['path']).read_bytes()).hexdigest();e['source']='VECTOR original suit v2; scripts/vector_fighter/build.py; compatible attributed Trooper skeleton and articulated glove derivative'
 (ART/'asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
 mounts=json.loads((ART/'trooper-mounts.json').read_text());mounts['character']='vector-fighter-v2'
 for key,kind in [('body','body'),('arms','arms')]:mounts[key]['sha256']=hashlib.sha256((ART/('trooper-'+kind+'-animated.glb')).read_bytes()).hexdigest()
 (ART/'trooper-mounts.json').write_text(json.dumps(mounts,indent=2)+'\n')
 print('VECTOR_FIGHTER_EXPORTED')

if __name__=='__main__':build()
