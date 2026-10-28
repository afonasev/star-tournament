"""F17 assault art, derived from immutable F16. Blender -b -noaudio --python this_file.

Offline dimensions describe the selected asset, not gameplay/Lab parameters.
Rig, actions, equipment and hand/grip geometry are never transformed.
"""
from pathlib import Path
import bpy
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT/'scripts/vector_fighter/f16-orbital-hands.blend'
OUTPUT = SOURCE.with_name('f17-assault.blend')
ARMOR_TINT = (.14, .20, .25, 1)


def mix_material(source, name, tint):
    result = source.copy()
    result.name = name
    bs = next(n for n in result.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    base = bs.inputs['Base Color']
    if base.is_linked:
        image = base.links[0].from_socket
        multiply = result.node_tree.nodes.new('ShaderNodeMixRGB')
        multiply.blend_type = 'MULTIPLY'
        multiply.inputs[0].default_value = 1
        multiply.inputs[2].default_value = (*tint, 1)
        result.node_tree.links.new(image, multiply.inputs[1])
        result.node_tree.links.new(multiply.outputs[0], base)
    else:
        base.default_value = (*tint, 1)
    return result


def palette(skip_prefix=()):
    """Keep image bytes and relief; split neutral armor from assigned-color marks."""
    cache = {}
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH' or obj.name == 'weapon:joined' or 'gloves' in obj.name or obj.name.startswith(skip_prefix):
            continue
        # Repartition after runtime collapse too: the reducer can move a paint
        # seam. Restore its original role before cutting the final exact boundary.
        slots = list(obj.data.materials)
        for index, material in enumerate(slots):
            if material.name.startswith('vector-assault-armor'):
                original = 'vector-fighter-identity-forearm-f9' if 'forearm' in material.name else 'vector-fighter-identity'
                source_index = next(i for i, m in enumerate(slots) if m.name == original)
                for face in obj.data.polygons:
                    if face.material_index == index:
                        face.material_index = source_index
        cuts = []
        name = obj.name
        for token, levels in [('UpperArm contoured', [1.415]), ('costal shell', [1.235, 1.28]),
                              ('scapular shell', [1.39]), ('LowerArm', [1.18]),
                              ('UpperLeg', [.62, .66]), ('LowerLeg', [.30, .34]),
                              ('instep', [.09, .115])]:
            if token in name:
                cuts.extend((Vector((0, 0, z)), Vector((0, 0, 1))) for z in levels)
        if 'helmet inner' in name:
            cuts.extend((Vector((x, 0, 0)), Vector((1, 0, 0))) for x in [-.035, .035])
        if cuts:
            # Put an actual continuous edge on each paint boundary. Selecting
            # whole existing triangles produces saw-toothed marks after reduction.
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            for point, normal in cuts:
                bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                    plane_co=obj.matrix_world.inverted()@point,
                    plane_no=(obj.matrix_world.to_3x3().transposed()@normal).normalized(),
                    dist=.000001, clear_inner=False, clear_outer=False)
            bm.to_mesh(obj.data)
            bm.free()
            obj.data.update()
        originals = list(obj.data.materials)
        for old_slot, source in enumerate(originals):
            if not source.name.startswith('vector-fighter-identity'):
                continue
            if source.name not in cache:
                suffix = '-forearm' if 'forearm' in source.name else ''
                target = 'vector-assault-armor'+suffix
                cache[source.name] = bpy.data.materials.get(target) or mix_material(source, target, ARMOR_TINT[:3])
            if cache[source.name] not in list(obj.data.materials):
                obj.data.materials.append(cache[source.name])
            neutral = list(obj.data.materials).index(cache[source.name])
            for face in obj.data.polygons:
                if face.material_index != old_slot:
                    continue
                center = sum((obj.matrix_world@obj.data.vertices[i].co for i in face.vertices), Vector())/len(face.vertices)
                # Both shoulder caps wrap front/side/back. Chest, back and helmet
                # marks provide a second visible zone when the weapon occludes one.
                shoulder = 'UpperArm contoured' in obj.name and center.z > 1.415
                chest = 'pectoral shell' in obj.name or ('costal shell' in obj.name and 1.235 < center.z < 1.28)
                back = 'scapular shell' in obj.name and center.z > 1.39
                helmet = 'rear helmet panel' in obj.name or ('helmet inner' in obj.name and abs(center.x) < .035)
                forearm = 'LowerArm' in obj.name and center.z > 1.18
                leg_mark = ('UpperLeg' in obj.name and .62 < center.z < .66) or ('LowerLeg' in obj.name and .30 < center.z < .34) or ('instep' in obj.name and .09 < center.z < .115)
                if not (shoulder or chest or back or helmet or forearm or leg_mark):
                    face.material_index = neutral


def shape():
    for obj in bpy.context.scene.objects:
        if obj.type != 'MESH' or obj.name == 'weapon:joined' or 'gloves' in obj.name:
            continue
        name = obj.name
        upper = 'UpperArm' in name
        torso = any(part in name for part in ('torso fabric', 'pectoral', 'costal', 'scapular', 'waist yoke', 'clavicle'))
        helmet = any(part in name for part in ('helmet', 'visor', 'cheek', 'temple', 'crown', 'chin'))
        leg = any(part in name for part in ('trousers', 'UpperLeg', 'LowerLeg', 'knee', 'instep'))
        for vertex in obj.data.vertices:
            p = obj.matrix_world@vertex.co
            if upper:
                # Paused grip/contact points stay fixed. Grow only the upper sleeve
                # and pauldron, fading before the original elbow/forearm.
                blend = max(0, min(1, (p.z-1.20)/.24))
                p.x *= 1+.27*blend
                p.y = .05+(p.y-.05)*(1+.16*blend)
            elif torso:
                blend = max(0, min(1, (p.z-1.02)/.25))
                p.x *= 1.08+.12*blend
                p.y *= 1.16
                if 'torso fabric' in name:
                    p.z -= .045*max(0, min(1, (p.z-1.49)/.05))
            elif helmet:
                p.x *= 1.075
                p.y *= 1.07
                p.z -= .045
            elif leg:
                p.x *= 1.08
                p.y *= 1.07
            vertex.co = obj.matrix_world.inverted()@p
        obj.data.update()


def main():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    shape()
    palette()
    # Retain separated authored parts, skin weights, actions and editable maps.
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(OUTPUT))
    print('VECTOR_ASSAULT_SOURCE', OUTPUT)


if __name__ == '__main__':
    main()
