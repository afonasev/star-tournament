# COBALT rifle

User-selected concept 01, 2026-10-26. Reference is retained in shared planning at
`docs/evidence/refine-cobalt-rifle-and-grip/approved-cobalt-concept.png`.

`cobalt-rifle.blend` is the editable detailed source, with individually named parts,
packed surface textures and the original mount-space basis. Rebuild from this directory's
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

PBR base-color maps are packed and exported. The runtime preserves the imported
COBALT materials; player identity color and boost must not permanently recolor them.
The model does not change bullet simulation, firing cadence, ammo, audio or VFX.

`.local/cobalt/asset-pass.json` records exact source/export counts. Blender studio
images are authoring evidence only. Native Unity captures and explicit human review
are separate gates; polygon counts are not an FPS measurement.

The default Trooper presentation profile is @22. Untouched scene Rifle defaults and the reserved published Default adopt the fitted COBALT grip. Named/custom profiles and immutable historical registries retain their saved values.
