## 1. Контракты и profile

- [x] 1.1 Обновить `docs/GAME_SPEC.md` и change log утверждёнными пятью рецептами, правилами support/layer, тоннелями в три capsule диаметра и material direction; проверить `openspec validate expand-multilevel-arena-variety --strict`.
- [x] 1.2 Расширить versioned `ArenaDefinition`, parser и legacy compatibility support/layer/transition semantics; проверить canonical round-trip, content hash и historical definition tests.
- [x] 1.3 Добавить descriptor-backed multi-level generator budgets и cross-field clearance/slope/headroom validation; проверить profile schema, range/step, descriptor coverage и hash tests.

## 2. Генерация, navigation и физика

- [x] 2.1 Реализовать пять seed-selected recipe graphs и compiler canonical floors, slabs, halls, galleries, balconies, basements и transitions; проверить deterministic recipe matrix для каждого size.
- [x] 2.2 Реализовать support-aware floor selection и sparse layer navigation; проверить overlapping-XZ negative fixtures, byte-identical routes и разделение aim/arrival distance.
- [x] 2.3 Обновить gameplay/spawn/LOS/fairness validation для всех combat regions, межэтажных slabs и triple-lane tunnel clearance; проверить FFA/teams seed corpus без ослабления существующих budgets.
- [x] 2.4 Обновить physical route validation и collision fixtures для feet Y, support identity, обеих сторон transition, headroom и corner sweep; проверить реальными derived Rapier worlds.

## 3. AI и presentation

- [ ] 3.1 Обновить planner/headless telemetry для declared layers, basement bypass и transition recovery; проверить deterministic bot coverage без repeated stuck recovery.
- [ ] 3.2 Добавить semantic renderer treatment и local manifest-compatible material variants для hall/gallery/balcony/basement; проверить material coverage, texture residency, dispose, UV/z-fighting и participant contrast.
- [ ] 3.3 Обновить renderer и performance diagnostics для canonical slabs/guards/ramps без presentation-owned collision; проверить large dense recipe на quality tiers и split-screen budget.

## 4. Интеграционная приёмка

- [x] 4.1 Выполнить targeted/unit/property/collision/AI/renderer tests, полный `npm test`, typecheck, build, deterministic replay и `git diff --check`; сохранить concise evidence для пяти families × size × mode.
- [ ] 4.2 Выполнить browser performance gate на наиболее насыщенной large arena и подтвердить отсутствие изменения simulation hashes от quality tiers.
- [ ] 4.3 Провести muted in-app Browser playtest с keyboard/mouse: каждый family, оба направления рамп, балконный прострел, slab blocking и стрейф в минимальном тоннеле; сохранить и визуально проверить актуальные PNG каждого изменённого состояния.
- [ ] 4.4 Проверить `openspec validate expand-multilevel-arena-variety --strict`, подготовить dedicated commit, fast-forward интеграцию в актуальный `main`, архивировать change отдельным commit и выполнить deploy/post-deploy проверку при наличии локально настроенной publish target.

## Evidence: isolated physical-navigation slice (2026-09-08)

Branch `codex/multilevel-physical-navigation`; no main integration, archive or deployment. See `docs/tasks/14-multilevel-physical-navigation.md` for the corpus and remaining acceptance gates. Previous 4.2 evidence predates the new physical geometry and must be refreshed. Task 3.1 has guided-goal traversal and telemetry coverage, but autonomous combat layer coverage is not yet accepted. Browser visual fixtures are captured muted; keyboard/mouse acceptance is blocked by denied pointer lock. Presentation and performance tasks remain open.
