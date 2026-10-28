# Lossless weapon mesh pass

Baseline: `a321b84ab87cc9b0b7ced089b0d2256ff5929f9d`.
Scope: COBALT, VECTOR, PULSE, SEAM, FORGE, both LOD files, Pulse projectile,
and the VECTOR mesh embedded in the world body and first-person arms.

The tool welds only byte-identical complete attribute tuples, orders triangles
for vertex cache reuse, and stores vertices in first-use order. It does not
simplify, quantize, bake textures, change materials/shadows/LOD policy, or edit
Blender sources. Oriented triangle multiplicity, UV/normal/skin attributes,
node/attachment transforms, animation data and embedded image bytes are audited.

From a clean asset source revision:

```sh
PYTHONDONTWRITEBYTECODE=1 PYTHONPATH=tools python3 -m unittest tools.test_optimize_weapon_meshes
python3 tools/optimize_weapon_meshes.py --report .local/weapon-render-optimization/audit.json --promote
python3 tools/audit_weapon_optimization.py --baseline a321b84ab87cc9b0b7ced089b0d2256ff5929f9d --report .local/weapon-render-optimization/preservation.json --prepare-baseline
```

The last command reconstructs ignored `Art/WeaponRenderBaseline` imports for
`WeaponRenderingOptimizationTests.CompareAllWeapons`. Run that explicit focused
PlayMode fixture graphically with `-noaudio -muted`, isolated
`STAR_TOURNAMENT_QA_LAB_HISTORY`, and `STAR_TOURNAMENT_WEAPON_OPT_EVIDENCE` set to an
owned output directory. Use the shared Unity runner; it captures all five actual
first-person views and three standalone model angles before/after in the same
frame after a warmup, with a repeated baseline image and imported mesh/cache metrics. Also run `WorldWeaponMaterialTests`.
No full tests, Player build, release or deployment are implied.

Durable evidence belongs to the shared planning store at
`docs/evidence/optimize-weapon-rendering/`. Cache misses are a modeled vertex
reuse diagnostic, not a measured GPU time or FPS claim. Human/device/Player
acceptance remains separate. Baseline import copies and Library are disposable
after evidence is preserved and owned processes exit.
