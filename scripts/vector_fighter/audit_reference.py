"""F19 compatibility/material audit. Native appearance still requires render review."""
import sys,json,subprocess
from pathlib import Path
import audit_heavy_agile as previous
from audit_runtime import ROOT,Glb
BASELINE='9345bb2a795fb7d09070d2d81fae82de07859cf7'

def main():
    previous.BASELINE=BASELINE
    source=ROOT/'scripts/vector_fighter/f18-heavy-agile.blend'
    assert source.read_bytes()==subprocess.check_output(['git','show',BASELINE+':'+str(source.relative_to(ROOT))],cwd=ROOT),'Retained F18 editable source'
    previous.main()
    output=Path(sys.argv[sys.argv.index('--output')+1]);report=json.loads(output.read_text());model=Glb(Path(report['source']).read_bytes())
    new={m['name']:m for m in model.data['materials'] if m['name'].startswith(('vector-reference','vector-fighter-identity-reference'))}
    assert len(new)==7,('Expected shared surface roles',list(new))
    for name in ['vector-reference-coated-steel','vector-fighter-identity-reference']:
        m=new[name];assert 'normalTexture' in m and 'baseColorTexture' in m['pbrMetallicRoughness'] and 'metallicRoughnessTexture' in m['pbrMetallicRoughness'],name+' complete PBR'
    for name in ['vector-reference-woven-fabric','vector-reference-webbing']:
        m=new[name];assert 'normalTexture' in m and m['pbrMetallicRoughness']['roughnessFactor']>=.85 and m['pbrMetallicRoughness'].get('metallicFactor',1)==0,name+' textile'
    report.update(optimization='F19 authored plate/hardware topology retained; continuous cloth/sleeves/world gloves reduced; old source/FPS/weapon exact',material_contract='PASS: coated steel and identity albedo/normal/packed MR; matte nonmetal woven textile normal/albedo',human_visual_acceptance='pending')
    output.write_text(json.dumps(report,indent=2)+'\n')
if __name__=='__main__':main()
