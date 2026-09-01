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
