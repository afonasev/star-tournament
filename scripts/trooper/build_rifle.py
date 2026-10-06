"""Build the selected cobalt/lime rifle as presentation-only GLB assets.

Run: blender -b -P scripts/trooper/build_rifle.py
The weapon grip is the origin, +Y is up, and +Z is the muzzle direction.
"""
import bpy
import math
from pathlib import Path
from mathutils import Matrix

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "unity/Assets/StarTournament/Art"


def material(name, color, metal=0.0, rough=.36):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF") or m.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
    output = m.node_tree.nodes.get("Material Output") or m.node_tree.nodes.new("ShaderNodeOutputMaterial")
    m.node_tree.links.new(bsdf.outputs["BSDF"], output.inputs["Surface"])
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    return m


def box(name, location, scale, mat, bevel=.008):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new("machined edges", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.modifiers.new("weighted normals", "WEIGHTED_NORMAL")
    return obj


def cylinder(name, location, radius, depth, mat, vertices=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return obj


def make(lod):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    cobalt = material("rifle cobalt ceramic", (.045, .15, .52), .42)
    navy = material("rifle dark frame", (.012, .021, .052), .72)
    lime = material("rifle lime identity", (.55, .95, .05), .25)
    steel = material("rifle brushed metal", (.45, .55, .63), .8)
    box("cobalt receiver", (0, .055, .12), (.14, .105, .29), cobalt)
    box("navy underframe", (0, -.016, .12), (.105, .055, .31), navy)
    box("angled forward shroud", (0, .045, .34), (.112, .086, .20), cobalt)
    box("recessed rear shoulder", (0, .012, -.075), (.072, .055, .11), navy)
    box("cobalt stock cheek", (0, .058, -.07), (.08, .028, .105), cobalt)
    box("upper sight bridge", (0, .136, .08), (.042, .035, .23), cobalt)
    box("carry handle top", (0, .19, .06), (.11, .016, .14), cobalt, .004)
    for side in (-1, 1):
        box("carry handle strut", (side*.047, .15, .09), (.014, .08, .028), cobalt, .003)
    box("lime spine", (0, .105, .28), (.013, .012, .19), lime, .003)
    box("pistol grip", (0, -.095, -.015), (.055, .16, .075), navy)
    box("lime magazine edge", (0, -.069, .11), (.068, .013, .09), lime, .003)
    box("receiver inset", (0, .025, -.025), (.11, .045, .055), steel, .004)
    cylinder("single recessed muzzle", (0, .046, .466), .028, .047, steel)
    cylinder("dark muzzle bore", (0, .046, .49), .017, .003, navy)
    if lod == 0:
        for side in (-1, 1):
            x = side * .073
            box("side lime inlay", (x, .053, .13), (.004, .013, .18), lime, .002)
            box("raised side armor", (side*.081, .065, .31), (.016, .055, .12), cobalt, .004)
            box("side rail", (side*.065, .103, .265), (.012, .01, .23), navy, .002)
            for index in range(3):
                cylinder("vent ring", (side*.059, .053, .27+index*.036), .009, .004, steel, 12).rotation_euler[1] = math.pi/2
                box("upper vent slot", (side*.04, .116, .24+index*.042), (.018, .005, .018), navy, .002)
        cylinder("receiver dial", (.078, .046, .015), .03, .008, steel).rotation_euler[1] = math.pi/2
        cylinder("dial core", (.084, .046, .015), .018, .009, lime).rotation_euler[1] = math.pi/2
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, -.09, 0))
    bpy.context.object.name = "rifle-grip"
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, .046, .49))
    bpy.context.object.name = "rifle-muzzle"
    # Blender is Z-up; glTF is Y-up. Rotate authored Y-up/+Z-forward geometry
    # into Blender coordinates before the exporter applies its axis conversion.
    axis = Matrix.Rotation(math.pi / 2, 4, "X")
    for obj in bpy.context.scene.objects:
        obj.matrix_world = axis @ obj.matrix_world
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=str(ART / f"automatic-rifle-lod{lod}.glb"), export_format="GLB", use_selection=True, export_apply=True)


ART.mkdir(parents=True, exist_ok=True)
for level in (0, 1):
    make(level)
