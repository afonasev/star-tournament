# COBALT rifle

User-selected concept 01, 2026-10-26. Reference is retained in shared planning at
`docs/evidence/refine-cobalt-rifle-and-grip/approved-cobalt-concept.png`.

`cobalt-rifle.blend` is the editable detailed source, with individually named parts,
editable procedural PBR surfaces and the original mount-space basis. Rebuild from this directory's
`build_cobalt.py` with Blender; `build_rifle.py` forwards to the current generator.

```sh
blender -b -noaudio --python-exit-code 1 -P scripts/trooper/build_cobalt.py
# Non-shipping authoring preview (does not replace Unity assets or the saved source):
blender -b -noaudio --python-exit-code 1 -P scripts/trooper/build_cobalt.py -- --preview-only
```

The generator produces both rifle GLBs and updates only their manifest entries.
No decimation is performed. LOD1 deliberately retains LOD0 detail for this first
art review. 280 authored objects are batched by seven surface materials without
removing triangles. Geometry uses metres, Y up, Z forward, original mount origin;
`rifle-grip` and `rifle-muzzle` retain their API identities. No collision mesh.

F1 correction (2026-10-27): real per-material UV atlases replace the original 512px micrograin. Each of seven surfaces has base color, packed roughness/metallic and tangent-space normal maps: 2048px for main blue armor, 1024px for the others. Editable source shaders are in the Blender source and `cobalt_surfaces.py`; exported PNGs are in `assets/source/cobalt/textures/`. Edge wear is masked to authored bevel faces, flat panels carry broader polish and sparse scratches, and rubber has cellular relief. Standard glTF images are packed into each GLB. The runtime preserves the imported
COBALT materials; player identity color and boost must not permanently recolor them.
The model does not change bullet simulation, firing cadence, ammo, audio or VFX.

`.local/cobalt/asset-pass.json` records exact source/export counts. Blender studio
images are authoring evidence only. Native Unity captures and explicit human review
are separate gates; polygon counts are not an FPS measurement.

The default Trooper presentation profile is @22. Untouched scene Rifle defaults and the reserved published Default adopt the fitted COBALT grip. Named/custom profiles and immutable historical registries retain their saved values.

### F2: near-stock tactile surface and support thumb

The existing blue stock spine carries a graphite diamond-knurled inset with a
blue perimeter. The shoulder pad uses the same 5 mm crossed pattern. A named
`cobalt_stock_texture` corner attribute restricts the relief to these two source
objects; color, packed roughness/metallic and tangent normals are baked into the
existing atlases. No additional mesh, material, texture or draw unit is added.
Both exports retain 97,422 triangles and the original anchors. Source and
export topology are preserved; simplification remains deferred.

Presentation profile @25 adjusts only the Rifle left thumb articulation and
opposition. The @24 registry is reconstructed by `BeforeCobaltThumb`; named
custom poses and immutable history remain readable. The stock surface and hand
must be reviewed together in native Unity FPS and opposing close-up views.
