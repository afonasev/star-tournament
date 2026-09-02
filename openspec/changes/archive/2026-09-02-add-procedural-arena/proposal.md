## Why

Текущий playable runtime всегда запускает одну жёстко заданную collision-fixture арену, а позиции участников принадлежат отдельному scenario, поэтому утверждённый контракт воспроизводимых процедурных арен пока не даёт игроку нового пространства для матчей. Change создаёт первый играбельный одноуровневый procedural slice с осмысленными маршрутами, тремя размерами и точной воспроизводимостью, сохраняя simulation/renderer/collision/replay границы из `GAME_SPEC` §§2, 6–8.

## What Changes

- Генерировать канонический `ArenaDefinition` из точного seed, `generatorVersion`, выбранного size preset и identity полной сохранённой ревизии `GameDesignProfile`.
- Добавить размеры `small`, `medium` и `large` как разные topology recipes семейства «Разорванное кольцо», а не как масштабирование одной геометрии: связные стеновые цепочки формируют комнаты, коридоры и читаемые дверные проёмы, а замкнутый внешний контур имеет размерно-зависимый неровный силуэт вместо прямоугольной коробки; все размеры поддерживают FFA, team mode и roster до восьми участников.
- Выводить collision geometry, navigation graph, spawn regions/slots и renderer description из одной semantic model; убрать spatial authority из combat scenario.
- Проецировать в collision world капсулы всего живого roster, чтобы participants блокировали проход друг сквозь друга; отключать capsule после смерти и возвращать при respawn.
- Валидировать structural integrity связной архитектуры, проходимость коридоров и дверей, capsule clearance, минимум 12 spawn slots, безопасно разнесённое одновременное размещение roster, FFA/team fairness и отсутствие единственного обязательного chokepoint до старта матча.
- Добавить в setup выбор размера и необязательный ручной seed. Auto seed разрешается в конкретный seed до старта; Repeat сохраняет exact arena, новый auto-seed матч получает новый seed.
- Показывать actionable generation error с seed и явными действиями retry/new seed/fallback; shipped fallback arena никогда не применяется молча.
- Включить generator numeric settings и metadata в immutable `prototype-v1`; параметры влияют только на следующую генерацию.
- Включить полную `ArenaDefinition` в replay один раз и проверять совпадение её identity с initial snapshot до первого tick.
- Сохранить текущие аналитические box-примитивы и material slots `floor/wall/accent` как временную presentation; финальный visual direction не утверждается.
- Проверить change property-based seed corpus, deterministic replay/collision reconstruction, production build, browser performance gate и физическим in-app Browser playtest с выключенным звуком и актуальными screenshots.

Non-goals: vertical routes, jump-only links, pickups, hazards, bots, runtime bot navigation, dynamic geometry, GLB module kit, split-screen, gamepad и online adapter.

## Capabilities

### New Capabilities

- `procedural-arena-generation`: детерминированная одноуровневая генерация semantic arena, size presets, validation/fairness gates и derived projections.

### Modified Capabilities

- `game-design-profile-core`: полный профиль получает generator rules и descriptor metadata для трёх size presets.
- `match-loop-ui`: setup получает выбор размера, auto/manual seed и actionable generation/fallback surface.
- `browser-runtime-foundation`: browser session создаётся из validated generated arena, а replay становится самодостаточным относительно arena content.
- `match-session-lifecycle`: initial placement и fixture respawn используют arena spawn slots; Repeat сохраняет exact arena, новый auto-seed матч создаёт новую.

## Impact

Затрагиваются `src/arena`, `src/profiles`, `src/match`, `src/scenario`, `src/simulation`, collision/runtime adapters, Three.js renderer и React setup UI. Потребуются schema/revision bumps для `ArenaDefinition`, `GameDesignProfile`, match configuration, snapshot/replay и соответствующие migration-by-rejection checks. Новых runtime-зависимостей и удалённых сервисов не требуется.

Открытыми вне change остаются финальный visual/material kit, reference hardware и универсальные team emergency rules будущего dynamic respawn; текущий slice применяет детерминированное distinct-slot batch allocation и явный validated fallback.
