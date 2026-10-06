## ADDED Requirements

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
