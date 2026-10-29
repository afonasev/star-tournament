"""F01 approved military helmet, over the immutable color-3 F23 source.

Blender -b -noaudio --python-exit-code 1 --python scripts/vector_fighter/military_f23_helmet.py
Only rigid Head parts are rebuilt. Body geometry, UVs, materials and weights
are verified exactly before saving. Coordinates are authored metres, Z up.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).parent))
import build as g
import reconstruct_f22 as cage
import author_reference as r
from round_f23_helmet import geometry

SOURCE = ROOT / 'scripts/vector_fighter/f23-round-color.blend'
TARGET = ROOT / 'scripts/vector_fighter/f23-military-color.blend'
OUT = ROOT / '.local/f23-military'


def fingerprint(obj):
    return (geometry(obj), tuple(m.name for m in obj.data.materials),
            tuple(p.material_index for p in obj.data.polygons),
            tuple(tuple(x.uv) for x in obj.data.uv_layers.active.data))


def ring(a, width, front, rear, z):
    # Rounded rectangular horizontal section; no common ellipsoid for the face.
    sn, cs = math.sin(a), math.cos(a)
    return Vector((width * math.copysign(abs(sn) ** .87, sn),
                   .018 - (front if cs >= 0 else rear) * math.copysign(abs(cs) ** .87, cs), z))


def crown(q):
    a, e = q
    return ring(a, .108 * math.cos(e), .128 * math.cos(e),
                .112 * math.cos(e), 1.675 + .068 * math.sin(e) ** .8)


def face(q):
    a, z = q
    return ring(a, .105, .130, .108, z)


def outward(surface, q):
    a, b = q
    u = surface((a + .0001, b)) - surface((a - .0001, b))
    v = surface((a, b + .0001)) - surface((a, b - .0001))
    return u.cross(v).normalized()


def panel(name, coords, material, surface=face):
    obj = cage.shellpanel('military helmet ' + name, coords, 'Head', surface,
                          lambda q: outward(surface, q), material)
    obj.name = obj.name.replace('F22', 'F23M')
    return obj


def plate(name, points, material, axis=(0, -1, 0), hardware=True):
    obj = cage.solid('military helmet ' + name, points, 'Head', material,
                     axis=axis, depth=.009, crown=.0015, inset=.10, hardware=hardware)
    obj.name = obj.name.replace('F22', 'F23M')
    return obj


def closed_sheet(name, verts, faces, material, smooth=True):
    """Regular authored topology avoids clipped-grid slivers on curved glass."""
    obj = g.finish('VECTOR F23M ' + name, verts, faces, material, 'Head')
    for p in obj.data.polygons:
        p.use_smooth = smooth
    obj.modifiers.remove(obj.modifiers['Skin'])
    obj.data.materials.append(r.edge)
    obj.data.materials.append(r.seal)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new('Manufactured wall', 'SOLIDIFY')
    mod.thickness = .005
    mod.offset = -1
    mod.material_offset = 2
    mod.material_offset_rim = 1
    bpy.ops.object.modifier_apply(modifier=mod.name)
    mod = obj.modifiers.new('Machined edge', 'BEVEL')
    mod.width = .0005
    mod.segments = 2
    mod.limit_method = 'ANGLE'
    mod.angle_limit = .6
    bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.select_set(False)
    mod = obj.modifiers.new('Skin', 'ARMATURE')
    mod.object = g.rig
    return obj


def curved_band(name, material, lower, upper, depth=0):
    verts, faces = [], []
    for j in range(9):
        for i in range(49):
            a = -1.26 + 2.52*i/48
            z = lower(a) + (upper(a)-lower(a))*j/8
            p = face((a, z))
            p += Vector((p.x, p.y-.018, 0)).normalized()*depth
            verts.append(p)
    for j in range(8):
        for i in range(48):
            k = j*49+i
            faces.append((k, k+1, k+50, k+49))
    return closed_sheet(name, verts, faces, material)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    source_hash = hashlib.sha256(SOURCE.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    g.rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    g.rig.data.pose_position = 'REST'
    g.parts = []
    for key, name in [('steel', 'vector-reference-coated-steel'),
                      ('paint', 'vector-fighter-identity-reference'),
                      ('edge', 'vector-reference-edge-metal'),
                      ('seal', 'vector-reference-flexible-seals'),
                      ('glass', 'vector-reference-opaque-visor')]:
        setattr(r, key, bpy.data.materials[name])
    kept, removed = {}, []
    for obj in list(bpy.context.scene.objects):
        if obj.type != 'MESH':
            continue
        head = len(obj.vertex_groups) == 1 and obj.vertex_groups[0].name == 'Head'
        if head and 'comms' not in obj.name:
            removed.append(obj.name)
            bpy.data.objects.remove(obj, do_unlink=True)
        else:
            kept[obj.name] = fingerprint(obj)
    cage.PANEL_GRID = 12
    cage.solids.clear()
    g.ellipsoid('VECTOR F23M recessed helmet liner', (0, .018, 1.625),
                (.093, .098, .105), r.seal, 'Head')
    # Low swept crown. Broad curved panels preserve the approved shell.
    for i, (a, b) in enumerate([(-math.pi, -1.75), (-1.75, 1.75), (1.75, math.pi)]):
        panel('crown ' + str(i), [(a, .06), (b, .06), (b, 1.565), (a, 1.565)], r.paint, crown)
    curved_band('visor', r.glass, lambda a: 1.603+.023*(a/1.26)**2, lambda a: 1.669)
    # A shallow protective brow follows the visor, rather than a floating box.
    curved_band('brow', r.steel, lambda a: 1.671, lambda a: 1.691, .007)
    # Chin is a separately manufactured broad plate, with two real open slots
    # on each side; dark recessed grilles sit behind the openings.
    def chin(q):
        x, z = q
        return Vector((x, -.120 + .45 * abs(x) + max(0, 1.55-z) * .28, z))
    for s in (-1, 1):
        # Side silhouette: distinct cheek and chin volume; no tapered egg tip.
        plate('cheek ' + str(s), [(s*.060, -.090, 1.608), (s*.094, -.057, 1.638),
              (s*.112, -.013, 1.626), (s*.108, .016, 1.576),
              (s*.078, -.015, 1.540), (s*.052, -.089, 1.535)], r.steel,
              axis=(s*.65, -.75, 0))
        for z in (1.5535, 1.5715):
            # Recessed horizontal ribs behind the machined apertures.
            for dz in (-.0015, .0015):
                pts = [chin((s*x, z+dz)) + Vector((0, .004, 0)) for x in (.016, .030, .044)]
                r.line('F23M recessed grille', pts, 'Head', r.seal, .00065)
        panel('temple ' + str(s), [(s*1.28, 1.680), (s*1.86, 1.680),
              (s*1.93, 1.602), (s*1.39, 1.586)], r.paint)
        panel('rear upper ' + str(s), [(s*1.87, 1.680), (s*3.14, 1.680),
              (s*3.14, 1.611), (s*2.02, 1.602)], r.paint)
        panel('nape ' + str(s), [(s*1.98, 1.597), (s*3.14, 1.607),
              (s*3.14, 1.552), (s*2.18, 1.544), (s*1.65, 1.563)], r.paint)
        plate('temple mount ' + str(s), [(s*.095, -.068, 1.674),
              (s*.111, -.026, 1.684), (s*.113, -.023, 1.671),
              (s*.099, -.064, 1.662)], r.steel, axis=(s, -.3, 0))
    xs = [-.055, -.044, -.016, 0, .016, .044, .055]
    zs = [1.532, 1.550, 1.557, 1.568, 1.575, 1.603]
    verts = [chin((x, z)) for z in zs for x in xs]
    faces = []
    for j in range(len(zs)-1):
        for i in range(len(xs)-1):
            if j in (1, 3) and i in (1, 4):
                continue
            k = j*len(xs)+i
            faces.append((k, k+1, k+1+len(xs), k+len(xs)))
    closed_sheet('one-piece vented chin', verts, faces, r.steel, False)
    for s in (-1, 1):
        for z in (1.539, 1.594):
            p = chin((s*.049, z))
            r.fastener('F23M chin fastener', p, (0, -1, 0), 'Head')
    assert all(fingerprint(bpy.data.objects[n]) == v for n, v in kept.items()), 'Non-head/color-3 invariant'
    report = {'source': str(SOURCE.relative_to(ROOT)), 'source_sha256': source_hash,
              'removed_head_objects': removed, 'preserved_objects': len(kept),
              'nonhead_geometry_weights_transforms_uvs_materials_exact': True,
              'new_materials': 0, 'new_textures': 0, 'helmet_grid': cage.PANEL_GRID,
              'design': 'Approved F01 military variant 2; low crown, brow, separate cheek/chin, recessed vents; color 3 unchanged'}
    (OUT / 'authoring.json').write_text(json.dumps(report, indent=2) + '\n')
    g.rig.data.pose_position = 'POSE'
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(TARGET), compress=True)
    print('F23_MILITARY', report)


if __name__ == '__main__':
    main()
