"""F16: restore selected Orbital gloves on the retained VECTOR suit and rig.
Run with Blender; immutable input assets are read from Git, never overwritten.
"""
import bpy, bmesh, importlib.util, json, subprocess
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '.local/vector-f16'
OUT.mkdir(parents=True, exist_ok=True)
BASE = 'a095e6ef17c9cb9e5e373101e3fcb27896567716'
ORBITAL = '00fbd33209eb755dd5f86499aa8ea12867a2efd0'
source = OUT / 'vector-source.blend'
source.write_bytes(subprocess.check_output(['git', 'show', BASE + ':scripts/vector_fighter/f15-hand-anatomy.blend'], cwd=ROOT))
bpy.ops.wm.open_mainfile(filepath=str(source))
rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
actions = set(bpy.data.actions)
old = bpy.data.objects['VECTOR articulated gloves']
bpy.data.objects.remove(old, do_unlink=True)
before = set(bpy.context.scene.objects)
asset = OUT / 'orbital-arms.glb'
asset.write_bytes(subprocess.check_output(['git', 'show', ORBITAL + ':unity/Assets/StarTournament/Art/trooper-arms-animated.glb'], cwd=ROOT))
bpy.ops.import_scene.gltf(filepath=str(asset))
added = set(bpy.context.scene.objects) - before
source_rig = next(o for o in added if o.type == 'ARMATURE')
for bone in source_rig.data.bones:
    assert bone.name in rig.data.bones, bone.name
    assert max(abs(bone.matrix_local[i][j] - rig.data.bones[bone.name].matrix_local[i][j]) for i in range(4) for j in range(4)) < .00001, bone.name
hands = []
audit = []
for obj in list(added):
    if obj.type != 'MESH' or not obj.find_armature() or obj.name.startswith('weapon:'):
        continue
    world = obj.matrix_world.copy()
    names = {g.index: g.name for g in obj.vertex_groups}
    # Same anatomical cut used by the original VECTOR importer. Preserve
    # original vertices, UVs, normals, materials and all retained skin weights.
    keep = {v.index for v in obj.data.vertices if sum(g.weight for g in v.groups if any(names[g.group].startswith(s+d) for s in ['Left', 'Right'] for d in ['Hand', 'Thumb', 'Index', 'Middle', 'Ring', 'Little'])) > .10}
    # Remove the old sleeve behind the wrist, with a small overlap inside
    # the VECTOR cuff. No retained glove vertex is moved.
    frames = {}
    for side in ['Left', 'Right']:
        wrist = source_rig.data.bones[side+'Hand'].head_local
        end = sum((source_rig.data.bones[side+d+'Proximal'].head_local for d in ['Index','Middle','Ring','Little']), Vector())/4
        frames[side] = (wrist, (end-wrist).normalized())
    transform = source_rig.matrix_world.inverted() @ obj.matrix_world
    def beyond_cuff(vertex):
        point = transform @ vertex.co
        wrist, axis = frames['Left' if point.x > 0 else 'Right']
        return (point-wrist).dot(axis) >= -.004
    keep = {i for i in keep if beyond_cuff(obj.data.vertices[i])}
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context='VERTS')
    bm.to_mesh(obj.data); bm.free()
    if not obj.data.polygons:
        continue
    # Original glove textures already contain the visible stitching. The old
    # extra raised thread islands bridge independently posed digits and create
    # spikes with current grips; omit only those islands, not anatomical cloth.
    stitches = {i for p in obj.data.polygons if 'seam' in obj.data.materials[p.material_index].name for i in p.vertices}
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index in stitches], context='VERTS')
    bm.to_mesh(obj.data); bm.free()
    # Distinct authored roles must not alias through the prefab importer's
    # legacy .001 suffix coalescing (glove texture versus VECTOR sleeve).
    for mat in obj.data.materials:
        base = mat.name.split('.')[0]
        mat.name = 'vector-ceramic-orbital' if base == 'vector-ceramic' else 'orbital-restored-'+base
    obj.parent = rig; obj.matrix_world = world
    for mod in obj.modifiers:
        if mod.type == 'ARMATURE': mod.object = rig
    obj.name = 'VECTOR restored Orbital gloves'
    hands.append(obj)
    audit.append({'vertices': len(obj.data.vertices), 'faces': len(obj.data.polygons), 'separate_thread_vertices_omitted':len(stitches), 'cuff_cut_m':-.004, 'materials': [m.name for m in obj.data.materials]})
assert len(hands) == 1, len(hands)
for obj in added:
    if obj not in hands: bpy.data.objects.remove(obj, do_unlink=True)
for action in set(bpy.data.actions) - actions: bpy.data.actions.remove(action)
bpy.context.view_layer.update()
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'scripts/vector_fighter/f16-orbital-hands.blend'), compress=True)
spec = importlib.util.spec_from_file_location('vector_build', ROOT/'scripts/vector_fighter/build.py')
builder = importlib.util.module_from_spec(spec); spec.loader.exec_module(builder); builder.rig = rig
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.name != 'weapon:joined']
arm_parts = [o for o in meshes if 'LowerArm' in o.name or 'gloves' in o.name or ('UpperArm' in o.name and 'fabric' in o.name)]
body_parts = [o for o in meshes if o not in arm_parts]
equipment = [o for o in bpy.context.scene.objects if o.type != 'MESH' or o.name == 'weapon:joined']
def join(items, name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in items: obj.select_set(True)
    bpy.context.view_layer.objects.active = items[0]; bpy.ops.object.join(); items[0].name = name
    return items[0]
arms = join(arm_parts, 'VECTOR world forearms'); body = join(body_parts, 'VECTOR body')
builder.export(OUT/'candidate-body.glb', equipment+[arms, body])
arms.name = 'vector-armored-hands'
builder.export(OUT/'candidate-arms.glb', equipment+[arms])
(OUT/'source-audit.json').write_text(json.dumps({'vector_baseline': BASE, 'orbital_baseline': ORBITAL, 'gloves': audit, 'bind_skeleton': 'matching within 1e-5', 'anatomical_cloth_vertex_edits': 0, 'raised_stitches': 'separate thread islands omitted; original textured stitches retained', 'rig_actions': 'retained'}, indent=2)+'\n')
print('F16_ORBITAL_GLOVES_EXPORTED')
