## Why

Четырёхместный native match-loop требует четыре устройства даже для двух или трёх людей. Следующий связный срез этапа 2 переносит утверждённые GAME_SPEC §2/§5 layouts и variable local-seat ownership до добавления teams/bots и восьми участников.

## What Changes

- Native setup выбирает 2–4 local seats; только выбранные seats требуют уникальных устройств.
- Камеры и HUD используют общий layout: два left/right, три равных 2×2 с постоянной read-only таблицей в четвёртой ячейке, четыре 2×2.
- Реальный roster, collision, combat и таблицы соответствуют активному составу; скрытые участники не дополняют матч.
- Repeat сохраняет состав и assignments; только setup меняет количество. Удалённые устройства освобождаются, disconnect активного seat требует явного resume/rebind.
- Полные native проверки, FHD/4K muted evidence, handoff 20 и отдельный commit.

## Capabilities

### New Capabilities
- `unity-native-seat-layouts`: изменяемый состав local seats и согласованные native viewports/HUD/standings.

### Modified Capabilities
Нет.

## Impact

ProvingGround, SeatInputCoordinator, native standings и lifecycle адаптеры, tests/docs. Зависимость: native match-loop `001824b`; чистая база main `26ce3ff` является предком, missing commits взяты без редактирования старых worktrees. Teams/bots, восемь участников, assets, сеть/replay, integration/archive/deploy вне среза. Physical/TV/performance gates остаются открытыми.

Текущий независимый контур реализации — 2–4 человека. Одиночный запуск до ботов требует отдельного решения (вопрос отправлен пользователю) и не включён без ответа. Это ограничение текущего среза, а не отмена утверждённого single viewport.
