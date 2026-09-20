# Unity bot navigation — 2026-09-20

Проверен внутренний срез `add-unity-bot-navigation`, база `6f8ed15`, ветка `codex/unity-bot-navigation`, worktree b6fa. Unity 6000.3.23f1, профиль `unity-bot-navigation-v1@1`. [Build identity и source hashes](identity.json).

## Проверка

- EditMode **61/61**, PlayMode **44/44**, Mac Development build **PASS**. XML приложены. После этих tests менялся только diagnostic frame/allocation measurement; финальная сборка и оба Player journey проверяют его.
- Actual CharacterMotor routes: stairs/ramp × оба направления × три lanes; mid-transition restore и смена цели сохраняют выбранный выход. Ранняя/поздняя горизонтальная скорость подъёма в fixtures 9.000/9.000, накопительного замедления нет.
- Physical tests: перекрывающиеся XZ разных этажей, partial/disconnected route, чужой NavMesh при отключённой owned surface, живой blocker, узкий occupied corridor. Нет teleport/push/collider bypass; попытки recovery ограничены.
- State tests: independent snapshots, atomic invalid restore, moving-goal stuck budget, stale Arrived и same-goal retry, honest memory expiry. Lifecycle: pause, death, frozen Repeat, menu disposal.
- **16 PNG + 16 JSON на каждом FHD/4K**, реальные размеры 1920×1080 / 3840×2160, focus/mute подтверждены во всех состояниях. Runtime exceptions отсутствуют; completion marker есть в обоих логах. Обе contact sheets и полный stairs-ascending просмотрены.
- Read-only Astra architecture/review: найденные clock/recovery/Arrived/transition/lifecycle дефекты исправлены и покрыты regressions; последний review не нашёл блокеров в срезе.
- [Обычный Player regression через CUA](cua-journey.md). Strict OpenSpec и diff check проверяются перед коммитом.

## Состояния Player

[Обзор FHD](fhd-contact-sheet.png), [обзор 4K](4k-contact-sheet.png). Полные PNG/JSON в [FHD](fhd/) и [4K](4k/).

`start-empty`; для stairs и ramp: ascending → upper → descending → lower; direct-observed-goal → hidden-remembered-goal → expired-goal; paused-route → resumed-route → repeat-empty → return-setup.

Journey использует fixture placements только для начальных условий. Далее навигация работает через advancing session: Observe → Sample → ordinary action → один Session.Tick. Пауза сохраняет session clock и весь navigation snapshot; Repeat очищает намерение; menu удаляет driver. Диагностический баннер перекрывает часть верхнего HUD намеренно и не относится к shipping UI.

## Performance diagnostic

| Измерение | samples | p50 ms | p95 ms | p99 ms | worst ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| FHD foreground intervals | 4763 | 8.334 | 9.611 | 12.865 | 84.275 |
| 4K foreground intervals | 4659 | 8.340 | 9.532 | 11.178 | 237.175 |
| FHD route query | 1000 | 0.1015 | 0.1167 | 0.4366 | 0.5800 |
| 4K route query | 1000 | 0.1071 | 0.1198 | 0.5204 | 10.4017 |

Mac14,10 / Apple M2 Pro / macOS 14.6.1. Интервалы между двумя наблюдёнными foreground Running Update, около 42/43 секунд, один navigator + perception/motor, три trooper viewport. Включают screenshot/UI cost. Raw intervals сохранены; route-query benchmark отдельный синхронный проход. GPU timing недоступен. Mono allocation counter возвращал нефункциональный ноль: `allocationCounterAvailable=false`, bytes=-1. Это **DIAGNOSTIC_NOT_TARGET_60FPS_ACCEPTANCE**. Редкие задержки сохранены и не объявлены исправленными.

## Неудачные и заменённые прогоны

Полный архив сохраняет первый `fhd-startup-stall`, native stack sample (`GfxDeviceMetal::WaitForLastPresentationAndGetTimestamp`) и лог. Процесс остановлен после зависания начального захвата. Harness теперь ждёт завершения Unity splash перед ScreenCapture; последующие journeys прошли. Причина первоначального native stall полностью не доказана.

`fhd-premeasurement-fix` / `4k-premeasurement-fix` сохраняют завершённые journeys с ошибочным первым unscaledDeltaTime, включавшим ожидание до сбора (31.4/19.5 секунды). Эти performance JSON заменены финальными; исходные артефакты не удалены. Исправление измерителя не доказывает устранения старого ~1009 ms spike из teams-среза.

## Границы

Это navigation capability, ещё не playable combat AI: shipping planner/strafe/jumps/weapon/support, три difficulty, setup, participant/local-seat separation, solo/8 и independent evaluation остаются в полной [матрице миграции](../../UNITY_MIGRATION_MATRIX.md). NavMesh query глобален; физическая проверка принадлежности пути owned арене не гарантирует эксклюзивную query-source isolation для разных generated maps. Snapshot navigation не определяет полный recorded-state replay.

Physical gamepads/TV, художественная приёмка, reference hardware/quality/internal scale и длительный foreground 60 FPS открыты. Main/archive/deploy не выполнялись.

Полный архив с Player zip, логами, superseded/stall evidence и SHA256 manifest: `/Users/eaafonasev/Documents/Codex/qa-vault/star-tournament/bot-navigation-2026-09-20`.
