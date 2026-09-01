## 1. Profile, actions и playable snapshot

- [x] 1.1 Повысить schema/revision `prototype-v1`, добавить все capsule/movement/camera/shotgun fields из design и полные descriptor metadata; проверить coverage, range/step и cross-field validation unit-тестами без дублирующих gameplay constants.
- [x] 1.2 Расширить canonical action contract стабильными `move-forward`, `move-right`, `look-yaw`, `look-pitch`, `jump`, `fire`; проверить exact keys/ranges, ordering, transition-friendly pressed state и hash round-trip unit-тестами.
- [x] 1.3 Создать versioned `combat-slice-scenario-v1` с semantic-ID-sorted robot targets/zoned hit volumes и content hash; проверить schema, geometry validation, reordered input normalization и immutability.
- [x] 1.4 Заменить diagnostic snapshot на versioned playable snapshot с player/weapon/targets/current-tick events и collision/profile identities; проверить initial state, canonical serialize/parse/hash, unsupported schema/identity и replay fixtures.
- [x] 1.5 Расширить architecture audit: simulation не импортирует Rapier/renderer/DOM/input devices/wall-clock, input не меняет snapshot, renderer/UI не вызывают gameplay mutations; проверить positive и forbidden fixtures.

## 2. Movement и collision-backed runner

- [x] 2.1 Реализовать pure horizontal wish-direction, acceleration/deceleration, gravity, jump edge и yaw/pitch clamp из profile; проверить ground/air speed, no-input stop, held jump, pitch boundaries и frame-rate independence unit-тестами.
- [x] 2.2 Параметризовать Rapier capsule controller принятыми profile KCC values и включить configuration identity в checkpoint; проверить exact configuration, independent reconstruction, lifecycle и несовместимую identity.
- [x] 2.3 Реализовать `stepPlayableSimulation` через backend-neutral collision port с projection до tick и velocity из разрешённого displacement; проверить floor/landing, wall slide, corner, ceiling, low/high step, slope, ledge и tunnelling integration-тестами.
- [x] 2.4 Реализовать headless playable runner/replay с одинаковыми per-tick hashes при двух независимых runs, restore и observer cadence 0/1/2/4; проверить movement/look/jump sequence минимум на 10 000 fixed ticks.

## 3. Shotgun combat

- [x] 3.1 Реализовать versioned deterministic pellet pattern из life/shot/tick и first-person basis; проверить pellet count, normalized directions, spread bounds, одинаковый seed и различающийся shot sequence unit-тестами.
- [x] 3.2 Реализовать ordered analytic ray intersections с head sphere, torso box и limb capsules; проверить nearest zone, misses, range boundary, equal-distance semantic tie и inactive-target filtering.
- [x] 3.3 Соединить target intersections со static collision occlusion и canonical pellet ordering; проверить cover-before-target, target-before-cover и backend callback order independence.
- [x] 3.4 Реализовать ammo, rising-edge fire, cooldown, dry fire, zoned pellet damage, mixed reduction и target inactive transition/events; проверить 20 shots/no reload, head kill, torso/limb damage, mixed hit, hold/cooldown и post-destroy immunity.
- [x] 3.5 Добавить deterministic combat replay evidence; проверить совпадение shot directions, ordered hits, ammo, target health/events и hashes в независимых/restore runs.

## 4. Keyboard/mouse, renderer и HUD

- [x] 4.1 Реализовать single-seat keyboard/mouse adapter с held WASD/Space, accumulated mouse delta и primary fire; проверить canonical frames, per-tick delta drain, key repeat, pointer-lock gating и atomic clear на blur/hidden/unlock.
- [x] 4.2 Реализовать pointer-lock controller/start-resume lifecycle без зависимости от гарантированного `Escape`; проверить request success/failure, stop до lock, pause/clear при потере lock, explicit resume и отсутствие чтения gamepad API component-тестами.
- [x] 4.3 Заменить flyover renderer на first-person camera из snapshot/profile, arena + primitive robot targets и transient muzzle/impact/destroyed presentation; проверить camera transform/FOV/resize, target state, event expiry и полный dispose unit/component-тестами.
- [x] 4.4 Реализовать immutable gameplay UI snapshot и compact React HUD: health слева, shotgun/ammo справа, crosshair по центру, hit/dry feedback, start/resume overlay и optional `?debug=1`; проверить DOM semantics, default playfield clearance и debug identity component-тестами.
- [x] 4.5 Подключить playable runner/input/renderer/HUD к async browser bootstrap, сохранив collision/profile/WebGL error surfaces и pagehide cleanup; проверить no ticks before lock, start/pause/resume, fixed-step actions, renderer observers и idempotent disposal integration-тестами.

## 5. Проверка и поставка slice

- [x] 5.1 Выполнить `npm run typecheck`, `npm test`, `npm run build`, architecture audits, `git diff --check` и `openspec validate add-first-person-combat-slice --strict`; сохранить pass/fail и bundle/performance warnings без маскировки.
- [x] 5.2 Запустить dev server из change worktree, получить фактический Vite URL и подтвердить `200 OK` HTTP-запросом.
- [x] 5.3 Провести обязательный in-app Browser playtest: click-to-lock, physical WASD/strafe, mouse look, jump/landing, fire/ammo, hit/destroy, lock loss/resume, resize, default/debug HUD и console; сохранить screenshots и state/DOM/canvas evidence, не подменяя physical input одними synthetic tests.
- [x] 5.4 По фактическому browser outcome обновить `verification.md`, `GAME_SPEC` §§2–3, 5–8, §10 и журнал; явно отметить single-seat/mannequin ограничения и не заявлять готовность gamepad/split-screen/bots/match/network.
- [x] 5.5 После outcome-правок повторить automated checks, strict validation и browser smoke; проверить согласованность proposal/specs/design/tasks перед одним change-коммитом.

## 6. Performance и power regression gate

- [x] 6.1 Добавить immutable `presentation-balanced-v1` и descriptor registry для backing-buffer pixel budget и maximum HUD publication cadence; проверить path/group/label/description/unit/minimum/maximum/step, range/step validation, stable identity и отсутствие presentation profile в playable snapshot/replay/hash unit-тестами.
- [x] 6.2 Реализовать pixel-budget resolution, стандартный WebGL power preference и render-on-new-tick/explicit-invalidation lifecycle; проверить Retina/4K scale cases, отсутствие duplicate WebGL submissions, ровно один redraw после paused resize, hidden/pause zero-work и dispose component/integration-тестами.
- [x] 6.3 Убрать per-tick React publication, использовать profile-defined periodic cadence и immediate lifecycle/error publications; проверить не более shipped budget при running state, отсутствие periodic commits на pause/hidden и bounded health/ammo/feedback latency integration-тестами.
- [x] 6.4 Добавить presentation-only performance probe и versioned `performance-reference-v1` для pause, running-idle, movement/jump и combat burst; проверить simulation/WebGL/HUD counters, timings, renderer info, unavailable optional metrics и неизменность snapshot/replay/hash automated-тестами.
- [x] 6.5 Добавить `npm run perf:browser` и `docs/PERFORMANCE_GATES.md` с production-preview protocol, trigger/exemption matrix, hard/provisional budgets, environment evidence, screenshots и 10-minute thermal soak; проверить машинно-читаемый pass/fail отчёт и документацию тестом/CLI smoke.
- [x] 6.6 Выполнить typecheck/tests/build/architecture audit/diff check/strict OpenSpec validation, затем production-preview browser performance прогон и default/debug/performance screenshots; обновить `verification.md`, `GAME_SPEC` §8/§10 и журнал фактическими результатами, не подменяя оставшийся physical input gate synthetic driver.
