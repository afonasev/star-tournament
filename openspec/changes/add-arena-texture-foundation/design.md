## Context

См. `proposal.md` для мотивации и delta specs для contract. Renderer уже получает immutable `ArenaDefinition`, создаёт `MeshStandardMaterial` из semantic surface slots и ограничивает pixel ratio через `presentation-balanced-v1`; React shell владеет menu/match/results surfaces. GLB-kit загружается отдельным presentation adapter, поэтому texture и quality state должны оставаться вне simulation package.

## Goals / Non-Goals

**Goals:**

- Дать renderer единый immutable `GraphicsQualityProfile` с effective options и residency limits.
- Применить tiling PBR maps к analytic arena surfaces, не меняя semantic surface slots.
- Предоставить компактную graphics section в DOM match menu и передать только resolved presentation profile в renderer bootstrap.
- Сохранить local persistence, deterministic boundaries, zero-work lifecycle и observability для browser performance gate.

**Non-Goals:**

- Не менять `ArenaDefinition`, procedural generation, collision, navigation, spawn, replay или match settings.
- Не добавлять gamepad/split-screen flow, online state, texture streaming from a remote service или runtime-generated textures.
- Не менять GLB topology/LOD or add content-dependent atlas coordinates to simulation data.

## Decisions

### Presentation-owned quality profile

Новый presentation module хранит versioned descriptors `Low`, `Balanced`, `High`, `Ultra`, validates local preference и returns an immutable effective profile. `Balanced` — fallback для corrupt/unsupported values. Пресет задаёт render scale, texture tier, GLB LOD preference, decals и post-processing; отдельные controls создают effective profile поверх preset. Это отделяет user preference от Game Design Lab и snapshot. Альтернатива — записывать настройки в match configuration — отвергнута: это включило бы presentation details в replay identity.

### Hybrid texture assets without new dependencies

Первый pass использует локальные procedurally-authored canvas textures as the shipping-compatible representation: colour, normal-like detail and ORM-like roughness/metalness maps shared by surface class, plus a compact decal atlas for route/sector marking. Three.js `MeshStandardMaterial` receives repeat-wrapped maps; renderer owns all `Texture` instances and disposes them with materials. Asset manifest data stays presentation-only. KTX2/Basis transcoding is a future drop-in authoring upgrade; adding a transcoder now would be an unverified dependency and does not improve this first local pass.

### 4K display through pixel budget, not simulation scaling

The canvas can occupy a 4K CSS/output display while the resolved profile supplies a capped device pixel ratio/render scale. Existing pixel-budget resolver remains the sole backing-buffer authority; quality profile affects its input only. This protects 60 Hz presentation cadence and prevents a 4K monitor from silently raising draw resolution beyond the active budget. Альтернатива — always-native 4K framebuffer — отвергнута из-за memory/thermal risk and split-screen incompatibility.

### Compact menu, explicit pause boundary

`MatchSetupMenu` owns a collapsed graphics `<details>` section, local persistence and effective-value feedback. Gameplay pause does not receive a new live settings control in this iteration, so changing settings remains a menu action that affects the next session; this avoids pointer-lock and renderer-resource hot-swap risk. The settings component is DOM-only and visually secondary to match setup. Альтернатива — full-screen settings overlay during gameplay — отложена до утверждённого pause-menu navigation design.

## Risks / Trade-offs

- [Canvas-generated first-pass maps are stylized, not final authored art] → use stable manifest-style keys and isolated factory so authored compressed files can replace them.
- [Texture residency cannot be measured exactly across browsers] → profile exposes budget intent; performance probe records Three.js texture counters and browser-provided memory as evidence.
- [High-tier settings on weak GPUs] → effective profile caps DPR/detail and falls back to `Balanced` on invalid persistence.
- [Settings hidden behind menu] → preserve unobstructed playfield and avoid pointer-lock conflict; pause-menu integration stays a later UI decision.

## Migration Plan

1. Add quality descriptor/validation and tests without simulation imports.
2. Add renderer-owned tiling maps, decal atlas and quality-aware pixel budget/LOD selection; dispose resources with renderer.
3. Add menu section and local preference, inject resolved profile at runtime startup.
4. Run unit/build checks, production performance gate and muted in-app Browser evidence at default and high tiers.
5. Rollback removes presentation profile and texture factory only; saved preferences are ignored safely and no replay/data migration is required.
