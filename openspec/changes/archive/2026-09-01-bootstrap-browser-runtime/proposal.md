## Why

Star Tournament пока состоит только из спецификации и не имеет запускаемого runtime. Нужен минимальный проверяемый browser-фундамент, который сразу закрепит границы детерминированной симуляции, Three.js-renderer, DOM-интерфейса и балансных профилей, чтобы последующие игровые механики не создавали несовместимые архитектурные пути.

## What Changes

- Создать Vite/TypeScript приложение с одним WebGL canvas на Three.js и отдельным React DOM-слоем для shell и диагностик.
- Ввести lifecycle runtime, fixed-step runner, сериализуемое состояние, нормализованный `ActionFrame`, snapshots, replay-ввод и стабильный state hash без зависимостей симуляции от DOM, Three.js или React.
- Добавить статическую диагностическую `ArenaDefinition`, renderer-adapter и минимальную камеру, чтобы browser smoke test доказывал реальный запуск WebGL-сцены.
- Ввести типизированный `GameDesignProfile` `prototype-v1`, единый descriptor registry, range/cross-field validation, identity и content hash. Полноценная UI-лаборатория и история ревизий не входят в этот change.
- Добавить unit-тесты детерминизма, snapshot round-trip и profile validation, а также typecheck, production build и реальный browser smoke test.
- Зафиксировать технические инварианты и причины исключения их из Game Design Lab.
- Пользовательский эффект: появляется стабильно запускаемая диагностическая сцена и доказанная инфраструктура, на которую можно последовательно добавлять движение, бой, split-screen, арены и ботов.
- Затронуты `GAME_SPEC` §§ 6–8: Game Design Lab, архитектурные границы и критерии качества.
- Не входят: управление игроком, оружие, урон, смерть/respawn, match flow, lobby/HUD, split-screen, геймпады, процедурная генерация, боты, звук, полноценные настройки, сетевой transport и выбор authoritative-модели.
- Зависимости: актуальные стабильные версии Vite, TypeScript, Three.js, React DOM и тестового инструментария. Rapier не добавляется до отдельного deterministic/performance spike.
- Открытыми остаются целевые браузеры/TV и performance budget; этот change проверяется в доступном Chromium browser и не заявляет production-совместимость со всеми целевыми устройствами.

## Capabilities

### New Capabilities

- `browser-runtime-foundation`: запуск, lifecycle и строгие границы fixed-step simulation, Three.js-renderer, React DOM и replay/hash.
- `game-design-profile-core`: типизированный полный профиль, metadata-driven descriptor registry, валидация, identity и content hash без UI истории ревизий.

### Modified Capabilities

- Нет.

## Impact

- Новый browser application scaffold, package scripts и TypeScript-конфигурация.
- Новые модули `simulation`, `arena`, `render`, `ui`, `input`, `profiles` и `diagnostics`.
- Новые зависимости runtime/build/test; сетевые библиотеки и удалённые сервисы не добавляются.
- Форматы `SimulationSnapshot`, `ActionFrame`, `ArenaDefinition` и `GameDesignProfile` станут базовыми внутренними контрактами для следующих changes.
