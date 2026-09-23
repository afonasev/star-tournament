"""Render reproducible review views of the authored Crystal Runes GLBs."""

import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ART = Path(__file__).resolve().parent
OUT = ART.parents[3] / "docs/evidence/unify-bonus-pickup-crystal-runes-detail-2026-09-23"
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)

roots = []
for name in ("armor", "speed", "damage"):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.import_scene.gltf(filepath=str(ART / f"{name}-pickup-crystal-runes-lod0.glb"))
    imported = list(bpy.context.selected_objects)
    root = bpy.data.objects.new(f"{name}-preview-root", None)
    bpy.context.collection.objects.link(root)
    for obj in imported:
        if obj.parent is None:
            obj.parent = root
    roots.append(root)

for root, x in zip(roots, (-1.45, 0, 1.45)):
    root.location = (x, 0, 0.72)

floor_mat = bpy.data.materials.new("neutral arena floor")
floor_mat.use_nodes = True
floor_mat.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = (.055, .08, .11, 1)
floor_mat.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value = .82
bpy.ops.mesh.primitive_plane_add(size=200)
floor = bpy.context.object
floor.name = "preview floor"
floor.data.materials.append(floor_mat)

world = bpy.context.scene.world
world.color = (.10, .10, .10)

def light(name, location, power, color, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy = power
    data.color = color
    data.shape = "DISK"
    data.size = size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector((0, 1, 0)) - obj.location).to_track_quat("-Z", "Y").to_euler()


light("key", (-3, -4, 5), 650, (0.77, .88, 1), 4)
light("fill", (3, -2, 3), 350, (1, .83, .70), 4)
light("rear", (0, 3, 4), 450, (.65, .79, 1), 3)

camera_data = bpy.data.cameras.new("review camera")
camera = bpy.data.objects.new("review camera", camera_data)
bpy.context.collection.objects.link(camera)
bpy.context.scene.camera = camera
camera_data.type = "ORTHO"
camera_data.ortho_scale = 5.3
camera.location = (0, -6, 1.85)
camera.rotation_euler = (Vector((0, 0, 1.15)) - camera.location).to_track_quat("-Z", "Y").to_euler()

scene = bpy.context.scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 32
scene.render.resolution_x = 1600
scene.render.resolution_y = 900
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.view_settings.view_transform = "AgX"

views = (("front", 0, 5.3), ("quarter", 45, 5.3),
         ("back", 180, 5.3), ("side", 90, 5.3), ("distant", 0, 10.0))
if "--quick-audit" in sys.argv:
    scene.cycles.samples = 8
    scene.render.resolution_x = 800
    scene.render.resolution_y = 450
    views = tuple((f"turn-{angle:03d}", angle, 5.3) for angle in range(0, 360, 45))
for label, angle, scale in views:
    for root in roots:
        root.rotation_euler = (0, 0, math.radians(angle))
    camera_data.ortho_scale = scale
    scene.render.filepath = str(OUT / f"crystal-runes-{label}.png")
    bpy.ops.render.render(write_still=True)
    print("CRYSTAL_RUNE_PREVIEW", scene.render.filepath)

if "--quick-audit" in sys.argv:
    for root in roots:
        root.rotation_euler = (0, 0, 0)
    scene.use_nodes = True
    compositor = bpy.data.node_groups.new("Crystal Runes grayscale QA", "CompositorNodeTree")
    scene.compositing_node_group = compositor
    compositor.interface.new_socket(name="Image", in_out="OUTPUT", socket_type="NodeSocketColor")
    nodes = compositor.nodes
    source = nodes.new("CompositorNodeRLayers")
    bw = nodes.new("CompositorNodeRGBToBW")
    composite = nodes.new("NodeGroupOutput")
    compositor.links.new(source.outputs["Image"], bw.inputs["Image"])
    compositor.links.new(bw.outputs["Val"], composite.inputs["Image"])
    scene.render.filepath = str(OUT / "crystal-runes-grayscale.png")
    bpy.ops.render.render(write_still=True)
    print("CRYSTAL_RUNE_PREVIEW", scene.render.filepath)
