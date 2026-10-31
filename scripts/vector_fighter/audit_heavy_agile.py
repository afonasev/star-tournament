"""Focused F18 compatibility/resource audit; does not claim visual acceptance."""
import argparse,hashlib,json,subprocess,math
from pathlib import Path
from audit_runtime import ROOT,ART,Glb

BASELINE='9d2e15bf'

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--source',type=Path,default=ART/'trooper-body-animated.glb');parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();raw=args.source.read_bytes();new=Glb(raw)
    def original(path):return subprocess.check_output(['git','show',BASELINE+':'+str(path.relative_to(ROOT))],cwd=ROOT)
    old=Glb(original(ART/'trooper-body-animated.glb'))
    assert new.binds()==old.binds(),'Exact named bind skeleton'
    assert new.primitives(weapon=True)==old.primitives(weapon=True),'Exact weapon geometry and named skin'
    assert new.triangles()<=32989,('World mesh budget',new.triangles())
    arms=ART/'trooper-arms-animated.glb';assert arms.read_bytes()==original(arms),'First-person geometry/materials/images remain byte-identical'
    source=ROOT/'scripts/vector_fighter/f17-assault.blend';assert source.read_bytes()==original(source),'Retained editable F17'
    for path in (ROOT/'unity/Assets/StarTournament/Trooper').glob('*.anim'):assert path.read_bytes()==original(path),'Retained imported clip '+path.name
    for node in new.data['nodes']:
        if 'skin' not in node or 'mesh' not in node:continue
        for primitive in new.data['meshes'][node['mesh']]['primitives']:
            for weights in new.array(primitive['attributes']['WEIGHTS_0']):assert abs(sum(weights)-1)<.0001,'Normalized skin'
            for p in new.array(primitive['attributes']['POSITION']):assert all(math.isfinite(x) for x in p),'Finite geometry'
    if args.source.resolve()==(ART/'trooper-body-animated.glb').resolve():
        manifest=json.loads((ART/'asset-manifest.json').read_text());mounts=json.loads((ART/'trooper-mounts.json').read_text());digest=hashlib.sha256(raw).hexdigest()
        assert digest==mounts['body']['sha256']==next(e['sha256'] for e in manifest['entries'] if e['key']=='player-body')
    def stats(glb):return {'skin_triangles':glb.triangles(),'materials':len(glb.data['materials']),'embedded_images':len(glb.data.get('images',[])),
        'skinned_renderers':sum('skin' in n for n in glb.data['nodes']),
        'mesh_primitives':sum(len(m['primitives']) for m in glb.data['meshes'])}
    report={'baseline':BASELINE,'source':str(args.source),'sha256':hashlib.sha256(raw).hexdigest(),
        'before':stats(old),'after':stats(new),'binds_weapons_first_person_clips':'PASS exact',
        'weights_geometry':'PASS normalized finite','added_images':sorted(set(new.images())-set(old.images())),
        'optimization':'purpose-built panel topology; lower garment decimation; original world/FP split retained',
        'performance_device_human_acceptance':'NotRun; static asset counts are not FPS/device evidence'}
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
