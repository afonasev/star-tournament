# Pulse hot impulse asset contract

Selected concept: variant 1, 2026-10-02; canonical PNG and choice live in the shared planning change `improve-pulse-projectile-vfx`.

Original source: `scripts/trooper/build_pulse_projectile.py` (Blender offline). Manifest key `pulse-projectile`; `pulse-projectile.glb`, meters, +Y up, +Z forward, center origin. Four shared material meshes: ceramic nose/fins, graphite shell, orange collar, recessed nozzle. Authored baseline body radius .23 m / total length 1 m is normalized by existing `presentation.rocketBodyRadius/Length`; fins are fixed authored topology. There are no colliders, animations, gameplay hit proxies, or gameplay LOD changes. Model is small enough that one mesh tier is sufficient; only cosmetic dimensions scale.

Particle textures are original procedural art sampled at a fixed 128 square resolution, shared across the session: irregular fire, soft smoke and radial hot core. The baked noise/palette/taper motifs are art construction constants; absolute size, duration, counts, spread, opacity, tint mix and resource cap belong to `pulse-effects-v1@1` / existing presentation profile. No external download, per-explosion material clone, bloom requirement or dynamic light.

All particles are analytically positioned from the session clock and event ID. Golden-angle sample distribution is renderer-only and never consumes gameplay RNG. Restore discards transient VFX, active flights reconstruct from authoritative state. Burst instances reuse a bounded pool and replace its oldest slot if capacity is reached. Character/HUD colors and physics remain unchanged.
