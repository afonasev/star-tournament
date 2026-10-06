# 11 — Game Design Lab

> Статус: CLOSED — остаток работ CANCELLED / SUPERSEDED переходом на Unity, 2026-09-19, по явному решению пользователя. Исторические реализации и результаты ниже сохранены; незавершённая приёмка не объявляется успешной. Не возобновлять browser-разработку или интеграцию по этому handoff. Unity получает новую реализацию и тесты. См. `../../BROWSER_BACKLOG_CLOSURE.md`.

## Scope

Полноэкранная DOM-лаборатория создаёт и выбирает локальные immutable ревизии `GameDesignProfile` только для следующего матча. Она не открывается из pause menu и не меняет действующие session, arena, snapshot или replay.

## Acceptance

- Поля строятся из canonical descriptor registry, включая metadata диапазона и шага.
- Draft сохраняется только явной командой, а изменение закрытия требует Continue/Discard.
- Changelog показывает старое, новое и signed delta.
- Выбранный profile передаётся в generation и bootstrap следующего матча.
- Требуются automated checks, strict OpenSpec validation и muted in-app Browser evidence с сохранёнными PNG.
