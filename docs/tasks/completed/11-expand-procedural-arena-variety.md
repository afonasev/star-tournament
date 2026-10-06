# 11 — Expand procedural arena variety

Статус: завершено и принято пользователем 2026-09-06. Физическая keyboard/mouse-приёмка проведена пользователем в muted Chrome, потому что in-app Browser не выдаёт pointer lock; это ограничение среды, а не игровая ошибка.

- Change: `expand-procedural-arena-variety`.
- Worktree проверки: `/Users/eaafonasev/.codex/worktrees/19d3/star-tournament`.
- Branch: `codex/verify-procedural-arena-variety`; исходная base `b6a0050`.
- Scope: approved recipe graph, elevations/ramps, surface query masks, window/barrier/relief presentation, local manifest lamps, profile and legacy compatibility.
- Приёмка: пользователь подтвердил medium seed 1 (`split-atrium`, hash `aeb3500d`) и large seed 3 (`flanked-courtyard`, hash `d89504e0`): оба направления ramps, collision у низких и высоких barriers, прострел через barriers и relief niches, блокировку shots окнами. Pointer lock и mouse-look подтвердились в Chrome; подробности в [отчёте](../../qa/expand-procedural-arena-variety/repair/README.md).
- Проверки после rebase: strict OpenSpec — 20 specs PASS; `npm run typecheck` PASS; `npm test` — 59 файлов / 347 тестов PASS.
- Архив: `openspec/changes/archive/2026-09-06-expand-procedural-arena-variety/`; шесть delta-spec синхронизированы с canonical specs.
- Интеграция: QA commit `97d4f09` и archive commit `060658d` fast-forward включены в `main`.
- Публикация остаётся технически недоступна: в репозитории нет Git remote, deploy script или hosting configuration, поэтому production URL и post-deploy проверка не могли быть выполнены.

2026-09-06: после пользовательского playtest исправлены масштаб wall-bay, canonical portal solids и UV/ORM coverage. Начать новый матч с `arena-recipes-v4`; medium seed 1 имеет hash suffix `aeb3500d`. Проверить стойки, дуги, стеновые панели и фактуру пола.
