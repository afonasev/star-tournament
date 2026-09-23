"""Author three original, two-sided Crystal Runes pickup GLBs with Blender.

Run: blender -b --factory-startup --python generate-crystal-rune-pickups.py
The output is deterministic and contains render geometry only (no colliders).
"""

import math
from pathlib import Path

import bpy
from mathutils import Vector


ART = Path(__file__).resolve().parent
SILHOUETTE_SCALE = 0.82
DEPTH_SCALE = 0.40


def material(name, color, alpha=1.0, emission=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, alpha)
    mat.use_nodes = True
    node = mat.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value = (*color, 1)
    node.inputs["Metallic"].default_value = 0.07
    node.inputs["Roughness"].default_value = 0.22
    node.inputs["Alpha"].default_value = alpha
    node.inputs["Emission Color"].default_value = (*color, 1)
    node.inputs["Emission Strength"].default_value = emission
    if alpha < 1:
        mat.surface_render_method = "BLENDED"
        mat.use_transparency_overlap = False
    return mat


def prism(root, name, outline, depth, z, mat, bevel=0.022):
    """Watertight polygon extruded about z; visible detail is mirrored on both sides."""
    outline = [(x * SILHOUETTE_SCALE, y * SILHOUETTE_SCALE) for x, y in outline]
    depth *= DEPTH_SCALE
    z *= DEPTH_SCALE
    bevel *= DEPTH_SCALE
    n = len(outline)
    # Blender is Z-up; its glTF exporter maps Blender Z to Unity Y.
    # Author the upright silhouette in XZ, with thickness on Blender Y.
    verts = [(x, -(z + depth / 2), y) for x, y in outline]
    verts += [(x, -(z - depth / 2), y) for x, y in outline]
    faces = [tuple(range(n)), tuple(range(2 * n - 1, n - 1, -1))]
    for i in range(n):
        j = (i + 1) % n
        faces.append((i, i + n, j + n, j))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.materials.append(mat)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.parent = root
    bevel_mod = obj.modifiers.new("soft crystal bevel", "BEVEL")
    bevel_mod.width = bevel
    bevel_mod.segments = 3
    bevel_mod.affect = "EDGES"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=bevel_mod.name)
    obj.select_set(False)
    for face in obj.data.polygons:
        face.use_smooth = True
    return obj


def raised_path(root, name, xy, z, radius, mat, closed=False):
    xy = [(x * SILHOUETTE_SCALE, y * SILHOUETTE_SCALE) for x, y in xy]
    z *= DEPTH_SCALE
    radius *= 0.65
    for side, z_side in (("front", z), ("rear", -z)):
        curve = bpy.data.curves.new(name + "-" + side, "CURVE")
        curve.dimensions = "3D"
        curve.resolution_u = 2
        curve.bevel_depth = radius
        curve.bevel_resolution = 3
        spline = curve.splines.new("POLY")
        spline.points.add(len(xy) - 1)
        for point, (x, y) in zip(spline.points, xy):
            point.co = (x, -z_side, y, 1)
        spline.use_cyclic_u = closed
        obj = bpy.data.objects.new(name + "-" + side, curve)
        bpy.context.collection.objects.link(obj)
        obj.parent = root
        obj.data.materials.append(mat)
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        bpy.ops.object.convert(target="MESH")
        obj.select_set(False)


def root(name):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    return obj


def armor():
    base = root("armor-pickup")
    glass = material("armor-cyan-glass", (0.02, 0.50, 0.82), 0.82, 0.12)
    core = material("armor-inner-crystal", (0.12, 0.80, 0.96), 0.92, 0.23)
    trace = material("armor-white-cyan-trace", (0.63, 0.97, 1.00), 1, 1.8)
    outer = [(-.44, 1.02), (0, 1.17), (.44, 1.02), (.42, .52), (.24, .16), (0, .03), (-.24, .16), (-.42, .52)]
    inner = [(-.31, .91), (0, 1.00), (.31, .91), (.28, .52), (.16, .29), (0, .17), (-.16, .29), (-.28, .52)]
    prism(base, "armor-shield-body", outer, .30, 0, glass)
    prism(base, "armor-shield-inset", inner, .18, 0, core, .015)
    raised_path(base, "armor-perimeter-light", outer, .162, .012, trace, True)
    hexagon = [(math.cos(math.radians(30 + 60 * i)) * .19,
                .64 + math.sin(math.radians(30 + 60 * i)) * .19) for i in range(6)]
    raised_path(base, "armor-hex-reinforcement", hexagon, .174, .023, trace, True)
    raised_path(base, "armor-center-spine", [(0, .38), (0, .20)], .174, .012, trace)
    return base


def speed():
    base = root("speed-pickup")
    glass = material("speed-gold-glass", (0.92, .48, .025), .84, .12)
    inset = material("speed-inner-crystal", (1.0, .71, .08), .94, .25)
    trace = material("speed-white-gold-trace", (1.0, .94, .61), 1, 1.7)
    chevrons = [
        [(-.51, 1.10), (-.28, 1.10), (.12, .62), (-.28, .12), (-.51, .12), (-.10, .62)],
        [(-.10, 1.10), (.13, 1.10), (.52, .62), (.13, .12), (-.10, .12), (.31, .62)],
    ]
    for i, shape in enumerate(chevrons):
        prism(base, "speed-chevron-%d" % (i + 1), shape, .30, 0, glass)
        raised_path(base, "speed-chevron-edge-%d" % (i + 1), shape, .162, .011, trace, True)
    bolt = [(-.045, .91), (.13, .91), (.035, .68), (.18, .68), (-.04, .34), (.01, .56), (-.13, .56)]
    prism(base, "speed-lightning-core", bolt, .35, 0, inset, .013)
    raised_path(base, "speed-lightning-light", bolt, .188, .018, trace, True)
    return base


def damage():
    base = root("damage-pickup")
    glass = material("damage-crimson-glass", (.82, .035, .055), .84, .14)
    inset = material("damage-orange-crystal", (1.0, .32, .04), .93, .28)
    trace = material("damage-white-orange-trace", (1.0, .78, .40), 1, 1.8)
    left = [(-.18, .92), (-.40, .67), (-.31, .51), (-.50, .67), (-.46, .28), (-.28, .08), (-.04, .03), (-.07, .33), (-.18, .53), (-.06, .65)]
    right = [(.13, 1.17), (.40, .77), (.31, .58), (.50, .69), (.46, .32), (.28, .08), (.04, .03), (.07, .36), (.20, .55), (.04, .72)]
    for name, shape in (("left", left), ("right", right)):
        prism(base, "damage-flame-" + name, shape, .30, 0, glass)
        raised_path(base, "damage-flame-edge-" + name, shape, .162, .011, trace, True)
    inner = [(-.04, .95), (.075, .75), (-.015, .64), (.07, .44), (-.02, .28), (.015, .54), (-.085, .69)]
    prism(base, "damage-split-core", inner, .35, 0, inset, .014)
    raised_path(base, "damage-split-light", inner, .188, .018, trace, True)
    return base


def export_one(obj, filename):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    for child in obj.children_recursive:
        child.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(filepath=str(ART / filename), export_format="GLB", use_selection=True,
                              export_cameras=False, export_lights=False, export_extras=True)
    bpy.ops.object.select_all(action="DESELECT")


bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
models = (armor(), speed(), damage())
for obj, filename in zip(models, ("armor-pickup-crystal-runes-lod0.glb",
                                  "speed-pickup-crystal-runes-lod0.glb",
                                  "damage-pickup-crystal-runes-lod0.glb")):
    export_one(obj, filename)
print("CRYSTAL_RUNE_PICKUPS_EXPORTED", *(str(ART / name) for name in
      ("armor-pickup-crystal-runes-lod0.glb", "speed-pickup-crystal-runes-lod0.glb",
       "damage-pickup-crystal-runes-lod0.glb")))
