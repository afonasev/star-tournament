# Unity bot behavior — текущий проверяемый срез

`add-unity-bot-planner` соединяет frozen participant roster и difficulty с `NativeBotMatchDriver`. Один observation pass до всех действий; human direct reports исключены. Planner получает собственные pose/life, copied knowledge и ally identity/position, без session/скрытых enemy states. Все выстрелы/движение исполняет общий `NativeCombatSession`.

`unity-bot-behavior-v1@1` переносит behavioral tuning трёх сложностей. Search использует semantic anchors обоих этажей; engage ограничивает реакцию/прицел и выдаёт отдельные press/release ticks. Retreat/support конечны и имеют cooldown. Local strafe требует общей опоры и безопасной прямой области; active transition сохраняет exit при потере цели и bounded retry. Прыжок прогнозирует текущую velocity и frozen fixed-step motor integration; airborne world direction удерживается до landing. Native physics остаётся authority, не обещается bitwise replay.

Planner snapshot включает timers/RNG/target life/intent и navigation DTO; restore проверяет deadline bounds, вложенный clock и weapon identity. Pause/results не вызывают driver, Repeat создаёт driver заново с frozen seed/profiles/composition. Start/Repeat применяют frozen simulation cadence.

Development `-botBehaviorReview -botBehaviorEvidence <dir>` исполняет самостоятельный AI (seed20260920), `-botControls` подключает настоящий keyboard/mouse viewport против семи ботов. Все world и first-person тела используют сохранённые seven-clips trooper v2 через `TrooperVisual`; AI не получает отдельной animation path. FHD/4K focused muted Player pilot и keyboard/mouse smoke завершены; это не shipping add/remove/difficulty setup и не доказывает статистическое превосходство уровней либо target60FPS.

---

# Unity participant composition — проверенный внутренний срез

`add-unity-participant-roster` отделяет immutable metadata/mapping от combat roster: 2–8 participants и 1–4 local views. Human actions адресуются через seat→participant; fixtures не получают devices/cameras, unsupported bot source блокирует запуск. `NativeMatchComposition` хранит kind/name/resolved vetted color/difficulty и независимый snapshot; он не определяет полный replay.

Scene/life/corpse работают для N участников, camera/arms/HUD для H людей. Development `-participantReview -participantEvidence <dir>` показывает 1/2/3/4 views и восемь stationary fixtures/humans; `-participantControls` позволяет проверить один keyboard/mouse viewport с семью явно помеченными fixtures (F9 сохраняет state/PNG). Это ещё не playable AI. Shipping human setup остаётся2–4; bot planner/setup/evaluation следуют по общей матрице.

Профиль `unity-native-roster-v1@1` задаёт budgets initial candidate generation и search nodes. Distinct physical slots для FFA/teams сохраняют opponent separation. Превышение budgets возвращает явную ошибку без частичной активации. Repeat сохраняет metadata/mapping/profiles; неверный draft возвращает safe preview и остаётся исправляемым.

Проверки:66/66 EditMode,49/49 PlayMode, Mac build,18 focused/muted состояний FHD и4K. Evidence: `evidence/unity-participant-roster-2026-09-20/README.md`; handoff26. Physical input/TV и long performance остаются открытыми.

---

# Unity bot navigation — проверенный внутренний срез

`add-unity-bot-navigation` добавляет исполнение статической либо честно наблюдаемой/запомненной цели через `NativeBotNavigation → LocalAction → CharacterMotor`. `NativeNavigationProvider` использует NavMesh как источник кандидата и независимо проверяет принадлежность физической геометрии/опор текущей арене, headroom и declared transitions. Global query не означает эксклюзивного выбора одного NavMeshData; ограничения необходимо пересмотреть для разных generated scenes.

Follower требует actual support/grounded и высоту, хранит выбранный выход stairs/ramp при смене цели и restore. Промежуточные ступени не являются остановками; торможение применяется к конечной точке. Застревание и recovery budget не сбрасываются непрерывным обновлением видимой цели. Выход — обычное боковое/обратное движение с коллизиями и ограничением попыток, затем явный Blocked.

Профиль `unity-bot-navigation-v1@1` имеет общие descriptor metadata UI/validation. Snapshot содержит намерение, маршрут/курсор, active transition/exit, progress/deadlines и attempts; restore валидирует и перепроверяет native route, не обещая полного Unity replay.

Development review `-botNavigationReview -navigationEvidence <absolute-directory>` подключает **реальный session tick**: Observe → Sample → action → один Session.Tick. Пауза не вызывает цепочку; смерть очищает цель, Repeat создаёт новый driver с frozen profiles, menu освобождает его. Journey проверяет stairs/ramp, hidden memory/expiry и lifecycle. Это не shipping setup ботов и не полный combat planner.

Полная цель и оставшиеся разрывы: `UNITY_MIGRATION_MATRIX.md`; handoff: `tasks/completed/25-unity-bot-navigation.md`. Playable planner/weapon/support, три полных difficulty, shipping mixed roster, solo versus bots и evaluation остаются обязательными следующими changes.

---

# Unity bot perception foundation

`add-unity-bot-perception` is an internal prerequisite for playable native bots, based on native human teams `4521a7d`. It does not add bots to setup or drive participants.

- `NativeBotObservationProvider` is the sole raw-world adapter. It samples the existing `NativeCombatSession` and its PhysicsScene, with horizontal FOV and a ray from movement eye height to the combat torso anchor. Static WorldLayer occludes; movement-only barriers and participant bodies do not occlude perception. This does not change projectile blocking by allies. One torso sample is an explicit initial sensor model, not a promise to detect every exposed limb.
- `NativeBotPerception` consumes filtered direct sightings and each observer's own life/alive status. The future planner receives a copied per-observer `NativeBotKnowledge`, not a session or the aggregate diagnostic snapshot. Invisible current position, death and respawn never refresh old knowledge. This perception component does not plan navigation, aim or fire; the navigation adapter described above consumes its copied knowledge.
- Team reports contain the original direct sighting, observation time and recipient life. Delivery is delayed; it cannot refresh the timestamp, overwrite newer knowledge or rebroadcast. One pending report per receiver/source/target bounds the queue without postponing delivery under continuous sight.
- `unity-bot-perception-v1@1` includes only the three difficulties' FOV/memory and shared communication delay, with the existing Inspector/validation descriptor registry. Defaults come from `src/profiles/botRules.ts`; combat stats are untouched.
- Snapshot v1 copies perception state and pending reports, validates the exact roster/difficulty/profile configuration, and resumes the same observation stream. It is not a full match save, Unity replay schema, or cross-platform deterministic replay.
- Call sampling once per advancing simulation observation time. No sample during pause means no memory aging or delivery. Own death/new life clears old knowledge and incoming reports. A new match gets a new perception instance. The ordinary Player does not instantiate or tick this foundation yet.

Development Player review: `-botPerceptionReview -botEvidence <absolute-directory>` produces muted FHD/4K PNG/JSON for direct/hidden/remembered/expired/reacquired states, delayed team sharing, pause, Repeat and FFA isolation. It uses scripted placements and an explicit diagnostic observation clock; screenshots show human bodies, not playable bots. The overhead microbenchmark uses actual scene queries but is not a full AI match or a 60 FPS acceptance test.

The remaining bot slices must connect the verified navigation/recovery capability to shipping planner behavior, strafe/jumps, weapon policy, temporary support, three difficulty behaviors, setup/roster, evaluation and native human-versus-bot journeys. Playable solo remains open; eight scene participants are verified by the composition slice above. Real gamepads/TV and long target-hardware performance remain separate gates.

---

# Historical browser Bot AI v1

The remainder describes the retained browser reference; it is not evidence of native bot delivery.


AI produces ordinary action frames. `stepPlayableSimulation` moves every living
participant and resolves all shooters against the same pre-damage combat state;
the reducer applies canonical damage, scoring, deaths and respawns. Humans and
bots share capsules, health, movement, weapon damage and cooldown. Ammo reaching
zero refills from `rules.weapons.doubleBarrelShotgun.emptyRefillAmmo`.

## Boundaries and extensions

- `src/ai/planner.ts`: `ObservationProvider` supplies FOV/LOS-limited sightings,
  timestamped memories and delayed allied reports. Never give a goal evaluator
  the raw enemy snapshot. Rear-damage scanning uses only the bot's own health.
- `GoalEvaluator` returns id, position and utility. Register additional evaluators
  for future pickups/objectives through `BotExtensions.goals`; the test goal
  demonstrates integration without changing movement/combat.
- `WeaponPolicy` supplies an aim point and readiness decision by weapon semantic
  id. Register future weapon policies through `BotExtensions.weapons`; actual
  firing, cooldown, ammo and damage remain authoritative combat rules.
- `NavigationCapabilities` provides destinations, capsule clearance and routes.
  The current derived graph supports the shipped single-level arenas; a future
  multilayer/jump-link implementation can replace it without renderer imports.
- `BotState` contains a separately seeded RNG, memories, intention, path,
  personality and tick deadlines. It is versioned and serialized. No wall clock,
  `Math.random`, DOM, renderer object or Rapier handle belongs in bot state.
- Defaults and numeric metadata live together in `src/profiles/botRules.ts` and
  the full immutable `prototype-v1` revision 6. No weapon/health/speed advantage
  is granted by difficulty. Actual stance and timing vary within the profile.
- `float32-transcendentals-v1` normalizes platform math before it can diverge
  across browser/Node replay. It is a numerical compatibility invariant, not a
  designer-controlled parameter.

## Headless commands

```sh
npm run ai:simulate -- --seed 101 --levels easy,normal,hard --mode ffa --size small --output test-results/ai-smoke
npm run ai:simulate -- --seed 103 --levels easy,hard,normal,hard --mode teams --size medium
npm run ai:evaluate -- --output test-results/ai-evaluation
npm run ai:simulate -- --replay test-results/ai-smoke/replay.json
```

The runner uses the shipped Rapier backend and the same fixed tick as the
browser. It does not approximate combat with a separate duel formula. Output
includes identities, timings/speedup, damage/score, contact/movement/jump/support
metrics and checkpoint hashes. `--ticks` is a safety budget, not an automatic
winner. Timeout/stall is never silently counted as a completed game. A replay
can be stopped at an exact tick with `--ticks`; `--output` then saves its snapshot.

`ai:evaluate` writes its suite and jobs before running them. It uses mirrored
participant identities/spawn sides, 200 games per neighboring difficulty pair,
Wilson confidence intervals, and FFA/team scenarios with 2/4/8 participants on
all three arena sizes. Small `--seeds` runs are pilots, not acceptance. Bug seeds
10036/10060 are regressions; final heldouts start at 20001. Never tune on a final
holdout and then call the same range independent.

## Browser QA

Use `?muted=1` for normal physical playtesting. The existing diagnostic query
`?muted=1&performanceBenchmark=1` supplies synthetic human commands, while bots
make real decisions. It is not evidence of working keyboard/mouse pointer lock.
After completion, the playfield exposes `data-ai-replay`, `data-ai-final-hash`,
`data-ai-final-snapshot`, sampled `data-ai-checkpoints`, and the performance
report. Export the full replay, run it through `ai:simulate --replay`, and compare
the exact final hash. Presentation-only timing does not enter replay identity.

For a full one-minute diagnostic match, `performanceSoakSeconds=60` can be used
with the one-minute setup duration. Do not measure an already-finished match as
running work: a 65-second window around a 60-second match intentionally fails
the running-cadence gate because the final seconds correctly perform no work.

Acceptance still requires normal in-app Browser move/look/jump/fire,
pause/resume, killcam, roster/results and Repeat checks with audio muted.
