# Vector / Petrol detailed source

Approved visual reference: shared planning `docs/evidence/refine-vector-shotgun/approved-vector.png` (original F4 №01, selected as option 2 in the current chat). The short generated pistol-like redesign was rejected.

`vector-petrol.blend` is the editable detailed source with separate named solid panels, muzzle rings, fasteners, rail, sights and contact surfaces fitted to the unchanged native hand pose. It preserves the current character/gloves. Coordinates: Blender Z up, -Y forward, original equipment-local units; standalone GLB root uses the existing 0.32 scale. The two existing muzzle mounts remain exactly unchanged. No collision shapes are authored.

Rebuild:

```sh
blender -b -noaudio --python-exit-code 1 --python scripts/vector_shotgun/author.py
python3 scripts/vector_shotgun/install.py --ref HEAD
```

Authoring reads immutable `efc73a7a5ade08f161022f3b3afb2b7dd89c6b1e` material maps, never its own output. Installation takes current compatible character assets from the explicitly selected ref, appends new resources and replaces only `weapon:joined` mesh. It asserts exact preservation of original binary payload, nodes, skins, animations and every other mesh. When integrating parallel character changes rerun installation on their resulting ref; never overwrite their GLBs wholesale. Asset manifest/mount hashes are updated by entry. Re-running against the same input is byte reproducible; against an already-installed ref would retain unused old appended data and is not intended.

Detailed `lod0` and `lod1` intentionally contain the same geometry because the user explicitly deferred game simplification. No decimation, texture downscale or performance claim. Identical-material batching  do not alter appearance. Record counts/bytes/materials in `.local/vector-shotgun/asset-audit.json`; runtime performance remains NotRun. An explicit later optimization can retain this master.

Original 128×128 shotgun metal map bytes remain bound to chassis/titanium; new petrol/edge surfaces add authored UVs and a packed roughness micrograin map. Glove maps, skin geometry and existing animations are unchanged. Fine geometry has smooth bevel normals and weighted planar faces.

`ShotgunDetailArt.cs` is the editable normalized art source for metallic spherical shot with a shallow machining seam, irregular fractured chips, a billowing smoke texture and an incandescent muzzle texture. It uses existing named profile sizes/counts/lifetimes; normalized texture motifs and mesh tessellation are authored asset details. No physical projectiles or damage logic are added. Transient rendering follows the session clock and clears on restore. The plain grey trace remains for readability alongside the small metallic core.

Native preparation: `TrooperPreparation.RefreshExistingScene` refreshes body/arms prefabs without serializing unrelated scene defaults. Focused checks and exact source revisions live in the shared change evidence, not this README. Blender renders are actual mesh renders, but only native captures show the applied additive hand poses. Neither implies human acceptance.
