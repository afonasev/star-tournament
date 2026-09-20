## Why

ARENA-1 отделил каноническую arena definition от Unity-проекции, но по-прежнему генерирует только один проверочный layout. Следующий ограниченный срез должен добавить утверждённое пространственное разнообразие без смешивания выбора семейства с seed и без преждевременного расширения validators, lifecycle или replay.

## What Changes

- Добавить пять зарегистрированных native spatial families из раздела 2 и журнала решений `docs/GAME_SPEC.md`: широкий зал с обходом, обходная галерея, балконы, две боевые арены по ярусам и подвал.
- Сделать family явным canonical profile value; seed не выбирает и не меняет family.
- Включить stable family ID в immutable `ArenaDefinition`, content identity и navigation identity.
- Строить collision, navigation, spawn и presentation всех пяти вариантов через существующую единую definition boundary ARENA-1.
- Проверить meaningful EditMode/PlayMode, Mac Player build и muted native Player diagnostics на representative families.
- Не включать ARENA-3 generated-map navigation, ARENA-4 validators, ARENA-5 Repeat freeze/retry/fallback/replay, новые assets, integration, archive или deploy.

## Capabilities

### New Capabilities

- `unity-arena-families`: явный profile-owned выбор пяти native пространственных семейств и их каноническая identity поверх ARENA-1.

### Modified Capabilities

<!-- Нет: browser procedural-arena-generation остаётся историческим baseline; ARENA-1 change не архивируется и не переписывается. -->

## Impact

Затрагиваются `docs/GAME_SPEC.md`, Unity arena profile/generator/definition и projections, native EditMode/PlayMode tests, Mac build и handoff/evidence. Player impact — пять визуально и пространственно различимых arena layouts при сохранении существующих movement/combat/seat contracts. Открытыми остаются physical device/TV, artistic и reference-hardware performance acceptance, а также отдельные ARENA-3–5 решения.
