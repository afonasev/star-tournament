# 09 — Плавные переходы анимации light-sport-robot

Статус: завершено, интегрировано в main и архивировано. Деплой не выполнен. Результаты и dev URL ниже относятся к исходной проверке.

Ветка: `codex/smooth-robot-animation`, от `d9a03e9`.

## Объём

- Сгладить только renderer-owned joint poses и сохранить current animation state contract.
- Сохранить authored GLB, simulation, collision, combat, input, camera и first-person viewmodel.
- Удлинить full review для наблюдаемого transition и проверить muted in-app Browser.

## Проверка

- Time-based controller tests, typecheck, focused tests, production build, strict OpenSpec и diff check.
- Сохранённые PNG/GIF из in-app Browser для idle, walk и fire; browser остаётся muted и renderer-only.

## Результат

- Controller отделяет target pose от отображённой и сглаживает только semantic joint rotations с time-based easing; реакции fire/hit получают более быстрый blend, destruction — более тяжёлый.
- Full review удерживает каждое состояние 1250 ms, чтобы переход был наблюдаемым.
- In-app Browser на `http://127.0.0.1:5194/?animationReview` подтвердил muted renderer-only viewer; canvas evidence сохранён в `docs/evidence/smooth-robot-animation/` и визуально проверен.
- `npm run typecheck`, 40 focused tests, `npm run build`, `openspec validate smooth-robot-animation --strict` и `git diff --check` прошли. Vite предупредил о chunks >500 kB.
