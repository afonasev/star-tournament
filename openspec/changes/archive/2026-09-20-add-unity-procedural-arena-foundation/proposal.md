## Why

Native Unity матч пока собирает проверочную арену из разрозненных констант, поэтому seed и profile не образуют проверяемую identity, а следующие пространственные семейства и валидаторы пришлось бы встраивать в presentation-сборку. Нужен маленький канонический фундамент до расширения topology.

## What Changes

- Вводится immutable native `ArenaDefinition` с явными seed, generator version и profile identity для текущего двухуровневого proving-ground layout.
- Отдельный генератор создаёт definition до создания матча, без потребления match RNG; native arena строит collision/navigation/spawn/presentation из definition.
- Вводятся registration seams для будущих spatial families и чистых validators, но ARENA-1 реализует только shipped foundation layout и identity validator.
- **BREAKING** Внутренняя native arena-сборка перестаёт быть источником spatial constants; существующий `Build(profile)` остаётся совместимым входом и получает default canonical definition.

## Capabilities

### New Capabilities
- `unity-procedural-arena-foundation`: каноническая native arena identity и независимая генерация current proving-ground layout.

### Modified Capabilities

<!-- Нет: browser procedural-arena-generation остаётся историческим baseline и не меняется этим Unity-срезом. -->

## Impact

Затрагиваются Unity runtime arena/composition, native EditMode/PlayMode tests, `docs/GAME_SPEC.md`, handoff/evidence. Не затрагиваются spatial families, size presets, replay, deployment, assets, input, bot policy или browser runtime.
