# Pulse Mk II — detailed editable source

Approved A concept and grip study are preserved in the canonical planning store at
`docs/evidence/refine-pulse-mk2-presentation/`. These are concept references, not native evidence.

- `pulse-mk2-master.blend`: named full-detail launcher parts, packed authored surface maps.
- `pulse-mk2-projectile-master.blend`: full-detail rocket parts and packed materials.
- `launcher_body.py`: sculpted A body, tapered ceramic frames, actual machined recesses and shortened breech.
- `build.py`: deterministic Blender 5.2 recipe, semantic attachment frame retained.
- `render.py`: studio renders of the **exported GLBs**.
- `bake_effects.py`: deterministic smoke and earlier exhaust recipe; does not overwrite selected fire/exhaust art.
- `fire-art-source.json` / `exhaust-art-source.json`: imagegen provenance and SHA for the fire atlas and turbulent jet.

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

Pulse fire/smoke use an isolated soft-particle shader. An input-only URP pass requests depth before transparent rendering, only on owned cameras with an active burst; original per-camera depth options are restored on expiration/restore/dispose. Other particle shaders and global renderer assets are unchanged. Surface fade and fire radiance are profile v4 metadata; v1-v3 registries remain byte/hash compatible.

F1 rework preserves all grip/muzzle markers and hand poses. Base color and packed metal/roughness maps distinguish coating, graphite and exposed metal; no painted lighting. The authored shell cavities receive final machining bevels after booleans. Detail reduction remains deferred.

## Integrated reserve instrument (C, 2026-10-28)

The native assembled weapon now adds an editable circular instrument inside the existing
rear breech inset. Its source is `Runtime/PulseAmmoDisplay.cs`, `Resources/PulseDisplay.shader`
and `Core/PulseDisplayProfile.cs` under `unity/Assets/StarTournament/`. GLB surfaces, source
Blender files, grips and muzzle markers are byte-for-byte retained. The dynamic surface
must be rendered by Unity; offline GLB studio previews do not show live ammunition.

One shared 49-vertex/48-triangle front face and one shared opaque material use the existing
640×128 authored `ShotgunDigits` atlas (source: `scripts/shotgun_display/atlas.py`). No new
texture, lights, colliders or shadow passes. Digits, icon, hairline rim and reserve arc are
one pass; per-owner values use a property block only when they change. Extra LOD geometry
would cost more than this face; existing actor/weapon visibility and view layers remain.
Profile metadata controls diameter, position, digit size and digit/arc luminance.
