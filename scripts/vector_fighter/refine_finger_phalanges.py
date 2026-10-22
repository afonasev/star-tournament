"""F15: rebuild anatomical glove cloth around the unchanged F12 skeleton.
Continuous palm, thenar volume and tapered fingers replace inherited cloth;
existing ceramic layers are retained. Arms/animation stay fixed; the approved
Shotgun palm swell is reduced independently.
"""
import bpy,bmesh,math,json,importlib.util
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'.local/vector-f15';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f11-fps-forms.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');o=bpy.data.objects['VECTOR articulated gloves'];count=len(bpy.context.scene.objects)
# User-approved F14 clearance correction: shrink only the Strata palm swell.
# The attachment, central grip and every other weapon component stay exact.
weapon=bpy.data.objects['weapon:joined'];wb=bmesh.new();wb.from_mesh(weapon.data)
bmesh.ops.remove_doubles(wb,verts=list(wb.verts),dist=.000001);remaining=set(wb.verts);swell=[]
while remaining:
 todo=[remaining.pop()];part=[]
 while todo:
  v=todo.pop();part.append(v)
  for edge in v.link_edges:
   other=edge.other_vert(v)
   if other in remaining:remaining.remove(other);todo.append(other)
 lo=Vector([min(v.co[i] for v in part) for i in range(3)]);hi=Vector([max(v.co[i] for v in part) for i in range(3)])
 if abs((hi-lo).x-.153)<.00001 and abs(lo.z+.295)<.00001:swell.extend(tuple(round(c,6) for c in v.co) for v in part)
wb.free();assert len(swell)==96,'Expected original Strata palm swell only'
swell=set(swell);changed_swell=0
for v in weapon.data.vertices:
 if tuple(round(c,6) for c in v.co) in swell:
  v.co.x=-.0095+(v.co.x+.0095)*(.0455/.153)
  v.co.y=.07+(v.co.y-.07)*(.15/.28);changed_swell+=1
assert changed_swell==115,changed_swell
weapon.data.update()

# The inherited detached stitch strips tear across the reshaped thumb web.
# Replace their raised geometry with the continuous woven cloth surface;
# retain the original source for comparison and all ceramic/chassis details.
seams={i for face in o.data.polygons if 'seam' in o.data.materials[face.material_index].name for i in face.vertices}
bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index in seams],context='VERTS');bm.to_mesh(o.data);bm.free()
frames={}
for side,sign in [('Left',1),('Right',-1)]:
 wrist=rig.data.bones[side+'Hand'].head_local.copy();end=sum((rig.data.bones[side+d+'Proximal'].head_local for d in ['Index','Middle','Ring','Little']),Vector())/4
 axis=(end-wrist).normalized();normal=Vector((sign,0,1)).normalized();normal=(normal-axis*normal.dot(axis)).normalized();lateral=normal.cross(axis).normalized()
 frames[side]=(wrist,axis,normal,lateral,(end-wrist).length)
def smooth(a,b,x):
 t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
changed=0
for v in o.data.vertices:
 p=v.co.copy();side='Left' if p.x>0 else 'Right';w,axis,n,lat,length=frames[side];rel=p-w;t=rel.dot(axis)/length;depth=rel.dot(n)
 # Preserve the seated cuff and knuckle line; reduce the padded spherical palm
 # to a flatter metacarpal volume with a gradual thenar/hypothenar transition.
 palm=smooth(.02,.22,t)*(1-smooth(.72,1.04,t))
 p-=n*depth*.36*palm
 # Taper the palm heel into the unchanged cuff instead of an abrupt bulge.
 heel=smooth(.02,.20,t)*(1-smooth(.32,.72,t))
 p-=lat*rel.dot(lat)*.14*heel
 # Follow the nearest finger chain only after its root web. The palmar contact
 # half stays in place; flatten the dorsal padding, preserving gripping pads.
 chains=[]
 for digit in ['Thumb','Index','Middle','Ring','Little']:
  chain=[rig.data.bones[side+digit+s] for s in ['Proximal','Intermediate','Distal']]
  for b in chain:
   d=b.tail_local-b.head_local;u=max(0,min(1,(v.co-b.head_local).dot(d)/d.length_squared));q=b.head_local+d*u
   chains.append(((v.co-q).length,digit,b,q,d.normalized()))
 _,digit,b,q,d=min(chains,key=lambda item:item[0]);root=rig.data.bones[side+digit+'Proximal'];advance=(v.co-root.head_local).dot((root.tail_local-root.head_local).normalized())
 if advance>0:
  fade=smooth(0,.018,advance);radial=v.co-q;dn=n-d*n.dot(d);dn.normalize();dorsal=radial.dot(dn)
  if dorsal>0:p-=dn*dorsal*.20*fade
  # A less swollen thumb shaft, while retaining its pad and bone positions.
  if digit=='Thumb':p-=(radial-dn*dorsal)*.10*fade
 if (p-v.co).length>1e-7:changed+=1
 v.co=p
o.data.update()
# Replace the inherited folded textile with a closed anatomical volume.
# Keep neutral cloth and participant-colored ceramic material roles.
from mathutils.bvhtree import BVHTree
cloth={i for face in o.data.polygons if o.data.materials[face.material_index].name in ['material.001','vector-glove.001'] for i in face.vertices}
# Author a clean closed hand volume around the existing skeleton. Elliptic
# palm sections define the heel, metacarpals and knuckle row; tapered digit
# sections join that volume before smoothing/skin assignment.
verts=[];faces=[]
def loft(rings,axis,normal,lateral,N=32,power=1):
 start=len(verts)
 for center,rx,ry in rings:
  exponent=power(center) if callable(power) else power
  for i in range(N):
   theta=2*math.pi*i/N;c=math.cos(theta);s=math.sin(theta)
   verts.append(center+lateral*(rx*math.copysign(abs(c)**exponent,c))+normal*(ry*math.copysign(abs(s)**exponent,s)))
 faces.append(tuple(reversed([start+i for i in range(N)])))
 for j in range(len(rings)-1):
  for i in range(N):k=start+j*N+i;l=start+j*N+(i+1)%N;faces.append((k,l,l+N,k+N))
 faces.append(tuple(start+(len(rings)-1)*N+i for i in range(N)))
for side in ['Left','Right']:
 w,axis,n,lat,length=frames[side]
 # Natural glove heel, flatter palm and broad MCP row, with no detached cuff.
 sections=[(-.03,.029,.029),(0,.030,.028),(.15,.033,.023),(.35,.039,.019),(.60,.043,.017),(.82,.044,.013),(.96,.042,.010),(1.015,.033,.006),(1.035,.012,.003)]
 palm_start=len(verts)
 # A padded palm has rounded rectangular sections; an ellipse collapses at
 # the ulnar edge and exposes the little metacarpal as a separate tube.
 loft([(w+axis*(t*length)-n*(.002*smooth(.10,.32,t)),rx,ry) for t,rx,ry in sections],axis,n,lat,
      power=lambda center:1-.35*smooth(.10,.32,(center-w).dot(axis)/length))
 knuckles=sorted(((rig.data.bones[side+d+'Proximal'].head_local-w).dot(lat),
                  (rig.data.bones[side+d+'Proximal'].head_local-w).dot(axis)) for d in ['Index','Middle','Ring','Little'])
 for v in verts[palm_start:]:
  t=(v-w).dot(axis)/length;x=(v-w).dot(lat)
  y=knuckles[0][1] if x<knuckles[0][0] else knuckles[-1][1]
  for (x0,y0),(x1,y1) in zip(knuckles,knuckles[1:]):
   if x0<=x<=x1:y=y0+(y1-y0)*(x-x0)/(x1-x0);break
  v+=axis*smooth(.7,1.04,t)*(y-length-.003)
 for digit,rr in [('Thumb',.0115),('Index',.0095),('Middle',.010),('Ring',.0093),('Little',.008)]:
  bones=[rig.data.bones[side+digit+seg] for seg in ['Proximal','Intermediate','Distal']]
  if digit!='Thumb':
   # Local rounded MCP volume, confined to the knuckle. The old 32 mm
   # metacarpal stalk is gone; this only maintains volume through flexion.
   root=bones[0];d=(root.tail_local-root.head_local).normalized();dn=(n-d*n.dot(d)).normalized();dl=dn.cross(d).normalized()
   loft([(root.head_local+d*t,rr*rx,rr*ry) for t,rx,ry in [
    (-.010,.35,.32),(-.006,.90,.78),(0,1.14,.94),(.006,1.02,.86),(.010,.60,.50)]],d,dn,dl,24)
  if digit=='Thumb':
   # The first thumb bone is a metacarpal inside the thenar eminence,
   # not a third exposed phalanx. Blend it into the palm/web.
   # Broad thenar eminence: the metacarpal is enclosed in the palm,
   # with a rounded triangular transition into the two exposed phalanges.
   # A narrow capsule here creates an apparent third finger segment.
   a=w+axis*(length*.42)+lat*(-.015 if side=='Left' else .015)-n*.005
   b=bones[0].tail_local;da=(b-a).normalized();tn=(n-da*n.dot(da)).normalized();tl=tn.cross(da).normalized()
   loft([(a.lerp(b,t),rx,ry) for t,rx,ry in [
    (0,.024,.013),(.18,.023,.014),(.42,.020,.014),
    (.65,.016,.012),(.82,.013,.011),(1,.012,.010),(1.04,.010,.009)]],da,tn,tl)
  # One continuous digit: near-straight shafts, narrow flexion creases,
  # modest knuckle volume and a flattened pad, not overlapping capsules.
  rings=[]
  for j,bone in enumerate(bones):
   if digit=='Thumb' and j==0:continue # Metacarpal is covered by thenar, not an exposed finger shaft.
   d=bone.tail_local-bone.head_local
   # Extend the root section into the padded MCP row. A separate capped
   # metacarpal capsule leaves a false extra segment beside a flexed pinky.
   stations=([(-.18,1.06)] if j==0 and digit!='Thumb' else [])+[(0,1.0),(.10,.97),(.28,.95),(.62,.92),(.88,.96),(.97,1.0)]
   for t,f in stations:
    if j==2 and t>.70:continue
    r=rr*(1-.11*j)*f
    pad_root=(1-smooth(.02,.45,t)) if j==0 and digit!='Thumb' else 0
    nd=n-d.normalized()*n.dot(d.normalized());nd.normalize()
    rings.append((bone.head_local+d*t+nd*(.0025*pad_root),r,r*(.82-.20*pad_root),d.normalized()))
  d=(bones[-1].tail_local-bones[-1].head_local).normalized();tip=bones[-1].tail_local
  for back,f in [(rr*.65,.90),(rr*.38,.76),(rr*.15,.48),(0,.08)]:rings.append((tip-d*back,rr*.78*f,rr*.64*f,d))
  direction=(bones[-1].tail_local-bones[0].head_local).normalized();dn=n-direction*n.dot(direction);dn.normalize();dl=dn.cross(direction).normalized()
  # Sections follow each phalanx, rather than shearing obliquely to a
  # single overall digit axis. This preserves pads in flexed skinning.
  start=len(verts);N=24
  for center,rx,ry,segment_axis in rings:
   normal=n-segment_axis*n.dot(segment_axis);normal.normalize();lateral=normal.cross(segment_axis).normalized()
   for i in range(N):
    theta=2*math.pi*i/N;verts.append(center+lateral*(rx*math.cos(theta))+normal*(ry*math.sin(theta)))
  faces.append(tuple(reversed([start+i for i in range(N)])))
  for j in range(len(rings)-1):
   for i in range(N):k=start+j*N+i;l=start+j*N+(i+1)%N;faces.append((k,l,l+N,k+N))
  faces.append(tuple(start+(len(rings)-1)*N+i for i in range(N)))
mesh=bpy.data.meshes.new('F14 continuous anatomical glove');mesh.from_pydata(verts,[],faces);mesh.update()
shell=bpy.data.objects.new('F14 anatomical cloth',mesh);bpy.context.collection.objects.link(shell)
for mat in o.data.materials:mesh.materials.append(mat)
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
bpy.context.view_layer.objects.active=shell
for mod in list(shell.modifiers):shell.modifiers.remove(mod)
mod=shell.modifiers.new('Continuous anatomical glove surface','REMESH');mod.mode='VOXEL';mod.voxel_size=.00065;mod.use_smooth_shade=True;mod.use_remove_disconnected=False;bpy.ops.object.modifier_apply(modifier=mod.name)
assert len(shell.data.vertices)>10000, 'Voxel shell lost volume: '+str(len(shell.data.vertices))
# Each hand must remain one connected textile surface.
bm=bmesh.new();bm.from_mesh(shell.data);remaining=set(bm.verts);component_sizes=[]
while remaining:
 todo=[remaining.pop()];size=0
 while todo:
  v=todo.pop();size+=1
  for edge in v.link_edges:
   other=edge.other_vert(v)
   if other in remaining:remaining.remove(other);todo.append(other)
 component_sizes.append(size)
bm.free();assert len(component_sizes)==2,('Disconnected glove cloth',component_sizes)
mod=shell.modifiers.new('Relax thumb web','SMOOTH');mod.factor=.35;mod.iterations=10;bpy.ops.object.modifier_apply(modifier=mod.name)
def assign(obj,index,weights):
 for group in obj.vertex_groups:group.remove([index])
 weights=dict(sorted(weights.items(),key=lambda x:-x[1])[:4]);total=sum(weights.values())
 for name,value in weights.items():
  if value>0:(obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)).add([index],value/total,'REPLACE')
# Re-author skin from anatomical chains instead of inheriting the source's
# alternating palm/finger weights. The palm is rigid Hand, with a continuous
# web transition and only adjacent segment blending around each knuckle.
chains={}
for side in ['Left','Right']:
 for digit in ['Thumb','Index','Middle','Ring','Little']:
  bones=[rig.data.bones[side+digit+seg] for seg in ['Proximal','Intermediate','Distal']]
  lengths=[(b.tail_local-b.head_local).length for b in bones];chains[side,digit]=(bones,lengths)
for v in shell.data.vertices:
 side='Left' if v.co.x>0 else 'Right';p=v.co;options=[]
 w,hand_axis,n,lat,palm_length=frames[side];longitudinal=(p-w).dot(hand_axis)/palm_length;lateral=(p-w).dot(lat)
 thumb_lateral=(rig.data.bones[side+'ThumbProximal'].head_local-w).dot(lat)
 for digit in ['Thumb','Index','Middle','Ring','Little']:
  bones,lengths=chains[side,digit];offset=0;nearest=None
  for j,bone in enumerate(bones):
   axis=(bone.tail_local-bone.head_local).normalized();t=(p-bone.head_local).dot(axis);q=bone.head_local+axis*max(0,min(lengths[j],t));dist=(p-q).length
   if nearest is None or dist<nearest[0]:nearest=(dist,offset+max(0,min(lengths[j],t)))
   offset+=lengths[j]
  root_axis=(bones[0].tail_local-bones[0].head_local).normalized();advance=(p-bones[0].head_local).dot(root_axis)
  fade=smooth(-.020,.008,advance)
  fade*=1-smooth(.028 if digit=='Thumb' else .025,.048 if digit=='Thumb' else .045,nearest[0])
  if digit!='Thumb':
   # Keep metacarpals in the palm; allow rotation only around their MCP.
   fade=smooth(-.016,.012,advance)*(1-smooth(.022,.042,nearest[0]))
  else:
   # Continuous thenar deformation across the palm-to-metacarpal fan.
   radial=lateral*(1 if thumb_lateral>0 else -1)
   fade=max(smooth(.016,.065,radial)*smooth(.018,.060,(p-w).dot(hand_axis)),smooth(lengths[0]-.016,lengths[0]+.005,advance))
   fade*=1-smooth(.018,.038,nearest[0])
  options.append((fade/(nearest[0]+.008),fade,digit,nearest[1]))
 # Blend the web between adjacent chains continuously. A hard nearest-chain
 # switch produces a visible fan of stretched triangles when fingers spread.
 candidates=sorted(options,reverse=True)[:2];scores=[max(0,item[0])**6 for item in candidates];total=sum(scores)
 weights={side+'Hand':1.0}
 for (_,fade,digit,arc),score in zip(candidates,scores):
  influence=fade*score/total if total>1e-12 else 0
  bones,lengths=chains[side,digit]
  j1=smooth(lengths[0]-.0045,lengths[0]+.0045,arc);j2=smooth(lengths[0]+lengths[1]-.004,lengths[0]+lengths[1]+.004,arc)
  weights[side+'Hand']-=influence
  for name,value in [(bones[0].name,influence*(1-j1)),(bones[1].name,influence*j1*(1-j2)),(bones[2].name,influence*j2)]:weights[name]=weights.get(name,0)+value
 w,axis,n,lat,length=frames[side];along=(p-w).dot(axis)
 if along<.012:
  hand=smooth(-.015,.012,along);weights={side+'LowerArm':1-hand,side+'Hand':hand}
 assign(shell,v.index,weights)
# Continuous UVs in the hand frame, avoiding inherited stretched web triangles.
uv=shell.data.uv_layers.new(name='UVMap')
for loop in shell.data.loops:
 v=shell.data.vertices[loop.vertex_index];side='Left' if v.co.x>0 else 'Right';w,axis,n,lat,length=frames[side];r=v.co-w
 uv.data[loop.index].uv=(r.dot(lat)*3,r.dot(axis)*3)
glove_slot=next(i for i,m in enumerate(shell.data.materials) if m.name=='vector-glove.001')
thread_slot=next(i for i,m in enumerate(shell.data.materials) if 'seam' in m.name)
for face in shell.data.polygons:
 face.use_smooth=True;center=sum((shell.data.vertices[i].co for i in face.vertices),Vector())/len(face.vertices)
 side='Left' if center.x>0 else 'Right';w,axis,n,lat,length=frames[side];r=center-w;along=r.dot(axis)
 face.material_index=0 if along<.012 else glove_slot
 # Stitching is a flush surface region, not a detached strip that can tear.
 if .025<along<.10 and r.dot(n)>0 and abs(abs(r.dot(lat))-.025)<.0015:face.material_index=thread_slot
assert {0,glove_slot,thread_slot}.issubset({p.material_index for p in shell.data.polygons})
bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index in cloth],context='VERTS');bm.to_mesh(o.data);bm.free()
# Re-seat all existing seam islands on the clean textile and give them its
# deformation. The ceramic assemblies retain their authored material roles.
shell.data.calc_loop_triangles();surface=[v.co.copy() for v in shell.data.vertices];triangles=[tuple(t.vertices) for t in shell.data.loop_triangles];surf=BVHTree.FromPolygons(surface,triangles,all_triangles=True)
# Move each existing backing/coating pair together onto the new cloth.
# Retain their closed topology and relative layer clearance.
bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bm.verts.index_update();remaining=set(bm.verts);panels=[]
while remaining:
 todo=[remaining.pop()];part=[]
 while todo:
  vertex=todo.pop();part.append(vertex)
  for edge in vertex.link_edges:
   other=edge.other_vert(vertex)
   if other in remaining:remaining.remove(other);todo.append(other)
 if len(part)==672:
  center=sum((v.co for v in part),Vector())/len(part);mat=o.data.materials[next(iter(part[0].link_faces)).material_index].name;panels.append((part,center,mat))
for part,center,mat in panels:
 if 'chassis' not in mat:continue
 coating=min((item for item in panels if 'ceramic' in item[2]),key=lambda item:(item[1]-center).length_squared)
 hit,normal,idx,dist=surf.find_nearest(center);delta=hit+normal*.0005-center
 for layer in [part,coating[0]]:
  for vertex in layer:o.data.vertices[vertex.index].co+=delta
bm.free()
# Retained palm shields remain rigid. Only the textile web and the newly
# authored individual phalanx shields participate in finger deformation.
for v in o.data.vertices:
 side='Left' if v.co.x>0 else 'Right'
 assign(o,v.index,{side+'Hand':1})
# Replace inherited finger armor islands: they were sized for the rejected
# inflated fingers and bridged flexion joints. Keep palm/wrist armor untouched.
bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();remaining=set(bm.verts);remove=[]
while remaining:
 todo=[remaining.pop()];part=[]
 while todo:
  v=todo.pop();part.append(v)
  for edge in v.link_edges:
   other=edge.other_vert(v)
   if other in remaining:remaining.remove(other);todo.append(other)
 center=sum((v.co for v in part),Vector())/len(part);side='Left' if center.x>0 else 'Right';nearest=[]
 for digit in ['Thumb','Index','Middle','Ring','Little']:
  bones,lengths=chains[side,digit];root=bones[0];direction=(root.tail_local-root.head_local).normalized()
  advance=(center-root.head_local).dot(direction)
  dist=min((center-(b.head_local+(b.tail_local-b.head_local)*max(0,min(1,(center-b.head_local).dot(b.tail_local-b.head_local)/(b.tail_local-b.head_local).length_squared)))).length for b in bones)
  nearest.append((dist,advance,digit))
 dist,advance,digit=min(nearest)
 if dist<.045 and advance>-.004:remove.extend(part)
bmesh.ops.delete(bm,geom=remove,context='VERTS');bm.to_mesh(o.data);bm.free()
# Small closed dorsal shields stay wholly on one phalanx, clear of the joints.
plate_objects=[]
for side in ['Left','Right']:
 w,axis,n,lat,length=frames[side]
 for digit,rr in [('Thumb',.0115),('Index',.0095),('Middle',.010),('Ring',.0093),('Little',.008)]:
  bones,lengths=chains[side,digit]
  for j,bone in enumerate(bones):
   if digit=='Thumb' and j==0:continue
   direction=(bone.tail_local-bone.head_local).normalized();dn=n-direction*n.dot(direction);dn.normalize();dl=dn.cross(direction).normalized()
   radius=rr*(1-.11*j);center=bone.head_local.lerp(bone.tail_local,.48)
   for label,offset,scale in [('chassis',.0005,1.0),('ceramic',.0014,.88)]:
    width=radius*.78*scale;half=lengths[j]*.30*scale;thick=.0006
    # Sample the curved surface internally: an ngon bridges across the crown
    # and cuts through the textile after triangulation.
    rows=[-1,-.65,0,.65,1];columns=9;layer_size=len(rows)*columns
    pv=[];pf=[]
    for depth in [-thick,thick]:
     for y in rows:
      row_width=.65 if abs(y)==1 else 1
      for k in range(columns):
       x=(-1+2*k/(columns-1))*row_width
       pv.append(center+dl*(x*width)+direction*(y*half)+dn*(radius*.80*math.sqrt(max(.01,1-(x*.78*scale)**2))+offset+depth))
    for layer in [0,layer_size]:
     for row in range(len(rows)-1):
      for col in range(columns-1):
       a=layer+row*columns+col;pf.append((a,a+1,a+columns+1,a+columns))
    boundary=list(range(columns))+[r*columns+columns-1 for r in range(1,len(rows))]+list(range(layer_size-2,layer_size-columns-1,-1))+[r*columns for r in range(len(rows)-2,0,-1)]
    for i,a in enumerate(boundary):
     b=boundary[(i+1)%len(boundary)];pf.append((a,b,b+layer_size,a+layer_size))
    pm=bpy.data.meshes.new('F14 '+label+' phalanx');pm.from_pydata(pv,[],pf);pm.update();pm.materials.append(next(m for m in o.data.materials if label in m.name))
    pb=bmesh.new();pb.from_mesh(pm);bmesh.ops.recalc_face_normals(pb,faces=list(pb.faces));pb.to_mesh(pm);pb.free()
    po=bpy.data.objects.new('F14 fitted '+label,pm);bpy.context.collection.objects.link(po);po.vertex_groups.new(name=bone.name).add(list(range(len(pv))),1,'REPLACE')
    for face in pm.polygons:face.use_smooth=False
    plate_objects.append(po)
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);shell.select_set(True)
for po in plate_objects:po.select_set(True)
bpy.context.view_layer.objects.active=o;bpy.ops.object.join()
selected=cloth
assert len(bpy.context.scene.objects)==count
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f15-hand-anatomy.blend'),compress=True)
spec=importlib.util.spec_from_file_location('vector_build',ROOT/'scripts/vector_fighter/build.py');g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g);g.rig=rig
meshes=[m for m in bpy.context.scene.objects if m.type=='MESH' and m.name!='weapon:joined'];arm_parts=[m for m in meshes if ('LowerArm' in m.name or 'gloves' in m.name or ('UpperArm' in m.name and 'fabric' in m.name))];body_parts=[m for m in meshes if m not in arm_parts]
def join(items,name):
 bpy.ops.object.select_all(action='DESELECT')
 for item in items:item.select_set(True)
 bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();items[0].name=name;return items[0]
arms=join(arm_parts,'VECTOR world forearms');body=join(body_parts,'VECTOR body');equipment=[m for m in bpy.context.scene.objects if m.type!='MESH' or m.name=='weapon:joined']
g.export(OUT/'candidate-body.glb',equipment+[arms,body]);arms.name='vector-armored-hands';g.export(OUT/'candidate-arms.glb',equipment+[arms])
(OUT/'anatomy-report.json').write_text(json.dumps({'source':'f11-fps-forms.blend','changed_glove_vertices':changed,'replaced_cloth_vertices':len(selected),'removed_stitch_vertices':len(seams),'objects':count,'cloth_component_sizes':component_sizes,'bones_and_actions':'unchanged','status':'candidate, requires native contact review'},indent=2)+'\n')
