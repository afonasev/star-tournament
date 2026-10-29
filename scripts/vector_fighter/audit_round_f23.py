"""Focused compatibility/resource audit of the rounded F23 export against main.
Actual GLB data only; rendering and human acceptance are separate gates.
"""
import argparse,hashlib,json,math,subprocess
from pathlib import Path
from audit_runtime import ROOT,ART,Glb

def stats(g):
    return {'skin_triangles':g.triangles(),'materials':len(g.data['materials']),'images':len(g.data.get('images',[])),'skinned_renderers':sum('skin' in n for n in g.data['nodes']),'mesh_primitives':sum(len(m['primitives']) for m in g.data['meshes'])}

def main():
    p=argparse.ArgumentParser();p.add_argument('--baseline',required=True);p.add_argument('--source',type=Path,required=True);p.add_argument('--high',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    def old(path):return subprocess.check_output(['git','show',a.baseline+':'+path.relative_to(ROOT).as_posix()],cwd=ROOT)
    before=Glb(old(ART/'trooper-body-animated.glb'));after=Glb(a.source.read_bytes());high=Glb(a.high.read_bytes())
    if a.source.resolve()==(ART/'trooper-body-animated.glb').resolve():
        digest=hashlib.sha256(a.source.read_bytes()).hexdigest()
        manifest=json.loads((ART/'asset-manifest.json').read_text());mounts=json.loads((ART/'trooper-mounts.json').read_text())
        assert digest==mounts['body']['sha256']==next(e['sha256'] for e in manifest['entries'] if e['key']=='player-body'),'World manifest hashes'
    assert after.binds()==before.binds()==high.binds(),'Named bind skeleton'
    assert after.primitives(weapon=True)==before.primitives(weapon=True)==high.primitives(weapon=True),'Weapon geometry/skin'
    assert after.primitives(glove=True)==before.primitives(glove=True),'World glove anatomy'
    assert after.images()==before.images()==high.images(),'Embedded texture bytes'
    assert after.material_contract()==before.material_contract()==high.material_contract(),'Existing PBR roles'
    assert after.triangles()<=before.triangles(),'Do not regress optimized world geometry cost'
    assert after.triangles()<high.triangles(),'Separate optimization pass'
    for node in after.data['nodes']:
        if 'skin' not in node or 'mesh' not in node:continue
        for primitive in after.data['meshes'][node['mesh']]['primitives']:
            for weights in after.array(primitive['attributes']['WEIGHTS_0']):assert abs(sum(weights)-1)<.0001,'Normalized skin'
            for v in after.array(primitive['attributes']['POSITION']):assert all(math.isfinite(x) for x in v),'Finite geometry'
    fixed=[ART/'trooper-arms-animated.glb',ROOT/'scripts/vector_fighter/f23-color-coverage.blend']+list((ROOT/'unity/Assets/StarTournament/Trooper').glob('*.anim'))
    for f in fixed:assert f.read_bytes()==old(f),'Unchanged '+str(f)
    result={'result':'PASS','baseline':a.baseline,'source':str(a.source),'sha256':hashlib.sha256(a.source.read_bytes()).hexdigest(),'before':stats(before),'high':stats(high),'after':stats(after),'exact':['named bind skeleton','weapon geometry/skin','world gloves','embedded textures','PBR materials','FPS GLB','original F23 source','persisted animation clips'],'weights':'normalized finite','fixed_hashes':{str(f.relative_to(ROOT)):hashlib.sha256(f.read_bytes()).hexdigest() for f in fixed},'performance_hardware_human':'NotRun; static resource counts are not frame-time acceptance'}
    a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({k:v for k,v in result.items() if k!='fixed_hashes'},indent=2))
if __name__=='__main__':main()
