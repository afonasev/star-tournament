"""Audit generated GLBs: blender -b --factory-startup --python audit-crystal-rune-pickups.py"""

import hashlib
import json
from pathlib import Path

import bpy


art = Path(__file__).resolve().parent
out = art.parents[3] / "docs/evidence/add-full-heal-pickup/asset-audit.json"
out.parent.mkdir(parents=True, exist_ok=True)
records = []
for name in ("heal",):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    path = art / f"{name}-pickup-crystal-runes-lod0.glb"
    bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    verts = [obj.matrix_world @ vertex.co for obj in meshes for vertex in obj.data.vertices]
    record = {
        "key": name + "-pickup",
        "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
        "mesh_count": len(meshes),
        "triangle_count": sum(len(poly.vertices) - 2 for obj in meshes for poly in obj.data.polygons),
        "material_names": sorted({mat.name for obj in meshes for mat in obj.data.materials if mat}),
        "bounds_blender_xyz_m": {
            "min": [round(min(v[axis] for v in verts), 5) for axis in range(3)],
            "max": [round(max(v[axis] for v in verts), 5) for axis in range(3)],
        },
        "front_meshes": sum(obj.name.endswith("-front") for obj in meshes),
        "rear_meshes": sum(obj.name.endswith("-rear") for obj in meshes),
        "degenerate_normal_faces": sum(poly.normal.length < .99 for obj in meshes for poly in obj.data.polygons),
        "collider_nodes": [obj.name for obj in bpy.context.scene.objects if "collider" in obj.name.lower()],
    }
    assert record["front_meshes"] and record["rear_meshes"]
    assert not record["degenerate_normal_faces"] and not record["collider_nodes"]
    records.append(record)
out.write_text(json.dumps(records, indent=2) + "\n")
print("CRYSTAL_RUNE_AUDIT", out)
