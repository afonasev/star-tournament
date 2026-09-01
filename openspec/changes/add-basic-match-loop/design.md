## Context

См. `proposal.md` — Why. Текущий runtime имеет один fixed-step `SimulationSnapshot` с одним player, одним weapon и тремя permanently inactive targets; browser shell стартует сразу в gameplay surface и использует pointer-lock controller как pause authority. `prototype-v1` уже содержит duration, assist, kill-chain и death numbers, но snapshot и UI их не потребляют. Renderer умеет один viewport и должен остаться presentation-only.

## Goals / Non-Goals

**Goals:**

- Сделать `MatchConfiguration` и match snapshot достаточными для deterministic replay, FFA/teams, statistics, timer, overtime и final standings.
- Сохранить один seat-backed player и переиспользовать три mannequin fixtures как неподвижных damageable participants без AI.
- Вывести menu/HUD/scoreboard/pause/results только из immutable projections simulation/session state.
- Сохранить zero-work pause/hidden/results и пройти существующий performance contract.

**Non-Goals:**

- Generalized multi-body player movement, attacking opponents, bot decisions, controller discovery, split-screen viewports и online authority.
- Универсальный safe/team spawn resolver: stationary fixtures возвращаются только в свои versioned anchors.
- Game Design Lab editor, persistence пользовательских match presets и полноценный audio subsystem.

## Decisions

### 1. `MatchConfiguration` отделена от `GameDesignProfile`

Создаётся immutable configuration schema v1 с identity/content hash: mode, ordered participant descriptors, local seat binding, duration minutes и nullable score target. Profile хранит допустимые ranges/defaults и gameplay tuning, но не конкретный выбор пользователя. Scenario хранит fixture anchors/hit volumes, но не выбранные имена, команды или цвета.

Так replay однозначно ссылается на profile + configuration + scenario, а изменение меню не меняет балансный профиль. Альтернатива — записать текущий выбор в profile — смешала бы immutable tuning с параметрами отдельного матча.

### 2. Playable snapshot schema v3 хранит весь authoritative match state

Snapshot заменяет singular `player`/`weapon`/`targets` на canonical arrays участников, lives и weapons, отсортированные по semantic id. `match` хранит configuration identity, phase, trigger, elapsed/remaining ticks и nullable final result. У каждого participant есть identity/type (`local-seat` или `stationary-fixture`), team/color, transform, health, life sequence, alive/dead timing, weapon, statistics, текущая kill-chain и ordered damage ledger.

Renderer получает local participant и mannequin transforms через projection. HUD получает отдельный immutable projection; neither может менять timer/statistics/result. Snapshot schema, replay schema и scenario identity повышаются совместно и отклоняют старые identities до step.

Альтернатива — оставить targets рядом с отдельным player — сохранила бы специальные ветки и не позволила бы одной scoring модели работать для будущих seats/bots.

### 3. Один tick состоит из фиксированных детерминированных фаз

Порядок шага:

1. validate configuration/profile/snapshot/action tick;
2. применить movement и fire живого seat-backed participant;
3. вычислить ordered pellet impacts и aggregate damage по victim life;
4. применить health, damage ledger и death transitions;
5. начислить assists, kills, deaths и kill-chain score;
6. выполнить due fixture respawns;
7. увеличить match clock для running/overtime;
8. после всех events вычислить individual/team totals и finish/overtime transition;
9. canonicalize events и snapshot.

Это делает одновременное достижение лимита атомарным. При score target на последнем regulation tick trigger сначала фиксируется как `score-limit`; tie переводит phase в overtime с сохранённым trigger. В overtime countdown остаётся нулевым, но ticks продолжаются до strict leader.

Альтернатива — заканчивать на первом kill event внутри tick — дала бы результат, зависящий от порядка массива hits.

### 4. Scoring хранит заработанные deltas, а не пересчитывает историю

Для серии хранятся current chain length, last kill tick и total already awarded. На новом kill simulation добавляет разницу между следующим cumulative total и предыдущим chain total; после пятого — configured increment. Разрыв gap или смерть сбрасывает только активную chain, не общий score. Assist ledger хранит последний positive damage tick каждого attacker для текущей victim life и очищается при смерти/respawn.

Это позволяет compact snapshot и однозначный replay. Альтернатива — хранить всю историю damage/kill events — раздула бы snapshot и runtime allocations.

### 5. Fixture participant lifecycle сознательно узкий

Три scenario mannequins получают participant ids, исходные anchors и hit volumes. Они не создают movement/fire actions и не collision-block игрока. После lethal damage жизнь не принимает hits; по завершении `killcamSeconds` создаётся новая life identity в собственном anchor с полными health/ammo. `corpseSeconds` управляет visual corpse lifetime отдельно от respawn.

Это даёт повторяемые kills и scoring, не имитируя AI. General safe spawn, team spawn и all-anchors-occupied fallback остаются будущим capability.

### 6. Browser shell владеет session lifecycle, но не match outcome

React shell имеет surfaces `menu | match | results` и создаёт/disposing один `MatchSession`. Внутри match pointer-lock controller управляет `ready/locked/paused/error`; simulation phase управляет `running/overtime/finished`. При finished runtime синхронно stop, clear input и показывает final projection. Repeat dispose старую session и создаёт новую с той же exact configuration/profile/scenario; menu dispose без hidden resume state.

`Tab` и `Escape` обрабатываются keyboard adapter как локальный `UiActionSnapshot`, отдельный от action schema v2. `Tab` управляет visibility scoreboard, `Escape` вызывает pause, когда browser доставляет event; pointer-lock loss остаётся независимым authority fallback. Gamepad API не читается.

### 7. UI projections имеют стабильную cadence и immediate transitions

Periodic gameplay HUD остаётся ограничен `presentation-balanced-v1`. Scoreboard получает ту же cadence во время удержания `Tab`; открытие/закрытие, pause и finished публикуются немедленно как lifecycle invalidation. В menu/paused/results нет periodic publications. Remaining time форматируется из simulation ticks; team totals и sorting уже приходят из projector с semantic-id tie-break.

DOM surfaces остаются полноэкранными поверх одного canvas, не создают второй viewport/camera и не меняют renderer submission topology. Performance driver расширяется фазами settled menu, live scoreboard, pause, overtime/results и проверяет существующие hard counters; 10-minute soak не требуется, поскольку renderer/assets/entity count не растут, но короткий production-browser gate обязателен из-за simulation/HUD/lifecycle изменений.

### 8. Profile evolution

`GameDesignProfile` schema/revision повышается. Match target metadata представлены numeric fields `rules.match.scoreTarget.defaultPoints`, `minimumPoints`, `maximumPoints`, `stepPoints`; defaults 3000/1000/20000/100. Existing duration/scoring/death fields сохраняются и получают полное match-loop requirement coverage. Cross-field validation проверяет ordering и то, что default совместим со step от minimum.

Конкретное `scoreTargetPoints: null | number` остаётся в `MatchConfiguration`; `null` означает выключенную цель.

## Risks / Trade-offs

- [Stationary opponents не могут убить local player, поэтому часть statistics не возникает физически] → Покрыть incoming damage/death/assist multi-attacker cases headless deterministic fixtures и честно отметить browser slice как one-sided combat.
- [Переход с singular player/targets на participant arrays затрагивает много тестов] → Ввести migration одним schema bump, сначала pure validation/projectors, затем step/runtime; старые данные fail fast.
- [Overtime теоретически может длиться бесконечно, если score не меняется] → Это утверждённое отсутствие ничьи; runtime продолжает матч без скрытого timeout, а paused/menu остаются доступны.
- [Fixture respawn может визуально пересечь local capsule] → Mannequins остаются non-colliding и используют отдельные anchors; универсальный occupied-anchor resolver не заявляется.
- [Scoreboard может увеличить React work] → Публиковать не чаще presentation profile cadence, не рендерить скрытую таблицу и включить live-scoreboard phase в performance gate.

## Migration Plan

1. Повысить profile, scenario, snapshot и replay identities; добавить configuration contract и fail-fast parsing.
2. Перевести combat/movement на canonical participant arrays и внедрить deterministic match reducer.
3. Добавить UI projections и browser shell transitions, сохраняя старый debug mode.
4. Обновить tests/performance driver, выполнить strict validation, production build, in-app Browser playtest и screenshots.
5. Rollback выполняется откатом единственного change-коммита; persisted compatible user data ещё отсутствует, поэтому data migration не нужна.
