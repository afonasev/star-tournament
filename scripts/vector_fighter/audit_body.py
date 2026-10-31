"""Authorized B01 body replacement: exact protected contracts, new surface QA."""
import argparse,hashlib,json,math,subprocess
from pathlib import Path
from audit_runtime import Glb,ROOT,ART


def head(glb):
    result=[]
    for node in glb.data['nodes']:
        if 'skin' not in node or 'mesh' not in node:continue
        joints=[glb.data['nodes'][i]['name'] for i in glb.data['skins'][node['skin']]['joints']]
        for p in glb.data['meshes'][node['mesh']]['primitives']:
            a={k:glb.array(v) for k,v in p['attributes'].items()};ids=[v[0] for v in glb.array(p['indices'])]
            selected=set(i for i,(js,ws) in enumerate(zip(a['JOINTS_0'],a['WEIGHTS_0'])) if all(joints[j]=='Head' or w==0 for j,w in zip(js,ws)))
            for t in range(0,len(ids),3):
                tri=ids[t:t+3]
                if not all(i in selected for i in tri):continue
                result.append((glb.data['materials'][p['material']]['name'],tuple(tuple((k,a[k][i]) for k in sorted(a) if k not in ('JOINTS_0','WEIGHTS_0')) for i in tri)))
    return sorted(result)


def main():
    pa=argparse.ArgumentParser();pa.add_argument('--output',type=Path,required=True);args=pa.parse_args()
    base='e616d83657daf483c4f00f856529ffae40416426';out=ROOT/'.local/vector-body-reconstruction'
    def old(p):return subprocess.check_output(['git','show',base+':'+p.relative_to(ROOT).as_posix()],cwd=ROOT)
    b=Glb(old(ART/'trooper-body-animated.glb'));a=Glb((ART/'trooper-body-animated.glb').read_bytes());h=Glb((out/'high-body.glb').read_bytes())
    assert a.binds()==b.binds()==h.binds(),'exact bind matrices'
    assert a.primitives(weapon=True)==b.primitives(weapon=True),'exact equipment'
    assert a.primitives(glove=True)==b.primitives(glove=True),'exact world gloves'
    assert head(a)==head(b)==head(h),'exact F02 head geometry, UV and normals'
    protected=['vector-fighter-identity-f23-helmet','vector-f23-helmet-coated-steel','vector-reference-opaque-visor','vector-reference-coated-steel','vector-reference-woven-fabric','vector-reference-flexible-seals','vector-reference-edge-metal','vector-reference-webbing','vector-fighter-identity-reference']
    for n in protected:assert a.material_contract()[n]==b.material_contract()[n],n
    for n,digest in b.images().items():
        if n in a.images():assert a.images()[n]==digest,n
    assert a.images()==h.images() and a.material_contract()==h.material_contract(),'optimization maps/materials'
    assert a.triangles()<h.triangles()<b.triangles(),'separate bounded optimization'
    fixed=[ART/'trooper-arms-animated.glb',ROOT/'scripts/vector_fighter/f23-reconstructed.blend']+list((ROOT/'unity/Assets/StarTournament/Trooper').glob('*.anim'))
    for p in fixed:assert p.read_bytes()==old(p),p
    for node in a.data['nodes']:
        if 'skin' not in node or 'mesh' not in node:continue
        for p in a.data['meshes'][node['mesh']]['primitives']:
            for w in a.array(p['attributes']['WEIGHTS_0']):assert abs(sum(w)-1)<.0001 and min(w)>=0
            for v in a.array(p['attributes']['POSITION']):assert all(math.isfinite(x) for x in v)
    digest=hashlib.sha256((ART/'trooper-body-animated.glb').read_bytes()).hexdigest()
    assert digest==json.loads((ART/'trooper-mounts.json').read_text())['body']['sha256']
    assert digest==next(e['sha256'] for e in json.loads((ART/'asset-manifest.json').read_text())['entries'] if e['key']=='player-body')
    report={'result':'PASS','baseline':base,'sha256':digest,'triangles':{'baseline':b.triangles(),'new_source':h.triangles(),'optimized':a.triangles()},'materials':{'baseline':len(b.data['materials']),'new':len(a.data['materials'])},'images':{'baseline':len(b.images()),'new':len(a.images())},'exact':['F02 head vertices/normals/UV','world glove triangles/weights/UV','weapon','binds','FPS GLB bytes','persisted clips','protected canonical FPS material palette','all retained old image bytes'],'new':'body cages, three dedicated atlas materials and three shared images','notrun':['physical/performance/human acceptance']}
    args.output.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
