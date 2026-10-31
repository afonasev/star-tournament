"""Unreduced art-stage audit; runtime cap assertions remain in audit_reference.py.

This checks source/export compatibility, not artistic acceptance or game readiness.
"""
import argparse
import hashlib
import json
import math
import subprocess
from pathlib import Path
from audit_runtime import ROOT, ART, Glb

BASELINE = '9345bb2a795fb7d09070d2d81fae82de07859cf7'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    def pinned(path):
        return subprocess.check_output(['git', 'show', BASELINE + ':' + str(path.relative_to(ROOT))], cwd=ROOT)
    raw = args.source.read_bytes()
    model = Glb(raw)
    old = Glb(pinned(ART / 'trooper-body-animated.glb'))
    assert model.binds() == old.binds(), 'Exact bind skeleton'
    assert model.primitives(weapon=True) == old.primitives(weapon=True), 'Exact equipped weapon'
    for path in [ART / 'trooper-arms-animated.glb', ROOT / 'scripts/vector_fighter/f18-heavy-agile.blend']:
        assert path.read_bytes() == pinned(path), 'Preserved source/FPS: ' + path.name
    for path in (ROOT / 'unity/Assets/StarTournament/Trooper').glob('*.anim'):
        assert path.read_bytes() == pinned(path), 'Preserved clip: ' + path.name
    for mesh in model.data['meshes']:
        for primitive in mesh['primitives']:
            attributes = primitive['attributes']
            for key in ['POSITION', 'NORMAL']:
                assert all(all(math.isfinite(v) for v in row) for row in model.array(attributes[key])), 'Finite ' + key
            if 'WEIGHTS_0' in attributes:
                assert all(abs(sum(row) - 1) < .0001 for row in model.array(attributes['WEIGHTS_0'])), 'Normalized skin'
    materials = {m['name']: m for m in model.data['materials']}
    for name in ['vector-reference-coated-steel', 'vector-fighter-identity-reference']:
        m = materials[name]
        assert 'normalTexture' in m and 'baseColorTexture' in m['pbrMetallicRoughness'] and 'metallicRoughnessTexture' in m['pbrMetallicRoughness'], name + ' PBR'
    report = {'result': 'PASS art compatibility', 'baseline': BASELINE,
              'source': str(args.source), 'sha256': hashlib.sha256(raw).hexdigest(),
              'skin_triangles': model.triangles(), 'materials': len(materials),
              'images': len(model.data.get('images', [])), 'optimization': 'None; unreduced art source',
              'exact': ['binds', 'weapon', 'FPS', 'retained F18 source', 'clips'],
              'runtime_budget': 'Deferred by user 2026-10-29; runtime audit unchanged',
              'visual_human_native_player': 'pending / NotRun'}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, indent=2))


if __name__ == '__main__':
    main()
