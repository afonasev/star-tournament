"""Lossless GLB weapon optimization: exact attribute welding and triangle/cache ordering.

No quantization, simplification, texture/material changes or transform baking.
Only opaque triangle primitives without morphs are eligible. The editable sources
remain authoritative. Every promoted primitive is checked by its oriented triangle
multiset (all vertex attributes, including normals, UVs and skin weights).
"""
import argparse
from collections import Counter
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import struct

WIDTH = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4, 'MAT4': 16}
SIZE = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}
ART = Path('unity/Assets/StarTournament/Art')
FILES = [name + '-lod' + str(lod) + '.glb' for name in
         ['automatic-rifle', 'vector-shotgun', 'pulse-launcher', 'cutter', 'forge-plasma'] for lod in [0, 1]]
FILES += ['pulse-projectile.glb', 'trooper-body-animated.glb', 'trooper-arms-animated.glb']


def read(raw):
    assert raw[:4] == b'glTF'
    length = struct.unpack_from('<I', raw, 12)[0]
    doc = json.loads(raw[20:20 + length])
    offset = 20 + length
    assert raw[offset + 4:offset + 8] == b'BIN\0'
    return doc, raw[offset + 8:]


def values(doc, binary, index):
    a = doc['accessors'][index]
    assert 'sparse' not in a
    v = doc['bufferViews'][a['bufferView']]
    width = WIDTH[a['type']] * SIZE[a['componentType']]
    offset = v.get('byteOffset', 0) + a.get('byteOffset', 0)
    stride = v.get('byteStride', width)
    return [bytes(binary[offset + i * stride:offset + i * stride + width]) for i in range(a['count'])]


def indices(doc, binary, primitive):
    a = doc['accessors'][primitive['indices']]
    code = {5121: 'B', 5123: 'H', 5125: 'I'}[a['componentType']]
    return [struct.unpack('<' + code, v)[0] for v in values(doc, binary, primitive['indices'])]


def misses(order, size):
    cache = []
    count = 0
    for vertex in order:
        if vertex in cache:
            cache.remove(vertex)
        else:
            count += 1
        cache.insert(0, vertex)
        del cache[size:]
    return count


def reorder(order, count, size=24):
    triangles = [tuple(order[i:i + 3]) for i in range(0, len(order), 3)]
    adjacency = [set() for _ in range(count)]
    for i, tri in enumerate(triangles):
        for vertex in tri:
            adjacency[vertex].add(i)
    remaining = set(range(len(triangles)))
    cache, result = [], []
    cursor = 0
    while remaining:
        candidates = set().union(*(adjacency[v] for v in cache)) if cache else set()
        if candidates:
            chosen = max(candidates, key=lambda i: (sum(v in cache for v in triangles[i]),
                         -sum(len(adjacency[v]) for v in set(triangles[i])), -i))
        else:
            while cursor not in remaining:
                cursor += 1
            chosen = cursor
        tri = triangles[chosen]
        result.extend(tri)
        remaining.remove(chosen)
        for v in set(tri):
            adjacency[v].remove(chosen)
        for v in tri:
            if v in cache:
                cache.remove(v)
            cache.insert(0, v)
        del cache[size:]
    # Never trade a regression in either modeled cache for another metric.
    return result if all(misses(result, s) <= misses(order, s) for s in (16, 32)) else order


def signature(attributes, order):
    keys = sorted(attributes)
    vertex = [b''.join(attributes[k][i] for k in keys) for i in range(len(attributes[keys[0]]))]
    return Counter(hashlib.sha256(b''.join(vertex[v] for v in order[i:i+3])).digest()
                   for i in range(0, len(order), 3))


def compact(doc, binary):
    # Standard glTF accessor owners. Other extensions are retained byte-for-byte;
    # unsupported geometry compression/morph layouts never reach this routine.
    refs = []
    for mesh in doc['meshes']:
        for p in mesh['primitives']:
            refs.extend((p['attributes'], key) for key in p['attributes'])
            if 'indices' in p:
                refs.append((p, 'indices'))
            for target in p.get('targets', []):
                refs.extend((target, key) for key in target)
    for skin in doc.get('skins', []):
        if 'inverseBindMatrices' in skin:
            refs.append((skin, 'inverseBindMatrices'))
    for animation in doc.get('animations', []):
        for sampler in animation['samplers']:
            refs.extend([(sampler, 'input'), (sampler, 'output')])
    used = sorted({obj[key] for obj, key in refs})
    mapping = {old: new for new, old in enumerate(used)}
    doc['accessors'] = [doc['accessors'][i] for i in used]
    for obj, key in refs:
        obj[key] = mapping[obj[key]]
    # Find every bufferView reference, including preserved images/extensions.
    refs = []
    def visit(value):
        if isinstance(value, dict):
            for key, item in value.items():
                if key == 'bufferView':
                    refs.append((value, key))
                else:
                    visit(item)
        elif isinstance(value, list):
            for item in value:
                visit(item)
    visit({k: v for k, v in doc.items() if k != 'bufferViews'})
    used = sorted({obj[key] for obj, key in refs})
    mapping = {old: new for new, old in enumerate(used)}
    result, views = bytearray(), []
    for old in used:
        v = deepcopy(doc['bufferViews'][old])
        assert v['buffer'] == 0
        result.extend(b'\0' * (-len(result) % 4))
        start = v.get('byteOffset', 0)
        payload = binary[start:start + v['byteLength']]
        v['byteOffset'] = len(result)
        result.extend(payload)
        views.append(v)
    for obj, key in refs:
        obj[key] = mapping[obj[key]]
    doc['bufferViews'] = views
    doc['buffers'] = [{'byteLength': len(result)}]
    return result


def optimize(raw, embedded=False):
    doc, binary = read(raw)
    binary = bytearray(binary)
    rows = []
    def append_accessor(template, payload, count, target):
        binary.extend(b'\0' * (-len(binary) % 4))
        view = len(doc['bufferViews'])
        doc['bufferViews'].append({'buffer': 0, 'byteOffset': len(binary), 'byteLength': len(payload), 'target': target})
        binary.extend(payload)
        a = deepcopy(template)
        a.update(bufferView=view, count=count)
        a.pop('byteOffset', None)
        index = len(doc['accessors'])
        doc['accessors'].append(a)
        return index
    for mesh in doc['meshes']:
        if embedded and mesh.get('name') != 'VectorPetrolDetailed':
            continue
        for primitive in mesh['primitives']:
            material = doc.get('materials', [])[primitive['material']]
            if material.get('alphaMode', 'OPAQUE') != 'OPAQUE':
                continue
            assert primitive.get('mode', 4) == 4 and not primitive.get('targets') and not primitive.get('extensions')
            attrs = {k: values(doc, binary, a) for k, a in primitive['attributes'].items()}
            order = indices(doc, binary, primitive)
            assert len(order) % 3 == 0
            keys = sorted(attrs)
            count = len(attrs[keys[0]])
            unique, remap, source = {}, [], []
            for i in range(count):
                key = tuple(attrs[k][i] for k in keys)
                if key not in unique:
                    unique[key] = len(source)
                    source.append(i)
                remap.append(unique[key])
            welded = [remap[i] for i in order]
            optimized = reorder(welded, len(source))
            # First-use layout improves fetch locality without changing attribute bytes.
            fetch = list(dict.fromkeys(optimized))
            inverse = {v: i for i, v in enumerate(fetch)}
            final = [inverse[v] for v in optimized]
            selected = [source[i] for i in fetch]
            after = {k: [attrs[k][i] for i in selected] for k in keys}
            assert signature(attrs, order) == signature(after, final), 'Oriented geometry changed'
            row = {'mesh': mesh.get('name'), 'material': material['name'], 'triangles': len(order)//3,
                   'vertices_before': count, 'vertices_after': len(selected),
                   'cache16_before': misses(order, 16), 'cache16_after': misses(final, 16),
                   'cache32_before': misses(order, 32), 'cache32_after': misses(final, 32),
                   'oriented_attribute_triangle_multiset': 'exact'}
            rows.append(row)
            for k in keys:
                template = doc['accessors'][primitive['attributes'][k]]
                primitive['attributes'][k] = append_accessor(template, b''.join(after[k]), len(selected), 34962)
            template = deepcopy(doc['accessors'][primitive['indices']])
            template.pop('min', None); template.pop('max', None)
            template['componentType'] = 5123 if len(selected) <= 65535 else 5125
            code = 'H' if template['componentType'] == 5123 else 'I'
            primitive['indices'] = append_accessor(template, struct.pack('<' + code*len(final), *final), len(final), 34963)
    binary = compact(doc, binary)
    js = json.dumps(doc, separators=(',', ':'), ensure_ascii=False).encode()
    js += b' ' * (-len(js) % 4)
    binary.extend(b'\0' * (-len(binary) % 4))
    result = struct.pack('<4sII', b'glTF', 2, 28 + len(js) + len(binary))
    result += struct.pack('<I4s', len(js), b'JSON') + js + struct.pack('<I4s', len(binary), b'BIN\0') + binary
    return result, rows


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--report', type=Path, required=True)
    parser.add_argument('--promote', action='store_true')
    args = parser.parse_args()
    report = []
    for filename in FILES:
        path = ART/filename
        raw = path.read_bytes()
        candidate, rows = optimize(raw, filename.startswith('trooper-'))
        report.append({'file': filename, 'sha256_before': hashlib.sha256(raw).hexdigest(),
                       'sha256_after': hashlib.sha256(candidate).hexdigest(),
                       'bytes_before': len(raw), 'bytes_after': len(candidate), 'primitives': rows})
        if args.promote:
            path.write_bytes(candidate)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, ensure_ascii=False)+'\n')
    if args.promote:
        for name in ['asset-manifest.json', 'trooper-mounts.json']:
            path = ART/name
            doc = json.loads(path.read_text())
            replacements = {row['sha256_before']: row['sha256_after'] for row in report}
            def update(obj):
                if isinstance(obj, dict):
                    for k, v in obj.items():
                        if k == 'sha256' and v in replacements:
                            obj[k] = replacements[v]
                        else:
                            update(v)
                elif isinstance(obj, list):
                    for v in obj:
                        update(v)
            update(doc)
            path.write_text(json.dumps(doc, indent=2, ensure_ascii=False)+'\n')
    print(json.dumps([{k: v for k, v in row.items() if k != 'primitives'} for row in report], indent=2))


if __name__ == '__main__':
    main()
