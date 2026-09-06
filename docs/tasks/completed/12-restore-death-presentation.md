# 12 — Restore hit-driven death presentation

Статус: завершено.

- Branch: `codex/restore-death-presentation`.
- Scope: восстановить утверждённое renderer-only падение GLB-робота и тело на floor contact после конфликтного merge, не меняя симуляцию, collision, replay или эффекты выстрела.
- Проверки: `npm run typecheck`; `npm test` — 61 файлов / 358 тестов; production build; muted in-app Browser и headless Chrome review финальной позы.
- Evidence: `docs/qa/restore-death-presentation-rest.png` — LOD0, `death-rest`; GLB-робот с оружием лежит на floor contact, без placeholder-сферы.
