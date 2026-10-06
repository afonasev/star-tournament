## 1. Контракты и profile

- [x] 1.1 Обновить `docs/GAME_SPEC.md` и change log утверждёнными пятью рецептами, правилами support/layer, тоннелями в три capsule диаметра и material direction; проверить `openspec validate expand-multilevel-arena-variety --strict`.
- [x] 1.2 Расширить versioned `ArenaDefinition`, parser и legacy compatibility support/layer/transition semantics; проверить canonical round-trip, content hash и historical definition tests.
- [x] 1.3 Добавить descriptor-backed multi-level generator budgets и cross-field clearance/slope/headroom validation; проверить profile schema, range/step, descriptor coverage и hash tests.

## 2. Генерация, navigation и физика

- [x] 2.5 Реализовать ограниченный каталог графов rooms/halls/tunnels с разной структурой внутри family/size; закрытые самостоятельные этажи преобладают в profile-owned распределении.
- [x] 2.6 Добавить canonical локальные ceilings и проёмы, замкнутые тоннели и самостоятельную сеть комнат верхнего этажа; сохранять два разнесённых перехода и проверять jump/headroom на стыках.
- [x] 2.7 Добавить descriptors высот по типам помещений, весов композиций, полезной площади этажей и structural corpus thresholds; версионировать без изменения historical releases.
- [x] 2.8 Проверить разнообразие графов, размещения переходов и высот внутри family/size; повторить physical/AI/fairness/replay corpus для новых ceilings и topology.

- [x] 2.1 Реализовать пять seed-selected recipe graphs и compiler canonical floors, slabs, halls, galleries, balconies, basements и transitions; проверить deterministic recipe matrix для каждого size.
- [x] 2.2 Реализовать support-aware floor selection и sparse layer navigation; проверить overlapping-XZ negative fixtures, byte-identical routes и разделение aim/arrival distance.
- [x] 2.3 Обновить gameplay/spawn/LOS/fairness validation для всех combat regions, межэтажных slabs и triple-lane tunnel clearance; проверить FFA/teams seed corpus без ослабления существующих budgets.
- [x] 2.4 Обновить physical route validation и collision fixtures для feet Y, support identity, обеих сторон transition, headroom и corner sweep; проверить реальными derived Rapier worlds.

## 3. AI и presentation

- [x] 3.4 Согласовать concept plans обоих этажей и разрезы трёх структурных вариантов из `artifacts/multilevel-navigation/room-layout-concepts.png`; затем проверить соответствующие игровые PNG.
- [x] 3.5 Заменить общий presentation ceiling новых карт отображением canonical локальных потолков; проверить нижние поверхности, локальное освещение и закрытые ramp corridors.

- [ ] 3.1 Обновить planner/headless telemetry для declared layers, basement bypass и transition recovery; проверить deterministic bot coverage без repeated stuck recovery.
- [ ] 3.2 Добавить semantic renderer treatment и local manifest-compatible material variants для hall/gallery/balcony/basement; проверить material coverage, texture residency, dispose, UV/z-fighting и participant contrast.
- [ ] 3.3 Обновить renderer и performance diagnostics для canonical slabs/guards/ramps без presentation-owned collision; проверить large dense recipe на quality tiers и split-screen budget.

## 4. Интеграционная приёмка

- [x] 4.1 Повторить targeted/unit/property/collision/AI/renderer tests, полный `npm test`, typecheck, build, deterministic replay и `git diff --check` после переработки помещений; сохранить concise evidence для пяти families × size × mode. Предыдущие 386 tests относятся только к срезу `a82a048`.
- [ ] 4.2 Выполнить browser performance gate на наиболее насыщенной large arena и подтвердить отсутствие изменения simulation hashes от quality tiers.
- [ ] 4.3 Провести muted in-app Browser playtest с keyboard/mouse: каждый family, оба направления рамп, балконный прострел, slab blocking и стрейф в минимальном тоннеле; сохранить и визуально проверить актуальные PNG каждого изменённого состояния.
- [ ] 4.4 Проверить `openspec validate expand-multilevel-arena-variety --strict`, подготовить dedicated commit, fast-forward интеграцию в актуальный `main`, архивировать change отдельным commit и выполнить deploy/post-deploy проверку при наличии локально настроенной publish target.

## Evidence: isolated physical-navigation slice (2026-09-08)

Branch `codex/multilevel-physical-navigation`; no main integration, archive or deployment. See `docs/tasks/completed/14-multilevel-physical-navigation.md` for the corpus and remaining acceptance gates. Previous 4.2 evidence predates the new physical geometry and must be refreshed. Task 3.1 has guided-goal traversal and telemetry coverage, but autonomous combat layer coverage is not yet accepted. Browser visual fixtures are captured muted; keyboard/mouse acceptance is blocked by denied pointer lock. Presentation and performance tasks remain open.

## 5. Подтверждённые лестницы и скорость подъёма

- [x] 5.1 Устранить накопительное торможение на склоне; сравнить flat/uphill/downhill speed, диагональ, стены, потолки и прыжок.
- [x] 5.2 Добавить versioned stair transitions, canonical ступени и profile descriptors; сохранить historical replay/profile paths.
- [x] 5.3 Генерировать примерно 50% лестниц по seed/transition ID; проверить распределение и geometry/clearance corpus.
- [x] 5.4 Расширить support-aware navigation, autostep acceptance и AI для обеих сторон лестницы, трёх lanes, поворотов и replan на ступени.
- [x] 5.5 Сгладить камеру на ступенях, проверить headroom и соответствие прицела выстрелу.
- [ ] 5.6 Повторить полный test/build/replay, browser performance и muted playtest, сохранить PNG и dedicated implementation commit.

Evidence предыдущего среза 4ec5132 (393 tests) не закрывает задачи 5.1–5.6.

## Evidence: stairs implementation (2026-09-09)

Tasks 5.1–5.5 implemented and verified. Full suite: 70 files / 414 tests passed, including physical routes, guided bot traversal, replay and legacy profiles. The final compatibility subset also passed (18 tests). Build, strict OpenSpec validation and diff check pass. Muted browser walkthrough uses shipping simulation/collision/renderer and captures both directions; large/5 Balanced cadence/lifecycle gate passes. See `docs/tasks/completed/14-multilevel-physical-navigation.md` and `artifacts/multilevel-navigation/stairs-*`. Task 5.6 remains open for manual input acceptance: normal Start explicitly reports denied pointer lock. Dedicated implementation commit does not authorize main integration, archive or deploy; older acceptance gates remain open.

## Archive authorization — 2026-09-09

Archived at the user's explicit request “архив коммит мерж” after they were informed of 19/26 completed tasks. Seven unchecked items above remain outstanding; archival does not certify manual pointer-lock input, autonomous combat coverage, all material/quality/split-screen gates or deployment. Code integrated into main at 9701127; all seven delta specifications synchronized and verified. Build and OpenSpec strict validation pass. Full 418-test run plus targeted reruns resolves its three failures; final collision/transition subset passed 17 tests and disabled-snap regression suite passed 10 tests. Browser large/5 Balanced reference gate passes. Detailed evidence: `docs/tasks/completed/14-multilevel-physical-navigation.md`. Deployment was not requested and was not performed.
