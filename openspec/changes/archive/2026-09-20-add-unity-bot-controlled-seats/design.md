## Context

Baseline f29c449. NativeBotSetup считает отображаемые места людьми; composition запрещает Bot mapping; input требует устройства для каждого view. BotDriver уже выбирает planners по Kind, CombatPresentation уже адресует камеры через mapping. Read-only astra_architect memo подтверждает повторное использование этих границ.

## Goals / Non-Goals

**Goals:** расширить существующий mapping и frozen lifecycle, сохранив один action/session/presentation pipeline.

**Non-Goals:** отдельный spectator registry, переключаемые камеры, AI-политики, replay, доставка, новые ассеты и числовой баланс.

## Decisions

- Существующий participant→viewport mapping допускает Bot и LocalHuman; каждый human обязан иметь view. Альтернатива отдельного spectator runtime дороже и не нужна. Подмена Bot на LocalHuman запрещена.
- Draft хранит kind/difficulty каждого отображаемого места, дополнительные боты остаются отдельно. Имена AI views привязаны к месту и не конфликтуют с именами дополнительных ботов.
- Human-only mask в SeatInputCoordinator определяет Ready, joining, capture и disconnect; изменение kind освобождает устройство/очередь. Human разрешён на любом индексе.
- AssembleLocalActions копирует только human frames; существующий BotDriver пишет все Bot frames, затем единственный Session.Tick.
- Escape доступен оператору вне gameplay ownership. В all-AI Start любого gamepad открывает паузу; при наличии human-мест сохраняется правило Start только назначенных gamepads. UI input module обслуживает меню. AI-only не захватывает курсор; focus loss ставит матч на паузу. Resume/Repeat требуют только human devices.
- Frozen composition хранит kind/difficulty/mapping; Repeat пересоздаёт driver и presentation с прежними seed/profiles. Выход возвращает draft. Камеры/руки/HUD и owner layers остаются общими; root motion выключен.

## Risks / Trade-offs

- LocalCount больше не human count → отдельный подсчёт для подписей, explicit input mask, mixed/sparse tests.
- Протекание operator actions в AI → фильтрация действий и отрицательные tests.
- Lifecycle/culling AI views → PlayMode и PNG live/death/killcam/respawn.
- Четыре камеры и восемь AI нагружают Mac → диагностические frame-time данные без закрытия целевого performance gate.

## Migration Plan

Одна ветка и commit, tests/build/muted Player 2/3/4 FHD/4K; без archive/integration/deploy. Откат отдельным revert будущего commit. Replay и поставка отложены; финальная пользовательская приёмка позднее.
