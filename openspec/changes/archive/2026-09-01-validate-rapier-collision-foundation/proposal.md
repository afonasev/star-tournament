## Why

Первый игровой vertical slice требует capsule movement, статических collision queries и hitscan occlusion, но `GAME_SPEC` §7 запрещает окончательно выбирать Rapier без отдельного детерминистического и производительного spike. Нужно доказать, что collision backend не создаёт скрытое несериализуемое состояние, не ломает replay/hash и укладывается в browser budget до реализации движения и оружия.

## What Changes

- Проверить exact-pinned кандидат `@dimforge/rapier3d-deterministic-compat@0.20.0`; обычный `@dimforge/rapier3d-compat` исключить из-за отсутствия гарантии cross-platform determinism.
- Ввести изолированный collision/query adapter и semantic contract для static arena colliders, kinematic capsule movement, ground/contact данных, ray/shape queries и lifecycle, не передавая Rapier objects в сериализуемое simulation state.
- Доказать два допустимых пути восстановления: authoritative binary collision checkpoint либо полная детерминированная реконструкция derived world; принять только путь без скрытого WASM-state между snapshot/hash и replay.
- Добавить воспроизводимый workload восьми участников с canonical create/query order, headless determinism/replay checks, browser benchmark, bundle/init measurements и create/dispose leak checks.
- Добавить browser diagnostic surface для init failure, collision fixtures и измерений; renderer cadence и количество viewport не должны менять simulation hashes.
- Обновить `GAME_SPEC` §§7–8 и журнал только по фактическому результату spike: `go` закрепляет provisional backend и ограничения, `no-go` фиксирует причины без внедрения его в игровой runtime.
- Пользовательский эффект: напрямую игровая механика не добавляется; следующий slice движения и дробовика получает проверенный backend либо явный запрет на неподходящий кандидат.
- Не входят: управление игроком, Quake acceleration/air control, gameplay camera, урон, число дробин, player-player collision, split-screen UI, боты, процедурная генерация, network authority и production performance targets.
- Открытыми остаются целевые ОС/TV/GPU и сетевой trust/authority из `GAME_SPEC` §10. Spike подтверждает доступный reference host и Chromium; cross-OS/browser evidence остаётся обязательным gate перед сетевой совместимостью и не подменяется локальными зелёными hash.

## Capabilities

### New Capabilities

- `collision-query-foundation`: наблюдаемый контракт deterministic collision world, восстановления, kinematic capsule/scene queries, lifecycle, diagnostics и измеримых go/no-go gates.

### Modified Capabilities

- Нет.

## Impact

- Новая exact runtime dependency-кандидат с WASM lazy chunk; фактический gzip/brotli, init latency и production bundle фиксируются в spike evidence.
- Новые модули `collision` и diagnostic benchmark, расширение runtime bootstrap/error handling и test fixtures, выведенных из `ArenaDefinition`.
- Simulation остаётся владельцем сериализуемых gameplay transform/velocity и правил; collision adapter не импортирует renderer/UI/input и не становится отдельным источником игрового состояния.
- Затронуты `GAME_SPEC` §§3, 7, 8 и открытые platform/performance решения §10; сетевой transport и модель authority не выбираются.
