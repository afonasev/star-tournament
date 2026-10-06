## Why

ARENA-2 уже создаёт пять канонических `ArenaDefinition`, но старое доказательство native navigation относится к одной fixture. ARENA-3 должен доказать, что bot movement действительно следует supports и declared transitions каждой generated family, а не случайно проходит один layout.

## What Changes

- Расширить native navigation context до definition-owned supports, transition entrances и route anchors всех пяти ARENA-2 families.
- Усилить semantic/physical route validation: полный NavMesh path проверяется по ordered transition feet, support sequence и capsule headroom; global NavMesh query допускается только для единственного активного owned context.
- Прогнать физический `CharacterMotor` traversal: два перехода × оба направления × три lanes для каждого family, включая overlapping XZ, чужую arena, живой blocker, replan/restore.
- Обновить canonical GAME_SPEC, migration matrix, muted native Player review и evidence. Измерения остаются diagnostic.

## Capabilities

### New Capabilities

- `unity-arena-family-navigation`: native route and bot traversal for all registered `ArenaDefinition` families.

### Modified Capabilities

Нет. Базовый navigation adapter остаётся отдельным неархивированным native change; этот срез добавляет самостоятельный contract для ARENA-2 definitions.

## Impact

GAME_SPEC §§2, 7–8; `ArenaDefinition`/`ProvingArena`, `NativeNavigationProvider`, bot navigation integration, EditMode/PlayMode, Player review and evidence. Depends on `add-unity-bot-navigation`, `add-unity-procedural-arena-foundation` and `add-unity-arena-families`. No new authoring/editor pipeline, browser runtime, ARENA-4 validators, ARENA-5 Repeat/replay/fallback, assets, integration, archive or deploy. Physical device/TV, artistic and reference-hardware performance acceptance remain open.
