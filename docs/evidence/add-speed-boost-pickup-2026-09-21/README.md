# Speed pickup verification — 2026-09-21

- Revision: pending implementation commit on `codex/add-speed-boost-pickup`.
- Asset: `speed-pickup-lod0.glb`, authored locally with explicit user authorization; 105 KB, SHA-256 recorded in `Art/asset-manifest.json` and `speed-pickup-ATTRIBUTION.md`.
- Format contract: GLB/glTF 2.0, meters, `+Y` up, `+Z` front, ground-centre pivot. The runtime removes all colliders from the instantiated renderer-only object.
- EditMode: 108/108 passed after adding lifecycle/restore coverage.
- PlayMode: 68/68 passed after scene migration.
- Native Player build: `STAR_PROVING_BUILD_PASS 459534252`.
- Visual diagnostic: muted native Player screenshot observed the yellow luminous corner pickup. It is diagnostic visual evidence only; physical input, controller/TV and human acceptance remain open.
