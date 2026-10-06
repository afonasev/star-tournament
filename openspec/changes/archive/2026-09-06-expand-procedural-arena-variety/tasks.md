## 1. Контракты и versioned profile

- [x] 1.1 Обновить `docs/GAME_SPEC.md` и change log утверждёнными recipe-картами, окнами, барьерами, нишами, ramps и visual direction; проверить согласованность с delta specs через `openspec validate expand-procedural-arena-variety --strict`.
- [x] 1.2 Расширить canonical `ArenaDefinition` versioned surface/elevation semantics и compatibility parsing, сохранив canonical order/content hash; проверить round-trip, invalid-mask и historical-definition tests.
- [x] 1.3 Добавить descriptor-backed recipe/elevation/ramp/barrier budgets в immutable generator profile и cross-field validation; проверить profile schema, descriptor coverage, range/step и hash tests.

## 2. Генерация и gameplay-проекции

- [x] 2.1 Реализовать seed-selected named layout recipes для каждого size с rooms, corridors, loops/courtyards и semantic attachments; проверить deterministic generation и variation across representative seeds.
- [x] 2.2 Скомпилировать малые elevation zones и двунаправленные ramps в canonical floors/links; проверить capsule traversal, отсутствие jump-only/cliff routes и 3D navigation waypoints.
- [x] 2.3 Скомпилировать окна, movement-only barriers и non-enterable wall-relief niche attachments; проверить capsule blocking, projectile pass/block filters и отсутствие reachable niche volumes.
- [x] 2.4 Расширить arena gameplay validation для recipe diversity, ramp reachability, masks, 3D route distance, spawn safety и FFA/team LOS fairness; проверить property-based seed matrix для всех размеров и recipes.

## 3. Collision, combat и AI

- [x] 3.1 Построить static collision world из canonical movement/projectile query masks в stable semantic-ID order; проверить checkpoint/reconstruction determinism и mask-specific fixture cases.
- [x] 3.2 Обновить hitscan occlusion, чтобы windows блокировали shots, а movement-only barriers — нет; проверить combat occlusion regressions и semantic-ID tie-break.
- [x] 3.3 Обновить navigation и bot planning для ramp route costs/waypoints; проверить, что bots проходят между elevation zones, не застревают у barriers и сохраняют deterministic replay.

## 4. Presentation и ресурсы

- [x] 4.1 Добавить renderer mapping semantic recipes/surfaces к аркам, окнам, нишам, barriers, ramps и большим room/corridor composition; проверить, что presentation не создаёт collision или false routes.
- [x] 4.2 Добавить compatible wall/floor/sector material variants, lamp fixtures и restrained local lights с quality-tier degradation; проверить texture budget, lifecycle/dispose, z-fighting и participant contrast.
- [x] 4.3 Подготовить необходимые local manifest-addressed GLB/texture assets с LOD и audit metadata; проверить manifest/source audit, отсутствие нового loader и renderer asset tests.

## 5. Интеграционная проверка

- [x] 5.1 Выполнить целевые unit/property/collision/combat/AI/renderer tests, `npm test`, build и deterministic replay checks; сохранить concise evidence по всем recipe/size/mask случаям.
- [x] 5.2 Выполнить required browser performance gate для quality tiers и затронутых viewport scenarios; проверить, что presentation budget не меняет simulation hashes.
- [x] 5.3 Провести muted in-app Browser playtest dev-стенда из отдельного worktree с keyboard/mouse: пройти рампы, столкнуться с каждым barrier height, стрелять через барьеры/ниши и в окна, оценить минимум два recipe; приложить актуальные screenshots каждого изменённого состояния.
- [x] 5.4 Проверить `git diff --check`, `openspec validate expand-procedural-arena-variety --strict`, отсутствие unrelated files, и подготовить один dedicated commit для проверенной change.

## Acceptance evidence (2026-09-06)

See `docs/qa/expand-procedural-arena-variety/README.md`. The initial in-app Browser limitation is recorded there; the user-authorized muted Chrome acceptance is now recorded in `repair/README.md`. No main integration or archival is authorized in this task.

## Повторная приёмка 2026-09-06

Пользователь сообщил о недостатке текстур и прохождении сквозь объекты. Повторно открыты renderer/collision alignment, material coverage и проверки. Исправить выход wall-bay за host solid, canonical proxies арок из общего GLB-контракта и вырожденные/растянутые UV. Старый QA-отчёт не доказывает исправление этих дефектов.

Исправление проверено: 59 файлов / 345 тестов, build, strict validation и пять performance gates PASS. Актуальные результаты: `docs/qa/expand-procedural-arena-variety/repair/README.md`. После user-authorized Chrome acceptance пункт 5.3 закрыт; in-app Browser limitation зафиксирован как ограничение среды автоматизации, а не gameplay defect.
