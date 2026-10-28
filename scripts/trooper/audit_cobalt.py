"""Validate exported COBALT surfaces/anchors/manifest without starting Unity."""
import hashlib
import json
from pathlib import Path
import struct

ROOT=Path(__file__).resolve().parents[2]
art=ROOT/'unity/Assets/StarTournament/Art'
manifest=json.loads((art/'asset-manifest.json').read_text())
rows=[]
for name in ('automatic-rifle-lod0.glb','automatic-rifle-lod1.glb'):
    raw=(art/name).read_bytes()
    magic,version,length=struct.unpack_from('<III',raw)
    assert magic==0x46546c67 and version==2 and length==len(raw), name
    count=struct.unpack_from('<I',raw,12)[0]
    data=json.loads(raw[20:20+count])
    for anchor in ('rifle-grip','rifle-muzzle','cobalt-support'):
        assert sum(n.get('name')==anchor for n in data['nodes'])==1, anchor
    assert len(data['materials'])==7 and len(data['images'])==21
    for m in data['materials']:
        assert 'baseColorTexture' in m['pbrMetallicRoughness']
        assert 'metallicRoughnessTexture' in m['pbrMetallicRoughness']
        assert 'normalTexture' in m
    assert all('TEXCOORD_0' in p['attributes'] for m in data['meshes'] for p in m['primitives'])
    triangles=sum(data['accessors'][p['indices']]['count']//3 for m in data['meshes'] for p in m['primitives'])
    sha=hashlib.sha256(raw).hexdigest()
    entry=next(e for e in manifest['entries'] if e['path'].endswith(name))
    assert sha==entry['sha256'], name+' manifest mismatch'
    assert triangles==97422, 'Texture correction must preserve detailed geometry'
    rows.append(dict(file=name,sha256=sha,triangles=triangles,bytes=len(raw),materials=7,textures=21,meshes=len(data['meshes'])))
assert rows[0]['sha256']==rows[1]['sha256'], 'The initial art review intentionally retains full detail in both exports'
print(json.dumps(dict(result='PASS',assets=rows),indent=2))
