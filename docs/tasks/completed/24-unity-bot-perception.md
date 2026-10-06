# 24 — Unity: честные наблюдения и память бота

Статус: реализован и проверен для отдельного коммита change `add-unity-bot-perception`. Ветка `codex/unity-native-bots`, worktree `/Users/eaafonasev/.codex/worktrees/292d/star-tournament`; база `4521a7d` поверх native human teams. Исходный worktree 83e9 сохранён.

## Реализация

Срез выбран после read-only Astra memo по утверждённому bot contract. `NativeBotObservationProvider` отделяет session/PhysicsScene от будущего planner: горизонтальный FOV, ray от eye к torso anchor, WorldLayer occlusion. Movement-only barriers не закрывают LOS; allied pellet blocking остаётся прежним.

`NativeBotPerception` хранит только direct sightings и сообщения союзников. Скрытое движение/смерть/respawn противника не обновляет память. Reports задержаны, сохраняют исходные position/life/time, не ретранслируются и не продлевают срок памяти. Очередь ограничена receiver/source/target; старые сообщения не вытесняют свежие знания. Собственная смерть/новая жизнь очищает знания и incoming queue. Snapshot v1 валидирует и копирует только состояние восприятия, без обещания полного replay.

`unity-bot-perception-v1@1` переносит FOV/срок памяти трёх уровней и communication delay из botRules; metadata управляют Inspector и валидацией. Статистики боя, trooper assets, human-only setup и обычный input flow не менялись. Sensor активен только в opt-in development review; игровых ботов пока нет.

## Проверка

- EditMode **50/50**, PlayMode **36/36**, Mac Development build **PASS**.
- 11 native muted PNG/JSON в каждом FHD/4K, focus подтверждён. Direct→hidden→remembered→expired→reacquired, delayed team report, FFA isolation, pause/Repeat/setup. Все состояния просмотрены.
- CUA обычного Player: keyboard join, 4→3 seats, teams, camera diagnostic, pause, Repeat, menu, Exit. Нет физических P2/P3 gamepads, поэтому обычный controlled match не объявляется проверенным.
- Astra review: блокирующих дефектов не найдено; по замечанию добавлены real-geometry FOV fixtures 60°/70°. Strict OpenSpec и diff check проходят.
- 10000 samples Observe+Sample: FHD p95 0.0058 ms; 4K p95 0.0061 ms. Это узкий CPU diagnostic трёх наблюдателей, не frame time и не длительная приёмка 60 FPS.

[Evidence, XML, snapshots, screenshots и build identity](../../evidence/unity-bot-perception-2026-09-20/README.md). Полный архив: `/Users/eaafonasev/Documents/Codex/qa-vault/star-tournament/bot-perception-2026-09-20`.

## Следующий срез

Следующая задача выбирает ограниченную часть playable bots: planner/navigation/weapon/support и participant/local-seat separation пока отсутствуют. Не считать честные наблюдения готовым противником. Playable solo и восемь scene participants остаются отдельными срезами.

Pause/Repeat evidence относится к явному sampling/reconstruction диагностического harness, а не ещё не подключённому shipping sensor lifecycle. Полная Unity replay-схема остаётся открытой.

Real gamepads/TV, художественная приёмка trooper, reference hardware/quality/internal scale и длительный foreground 60 FPS открыты. Прежний секундный FHD frame spike не расследован этим срезом. Этап 2 целиком не завершён. Main/archive/deploy не выполнялись.
