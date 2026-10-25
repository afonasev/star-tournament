"""Restore the original shotgun metal map without re-exporting geometry or animation."""
import copy
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'unity/Assets/StarTournament/Art'


def read(path):
    data = path.read_bytes()
    size, kind = struct.unpack_from('<II', data, 12)
    assert kind == 0x4E4F534A
    doc = json.loads(data[20:20 + size])
    length, kind = struct.unpack_from('<II', data, 20 + size)
    assert kind == 0x004E4942
    return doc, data[28 + size:28 + size + length]


def main():
    source, source_bin = read(ART / 'double-barrel-shotgun-lod0.glb')
    metal = next(m for m in source['materials'] if m['name'] == 'hardware')
    texture = source['textures'][metal['pbrMetallicRoughness']['baseColorTexture']['index']]
    image = source['images'][texture['source']]
    view = source['bufferViews'][image['bufferView']]
    payload = source_bin[view.get('byteOffset', 0):view.get('byteOffset', 0) + view['byteLength']]
    audit = {'texture_sha256': hashlib.sha256(payload).hexdigest(), 'files': []}
    for name in ('trooper-body-animated.glb', 'trooper-arms-animated.glb', 'vector-shotgun-lod0.glb', 'vector-shotgun-lod1.glb'):
        path = ART / name
        doc, binary = read(path)
        original = copy.deepcopy(doc)
        targets = [m for m in doc['materials'] if m['name'] in ('vector-chassis', 'vector-titanium')]
        assert len(targets) == 2
        if all('baseColorTexture' in m['pbrMetallicRoughness'] for m in targets):
            for material in targets:
                current = doc['textures'][material['pbrMetallicRoughness']['baseColorTexture']['index']]
                existing = doc['bufferViews'][doc['images'][current['source']]['bufferView']]
                offset = existing.get('byteOffset', 0)
                assert binary[offset:offset + existing['byteLength']] == payload, 'Unexpected existing metal map'
            audit['files'].append({'file': name, 'added_bytes': 0, 'geometry_rig_clips_unchanged': True, 'already_restored': True})
            continue
        sampler = copy.deepcopy(source['samplers'][texture['sampler']])
        samplers = doc.setdefault('samplers', [])
        if sampler not in samplers:
            samplers.append(sampler)
        # Append only: every original vertex, UV, index, skin, clip and image byte stays exact.
        binary += b'\0' * (-len(binary) % 4)
        doc['bufferViews'].append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(payload)})
        binary += payload
        doc['images'].append({'name': 'restored-shotgun-metal', 'bufferView': len(doc['bufferViews']) - 1, 'mimeType': image['mimeType']})
        doc['textures'].append({'source': len(doc['images']) - 1, 'sampler': samplers.index(sampler)})
        for material in targets:
            material['pbrMetallicRoughness']['baseColorTexture'] = {'index': len(doc['textures']) - 1}
        for key in ('nodes', 'meshes', 'accessors', 'skins', 'animations'):
            assert doc.get(key) == original.get(key), key
        doc['buffers'][0]['byteLength'] = len(binary)
        encoded = json.dumps(doc, separators=(',', ':')).encode()
        encoded += b' ' * (-len(encoded) % 4)
        binary += b'\0' * (-len(binary) % 4)
        output = struct.pack('<III', 0x46546C67, 2, 28 + len(encoded) + len(binary))
        output += struct.pack('<II', len(encoded), 0x4E4F534A) + encoded
        output += struct.pack('<II', len(binary), 0x004E4942) + binary
        previous_size = path.stat().st_size
        path.write_bytes(output)
        audit['files'].append({'file': name, 'added_bytes': len(output) - previous_size, 'geometry_rig_clips_unchanged': True})
    manifest_path = ART / 'asset-manifest.json'
    manifest = json.loads(manifest_path.read_text())
    for entry in manifest['entries']:
        path = ROOT / 'unity' / entry['path']
        if path.name in {item['file'] for item in audit['files']}:
            entry['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
    manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
    mounts_path = ART / 'trooper-mounts.json'
    mounts = json.loads(mounts_path.read_text())
    for key in ('body', 'arms'):
        mounts[key]['sha256'] = hashlib.sha256((ART / mounts[key]['path']).read_bytes()).hexdigest()
    mounts_path.write_text(json.dumps(mounts, indent=2) + '\n')
    directory = ROOT / '.local/shotgun-surface'
    directory.mkdir(parents=True, exist_ok=True)
    (directory / 'asset-audit.json').write_text(json.dumps(audit, indent=2) + '\n')
    print(json.dumps(audit, indent=2))


if __name__ == '__main__':
    main()
