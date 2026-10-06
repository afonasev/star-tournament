# Trooper identity material

`TrooperIdentity.shadergraph` derives from Unity glTFast 6.20.0's locked
`glTF-pbrMetallicRoughness.shadergraph` (Apache-2.0; see
`docs/licenses/TrooperIdentity-glTFast-LICENSE.md`). Changes: one fragment
albedo function plus identity color/surface/role inputs. Package lighting,
normal, metallic, roughness, occlusion, skinning and shadows remain intact.
Regenerate with `build_identity_graph.py` and the package graph path.

The function removes source chroma using peak reflectance. Dark seams and
hardware retain neutral reflectance; authored armor panels receive assigned
color with detail and a profile-controlled brightness floor. Ceramic plates
are full zones. Helmet, chest, backpack and pants use source-reflectance zones.
Oxblood fabric/accents become neutral, while weapons retain their own materials.
No bitmap, geometry or animation data is rewritten. Per-actor cloned materials
share source textures and are destroyed with their owner.
