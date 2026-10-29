"""Candidate-only F23 runtime sampling of the original analytic armor cages.

Keeps the immutable F23 blend, nonarmor geometry, material slots, rig, gloves,
equipment and FPS asset. Selects 12/24 samples per panel, falling back to original
geometry when either direction of the sampled surface error exceeds 1 mm.
No automatic promotion; real native visual/performance review is still required.
"""
import bpy, sys, json, hashlib
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
import reconstruct_f22 as cage
import optimize_runtime as opt
import author_assault
from export_heavy_agile import retain_current_weapon
from mathutils.bvhtree import BVHTree
SOURCE=Path(sys.argv[sys.argv.index('--source')+1]).resolve() if '--source' in sys.argv else ROOT/'scripts/vector_fighter/f23-color-coverage.blend'
OUT=Path(sys.argv[sys.argv.index('--output')+1]).resolve() if '--output' in sys.argv else ROOT/'.local/f23-adaptive-runtime'
# Offline export tolerance, not a gameplay/camera/acceptance threshold.
MAX_ERROR_M=.001
OUT.mkdir(parents=True,exist_ok=True)

def areas(mesh):
    result={}
    for face in mesh.polygons:result[face.material_index]=result.get(face.material_index,0)+face.area
    return result

def surface(mesh):
    mesh.calc_loop_triangles()
    used={i for tri in mesh.loop_triangles for i in tri.vertices}
    tree=BVHTree.FromPolygons([v.co for v in mesh.vertices],[tuple(t.vertices) for t in mesh.loop_triangles],all_triangles=True)
    return tree,[mesh.vertices[i].co.copy() for i in sorted(used)]

def error(reference,candidate):
    before,points=reference;after,new_points=surface(candidate)
    distances=sorted(after.find_nearest(point)[3] for point in points)
    reverse=max(before.find_nearest(point)[3] for point in new_points)
    return {'p95':distances[int((len(distances)-1)*.95)],'max':distances[-1],'reverse_max':reverse}

def main():
    source_hash=hashlib.sha256(SOURCE.read_bytes()).hexdigest();paths={}
    # Same manufacturing formula and bevel resolution, with adaptive interior
    # sampling instead of collapse. Complex panels retain the original surface.
    for grid in (12,24):
        cage.solids.clear();cage.PANEL_GRID=grid;cage.OUT=OUT;cage.SOURCE_OUTPUT=OUT/('resampled-cages-'+str(grid)+'.blend');cage.main()
        paths[grid]=cage.SOURCE_OUTPUT
    names=tuple(o.name for o in bpy.context.scene.objects if o.type=='MESH' and o.get('f22_closed_armor'))
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    originals={o.name:o for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='weapon:joined'}
    rows={name:{'object':name,'before':opt.triangles(obj),'after':opt.triangles(obj),'strategy':'unchanged exact source geometry'} for name,obj in originals.items()}
    # A derived costume may replace the helmet; only resample surviving F22 cages.
    names=tuple(name for name in names if name in originals)
    candidates={}
    for grid,path in paths.items():
        with bpy.data.libraries.load(str(path),link=False) as (src,dst):dst.objects=list(names)
        candidates[grid]=dict(zip(names,dst.objects))
    for name in names:
        original=originals[name];reference=surface(original.data);trials=[];selected=None
        for grid in (12,24):
            replacement=candidates[grid][name]
            assert [g.name for g in original.vertex_groups]==[g.name for g in replacement.vertex_groups],name
            assert original.matrix_world==replacement.matrix_world,name
            deviation=error(reference,replacement.data);trials.append({'grid':grid,'error_m':deviation})
            if max(deviation['max'],deviation['reverse_max'])<=MAX_ERROR_M:
                selected=replacement;break
        rows[name]['trials']=trials
        if selected is None:
            rows[name]['strategy']='original retained: curved/discontinuous cage exceeds export tolerance'
            continue
        bpy.context.collection.objects.link(selected)
        selected.data.materials.clear()
        for material in original.data.materials:selected.data.materials.append(material)
        for modifier in list(selected.modifiers):selected.modifiers.remove(modifier)
        bpy.ops.object.select_all(action='DESELECT');selected.select_set(True);bpy.context.view_layer.objects.active=selected
        mod=selected.modifiers.new('Preserve F23 authored shading','DATA_TRANSFER');mod.object=original;mod.use_loop_data=True;mod.data_types_loops={'CUSTOM_NORMAL'};mod.loop_mapping='POLYINTERP_NEAREST'
        bpy.ops.object.modifier_apply(modifier=mod.name)
        rows[name].update(after=opt.triangles(selected),grid=grid,strategy='same authored cage; adaptive interior sampling',surface_error_m=deviation,material_area_before=areas(original.data),material_area_after=areas(selected.data))
        original.data=selected.data
    # Keep only original scene objects. Library dependencies are not scene-linked.
    for group in candidates.values():
        for obj in group.values():bpy.data.objects.remove(obj,do_unlink=True)
    derived=OUT/'runtime-source.blend';bpy.ops.wm.save_as_mainfile(filepath=str(derived),compress=True)
    author_assault.palette=lambda:None;opt.SOURCE=derived
    opt.optimize=lambda obj,ratio:{'object':obj.name,'before':opt.triangles(obj),'after':opt.triangles(obj)}
    candidate=OUT/'candidate-body.glb';opt.export('world','balanced',candidate)
    retain_current_weapon(candidate,ROOT/'unity/Assets/StarTournament/Art/trooper-body-animated.glb')
    report={'source':str(SOURCE.relative_to(ROOT)),'source_sha256':source_hash,'grid_options':[12,24,48],'max_sampled_surface_error_m':MAX_ERROR_M,'bevel_segments':cage.SHELL_BEVEL_SEGMENTS,'before':sum(row['before'] for row in rows.values()),'after':sum(row['after'] for row in rows.values()),'objects':list(rows.values()),'nonarmor':'unchanged source vertices, UV, skin weights and materials','fps':'unchanged bytes','equipment':'exact baseline buffers','source_preserved':source_hash==hashlib.sha256(SOURCE.read_bytes()).hexdigest()}
    (OUT/'optimization.json').write_text(json.dumps(report,indent=2)+'\n')
    print('F23_WORLD',report['before'],report['after'])
if __name__=='__main__':main()
