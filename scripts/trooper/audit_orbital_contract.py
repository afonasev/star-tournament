"""Read shipped GLB contracts without importing or modifying the artifacts."""
import hashlib,json,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
ART=ROOT/'unity/Assets/StarTournament/Art'
REPORT={}
for name in ['vector-shotgun-lod0.glb','vector-shotgun-lod1.glb','trooper-arms-animated.glb','trooper-body-animated.glb']:
    blob=(ART/name).read_bytes()
    assert blob[:4]==b'glTF' and struct.unpack_from('<I',blob,4)[0]==2
    length=struct.unpack_from('<I',blob,12)[0];asset=json.loads(blob[20:20+length])
    assert all('uri' not in image for image in asset.get('images',[])), 'Texture must be embedded'
    record={'sha256':hashlib.sha256(blob).hexdigest(),
            'triangles':sum(asset['accessors'][p['indices']]['count']//3 for m in asset['meshes'] for p in m['primitives']),
            'embeddedImages':len(asset.get('images',[])),
            'normalMappedMaterials':[m['name'] for m in asset.get('materials',[]) if 'normalTexture' in m],
            'skinJoints':[len(s['joints']) for s in asset.get('skins',[])],
            'clips':[a['name'] for a in asset.get('animations',[])]}
    if name.startswith('trooper-'):
        assert record['skinJoints']==[57]
        assert set(record['clips'])=={'idle','walk','run','aim','fire','hit','death'}
        names=[n.get('name') for n in asset['nodes']]
        assert all(names.count('vector-muzzle-'+str(i))==1 for i in range(2))
        if '-arms-' in name:assert names.count('vector-armored-hands')==1
    REPORT[name]=record
assert REPORT['vector-shotgun-lod1.glb']['triangles'] < REPORT['vector-shotgun-lod0.glb']['triangles']*.7
manifest=json.loads((ART/'asset-manifest.json').read_text())
for entry in manifest['entries']:
    name=Path(entry['path']).name
    if name in REPORT:assert entry['sha256']==REPORT[name]['sha256'], entry['key']
output=ROOT/'docs/evidence/orbital-f3/export-contract.json'
output.write_text(json.dumps(REPORT,indent=2)+'\n')
print('ORBITAL_F3_CONTRACT_PASS', {name:item['triangles'] for name,item in REPORT.items()})
