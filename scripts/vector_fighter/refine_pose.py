"""Repair existing VECTOR study surfaces and skin. No new costume objects.
Source remains editable; diagnostic export goes to .local/vector-f7.
"""
import bpy,bmesh,sys,math,json,importlib.util
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'.local/vector-f7';OUT.mkdir(exist_ok=True,parents=True)
spec=importlib.util.spec_from_file_location('vector_build',ROOT/'scripts/vector_fighter/build.py');g=importlib.util.module_from_spec(spec);sys.modules['vector_build']=g;spec.loader.exec_module(g)
sys.argv.extend(['--candidate','--forms','remodel','--source-only','--pose-base']);g.build()
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
for track in rig.animation_data.nla_tracks:track.mute=True
rig.animation_data.action=None
for b in rig.pose.bones:b.matrix_basis.identity()
bpy.context.view_layer.update()
report={};initial_objects=len(bpy.context.scene.objects)
def components(bm):
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

def weights_near(mesh,point):
    tree,triangles,verts,weights=mesh
    hit,normal,index,distance=tree.find_nearest(point)
    a,b,c=triangles[index];v0=verts[b]-verts[a];v1=verts[c]-verts[a];v2=hit-verts[a]
    d00=v0.dot(v0);d01=v0.dot(v1);d11=v1.dot(v1);den=d00*d11-d01*d01
    if abs(den)<1e-15:return weights[a],hit,normal,distance
    v=(d11*v2.dot(v0)-d01*v2.dot(v1))/den;w=(d00*v2.dot(v1)-d01*v2.dot(v0))/den
    ws={}
    for i,f in [(a,1-v-w),(b,v),(c,w)]:
        for name,weight in weights[i].items():ws[name]=ws.get(name,0)+max(0,f)*weight
    ws=dict(sorted(ws.items(),key=lambda p:p[1],reverse=True)[:4]);total=sum(ws.values());ws={k:v/total for k,v in ws.items() if v>0}
    return ws,hit,normal,distance

def surface(objects):
    verts=[];triangles=[];weights=[]
    for o in objects:
        offset=len(verts);o.data.calc_loop_triangles();verts.extend(o.matrix_world@v.co for v in o.data.vertices)
        triangles.extend(tuple(offset+i for i in t.vertices) for t in o.data.loop_triangles)
        names={g.index:g.name for g in o.vertex_groups}
        weights.extend({names[g.group]:g.weight for g in v.groups if g.weight>0} for v in o.data.vertices)
    return BVHTree.FromPolygons(verts,triangles,all_triangles=True),triangles,verts,weights

def set_weights(o,index,ws):
    for g in o.vertex_groups:g.remove([index])
    for name,w in ws.items():
        group=o.vertex_groups.get(name) or o.vertex_groups.new(name=name);group.add([index],w,'REPLACE')

for o in list(bpy.context.scene.objects):
    if o.type!='MESH' or 'gloves' not in o.name:continue
    bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6);bm.to_mesh(o.data);bm.free()
    bm=bmesh.new();bm.from_mesh(o.data);bm.verts.ensure_lookup_table();bm.verts.index_update();parts=components(bm)
    bodyids={v.index for part in parts if len(part)>1000 for v in part}
    # Source cloth stays intact. Only existing seam islands inherit its smooth skin;
    # manufactured finger plates retain their rigid segment weights.
    copy=o.copy();copy.data=o.data.copy();bpy.context.collection.objects.link(copy)
    sub=bmesh.new();sub.from_mesh(copy.data);sub.verts.ensure_lookup_table();bmesh.ops.delete(sub,geom=[v for v in sub.verts if v.index not in bodyids],context='VERTS');sub.to_mesh(copy.data);sub.free()
    cloth=surface([copy]);changed=0
    for part in parts:
        if len(part)>50:continue
        for v in part:
            ws,hit,normal,distance=weights_near(cloth,o.matrix_world@v.co)
            if distance<.015:set_weights(o,v.index,ws);changed+=1
    bm.free();bpy.data.objects.remove(copy,do_unlink=True);report['seam_vertices_reweighted']=changed

cloth=[o for o in bpy.context.scene.objects if o.type=='MESH' and ('fabric' in o.name or 'trousers' in o.name)]
cloth_surface=surface(cloth)
shells=[o for o in bpy.context.scene.objects if o.type=='MESH' and any(n in o.name for n in ['shell','yoke','knee shield','articulated instep']) and 'helmet' not in o.name]
# Keep armor rigid and its designed surface smooth. Conform the covered cloth
# to that surface and its bone, instead of copying cloth wrinkles onto armor.
outer=[]
for o in shells:
    if 'costal' in o.name:
        for v in o.data.vertices:v.co.z-=.018
    copy=o.copy();copy.data=o.data.copy();bpy.context.collection.objects.link(copy)
    bm=bmesh.new();bm.from_mesh(copy.data);bm.verts.ensure_lookup_table();half=len(bm.verts)//2
    bmesh.ops.delete(bm,geom=list(bm.verts)[half:],context='VERTS');bm.to_mesh(copy.data);bm.free();outer.append(copy)
buried=0
for o in cloth:
    if 'torso' in o.name:subset=[p for p in outer if not any(k in p.name for k in ['Arm','Leg','knee','instep'])]
    elif 'trousers' in o.name:subset=[p for p in outer if any(k in p.name for k in ['Leg','knee','instep','waist'])]
    else:subset=[p for p in outer if 'Arm' in p.name]
    armor_surface=surface(subset)
    for v in o.data.vertices:
        point=o.matrix_world@v.co;ws,hit,normal,distance=weights_near(armor_surface,point)
        signed=(point-hit).dot(normal)
        if distance<.055 and -.035<signed<.05:
            v.co=o.matrix_world.inverted()@(point-normal*max(0,signed+.012))
            set_weights(o,v.index,ws);buried+=1
    o.data.update()
for o in outer:bpy.data.objects.remove(o,do_unlink=True)
report['cloth_vertices_recessed']=buried;report['object_count_before']=initial_objects;report['object_count_after']=len(bpy.context.scene.objects)
assert report['object_count_before']==report['object_count_after']
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f7-pose-repair.blend'),compress=True)
spec=importlib.util.spec_from_file_location('vector_build',ROOT/'scripts/vector_fighter/build.py');g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g);g.rig=rig
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='weapon:joined']
arm_parts=[o for o in meshes if ('LowerArm' in o.name or 'gloves' in o.name or ('UpperArm' in o.name and 'fabric' in o.name))]
body_parts=[o for o in meshes if o not in arm_parts]
def join(items,name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in items:o.select_set(True)
    bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();o=items[0];o.name=name;return o
arms=join(arm_parts,'VECTOR world forearms');body=join(body_parts,'VECTOR body')
equipment=[o for o in bpy.context.scene.objects if o.type!='MESH' or o.name=='weapon:joined']
g.export(OUT/'candidate-body.glb',equipment+[arms,body]);arms.name='vector-armored-hands';g.export(OUT/'candidate-arms.glb',equipment+[arms])
(OUT/'repair-report.json').write_text(json.dumps(report,indent=2)+'\n');print('F7_REPAIR',report)
