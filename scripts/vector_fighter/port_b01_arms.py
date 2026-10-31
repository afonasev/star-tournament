"""Port exact optimized B01 forearms onto accepted FPS gloves, without resampling.

Run with Python; --promote updates the shipping asset and its manifests.
The pinned GLBs plus vector-body-b01.blend are the editable/reproducible source.
"""
import argparse
import copy
import hashlib
import json
import struct
import subprocess
from pathlib import Path

from audit_runtime import Glb, ROOT, ART
from export_heavy_agile import retain_current_weapon

BASE = '70828f746096ee7fda72d77b73ba8758d48b6e80'
FOREARMS = {'vector-body-b01-textile-baked', 'vector-body-b01-graphite-baked',
            'vector-fighter-identity-body-b01-baked', 'vector-reference-woven-fabric',
            'vector-reference-flexible-seals'}
GLOVES = {'orbital-restored-material', 'orbital-restored-vector-glove',
          'orbital-restored-vector-chassis', 'vector-ceramic-orbital'}


def source(filename):
    return subprocess.check_output(['git', 'show', BASE+':'+(ART/filename).relative_to(ROOT).as_posix()], cwd=ROOT)


def write(path, data, binary):
    data['buffers'][0]['byteLength'] = len(binary)
    binary += b'\0' * (-len(binary) % 4)
    payload = json.dumps(data, separators=(',', ':')).encode()
    payload += b' ' * (-len(payload) % 4)
    path.write_bytes(struct.pack('<4sII', b'glTF', 2, 28+len(payload)+len(binary)) +
                     struct.pack('<I4s', len(payload), b'JSON') + payload +
                     struct.pack('<I4s', len(binary), b'BIN\0') + binary)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--promote', action='store_true')
    args = parser.parse_args()
    out = ROOT/'.local/b01-fps'; out.mkdir(parents=True, exist_ok=True)
    original = source('trooper-arms-animated.glb')
    arms = Glb(original); body = Glb(source('trooper-body-animated.glb'))
    assert arms.binds() == body.binds(), 'Transfer requires identical bind transforms'
    data = arms.data; binary = bytearray(arms.binary); cache = {}
    view = next(n for n in data['nodes'] if n['name'] == 'vector-armored-hands')
    world = next(n for n in body.data['nodes'] if n['name'] == 'VECTOR world forearms')
    def names(g, n):
        return [g.data['nodes'][j]['name'] for j in g.data['skins'][n['skin']]['joints']]
    assert names(arms, view) == names(body, world), 'Joint indices must have the same meaning'
    for key in ('matrix', 'translation', 'rotation', 'scale'):
        assert view.get(key) == world.get(key), 'Mesh-local coordinates must match'

    def item(kind, index):
        key = (kind, index)
        if key in cache:
            return cache[key]
        value = copy.deepcopy(body.data[kind][index])
        if kind == 'bufferViews':
            start = value.get('byteOffset', 0)
            binary.extend(b'\0' * (-len(binary) % 4))
            value['byteOffset'] = len(binary); value['buffer'] = 0
            binary.extend(body.binary[start:start+value['byteLength']])
        elif kind in ('accessors', 'images'):
            value['bufferView'] = item('bufferViews', value['bufferView'])
        elif kind == 'textures':
            value['source'] = item('images', value['source'])
            if 'sampler' in value:
                value['sampler'] = item('samplers', value['sampler'])
        elif kind == 'materials':
            def textures(v):
                if isinstance(v, dict):
                    if 'index' in v:
                        v['index'] = item('textures', v['index'])
                    for child in v.values():
                        textures(child)
                elif isinstance(v, list):
                    for child in v:
                        textures(child)
            textures(value)
        data.setdefault(kind, []).append(value)
        cache[key] = len(data[kind])-1
        return cache[key]

    mesh = data['meshes'][view['mesh']]
    mesh['primitives'] = [p for p in mesh['primitives'] if data['materials'][p['material']]['name'] in GLOVES]
    assert len(mesh['primitives']) == len(GLOVES)
    for p in body.data['meshes'][world['mesh']]['primitives']:
        if body.data['materials'][p['material']]['name'] not in FOREARMS:
            continue
        q = copy.deepcopy(p)
        q['indices'] = item('accessors', p['indices'])
        q['attributes'] = {k:item('accessors', v) for k,v in p['attributes'].items()}
        q['material'] = item('materials', p['material'])
        mesh['primitives'].append(q)
    assert len(mesh['primitives']) == len(GLOVES) + len(FOREARMS)
    candidate = out/'candidate-arms.glb'
    write(candidate, data, binary)
    baseline = out/'baseline-arms.glb'; baseline.write_bytes(original)
    # Existing exact equipment copier also prunes all orphaned tables and bytes.
    retain_current_weapon(candidate, baseline)
    result = Glb(candidate.read_bytes()); old = Glb(original)
    assert result.binds() == old.binds()
    assert result.primitives(glove=True) == old.primitives(glove=True)
    assert result.primitives(weapon=True) == old.primitives(weapon=True)
    for name in FOREARMS:
        assert result.material_contract()[name] == body.material_contract()[name], name
    for name, digest in result.images().items():
        assert digest in (old.images() | body.images()).values(), name
    assert result.triangles() <= 45000
    # Exact triangle/attribute comparison, including UV, normal and named weights.
    def surfaces(g):
        return {k:v for k,v in g.primitives().items() if k in FOREARMS}
    # The body also uses these materials elsewhere: restrict it to its arm mesh.
    body.data['nodes'] = copy.deepcopy(body.data['nodes'])
    for n in body.data['nodes']:
        if n.get('name') != 'VECTOR world forearms':
            n.pop('mesh', None)
    assert surfaces(result) == surfaces(body), 'Every B01 forearm triangle must survive exactly'
    report = {'result':'PASS', 'baseline':BASE, 'sha256':hashlib.sha256(candidate.read_bytes()).hexdigest(),
              'triangles':{'before':old.triangles(), 'after':result.triangles(), 'ceiling':45000},
              'material_tables':{'before':len(old.data['materials']), 'after':len(result.data['materials'])},
              'used_materials':{'before':len({p['material'] for m in old.data['meshes'] for p in m['primitives']}), 'after':len(result.data['materials'])},
              'bytes':{'before':len(original), 'after':candidate.stat().st_size},
              'protected':['exact FPS gloves and wrist mounts', 'binds/joint order', 'equipment', 'exact optimized B01 triangles/UV/normals/weights/materials'],
              'optimization':'Reuse already bounded optimized B01 surfaces; remove replaced FPS armor and compact orphan buffers/maps. No further lossy reduction.',
              'draw_calls':'one existing arm skin; 7 to 9 material slots, shared with body and all seats',
              'textures':'existing B01 maps, no rebake; shared body subassets through TrooperPreparation',
              'shadows_LOD':'existing FPS shadow-off policy, single near-camera mesh; no distance LOD appropriate',
              'performance':'NotRun; resource accounting is not a CPU/GPU benchmark'}
    (out/'asset-audit.json').write_text(json.dumps(report, indent=2)+'\n')
    if args.promote:
        target = ART/'trooper-arms-animated.glb'
        assert (ART/'trooper-body-animated.glb').read_bytes() == source('trooper-body-animated.glb'), 'Review newer B01 changes before promotion'
        assert target.read_bytes() in (original, candidate.read_bytes()), 'Do not overwrite newer FPS work'
        target.write_bytes(candidate.read_bytes())
        manifest = json.loads((ART/'asset-manifest.json').read_text())
        entry = next(e for e in manifest['entries'] if e['key'] == 'player-hands')
        entry['sha256'] = report['sha256']
        entry['source'] = 'B01 exact optimized forearms + preserved accepted FPS gloves; scripts/vector_fighter/port_b01_arms.py'
        mounts = json.loads((ART/'trooper-mounts.json').read_text()); mounts['arms']['sha256'] = report['sha256']
        (ART/'asset-manifest.json').write_text(json.dumps(manifest, indent=2)+'\n')
        (ART/'trooper-mounts.json').write_text(json.dumps(mounts, indent=2)+'\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
