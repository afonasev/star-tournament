"""Inventory body GLBs actually present on main's first-parent game history.

Uses Git objects, never replaces current game assets. Full GLB and non-weapon
skin payload signatures distinguish equipment-only revisions from body changes.
This is provenance/resource discovery, not visual or runtime acceptance.
"""
import argparse
import hashlib
import json
import struct
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ASSET = 'unity/Assets/StarTournament/Art/trooper-body-animated.glb'
SIZES = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}
WIDTHS = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT)


def inspect(raw):
    assert raw[:4] == b'glTF'
    size = struct.unpack_from('<I', raw, 12)[0]
    data = json.loads(raw[20:20+size])
    binary = raw[28+size:]
    def accessor(index):
        a = data['accessors'][index]
        view = data['bufferViews'][a['bufferView']]
        width = SIZES[a['componentType']] * WIDTHS[a['type']]
        start = view.get('byteOffset', 0) + a.get('byteOffset', 0)
        stride = view.get('byteStride', width)
        payload = b''.join(binary[start+i*stride:start+i*stride+width] for i in range(a['count']))
        return {'count': a['count'], 'type': a['type'], 'component': a['componentType'],
                'sha256': hashlib.sha256(payload).hexdigest()}
    rows = []
    triangles = 0
    for node in data['nodes']:
        if 'mesh' not in node or node.get('name') == 'weapon:joined':
            continue
        joints = [data['nodes'][i]['name'] for i in data['skins'][node['skin']]['joints']] if 'skin' in node else []
        for primitive in data['meshes'][node['mesh']]['primitives']:
            mat = data['materials'][primitive['material']]['name']
            indices = accessor(primitive['indices']) if 'indices' in primitive else {'count': data['accessors'][primitive['attributes']['POSITION']]['count'], 'type':'implicit-sequential'}
            triangles += indices['count']//3
            rows.append({'material': mat, 'joints': joints,
                         'attributes': {k: accessor(v) for k,v in sorted(primitive['attributes'].items())},
                         'indices': indices})
    # Include body material definitions and image bytes because paint/material
    # changes are visually meaningful generations even with identical geometry.
    used = {r['material'] for r in rows}
    materials = [m for m in data['materials'] if m['name'] in used]
    geometry = hashlib.sha256(json.dumps(rows, sort_keys=True).encode()).hexdigest()
    images = []
    for im in data.get('images', []):
        view = data['bufferViews'][im['bufferView']];start = view.get('byteOffset', 0)
        images.append({'name': im.get('name'), 'sha256': hashlib.sha256(binary[start:start+view['byteLength']]).hexdigest()})
    return {'body_skin_payload_sha256': geometry, 'body_skin_triangles': triangles,
            'body_material_names': sorted(used), 'body_materials': materials, 'images': images,
            'skinned_renderers': sum('skin' in n for n in data['nodes']),
            'signature_limit': 'payload equality is exact; changed signature may be topology/order/UV/skin optimization, not a new artistic design'}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--ref', default='main')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--extract', type=Path, help='Optional owned Unity import directory for native history review')
    args = parser.parse_args()
    history = git('log', '--first-parent', '--reverse', '--format=%H%x09%s', args.ref, '--', ASSET).decode().splitlines()
    rows = [];seen = {}
    for line in history:
        sha, title = line.split('\t', 1)
        raw = git('show', sha+':'+ASSET)
        digest = hashlib.sha256(raw).hexdigest()
        row = {'revision': sha, 'subject': title, 'glb_sha256': digest, 'bytes': len(raw),
               'integrated': True, 'path': ASSET}
        if digest in seen:
            row['exact_glb_duplicate_of'] = seen[digest]
        else:
            seen[digest] = sha
            row.update(inspect(raw))
        rows.append(row)
    legacy=[]
    for ref in ['14d2ad9f','5b6f408f']:
        sha=git('rev-parse',ref).decode().strip();path='public/assets/participants/light-sport-robot-lod0.glb'
        raw=git('show',sha+':'+path)
        legacy.append({'revision':sha,'subject':'Light Sport Robot '+ref,'glb_sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(raw),'integrated':True,'path':path,'kind':'browser-static-rig',**inspect(raw)})
    old='14d2ad9f^';source_paths=['src/render/firstPersonRenderer.ts','src/scenario/combatSliceScenarioV1.ts']
    procedural={'revision':git('rev-parse',old).decode().strip(),'type':'analytical primitives before GLB','sources':{p:hashlib.sha256(git('show',old+':'+p)).hexdigest() for p in source_paths},'native_review':'B01HistoryReviewTests recreates exact dimensions; Unity tessellation differs from historical Three.js. No original GLB existed; retained static pose, normalized height.'}
    result = {'ref': git('rev-parse', args.ref).decode().strip(),
              'selection': 'Every first-parent native body GLB change plus both integrated Light Sport Robot GLBs and pre-GLB procedural source',
              'technical_duplicates':'Payload signature changes include topology/UV/skin optimization; never counted as a new artistic generation.',
              'pose_limit':'Native Trooper versions use common persisted idle/run clips; incompatible browser rig and procedural target use authored rest pose with explicit labels.',
              'procedural':procedural,'legacy':legacy,'revisions':rows}
    if args.extract:
        args.extract.mkdir(parents=True,exist_ok=True);seen=set();items=[]
        for row in legacy+rows:
            signature=row.get('body_skin_payload_sha256')
            if not signature or signature in seen:continue
            seen.add(signature);name=row['revision'][:8]
            (args.extract/(name+'.glb')).write_bytes(git('show',row['revision']+':'+row['path']))
            items.append({'id':name,'revision':row['revision'],'label':row['subject'],'path':'Assets/'+args.extract.name+'/'+name+'.glb','kind':row.get('kind','native'),'sha256':row['glb_sha256']})
        (args.extract/'gallery.json').write_text(json.dumps({'items':items},indent=2)+'\n')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2)+'\n')
    print(json.dumps({'revisions': len(rows), 'unique_glbs': len(seen), 'output': str(args.output)}))


if __name__ == '__main__':
    main()
