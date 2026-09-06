# browser-runtime-foundation Specification

## Purpose

Capability задаёт наблюдаемый контракт запускаемого browser runtime и детерминированной симуляционной основы, на которой последующие игровые changes смогут строить единое локальное, replay и сетевое поведение.

## Requirements

### Requirement: Запускаемый browser runtime
Приложение SHALL запускаться как browser application, создавать один WebGL playfield и отдельный DOM-слой, а также показывать диагностический статус runtime без смешивания DOM и renderer state.

#### Scenario: Успешный запуск
- **WHEN** пользователь открывает dev или production build в совместимом браузере
- **THEN** приложение показывает диагностическую 3D-арену и DOM-статус активной симуляции без runtime errors

#### Scenario: WebGL недоступен
- **WHEN** браузер не может создать требуемый WebGL context
- **THEN** приложение показывает понятное DOM-сообщение об ошибке и не оставляет бесконечный пустой loading state

### Requirement: Детерминированный fixed-step runner
Runtime MUST обновлять игровое состояние только дискретными simulation ticks и MUST отделять simulation time от wall-clock и render time.

#### Scenario: Одинаковый поток команд
- **WHEN** два runner получают одинаковые initial snapshot, profile, arena seed и последовательность `ActionFrame`
- **THEN** после каждого соответствующего tick они создают одинаковый сериализуемый snapshot и state hash

#### Scenario: Разная частота renderer
- **WHEN** одна и та же последовательность tick-команд отображается с разным количеством render frames
- **THEN** итоговое simulation state остаётся одинаковым

### Requirement: Сериализуемые snapshots и replay
Симуляция SHALL предоставлять versioned snapshot и replay-ввод, достаточные для восстановления и повторного детерминированного выполнения без renderer-объектов.

#### Scenario: Snapshot round-trip
- **WHEN** snapshot сериализуется и затем восстанавливается поддерживаемой версией runtime
- **THEN** восстановленное состояние имеет тот же schema version, tick, данные сущностей, RNG state и state hash

#### Scenario: Повтор replay
- **WHEN** записанные `ActionFrame` повторно применяются к сохранённому initial snapshot
- **THEN** replay достигает того же конечного tick и state hash

### Requirement: Платформенно-независимая симуляция
Simulation package MUST NOT зависеть от DOM, WebGL, Three.js, React, физических устройств или wall-clock API; адаптеры SHALL передавать в неё только сериализуемые команды и данные.

#### Scenario: Проверка границы импортов
- **WHEN** выполняется автоматическая архитектурная проверка исходного дерева simulation
- **THEN** она не обнаруживает imports из renderer, UI, browser input или browser globals

### Requirement: Каноническое описание диагностической арены
Runtime SHALL загружать статическую versioned `ArenaDefinition`, которая является общим входом для simulation-side данных и renderer-adapter и не содержит Three.js objects.

#### Scenario: Одна arena identity
- **WHEN** diagnostic arena запускается в browser runtime
- **THEN** DOM-диагностика, simulation snapshot и renderer используют одну arena identity и один content hash

### Requirement: Управляемый lifecycle и resize
Runtime SHALL иметь явные операции start, stop и dispose, корректно адаптировать canvas/camera к размеру playfield и освобождать созданные renderer/UI resources при уничтожении приложения.

#### Scenario: Изменение размера
- **WHEN** размеры playfield изменяются
- **THEN** canvas и camera projection обновляются без изменения simulation state

#### Scenario: Остановка runtime
- **WHEN** вызывается dispose
- **THEN** animation loop, listeners, React root, geometry, materials и WebGL renderer освобождаются без последующих simulation ticks

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

### Requirement: Browser lifecycle полного single-seat match loop
Browser shell SHALL управлять взаимоисключающими surfaces `menu`, `match` и `results`, а running match SHALL отражать simulation phase `running`, `overtime` либо `finished`. Только running/overtime с активным pointer lock MUST выполнять fixed ticks и WebGL submissions; menu, paused, hidden и results MUST оставаться zero-work после transition settle, кроме явной presentation invalidation.

#### Scenario: Старт из меню
- **WHEN** пользователь подтверждает валидную match configuration
- **THEN** shell создаёт новый runtime/snapshot со свободной от static geometry local-seat capsule, показывает start-to-lock surface и не выполняет tick до подтверждённого pointer lock

#### Scenario: Старт без input-коррекции
- **WHEN** renderer впервые показывает initial snapshot до movement либо другого gameplay input
- **THEN** камера находится в валидной свободной позиции и не зависит от первого collision movement для выхода из стены

#### Scenario: Цвет mannequin-участника
- **WHEN** snapshot публикует живых stationary fixtures с разными participant colors либо mannequin возрождается
- **THEN** renderer показывает каждому его authoritative participant color и не заменяет все fixtures одним hardcoded цветом

#### Scenario: Завершение матча
- **WHEN** simulation snapshot становится finished
- **THEN** runtime прекращает tick scheduling, освобождает held input/pointer lock и публикует results без дополнительного gameplay tick

#### Scenario: Menu, pause и results performance
- **WHEN** browser performance probe наблюдает settled menu, pause либо results
- **THEN** counters содержат 0 simulation ticks, 0 periodic HUD publications и 0 WebGL submissions до user action или explicit invalidation

#### Scenario: Dispose и repeat
- **WHEN** shell выходит в menu или повторяет матч
- **THEN** старые listeners, runtime, collision world, renderer resources и React state освобождаются ровно один раз до владения новой сессией

### Requirement: Validated profile startup compatibility
Browser runtime SHALL использовать один exact validated profile для match configuration, arena generation, initial simulation snapshot и последующего snapshot parsing. Внутренний parser MUST NOT отклонять initial snapshot, созданный из этого же profile.

#### Scenario: Start current release match
- **WHEN** пользователь запускает матч с current valid release profile
- **THEN** browser создаёт ready match surface без `SimulationSnapshotError` до pointer lock и первого gameplay tick

### Requirement: Browser session из validated ArenaDefinition
Browser shell SHALL завершить profile validation, generation и arena validation до создания initial snapshot, collision world и renderer. Все runtime consumers MUST получать один immutable `ArenaDefinition`; fixture arena не может оставаться скрытым production default.

#### Scenario: Успешный generated startup
- **WHEN** выбранные size/seed/profile создают accepted arena
- **THEN** snapshot, collision checkpoint, renderer dataset и debug surface показывают одну arena identity и content hash до первого input tick

#### Scenario: Spatial source mismatch
- **WHEN** snapshot identity, definition, collision projection либо renderer description не совпадают
- **THEN** startup завершается стабильной compatibility error до gameplay tick и WebGL loop

### Requirement: Самодостаточный arena replay
Playable replay SHALL хранить validated `ArenaDefinition` один раз рядом с initial snapshot и MUST проверять совпадение id, seed, generator version и content hash до reconstruct collision world или применения action frame.

#### Scenario: Replay reconstruction
- **WHEN** replay запускается без исходной browser session
- **THEN** runtime восстанавливает derived collision/navigation data из embedded definition и получает те же per-tick hashes

#### Scenario: Подменена definition
- **WHEN** embedded definition не соответствует initial snapshot arena identity
- **THEN** replay отклоняется до первого action frame

### Requirement: Procedural arena performance evidence
Каждый size preset MUST проходить production-browser gate; densest accepted representative seed SHALL сохранять renderer counters, collision timings, generation/validation timings и lifecycle counters без включения telemetry в simulation/replay/hash.

#### Scenario: Representative sizes
- **WHEN** performance gate запускает small, medium и large seeds
- **THEN** portable cadence/lifecycle gates проходят, а device-sensitive counters публикуются как evidence или явно unavailable

### Requirement: Presentation style validated arena
Browser runtime SHALL применять утверждённый arena presentation style, active graphics-quality profile и versioned GLB asset manifest к renderer-derived surfaces одной immutable validated `ArenaDefinition`. Style, textures, decals, quality preferences, GLB asset loading, LOD, lights, fog и transient presentation MUST NOT входить в simulation snapshot, replay, RNG или state hash; failed mandatory asset preparation MUST блокировать только presentation startup со стабильной actionable error. Runtime MUST поддерживать display output до 3840×2160; при ограниченном presentation budget он SHALL уменьшать только internal render scale и presentation detail, сохраняя DOM/HUD layout и gameplay camera/simulation.

#### Scenario: Renderer style при неизменной симуляции
- **WHEN** один и тот же initial snapshot отображается с разной render cadence, после resize, с другим quality profile либо в debug surface
- **THEN** GLB style kit не меняет arena identity, participant transforms, simulation hash или количество gameplay ticks

#### Scenario: Presentation performance
- **WHEN** GLB style kit изменяет материалы, texture quality, LOD, decals, освещение или visibility treatment arena
- **THEN** production-browser performance gate сохраняет lifecycle counters и проходит утверждённые portable hard gates

#### Scenario: Ошибка обязательного asset
- **WHEN** manifest entry или required GLB нельзя подготовить до первого render
- **THEN** runtime показывает DOM actionable error, не создаёт WebGL loop и не изменяет snapshot либо replay

#### Scenario: 4K display output
- **WHEN** playfield отображается на 3840×2160 display output
- **THEN** browser runtime сохраняет читаемый DOM/HUD layout и применяет active graphics-quality profile без повышения simulation cadence либо нарушения pause/hidden zero-work contract

### Requirement: Performance evidence локальных теней

Production-browser performance gate SHALL фиксировать effective graphics preset и local-light shadow budget для renderer change, затрагивающего локальные тени. Gate MUST сохранять existing lifecycle counters и MUST NOT включать shadow telemetry в simulation, replay или state hash.

#### Scenario: Проверка quality tiers
- **WHEN** browser performance gate запускает renderer с каждым поддерживаемым graphics preset
- **THEN** report показывает active preset и effective shadow budget, а portable lifecycle hard gates сохраняют passed result
