## Context

NativeMatchState сейчас индексирует FFA rows полем Seat. NativeCombatSession и Player используют этот индекс как human slot. Утверждённые правила team completion находятся в GAME_SPEC §5; см. proposal.md.

## Goals / Non-Goals

**Goals:** добавить состав и team ranking в существующий reducer без зависимости от Unity, устройств и presentation.

**Non-Goals:** подключение teams к NativeCombatSession, выбор friendly-fire правил, spawn, UI, ботов и replay. Reducer по-прежнему принимает уже разрешённые applied-damage events; он не разрешает попадания и не меняет здоровье.

## Decisions

- Immutable roster хранит mode и копию team assignments. Participant identity — стабильный индекс 0..N-1 внутри session; он не обозначает устройство/viewport. Историческое DTO поле Seat сохраняется для совместимости и поясняется в коде. Отдельная схема произвольных external IDs не нужна до network/restore контракта.
- Старый constructor создаёт FFA roster. Новый принимает явный roster; нет изменения native setup либо скрытых участников. Две непустые команды обязательны; в FFA команды отсутствуют.
- Winner остаётся individual slot только для FFA, WinnerTeam отдельно указывает Team A/B. Team totals вычисляются из personal scores после полного tick. Tie и trigger priority используют тот же reducer, без второго алгоритма scoring.
- Snapshot копирует roster/rows/team totals; profile остаётся unity-native-match-v1@1. Лимит 2–8 и две команды — утверждённый structural product contract, не новое балансируемое число.
- Не создаём второй team reducer: общий путь снижает риск расхождения overtime/chain/assist правил. Не меняем Seat на Participant во всех adapters: это преждевременное подключение native roster.

## Risks / Trade-offs

- Путаница team winner с participant → разные поля и тесты JSON/FFA regression.
- Мутация caller-owned arrays → defensive copies и тесты до/после finish.
- Domain готовность могут принять за playable teams → явно отделить handoff от следующего native integration.
- Performance нового team aggregation проверяется bounded eight-participant diagnostic, без заявления physical/60FPS acceptance.
