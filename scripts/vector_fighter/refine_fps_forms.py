"""F11 selected broad ceramic / tailored textile; reproducible F9-source edit.
Replaces existing forearm and cuff surfaces, never adds costume overlays.
Exports candidates only. Rig/actions, camera and weapons are not authored here.
"""
import bpy,bmesh,math,json,importlib.util
import numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'.local/vector-f11';OUT.mkdir(exist_ok=True,parents=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f9-fp-detail.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
initial_objects=len(bpy.context.scene.objects)
def parts(bm):
 left=set(bm.verts);result=[]
 while left:
  todo=[left.pop()];part=[]
  while todo:
   v=todo.pop();part.append(v)
   for e in v.link_edges:
    n=e.other_vert(v)
    if n in left:left.remove(n);todo.append(n)
  result.append(part)
 return result

def frame(side):
 a=rig.data.bones[side+'LowerArm'].head_local.copy();b=rig.data.bones[side+'Hand'].head_local.copy()
 axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized()
 return a,b,axis,front,axis.cross(front).normalized()
def set_weights(o,i,ws):
 for g in o.vertex_groups:g.remove([i])
 for n,w in ws.items():
  if w>0:(o.vertex_groups.get(n) or o.vertex_groups.new(name=n)).add([i],w,'REPLACE')
def mesh_replace(o,v,f,weights,uvs,materials):
 old=o.data;mesh=bpy.data.meshes.new(o.name+' F11 surface');mesh.from_pydata(v,[],f);mesh.update();o.data=mesh
 for m in materials:mesh.materials.append(m)
 uv=mesh.uv_layers.new(name='UVMap')
 for loop in mesh.loops:uv.data[loop.index].uv=uvs[loop.vertex_index]
 for i,w in enumerate(weights):set_weights(o,i,w)
 for p in mesh.polygons:p.use_smooth=True

def radius(t):
 # Authored anatomical taper: quiet elbow, brachioradialis belly, fitted wrist.
 t=max(0,min(1,t));return .030+.013*(1-t)+.005*math.sin(math.pi*t),.032+.014*(1-t)+.005*math.sin(math.pi*t)

# Continuous wide panel: explicit rounded rectangular outline, crown and rolled
# edge. The old sharp eight-sided perimeter is replaced, not merely subdivided.
for side,sign in [('Left',1),('Right',-1)]:
 o=bpy.data.objects['VECTOR '+side+'LowerArm contoured shell'];a,b,axis,front,lat=frame(side)
 v=[];f=[];uv=[];ws=[];N=96
 rings=[(.015,0),(.16,0),(.32,0),(.48,0),(.64,0),(.79,0),(.90,0),(.96,0),(.985,-.00035),(1,-.0015)]
 for scale,drop in rings:
  for i in range(N):
   q=2*math.pi*i/N;c=math.cos(q);s=math.sin(q)
   angle=1.12*math.copysign(abs(c)**.55,c)*scale
   t=.49+.405*math.copysign(abs(s)**.55,s)*scale
   rx,ry=radius(t);theta=angle-.12*sign
   v.append(a.lerp(b,t)+lat*((rx+.006)*math.sin(theta))+front*((ry+.009)*math.cos(theta)+.003*math.sin(math.pi*t)*(1-(angle/1.2)**2)+drop))
   uv.append(((angle*sign+1.5)/3,t));ws.append({side+'LowerArm':1})
 f.append(tuple(reversed(range(N))))
 for j in range(len(rings)-1):
  for i in range(N):k=j*N+i;l=j*N+(i+1)%N;f.append((k,l,l+N,k+N))
 mesh_replace(o,v,f,ws,uv,[o.data.materials[0]])
 # Consistent outward normals and inward thickness make a rounded closed edge.
 if sum((p.normal*p.area for p in o.data.polygons),Vector()).dot(front)<0:o.data.flip_normals()
 bpy.context.view_layer.objects.active=o
 skin=o.modifiers.get('Skin')
 if skin:o.modifiers.remove(skin)
 mod=o.modifiers.new('Ceramic inward thickness','SOLIDIFY');mod.thickness=.003;mod.offset=-1;bpy.ops.object.modifier_apply(modifier=mod.name)
 mod=o.modifiers.new('Skin','ARMATURE');mod.object=rig

# Keep original upper sleeve; replace its two low-poly elbow/forearm islands
# with one fitted quad surface per side. No extra sleeve or floating cuff.
o=bpy.data.objects['VECTOR tailored UpperArm fabric'];bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table()
remove=[v for p in parts(bm) if len(p) in (131,167) for v in p]
assert len(remove)==596
bmesh.ops.delete(bm,geom=remove,context='VERTS');bm.to_mesh(o.data);bm.free()
# The retained upper sleeve also enters the near plane. Give its authored
# garment contour a smooth limit surface before joining the new lower sleeve.
bpy.context.view_layer.objects.active=o
mod=o.modifiers.new('Tailored upper sleeve limit surface','SUBSURF');mod.levels=2
bpy.ops.object.modifier_apply(modifier=mod.name)
verts=[v.co.copy() for v in o.data.vertices];faces=[tuple(p.vertices) for p in o.data.polygons]
weights=[{o.vertex_groups[g.group].name:g.weight for g in v.groups} for v in o.data.vertices]
uvs=[]
for v in verts:
 side='Left' if v.x>0 else 'Right';bone=rig.data.bones[side+'UpperArm'];a=bone.head_local;b=rig.data.bones[side+'LowerArm'].head_local
 axis=(b-a).normalized();front=Vector((0,-1,0));front=(front-axis*front.dot(axis)).normalized();lat=axis.cross(front)
 t=(v-a).dot(axis)/(b-a).length;rel=v-a-axis*(v-a).dot(axis)
 uvs.append((math.atan2(rel.dot(lat),rel.dot(front))/(4*math.pi),t*.5))
for side in ['Left','Right']:
 a,b,axis,front,lat=frame(side);start=len(verts);N=64;M=48
 for j in range(M):
  t=-.28+1.305*j/(M-1);rx,ry=radius(t)
  seat=max(0,min(1,(t-.94)/.06));rx-=.0015*seat;ry-=.0015*seat
  # Subtle longitudinal gusset, no concentric metal rings.
  for i in range(N):
   angle=2*math.pi*i/N
   fold=.0009*math.sin(t*27+2*angle)*math.exp(-((t-.03)/.22)**2)
   p=a.lerp(b,t)+lat*((rx+fold)*math.sin(angle))+front*((ry+fold)*math.cos(angle))
   verts.append(p);uvs.append((i/N*.5,t*.5))
   upper=max(0,min(1,(.10-t)/.38));hand=max(0,min(1,(t-.88)/.18))
   weights.append({side+'UpperArm':upper,side+'LowerArm':(1-upper)*(1-hand),side+'Hand':hand})
 for j in range(M-1):
  for i in range(N):k=start+j*N+i;l=start+j*N+(i+1)%N;faces.append((k,l,l+N,k+N))
mesh_replace(o,verts,faces,weights,uvs,list(o.data.materials))
# Unwrap the periodic sleeve seam per face. Interpolating from the final
# azimuth back to zero across one quad produces a stretched tangent map.
uv=o.data.uv_layers[0]
for face in o.data.polygons:
 values=[uv.data[i].uv.x for i in face.loop_indices]
 if max(values)-min(values)>.25:
  for i in face.loop_indices:
   if uv.data[i].uv.x<max(values)-.25:uv.data[i].uv.x+=.5


# Re-seat existing glove layers onto the actual cloth and remove the old stacked
# cuff rings. Keep fingers and their established contact surface unchanged.
o=bpy.data.objects['VECTOR articulated gloves'];bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bm.verts.index_update()
islands=parts(bm);remove=[v for p in islands if len(p)==384 for v in p]
assert len(remove)==2304
for part in islands:
 if len(part)!=40:continue
 center=sum((v.co for v in part),Vector())/len(part);side='Left' if center.x>0 else 'Right'
 a,b,axis,front,lat=frame(side)
 if (center-b).dot(axis)<.04:remove.extend(part)
# The two obsolete cuff clasp patches belong to the removed ring assembly.
assert len(remove)==2384
bmesh.ops.delete(bm,geom=remove,context='VERTS');bm.to_mesh(o.data);bm.free()
bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bm.verts.index_update();islands=parts(bm)
cloth={v.index for p in islands if len(p)>1000 for v in p}
# Round only the proximal cuff of the existing cloth; fingers retain F8 vertices.
for i in cloth:
 v=o.data.vertices[i];side='Left' if v.co.x>0 else 'Right';a,b,axis,front,lat=frame(side)
 s=(v.co-b).dot(axis)
 if s>.025:continue
 center=b+axis*s;rel=v.co-center;angle=math.atan2(rel.dot(lat),rel.dot(front))
 if s<.006:s=-.004;center=b+axis*s
 fade=max(0,min(1,(.025-s)/.035));rx=.030;ry=.032
 target=center+lat*(rx*math.sin(angle))+front*(ry*math.cos(angle))
 v.co=v.co.lerp(target,fade)
 hand=max(0,min(1,(s+.025)/.04));set_weights(o,i,{side+'LowerArm':1-hand,side+'Hand':hand})
o.data.calc_loop_triangles();triangles=[tuple(t.vertices) for t in o.data.loop_triangles if all(i in cloth for i in t.vertices)]
vertices=[v.co.copy() for v in o.data.vertices];tree=BVHTree.FromPolygons(vertices,triangles,all_triangles=True)
# Seat each chassis/ceramic pair on ONE fitted surface so the independent
# regression of two layers cannot make them cross after a hand rotation.
patches={};panels=[p for p in islands if len(p)==140]
def material(part):return o.data.materials[next(iter(part[0].link_faces)).material_index].name
def center_of(part):return sum((o.data.vertices[v.index].co for v in part),Vector())/len(part)
for backing in [p for p in panels if 'chassis' in material(p)]:
 center=center_of(backing)
 coating=min((p for p in panels if 'ceramic' in material(p)),key=lambda p:(center_of(p)-center).length_squared)
 hit,normal,idx,dist=tree.find_nearest(center)
 tangent=normal.cross(Vector((0,0,1)))
 if tangent.length<.01:tangent=normal.cross(Vector((0,1,0)))
 tangent.normalize();bitangent=normal.cross(tangent);samples=[];heights=[]
 for vert in backing+coating:
  point=o.data.vertices[vert.index].co;rel=point-center;x=rel.dot(tangent);y=rel.dot(bitangent)
  h,_,_,_=tree.find_nearest(point);samples.append([1,x,y,x*x,x*y,y*y]);heights.append((h-center).dot(normal))
 matrix=np.asarray(samples);coeff=np.linalg.lstsq(matrix,np.asarray(heights),rcond=None)[0]
 coeff[0]+=max(0,float(np.max(np.asarray(heights)-matrix@coeff)))
 for part in [backing,coating]:patches[part[0].index]=(center,normal,tangent,bitangent,coeff)
new_panel_vertices=[];new_panel_faces=[];new_panel_weights=[];new_panel_materials=[]
for part in islands:
 if len(part)>1000:continue
 mat=material(part)
 if len(part)==140:
  center,normal,tangent,bitangent,coeff=patches[part[0].index]
  coords=np.asarray([[(o.data.vertices[v.index].co-center).dot(tangent),(o.data.vertices[v.index].co-center).dot(bitangent)] for v in part])
  origin=coords.mean(axis=0);_,axes=np.linalg.eigh(np.cov(coords.T));local=(coords-origin)@axes
  radii=np.max(np.abs(local),axis=0);clearance=.0015 if 'ceramic' in mat else .00035
  weights={}
  for vert in part:
   for group in o.data.vertices[vert.index].groups:
    name=o.vertex_groups[group.group].name;weights[name]=weights.get(name,0)+group.weight
  total=sum(weights.values());weights={name:weight/total for name,weight in weights.items()}
  base=len(new_panel_vertices);N=48
  rings=[(.012,.0009),(.25,.0009),(.50,.0009),(.75,.0009),(.9,.0008),(.97,.00055),(1,.0002),(1,0),(.97,0),(.9,0),(.75,0),(.50,0),(.25,0),(.012,0)]
  for scale,relief in rings:
   for i in range(N):
    angle=2*math.pi*i/N;c=math.cos(angle);sn=math.sin(angle)
    xy=origin+axes@np.asarray([math.copysign(abs(c)**.55,c)*radii[0]*scale,math.copysign(abs(sn)**.55,sn)*radii[1]*scale])
    x,y=xy;height=float(np.asarray([1,x,y,x*x,x*y,y*y])@coeff)+clearance+relief
    new_panel_vertices.append(center+tangent*float(x)+bitangent*float(y)+normal*height);new_panel_weights.append(weights)
  faces=[tuple(base+i for i in range(N))]
  for j in range(len(rings)-1):
   for i in range(N):k=base+j*N+i;l=base+j*N+(i+1)%N;faces.append((k,l,l+N,k+N))
  faces.append(tuple(reversed([base+(len(rings)-1)*N+i for i in range(N)])))
  new_panel_faces.extend(faces);new_panel_materials.extend([mat]*len(faces))
  continue
 for vert in part:
  v=o.data.vertices[vert.index];hit,normal,idx,dist=tree.find_nearest(v.co)
  if hit is None:continue
  # Follow glove skin with the same barycentric weights to prevent different
  # deformation of cloth and seated plates at bent finger joints.
  aa,bb,cc=triangles[idx];x=vertices[bb]-vertices[aa];y=vertices[cc]-vertices[aa];z=hit-vertices[aa]
  den=x.dot(x)*y.dot(y)-x.dot(y)**2
  if abs(den)<1e-15:continue
  u=(y.dot(y)*z.dot(x)-x.dot(y)*z.dot(y))/den;w=(x.dot(x)*z.dot(y)-x.dot(y)*z.dot(x))/den
  ws={}
  for ii,factor in [(aa,1-u-w),(bb,u),(cc,w)]:
   for g in o.data.vertices[ii].groups:
    n=o.vertex_groups[g.group].name;ws[n]=ws.get(n,0)+max(0,factor)*g.weight
  total=sum(ws.values());set_weights(o,v.index,{n:w/total for n,w in ws.items()})
  signed=(v.co-hit).dot(normal)
  offset=(.0015+.00045*math.tanh(signed/.001)) if 'ceramic' in mat else (.00055+.0002*math.tanh(signed/.001)) if 'chassis' in mat else .0003
  v.co=hit+normal*offset
ids={v.index for part in panels for v in part}
bm.free();o.data.update()
# Replace the old panel islands with clean closed quad patches. Their original
# triangulated bevels folded over themselves after fitting; density alone was
# insufficient. Existing pair count, material roles and bindings are retained.
bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.index in ids],context='VERTS');bm.to_mesh(o.data);bm.free()
mesh=bpy.data.meshes.new('F11 seated glove panels');mesh.from_pydata(new_panel_vertices,[],new_panel_faces);mesh.update()
replacement=bpy.data.objects.new('F11 temporary panel batch',mesh);bpy.context.collection.objects.link(replacement)
materials=list(o.data.materials)
for mat in materials:mesh.materials.append(mat)
for face,name in zip(mesh.polygons,new_panel_materials):face.material_index=next(i for i,m in enumerate(materials) if m.name==name);face.use_smooth=True
for i,weights in enumerate(new_panel_weights):set_weights(replacement,i,weights)
bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
bpy.ops.object.select_all(action='DESELECT');o.select_set(True);replacement.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.join()

# Real exportable woven PBR maps, with restrained thread highlights.
def texture(name,rgb):
 h,w=rgb.shape[:2];im=bpy.data.images.new(name,width=w,height=h);im.colorspace_settings.name='Non-Color';im.pixels.foreach_set(np.dstack((rgb,np.ones((h,w)))).astype(np.float32).ravel());im.pack();return im
n=2048;y,x=np.mgrid[0:n,0:n]/4
warp=np.sin(x*2*math.pi);weft=np.sin(y*2*math.pi);over=((np.floor(x)+np.floor(y))%2)*2-1
height=(.5+.5*over)*warp+(.5-.5*over)*weft
ny,nx=np.gradient(height);norm=np.dstack((-nx*.4,-ny*.4,np.ones_like(height)));norm/=np.linalg.norm(norm,axis=2)[...,None]
value=.19+.055*(height+1)/2
base=texture('vector-f11-graphite-weave',np.dstack((value*.87,value*.94,value)))
normal=texture('vector-f11-woven-normal',norm*.5+.5)
for mat in o.data.materials:
 if not mat:continue
 bs=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
 if not bs:continue
 if mat.name=='material.001' or mat.name.startswith('vector-glove'):
  for socket in ['Base Color','Normal','Roughness','Metallic']:
   for link in list(bs.inputs[socket].links):mat.node_tree.links.remove(link)
  bs.inputs['Roughness'].default_value=.83;bs.inputs['Metallic'].default_value=0
  t=mat.node_tree.nodes.new('ShaderNodeTexImage');t.image=base;mat.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
  t=mat.node_tree.nodes.new('ShaderNodeTexImage');t.image=normal;nm=mat.node_tree.nodes.new('ShaderNodeNormalMap');mat.node_tree.links.new(t.outputs['Color'],nm.inputs['Color']);mat.node_tree.links.new(nm.outputs[0],bs.inputs['Normal'])
 elif 'seam' in mat.name or 'chassis' in mat.name or 'titanium' in mat.name:
  for socket in ['Base Color','Metallic','Roughness']:
   for link in list(bs.inputs[socket].links):mat.node_tree.links.remove(link)
  bs.inputs['Base Color'].default_value=(.075,.085,.095,1) if 'seam' in mat.name else (.020,.025,.030,1)
  bs.inputs['Metallic'].default_value=0;bs.inputs['Roughness'].default_value=.8
# Same textile on sleeve, leaving body materials untouched.
sleeve=bpy.data.objects['VECTOR tailored UpperArm fabric'];sleeve.data.materials[0]=o.data.materials[0]
assert len(bpy.context.scene.objects)==initial_objects
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f11-fps-forms.blend'),compress=True)
spec=importlib.util.spec_from_file_location('vector_build',ROOT/'scripts/vector_fighter/build.py');g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g);g.rig=rig
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='weapon:joined'];arm_parts=[o for o in meshes if ('LowerArm' in o.name or 'gloves' in o.name or ('UpperArm' in o.name and 'fabric' in o.name))];body_parts=[o for o in meshes if o not in arm_parts]
def join(items,name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in items:o.select_set(True)
 bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();items[0].name=name;return items[0]
arms=join(arm_parts,'VECTOR world forearms');body=join(body_parts,'VECTOR body');equipment=[o for o in bpy.context.scene.objects if o.type!='MESH' or o.name=='weapon:joined']
g.export(OUT/'candidate-body.glb',equipment+[arms,body]);arms.name='vector-armored-hands';g.export(OUT/'candidate-arms.glb',equipment+[arms])
(OUT/'forms-report.json').write_text(json.dumps({'source':'f9-fp-detail.blend','objects_preserved':initial_objects,'removed_cuff_ring_vertices':2304,'removed_cuff_clasp_vertices':80,'replaced_sleeve_vertices':596,'form':'broad rounded shell; continuous tapered elbow/wrist textile; fitted glove layers','status':'candidate awaiting actual FPS'},indent=2)+'\n')
