## Context

См. proposal.md. Composition отделяет участников от seats; planner уже выдаёт общие LocalAction. Обычный ProvingGround пока создаёт human-only состав и подключает AI только через review flag.

## Goals / Non-Goals

**Goals:** минимальная интеграция обычного setup с frozen composition и общими actor/session/presentation factories.

**Non-Goals:** изменение planner, оружия, ресурсов или баланса; online, replay и полная миграция.

## Decisions

- Небольшой data-only draft хранит записи ботов (имя, сложность, команда); Build создаёт проверенный NativeMatchComposition. Альтернатива — UI-only массивы — хуже проверяется и смешивает validation с renderer.
- Число людей ограничено свободными participant slots, bots не удаляются молча. Удаление конкретного бота сохраняет остальные записи. Старт требует валидный состав и input.Ready; invalid draft остаётся редактируемым.
- ProvingGround включает NativeBotMatchDriver по наличию Bot-kind в frozen composition, без зависимости от diagnostic. Seed создаётся один раз перед матчем и сохраняется для Repeat; поведение использует существующие именованные profiles. Новых gameplay чисел нет.
- Общие CreateActor/TrooperVisual/CombatPresentation обслуживают всех. LocalSeatLayout и SeatInputCoordinator продолжают обслуживать только людей; 1 full, 2 left/right, 3 четверти с таблицей, 4 четверти.
- Setup получает отдельную колонку ботов с доступными native Buttons. Существующие action maps, focus, pause/reconnect и cursor capture сохраняются; AI не тикает в паузе.

## Risks / Trade-offs

- [Восемь строк и split-screen читаемость] → screenshots FHD/4K, проверка выбора controls и таблиц.
- [Повтор сохранил stale AI] → тесты новых session/driver и frozen профилей.
- [Diagnostic не доказывает реальные устройства или 60 FPS] → явно открытые physical/TV/target-performance gates; ограниченный muted Player review.

## Migration Plan

Отдельная ветка и один commit; не архивировать, не интегрировать, не deploy. Rollback — отмена этого commit. Browser baseline не меняется.
