# match-session-lifecycle Specification

## Purpose

Capability задаёт детерминированный контракт конфигурации, хода и завершения локального FFA либо командного матча, включая participant lifecycle, статистику, scoring и overtime.

## Requirements

### Requirement: Versioned конфигурация матча
Матч SHALL запускаться только из валидной сериализуемой конфигурации с identity, roster от двух до восьми уникальных участников, одним seat-backed keyboard/mouse участником в browser либо без local seats в явно выбранном headless harness, режимом `ffa` либо `teams`, длительностью и nullable целью по очкам. FFA SHALL хранить индивидуальный participant color, назначенный из расширяемой vetted palette; `teams` MUST назначать каждого участника ровно в `team-a` либо `team-b`, содержать обе непустые команды и хранить одну контрастную team-color пару для Team A и Team B. Цвета и user-facing names MUST NOT использовать legacy identities `red` либо `blue`.

#### Scenario: Валидный FFA
- **WHEN** меню создаёт FFA с одним local seat, хотя бы одним ботом (либо stationary mannequin в diagnostic scenario), допустимыми попарно различимыми цветами, длительностью и выключенной целью
- **THEN** симуляция принимает immutable configuration и создаёт initial snapshot с тем же roster и identity

#### Scenario: Валидный командный матч
- **WHEN** roster содержит непустые Team A и Team B, одного seat-backed участника, ботов (либо stationary mannequin в diagnostic scenario) и допустимую контрастную team-color пару
- **THEN** configuration принимается, а цвет каждого участника выводится из его команды

#### Scenario: Недоступный participant contract
- **WHEN** configuration запрашивает второй local seat, gamepad, online participant, пустую команду, повторяющийся id либо число участников вне 2–8
- **THEN** validation отклоняет матч до первого tick со стабильным path и не подменяет участника mannequin без явной конфигурации

Боты SHALL хранить обязательный difficulty id `easy / normal / hard`, входящий в configuration identity и snapshot. Browser roster SHALL состоять из одного человека и 1–7 ботов; stationary fixtures сохраняются для явных diagnostic scenarios.

#### Scenario: Боты разных уровней
- **WHEN** валидный browser roster содержит ботов трёх уровней
- **THEN** configuration принимается и сохраняет отдельную сложность каждого

#### Scenario: Неверная сложность
- **WHEN** bot difficulty отсутствует или неизвестна
- **THEN** validation отклоняет configuration со стабильным path без default подмены

#### Scenario: Headless roster
- **WHEN** evaluation harness явно запускает валидные 2–8 bot-only участников
- **THEN** симуляция принимает состав без фиктивного человека, а browser setup продолжает требовать один local seat

### Requirement: Детерминированные фазы и таймер
Simulation MUST владеть фазой `running`, `overtime` либо `finished`, elapsed/remaining ticks и finish reason; wall-clock, DOM и renderer MUST NOT менять время матча. Основное время SHALL содержать ровно `durationMinutes * 60 * simulationTicksPerSecond` активных ticks; paused/menu/finished runtime не выполняет gameplay ticks.

#### Scenario: Основное время
- **WHEN** матч без цели выполняет последний tick заданной длительности и имеет единоличного лидера
- **THEN** snapshot становится `finished` на этом tick с причиной `time-limit` и фиксированным результатом

#### Scenario: Пауза
- **WHEN** runtime остановлен pause lifecycle на несколько секунд wall-clock
- **THEN** match phase, elapsed ticks, remaining ticks, scores и state hash не меняются

#### Scenario: Replay с другой render cadence
- **WHEN** два runner применяют одинаковые configuration/profile/arena/actions при разной render cadence
- **THEN** phase, timer, events, result и каждый state hash совпадают

### Requirement: Participant life, death и fixture respawn
Каждый участник SHALL иметь versioned life identity, health, alive/dead state и statistics. В этом single-seat slice stationary mannequin не двигается и не атакует; после lethal damage он проходит profile-defined death/killcam delay и возрождается с новой life identity, полным health и ammo в своём versioned fixture anchor. Этот контракт MUST NOT объявляться bot AI либо универсальной spawn policy.

#### Scenario: Убийство mannequin
- **WHEN** local participant наносит stationary mannequin lethal damage
- **THEN** victim один раз становится dead, получает death, killer получает kill, corpse/death timing начинается, а новые hits не наносят damage этой жизни

#### Scenario: Fixture respawn
- **WHEN** profile-defined respawn delay завершён
- **THEN** stationary mannequin получает новую life identity, полные health/ammo и исходный fixture transform, оставаясь неподвижным и не создавая attack actions

#### Scenario: Gamepad не создаёт участника
- **WHEN** gamepad подключается или отключается во время матча
- **THEN** roster, participant state, teams, viewport и simulation не меняются

### Requirement: Damage ledger и статистика
Simulation SHALL атомарно учитывать для каждого участника kills, assists, deaths, damage dealt, damage received и score. Assist MUST начисляться каждому не-убийце, который наносил victim положительный damage в profile-defined окне до lethal tick; один участник получает не более одного assist за смерть независимо от числа hits.

#### Scenario: Damage statistics
- **WHEN** attacker наносит victim положительный урон
- **THEN** damage dealt attacker и damage received victim увеличиваются на одну и ту же величину без округления, зависящего от порядка events

#### Scenario: Assist в окне
- **WHEN** один участник наносит damage, а другой убивает victim не позднее profile-defined assist window
- **THEN** первый получает один assist и profile-defined assist points, а killer assist не получает

#### Scenario: Assist вне окна или в своей смерти
- **WHEN** последний damage участника старше окна либо участник является killer или victim
- **THEN** assist и assist points ему не начисляются

### Requirement: Kill-chain scoring
Kill score SHALL вычисляться из profile-defined cumulative totals для непрерывной серии убийств, где gap между соседними lethal ticks не превышает maximum gap; каждое убийство после пятого SHALL добавлять post-five increment. Смерть MUST завершать текущую серию.

#### Scenario: Серия до пяти убийств
- **WHEN** участник делает последовательные kills внутри допустимого gap
- **THEN** его kill-derived score после каждого убийства равен соответствующему cumulative total профиля, а общий score дополнительно включает assists

#### Scenario: Разрыв серии
- **WHEN** следующий kill происходит после maximum gap либо участник умирает
- **THEN** новый kill начинает новую серию с первого cumulative total без удаления ранее заработанных очков

#### Scenario: Шестое убийство
- **WHEN** серия продолжается после пятого kill
- **THEN** каждый следующий kill увеличивает ранее заработанный kill-derived score на post-five increment

### Requirement: Победитель, командный итог и overtime без ничьей
После полного применения events каждого tick simulation SHALL вычислять FFA ranking по individual score либо team ranking по сумме scores участников. Активная цель SHALL завершать матч досрочно только при единоличном лидере среди достигших лимита; если лидеров несколько, phase становится `overtime`. На последнем tick основного времени единоличный лидер завершает матч, а равенство запускает overtime. Overtime MUST закончиться на первом последующем tick с единоличным лидером и MUST NOT иметь дополнительного лимита времени.

#### Scenario: Единоличный FFA лидер достигает цели
- **WHEN** после атомарного tick один игрок имеет наибольший score не ниже цели
- **THEN** матч завершается с ним как winner и причиной `score-limit`

#### Scenario: Командный лимит
- **WHEN** сумма scores одной команды достигает цели и строго больше суммы другой
- **THEN** матч завершается победой этой команды; индивидуальный score сам по себе командный матч не завершает

#### Scenario: Одновременное достижение с разными totals
- **WHEN** несколько игроков либо обе команды пересекают лимит в одном tick, но один итог строго больше
- **THEN** полный tick учитывается и матч завершается победой наибольшего итога

#### Scenario: Равенство на лимите или по времени
- **WHEN** после score-limit tick либо последнего tick основного времени максимальный итог разделяют несколько contenders
- **THEN** матч переходит в `overtime`, не объявляет ничью и сохраняет все scores/state

#### Scenario: Завершение overtime
- **WHEN** после последующего overtime tick появляется единоличный лидер
- **THEN** матч немедленно завершается с этим winner и исходной причиной trigger (`score-limit` либо `time-limit`)

#### Scenario: Лимит на последнем tick времени
- **WHEN** score limit впервые достигнут на последнем tick основного времени
- **THEN** finish trigger фиксируется как `score-limit`, включая случай перехода в overtime

### Requirement: Immutable результат и повтор матча
Finished snapshot SHALL содержать immutable ordered standings, winner identity, finish trigger и final tick. Gameplay actions после finish MUST NOT менять snapshot. Repeat SHALL создать новый initial snapshot из той же exact configuration, profile, arena/scenario identity и seed policy, обнулив timer/statistics/lives; exit SHALL уничтожить match runtime и вернуть menu без скрытого продолжения.

#### Scenario: Actions после finish
- **WHEN** runner получает movement/fire actions после `finished`
- **THEN** gameplay state и final result не меняются

#### Scenario: Повтор
- **WHEN** пользователь выбирает повтор после результата или из pause menu
- **THEN** новый матч использует ту же configuration/profile identity, начинается с initial timer/statistics и не продолжает старые life ids

### Requirement: Arena-bound match session
Match session SHALL владеть exact validated `ArenaDefinition`, выбранным size и resolved seed вместе с configuration/profile identities. Initial participant transforms и stationary fixture respawn transforms MUST назначаться из arena spawn slots, а combat scenario SHALL владеть только opponent/hit-volume contract.

#### Scenario: Initial snapshot
- **WHEN** accepted arena запускается с валидным roster
- **THEN** каждый participant получает distinct свободный arena slot, FFA opponents и противостоящие команды удовлетворяют profile-defined minimum separation, союзники могут занимать соседние slots, а local-seat capsule свободна до первого input tick

#### Scenario: Fixture respawn
- **WHEN** stationary participant завершает death delay
- **THEN** simulation детерминированно выбирает допустимый arena slot и создаёт новую life без обращения к scenario position

### Requirement: Живые participants блокируют движение
Перед каждым movement query collision adapter SHALL проецировать из simulation state капсулы всех живых participants. Капсула движущегося participant MUST сталкиваться с другими живыми participant capsules без проталкивания stationary fixtures. Неживой participant MUST быть исключён из collision queries до respawn.

#### Scenario: Контакт с живым participant
- **WHEN** local-seat движется в сторону живого stationary participant
- **THEN** character controller останавливается либо скользит по его капсуле и не проходит сквозь неё

#### Scenario: Смерть и respawn
- **WHEN** participant умирает, а затем получает новую life
- **THEN** его capsule перестаёт блокировать движение после смерти и снова проецируется в respawn position до следующего movement query

### Requirement: Repeat сохраняет arena, новый матч разрешает новый seed
Repeat SHALL повторно использовать exact definition, size, seed, generator/profile identities и configuration. Новый матч с auto seed SHALL разрешать новое concrete значение; новый матч с ручным seed SHALL использовать введённое значение.

#### Scenario: Repeat
- **WHEN** пользователь выбирает Repeat из pause либо results
- **THEN** новый initial snapshot имеет прежнюю arena identity/hash и сброшенные match/life state

#### Scenario: Новый auto-seed матч
- **WHEN** пользователь выходит в setup и снова запускает матч с пустым seed
- **THEN** shell создаёт новый resolved seed и новую arena identity без изменения предыдущего replay

### Requirement: Общий tick управляемых участников
Simulation SHALL обрабатывать actions каждого живого управляемого участника один раз за общий tick, затем атомарно применять combat events и проверять окончание матча. Решения AI SHALL получать одно исходное состояние tick; события и collision queries MUST иметь стабильный порядок. AI dead state не создаёт movement/fire, а respawn SHALL очищать память прежней жизни и выбирать свободный arena spawn по общей policy.

#### Scenario: Взаимный lethal fire
- **WHEN** два живых на combat stage участника делают допустимые смертельные выстрелы в одном tick
- **THEN** оба выстрела разрешаются из единого combat state, обе смерти учитываются до определения победителя

#### Scenario: Пауза AI
- **WHEN** browser находится в pause, hidden либо finished state
- **THEN** AI timers, RNG, actions и simulation state не продвигаются

#### Scenario: Возрождение бота
- **WHEN** завершён death delay
- **THEN** бот получает новую life и валидный свободный arena slot, сохраняя сложность и statistics и сбрасывая устаревшую боевую память
