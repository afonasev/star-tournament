## Why

Технический runtime и collision foundation уже доказаны, но прототип пока нельзя играть как arena shooter. Нужен минимальный вертикальный FPS-срез, который проверит связку детерминированной симуляции, first-person камеры, keyboard/mouse input и двухствольного дробовика до усложнения split-screen, матча и ботов. Первый физический playtest также выявил заметный нагрев устройства, поэтому slice не считается готовым без устранения лишней presentation-нагрузки и постоянного performance-gate для критичных изменений.

## What Changes

- Добавить одного локального keyboard/mouse игрока с pointer lock, WASD/strafe, ground acceleration, прыжком, гравитацией и умеренным air control на Rapier collision adapter.
- Добавить first-person камеру с yaw/pitch, настраиваемыми sensitivity/FOV и явным mouse-focus lifecycle.
- Добавить двухствольный hitscan-дробовик без перезарядки: один click расходует одну из 20 единиц боезапаса, выстрел создаёт детерминированный набор дробин, учитывает occlusion и зоны head/torso/limb.
- Добавить лёгкие человекоподобные robot target mannequins как детерминированные combat targets для проверки попаданий, урона и уничтожения; они не являются ботами и не возрождаются в этом change.
- Заменить постоянную техническую панель на компактный игровой HUD для одного viewport; collision/profile diagnostics оставить доступными только в debug-режиме.
- Исключить повторные WebGL submissions для неизменившегося simulation tick, ограничить backing-buffer и HUD cadence именованным presentation-профилем и не выполнять simulation/render/HUD work на паузе или скрытой вкладке.
- Добавить воспроизводимый browser performance-gate с presentation-only telemetry, production-preview сценариями и обязательной матрицей запуска для критичных runtime/render/content изменений.
- Расширить `prototype-v1` и единый descriptor registry всеми числовыми параметрами движения, камеры, capsule и дробовика, которые использует этот срез.
- Обновить `GAME_SPEC` §§2–3, 5–8 и журнал после фактической browser-проверки.
- Non-goals: геймпад, split-screen, полноценные участники/боты, scoring, таймер матча, death/killcam/respawn, процедурная генерация, меню и online adapter остаются отдельными changes. Пользовательские graphics presets, абсолютный температурный порог и обещание одинакового энергопотребления на всех устройствах также не входят в slice.

## Capabilities

### New Capabilities

- `first-person-player-control`: детерминированное перемещение одного локального игрока, first-person camera intent и keyboard/mouse focus lifecycle.
- `double-barrel-shotgun-combat`: боезапас, cadence, hitscan pellets, occlusion, hit zones, damage и combat-target feedback первого оружия.

### Modified Capabilities

- `browser-runtime-foundation`: browser application переходит от диагностического flyover к игровому first-person viewport и compact HUD, сохраняет debug diagnostics отдельно и получает проверяемый presentation/performance lifecycle.
- `game-design-profile-core`: полный профиль и descriptor registry получают обязательные параметры player movement, capsule, camera и shotgun behavior, а отдельный presentation-профиль — параметры pixel budget и HUD cadence без влияния на simulation identity.

## Impact

- Игрок впервые получает прямой управляемый FPS-цикл: захват мыши → движение/прыжок → прицеливание → выстрел → видимый результат урона.
- Затронуты `simulation`, `input`, `collision` adapter integration, Three.js renderer/camera, React DOM HUD, `prototype-v1`, presentation profile, replay/snapshot schemas, browser bootstrap и performance tooling/documentation.
- Новых runtime-зависимостей не требуется; используется уже принятый deterministic Rapier backend.
- GAME_SPEC: §§2–3 (игрок/движение/оружие), §5 (HUD), §6 (profile descriptors), §7 (границы simulation/input/camera/renderer), §8 (реальный keyboard/mouse browser playtest и performance-gate), §10 (размер первого вертикального среза и пока открытый hardware target).
- Новых блокирующих продуктовых решений этого change нет: для минимального среза фиксируется один локальный keyboard/mouse seat и robot mannequins; параметры баланса считаются provisional `prototype-v1` и остаются настраиваемыми через descriptors. Финальные reference hardware/OS/browser, minimum FPS и graphics presets остаются открытыми в `GAME_SPEC` §10, поэтому device-sensitive performance thresholds текущего gate помечаются provisional.
