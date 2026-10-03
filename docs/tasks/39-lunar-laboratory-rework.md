# F1 — Лунная лаборатория

Change: add-lunar-laboratory-map; spec planning commit 2851297. Owner 01a101c0-8615-7721-a7bd-bc88b276ac66, branch codex/lunar-laboratory-rework, baseline 1a1f993. User selected Astra/medium.

Согласованный scope: отдельный shot obstruction при сохранении optical sight; свободные от табличек двери; физические ящики/проходы внизу; стекло; Земля/фон/крыша/фасад; фонари по ограде и лампы в более тёмной среде. Human acceptance отдельно, внешний deploy запрещён.

Реализованы F1 изменения, focused EditMode 32/32 и PlayMode 6/6. Во время первой проверки исправлены восстановление legacy defaults Lab и тесный проход кладовой. После focused добавлена проверка движения по маршруту при ShotBlocked; финальный make check выполняется. Прежний immutable candidate сохранён.

Осталось: full gate, Player screenshots/natural match, визуальная оценка/исправления, commit, integration/spec sync, сохранение exact candidate, cleanup и finalization. До запуска повторно сверять место; пользователь чистит чужие старые сборки отдельно. Здесь чужие файлы/процессы не трогать.

Final automatic evidence: docs/evidence/lunar-laboratory-f1. make check 313+188 PASS; renderer-only follow-up focused physics 1 PASS/build PASS; final Player natural match 146 kills/139 pickups and 21 PNG. Q3 locked Mac blocks visible menu/HUD smoke, verified stage, merge/spec sync and finalization. Worktree remains owned and must be retained.

Checkpoint after first compaction: runtime commit c8e32a38b91dd372d07d8b945bc91f4f9291d2e1; exact retained candidate recorded in ../evidence/lunar-laboratory-f1/candidate.json. All autonomous checks complete; Mac lock freshly confirmed. Next action requires manual unlock, then exact candidate visible smoke and authorized integration/spec sync/cleanup. No owned processes running; preserve worktree and both candidates.
