"""F13: rebuild anatomical glove cloth around the unchanged F12 skeleton.
Continuous palm, thenar volume and tapered fingers replace inherited cloth;
existing ceramic layers are retained. No arm, weapon or animation edits.
"""
import bpy,bmesh,math,json,importlib.util
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'.local/vector-f13';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f11-fps-forms.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');o=bpy.data.objects['VECTOR articulated gloves'];count=len(bpy.context.scene.objects)
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
# capsules merge into that volume before smoothing/skin assignment.
verts=[];faces=[]
def loft(rings,axis,normal,lateral,N=32):
 start=len(verts)
 for center,rx,ry in rings:
  for i in range(N):
   theta=2*math.pi*i/N;verts.append(center+lateral*(rx*math.cos(theta))+normal*(ry*math.sin(theta)))
 faces.append(tuple(reversed([start+i for i in range(N)])))
 for j in range(len(rings)-1):
  for i in range(N):k=start+j*N+i;l=start+j*N+(i+1)%N;faces.append((k,l,l+N,k+N))
 faces.append(tuple(start+(len(rings)-1)*N+i for i in range(N)))
def capsule(a,b,ra,rb):
 axis=(b-a).normalized();n=axis.cross(Vector((0,1,0)))
 if n.length<.01:n=axis.cross(Vector((0,0,1)))
 n.normalize();lat=axis.cross(n);rings=[]
 for theta in [-math.pi/2,-math.pi/3,-math.pi/6,0]:rings.append((a+axis*(ra*math.sin(theta)),max(.0001,ra*math.cos(theta)),max(.0001,ra*math.cos(theta))))
 rings.append((a.lerp(b,.5),(ra+rb)/2,(ra+rb)/2))
 for theta in [0,math.pi/6,math.pi/3,math.pi/2]:rings.append((b+axis*(rb*math.sin(theta)),max(.0001,rb*math.cos(theta)),max(.0001,rb*math.cos(theta))))
 loft(rings,axis,n,lat,24)
for side in ['Left','Right']:
 w,axis,n,lat,length=frames[side]
 # Natural glove heel, flatter palm and broad MCP row, with no detached cuff.
 sections=[(-.03,.029,.029),(0,.030,.028),(.15,.033,.023),(.35,.039,.019),(.60,.043,.017),(.82,.044,.016),(1,.041,.014),(1.04,.032,.011)]
 loft([(w+axis*(t*length),rx,ry) for t,rx,ry in sections],axis,n,lat)
 for digit,rr in [('Thumb',.014),('Index',.011),('Middle',.0115),('Ring',.0105),('Little',.009)]:
  bones=[rig.data.bones[side+digit+seg] for seg in ['Proximal','Intermediate','Distal']]
  if digit=='Thumb':capsule(w+axis*(length*.36),bones[0].head_local,.016,.014)
  for j,bone in enumerate(bones):
   radius=rr*(.94-.10*j);tip=bone.tail_local-(bone.tail_local-bone.head_local).normalized()*radius if j==2 else bone.tail_local
   capsule(bone.head_local,tip,rr*(1-.10*j),radius)
mesh=bpy.data.meshes.new('F13 continuous anatomical glove');mesh.from_pydata(verts,[],faces);mesh.update()
shell=bpy.data.objects.new('F13 anatomical cloth',mesh);bpy.context.collection.objects.link(shell)
for mat in o.data.materials:mesh.materials.append(mat)
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
bpy.context.view_layer.objects.active=shell
for mod in list(shell.modifiers):shell.modifiers.remove(mod)
mod=shell.modifiers.new('Continuous anatomical glove surface','REMESH');mod.mode='VOXEL';mod.voxel_size=.0015;mod.use_smooth_shade=True;mod.use_remove_disconnected=False;bpy.ops.object.modifier_apply(modifier=mod.name)
assert len(shell.data.vertices)>10000, 'Voxel shell lost volume: '+str(len(shell.data.vertices))
mod=shell.modifiers.new('Relax thumb web','SMOOTH');mod.factor=.6;mod.iterations=6;bpy.ops.object.modifier_apply(modifier=mod.name)
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
  fade=smooth(-.020 if digit=='Thumb' else -.012,.035 if digit=='Thumb' else .018,advance)
  fade*=1-smooth(.028 if digit=='Thumb' else .025,.048 if digit=='Thumb' else .045,nearest[0])
  if digit!='Thumb':fade*=smooth(.86,1.02,longitudinal)
  else:fade*=smooth(.025,.050,lateral*(1 if thumb_lateral>0 else -1))
  options.append((fade/(nearest[0]+.008),fade,digit,nearest[1]))
 _,fade,digit,arc=max(options);bones,lengths=chains[side,digit];weights={side+'Hand':1-fade}
 j1=smooth(lengths[0]-.007,lengths[0]+.007,arc);j2=smooth(lengths[0]+lengths[1]-.006,lengths[0]+lengths[1]+.006,arc)
 weights[bones[0].name]=fade*(1-j1);weights[bones[1].name]=fade*j1*(1-j2);weights[bones[2].name]=fade*j2
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
for v in o.data.vertices:
 hit,normal,idx,dist=surf.find_nearest(v.co)
 # Match deformation on the cloth web; use the nearest triangle's blended skin.
 a,b,c=triangles[idx];x=surface[b]-surface[a];y=surface[c]-surface[a];z=hit-surface[a];den=x.dot(x)*y.dot(y)-x.dot(y)**2
 u=(y.dot(y)*z.dot(x)-x.dot(y)*z.dot(y))/den if abs(den)>1e-15 else 0;vv=(x.dot(x)*z.dot(y)-x.dot(y)*z.dot(x))/den if abs(den)>1e-15 else 0
 weights={}
 for ii,factor in [(a,1-u-vv),(b,u),(c,vv)]:
  for group in shell.data.vertices[ii].groups:
   name=shell.vertex_groups[group.group].name;weights[name]=weights.get(name,0)+max(0,factor)*group.weight
 assign(o,v.index,weights)
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);shell.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.join()
selected=cloth
assert len(bpy.context.scene.objects)==count
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f13-hand-anatomy.blend'),compress=True)
spec=importlib.util.spec_from_file_location('vector_build',ROOT/'scripts/vector_fighter/build.py');g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g);g.rig=rig
meshes=[m for m in bpy.context.scene.objects if m.type=='MESH' and m.name!='weapon:joined'];arm_parts=[m for m in meshes if ('LowerArm' in m.name or 'gloves' in m.name or ('UpperArm' in m.name and 'fabric' in m.name))];body_parts=[m for m in meshes if m not in arm_parts]
def join(items,name):
 bpy.ops.object.select_all(action='DESELECT')
 for item in items:item.select_set(True)
 bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();items[0].name=name;return items[0]
arms=join(arm_parts,'VECTOR world forearms');body=join(body_parts,'VECTOR body');equipment=[m for m in bpy.context.scene.objects if m.type!='MESH' or m.name=='weapon:joined']
g.export(OUT/'candidate-body.glb',equipment+[arms,body]);arms.name='vector-armored-hands';g.export(OUT/'candidate-arms.glb',equipment+[arms])
(OUT/'anatomy-report.json').write_text(json.dumps({'source':'f11-fps-forms.blend','changed_glove_vertices':changed,'replaced_cloth_vertices':len(selected),'removed_stitch_vertices':len(seams),'objects':count,'bones_and_actions':'unchanged','status':'candidate, requires native contact review'},indent=2)+'\n')
