"""Offline VECTOR runtime meshes; high-poly source and accepted glove anatomy stay intact.

blender -b -noaudio --python scripts/vector_fighter/optimize_runtime.py -- --variant balanced
Candidate-only by default. --promote updates the two shipping GLBs and their hash manifests.
No runtime distance switch, skeleton/clip/weapon edits or material baking is performed.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import struct
import sys

import bpy
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
SOURCE = ROOT / 'scripts/vector_fighter/f17-assault.blend'
ART = ROOT / 'unity/Assets/StarTournament/Art'
# Export resolutions are offline technical choices, never simulation or camera tuning.
RATIOS = {
    'conservative': {'world': {'cloth': .30, 'sleeve': .20, 'gloves': .35, 'shell': .32, 'pants': .65},
                     'view': {'cloth': 1, 'sleeve': .30, 'gloves': 1, 'shell': .55, 'pants': 1}},
    'balanced': {'world': {'cloth': .20, 'sleeve': .12, 'gloves': .22, 'shell': .24, 'pants': .50},
                 'view': {'cloth': 1, 'sleeve': .16, 'gloves': 1, 'shell': .38, 'pants': 1}},
}


def role(obj):
    if 'gloves' in obj.name:
        return 'gloves'
    if 'UpperArm fabric' in obj.name:
        return 'sleeve'
    if 'torso fabric' in obj.name:
        return 'cloth'
    if 'trousers' in obj.name:
        return 'pants'
    return 'shell'


def arm_part(obj):
    return 'LowerArm' in obj.name or 'gloves' in obj.name or ('UpperArm' in obj.name and 'fabric' in obj.name)


def triangles(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def optimize(obj, ratio):
    count = triangles(obj)
    used = {i for tri in obj.data.loop_triangles for i in tri.vertices}
    before = [obj.data.vertices[i].co.copy() for i in sorted(used)]
    # Keep small authored hardware, seams and every first-person glove vertex.
    if ratio < 1 and count >= 200:
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new('Runtime surface reduction', 'DECIMATE')
        modifier.decimate_type = 'COLLAPSE'
        modifier.ratio = ratio
        modifier.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    mesh = obj.data
    mesh.calc_loop_triangles()
    tree = BVHTree.FromPolygons([v.co for v in mesh.vertices], [tuple(t.vertices) for t in mesh.loop_triangles], all_triangles=True)
    distances = sorted(tree.find_nearest(v)[3] for v in before)
    return {'object': obj.name, 'role': role(obj), 'before': count, 'after': triangles(obj),
            'vertex_surface_error_m': {'p95': distances[int((len(distances)-1)*.95)], 'max': distances[-1]}}


def join(items, name, mesh_name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in items:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = sorted(items, key=lambda obj: obj.name)[0]
    bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    obj.name = name
    # World and view resolutions differ deliberately. Distinct mesh names prevent
    # TrooperPreparation from incorrectly coalescing same-name unequal skins.
    obj.data.name = mesh_name
    return obj


def persist_assault_factors(path):
    # Blender's exporter drops a linked MixRGB constant. Write the equivalent
    # standard glTF baseColorFactor, sharing the original image bytes/maps.
    from author_assault import ARMOR_TINT
    raw = path.read_bytes()
    size = struct.unpack_from('<I', raw, 12)[0]
    document = json.loads(raw[20:20+size])
    for material in document['materials']:
        if material['name'].startswith('vector-assault-armor'):
            material['pbrMetallicRoughness']['baseColorFactor'] = list(ARMOR_TINT)
    payload = json.dumps(document, separators=(',', ':')).encode()
    payload += b' ' * (-len(payload) % 4)
    tail = raw[20+size:]
    path.write_bytes(struct.pack('<4sII', b'glTF', 2, 20+len(payload)+len(tail))+
                     struct.pack('<I4s', len(payload), b'JSON')+payload+tail)


def export(kind, variant, output):
    import author_assault
    if kind == 'view':
        # The selected silhouette concerns the world body. First-person camera,
        # sleeves, hands and weapon retain the accepted F16 contact geometry.
        bpy.ops.wm.open_mainfile(filepath=str(author_assault.SOURCE))
    else:
        bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
    rig.data.pose_position = 'REST'
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH' and obj.name != 'weapon:joined']
    arms = [obj for obj in meshes if arm_part(obj)]
    selected = meshes if kind == 'world' else arms
    rows = [optimize(obj, RATIOS[variant][kind][role(obj)]) for obj in selected]
    # Paint boundaries are final geometry. Cutting them before collapse lets
    # that reducer drag colored edges into a saw-tooth pattern.
    author_assault.palette()
    for row, obj in zip(rows, selected):
        row['after'] = triangles(obj)
    equipment = [obj for obj in bpy.context.scene.objects if obj.type != 'MESH' or obj.name == 'weapon:joined']
    arm_mesh = join(arms, 'VECTOR world forearms' if kind == 'world' else 'vector-armored-hands', 'vector-'+kind+'-arms-runtime')
    objects = equipment + [arm_mesh]
    if kind == 'world':
        body_mesh = join([obj for obj in meshes if obj not in arms], 'VECTOR body', 'vector-world-body-runtime')
        objects.append(body_mesh)
    # Armature modifiers remain present. Collapse runs on the bind mesh, not on a
    # sampled animation pose. Persisted Unity clips remain the animation authority.
    rig.data.pose_position = 'POSE'
    spec = importlib.util.spec_from_file_location('vector_build', ROOT/'scripts/vector_fighter/build.py')
    builder = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(builder)
    builder.rig = rig
    builder.export(output, objects)
    persist_assault_factors(output)
    return rows


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--variant', choices=RATIOS, required=True)
    parser.add_argument('--promote', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    output = ROOT/'.local/vector-runtime'/args.variant
    output.mkdir(parents=True, exist_ok=True)
    rows = {}
    for kind, filename in [('world', 'candidate-body.glb'), ('view', 'candidate-arms.glb')]:
        rows[kind] = export(kind, args.variant, output/filename)
    report = {'variant': args.variant, 'source': str(SOURCE.relative_to(ROOT)),
              'source_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(), 'objects': rows,
              'skin_triangles': {kind: {'before': sum(r['before'] for r in values), 'after': sum(r['after'] for r in values)} for kind, values in rows.items()},
              'first_person_glove_geometry': 'unchanged', 'weapons_textures_rig_clips': 'unchanged',
              'materials': 'F17 neutral armor plus assigned identity marks'}
    (output/'optimization.json').write_text(json.dumps(report, indent=2)+'\n')
    if args.promote:
        files = {'player-body': ('body', 'candidate-body.glb'), 'player-hands': ('arms', 'candidate-arms.glb')}
        manifest = json.loads((ART/'asset-manifest.json').read_text())
        mounts = json.loads((ART/'trooper-mounts.json').read_text())
        for key, (kind, filename) in files.items():
            target = ART/('trooper-'+kind+'-animated.glb')
            shutil.copyfile(output/filename, target)
            digest = hashlib.sha256(target.read_bytes()).hexdigest()
            entry = next(e for e in manifest['entries'] if e['key'] == key)
            entry['sha256'] = digest
            entry['source'] = 'VECTOR F17 assault (concept 1); offline '+args.variant+' runtime mesh; scripts/vector_fighter/optimize_runtime.py; F16 anatomy retained'
            mounts[kind]['sha256'] = digest
        (ART/'asset-manifest.json').write_text(json.dumps(manifest, indent=2)+'\n')
        (ART/'trooper-mounts.json').write_text(json.dumps(mounts, indent=2)+'\n')
    print('VECTOR_RUNTIME_OPTIMIZATION', json.dumps(report['skin_triangles']))


if __name__ == '__main__':
    main()
