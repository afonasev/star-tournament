## Why

ARENA-2 создаёт пять canonical family definitions, а ARENA-3 проверяет их runtime traversal, но до создания Unity scene definition ещё может содержать семантически неверную геометрию, коллайдеры, spawn или declared routes. Нужен независимый pre-runtime gate с диагностикой, чтобы ошибка authoring/generation была понятна до NavMesh bake и session creation.

## What Changes

- Добавить ARENA-4 pure validator для `ArenaDefinition` всех зарегистрированных native families.
- Проверять finite/non-degenerate geometry, semantic supports и collider-material/layer contract, spawn/support/clearance и declared route endpoints/ordered feet.
- Возвращать stable, actionable diagnostics с family и semantic element identifier; generation отклоняет invalid definition до Unity projection.
- Добавить representative EditMode/PlayMode coverage, не меняя bot runtime, replay, lifecycle, delivery или финальную физическую приёмку.

## Capabilities

### New Capabilities

- `unity-arena-family-validation`: Pre-runtime validation contract for native Unity arena-family definitions.

### Modified Capabilities

<!-- Нет: ARENA-2 remains an unarchived delta; the new ARENA-4 capability owns its pre-runtime gate. -->

## Impact

`ArenaDefinition`/`ArenaGenerator`, `ArenaFamilyCatalog`, native `ProvingArena` construction and focused EditMode/PlayMode tests. Player-visible topology, input, bots, snapshots/replay, assets, deploy and integration are out of scope.
