# Pulse Mk II — detailed editable source

Approved A concept and grip study are preserved in the canonical planning store at
`docs/evidence/refine-pulse-mk2-presentation/`. These are concept references, not native evidence.

- `pulse-mk2-master.blend`: named full-detail launcher parts, packed authored surface maps.
- `pulse-mk2-projectile-master.blend`: full-detail rocket parts and packed materials.
- `build.py`: deterministic Blender 5.2 recipe, semantic attachment frame retained.
- `render.py`: studio renders of the **exported GLBs**.
- `bake_effects.py`: deterministic smoke/exhaust texture source; does not overwrite selected fire art.
- `fire-art-source.json`: imagegen provenance and SHA for the detailed 4x4 fire atlas.

```sh
blender -b -noaudio --python-exit-code 1 --python scripts/pulse_mk2/build.py
blender -b -noaudio --python-exit-code 1 --python scripts/pulse_mk2/render.py
blender -b -noaudio --python-exit-code 1 --python scripts/pulse_mk2/render.py -- --projectile
blender -b -noaudio --python-exit-code 1 --python scripts/pulse_mk2/bake_effects.py
python3 scripts/pulse_mk2/audit.py
```

Art coordinates are metres, X lateral/Y up/Z forward, matching the original Pulse
muzzle/grip markers exactly. The source uses Blender Z-up after the final conversion.
The glTF postpass explicitly embeds texture bytes and PBR factors because the installed
exporter omitted linked factors/images; audit and native imports verify the actual output.

Detail reduction is explicitly deferred by the user. LOD0/LOD1 currently expose the same
high-detail mesh; this is not a performance-optimized shipping claim. Grouping by material
is lossless; source files retain individually editable objects. No collider or physics data
is authored. Other weapons and the character source remain untouched.

Real native grips, effects and deviations from the concept must be reviewed separately;
asset validity, tests and studio images do not establish human acceptance.
