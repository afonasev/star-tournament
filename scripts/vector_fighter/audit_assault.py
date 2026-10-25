"""Strict F17 change contract; historical optimization audit remains unmodified."""
import argparse
import copy
import hashlib
import json
import subprocess
from pathlib import Path
from audit_runtime import ROOT, ART, Glb

BASELINE = '19f5a987'


def audit(output):
    report = {'baseline': BASELINE, 'assets': {}}
    manifest = json.loads((ART/'asset-manifest.json').read_text())
    mounts = json.loads((ART/'trooper-mounts.json').read_text())
    for kind, key, cap in [('body', 'player-body', 32989), ('arms', 'player-hands', 45000)]:
        path = ART/('trooper-'+kind+'-animated.glb')
        old = Glb(subprocess.check_output(['git', 'show', BASELINE+':'+path.relative_to(ROOT).as_posix()], cwd=ROOT))
        raw = path.read_bytes()
        new = Glb(raw)
        digest = hashlib.sha256(raw).hexdigest()
        assert digest == mounts[kind]['sha256'] == next(e['sha256'] for e in manifest['entries'] if e['key'] == key)
        assert new.triangles() <= cap, (kind, 'triangle budget')
        assert new.binds() == old.binds(), (kind, 'bind skeleton')
        assert new.images() == old.images(), (kind, 'image byte identity and image count')
        assert new.primitives(weapon=True) == old.primitives(weapon=True), (kind, 'weapon geometry')
        before = old.material_contract()
        after = new.material_contract()
        additions = {'vector-assault-armor-forearm': 'vector-fighter-identity-forearm-f9'}
        if kind == 'body':
            additions['vector-assault-armor'] = 'vector-fighter-identity'
        expected = copy.deepcopy(before)
        for target, source in additions.items():
            expected[target] = copy.deepcopy(before[source])
            expected[target]['name'] = target
            expected[target]['pbrMetallicRoughness']['baseColorFactor'] = [.14, .20, .25, 1]
        assert after == expected, (kind, 'only specified neutral material factors/roles may change')
        if kind == 'arms':
            assert new.primitives(glove=True) == old.primitives(glove=True), 'Exact glove anatomy/UV/normals/named weights'
        for node in new.data['nodes']:
            if 'skin' not in node or 'mesh' not in node:
                continue
            for primitive in new.data['meshes'][node['mesh']]['primitives']:
                for weights in new.array(primitive['attributes']['WEIGHTS_0']):
                    assert abs(sum(weights)-1) < .0001, (kind, 'skin normalization')
        report['assets'][kind] = {'sha256': digest, 'skin_triangles': new.triangles(), 'cap': cap,
                                'binds_images_weapon': 'exactly unchanged', 'materials': 'only specified neutral roles added',
                                'glove_geometry': 'exactly unchanged' if kind == 'arms' else 'world resolution'}
    source = ROOT/'scripts/vector_fighter/f16-orbital-hands.blend'
    assert source.read_bytes() == subprocess.check_output(['git', 'show', BASELINE+':'+source.relative_to(ROOT).as_posix()], cwd=ROOT)
    report['f16_source'] = 'exactly unchanged'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2)+'\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    audit(parser.parse_args().output)
