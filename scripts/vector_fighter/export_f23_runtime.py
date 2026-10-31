"""Experimental F23 reduction; rejected candidates, NOT the promoted runtime asset. Preserve unreduced source, rig, FPS and equipment.
Dissolve redundant tessellation then simplify per surface role, retaining all objects.
Explicit --promote updates the world GLB and its two hash records only.
"""
import bpy,bmesh,sys,json,hashlib,shutil,math
from pathlib import Path
from mathutils.bvhtree import BVHTree
ROOT=Path(__file__).resolve().parents[2];sys.path.insert(0,str(Path(__file__).parent))
import optimize_runtime as opt
import author_assault
from export_heavy_agile import retain_current_weapon
OUT=ROOT/'.local/vector-f23-runtime';OUT.mkdir(parents=True,exist_ok=True)
SOURCE=ROOT/'scripts/vector_fighter/f23-color-coverage.blend'

def simplify(obj,unused):
    original_count=opt.triangles(obj)
    # Backing faces are wholly internal to the solid costume. Keep the exterior
    # and complete visible rim; source stays closed and unreduced.
    hidden=0
    if obj.get('f22_closed_armor'):
        bm=bmesh.new();bm.from_mesh(obj.data);back=[f for f in bm.faces if f.material_index==2];hidden=len(back)
        bmesh.ops.delete(bm,geom=back,context='FACES');bm.to_mesh(obj.data);bm.free()
    points=[v.co.copy() for v in obj.data.vertices]
    reference=obj.copy();reference.data=obj.data.copy();bpy.context.collection.objects.link(reference)
    for m in list(reference.modifiers):reference.modifiers.remove(m)
    name=obj.name
    fabric=any(s in name for s in ['fabric','garment','textile','gusset','woven','sewn boot'])
    gloves='gloves' in name
    # Ratios are offline export choices; no authored source or gameplay value changes.
    target=max(12,round(original_count*(.19 if gloves else .035 if fabric else .38)))
    if not gloves and not fabric and original_count>200:
        target=(190 if any(obj.vertex_groups[g.group].name=='Head' and g.weight>.5 for g in obj.data.vertices[0].groups) else 130) if obj.get('f22_closed_armor') else 55
    if 'perimeter wear' in name or ('--analytic' in sys.argv and obj.get('f22_closed_armor')):target=original_count
    if original_count>200 and not fabric and not gloves and '--analytic' not in sys.argv:
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        mod=obj.modifiers.new('Dissolve redundant planar grid','DECIMATE');mod.decimate_type='DISSOLVE';mod.angle_limit=.006;mod.delimit={'MATERIAL'}
        bpy.ops.object.modifier_apply(modifier=mod.name)
    after_planar=opt.triangles(obj)
    if after_planar>target:
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        mod=obj.modifiers.new('Stable triangulation before collapse','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
        mod=obj.modifiers.new('Runtime surface reduction','DECIMATE');mod.decimate_type='COLLAPSE';mod.ratio=target/opt.triangles(obj);mod.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # Transfer authored split normals after simplification, including flat facets.
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new('Preserve source surface normals','DATA_TRANSFER');mod.object=reference;mod.use_loop_data=True;mod.data_types_loops={'CUSTOM_NORMAL'};mod.loop_mapping='POLYINTERP_NEAREST'
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(reference,do_unlink=True)
    mesh=obj.data;mesh.calc_loop_triangles()
    tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[tuple(t.vertices) for t in mesh.loop_triangles],all_triangles=True)
    errors=sorted(tree.find_nearest(v)[3] for v in points)
    return {'object':name,'role':'gloves' if gloves else 'fabric' if fabric else 'armor/hardware','before':original_count,'hidden_back_faces_removed':hidden,'after_planar':after_planar,'after':len(mesh.loop_triangles),'surface_error_m':{'p95':errors[int((len(errors)-1)*.95)],'max':errors[-1]}}

def main():
    opt.SOURCE=OUT/'analytic-runtime.blend' if '--analytic' in sys.argv else SOURCE;opt.optimize=simplify_wrapper
    author_assault.palette=lambda:None
    body=ROOT/'unity/Assets/StarTournament/Art/trooper-body-animated.glb'
    pinned=OUT/'equipment-baseline.glb'
    if not pinned.exists():shutil.copy2(body,pinned)
    rows=opt.export('world','balanced',OUT/'candidate-body.glb')
    retain_current_weapon(OUT/'candidate-body.glb',pinned)
    report={'source':str(SOURCE.relative_to(ROOT)),'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'before_triangles':sum(x['before'] for x in rows),'after_triangles':sum(x['after'] for x in rows),'objects':rows,'strategy':'Dissolve redundant panel tessellation, bounded role reduction, existing material slots and identity boundaries; no objects removed','fps':'byte unchanged','weapon':'exact current baseline'}
    (OUT/'optimization.json').write_text(json.dumps(report,indent=2)+'\n')
    if '--promote' in sys.argv:
        assert report['after_triangles']<=32989,report['after_triangles']
        shutil.copy2(OUT/'candidate-body.glb',body);sha=hashlib.sha256(body.read_bytes()).hexdigest()
        m=body.parent/'asset-manifest.json';d=json.loads(m.read_text());entry=next(e for e in d['entries'] if e['key']=='player-body');entry['sha256']=sha;entry['source']='VECTOR F23 optimized world; scripts/vector_fighter/f23-color-coverage.blend + export_f23_runtime.py';m.write_text(json.dumps(d,indent=2)+'\n')
        m=body.parent/'trooper-mounts.json';d=json.loads(m.read_text());d['body']['sha256']=sha;m.write_text(json.dumps(d,indent=2)+'\n')
    print('F23_RUNTIME',report['before_triangles'],report['after_triangles'])

original_optimize=opt.optimize
# Keep the existing reducer available inside the custom pre-pass without recursion.
def simplify_wrapper(obj,ratio):
    opt.optimize=original_optimize
    try:return simplify(obj,ratio)
    finally:opt.optimize=simplify_wrapper
if __name__=='__main__':main()
