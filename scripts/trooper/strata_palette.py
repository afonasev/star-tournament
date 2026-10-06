"""Recolour the attributed Trooper texture atlases without losing their detail.

Only base-colour images are touched. UVs, normal maps, rig weights and animation
remain from the immutable attributed source. The transform is deterministic.
"""

import bpy
import numpy as np


def apply():
    for material_name in ('pants', 'material', 'TECI_helmet'):
        mat = bpy.data.materials.get(material_name)
        if mat is None or not mat.use_nodes:
            continue
        principled = mat.node_tree.nodes.get('Principled BSDF')
        if principled is None:
            continue
        links = principled.inputs['Base Color'].links
        if not links or links[0].from_node.type != 'TEX_IMAGE':
            continue
        image = links[0].from_node.image
        if image is None:
            continue
        # The copy prevents any other material sharing the source image from
        # acquiring an unintended tint.
        image = image.copy()
        image.name = 'strata-oxblood-' + material_name
        links[0].from_node.image = image
        width, height = image.size
        pixels = np.empty(width * height * 4, dtype=np.float32)
        image.pixels.foreach_get(pixels)
        rgba = pixels.reshape((height, width, 4))
        rgb = rgba[..., :3]
        lum = rgb.mean(axis=2)
        sat = rgb.max(axis=2) - rgb.min(axis=2)
        opaque = rgba[..., 3] > .2
        if material_name == 'TECI_helmet':
            # F4 keeps the helmet charcoal while the forearm and leg armor
            # provide the pale contrast. Preserve original roughness/normal.
            rgb[opaque] *= .19
        else:
            # Cloth is oxblood; dark hardware and the anatomical glove stay dark.
            cloth = opaque & (lum > .075) & (sat < .20)
            if material_name == 'material':
                # The attributed glove occupies the darker part of this atlas.
                cloth &= (lum < .36)
            target = np.stack((lum * .57 + .018, lum * .115 + .004,
                               lum * .145 + .008), axis=2)
            rgb[cloth] = target[cloth]
        image.pixels.foreach_set(pixels)
        image.update()
        image.pack()
