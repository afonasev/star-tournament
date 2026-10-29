"""B01 export and separate bounded optimization pass; never alters F02 or gloves."""
import bpy,bmesh,sys,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(Path(__file__).parent))
import optimize_runtime as opt
import author_assault
from export_heavy_agile import retain_current_weapon
from reconstruct_body import TARGET,OUT
from optimize_f23_world import surface,error

opt.SOURCE=TARGET;author_assault.palette=lambda:None
original_optimize=opt.optimize

def trim_covered_textile(o):
    if 'LowerArm anatomical textile' not in o.name:return 0
    bone=next(g.name for g in o.vertex_groups);rig=next(m.object for m in o.modifiers if m.type=='ARMATURE')
    start=rig.data.bones[bone].head_local;end=rig.data.bones[bone.replace('LowerArm','Hand')].head_local;axis=(end-start).normalized()
    occluders=[x for x in bpy.context.scene.objects if x.type=='MESH' and bone+' vambrace' in x.name]
    v=[];f=[]
    for x in occluders:
        shift=len(v);v.extend(x.matrix_world@p.co for p in x.data.vertices);x.data.calc_loop_triangles()
        f.extend(tuple(i+shift for i in t.vertices) for t in x.data.loop_triangles)
    tree=BVHTree.FromPolygons(v,f,all_triangles=True)
    def covered(p):
        center=start+axis*(p-start).dot(axis);direction=(p-center).normalized()
        hit=tree.ray_cast(p+direction*.00005,direction,.04)
        return hit[0] is not None
    bm=bmesh.new();bm.from_mesh(o.data);doomed=[]
    for face in bm.faces:
        points=[x.co for x in face.verts]
        samples=points+[(points[i]+points[(i+1)%len(points)])/2 for i in range(len(points))]+[face.calc_center_median()]
        if all(covered(p) for p in samples):doomed.append(face)
    count=len(doomed);bmesh.ops.delete(bm,geom=doomed,context='FACES');bm.to_mesh(o.data);bm.free()
    return count

rows=[]
def reduce(o,ratio):
    before=opt.triangles(o)
    if not o.get('body_reconstruction')=='B01' or before<400:
        return {'object':o.name,'before':before,'after':before,'strategy':'preserved exact'}
    removed=trim_covered_textile(o)
    reference=surface(o.data);backup=o.data.copy()
    # Reducer works only on newly authored surfaces; rollback per object if the
    # bidirectional bound exceeds a submillimetre asset tolerance.
    original_optimize(o,.35 if 'LowerArm' in o.name else .65)
    deviation=error(reference,o.data)
    accepted=max(deviation['max'],deviation['reverse_max'])<=.0007
    if not accepted:o.data=backup
    return {'object':o.name,'before':before,'after':opt.triangles(o),'accepted':accepted,'deviation_m':deviation,'hidden_same_bone_textile_faces_removed':removed}

opt.optimize=lambda o,r:{'object':o.name,'before':opt.triangles(o),'after':opt.triangles(o)}
opt.export('world','balanced',OUT/'high-body.glb')
retain_current_weapon(OUT/'high-body.glb',ROOT/'unity/Assets/StarTournament/Art/trooper-body-animated.glb')
opt.optimize=reduce
rows=opt.export('world','balanced',OUT/'candidate-body.glb')
retain_current_weapon(OUT/'candidate-body.glb',ROOT/'unity/Assets/StarTournament/Art/trooper-body-animated.glb')
(OUT/'optimization.json').write_text(json.dumps({'method':'new B01 surfaces only; source and protected parts exact; 0.7mm bidirectional sampled bound; material and texture maps unchanged','before':sum(r['before'] for r in rows),'after':sum(r['after'] for r in rows),'objects':rows,'draw_calls':'merged by existing material contract; two body skins unchanged','shadows':'existing runtime shadow contract unchanged','LOD':'single existing world topology; no new distance switching','performance':'NotRun; geometry/resource comparison only'},indent=2))
