## Context

Мотивация и границы описаны в proposal.md. GAME_SPEC §§2, 3, 5–8 содержит согласованные решения. `ActionFrame` уже адресует participantId, но `playableStep` выбирает local participant и fixture targets, а `MatchSetupMenu` привязан к фиксированному roster. `FixedStepHeadlessRunner` использует прежний diagnostic step: для AI evaluation нужен adapter к актуальному playable tick и реальному collision world. Arena navigation projection уже содержит regions/links; robot animations существуют как derived presentation.

## Goals / Non-Goals

**Goals:** общий tick для человека/ботов, проверяемая observation boundary, сериализуемый AI, навигация по действительной геометрии и один engine для browser/headless/replay. Создать полный planning slice, а затем реализовать и проверить его отдельной apply фазой в этом же worktree.

**Non-Goals:** новые transport/model-training/physics зависимости, расширенные устройства/viewport и полный editor Balance Lab. Sound cues, новые pickups и оружия добавляются последующими capabilities, без фиктивных доступных объектов в текущем AI.

## Decisions

### Общий simulation tick

Внешние human actions валидируются по доступным local seats. AI вычисляет actions из immutable start-of-tick observations; внешние actions для bot slots отклоняются. Сначала разрешаются movements в stable participant-ID order с актуализацией capsule projections после перемещения каждого участника; затем фиксируется единый post-movement combat state. Все допустимые shots живых на этой стадии участников собираются до damage reduction. Damage применяется в canonical shooter/pellet order с ограничением фактическим оставшимся health; death/assist/score считаются ровно один раз, затем respawn и match finish. Это разрешает взаимные убийства и не позволяет первому обработанному shot отменить уже допустимый ответный выстрел. Stable movement order может давать небольшой приоритет в узких проходах; paired spawn/identity permutations в evaluation измеряют эффект.

Физика, hit volumes и оружие работают для любого participant kind по актуальным transforms. AI выбирает только противников; existing friendly-hit policy проверяется по исходному combat contract и не меняется этим планом. Бот старается не стрелять при союзнике в линии огня. Проверить local damage/killcam, shooter-specific events, target lists и renderer projection — они сейчас содержат single-player/fixture допущения.

### Наблюдения, память и решения

`BotObservation` содержит tick, self, известную статическую navigation map, доступные allies и видимые enemies; visibility строится FOV + static LOS. Логику решений тестировать без доступа к полному snapshot. Memory хранит last-seen position/tick/lifeId и истекает; скрытое движение не обновляет память. Командные сообщения содержат только реальные last-seen observations с timestamp и профильной задержкой. Новая жизнь врага не наследует точную позицию старой. Произвольные global combat events не раскрывают скрытые координаты.

Выбор намерения по оценке полезности с hysteresis и bounded commitment: поиск/исследование → подход → атака → короткий отход либо поддержка. Он сочетается с небольшими состояниями locomotion/aim/fire. Эта комбинация позволяет добавить новую цель через registry без большого дерева условий в tick; полный behavior-tree framework для первого среза избыточен.

`BotState` хранит per-bot RNG, memory, intent, route progress, strafe direction/deadline, jump cooldown, aim error/response timers и ограниченные индивидуальные склонности. Начальные RNG выводятся из match seed и participant identity; consumed random не зависит от render cadence/количества наблюдателей. Stateless derivations не сериализуются, их cache безопасно пересоздаётся по arena/profile identity. AI version входит в replay compatibility.

### Навигация и движение

Маршрут выбирается по regions/links с детерминированным tie-break; локальные waypoints/clearance проверяются по canonical collision solids с capsule radius, включая большие architectural panels. Одних центров regions недостаточно для проходов: waypoint refinement проверяет прямые сегменты и локальные обходы. Навигация учитывает живые capsules для локального уступания, но не перестраивает всю карту при каждом шаге. Отсутствие прогресса запускает bounded recovery/repath и смену intent, без телепортации.

Combat steering совмещает желательную weapon distance, strafe, obstacle clearance и separation от союзников. Прыжок имеет проверку препятствия/боевой ситуации, clearance приземления, cooldown и budget; смена направления strafe имеет длительность, чтобы исключить дрожание на каждом tick. Низкий health повышает вес короткого отхода; конечный commitment возвращает бота к поиску боя. Обход/временная поддержка не требуют постоянного лидера.

### Расширение контента и профилей

Разделить `ObservationProvider`, `GoalEvaluator`, `NavigationCapabilities` и `WeaponPolicy`. Первая weapon policy описывает дробовик: эффективную дистанцию, LOS, прицеливание и readiness, а фактические shots исполняет общий combat. Будущие цели бонусов и типы оружия подключаются через semantic IDs и capabilities; contract tests используют тестовые реализации без shipping новых объектов.

`rules.bots.{easy,normal,hard}` содержит reaction/decision cadence, FOV/memory, aim response/error, combat distance preferences, strafe intervals, jump cooldown/budget, retreat duration и cooperation weights; общие поля `rules.bots.navigation`/`cooperation` задают clearance/repath/stuck/separation/support limits. Каждый numeric leaf получает descriptor metadata и cross-field validation. Скорость перемещения, урон и weapon cooldown читаются только из общего player/weapon profile. Числа AI калибруются в apply фазе и фиксируются до независимого evaluation набора.

`rules.weapons.doubleBarrelShotgun.emptyRefillAmmo` имеет shipped значение 20, unit ammo, integer bounds 1–100 и step 1. Последний допустимый shot расходует патрон и сразу пополняет остаток; валидный zero-ammo state нормализуется перед trigger. Операция не сбрасывает cooldown/shotSequence/pressed state и не выдаёт дополнительный выстрел. Refill применяется ко всем вооружённым участникам, включая humans; respawn по-прежнему даёт общий стартовый ammo. GAME_SPEC хранит назначение и profile name, shipped число указано в журнале/контракте и затем в конфигурации.

### Setup, результаты и lifecycle

Roster становится массивом entries со stable semanticId; новые боты получают normal по умолчанию, независимые имена/цвета/сложности. Default setup сохраняет размер текущего состава: один человек и три бота. Удаление строки не переносит сложность на соседнюю. В teams назначение команды редактируется отдельно; пустая команда блокирует старт. Base nickname и difficulty хранятся отдельно, display formatter используется live/results и winner label; длинные имена не разрушают сетку. Fixtures доступны только diagnostic/test scenarios.

Browser сохраняет один local-seat viewport. Добавление бота не создаёт camera/viewport/device slot. WASD/look/jump/fire/Tab и явный pointer-lock start/resume сохраняются; pause/hidden/finished останавливают все AI вместе с simulation. Подключение gamepad не меняет roster. Bot-only запуск поддерживает core/harness configuration без localParticipant assumptions; пользовательский browser setup требует одного человека. Observer replay для QA допускается как диагностическое представление и не считается physical playtest.

### Ускоренные проверки и критерии

CLI `ai:simulate` и `ai:evaluate` запускают shared playable simulation с реальным collision, фиксированным delta без RAF/wait, явными seed/roster/profile/suite/tickBudget. JSON summary содержит исходы, metadata и метрики; replay bundle хранит validated arena один раз, initial state, внешние frames, AI version и checkpoint hashes. Полная telemetry может быть выборочной, но не меняет simulation. Не хранить все огромные tick snapshots в памяти; streaming/report aggregation ограничивает overhead. Измерять скорость отдельно от hashing/report I/O.

Версионный `ai-evaluation-v1` фиксирует calibration и независимые evaluation seeds до настройки. Pairwise easy-normal и normal-hard: минимум 100 paired seeds на соседнюю пару, смена spawn/identity и команды; lower bound 95% Wilson interval побед более сильного уровня среди завершённых игр должен превышать 0.5. Любой timeout/stall в acceptance наборе — FAIL, не исключённый удобный результат. Дополнительно смешанные FFA/teams на small/medium/large с 2/4/8 участниками и детерминированные fixtures для поддержки, распада группы, strafe, grounded интервалов, обхода и recovery. Behavioral thresholds читаются из versioned suite/profile; явные failure cases обязательны. Недостаточная выборка — inconclusive.

Determinism: повторный full run, checkpoint restore в новом мире и browser/headless parity fixture дают одинаковые hashes/actions/events. Browser acceptance проверяет lobby, смешанные уровни, human controls, combat, killcam/respawn, live/results, Repeat, pause/hidden/resume; muted PNG каждого изменённого состояния сохраняются с seed/profile/revision. Выполнить existing collision/performance gates с полной нагрузкой AI и восьмью participants. Физическое управление подтверждается устойчивой сессией in-app Browser.

## Risks / Trade-offs

- [Граф regions не описывает локальный обход panel geometry] → clearance refinement, real-world route fixtures и seed sweep.
- [Прицеливание hard превращается в мгновенный aimbot] → reaction/turn limits, continuous aim error и browser human playtest.
- [Случайная активность скрывает застревание] → измерять реальный прогресс/контакт и маршрут, а не только наличие actions.
- [Команда скапливается в дверях] → separation, bounded support commitment, уступание и timeout recovery.
- [Headless PASS не доказывает читаемость/играбельность] → отдельная browser acceptance и performance evidence.
- [Старые snapshot assumptions и schema versions] → explicit version bump, reject fixtures и восстановление в новом world; не ослаблять parsers.

## Migration Plan

План хранится в `codex/add-extensible-bot-ai`, worktree `/private/tmp/star-tournament-bot-ai`, handoff `docs/tasks/10-add-extensible-bot-ai.md`. Planning checkpoint коммитится отдельно; feature implementation остаётся одним последующим feature-коммитом. После реализации и acceptance последовательно проверить актуальный main/status/worktrees, интегрировать, архивировать OpenSpec отдельным worktree/коммитом и выполнить согласованный deployment workflow. До реализации main продолжает прежнее поведение. Rollback выполняется отдельным revert feature commit с восстановлением согласованных schema/profile identities; непроверенные snapshots не мигрируют молча.
