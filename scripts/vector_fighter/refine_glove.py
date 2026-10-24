"""Repair finger-chain skin and existing stitching in the preserved F7 source.
No objects, bones, overlays or gameplay changes. Exports diagnostic GLBs first.
"""
import bpy,bmesh,json,importlib.util
from pathlib import Path
from mathutils.bvhtree import BVHTree
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'.local/vector-f8';OUT.mkdir(exist_ok=True,parents=True)
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f7-pose-repair.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
initial=len(bpy.context.scene.objects);report={'cloth_vertices':0,'stitch_vertices':0}
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
def assign(o,i,ws):
 for g in o.vertex_groups:g.remove([i])
 total=sum(ws.values())
 for n,w in ws.items():
  if w>0:(o.vertex_groups.get(n) or o.vertex_groups.new(name=n)).add([i],w/total,'REPLACE')
for o in [o for o in bpy.context.scene.objects if 'gloves' in o.name]:
 bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bm.verts.index_update();islands=parts(bm)
 cloth={v.index for p in islands if len(p)>1000 for v in p};names={g.index:g.name for g in o.vertex_groups}
 for i in sorted(cloth):
  v=o.data.vertices[i];point=rig.matrix_world.inverted()@o.matrix_world@v.co;old={names[g.group]:g.weight for g in v.groups if g.weight>0}
  totals={side+digit:sum(w for n,w in old.items() if n.startswith(side+digit)) for side in ['Left','Right'] for digit in ['Thumb','Index','Middle','Ring','Little']}
  digit=max(totals,key=totals.get)
  if totals[digit]<.5:continue
  chain=[rig.data.bones[digit+s] for s in ['Proximal','Intermediate','Distal']]
  def project(b):
   axis=b.tail_local-b.head_local;t=(point-b.head_local).dot(axis)/axis.length_squared
   return ((point-(b.head_local+axis*max(0,min(1,t)))).length,t)
  j=min(range(3),key=lambda j:project(chain[j])[0]);t=project(chain[j])[1]
  ws={chain[j].name:1.}
  # Each skin joint blends only its two anatomical neighbours. Distant digits
  # must not pull a fingertip or stitch when they spread independently.
  if t<.2:
   f=max(0,min(1,(t+.2)/.4));ws={chain[j].name:f,(chain[j-1].name if j else ('Left' if digit.startswith('Left') else 'Right')+'Hand'):1-f}
  elif t>.8 and j<2:
   f=max(0,min(1,(t-.8)/.4));ws={chain[j].name:1-f,chain[j+1].name:f}
  # Fade into unchanged palm webbing at the root, rather than a hard split.
  root=chain[0];axis=(root.tail_local-root.head_local).normalized();advance=(point-root.head_local).dot(axis)
  blend=max(0,min(1,(advance+.005)/.016))
  for n,w in old.items():ws[n]=ws.get(n,0)*blend+(1-blend)*w
  for n in list(ws):
   if n not in old:ws[n]*=blend
  assign(o,i,dict(sorted(ws.items(),key=lambda p:-p[1])[:4]));report['cloth_vertices']+=1
 # Transfer from repaired cloth using barycentric coordinates and conform the
 # already present seam islands to the same surface; retain all mesh islands.
 o.data.calc_loop_triangles();triangles=[tuple(t.vertices) for t in o.data.loop_triangles if all(i in cloth for i in t.vertices)]
 vertices=[o.matrix_world@v.co for v in o.data.vertices];tree=BVHTree.FromPolygons(vertices,triangles,all_triangles=True)
 # Finger layers keep their original segment bindings.
 palm=[]
 for part in islands:
  if len(part)!=140:continue
  bone=max(o.data.vertices[part[0].index].groups,key=lambda g:g.weight)
  name=o.vertex_groups[bone.group].name
  center=o.matrix_world@(sum((v.co for v in part),Vector())/len(part))
  material=o.material_slots[next(iter(part[0].link_faces)).material_index].name
  if 'Hand' in name:
   palm.append((part,name,center,material));continue
  hit,normal,idx,dist=tree.find_nearest(center)
  clearance=.0022 if 'ceramic' in material else .0007
  shift=normal*(clearance-(center-hit).dot(normal))
  for v in part:o.data.vertices[v.index].co+=o.matrix_world.inverted().to_3x3()@shift
  report['existing_plates_seated']=report.get('existing_plates_seated',0)+1
 # Each existing dorsal rim+ceramic pair is one authored assembly. Keep its
 # layer spacing, rigid Hand weights and contour; fit its footprint inside the
 # actual glove instead of deforming its vertices over cloth triangles.
 for side in ['Left','Right']:
  w=rig.data.bones[side+'Hand'].head_local
  end=sum((rig.data.bones[side+d+'Proximal'].head_local for d in ['Index','Middle','Ring','Little']),Vector())/4
  axis=(end-w).normalized();dorsal=Vector((1 if side=='Left' else -1,0,1)).normalized()
  normal=(dorsal-axis*axis.dot(dorsal)).normalized();lateral=axis.cross(normal).normalized()
  axis=rig.matrix_world.to_3x3()@axis;normal=rig.matrix_world.to_3x3()@normal;lateral=rig.matrix_world.to_3x3()@lateral
  layers=[p for p in palm if p[1]==side+'Hand']
  for backing in [p for p in layers if 'ceramic' not in p[3]]:
   coating=min((p for p in layers if 'ceramic' in p[3]),key=lambda p:(p[2]-backing[2]).length)
   center=backing[2];hand_center=rig.matrix_world@w
   side_offset=(center-hand_center).dot(lateral)
   shift_side=-lateral*(.007 if side_offset>0 else -.007)
   fit_center=center+shift_side
   hit,hn,idx,distance=tree.ray_cast(fit_center+normal*.05,-normal,.15)
   assert hit is not None,(side,tuple(fit_center))
   shift=shift_side+normal*((hit-fit_center).dot(normal)+.002)
   for layer in [backing,coating]:
    for v in layer[0]:
     point=o.matrix_world@v.co;relative=point-center
     point-=lateral*relative.dot(lateral)*.25+axis*relative.dot(axis)*.10
     o.data.vertices[v.index].co=o.matrix_world.inverted()@(point+shift)
    report['palm_plate_layers_seated']=report.get('palm_plate_layers_seated',0)+1
   report.setdefault('palm_assembly_travel_m',[]).append(shift.length)
 for part in islands:
  if len(part)>50:continue
  clearance=.00045
  for b in part:
   v=o.data.vertices[b.index];hit,normal,idx,dist=tree.find_nearest(o.matrix_world@v.co)
   if dist>.015:continue
   a,c,d=triangles[idx];v0=vertices[c]-vertices[a];v1=vertices[d]-vertices[a];v2=hit-vertices[a]
   den=v0.dot(v0)*v1.dot(v1)-v0.dot(v1)**2
   if abs(den)<1e-15:continue
   u=(v1.dot(v1)*v2.dot(v0)-v0.dot(v1)*v2.dot(v1))/den;w=(v0.dot(v0)*v2.dot(v1)-v0.dot(v1)*v2.dot(v0))/den
   weights={}
   for index,f in [(a,1-u-w),(c,u),(d,w)]:
    for g in o.data.vertices[index].groups:weights[o.vertex_groups[g.group].name]=weights.get(o.vertex_groups[g.group].name,0)+max(0,f)*g.weight
   assign(o,v.index,dict(sorted(weights.items(),key=lambda p:-p[1])[:4]))
   v.co=o.matrix_world.inverted()@(hit+normal*clearance)
   report['stitch_vertices']+=1
 bm.free();o.data.update()
report.update(objects_before=initial,objects_after=len(bpy.context.scene.objects));assert initial==len(bpy.context.scene.objects)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f8-glove-repair.blend'),compress=True)
spec=importlib.util.spec_from_file_location('vector_build',ROOT/'scripts/vector_fighter/build.py');g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g);g.rig=rig
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='weapon:joined'];arm_parts=[o for o in meshes if ('LowerArm' in o.name or 'gloves' in o.name or ('UpperArm' in o.name and 'fabric' in o.name))];body_parts=[o for o in meshes if o not in arm_parts]
def join(items,name):
 bpy.ops.object.select_all(action='DESELECT')
 for o in items:o.select_set(True)
 bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();o=items[0];o.name=name;return o
arms=join(arm_parts,'VECTOR world forearms');body=join(body_parts,'VECTOR body');equipment=[o for o in bpy.context.scene.objects if o.type!='MESH' or o.name=='weapon:joined']
g.export(OUT/'candidate-body.glb',equipment+[arms,body]);arms.name='vector-armored-hands';g.export(OUT/'candidate-arms.glb',equipment+[arms]);(OUT/'glove-report.json').write_text(json.dumps(report,indent=2)+'\n');print('F8_GLOVE',report)
