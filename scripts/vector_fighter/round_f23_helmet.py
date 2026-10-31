"""Selected helmet 2 + world color 3. Keep F23 source and all non-head geometry.

blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/round_f23_helmet.py
The rounded shell is sampled analytically, not a subdivision of the old faceted mask.
"""
import hashlib, json, math, sys
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(Path(__file__).parent))
import build as g
import reconstruct_f22 as cage
import author_reference as r
SOURCE=ROOT/'scripts/vector_fighter/f23-color-coverage.blend'
TARGET=ROOT/'scripts/vector_fighter/f23-round-color.blend'
OUT=ROOT/'.local/f23-round'

def geometry(obj):
    h=hashlib.sha256()
    for v in obj.data.vertices:
        h.update(str((tuple(v.co),[(obj.vertex_groups[x.group].name,x.weight) for x in v.groups])).encode())
    for p in obj.data.polygons:h.update(str(tuple(p.vertices)).encode())
    h.update(str(tuple(tuple(x) for x in obj.matrix_world)).encode())
    return h.hexdigest()

def area():
    return sum(p.area for o in bpy.context.scene.objects if o.type=='MESH' and o.name!='weapon:joined' for p in o.data.polygons if o.data.materials[p.material_index].name.startswith('vector-fighter-identity'))

def surface(q):
    a,e=q;ce=math.cos(e);depth=.124 if math.cos(a)>=0 else .110
    return Vector((.105*ce*math.sin(a),.018-depth*ce*math.cos(a),1.625+.132*math.sin(e)))

def normal(q):
    a,e=q
    return Vector((math.cos(e)*math.sin(a)/.105,-math.cos(e)*math.cos(a)/.124,math.sin(e)/.132)).normalized()

def panel(name,coords,mat):
    obj=cage.shellpanel('round helmet '+name,coords,'Head',surface,normal,mat)
    obj.name=obj.name.replace('F22','F23R');obj['f23_round_helmet']=True
    return obj

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    assert hashlib.sha256(SOURCE.read_bytes()).hexdigest()=='b784f2b7cd8a87d80a7ac080325704cf5e28f67a25718bfa41d68d66db629324','Pinned F23 source'
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    g.rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');g.parts=[]
    g.rig.data.pose_position='REST'
    for key,name in [('steel','vector-reference-coated-steel'),('paint','vector-fighter-identity-reference'),('edge','vector-reference-edge-metal'),('seal','vector-reference-flexible-seals'),('glass','vector-reference-opaque-visor')]:setattr(r,key,bpy.data.materials[name])
    before_area=area();preserved={};removed=[]
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH':continue
        is_head=len(obj.vertex_groups)==1 and obj.vertex_groups[0].name=='Head'
        if is_head and 'comms' not in obj.name:
            removed.append(obj.name);bpy.data.objects.remove(obj,do_unlink=True)
        else:preserved[obj.name]=geometry(obj)
    painted=[]
    zones=('pectoral lower','sternum main','sternum articulated','costal articulated','rib wrap','abdominal center 0','abdominal center 1','oblique lamella','lumbar','thigh crown','shoulder front','shoulder back')
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH' or not obj.name.startswith('VECTOR F22 ') or 'perimeter wear' in obj.name:continue
        if any(z in obj.name for z in zones) and obj.data.materials[0]==r.steel:
            obj.data.materials[0]=r.paint;painted.append(obj.name)
    # Reuse the approved side communications hardware. The dark liner closes
    # every seam; all shell pieces carry the existing rigid Head skin weight.
    liner=g.ellipsoid('VECTOR F23R helmet liner',(0,.018,1.625),(.099,.102,.124),r.seal,'Head')
    cage.PANEL_GRID=24;cage.solids.clear()
    # Crown wedges share the same ellipsoid, so seams never create flat facets.
    for i,(a,b) in enumerate([(-math.pi,-1.8),(-1.8,-.72),(-.72,.72),(.72,1.8),(1.8,math.pi)]):
        panel('crown '+str(i),[(a,.46),(b,.46),(b,1.56),(a,1.56)],r.paint)
    # Wide one-piece smoked visor, curved horizontally and vertically.
    panel('visor',[(-1.22,.42),(1.22,.42),(1.19,.05),(.82,-.035),(0,-.075),(-.82,-.035),(-1.19,.05)],r.glass)
    # Rounded lower face remains compact; side cheeks rise toward ear hardware.
    for s in [-1,1]:
        panel('jaw '+str(s),[(s*.012,-.105),(s*.80,-.075),(s*1.20,.025),(s*1.52,-.18),(s*1.24,-.62),(s*.65,-.86),(s*.012,-.88)],r.steel)
        panel('temple '+str(s),[(s*1.25,.43),(s*1.78,.43),(s*1.92,.12),(s*1.65,-.18),(s*1.24,.035)],r.paint)
        panel('rear upper '+str(s),[(s*1.83,.43),(s*3.13,.43),(s*3.13,-.18),(s*2.32,-.24),(s*1.88,-.10)],r.paint)
        panel('rear lower '+str(s),[(s*1.88,-.15),(s*2.32,-.29),(s*3.13,-.23),(s*3.13,-.82),(s*2.28,-.82),(s*1.45,-.65)],r.paint)
        # Recessed cheek seam: follows the same rounded shell, no extra bulk.
        points=[surface(Vector((s*a,e)))+normal(Vector((s*a,e)))*.003 for a,e in [(.88,-.13),(1.07,-.24),(1.10,-.40),(.90,-.50)]]
        r.line('F23R cheek rebate '+str(s),points,'Head',r.edge,.0009)
    for e in [-.48,-.57,-.66]:
        pts=[surface(Vector((a,e)))+normal(Vector((a,e)))*.003 for a in [2.87,3.0,3.14,3.28,3.41]]
        r.line('F23R rear vent '+str(e),pts,'Head',r.seal,.0011)
    unchanged=all(geometry(bpy.data.objects[n])==h for n,h in preserved.items())
    assert unchanged,'Non-head geometry/weights/transforms changed'
    report={'source_sha256':hashlib.sha256(SOURCE.read_bytes()).hexdigest(),'removed_head_objects':removed,'painted_panels':painted,'unchanged_nonhead_objects':len(preserved),'nonhead_geometry_weights_transforms_exact':unchanged,'identity_surface_m2':{'before':before_area,'after':area()},'new_helmet_objects':len(cage.solids),'new_materials':0,'new_textures':0,'design':'helmet concept 2 + world paint concept 3; dimensions in metres; original F23 preserved'}
    (OUT/'authoring.json').write_text(json.dumps(report,indent=2)+'\n')
    g.rig.data.pose_position='POSE';bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(TARGET),compress=True)
    print('F23_ROUND',report)
if __name__=='__main__':main()
