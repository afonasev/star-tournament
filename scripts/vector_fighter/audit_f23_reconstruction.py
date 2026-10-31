"""F02 contract: exact unaffected maps/materials; explicitly added helmet atlas.

audit_round_f23.py retains the historical all-texture-byte invariant unchanged.
This audit names the authorized F02 additions instead of suppressing that check.
"""
import argparse,hashlib,json,subprocess,math,io
from pathlib import Path
from PIL import Image
import numpy as np
from audit_runtime import ROOT,ART,Glb
from audit_round_f23 import stats

def main():
    p=argparse.ArgumentParser();p.add_argument('--baseline',required=True);p.add_argument('--source',type=Path,required=True)
    p.add_argument('--high',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
    def old(path):return subprocess.check_output(['git','show',a.baseline+':'+path.relative_to(ROOT).as_posix()],cwd=ROOT)
    before=Glb(old(ART/'trooper-body-animated.glb'));after=Glb(a.source.read_bytes());high=Glb(a.high.read_bytes())
    assert after.binds()==before.binds()==high.binds(),'Bind skeleton'
    assert after.primitives(weapon=True)==before.primitives(weapon=True)==high.primitives(weapon=True),'Weapon geometry'
    assert after.primitives(glove=True)==before.primitives(glove=True),'World gloves'
    for name,value in before.images().items():assert after.images().get(name)==value,('Unaffected image bytes',name)
    for name,value in before.material_contract().items():assert after.material_contract().get(name)==value,('Unaffected material',name)
    assert after.images()==high.images() and after.material_contract()==high.material_contract(),'Optimization material preservation'
    added=set(after.material_contract())-set(before.material_contract())
    assert added=={'vector-fighter-identity-f23-helmet','vector-f23-helmet-coated-steel'},added
    for name in added:
        material=after.material_contract()[name]
        assert 'occlusionTexture' in material and 'normalTexture' in material,name
        assert abs(material['normalTexture'].get('scale',1)-.23)<.0001,'Calibrated relief depth'
    assert after.triangles()<=before.triangles() and after.triangles()<high.triangles(),'Geometry cost'
    for node in after.data['nodes']:
        if 'skin' not in node or 'mesh' not in node:continue
        for primitive in after.data['meshes'][node['mesh']]['primitives']:
            for weights in after.array(primitive['attributes']['WEIGHTS_0']):assert abs(sum(weights)-1)<.0001
            for v in after.array(primitive['attributes']['POSITION']):assert all(math.isfinite(x) for x in v)
    fixed=[ART/'trooper-arms-animated.glb',ROOT/'scripts/vector_fighter/f23-color-coverage.blend',ROOT/'scripts/vector_fighter/f23-military-color.blend']+list((ROOT/'unity/Assets/StarTournament/Trooper').glob('*.anim'))
    for f in fixed:assert f.read_bytes()==old(f),str(f)
    maps={}
    for im in after.data['images']:
        if not im['name'].startswith('F23-helmet'):continue
        view=after.data['bufferViews'][im['bufferView']];start=view.get('byteOffset',0)
        image=Image.open(io.BytesIO(after.binary[start:start+view['byteLength']])).convert('RGB')
        values=np.array(image)/255
        maps[im['name']]={'size':image.size,'range':[[float(values[:,:,i].min()),float(values[:,:,i].max())] for i in range(3)],'std':[float(values[:,:,i].std()) for i in range(3)]}
    assert len(maps)==3,maps.keys()
    normal=next(v for k,v in maps.items() if 'normal' in k)
    assert normal['std'][0]>.015 and normal['std'][1]>.015,'Non-flat tangent relief'
    digest=hashlib.sha256(a.source.read_bytes()).hexdigest()
    if a.source.resolve()==(ART/'trooper-body-animated.glb').resolve():
        manifest=json.loads((ART/'asset-manifest.json').read_text());mounts=json.loads((ART/'trooper-mounts.json').read_text())
        assert digest==mounts['body']['sha256']==next(e['sha256'] for e in manifest['entries'] if e['key']=='player-body')
    report={'result':'PASS','baseline':a.baseline,'source':str(a.source),'sha256':digest,'before':stats(before),'high':stats(high),'after':stats(after),'added_materials':sorted(added),'atlas':maps,'exact':['old material contracts','old embedded image bytes','binds','weapon','world gloves','FPS bytes','persisted clips','previous editable sources'],'notrun':['frame-time/hardware','human visual acceptance']}
    a.output.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))

if __name__=='__main__':main()
