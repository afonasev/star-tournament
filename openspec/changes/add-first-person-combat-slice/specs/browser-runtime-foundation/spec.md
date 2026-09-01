## ADDED Requirements

### Requirement: Игровой first-person boot surface
Browser runtime SHALL после готовности profile, collision и renderer показывать один first-person viewport, compact gameplay HUD и pointer-lock start overlay вместо постоянной технической панели; расширенная identity/benchmark диагностика MUST оставаться доступной только в явном debug-режиме.

#### Scenario: Обычная загрузка
- **WHEN** пользователь открывает приложение без debug-параметра
- **THEN** playfield занимает доступный viewport, HUD не закрывает центр действия, а start overlay кратко объясняет click-to-play и базовые keyboard/mouse controls

#### Scenario: Debug-режим
- **WHEN** приложение открыто с документированным local debug-параметром
- **THEN** runtime дополнительно показывает profile, arena, collision identity, hashes и diagnostic benchmark state без изменения simulation

#### Scenario: Initialization error
- **WHEN** profile validation, collision initialization или WebGL setup завершается ошибкой
- **THEN** gameplay runtime не стартует и DOM показывает стабильное actionable сообщение причины

### Requirement: Browser acceptance первого playable slice
Первый playable slice MUST проходить проверку в реальном браузере, которая различает DOM readiness, WebGL presentation и фактическую реакцию simulation на keyboard/mouse actions.

#### Scenario: Keyboard/mouse smoke
- **WHEN** browser получает pointer lock и выполняет forward, strafe, look, jump и fire
- **THEN** screenshot/state evidence подтверждает изменение player transform, camera orientation, airborne/landing state, ammo и target damage без console errors

#### Scenario: Потеря focus и resize
- **WHEN** pointer lock теряется либо viewport меняет размер
- **THEN** gameplay pause overlay и camera resize работают без stuck actions, дополнительных ticks во время pause или WebGL errors

### Requirement: Энергоэффективный presentation lifecycle
Browser runtime SHALL отправлять WebGL frame только для нового simulation tick либо явной presentation invalidation; повторный animation frame с тем же snapshot MUST NOT создавать дублирующий render submission. Runtime MUST останавливать simulation ticks, WebGL submissions и periodic HUD publications на паузе и при скрытии страницы.

#### Scenario: Частота экрана выше simulation cadence
- **WHEN** browser вызывает animation frames чаще, чем появляются новые simulation ticks, и presentation не invalidated
- **THEN** renderer показывает последний snapshot без повторных WebGL submissions, а simulation state и input cadence не меняются

#### Scenario: Resize без нового simulation tick
- **WHEN** paused viewport меняет размер или pixel ratio и camera projection invalidated
- **THEN** renderer выполняет ровно один redraw последнего snapshot с новой projection и затем снова остаётся idle

#### Scenario: Пауза или скрытая вкладка
- **WHEN** pointer lock потерян либо документ становится hidden
- **THEN** после завершения lifecycle transition runtime выполняет ноль simulation ticks, ноль WebGL submissions и ноль periodic HUD publications до явного resume

### Requirement: Воспроизводимый browser performance-gate
Критичные изменения renderer, runtime cadence, resolution, cameras, effects, arenas, entity counts, simulation, collision, bots, split-screen, HUD cadence, mass events или shipping assets MUST проходить production-browser performance-gate отдельно от gameplay acceptance. Gate SHALL публиковать presentation-only измерения и MUST NOT изменять snapshot, replay, simulation hash или игровой input.

#### Scenario: Референсный прогон
- **WHEN** performance-gate выполняет документированные фазы pause, running-idle, movement/jump и combat burst
- **THEN** отчёт содержит simulation ticks, WebGL submissions, HUD publications, frame timing, renderer counters и доступные memory signals для каждой фазы с проверкой versioned budget

#### Scenario: Изменение только текста или narrative-документации
- **WHEN** изменение не затрагивает runtime, renderer, content, simulation, assets или измеримый UI lifecycle
- **THEN** performance-gate может быть пропущен с зафиксированной причиной по trigger matrix

#### Scenario: Недоступная метрика
- **WHEN** browser или устройство не предоставляет GPU timing, memory либо thermal telemetry
- **THEN** отчёт помечает метрику как unavailable, не подменяет её выдуманным значением и продолжает проверять доступные обязательные counters
