## 1. Domain state

- [x] 1.1 Добавить combat profile factory с metadata и валидацией; проверить EditMode invalid profile и независимость от proving profile.
- [x] 1.2 Реализовать life state, fire/refill/cooldown, damage/death/readiness/respawn; проверить границы переходов и stale life hits в EditMode.
- [x] 1.3 Проверить JSON roundtrip DTO и отсутствие mutation через read; тесты сохраняют held fire, cooldown и killer identity.

## 2. Проверки и handoff

- [x] 2.1 Выполнить EditMode/PlayMode и Mac Player build; записать counts и hashes.
- [x] 2.2 Выполнить muted native regression setup/live/pause с FPS, сохранить screenshot; явно отметить отсутствие combat adapter и открытые physical/performance gates.
- [x] 2.3 Обновить roadmap/handoff, выполнить strict OpenSpec validation и отдельный commit; не архивировать Stage 0 и не деплоить browser.
