## Context

См. [proposal.md](proposal.md) — Why. Текущий `clean-future-sport-v1` строит крупные wall/portal/floor формы из Three.js primitives поверх renderer projection. Архивированный pass намеренно не вводил assets. Утверждённый `ArenaDefinition` уже даёт semantic surfaces и doorway gaps, а collision, navigation, spawn и replay derived только из него.

## Goals / Non-Goals

**Goals:**

- Поставить self-contained GLB-kit в репозитории, versioned manifest и unit-testable validation.
- Сохранить существующие semantic attachment rules и заменить ими только presentation geometry.
- Ограничить memory/submit cost shared immutable templates, clones и authored LOD.

**Non-Goals:**

- Не вводить asset data в snapshot, `ArenaDefinition`, profile или любой simulation hash.
- Не использовать GLB collision proxy, dynamic asset download, animation, texture streaming или editor workflow.

## Decisions

### Manifest — единственная точка адресации asset

`src/render` получает immutable `clean-future-sport-glb-v1` manifest с четырьмя стабильными keys: `portal-facade`, `wall-bay`, `floor-route-guide`, `central-landmark`. Каждый key содержит оба локальных `.glb` URI, attachment contract и authored LOD distance. Surface IDs не формируют URL и не входят в asset identity.

Альтернатива — напрямую импортировать GLB из renderer. Она отклонена: такой путь не задаёт stable shipping/API boundary и делает замену версии ассета небезопасной.

### Fixed authoring coordinate contract

Все GLB export в meters с `+Y up`, `+Z front`; scale в runtime остаётся `1`. Pivot: floor-contact center route guide, base-center wall/buttress, doorway-plane center portal и combat-region center landmark. Renderer создаёт transforms только из existing surface/gap anchors; portal retains its existing collinear-gap guard.

Альтернатива — нормализовать units/pivot после загрузки bounds-based math. Она отклонена: скрывает ошибку экспортёра, создаёт frame-dependent geometry и лишает module предсказуемости.

### Collision proxy exists only for DCC audit

Каждый asset содержит documented proxy ID/bounds в manifest для проверки authoring, но asset loader и collision adapter не имеют API, которое принимает proxy. `ArenaDefinition` remains the authoritative spatial source.

Альтернатива — передавать proxy в Rapier как дополнительный static solid. Она отклонена, потому что меняет collision/spawn/replay contract и выходит за scope.

### Authored LOD avoids topology mutation

Каждый module имеет LOD0 и LOD1 с identical anchor/pivot and attachment bounds. Renderer выбирает presentation LOD по distance with a small hysteresis; both variants share immutable loaded templates and clones own only Object3D transform. Dispose traverses and releases every loaded geometry/material exactly once.

Альтернатива — runtime decimation или один high-poly LOD. Она отклонена из-за unpredictable main-thread cost и риска split-screen performance.

### GLB materials respect the current style tokens

Asset materials retain off-white/pale-gray/navy/cyan/lime/orange from `clean-future-sport-v1`; renderer may apply immutable material token corrections during load but never participant/team swatches. Existing studio light and minimal haze remain renderer-owned, so asset files do not carry baked lighting.

Альтернатива — bake lighting into GLB. Она отклонена: она конфликтует с bright studio-light readability and makes light tuning asset-dependent.

## Risks / Trade-offs

- [GLB loader increases startup latency] → local bundled assets, explicit readiness/error state and no render loop before preparation completes.
- [LOD assets drift visually] → manifest validation checks equivalent pivot/attachment bounds and renderer tests exercise every key.
- [Resource leaks from clones] → central template cache ownership plus dispose tests and performance gate.
- [Asset silhouette suggests physical cover] → every placement follows existing surface/gap geometry and Browser evidence covers representative sizes.

## Migration Plan

1. Add manifest types, GLB assets and validation tests before modifying renderer ownership.
2. Update `GAME_SPEC` and presentation capability deltas with closed pipeline contract.
3. Replace primitive architectural detail layer with manifest-loaded clones while retaining base analytical surfaces and lights.
4. Verify unit tests, typecheck, build, strict OpenSpec validation, performance gate and muted Browser screenshots.
5. Roll back one isolated commit to restore renderer-native primitives; no saved state or replay migration is needed.
