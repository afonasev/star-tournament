## Context

См. proposal.md. ARENA-2 уже строит пять pure C# `ArenaDefinition` и проецирует их через одну `ProvingArena`; текущий provider проверен только на default fixture. Read-only Astra memo 2026-09-20 подтвердил, что отдельный native authoring/generation pipeline не нужен, но global NavMesh должен быть ограничен одним owned context и проверен на family geometry.

## Goals / Non-Goals

**Goals:** доказать definition-derived routes и ordinary `CharacterMotor` traversal для всей семейной матрицы, сохранив поддержку/transition/state contracts старого adapter.

**Non-Goals:** новый generator/editor/prefab source, family recognition policy, browser route, seed-driven topology, ARENA-4 validators, ARENA-5 Repeat/replay/fallback, assets, integration, archive, deploy или acceptance beyond diagnostic.

## Decisions

1. `ProvingArena` остаётся единственной projection accepted definition. `NativeNavigationProvider` получает identity/supports/transitions только через arena; family IDs не передаются follower. Это сохраняет C# builder → definition → collision/NavMesh → ordinary motor chain. Пять prefabs/второй graph и browser A* отвергнуты как второй spatial source.
2. Native NavMesh остаётся кандидатом полного route, но every corner/segment normalizes to physical support and may cross only declared ordered transition feet. Provider owns one active context; parallel different arena contexts остаются unsupported до отдельного contract. Это закрывает global query ambiguity без введения alternate engine.
3. Family matrix выбирает endpoints из definition route anchors и transition endpoints, а не fixture constants. PlayMode проходит 5 × 2 × 2 × 3 physical motor traversals, plus negative fixtures. Existing follower/recovery/state remain authority for actions/lifecycle.
4. Free-traversal measurements report route queries/update duration/allocations/frame timings only as Mac diagnostic; no numeric shipping budget is inferred without approved reference hardware.

## Risks / Trade-offs

- [Native path can skip a semantic entrance] → reject any segment whose physical support sequence fails ordered transition validation.
- [Transition support collider can hide an obstruction] → validate physical feet/headroom per ordered sample and add severed/headroom negative fixtures.
- [Fixture-specific tests may pass while a family fails] → derive every endpoint and lane from the generated definition matrix.
- [Concurrent NavMeshes] → reject a foreign endpoint and document one active owned context; do not claim multi-arena support.

## Migration Plan

1. Add definition-aware validation and definition-derived review/test helpers.
2. Run matrix and negative PlayMode fixtures plus full EditMode/PlayMode, Mac build and muted Player journeys.
3. Record evidence and handoff in one commit. Rollback is one isolated commit; no persisted schema or release state changes.
