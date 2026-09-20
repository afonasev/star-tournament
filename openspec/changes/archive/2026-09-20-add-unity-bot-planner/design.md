## Context

База9f207d2: perception/navigation/2–8 participants проверены, AI action source отсутствует. Мотивация в proposal.md; GAME_SPEC §2,3,5,7 утверждает механику. Read-only Astra memo получен2026-09-20; implementation scope остаётся отдельным worktree/commit.

## Goals / Non-Goals

**Goals:** data-only planner + policy, единственный native raw-world driver, полный настоящий бой в opt-in Player. Frozen inputs, bounded physics queries, сохранение common combat.

**Non-Goals:** новый AI пакет/ML, shipping setup в этом change, статистическое доказательство сложности, generated arenas, full replay/online/distribution.

## Decisions

- Astra сравнил wholesale browser port, native policies и behavior-tree package. Выбран небольшой native planner: browser служит источником profile defaults, не runtime-контрактом.
- `NativeBotMatchDriver`: сначала общий observation frame, исключение human direct-report sources, один Perception.Sample; затем copied own pose/life и knowledge каждого бота плюс allowlisted allies; все actions до одного Session.Tick. Planner не получает session/LiveTargets/aggregate knowledge.
- `NativeBotPlanner` владеет intention/target+life/decision/strafe/jump/retreat/support deadlines, independent RNG и shot edge. Static semantic anchors покрывают оба этажа. Hidden memory пригодна для pursuit, только direct visible для fire. Reaction запускается новой известной target life, а не каждым sighting.
- `NativeShotgunPolicy` получает observed feet/общую capsule geometry и собственный ammo/cooldown. Bounded yaw/pitch и smooth profile error; реальные release ticks. Последнее слово за Session resolver и allied blocking.
- Tactical environment проверяет capsule support/headroom/короткое движение и conservative ballistic landing. Приоритет transition/recovery выше tactical; suspended follower не накапливает ложный stuck timeout. Candidate count ограничен небольшим объявленным набором и cadence.
- Support выбирает временную боковую позицию возле собственного известного боя союзника. Allies дают только identity/position/alive, не enemy knowledge. Humans не получают фиктивную difficulty/report FOV. Support и retreat имеют конечный срок и cooldown.
- `unity-bot-behavior-v1@1`: все новые numeric tuning через descriptor registry. Existing perception/navigation остаются отдельными immutable profiles. RNG seed driver обязателен, не wall clock/Unity Random. Core snapshot проверяет version/config/finite ranges атомарно; полная Unity restore не заявляется.
- Pause/results не вызывают driver; resume release-safe. Death/new life очищает transient intent. Repeat создаёт новый driver с frozen composition/profiles/seed. Local input/focus/disconnect/layout сохраняются.

## Risks / Trade-offs

- False stuck при strafe → явное arbitration и regression.
- Скрытые сведения через team support → own copied knowledge и allowlisted allied poses; paired hidden-world contract test.
- Capsule/landing query approximation → conservative same-support rejection, actual CharacterMotor test; никаких teleport/падений между этажами как recovery.
- Eight bots cost → bounded anchors/query cadence, metrics per tick and Player long diagnostic; не объявлять target60FPS по короткому sample.
- Поведение сложности не равно доказанной силе → directed timing tests сейчас, separate paired evaluation/holdouts позже.

## Migration Plan

Реализация за opt-in development entry; human setup сохраняется. Проверить EditMode/PlayMode, native Player FHD/4K и реальные AI shots/damage. Отдельный commit, затем shipping setup change. Rollback: убрать opt-in driver hookup; существующий human runtime остаётся основой.

## Review corrections

Astra code review выявил transition loss при истечении памяти, local strafe против соседнего этажа, вечный Blocked pursuit, несовпадение predicted jump с motor и слабые snapshot guards. Исправления: ForgetEnemy сохраняет обязательный выход; tactical control требует общей опоры и прямого безопасного пространства; Blocked retry ограничен cadence существующего stuck+recovery окна; jump capability принимает текущие pose/velocity и проверяет ту же дискретную ground/air acceleration/vertical integration, steering фиксируется в world direction до landing. `jumpSamples` ограничивает число fixed-step physics проб, не coarse sampling. Snapshot валидирует temporal bounds/target-life/nested clock и включает weapon capability identity. Эти исправления получают отдельные regressions до финальной Player QA.

Unity API reference: [CapsuleCast](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsScene.CapsuleCast.html), [OverlapCapsule](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PhysicsScene.OverlapCapsule.html); version6000.3 проверена2026-09-20.

Повторный review: отдельный RetryBlockedRoute сохраняет active exit даже после Blocked; search не заменяет этот выход. Создание и Repeat применяют frozen simulation cadence, чтобы jump predictor и actual motor использовали один fixed step.
